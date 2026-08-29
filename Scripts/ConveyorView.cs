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
    private const float BandHeight = 10f;
    private const float TreadSpacing = 12f;

    private static readonly Color BandColor = new("2a2a3a");
    private static readonly Color SurfaceColor = new("4a4a5e");
    private static readonly Color TreadColor = new("1a1a26");
    private static readonly Color RimColor = new("6a6a82");
    private static readonly Color ZoneIdle = new("58f8d8");
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

    /// <summary>
    /// The bite zone's real half-width, handed over each frame by the phase itself.
    /// Drawing it from the judge's own number rather than from a shared constant is
    /// what lets Hunger widen the window without the window lying about its size.
    /// </summary>
    public void SetJawHalfWidth(float halfWidth)
    {
        if (Mathf.IsEqualApprox(_jawHalfWidth, halfWidth)) return;

        _jawHalfWidth = halfWidth;
        QueueRedraw();
    }

    private float _jawHalfWidth = GameRoot.JawHalfWidth;

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

        DrawRect(new Rect2(0, BandTop, width, BandHeight), BandColor.Lerp(heat, _frenzy * 0.22f));
        DrawRect(new Rect2(0, BandTop, width, 3f), SurfaceColor.Lerp(new Color("f8d878"), _frenzy * 0.55f));
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
        var left = GameRoot.JawCenterX - _jawHalfWidth;
        var right = GameRoot.JawCenterX + _jawHalfWidth;

        // Tall enough to frame the food itself, which sits centred on the belt line.
        var top = GameRoot.BeltY - 10f;
        var bottom = BandTop + 3f;
        var height = bottom - top;

        var colour = ZoneIdle.Lerp(ZoneHot, _occupiedGlow);

        // Each post is a bright bar on a black backing. Against the croc's own pale
        // mouth a dim grey post disappears into the sprite, so this is deliberately
        // the highest-contrast thing on the belt.
        DrawRect(new Rect2(left - 2f, top - 1f, 4f, height + 2f), Ui.Ink);
        DrawRect(new Rect2(right - 2f, top - 1f, 4f, height + 2f), Ui.Ink);
        DrawRect(new Rect2(left - 1f, top, 2f, height), colour);
        DrawRect(new Rect2(right - 1f, top, 2f, height), colour);

        // Feet turning inward at top and bottom, so the pair reads as one bracket.
        foreach (var y in new[] { top, bottom - 2f })
        {
            DrawRect(new Rect2(left - 1f, y - 1f, 6f, 4f), Ui.Ink);
            DrawRect(new Rect2(right - 5f, y - 1f, 6f, 4f), Ui.Ink);
            DrawRect(new Rect2(left, y, 4f, 2f), colour);
            DrawRect(new Rect2(right - 4f, y, 4f, 2f), colour);
        }

        // The floor of the window lights as an item crosses it.
        if (_occupiedGlow > 0.01f)
        {
            DrawRect(new Rect2(left + 1f, BandTop, right - left - 2f, 3f),
                     ZoneHot with { A = _occupiedGlow * 0.85f });
        }
    }
}
