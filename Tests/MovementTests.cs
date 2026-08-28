using System;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class MovementTests
{
    [Fact]
    public void ConstantAdvancesBySpeedTimesDelta() =>
        Assert.Equal(5f, Movement.Constant.DeltaX(beltSpeed: 100f, dt: 0.05f, age: 0f), precision: 4);

    [Fact]
    public void ConstantIgnoresAge()
    {
        var a = Movement.Constant.DeltaX(100f, 0.05f, age: 0f);
        var b = Movement.Constant.DeltaX(100f, 0.05f, age: 12.5f);
        Assert.Equal(a, b, precision: 4);
    }

    [Fact]
    public void StutterPausesEarlyInEachCycle()
    {
        Assert.Equal(0f, Movement.Stutter.DeltaX(100f, 0.05f, age: 0.00f), precision: 4);
        Assert.Equal(0f, Movement.Stutter.DeltaX(100f, 0.05f, age: 1.10f), precision: 4);
    }

    [Fact]
    public void StutterMovesAtFullSpeedOutsideThePause()
    {
        Assert.Equal(5f, Movement.Stutter.DeltaX(100f, 0.05f, age: 0.50f), precision: 4);
        Assert.Equal(5f, Movement.Stutter.DeltaX(100f, 0.05f, age: 1.60f), precision: 4);
    }

    [Fact]
    public void BounceNeverMovesBackwards()
    {
        for (var age = 0f; age < 4f; age += 0.01f)
            Assert.True(Movement.Bounce.DeltaX(100f, 0.05f, age) >= 0f);
    }

    [Fact]
    public void BounceSurgesAboveBeltSpeedAtItsPeak()
    {
        var peak = Movement.Bounce.DeltaX(100f, 0.05f, age: MathF.PI / 12f);
        Assert.True(peak > 5f, $"expected a surge above the 5f baseline, got {peak}");
    }

    [Theory]
    [InlineData("constant")]
    [InlineData("stutter")]
    [InlineData("bounce")]
    public void ByNameResolvesKnownStrategies(string name) => Assert.NotNull(Movement.ByName(name));

    [Fact]
    public void ByNameFallsBackToConstantForUnknownNames() =>
        Assert.Same(Movement.Constant, Movement.ByName("teleport"));
}
