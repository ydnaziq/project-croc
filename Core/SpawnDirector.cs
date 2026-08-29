using System.Collections.Generic;

namespace CrocGame.Core;

/// <summary>
/// Decides when the next item appears and what it is. All randomness comes through
/// IRandomSource, so a seed reproduces a run exactly.
/// </summary>
public sealed class SpawnDirector
{
    private readonly FoodTable _table;
    private readonly IRandomSource _rng;
    private readonly float _spawnX;

    private float _secondsUntilNext;
    private int _nextId = 1;
    private int _clusterRemaining;

    public SpawnDirector(FoodTable table, IRandomSource rng, float spawnX)
    {
        _table = table;
        _rng = rng;
        _spawnX = spawnX;
        _secondsUntilNext = Difficulty.ForEaten(0).SpacingMax;
    }

    /// <summary>
    /// Type ids forced to spawn next, in order. This is how a buff arrives guarded:
    /// the guards and the prize are queued together so they cannot be separated by a
    /// random gap, and the player reads the whole shape as one decision.
    /// </summary>
    private readonly Queue<string> _forced = new();

    /// <summary>
    /// How often a spawn opportunity becomes a buff instead of food. Low on purpose:
    /// a buff has to feel like something that turned up, not like part of the pace.
    /// </summary>
    private const float PowerUpChance = 0.07f;

    /// <summary>Returns an item to place on the belt, or null if it is not time yet.</summary>
    public FoodItem? Tick(float dt, int eaten, PhaseDef phase)
    {
        _secondsUntilNext -= dt;
        if (_secondsUntilNext > 0f) return null;

        var difficulty = Difficulty.ForEaten(eaten);
        ScheduleNext(difficulty);

        if (_forced.Count > 0)
        {
            // A queued spawn is back-to-back with the one before it, so a guarded buff
            // reads as a cluster rather than as three unrelated items.
            _secondsUntilNext = difficulty.SpacingMin * 0.42f;

            var forcedType = _table.ById(_forced.Dequeue());
            if (forcedType is not null) return Make(forcedType, difficulty);
        }

        if (phase.PowerUpsEnabled && _rng.NextFloat() < PowerUpChance)
        {
            var buffs = _table.PowerUps(eaten);
            if (buffs.Count > 0) return OpenBuffSpawn(PickWeighted(buffs), difficulty);
        }

        var hazardChance = difficulty.InedibleChance * phase.HazardScale;
        var wantInedible = hazardChance > 0f && _rng.NextFloat() < hazardChance;

        var pool = _table.Available(eaten, edible: !wantInedible);
        if (pool.Count == 0)
        {
            pool = _table.Available(eaten, edible: true);
            if (pool.Count == 0) return null;
        }

        return Make(PickWeighted(pool), difficulty);
    }

    /// <summary>
    /// Places a buff, with the guards its strength has to be paid for. The rule is that
    /// buff strength is inversely proportional to window width and the strongest ones
    /// spawn guarded - so SLOW arrives alone and free, and a GOLD TOOTH has to be taken
    /// out from between two bombs.
    /// </summary>
    private FoodItem OpenBuffSpawn(FoodType buff, Difficulty difficulty)
    {
        var guard = _table.ById("bomb");
        if (guard is null || buff.Power == "slow") return Make(buff, difficulty);

        _forced.Enqueue(buff.Id);
        if (buff.Power == "goldtooth") _forced.Enqueue(guard.Id);

        _secondsUntilNext = difficulty.SpacingMin * 0.42f;
        return Make(guard, difficulty);
    }

    private FoodItem Make(FoodType type, Difficulty difficulty) =>
        new(id: _nextId++,
            typeId: type.Id,
            x: _spawnX,
            halfWidth: type.Width / 2f,
            isEdible: type.Edible,
            score: type.Score,
            movement: SelectMovement(type, difficulty),
            power: type.Power);

    /// <summary>
    /// A cash-out coin. Its value is decided by the pot rather than by the food table,
    /// so it is built here only to keep item ids unique across the whole belt.
    /// </summary>
    public FoodItem MakeCoin(float halfWidth) =>
        new(id: _nextId++,
            typeId: "coin",
            x: _spawnX,
            halfWidth: halfWidth,
            isEdible: true,
            score: 0,
            movement: Movement.Constant,
            power: "coin");

    private float Spacing(Difficulty d) => d.SpacingMin + (d.SpacingMax - d.SpacingMin) * _rng.NextFloat();

    /// <summary>
    /// Decides when the item after this one arrives. Most of the time that is an
    /// ordinary jittered gap, but occasionally it opens a burst: three items nearly
    /// back to back, which the player has to take as one fast sequence. Bursts are the
    /// main source of moment-to-moment variety - a stream of evenly spaced food is the
    /// same press over and over however fast it moves.
    /// </summary>
    private void ScheduleNext(Difficulty d)
    {
        if (_clusterRemaining > 0)
        {
            _clusterRemaining--;
            _secondsUntilNext = d.SpacingMin * 0.42f;
            return;
        }

        if (d.ClusterChance > 0f && _rng.NextFloat() < d.ClusterChance)
        {
            _clusterRemaining = 2;
            _secondsUntilNext = d.SpacingMin * 0.42f;
            return;
        }

        _secondsUntilNext = Spacing(d);
    }

    /// <summary>Weighted pick, so rare items can be genuinely rare.</summary>
    private FoodType PickWeighted(IReadOnlyList<FoodType> candidates)
    {
        var total = 0;
        foreach (var c in candidates) total += c.Weight < 1 ? 1 : c.Weight;

        var roll = _rng.NextInt(total);

        foreach (var c in candidates)
        {
            roll -= c.Weight < 1 ? 1 : c.Weight;
            if (roll < 0) return c;
        }

        return candidates[^1];
    }

    /// <summary>
    /// The type's declared movement is a default. As difficulty rises, the director
    /// substitutes a harder strategy.
    /// </summary>
    private IMovement SelectMovement(FoodType type, Difficulty d)
    {
        var roll = _rng.NextFloat();

        if (roll < d.StutterWeight) return Movement.Stutter;
        if (roll < d.StutterWeight + d.BounceWeight) return Movement.Bounce;

        return Movement.ByName(type.Movement);
    }
}
