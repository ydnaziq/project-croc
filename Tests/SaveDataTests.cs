using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class SaveDataTests
{
    [Fact]
    public void DefaultsAreEmpty()
    {
        var data = new SaveData();
        Assert.Equal(0, data.BestScore);
        Assert.Equal(0, data.LifetimeEaten);
        Assert.Empty(data.UnlockedIds);
    }

    [Fact]
    public void RoundTripsThroughJson()
    {
        var data = new SaveData { BestScore = 420, LifetimeEaten = 99 };
        data.UnlockedIds.Add("skin_gold");
        var restored = SaveData.FromJson(data.ToJson());
        Assert.Equal(420, restored.BestScore);
        Assert.Equal(99, restored.LifetimeEaten);
        Assert.Contains("skin_gold", restored.UnlockedIds);
    }

    [Fact]
    public void RoundTripsAnEmptyUnlockSet()
    {
        var restored = SaveData.FromJson(new SaveData { BestScore = 7 }.ToJson());
        Assert.Equal(7, restored.BestScore);
        Assert.Empty(restored.UnlockedIds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{ \"bestScore\": ")]
    [InlineData("[1,2,3]")]
    public void CorruptOrMissingSaveYieldsDefaultsInsteadOfThrowing(string? json)
    {
        var data = SaveData.FromJson(json);
        Assert.Equal(0, data.BestScore);
        Assert.Empty(data.UnlockedIds);
    }

    [Fact]
    public void InMemoryStoreReturnsWhatWasSaved()
    {
        var store = new InMemorySaveStore();
        store.Save(new SaveData { BestScore = 55 });
        Assert.Equal(55, store.Load().BestScore);
    }

    [Fact]
    public void InMemoryStoreStartsWithDefaults() =>
        Assert.Equal(0, new InMemorySaveStore().Load().BestScore);
}
