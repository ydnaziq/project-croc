using Godot;

namespace CrocGame;

/// <summary>
/// The belt itself, and the bite window marked on it.
///
/// The treads scroll at the speed Core is actually using, so the surface visibly
/// carries the food rather than the food appearing to float. The bracket pair marks
/// the exact jaw zone the judge tests against - previously the player had to infer
/// where the croc's mouth ended from the sprite, which is the single biggest reason
/// the timing felt unfair rather than hard.
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
    private static readonly Color ZoneIdle = new("6a6a82");
    private static readonly Color ZoneHot = new("58d854");

    private float _scroll;
    private float _frenzy;
    private bool _occupied;
    private float _occupiedGlow;

    /// <summary>Advance the treads by the same speed the items are moving.</summary>
    public void Advance(float beltSpeed, float dt)
    {
        _scroll = (_scroll + beltSpeed * dt) % TreadSpacing;
        QueueRedraw();
    }

    /// <summary>0..1. Tints the belt hot while the frenzy runs.</summary>
    public void SetFrenzy(float amount) => _frenzy = amount;

    /// <summary>Whether something is inside the jaws right now, so the zone can light up.</summary>
    public void SetZoneOccupied(bool occupied) => _occupied = occupied;

    public override void _Process(double delta)
    {
        // Eases rather than snaps, so a fast item does not make the zone strobe.
        _occupiedGlow = Mathf.MoveToward(_occupiedGlow, _occupied ? 1f : 0f, (float)delta * 9f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var width = GameRoot.ViewportWidth;
        var heat = new Color("f83800");

        DrawRect(new Rect2(0, BandTop, width, BandHeight), BandColor.Lerp(heat, _frenzy * 0.5f));
        DrawRect(new Rect2(0, BandTop, width, 3f), SurfaceColor.Lerp(new Color("f8d878"), _frenzy));
        DrawLine(new Vector2(0, BandTop), new Vector2(width, BandTop), RimColor);

        // Treads scroll with the belt. Start one spacing off-screen left so a tread
        // entering the frame does not pop into existence at x = 0.
        for (var x = _scroll - TreadSpacing; x < width; x += TreadSpacing)
        {
            DrawRect(new Rect2(x, BandTop + 3f, 2f, BandHeight - 3f), TreadColor);
        }

        DrawBiteZone();
    }

    /// <summary>
    /// Two brackets at the exact edges of the jaw zone, brightening when something is
    /// inside them. Drawn from the same constants the judge uses, so what lights up is
    /// exactly what scores.
    /// </summary>
    private void DrawBiteZone()
    {
        var left = GameRoot.JawCenterX - GameRoot.JawHalfWidth;
        var right = GameRoot.JawCenterX + GameRoot.JawHalfWidth;
        var top = BandTop - 2f;
        var colour = ZoneIdle.Lerp(ZoneHot, _occupiedGlow);

        // Uprights at the window edges, each backed in black to keep the 1px rule.
        DrawRect(new Rect2(left - 1f, top - 4f, 1f, 8f), Ui.Ink);
        DrawRect(new Rect2(right, top - 4f, 1f, 8f), Ui.Ink);
        DrawRect(new Rect2(left, top - 4f, 1f, 8f), colour);
        DrawRect(new Rect2(right - 1f, top - 4f, 1f, 8f), colour);

        // Feet turning inward, so the pair reads as one bracket instead of two posts.
        DrawRect(new Rect2(left, top - 4f, 3f, 1f), colour);
        DrawRect(new Rect2(right - 3f, top - 4f, 3f, 1f), colour);

        // The floor of the window lights as the item crosses it.
        if (_occupiedGlow > 0.01f)
        {
            DrawRect(new Rect2(left, BandTop, right - left, 2f),
                     ZoneHot with { A = _occupiedGlow * 0.75f });
        }
    }
}
