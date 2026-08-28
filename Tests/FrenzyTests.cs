using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class FrenzyTests
{
    [Fact]
    public void StartsInactive()
    {
        var frenzy = new Frenzy();
        Assert.False(frenzy.IsActive);
        Assert.Equal(1, frenzy.Multiplier);
        Assert.Equal(1f, frenzy.SpeedMultiplier, precision: 4);
        Assert.Equal(0f, frenzy.Fraction, precision: 4);
    }

    [Fact]
    public void TriggeringActivatesItAtFullDuration()
    {
        var frenzy = new Frenzy();
        frenzy.Trigger();

        Assert.True(frenzy.IsActive);
        Assert.Equal(Frenzy.DurationSeconds, frenzy.Remaining, precision: 3);
        Assert.Equal(1f, frenzy.Fraction, precision: 3);
    }

    [Fact]
    public void WhileActiveItDoublesScoreAndSpeedsTheBelt()
    {
        var frenzy = new Frenzy();
        frenzy.Trigger();

        Assert.Equal(2, frenzy.Multiplier);
        Assert.True(frenzy.SpeedMultiplier > 1f);
    }

    [Fact]
    public void TickReportsTheFrameItExpires()
    {
        var frenzy = new Frenzy();
        frenzy.Trigger();

        var expiredEarly = frenzy.Tick(Frenzy.DurationSeconds - 0.1f);
        Assert.False(expiredEarly);

        Assert.True(frenzy.Tick(0.2f));
        Assert.False(frenzy.IsActive);
    }

    [Fact]
    public void TickOnAnInactiveFrenzyReportsNothing() =>
        Assert.False(new Frenzy().Tick(1f));

    [Fact]
    public void RetriggeringRefreshesToFullDuration()
    {
        var frenzy = new Frenzy();
        frenzy.Trigger();
        frenzy.Tick(4f);

        frenzy.Trigger();

        Assert.Equal(Frenzy.DurationSeconds, frenzy.Remaining, precision: 3);
    }

    [Fact]
    public void ResetEndsItImmediately()
    {
        var frenzy = new Frenzy();
        frenzy.Trigger();

        frenzy.Reset();

        Assert.False(frenzy.IsActive);
    }
}
