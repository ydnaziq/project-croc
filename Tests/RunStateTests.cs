using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class RunStateTests
{
    [Fact]
    public void StartsEmpty()
    {
        var state = new RunState();
        Assert.Equal(0, state.Score);
        Assert.Equal(0, state.Combo);
        Assert.Equal(0, state.Strikes);
        Assert.Equal(0, state.Eaten);
        Assert.False(state.IsOver);
    }

    [Fact]
    public void HitIncrementsComboAndEatenAndScores()
    {
        var state = new RunState();
        state.RegisterHit(10);
        Assert.Equal(1, state.Combo);
        Assert.Equal(1, state.Eaten);
        Assert.Equal(10, state.Score);
    }

    [Fact]
    public void ComboMultipliesTheScore()
    {
        var state = new RunState();
        state.RegisterHit(10);
        state.RegisterHit(10);
        Assert.Equal(30, state.Score);
    }

    [Fact]
    public void ComboMultiplierCapsAtFive()
    {
        var state = new RunState();
        for (var i = 0; i < 8; i++) state.RegisterHit(10);
        Assert.Equal(300, state.Score);
        Assert.Equal(8, state.Combo);
    }

    [Fact]
    public void StrikeResetsTheCombo()
    {
        var state = new RunState();
        state.RegisterHit(10);
        state.RegisterHit(10);
        state.RegisterStrike();
        Assert.Equal(0, state.Combo);
        Assert.Equal(1, state.Strikes);
    }

    [Fact]
    public void StrikeDoesNotReduceTheScoreOrEatenCount()
    {
        var state = new RunState();
        state.RegisterHit(10);
        state.RegisterStrike();
        Assert.Equal(10, state.Score);
        Assert.Equal(1, state.Eaten);
    }

    [Fact]
    public void ThirdStrikeEndsTheRun()
    {
        var state = new RunState();
        state.RegisterStrike();
        state.RegisterStrike();
        Assert.False(state.IsOver);
        state.RegisterStrike();
        Assert.True(state.IsOver);
    }

    [Fact]
    public void ElapsedAccumulates()
    {
        var state = new RunState();
        state.AddElapsed(0.5f);
        state.AddElapsed(0.25f);
        Assert.Equal(0.75f, state.Elapsed, precision: 4);
    }
}
