using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class JawZoneTests
{
    private static FoodItem Item(float x) =>
        new FoodItem(id: 1, typeId: "fish", x: x, halfWidth: 8f,
                     isEdible: true, score: 10, movement: Movement.Constant);

    private static readonly JawZone Jaw = new JawZone(Center: 100f, HalfWidth: 12f);

    [Fact]
    public void ItemAtCenterOverlaps() => Assert.True(Jaw.Overlaps(Item(100f)));

    [Theory]
    [InlineData(80f)]
    [InlineData(120f)]
    public void ItemExactlyTouchingOverlaps(float x) => Assert.True(Jaw.Overlaps(Item(x)));

    [Theory]
    [InlineData(79.9f)]
    [InlineData(120.1f)]
    public void ItemJustOutsideDoesNotOverlap(float x) => Assert.False(Jaw.Overlaps(Item(x)));

    [Fact]
    public void DistanceFromCenterIsAbsolute()
    {
        Assert.Equal(5f, Jaw.DistanceFromCenter(Item(105f)), precision: 4);
        Assert.Equal(5f, Jaw.DistanceFromCenter(Item(95f)), precision: 4);
    }
}
