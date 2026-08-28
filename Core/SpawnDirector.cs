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

    public SpawnDirector(FoodTable table, IRandomSource rng, float spawnX)
    {
        _table = table;
        _rng = rng;
        _spawnX = spawnX;
        _secondsUntilNext = Difficulty.ForEaten(0).SpacingMax;
    }

    /// <summary>Returns an item to place on the belt, or null if it is not time yet.</summary>
    public FoodItem? Tick(float dt, int eaten)
    {
        _secondsUntilNext -= dt;
        if (_secondsUntilNext > 0f) return null;

        var difficulty = Difficulty.ForEaten(eaten);
        _secondsUntilNext = Spacing(difficulty);

        var wantInedible = difficulty.InedibleChance > 0f
                           && _rng.NextFloat() < difficulty.InedibleChance;

        var candidates = _table.Available(eaten, edible: !wantInedible);
        if (candidates.Count == 0)
        {
            candidates = _table.Available(eaten, edible: true);
            if (candidates.Count == 0) return null;
        }

        var type = candidates[_rng.NextInt(candidates.Count)];

        return new FoodItem(
            id: _nextId++,
            typeId: type.Id,
            x: _spawnX,
            halfWidth: type.Width / 2f,
            isEdible: type.Edible,
            score: type.Score,
            movement: SelectMovement(type, difficulty));
    }

    private float Spacing(Difficulty d) => d.SpacingMin + (d.SpacingMax - d.SpacingMin) * _rng.NextFloat();

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
