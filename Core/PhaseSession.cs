using System;
using System.Collections.Generic;

namespace CrocGame.Core;

/// <summary>One bout in the contest: who you face, for how long, and how hard.</summary>
public sealed record MatchDef(OpponentDef Opponent, float DurationSeconds, int DifficultyOffset);

/// <summary>
/// One act of a bout: a belt, a clock, three teeth, and everything the player is
/// carrying while they last.
///
/// A phase does not know it is part of a bout. It is handed the carried scoreline each
/// frame - which is all Hunger needs to know how far behind the player is - and reports
/// what happened. BoutSession does the joining up.
/// </summary>
public sealed class PhaseSession
{
    private readonly Belt _belt;
    private readonly SpawnDirector _director;
    private readonly JawZone _jaw;
    private readonly PhaseDef _phase;
    private readonly int _difficultyOffset;
    private readonly List<GameEvent> _events = new();

    private float _grace;
    private float _secondsUntilCoin;

    public PhaseSession(FoodTable table, IRandomSource rng, JawZone jaw,
                        float spawnX, float retireX, PhaseDef phase, int difficultyOffset)
    {
        _jaw = jaw;
        _phase = phase;
        _difficultyOffset = difficultyOffset;
        _belt = new Belt(retireX);
        _director = new SpawnDirector(table, rng, spawnX);

        State = new MatchState(phase.DurationSeconds);
        Frenzy = new Frenzy();
        Pot = new Pot();
        Hunger = new Hunger();
        Buffs = new ActiveBuffs();

        _secondsUntilCoin = phase.CoinIntervalSeconds;
    }

    public MatchState State { get; }
    public Frenzy Frenzy { get; }
    public Pot Pot { get; }
    public Hunger Hunger { get; }
    public ActiveBuffs Buffs { get; }
    public PhaseDef Phase => _phase;
    public IReadOnlyList<FoodItem> Items => _belt.Items;

    /// <summary>Points scored in this phase alone.</summary>
    public int PhaseScore => State.Score;

    public bool KnockedOut { get; private set; }

    /// <summary>
    /// A bite that lands buys this long of forgiveness. A player who taps twice on one
    /// item - which is what hands do when a bite feels good - should not be charged a
    /// strike for the follow-through.
    /// </summary>
    public const float ChompGraceSeconds = 0.18f;

    /// <summary>Belt speed right now, including every multiplier acting on it.</summary>
    public float BeltSpeed =>
        Difficulty.ForEaten(State.Eaten + _difficultyOffset).BeltSpeed
        * Frenzy.SpeedMultiplier * Buffs.SpeedMultiplier * Hunger.SpeedMultiplier;

    /// <summary>
    /// The window as it is actually judged - and the width the view is required to draw.
    /// Nothing may widen this without the player seeing it widen.
    /// </summary>
    public JawZone EffectiveJaw => _jaw with { HalfWidth = _jaw.HalfWidth * Hunger.JawMultiplier };

    /// <summary>During Hunger the croc eats anything, bombs included.</summary>
    public bool IsEdibleNow(FoodItem item) => item.IsEdible || Hunger.IsActive;

    /// <summary>Puts an item straight on the belt. Used by tests and by coin spawning.</summary>
    public void Place(FoodItem item) => _belt.Add(item);

    public IReadOnlyList<GameEvent> Tick(float dt, int carriedPlayerScore, int opponentScore)
    {
        _events.Clear();
        if (State.IsOver) return _events;

        if (_grace > 0f) _grace = MathF.Max(0f, _grace - dt);

        if (Frenzy.Tick(dt)) _events.Add(new FrenzyEnded());
        if (Hunger.Tick(dt)) _events.Add(new HungerEnded());

        foreach (var kind in Buffs.Tick(dt)) _events.Add(new BuffExpired(kind));

        Hunger.Update(dt, carriedPlayerScore + State.Score, opponentScore);
        if (Hunger.TryFire())
        {
            _events.Add(new HungerStarted(Hunger.JawMultiplier, Hunger.DurationSeconds));
        }

        if (!KnockedOut)
        {
            AdvanceBelt(dt);
            SpawnCoin(dt);

            var spawned = _director.Tick(dt, State.Eaten + _difficultyOffset, _phase);
            if (spawned is not null)
            {
                _belt.Add(spawned);
                _events.Add(new Spawned(spawned));
            }
        }

        // The clock is settled last so the final frame's bites still count.
        if (State.AdvanceClock(dt)) State.Finish();

        return _events;
    }

    private void AdvanceBelt(float dt)
    {
        foreach (var retired in _belt.Advance(BeltSpeed, dt))
        {
            // Correct play: hazards are supposed to ride past, and a coin declined is
            // the whole point of the coin.
            if (!retired.IsEdible || retired.Power != "") continue;

            // Missing food costs the streak and the points, never a strike.
            _events.Add(new Passed(retired));
            State.BreakCombo();
            Frenzy.Reset();
        }
    }

    private void SpawnCoin(float dt)
    {
        if (_phase.CoinIntervalSeconds <= 0f) return;

        _secondsUntilCoin -= dt;
        if (_secondsUntilCoin > 0f) return;

        _secondsUntilCoin = _phase.CoinIntervalSeconds;

        // No pot, no wager, no coin. A coin worth nothing is a strike waiting to happen.
        if (Pot.IsEmpty) return;

        var coin = _director.MakeCoin(halfWidth: 8f);
        _belt.Add(coin);
        _events.Add(new CoinSpawned(coin, Pot.PendingAt(State.Combo)));
    }

    public IReadOnlyList<GameEvent> Chomp(int carriedPlayerScore)
    {
        _events.Clear();
        if (State.IsOver || KnockedOut) return _events;

        var result = ChompJudge.Judge(EffectiveJaw, _belt.Items);

        // The magnet suspends judging for three bites: it takes whatever is nearest.
        if (result.Outcome == ChompOutcome.Air && Buffs.MagnetBitesRemaining > 0
            && _belt.Items.Count > 0 && Buffs.ConsumeMagnetBite())
        {
            result = new ChompResult(ChompOutcome.Hit, Nearest());
        }

        if (result.Outcome == ChompOutcome.Air)
        {
            if (_grace > 0f) return _events;   // follow-through of a bite that landed

            _events.Add(new ChompedAir());
            AddStrike();
            return _events;
        }

        var item = result.Item!;
        _belt.Remove(item);

        if (item.Power == "coin")
        {
            BankPot();
            return _events;
        }

        if (!IsEdibleNow(item))
        {
            _events.Add(new Chomped(item, 0, 0, false));
            AddStrike();
            return _events;
        }

        _grace = ChompGraceSeconds;

        var buff = PowerUp.Parse(item.Power);
        if (buff is not null)
        {
            Buffs.Take(buff.Value);
            _events.Add(new BuffTaken(buff.Value));
            return _events;
        }

        ScoreBite(item);
        return _events;
    }

    private FoodItem Nearest()
    {
        var best = _belt.Items[0];
        var bestDistance = float.MaxValue;

        foreach (var item in _belt.Items)
        {
            var distance = MathF.Abs(item.X - _jaw.Center);
            if (distance >= bestDistance) continue;

            best = item;
            bestDistance = distance;
        }

        return best;
    }

    private void ScoreBite(FoodItem item)
    {
        var wasFrenzied = Frenzy.IsActive;
        var multiplier = Frenzy.Multiplier * Buffs.ScoreMultiplier * _phase.ScoreMultiplier;
        var points = State.RegisterHit(item.Score, multiplier);

        // The pot is upside stacked on the score, never a slice taken out of it.
        if (_phase.CoinIntervalSeconds > 0f) Pot.Add(points);

        _events.Add(new Chomped(item, State.Combo, points, wasFrenzied));

        if (!wasFrenzied && State.Combo >= Frenzy.ComboToTrigger)
        {
            Frenzy.Trigger();
            _events.Add(new FrenzyStarted());
        }
        else if (wasFrenzied)
        {
            Frenzy.Trigger();  // sustained play keeps the frenzy alive
        }
    }

    private void BankPot()
    {
        var amount = Pot.Amount;
        var multiplier = Pot.MultiplierForCombo(State.Combo);
        var paid = Pot.Bank(State.Combo);

        State.AddScore(paid);
        _events.Add(new PotBanked(amount, multiplier, paid));
    }

    private void AddStrike()
    {
        Hunger.OnStrike();

        if (Buffs.ConsumeShield())
        {
            _events.Add(new BuffExpired(BuffKind.Shield));
            return;
        }

        if (!Pot.IsEmpty)
        {
            _events.Add(new PotWiped(Pot.PendingAt(State.Combo)));
            Pot.Wipe();
        }

        State.RegisterStrike();
        Frenzy.Reset();
        _events.Add(new StrikeAdded(State.Strikes));

        if (State.Strikes < MatchState.MaxStrikes) return;

        KnockedOut = true;
        _belt.Clear();
        _events.Add(new PhaseKnockout(0, State.TimeRemaining));
    }
}
