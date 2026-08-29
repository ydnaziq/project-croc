using System.Collections.Generic;
using System.Linq;
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
            var item = director.Tick(dt, eaten, Career.Phases[1]);
            if (item is not null) spawned.Add(item);
        }
        return spawned;
    }

    [Fact]
    public void SpawnsNothingOnTheVeryFirstTick() =>
        Assert.Null(Director().Tick(1f / 60f, eaten: 0, Career.Phases[1]));

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
    public void RareItemsAppearFarLessOftenThanCommonOnes()
    {
        const string weighted = """
        [
          { "id": "common", "width": 16, "edible": true, "movement": "constant", "score": 10, "minEatenToAppear": 0, "weight": 9 },
          { "id": "rare",   "width": 16, "edible": true, "movement": "constant", "score": 60, "minEatenToAppear": 0, "weight": 1 }
        ]
        """;

        var director = new SpawnDirector(FoodTable.FromJson(weighted), new SeededRandom(4), spawnX: -20f);
        var items = RunFor(director, seconds: 600f, eaten: 0);

        var rare = items.Count(i => i.TypeId == "rare");

        Assert.True(rare > 0, "the rare item never appeared at all");
        Assert.True(rare < items.Count / 4,
            $"rare item was not rare: {rare} of {items.Count}");
    }

    [Fact]
    public void MissingWeightsDefaultToOne()
    {
        var table = FoodTable.FromJson(Json);
        Assert.All(table.Types, t => Assert.Equal(1, t.Weight));
    }

    [Fact]
    public void BurstsProduceGapsMuchTighterThanTheOrdinarySpacing()
    {
        // Clusters only start after 8 eaten, so ask for a difficulty that has them.
        var director = Director(seed: 12);
        var gaps = new List<float>();
        var sinceLast = 0f;

        for (var t = 0f; t < 400f; t += 1f / 60f)
        {
            sinceLast += 1f / 60f;

            if (director.Tick(1f / 60f, eaten: 40, Career.Phases[1]) is not null)
            {
                gaps.Add(sinceLast);
                sinceLast = 0f;
            }
        }

        var difficulty = Difficulty.ForEaten(40);
        var tight = gaps.Count(g => g < difficulty.SpacingMin * 0.6f);

        Assert.True(tight > 0, "no burst gaps appeared at all");
        Assert.True(tight < gaps.Count / 2, "almost everything was a burst");
    }

    [Fact]
    public void SpawnsFasterAtHigherDifficulty()
    {
        var early = RunFor(Director(seed: 5), seconds: 30f, eaten: 0).Count;
        var late = RunFor(Director(seed: 5), seconds: 30f, eaten: 60).Count;
        Assert.True(late > early, $"expected more spawns when escalated: {early} then {late}");
    }

    /// <summary>Drains a whole phase's worth of spawns.</summary>
    private static List<FoodItem> SpawnMany(SpawnDirector director, PhaseDef phase, int eaten, float seconds)
    {
        var items = new List<FoodItem>();
        for (var t = 0f; t < seconds; t += 0.02f)
        {
            var item = director.Tick(0.02f, eaten, phase);
            if (item is not null) items.Add(item);
        }
        return items;
    }

    private static FoodTable ShippedTable() =>
        FoodTable.FromJson(System.IO.File.ReadAllText(
            System.IO.Path.Combine(FoodTableTests.RepoRoot(), "Resources", "food.json")));

    [Fact]
    public void APhaseWithNoHazardScaleNeverSpawnsSomethingInedible()
    {
        var director = new SpawnDirector(ShippedTable(), new SeededRandom(7), spawnX: 0f);

        // Eaten is high enough that the difficulty curve would happily produce bombs.
        var items = SpawnMany(director, Career.Phases[0], eaten: 60, seconds: 60f);

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.True(i.IsEdible));
    }

    [Fact]
    public void APhaseWithPowerUpsDisabledNeverSpawnsABuff()
    {
        var director = new SpawnDirector(ShippedTable(), new SeededRandom(11), spawnX: 0f);

        var items = SpawnMany(director, Career.Phases[0], eaten: 60, seconds: 60f);

        Assert.All(items, i => Assert.Equal("", i.Power));
    }

    [Fact]
    public void APhaseWithPowerUpsEnabledEventuallySpawnsEachBuff()
    {
        var director = new SpawnDirector(ShippedTable(), new SeededRandom(3), spawnX: 0f);

        var items = SpawnMany(director, Career.Phases[2], eaten: 40, seconds: 400f);
        var powers = items.Select(i => i.Power).Where(p => p != "").Distinct().ToList();

        Assert.Contains("slow", powers);
        Assert.Contains("shield", powers);
        Assert.Contains("magnet", powers);
        Assert.Contains("goldtooth", powers);
    }

    [Fact]
    public void TheStrongestBuffsArriveGuardedByBombs()
    {
        var director = new SpawnDirector(ShippedTable(), new SeededRandom(3), spawnX: 0f);

        var items = SpawnMany(director, Career.Phases[2], eaten: 40, seconds: 400f);

        // A gold tooth is always the middle of bomb / tooth / bomb, and shield and
        // magnet always arrive directly after a bomb. Strength is paid for in what
        // surrounds it.
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Power == "goldtooth")
            {
                Assert.True(i >= 1 && i + 1 < items.Count);
                Assert.False(items[i - 1].IsEdible);
                Assert.False(items[i + 1].IsEdible);
            }

            if (items[i].Power is "shield" or "magnet")
            {
                Assert.True(i >= 1);
                Assert.False(items[i - 1].IsEdible);
            }
        }
    }

    [Fact]
    public void BuffsAreAMinorityOfWhatArrives()
    {
        var director = new SpawnDirector(ShippedTable(), new SeededRandom(21), spawnX: 0f);

        var items = SpawnMany(director, Career.Phases[2], eaten: 40, seconds: 400f);
        var buffs = items.Count(i => i.Power != "");

        // A belt made mostly of power-ups is not a timing game any more.
        Assert.True(buffs < items.Count / 5, $"{buffs} buffs out of {items.Count} items");
    }

    [Fact]
    public void CoinsCarryTheCoinPowerAndAUniqueId()
    {
        var director = new SpawnDirector(ShippedTable(), new SeededRandom(5), spawnX: 0f);

        var first = director.MakeCoin(halfWidth: 8f);
        var second = director.MakeCoin(halfWidth: 8f);

        Assert.Equal("coin", first.TypeId);
        Assert.Equal("coin", first.Power);
        Assert.True(first.IsEdible);
        Assert.NotEqual(first.Id, second.Id);
    }
}
