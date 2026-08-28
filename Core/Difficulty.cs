using System;

namespace CrocGame.Core;

/// <summary>
/// Every escalation lever, as a pure function of how many items the player has eaten.
/// This is the single tuning surface for the whole game.
/// </summary>
public readonly record struct Difficulty(
    float BeltSpeed,
    float SpacingMin,
    float SpacingMax,
    float StutterWeight,
    float BounceWeight,
    float InedibleChance,

    /// <summary>
    /// Odds that a spawn opens a three-item burst. Kept well under a half even at the
    /// ceiling: if most food arrives in bursts, bursts stop being a change of pace and
    /// become the pace.
    /// </summary>
    float ClusterChance)
{
    public static Difficulty ForEaten(int eaten)
    {
        var e = MathF.Max(0f, eaten);

        // Belt runs faster than the first pass, and the gap between items varies a
        // lot more. A near-constant gap lets the player fall into a metronome and stop
        // reacting - the timing has to be read off the food, not off a rhythm.
        var speed = Lerp(55f, 260f, e / 60f);
        var spacingMin = Lerp(0.72f, 0.22f, e / 60f);
        var jitter = Lerp(0.35f, 0.85f, e / 40f);

        return new Difficulty(
            BeltSpeed: speed,
            SpacingMin: spacingMin,
            SpacingMax: spacingMin * (1f + jitter),
            StutterWeight: e < 15 ? 0f : Lerp(0.15f, 0.45f, (e - 15f) / 45f),
            BounceWeight: e < 30 ? 0f : Lerp(0.10f, 0.35f, (e - 30f) / 40f),
            InedibleChance: e < 25 ? 0f : Lerp(0.04f, 0.20f, (e - 25f) / 45f),
            ClusterChance: e < 8 ? 0f : Lerp(0.08f, 0.22f, (e - 8f) / 45f));
    }

    /// <summary>Linear interpolation clamped to [a, b]. t below 0 or above 1 saturates.</summary>
    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
}
