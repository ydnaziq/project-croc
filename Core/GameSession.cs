using System.Collections.Generic;

namespace CrocGame.Core;

/// <summary>
/// The only surface the scene layer touches. Time arrives as dt; input arrives as
/// Chomp(). Everything that happened comes back as events.
/// </summary>
public sealed class GameSession
{
    private readonly Belt _belt;
    private readonly SpawnDirector _director;
    private readonly JawZone _jaw;
    private readonly List<GameEvent> _events = new();

    public GameSession(FoodTable table, IRandomSource rng, JawZone jaw, float spawnX, float retireX)
    {
        _jaw = jaw;
        _belt = new Belt(retireX);
        _director = new SpawnDirector(table, rng, spawnX);
        State = new RunState();
    }

    public RunState State { get; }

    public IReadOnlyList<FoodItem> Items => _belt.Items;

    public IReadOnlyList<GameEvent> Tick(float dt)
    {
        _events.Clear();
        if (State.IsOver) return _events;

        State.AddElapsed(dt);

        var difficulty = Difficulty.ForEaten(State.Eaten);

        foreach (var retired in _belt.Advance(difficulty.BeltSpeed, dt))
        {
            if (!retired.IsEdible) continue;  // correct play: inedibles should pass

            _events.Add(new Passed(retired));
            AddStrike();
            if (State.IsOver) return _events;
        }

        var spawned = _director.Tick(dt, State.Eaten);
        if (spawned is not null)
        {
            _belt.Add(spawned);
            _events.Add(new Spawned(spawned));
        }

        return _events;
    }

    public IReadOnlyList<GameEvent> Chomp()
    {
        _events.Clear();
        if (State.IsOver) return _events;

        var result = ChompJudge.Judge(_jaw, _belt.Items);

        if (result.Outcome == ChompOutcome.Air)
        {
            _events.Add(new ChompedAir());
            AddStrike();
            return _events;
        }

        var item = result.Item!;
        _belt.Remove(item);

        if (item.IsEdible)
        {
            var before = State.Score;
            State.RegisterHit(item.Score);
            _events.Add(new Chomped(item, State.Combo, State.Score - before));
        }
        else
        {
            _events.Add(new Chomped(item, 0, 0));
            AddStrike();
        }

        return _events;
    }

    private void AddStrike()
    {
        State.RegisterStrike();
        _events.Add(new StrikeAdded(State.Strikes));

        if (State.IsOver) _events.Add(new RunEnded(State.Score, State.Eaten));
    }
}
