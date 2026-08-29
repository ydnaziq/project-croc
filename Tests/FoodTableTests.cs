using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class FoodTableTests
{
    private const string Json = """
    [
      { "id": "hotdog", "width": 16, "edible": true,  "movement": "constant", "score": 10, "minEatenToAppear": 0 },
      { "id": "donut",  "width": 16, "edible": true,  "movement": "constant", "score": 15, "minEatenToAppear": 5 },
      { "id": "boot",   "width": 16, "edible": false, "movement": "stutter",  "score": 0,  "minEatenToAppear": 25 }
    ]
    """;

    [Fact]
    public void ParsesEveryEntry() => Assert.Equal(3, FoodTable.FromJson(Json).Types.Count);

    [Fact]
    public void ReadsFieldsOffAnEntry()
    {
        var hotdog = FoodTable.FromJson(Json).Types.Single(t => t.Id == "hotdog");
        Assert.Equal(16f, hotdog.Width, precision: 2);
        Assert.True(hotdog.Edible);
        Assert.Equal("constant", hotdog.Movement);
        Assert.Equal(10, hotdog.Score);
        Assert.Equal(0, hotdog.MinEatenToAppear);
    }

    [Fact]
    public void AvailableGatesOnMinEatenToAppear()
    {
        var table = FoodTable.FromJson(Json);
        Assert.Single(table.Available(eaten: 0, edible: true));
        Assert.Equal(2, table.Available(eaten: 5, edible: true).Count);
    }

    [Fact]
    public void AvailableSeparatesEdibleFromInedible()
    {
        var table = FoodTable.FromJson(Json);
        Assert.All(table.Available(eaten: 100, edible: true), t => Assert.True(t.Edible));
        Assert.All(table.Available(eaten: 100, edible: false), t => Assert.False(t.Edible));
    }

    [Fact]
    public void AvailableIsEmptyWhenNothingQualifiesYet() =>
        Assert.Empty(FoodTable.FromJson(Json).Available(eaten: 0, edible: false));

    [Fact]
    public void SeededRandomIsReproducible()
    {
        var a = new SeededRandom(1234);
        var b = new SeededRandom(1234);
        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(a.NextFloat(), b.NextFloat(), precision: 6);
            Assert.Equal(a.NextInt(100), b.NextInt(100));
        }
    }

    [Fact]
    public void SeededRandomStaysInRange()
    {
        var rng = new SeededRandom(99);
        for (var i = 0; i < 500; i++)
        {
            Assert.InRange(rng.NextFloat(), 0f, 1f);
            Assert.InRange(rng.NextInt(7), 0, 6);
        }
    }

    [Fact]
    public void RowsWithoutAPowerColumnAreOrdinaryFood()
    {
        var table = FoodTable.FromJson(
            """[{ "id":"pizza","width":16,"edible":true,"movement":"constant","score":10,"minEatenToAppear":0 }]""");

        Assert.Equal("", table.Types[0].Power);
    }

    [Fact]
    public void PowerRowsAreReadAndAreNotOfferedAsOrdinaryFood()
    {
        var table = FoodTable.FromJson(
            """
            [
              { "id":"pizza","width":16,"edible":true,"movement":"constant","score":10,"minEatenToAppear":0 },
              { "id":"slow","width":16,"edible":true,"movement":"constant","score":0,"minEatenToAppear":0,"power":"slow" }
            ]
            """);

        Assert.Equal("slow", table.Types[1].Power);

        // Available() feeds ordinary spawning, which must never pick a buff by accident.
        Assert.Single(table.Available(eaten: 0, edible: true));
        Assert.Equal("pizza", table.Available(eaten: 0, edible: true)[0].Id);

        var powers = table.PowerUps(eaten: 0);
        Assert.Single(powers);
        Assert.Equal("slow", powers[0].Id);
    }

    [Fact]
    public void PowerUpsRespectMinEatenToAppear()
    {
        var table = FoodTable.FromJson(
            """[{ "id":"magnet","width":10,"edible":true,"movement":"constant","score":0,"minEatenToAppear":6,"power":"magnet" }]""");

        Assert.Empty(table.PowerUps(eaten: 5));
        Assert.Single(table.PowerUps(eaten: 6));
    }

    [Fact]
    public void TheShippedTableDefinesAllFourBuffs()
    {
        var table = FoodTable.FromJson(System.IO.File.ReadAllText(
            System.IO.Path.Combine(RepoRoot(), "Resources", "food.json")));

        var powers = table.Types.Where(t => t.Power != "").Select(t => t.Power).ToList();

        Assert.Contains("slow", powers);
        Assert.Contains("shield", powers);
        Assert.Contains("magnet", powers);
        Assert.Contains("goldtooth", powers);
        Assert.All(table.Types.Where(t => t.Power != ""),
                   t => Assert.NotNull(PowerUp.Parse(t.Power)));
    }

    /// <summary>Walks up from the test binary to the repo root, so the shipped
    /// food.json is checked rather than a copy that can drift from it.</summary>
    internal static string RepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);

        while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CrocGame.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
