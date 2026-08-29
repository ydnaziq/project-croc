using System;
using System.Collections.Generic;

namespace CrocGame.Core;

/// <summary>One bout in the contest: who you face, for how long, and how hard.</summary>
public sealed record MatchDef(OpponentDef Opponent, float DurationSeconds, int DifficultyOffset);

/// <summary>
/// The only surface the scene layer touches. Time arrives as dt; input arrives as
/// Chomp(). Everything that happened comes back as events.
/// </summary>
public sealed class MatchSession
{
    private readonly Belt _belt;
    private readonly SpawnDirector _director;
    private readonly OpponentEater _opponent;
    private readonly JawZone _jaw;
    private readonly MatchDef _def;
    private readonly List<GameEvent> _events = new();

    public MatchSession(FoodTable table, IRandomSource rng, JawZone jaw,
                        float spawnX, float retireX, MatchDef def)
    {
        _jaw = jaw;
        _def = def;
        _belt = new Belt(retireX);
        _director = new SpawnDirector(table, rng, spawnX);
        _opponent = new OpponentEater(def.Opponent, rng);
        State = new MatchState(def.DurationSeconds);
        Frenzy = new Frenzy();
    }

    public MatchState State { get; }
    public Frenzy Frenzy { get; }
    public MatchDef Def => _def;
    public int OpponentScore => _opponent.Score;
    public IReadOnlyList<FoodItem> Items => _belt.Items;

    /// <summary>Belt speed right now, including the frenzy boost. Drives the conveyor.</summary>
    public float BeltSpeed =>
        Difficulty.ForEaten(State.Eaten + _def.DifficultyOffset).BeltSpeed * Frenzy.SpeedMultiplier;

    public IReadOnlyList<GameEvent> Tick(float dt)
    {
        _events.Clear();
        if (State.IsOver) return _events;

        if (_grace > 0f) _grace = MathF.Max(0f, _grace - dt);

        if (Frenzy.Tick(dt)) _events.Add(new FrenzyEnded());

        if (_opponent.Tick(dt)) _events.Add(new OpponentAte(_opponent.Score));

        foreach (var retired in _belt.Advance(BeltSpeed, dt))
        {
            if (!retired.IsEdible) continue;  // correct play: inedibles should pass

            // Missing food costs the streak and the points, never a strike. Strikes
            // are for mistakes the player made - biting nothing, or biting a bomb.
            _events.Add(new Passed(retired));
            State.BreakCombo();
            Frenzy.Reset();
        }

        var spawned = _director.Tick(dt, State.Eaten + _def.DifficultyOffset);
        if (spawned is not null)
        {
            _belt.Add(spawned);
            _events.Add(new Spawned(spawned));
        }

        // The clock is settled last so the final frame's bites still count.
        if (State.AdvanceClock(dt))
        {
            State.Settle(_opponent.Score);
            _events.Add(EndEvent());
        }

        return _events;
    }

    /// <summary>
    /// A bite that lands buys this long of forgiveness. A player who taps twice on one
    /// item - which is what hands do when a bite feels good - should not be charged a
    /// strike for the follow-through.
    /// </summary>
    public const float ChompGraceSeconds = 0.18f;

    private float _grace;

    public IReadOnlyList<GameEvent> Chomp()
    {
        _events.Clear();
        if (State.IsOver) return _events;

        var result = ChompJudge.Judge(_jaw, _belt.Items);

        if (result.Outcome == ChompOutcome.Air)
        {
            if (_grace > 0f) return _events;   // follow-through of a bite that landed

            _events.Add(new ChompedAir());
            AddStrike();
            return _events;
        }

        var item = result.Item!;
        _belt.Remove(item);

        if (!item.IsEdible)
        {
            _events.Add(new Chomped(item, 0, 0, false));
            AddStrike();
            return _events;
        }

        _grace = ChompGraceSeconds;

        var wasFrenzied = Frenzy.IsActive;
        var points = State.RegisterHit(item.Score, Frenzy.Multiplier);
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

        return _events;
    }

    private void AddStrike()
    {
        State.RegisterStrike();
        Frenzy.Reset();
        _events.Add(new StrikeAdded(State.Strikes));

        if (State.IsOver) _events.Add(EndEvent());
    }

    private MatchEnded EndEvent()
    {
        var won = State.Result == MatchResult.Won;
        return new MatchEnded(
            State.Result,
            State.Score,
            _opponent.Score,
            Prize: won ? _def.Opponent.PrizeMoney : 0,
            BestCombo: State.BestCombo,
            Eaten: State.Eaten);
    }
}
