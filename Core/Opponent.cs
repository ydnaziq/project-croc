namespace CrocGame.Core;

/// <summary>
/// A rival eater. They are not simulated on a belt of their own - they simply consume
/// at a rate, because the player never sees their timing, only their score climbing.
/// Modelling more than that would be work no one can observe.
/// </summary>
public sealed record OpponentDef(
    string Id,
    string Name,
    string SpriteId,
    float SecondsPerBite,
    float BiteJitter,
    int PointsPerBite,
    int PrizeMoney,
    string Taunt,
    string LineLosing = "",
    string LineWinning = "",
    string LinePanic = "");

/// <summary>Accrues an opponent's score over the length of a match.</summary>
public sealed class OpponentEater
{
    private readonly OpponentDef _def;
    private readonly IRandomSource _rng;
    private float _secondsUntilBite;

    public OpponentEater(OpponentDef def, IRandomSource rng)
    {
        _def = def;
        _rng = rng;
        _secondsUntilBite = NextInterval();
    }

    public int Score { get; private set; }
    public int Bites { get; private set; }

    /// <summary>Advances the rival. Returns true on the frames where they take a bite.</summary>
    public bool Tick(float dt)
    {
        _secondsUntilBite -= dt;
        if (_secondsUntilBite > 0f) return false;

        _secondsUntilBite += NextInterval();
        Score += _def.PointsPerBite;
        Bites++;
        return true;
    }

    private float NextInterval()
    {
        var jitter = (_rng.NextFloat() * 2f - 1f) * _def.BiteJitter;
        var interval = _def.SecondsPerBite * (1f + jitter);
        return interval < 0.05f ? 0.05f : interval;
    }
}
