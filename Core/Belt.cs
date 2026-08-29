using System.Collections.Generic;

namespace CrocGame.Core;

/// <summary>
/// Holds the items currently riding past the croc, advances them, and reports the
/// ones that have travelled fully past the jaws.
/// </summary>
public sealed class Belt
{
    private readonly List<FoodItem> _items = new();
    private readonly List<FoodItem> _retiredThisFrame = new();

    public Belt(float retireX) => RetireX = retireX;

    /// <summary>An item is retired once its trailing edge passes this X.</summary>
    public float RetireX { get; }

    public IReadOnlyList<FoodItem> Items => _items;

    public void Add(FoodItem item) => _items.Add(item);

    public void Remove(FoodItem item) => _items.Remove(item);

    /// <summary>Clears the belt. A knockout stops the phase dead.</summary>
    public void Clear() => _items.Clear();

    public IReadOnlyList<FoodItem> Advance(float beltSpeed, float dt)
    {
        _retiredThisFrame.Clear();

        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            item.X += item.Movement.DeltaX(beltSpeed, dt, item.Age);
            item.Age += dt;

            if (item.X - item.HalfWidth > RetireX)
            {
                _retiredThisFrame.Add(item);
                _items.RemoveAt(i);
            }
        }

        return _retiredThisFrame;
    }
}
