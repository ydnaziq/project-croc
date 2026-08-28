using System;

namespace CrocGame.Core;

/// <summary>The bite window, as a one-dimensional interval on the belt's X axis.</summary>
public readonly record struct JawZone(float Center, float HalfWidth)
{
    /// <summary>
    /// True when the item's interval touches or crosses the jaw interval.
    /// Touching counts: a bite that grazes the edge of the food is a bite.
    /// </summary>
    public bool Overlaps(FoodItem item) =>
        MathF.Abs(item.X - Center) <= HalfWidth + item.HalfWidth;

    public float DistanceFromCenter(FoodItem item) => MathF.Abs(item.X - Center);
}
