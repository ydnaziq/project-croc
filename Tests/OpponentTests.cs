using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class OpponentTests
{
    private static readonly OpponentDef Def = new(
        "penguin", "PIP", "penguin",
        SecondsPerBite: 1.0f, BiteJitter: 0f, PointsPerBite: 40,
        PrizeMoney: 25, Taunt: "hi");

    private static OpponentEater Eater(OpponentDef? def = null, int seed = 1) =>
        new(def ?? Def, new SeededRandom(seed));

    [Fact]
    public void StartsAtZero()
    {
        var eater = Eater();
        Assert.Equal(0, eater.Score);
        Assert.Equal(0, eater.Bites);
    }

    [Fact]
    public void DoesNotBiteBeforeItsInterval()
    {
        var eater = Eater();
        Assert.False(eater.Tick(0.5f));
        Assert.Equal(0, eater.Score);
    }

    [Fact]
    public void BitesOnceTheIntervalElapses()
    {
        var eater = Eater();
        eater.Tick(0.6f);

        Assert.True(eater.Tick(0.5f));
        Assert.Equal(40, eater.Score);
        Assert.Equal(1, eater.Bites);
    }

    [Fact]
    public void ScoresAtRoughlyTheExpectedRateOverAMatch()
    {
        var eater = Eater();
        for (var t = 0f; t < 30f; t += 1f / 60f) eater.Tick(1f / 60f);

        // 30 seconds at one bite a second, allowing a frame either side.
        Assert.InRange(eater.Bites, 29, 31);
    }

    [Fact]
    public void AFasterOpponentOutscoresASlowerOne()
    {
        var slow = Eater(Def with { SecondsPerBite = 1.5f });
        var fast = Eater(Def with { SecondsPerBite = 0.75f });

        for (var t = 0f; t < 20f; t += 1f / 60f)
        {
            slow.Tick(1f / 60f);
            fast.Tick(1f / 60f);
        }

        Assert.True(fast.Score > slow.Score);
    }

    [Fact]
    public void JitterStaysReproducibleForASeed()
    {
        var jittery = Def with { BiteJitter = 0.5f };
        var a = Eater(jittery, seed: 9);
        var b = Eater(jittery, seed: 9);

        for (var t = 0f; t < 15f; t += 1f / 60f)
        {
            a.Tick(1f / 60f);
            b.Tick(1f / 60f);
        }

        Assert.Equal(a.Score, b.Score);
    }
}
