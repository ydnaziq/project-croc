using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class DifficultyTests
{
    [Fact]
    public void StartsAtTheOpeningBeltSpeed() =>
        Assert.Equal(40f, Difficulty.ForEaten(0).BeltSpeed, precision: 2);

    [Fact]
    public void BeltSpeedIncreasesMonotonically()
    {
        var previous = 0f;
        for (var eaten = 0; eaten <= 200; eaten++)
        {
            var speed = Difficulty.ForEaten(eaten).BeltSpeed;
            Assert.True(speed >= previous, $"belt speed dropped at {eaten} eaten");
            previous = speed;
        }
    }

    [Fact]
    public void BeltSpeedIsCapped() =>
        Assert.Equal(220f, Difficulty.ForEaten(10_000).BeltSpeed, precision: 2);

    [Fact]
    public void SpacingNarrowsMonotonicallyAndIsFloored()
    {
        var previous = float.MaxValue;
        for (var eaten = 0; eaten <= 200; eaten++)
        {
            var min = Difficulty.ForEaten(eaten).SpacingMin;
            Assert.True(min <= previous, $"spacing widened at {eaten} eaten");
            previous = min;
        }
        Assert.Equal(0.35f, Difficulty.ForEaten(10_000).SpacingMin, precision: 2);
    }

    [Fact]
    public void SpacingMaxIsNeverBelowSpacingMin()
    {
        for (var eaten = 0; eaten <= 200; eaten++)
        {
            var d = Difficulty.ForEaten(eaten);
            Assert.True(d.SpacingMax >= d.SpacingMin, $"inverted spacing at {eaten} eaten");
        }
    }

    [Fact]
    public void StutterIsAbsentUntilFifteenEaten()
    {
        Assert.Equal(0f, Difficulty.ForEaten(14).StutterWeight, precision: 4);
        Assert.True(Difficulty.ForEaten(15).StutterWeight > 0f);
    }

    [Fact]
    public void BounceIsAbsentUntilThirtyEaten()
    {
        Assert.Equal(0f, Difficulty.ForEaten(29).BounceWeight, precision: 4);
        Assert.True(Difficulty.ForEaten(30).BounceWeight > 0f);
    }

    [Fact]
    public void InediblesAreAbsentUntilTwentyFiveEatenAndCapAtTwentyPercent()
    {
        Assert.Equal(0f, Difficulty.ForEaten(24).InedibleChance, precision: 4);
        Assert.True(Difficulty.ForEaten(25).InedibleChance > 0f);
        Assert.Equal(0.20f, Difficulty.ForEaten(10_000).InedibleChance, precision: 4);
    }

    [Fact]
    public void NegativeEatenIsTreatedAsZero() =>
        Assert.Equal(Difficulty.ForEaten(0), Difficulty.ForEaten(-5));
}
