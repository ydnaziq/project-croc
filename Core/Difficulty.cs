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
    float InedibleChance)
{
    public static Difficulty ForEaten(int eaten)
    {
        var e = MathF.Max(0f, eaten);

        var speed = Lerp(40f, 220f, e / 60f);
        var spacingMin = Lerp(1.10f, 0.35f, e / 60f);
        var jitter = Lerp(0.10f, 0.35f, e / 40f);

        return new Difficulty(
            BeltSpeed: speed,
            SpacingMin: spacingMin,
            SpacingMax: spacingMin * (1f + jitter),
            StutterWeight: e < 15 ? 0f : Lerp(0.15f, 0.45f, (e - 15f) / 45f),
            BounceWeight: e < 30 ? 0f : Lerp(0.10f, 0.35f, (e - 30f) / 40f),
            InedibleChance: e < 25 ? 0f : Lerp(0.04f, 0.20f, (e - 25f) / 45f));
    }

    /// <summary>Linear interpolation clamped to [a, b]. t below 0 or above 1 saturates.</summary>
    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
}
