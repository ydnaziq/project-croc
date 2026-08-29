using System.Collections.Generic;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class ActiveBuffsTests
{
    [Fact]
    public void ParsesTheFourPowerIds()
    {
        Assert.Equal(BuffKind.Slow, PowerUp.Parse("slow"));
        Assert.Equal(BuffKind.Shield, PowerUp.Parse("shield"));
        Assert.Equal(BuffKind.Magnet, PowerUp.Parse("magnet"));
        Assert.Equal(BuffKind.GoldTooth, PowerUp.Parse("goldtooth"));
        Assert.Null(PowerUp.Parse(""));
        Assert.Null(PowerUp.Parse("pizza"));
    }

    [Fact]
    public void StartsWithNothing()
    {
        var buffs = new ActiveBuffs();

        Assert.False(buffs.HasShield);
        Assert.Equal(0, buffs.MagnetBitesRemaining);
        Assert.Equal(1f, buffs.SpeedMultiplier, precision: 4);
        Assert.Equal(1, buffs.ScoreMultiplier);
    }

    [Fact]
    public void SlowReducesBeltSpeedUntilItExpires()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.Slow);

        Assert.Equal(ActiveBuffs.SlowSpeedMultiplier, buffs.SpeedMultiplier, precision: 4);

        var expired = Run(buffs, ActiveBuffs.SlowSeconds + 0.5f);

        Assert.Contains(BuffKind.Slow, expired);
        Assert.Equal(1f, buffs.SpeedMultiplier, precision: 4);
    }

    [Fact]
    public void GoldToothTriplesScoreUntilItExpires()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.GoldTooth);

        Assert.Equal(ActiveBuffs.GoldToothMultiplier, buffs.ScoreMultiplier);

        var expired = Run(buffs, ActiveBuffs.GoldToothSeconds + 0.5f);

        Assert.Contains(BuffKind.GoldTooth, expired);
        Assert.Equal(1, buffs.ScoreMultiplier);
    }

    [Fact]
    public void ShieldAbsorbsExactlyOneStrike()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.Shield);

        Assert.True(buffs.HasShield);
        Assert.True(buffs.ConsumeShield());
        Assert.False(buffs.HasShield);
        Assert.False(buffs.ConsumeShield());
    }

    [Fact]
    public void MagnetAppliesToExactlyThreeBites()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.Magnet);

        Assert.Equal(ActiveBuffs.MagnetBites, buffs.MagnetBitesRemaining);

        for (var i = 0; i < ActiveBuffs.MagnetBites; i++) Assert.True(buffs.ConsumeMagnetBite());

        Assert.False(buffs.ConsumeMagnetBite());
        Assert.Equal(0, buffs.MagnetBitesRemaining);
    }

    [Fact]
    public void MagnetDoesNotExpireOnTime()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.Magnet);

        Run(buffs, 60f);

        Assert.Equal(ActiveBuffs.MagnetBites, buffs.MagnetBitesRemaining);
    }

    [Fact]
    public void TakingTheSameBuffTwiceRefreshesRatherThanStacks()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.Slow);
        Run(buffs, ActiveBuffs.SlowSeconds * 0.5f);
        buffs.Take(BuffKind.Slow);

        Assert.Equal(ActiveBuffs.SlowSeconds, buffs.SlowRemaining, precision: 2);

        buffs.Take(BuffKind.Magnet);
        buffs.ConsumeMagnetBite();
        buffs.Take(BuffKind.Magnet);

        Assert.Equal(ActiveBuffs.MagnetBites, buffs.MagnetBitesRemaining);
    }

    [Fact]
    public void ResettingForAPhaseClearsEverything()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.Slow);
        buffs.Take(BuffKind.Shield);
        buffs.Take(BuffKind.Magnet);
        buffs.Take(BuffKind.GoldTooth);

        buffs.ResetForPhase();

        Assert.False(buffs.HasShield);
        Assert.Equal(0, buffs.MagnetBitesRemaining);
        Assert.Equal(1f, buffs.SpeedMultiplier, precision: 4);
        Assert.Equal(1, buffs.ScoreMultiplier);
    }

    private static List<BuffKind> Run(ActiveBuffs buffs, float seconds)
    {
        var expired = new List<BuffKind>();
        for (var t = 0f; t < seconds; t += 0.05f) expired.AddRange(buffs.Tick(0.05f));
        return expired;
    }
}
