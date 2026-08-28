using System;

namespace CrocGame.Core;

/// <summary>Everything that resets when a run starts over.</summary>
public sealed class RunState
{
    public const int MaxStrikes = 3;
    private const int MaxComboMultiplier = 5;

    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int Strikes { get; private set; }
    public int Eaten { get; private set; }
    public float Elapsed { get; private set; }

    public bool IsOver => Strikes >= MaxStrikes;

    public void AddElapsed(float dt) => Elapsed += dt;

    public void RegisterHit(int score)
    {
        Combo++;
        Eaten++;
        Score += score * Math.Min(Combo, MaxComboMultiplier);
    }

    public void RegisterStrike()
    {
        Strikes++;
        Combo = 0;
    }
}
