using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class PotTests
{
    [Fact]
    public void StartsEmpty()
    {
        var pot = new Pot();

        Assert.True(pot.IsEmpty);
        Assert.Equal(0, pot.Amount);
    }

    [Fact]
    public void AddingAccrues()
    {
        var pot = new Pot();
        pot.Add(10);
        pot.Add(15);

        Assert.Equal(25, pot.Amount);
        Assert.False(pot.IsEmpty);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(9, 2)]
    [InlineData(10, 3)]
    [InlineData(14, 3)]
    [InlineData(15, 5)]
    [InlineData(40, 5)]
    public void MultiplierStepsOnComboBoundaries(int combo, int expected)
    {
        Assert.Equal(expected, Pot.MultiplierForCombo(combo));
    }

    [Fact]
    public void BankingPaysTheAmountTimesTheMultiplierAndEmptiesThePot()
    {
        var pot = new Pot();
        pot.Add(30);

        Assert.Equal(90, pot.PendingAt(combo: 12));
        Assert.Equal(90, pot.Bank(combo: 12));
        Assert.True(pot.IsEmpty);
    }

    [Fact]
    public void BankingAnEmptyPotPaysNothing()
    {
        var pot = new Pot();

        Assert.Equal(0, pot.Bank(combo: 20));
    }

    [Fact]
    public void WipingLosesEverythingUnbanked()
    {
        var pot = new Pot();
        pot.Add(100);
        pot.Wipe();

        Assert.True(pot.IsEmpty);
        Assert.Equal(0, pot.Bank(combo: 15));
    }
}
