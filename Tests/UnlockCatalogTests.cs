using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class UnlockCatalogTests
{
    [Fact]
    public void AFreshSaveHasEarnedNothing() =>
        Assert.Empty(UnlockCatalog.NewlyUnlocked(new SaveData()));

    [Fact]
    public void MeetingTheLifetimeThresholdEarnsTheMilestone()
    {
        var data = new SaveData { LifetimeEaten = 100 };
        Assert.Contains(UnlockCatalog.NewlyUnlocked(data), m => m.Id == "croc_gold");
    }

    [Fact]
    public void FallingOneShortEarnsNothing()
    {
        var data = new SaveData { LifetimeEaten = 99 };
        Assert.DoesNotContain(UnlockCatalog.NewlyUnlocked(data), m => m.Id == "croc_gold");
    }

    [Fact]
    public void MeetingTheScoreThresholdEarnsTheMilestone()
    {
        var data = new SaveData { BestScore = 500 };
        Assert.Contains(UnlockCatalog.NewlyUnlocked(data), m => m.Id == "croc_blue");
    }

    [Fact]
    public void ApplyRecordsTheMilestoneOnTheSave()
    {
        var data = new SaveData { LifetimeEaten = 100 };

        UnlockCatalog.Apply(data);

        Assert.Contains("croc_gold", data.UnlockedIds);
    }

    [Fact]
    public void AMilestoneIsOnlyEarnedOnce()
    {
        var data = new SaveData { LifetimeEaten = 100 };

        var first = UnlockCatalog.Apply(data);
        var second = UnlockCatalog.Apply(data);

        Assert.Single(first);
        Assert.Empty(second);
        Assert.Single(data.UnlockedIds);
    }

    [Fact]
    public void ApplyEarnsEverythingQualifiedAtOnce()
    {
        var data = new SaveData { LifetimeEaten = 500, BestScore = 1500 };

        var earned = UnlockCatalog.Apply(data);

        Assert.Equal(UnlockCatalog.All.Count, earned.Count);
    }

    [Fact]
    public void UnlockedIdsSurviveASaveRoundTrip()
    {
        var data = new SaveData { LifetimeEaten = 100 };
        UnlockCatalog.Apply(data);

        var restored = SaveData.FromJson(data.ToJson());

        Assert.Contains("croc_gold", restored.UnlockedIds);
    }

    [Fact]
    public void EveryMilestoneIdIsUnique() =>
        Assert.Equal(UnlockCatalog.All.Count, UnlockCatalog.All.Select(m => m.Id).Distinct().Count());
}
