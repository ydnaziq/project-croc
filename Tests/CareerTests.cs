using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class CareerTests
{
    private static BoutEnded Win(int prize, int score = 900) =>
        new(BoutResult.Won, score, 800, prize, BestCombo: 10, Eaten: 30);

    [Fact]
    public void AFreshSaveFacesTheFirstRungOfTheLadder()
    {
        var match = Career.NextMatch(new SaveData());

        Assert.NotNull(match);
        Assert.Equal("penguin", match!.Opponent.Id);
    }

    [Fact]
    public void TheLadderGetsHarderEveryRung()
    {
        for (var i = 1; i < Career.Ladder.Count; i++)
        {
            Assert.True(Career.Ladder[i].SecondsPerBite < Career.Ladder[i - 1].SecondsPerBite,
                $"rung {i} does not eat faster than rung {i - 1}");
            Assert.True(Career.Ladder[i].PrizeMoney > Career.Ladder[i - 1].PrizeMoney,
                $"rung {i} does not pay better than rung {i - 1}");
        }
    }

    [Fact]
    public void WinningAdvancesToTheNextRungAndPays()
    {
        var data = new SaveData();

        Career.RecordWin(data, Win(prize: 25));

        Assert.Equal(25, data.Money);
        Assert.Equal(1, Career.Progress(data));
        Assert.Equal("cat", Career.NextMatch(data)!.Opponent.Id);
    }

    [Fact]
    public void LosingKeepsProgressAndPaysNothing()
    {
        var data = new SaveData();

        Career.RecordLoss(data, new BoutEnded(BoutResult.Lost, 500, 800, 0, 4, 20));

        Assert.Equal(0, data.Money);
        Assert.Equal(0, Career.Progress(data));
        Assert.Equal("penguin", Career.NextMatch(data)!.Opponent.Id);
    }

    [Fact]
    public void BeatingEveryRungMakesTheCrocChampion()
    {
        var data = new SaveData();
        foreach (var rung in Career.Ladder) Career.RecordWin(data, Win(rung.PrizeMoney));

        Assert.True(Career.IsChampion(data));
        Assert.Null(Career.NextMatch(data));
    }

    [Fact]
    public void ALossStillRecordsABestScore()
    {
        var data = new SaveData { BestScore = 100 };

        Career.RecordLoss(data, new BoutEnded(BoutResult.Lost, 700, 800, 0, 5, 25));

        Assert.Equal(700, data.BestScore);
    }

    [Fact]
    public void BuyingSpendsTheMoneyAndEquipsTheSkin()
    {
        var data = new SaveData { Money = 100 };
        var item = Career.Shop[0];

        var result = Career.Buy(data, item.Id);

        Assert.Equal(PurchaseResult.Bought, result);
        Assert.Equal(100 - item.Cost, data.Money);
        Assert.Contains(item.Id, data.OwnedSkinIds);
        Assert.Equal(item.Id, data.EquippedSkinId);
    }

    [Fact]
    public void BuyingWhatYouCannotAffordChangesNothing()
    {
        var data = new SaveData { Money = 1 };

        var result = Career.Buy(data, Career.Shop[0].Id);

        Assert.Equal(PurchaseResult.TooExpensive, result);
        Assert.Equal(1, data.Money);
        Assert.Empty(data.OwnedSkinIds);
    }

    [Fact]
    public void BuyingTwiceIsRefusedAndDoesNotDoubleCharge()
    {
        var data = new SaveData { Money = 500 };
        var item = Career.Shop[0];

        Career.Buy(data, item.Id);
        var moneyAfterFirst = data.Money;
        var result = Career.Buy(data, item.Id);

        Assert.Equal(PurchaseResult.AlreadyOwned, result);
        Assert.Equal(moneyAfterFirst, data.Money);
    }

    [Fact]
    public void BuyingSomethingThatDoesNotExistIsRefused() =>
        Assert.Equal(PurchaseResult.NoSuchItem, Career.Buy(new SaveData { Money = 999 }, "skin_nope"));

    [Fact]
    public void EquippingSomethingUnownedIsRefused()
    {
        var data = new SaveData();

        Assert.False(Career.Equip(data, Career.Shop[0].Id));
        Assert.Equal("", data.EquippedSkinId);
    }

    [Fact]
    public void EquippingNothingClearsTheSkin()
    {
        var data = new SaveData { Money = 500 };
        Career.Buy(data, Career.Shop[0].Id);

        Assert.True(Career.Equip(data, ""));
        Assert.Null(Career.EquippedSkin(data));
    }

    [Fact]
    public void EveryShopItemIdIsUnique() =>
        Assert.Equal(Career.Shop.Count, Career.Shop.Select(i => i.Id).Distinct().Count());

    [Fact]
    public void CareerProgressSurvivesASaveRoundTrip()
    {
        var data = new SaveData { Money = 75 };
        Career.RecordWin(data, Win(25));
        Career.Buy(data, Career.Shop[0].Id);

        var restored = SaveData.FromJson(data.ToJson());

        Assert.Equal(data.Money, restored.Money);
        Assert.Equal(data.DefeatedIds, restored.DefeatedIds);
        Assert.Equal(data.EquippedSkinId, restored.EquippedSkinId);
    }
}
