using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class HungerTests
{
    /// <summary>Runs the meter for a while at a fixed scoreline.</summary>
    private static void Feed(Hunger hunger, int player, int opponent, float seconds)
    {
        for (var t = 0f; t < seconds; t += 0.05f) hunger.Update(0.05f, player, opponent);
    }

    [Fact]
    public void StartsEmptyAndInactive()
    {
        var hunger = new Hunger();

        Assert.Equal(0f, hunger.Charge, precision: 4);
        Assert.False(hunger.IsActive);
        Assert.Equal(1f, hunger.SpeedMultiplier, precision: 4);
        Assert.Equal(1f, hunger.JawMultiplier, precision: 4);
    }

    [Fact]
    public void DoesNotChargeWhileAhead()
    {
        var hunger = new Hunger();
        Feed(hunger, player: 500, opponent: 100, seconds: 20f);

        Assert.Equal(0f, hunger.Charge, precision: 4);
        Assert.False(hunger.TryFire());
    }

    [Fact]
    public void DoesNotChargeWhenLevel()
    {
        var hunger = new Hunger();
        Feed(hunger, player: 200, opponent: 200, seconds: 20f);

        Assert.Equal(0f, hunger.Charge, precision: 4);
    }

    [Fact]
    public void ChargesFasterTheFurtherBehind()
    {
        var near = new Hunger();
        Feed(near, player: 90, opponent: 100, seconds: 2f);

        var far = new Hunger();
        Feed(far, player: 0, opponent: 100, seconds: 2f);

        Assert.True(far.Charge > near.Charge);
    }

    [Fact]
    public void AStrikeAdvancesTheMeter()
    {
        var hunger = new Hunger();
        hunger.OnStrike();

        Assert.Equal(Hunger.StrikeCharge, hunger.Charge, precision: 4);
    }

    [Fact]
    public void FiresOnlyOnceAFullMeterIsReached()
    {
        var hunger = new Hunger();
        Assert.False(hunger.TryFire());

        Feed(hunger, player: 0, opponent: 100, seconds: Hunger.ChargeSeconds + 1f);

        Assert.True(hunger.TryFire());
        Assert.True(hunger.IsActive);
        Assert.Equal(Hunger.DurationSeconds, hunger.Remaining, precision: 3);
    }

    [Fact]
    public void FiresAtMostOncePerPhase()
    {
        var hunger = new Hunger();
        Feed(hunger, player: 0, opponent: 100, seconds: Hunger.ChargeSeconds + 1f);
        Assert.True(hunger.TryFire());

        for (var i = 0; i < 200; i++) hunger.Tick(0.05f);
        Feed(hunger, player: 0, opponent: 100, seconds: Hunger.ChargeSeconds + 1f);

        Assert.True(hunger.HasFiredThisPhase);
        Assert.False(hunger.TryFire());
    }

    [Fact]
    public void ResettingForAPhaseAllowsItToFireAgain()
    {
        var hunger = new Hunger();
        Feed(hunger, player: 0, opponent: 100, seconds: Hunger.ChargeSeconds + 1f);
        Assert.True(hunger.TryFire());

        hunger.ResetForPhase();

        Assert.False(hunger.HasFiredThisPhase);
        Assert.False(hunger.IsActive);
        Assert.Equal(0f, hunger.Charge, precision: 4);
    }

    [Fact]
    public void WhileActiveItSlowsTheBeltAndWidensTheJaws()
    {
        var hunger = new Hunger();
        Feed(hunger, player: 0, opponent: 100, seconds: Hunger.ChargeSeconds + 1f);
        hunger.TryFire();

        Assert.Equal(Hunger.BeltSpeedMultiplier, hunger.SpeedMultiplier, precision: 4);
        Assert.Equal(Hunger.JawWidthMultiplier, hunger.JawMultiplier, precision: 4);
        Assert.True(hunger.JawMultiplier > 1f);
        Assert.True(hunger.SpeedMultiplier < 1f);
    }

    [Fact]
    public void TickReportsTheFrameItExpiresAndThenStopsHelping()
    {
        var hunger = new Hunger();
        Feed(hunger, player: 0, opponent: 100, seconds: Hunger.ChargeSeconds + 1f);
        hunger.TryFire();

        var ended = false;
        for (var t = 0f; t < Hunger.DurationSeconds + 1f; t += 0.05f)
        {
            if (hunger.Tick(0.05f)) ended = true;
        }

        Assert.True(ended);
        Assert.False(hunger.IsActive);
        Assert.Equal(1f, hunger.JawMultiplier, precision: 4);
    }
}
