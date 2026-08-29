using System;

namespace CrocGame.Core;

public enum MatchResult
{
    InProgress,
    Won,
    Lost,
    Disqualified,
}

/// <summary>Everything that resets when a match starts.</summary>
public sealed class MatchState
{
    public const int MaxStrikes = 3;
    private const int MaxComboMultiplier = 5;

    public MatchState(float durationSeconds) => TimeRemaining = durationSeconds;

    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int BestCombo { get; private set; }
    public int Strikes { get; private set; }
    public int Eaten { get; private set; }
    public float TimeRemaining { get; private set; }
    public MatchResult Result { get; private set; } = MatchResult.InProgress;

    public bool IsOver => Result != MatchResult.InProgress;

    /// <summary>Ticks the clock down. Returns true on the frame time runs out.</summary>
    public bool AdvanceClock(float dt)
    {
        if (TimeRemaining <= 0f) return false;

        TimeRemaining -= dt;
        if (TimeRemaining > 0f) return false;

        TimeRemaining = 0f;
        return true;
    }

    /// <summary>Scores a bite. Returns the points awarded.</summary>
    public int RegisterHit(int baseScore, int frenzyMultiplier)
    {
        Combo++;
        Eaten++;
        BestCombo = Math.Max(BestCombo, Combo);

        var points = baseScore * Math.Min(Combo, MaxComboMultiplier) * frenzyMultiplier;
        Score += points;
        return points;
    }

    public void RegisterStrike()
    {
        Strikes++;
        Combo = 0;

        if (Strikes >= MaxStrikes) Result = MatchResult.Disqualified;
    }

    /// <summary>
    /// Costs the player their streak without costing a strike. Food riding past is a
    /// missed opportunity in a scoring race, not a foul: the belt should never be able
    /// to disqualify someone who is playing correctly.
    /// </summary>
    public void BreakCombo() => Combo = 0;

    public void Settle(int opponentScore) =>
        Result = Score > opponentScore ? MatchResult.Won : MatchResult.Lost;
}
