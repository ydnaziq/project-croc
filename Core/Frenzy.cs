namespace CrocGame.Core;

/// <summary>
/// The peak in the gameplay's peaks and valleys. A long enough combo tips the match
/// into a short burst where the belt runs faster and everything scores double - so
/// good play compounds instead of just continuing.
/// </summary>
public sealed class Frenzy
{
    public const int ComboToTrigger = 5;
    public const float DurationSeconds = 6f;
    public const int ScoreMultiplier = 2;
    public const float BeltSpeedMultiplier = 1.4f;

    public bool IsActive => Remaining > 0f;
    public float Remaining { get; private set; }

    /// <summary>Fraction of the frenzy left, for a meter. 0 when inactive.</summary>
    public float Fraction => IsActive ? Remaining / DurationSeconds : 0f;

    /// <summary>Starts, or refreshes an active frenzy back to full.</summary>
    public void Trigger() => Remaining = DurationSeconds;

    /// <summary>Returns true on the frame the frenzy runs out.</summary>
    public bool Tick(float dt)
    {
        if (!IsActive) return false;

        Remaining -= dt;
        if (Remaining > 0f) return false;

        Remaining = 0f;
        return true;
    }

    public void Reset() => Remaining = 0f;

    public int Multiplier => IsActive ? ScoreMultiplier : 1;

    public float SpeedMultiplier => IsActive ? BeltSpeedMultiplier : 1f;
}
