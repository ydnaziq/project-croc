using System;

namespace CrocGame.Core;

/// <summary>
/// How far an item travels this frame. All movement is along X only: the jaw zone is
/// a one-dimensional overlap test, so a behavior on any other axis would be invisible
/// to the judge and would make the game look like it was lying.
/// </summary>
public interface IMovement
{
    float DeltaX(float beltSpeed, float dt, float age);
}

public static class Movement
{
    public static readonly IMovement Constant = new ConstantMovement();
    public static readonly IMovement Stutter = new StutterMovement();
    public static readonly IMovement Bounce = new BounceMovement();

    /// <summary>Resolves a name from food.json. Unknown names fall back to Constant.</summary>
    public static IMovement ByName(string name) => name switch
    {
        "stutter" => Stutter,
        "bounce" => Bounce,
        _ => Constant,
    };

    private sealed class ConstantMovement : IMovement
    {
        public float DeltaX(float beltSpeed, float dt, float age) => beltSpeed * dt;
    }

    /// <summary>Halts for the first 0.15s of every 1s cycle, then resumes full speed.</summary>
    private sealed class StutterMovement : IMovement
    {
        private const float CycleSeconds = 1f;
        private const float PauseSeconds = 0.15f;

        public float DeltaX(float beltSpeed, float dt, float age) =>
            age % CycleSeconds < PauseSeconds ? 0f : beltSpeed * dt;
    }

    /// <summary>
    /// Surges forward and settles, so the item crosses the jaw zone faster than the
    /// belt speed implies. Never negative: the belt only ever moves one way.
    /// </summary>
    private sealed class BounceMovement : IMovement
    {
        public float DeltaX(float beltSpeed, float dt, float age)
        {
            var surge = MathF.Max(0f, MathF.Sin(age * 6f));
            return beltSpeed * dt * (1f + 0.8f * surge);
        }
    }
}
