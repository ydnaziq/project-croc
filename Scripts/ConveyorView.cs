using Godot;

namespace CrocGame;

/// <summary>
/// The belt itself. Purely cosmetic: it reads the speed Core is already using and
/// scrolls its treads to match, so the surface visibly carries the food rather than
/// the food appearing to float.
/// </summary>
public partial class ConveyorView : Node2D
{
    private const float BandTop = GameRoot.BeltY + 8f;   // food's lower edge rests here
    private const float BandHeight = 16f;
    private const float TreadSpacing = 12f;

    private static readonly Color BandColor = new("2a2a3a");
    private static readonly Color SurfaceColor = new("4a4a5e");
    private static readonly Color TreadColor = new("1a1a26");
    private static readonly Color RimColor = new("6a6a82");

    private float _scroll;

    /// <summary>Advance the treads by the same speed the items are moving.</summary>
    public void Advance(float beltSpeed, float dt)
    {
        _scroll = (_scroll + beltSpeed * dt) % TreadSpacing;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var width = GameRoot.ViewportWidth;

        DrawRect(new Rect2(0, BandTop, width, BandHeight), BandColor);
        DrawRect(new Rect2(0, BandTop, width, 3f), SurfaceColor);
        DrawLine(new Vector2(0, BandTop), new Vector2(width, BandTop), RimColor);

        // Treads scroll with the belt. Start one spacing off-screen left so a tread
        // entering the frame does not pop into existence at x = 0.
        for (var x = _scroll - TreadSpacing; x < width; x += TreadSpacing)
        {
            DrawRect(new Rect2(x, BandTop + 3f, 2f, BandHeight - 3f), TreadColor);
        }
    }
}
