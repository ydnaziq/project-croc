using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class BeltTests
{
    private static FoodItem Item(int id, float x, bool edible = true) =>
        new FoodItem(id, "fish", x, halfWidth: 8f, isEdible: edible, score: 10,
                     movement: Movement.Constant);

    [Fact]
    public void AdvanceMovesItemsByBeltSpeed()
    {
        var belt = new Belt(retireX: 400f);
        var item = Item(1, x: 0f);
        belt.Add(item);
        belt.Advance(beltSpeed: 100f, dt: 0.1f);
        Assert.Equal(10f, item.X, precision: 4);
    }

    [Fact]
    public void AdvanceAccumulatesItemAge()
    {
        var belt = new Belt(retireX: 400f);
        var item = Item(1, x: 0f);
        belt.Add(item);
        belt.Advance(100f, 0.1f);
        belt.Advance(100f, 0.1f);
        Assert.Equal(0.2f, item.Age, precision: 4);
    }

    [Fact]
    public void ItemsPastRetireXAreReturnedAndRemoved()
    {
        var belt = new Belt(retireX: 50f);
        belt.Add(Item(1, x: 0f));
        belt.Add(Item(2, x: 49f));

        var retired = belt.Advance(beltSpeed: 100f, dt: 0.1f);

        Assert.Single(retired);
        Assert.Equal(2, retired[0].Id);
        Assert.Single(belt.Items);
        Assert.Equal(1, belt.Items[0].Id);
    }

    [Fact]
    public void RetirementUsesTrailingEdgeSoAnItemIsFullyPastTheJaws()
    {
        var belt = new Belt(retireX: 50f);
        belt.Add(Item(1, x: 50f));
        Assert.Empty(belt.Advance(beltSpeed: 0f, dt: 0.1f));
    }

    [Fact]
    public void RemoveTakesAnItemOffTheBelt()
    {
        var belt = new Belt(retireX: 400f);
        var item = Item(1, x: 0f);
        belt.Add(item);
        belt.Remove(item);
        Assert.Empty(belt.Items);
    }

    [Fact]
    public void AdvanceOnAnEmptyBeltReturnsNothing() =>
        Assert.Empty(new Belt(retireX: 400f).Advance(100f, 0.1f));
}
