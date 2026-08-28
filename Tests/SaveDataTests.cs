using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class SaveDataTests
{
    [Fact]
    public void DefaultsAreEmpty()
    {
        var data = new SaveData();

        Assert.Equal(0, data.Money);
        Assert.Equal(0, data.BestScore);
        Assert.Equal(0, data.LifetimeEaten);
        Assert.Empty(data.DefeatedIds);
        Assert.Empty(data.OwnedSkinIds);
        Assert.Equal("", data.EquippedSkinId);
    }

    [Fact]
    public void RoundTripsThroughJson()
    {
        var data = new SaveData { Money = 175, BestScore = 420, LifetimeEaten = 99 };
        data.DefeatedIds.Add("penguin");
        data.OwnedSkinIds.Add("skin_gold");
        data.EquippedSkinId = "skin_gold";

        var restored = SaveData.FromJson(data.ToJson());

        Assert.Equal(175, restored.Money);
        Assert.Equal(420, restored.BestScore);
        Assert.Equal(99, restored.LifetimeEaten);
        Assert.Contains("penguin", restored.DefeatedIds);
        Assert.Contains("skin_gold", restored.OwnedSkinIds);
        Assert.Equal("skin_gold", restored.EquippedSkinId);
    }

    [Fact]
    public void RoundTripsAnEmptyProgressSet()
    {
        var restored = SaveData.FromJson(new SaveData { Money = 7 }.ToJson());

        Assert.Equal(7, restored.Money);
        Assert.Empty(restored.DefeatedIds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{ \"money\": ")]
    [InlineData("[1,2,3]")]
    public void CorruptOrMissingSaveYieldsDefaultsInsteadOfThrowing(string? json)
    {
        var data = SaveData.FromJson(json);

        Assert.Equal(0, data.Money);
        Assert.Empty(data.DefeatedIds);
    }

    [Fact]
    public void InMemoryStoreReturnsWhatWasSaved()
    {
        var store = new InMemorySaveStore();
        store.Save(new SaveData { Money = 55 });

        Assert.Equal(55, store.Load().Money);
    }

    [Fact]
    public void InMemoryStoreStartsWithDefaults() =>
        Assert.Equal(0, new InMemorySaveStore().Load().Money);
}
