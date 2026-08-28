using System.Collections.Generic;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class ChompJudgeTests
{
    private static readonly JawZone Jaw = new JawZone(Center: 100f, HalfWidth: 12f);

    private static FoodItem Item(int id, float x, bool edible = true) =>
        new FoodItem(id, "hotdog", x, halfWidth: 8f, isEdible: edible, score: 10,
                     movement: Movement.Constant);

    [Fact]
    public void EmptyBeltIsAnAirChomp()
    {
        var result = ChompJudge.Judge(Jaw, new List<FoodItem>());
        Assert.Equal(ChompOutcome.Air, result.Outcome);
        Assert.Null(result.Item);
    }

    [Fact]
    public void ItemOutsideTheZoneIsAnAirChomp() =>
        Assert.Equal(ChompOutcome.Air, ChompJudge.Judge(Jaw, new List<FoodItem> { Item(1, x: 10f) }).Outcome);

    [Fact]
    public void ItemInsideTheZoneIsAHit()
    {
        var result = ChompJudge.Judge(Jaw, new List<FoodItem> { Item(1, x: 100f) });
        Assert.Equal(ChompOutcome.Hit, result.Outcome);
        Assert.Equal(1, result.Item!.Id);
    }

    [Fact]
    public void InedibleItemInTheZoneIsStillAHit()
    {
        var result = ChompJudge.Judge(Jaw, new List<FoodItem> { Item(1, x: 100f, edible: false) });
        Assert.Equal(ChompOutcome.Hit, result.Outcome);
        Assert.False(result.Item!.IsEdible);
    }

    [Fact]
    public void NearestToCentreWinsWhenTwoItemsOverlap()
    {
        var result = ChompJudge.Judge(Jaw, new List<FoodItem> { Item(1, x: 108f), Item(2, x: 102f) });
        Assert.Equal(2, result.Item!.Id);
    }

    [Fact]
    public void NearestToCentreWinsRegardlessOfListOrder()
    {
        var result = ChompJudge.Judge(Jaw, new List<FoodItem> { Item(2, x: 102f), Item(1, x: 108f) });
        Assert.Equal(2, result.Item!.Id);
    }
}
