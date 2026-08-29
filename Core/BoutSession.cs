using System.Collections.Generic;

namespace CrocGame.Core;

/// <summary>
/// A whole bout: three phases, a rival who eats through all of them, and one carried
/// scoreline. This is the only surface the scene layer talks to.
///
/// The rival lives here rather than in a phase because their score has to survive a
/// phase boundary and has to keep climbing through a knockout - conceding seconds is
/// what makes a third strike hurt now that it can no longer end the run.
/// </summary>
public sealed class BoutSession
{
    private readonly FoodTable _table;
    private readonly IRandomSource _rng;
    private readonly JawZone _jaw;
    private readonly float _spawnX;
    private readonly float _retireX;
    private readonly MatchDef _def;
    private readonly IReadOnlyList<PhaseDef> _phases;
    private readonly OpponentEater _opponent;
    private readonly List<GameEvent> _events = new();

    private PhaseSession? _current;
    private int _carriedScore;
    private int _carriedEaten;

    /// <summary>
    /// True between folding a finished phase into the carried totals and opening the
    /// next one. Without it PlayerScore would count the closing phase twice.
    /// </summary>
    private bool _phaseFolded = true;

    public BoutSession(FoodTable table, IRandomSource rng, JawZone jaw,
                       float spawnX, float retireX, MatchDef def, IReadOnlyList<PhaseDef> phases)
    {
        _table = table;
        _rng = rng;
        _jaw = jaw;
        _spawnX = spawnX;
        _retireX = retireX;
        _def = def;
        _phases = phases;
        _opponent = new OpponentEater(def.Opponent, rng);
    }

    public int PhaseIndex { get; private set; }
    public PhaseDef Phase => _phases[PhaseIndex];
    public PhaseSession Current => _current!;
    public MatchDef Def => _def;
    public BoutResult Result { get; private set; } = BoutResult.InProgress;
    public int OpponentScore => _opponent.Score;
    public int BestCombo { get; private set; }

    /// <summary>Carried total, including whatever the live phase has scored so far.</summary>
    public int PlayerScore => _carriedScore + (_phaseFolded || _current is null ? 0 : _current.PhaseScore);

    public int Eaten => _carriedEaten + (_phaseFolded || _current is null ? 0 : _current.State.Eaten);

    /// <summary>True when a phase has finished and the interlude is owed. The scene
    /// layer plays its dialogue and then calls BeginNextPhase.</summary>
    public bool AwaitingInterlude { get; private set; }

    public IReadOnlyList<GameEvent> Start()
    {
        _events.Clear();
        PhaseIndex = 0;
        OpenPhase();
        return _events;
    }

    public IReadOnlyList<GameEvent> Tick(float dt)
    {
        _events.Clear();
        if (Result != BoutResult.InProgress || AwaitingInterlude || _current is null) return _events;

        // The rival eats through everything, knockouts included.
        if (_opponent.Tick(dt)) _events.Add(new OpponentAte(_opponent.Score));

        _events.AddRange(_current.Tick(dt, _carriedScore, _opponent.Score));

        if (!_current.State.IsOver) return _events;

        ClosePhase();
        return _events;
    }

    public IReadOnlyList<GameEvent> Chomp()
    {
        _events.Clear();
        if (Result != BoutResult.InProgress || AwaitingInterlude || _current is null) return _events;

        _events.AddRange(_current.Chomp(_carriedScore));
        return _events;
    }

    /// <summary>Called once the interlude's dialogue has been read.</summary>
    public IReadOnlyList<GameEvent> BeginNextPhase()
    {
        _events.Clear();
        if (!AwaitingInterlude) return _events;

        AwaitingInterlude = false;
        PhaseIndex++;
        OpenPhase();
        return _events;
    }

    private void OpenPhase()
    {
        _current = new PhaseSession(
            _table, _rng, _jaw, _spawnX, _retireX,
            _phases[PhaseIndex], _def.DifficultyOffset + _phases[PhaseIndex].DifficultyOffset);

        _phaseFolded = false;
        _events.Add(new PhaseStarted(PhaseIndex, _phases[PhaseIndex]));
    }

    private void ClosePhase()
    {
        _carriedScore += _current!.PhaseScore;
        _carriedEaten += _current.State.Eaten;
        _phaseFolded = true;

        if (_current.State.BestCombo > BestCombo) BestCombo = _current.State.BestCombo;

        _events.Add(new PhaseEnded(PhaseIndex, _current.KnockedOut, PlayerScore, _opponent.Score));

        if (PhaseIndex + 1 < _phases.Count)
        {
            AwaitingInterlude = true;
            return;
        }

        Result = _carriedScore > _opponent.Score ? BoutResult.Won : BoutResult.Lost;

        _events.Add(new BoutEnded(
            Result,
            _carriedScore,
            _opponent.Score,
            Prize: Result == BoutResult.Won ? _def.Opponent.PrizeMoney : 0,
            BestCombo: BestCombo,
            Eaten: _carriedEaten));
    }
}
