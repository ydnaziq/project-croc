using System;

namespace CrocGame.Core;

/// <summary>Randomness as a dependency, so a seed reproduces a run exactly.</summary>
public interface IRandomSource
{
    /// <summary>A value in [0, 1).</summary>
    float NextFloat();

    /// <summary>A value in [0, maxExclusive).</summary>
    int NextInt(int maxExclusive);
}

public sealed class SeededRandom : IRandomSource
{
    private readonly Random _random;

    public SeededRandom(int seed) => _random = new Random(seed);

    public float NextFloat() => (float)_random.NextDouble();

    public int NextInt(int maxExclusive) => _random.Next(maxExclusive);
}
