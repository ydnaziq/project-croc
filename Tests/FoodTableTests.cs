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
}
