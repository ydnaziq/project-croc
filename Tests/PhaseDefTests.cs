using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class PhaseDefTests
{
    [Fact]
    public void ABoutIsExactlyThreePhases()
    {
        Assert.Equal(3, Career.Phases.Count);
    }

    [Fact]
    public void PlainIsCleanAndCarriesNoWager()
    {
        var plain = Career.Phases[0];

        Assert.Equal("PLAIN", plain.Name);
        Assert.Equal(0f, plain.HazardScale, precision: 4);
        Assert.False(plain.PowerUpsEnabled);
        Assert.Equal(0f, plain.CoinIntervalSeconds, precision: 4);
        Assert.Equal(1, plain.ScoreMultiplier);
    }

    [Fact]
    public void FeastIsTheHardestAndPaysDouble()
    {
        var hazard = Career.Phases[1];
        var feast = Career.Phases[2];

        Assert.True(feast.DifficultyOffset > hazard.DifficultyOffset);
        Assert.True(feast.HazardScale > hazard.HazardScale);
        Assert.True(feast.CoinIntervalSeconds < hazard.CoinIntervalSeconds);
        Assert.Equal(2, feast.ScoreMultiplier);
    }

    [Fact]
    public void PhaseLengthsMatchTheSpec()
    {
        Assert.Equal(8f, Career.Phases[0].DurationSeconds, precision: 3);
        Assert.Equal(9f, Career.Phases[1].DurationSeconds, precision: 3);
        Assert.Equal(10f, Career.Phases[2].DurationSeconds, precision: 3);
    }
}
