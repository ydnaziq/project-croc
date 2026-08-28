using System.Collections.Generic;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class SpawnDirectorTests
{
    private const string Json = """
    [
      { "id": "hotdog", "width": 16, "edible": true,  "movement": "constant", "score": 10, "minEatenToAppear": 0 },
      { "id": "boot",   "width": 16, "edible": false, "movement": "constant", "score": 0,  "minEatenToAppear": 25 }
    ]
    """;

    private static SpawnDirector Director(int seed = 7) =>
        new(FoodTable.FromJson(Json), new SeededRandom(seed), spawnX: -20f);

    private static List<FoodItem> RunFor(SpawnDirector director, float seconds, int eaten, float dt = 1f / 60f)
    {
        var spawned = new List<FoodItem>();
        for (var t = 0f; t < seconds; t += dt)
        {
            var item = director.Tick(dt, eaten);
            if (item is not null) spawned.Add(item);
        }
        return spawned;
    }

    [Fact]
    public void SpawnsNothingOnTheVeryFirstTick() =>
        Assert.Null(Director().Tick(1f / 60f, eaten: 0));

    [Fact]
    public void EventuallySpawns() =>
        Assert.NotEmpty(RunFor(Director(), seconds: 10f, eaten: 0));

    [Fact]
    public void SpawnsAtTheSpawnX() =>
        Assert.All(RunFor(Director(), seconds: 10f, eaten: 0), i => Assert.Equal(-20f, i.X, precision: 2));

    [Fact]
    public void AssignsHalfWidthFromTypeWidth() =>
        Assert.All(RunFor(Director(), seconds: 10f, eaten: 0), i => Assert.Equal(8f, i.HalfWidth, precision: 2));

    [Fact]
    public void AssignsUniqueIds()
    {
        var items = RunFor(Director(), seconds: 30f, eaten: 0);
        Assert.Equal(items.Count, new HashSet<int>(items.ConvertAll(i => i.Id)).Count);
    }

    [Fact]
    public void SameSeedProducesTheSameRun()
    {
        var a = RunFor(Director(seed: 42), seconds: 30f, eaten: 30);
        var b = RunFor(Director(seed: 42), seconds: 30f, eaten: 30);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].TypeId, b[i].TypeId);
            Assert.Equal(a[i].IsEdible, b[i].IsEdible);
        }
    }

    [Fact]
    public void DifferentSeedsDiverge()
    {
        var a = RunFor(Director(seed: 1), seconds: 60f, eaten: 40);
        var b = RunFor(Director(seed: 2), seconds: 60f, eaten: 40);

        if (a.Count != b.Count) return;

        var identical = true;
        for (var i = 0; i < a.Count && identical; i++) identical = a[i].TypeId == b[i].TypeId;
        Assert.False(identical, "two different seeds produced an identical sequence");
    }

    [Fact]
    public void NeverSpawnsInediblesBeforeTheirThreshold() =>
        Assert.All(RunFor(Director(), seconds: 60f, eaten: 10), i => Assert.True(i.IsEdible));

    [Fact]
    public void SpawnsFasterAtHigherDifficulty()
    {
        var early = RunFor(Director(seed: 5), seconds: 30f, eaten: 0).Count;
        var late = RunFor(Director(seed: 5), seconds: 30f, eaten: 60).Count;
        Assert.True(late > early, $"expected more spawns when escalated: {early} then {late}");
    }
}
