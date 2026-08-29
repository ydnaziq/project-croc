using Godot;

namespace CrocGame;

/// <summary>
/// Strikes, drawn as the croc's teeth.
///
/// This replaces three small squares that filled in red as strikes were *spent*, which
/// read backwards: a lit pip looks like something you have, not something you lost. So
/// the meaning is inverted now - a tooth you still own is bright and present, a lost
/// one is a dark socket with a crack through it - and losing one is animated, because
/// the single most important state change in the match was previously a silent colour
/// swap on an 8px square.
/// </summary>
public partial class StrikeMeter : Node2D
{
    private const int MaxTeeth = 3;
    private const float ToothWidth = 13f;
    private const float ToothHeight = 16f;
    private const float Gap = 3f;

    private static readonly Color Enamel = new("f8f8f8");
    private static readonly Color EnamelShade = new("c8c8d8");
    private static readonly Color Socket = new("2a1a1a");
    private static readonly Color Crack = new("f83800");
    private static readonly Color Gum = new("a83858");

    private int _lost;
    private float _breakAge = 1f;
    private int _breaking = -1;
    private readonly RandomNumberGenerator _rng = new();

    /// <summary>Sets how many strikes are spent, animating any newly lost tooth.</summary>
    public void SetStrikes(int strikes)
    {
        if (strikes == _lost) return;

        if (strikes > _lost)
        {
            _breaking = Mathf.Min(strikes, MaxTeeth) - 1;
            _breakAge = 0f;
        }

        _lost = strikes;
        QueueRedraw();
    }

    /// <summary>
    /// A shield rides as a fourth tooth, green so it is plainly not one of the three.
    /// Answering "what am I carrying" through the meter that already exists beats a new
    /// indicator, and it reads the right way round: a present tooth is something you
    /// have, not something you have spent.
    /// </summary>
    public void SetShield(bool has)
    {
        if (_shield == has) return;

        _shield = has;
        QueueRedraw();
    }

    private bool _shield;

    public void Reset()
    {
        _lost = 0;
        _breaking = -1;
        _breakAge = 1f;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_breakAge >= 1f) return;

        _breakAge = Mathf.Min(1f, _breakAge + (float)delta * 2.2f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Gum line the teeth sit in, so they read as a mouth rather than three boxes.
        var totalWidth = MaxTeeth * ToothWidth + (MaxTeeth - 1) * Gap;
        Ui.Panel(this, new Rect2(-2, -3, totalWidth + 4, 7), Gum);

        for (var i = 0; i < MaxTeeth; i++)
        {
            var x = i * (ToothWidth + Gap);
            var lost = i < _lost;
            var breaking = i == _breaking && _breakAge < 1f;

            if (breaking) DrawBreaking(x);
            else if (lost) DrawSocket(x);
            else DrawTooth(x, 0f);
        }

        if (!_shield) return;

        var shieldX = MaxTeeth * (ToothWidth + Gap);
        DrawTooth(shieldX, 0f);
        DrawRect(new Rect2(shieldX + 1f, 3f, ToothWidth - 2f, ToothHeight - 2f),
                 new Color(Ui.Green, 0.55f));
    }

    private void DrawTooth(float x, float offsetY)
    {
        var rect = new Rect2(x, 2f + offsetY, ToothWidth, ToothHeight);
        Ui.Panel(this, rect, Enamel);

        // Shading down the right edge and a rounded root, so it is a tooth shape and
        // not a white rectangle.
        DrawRect(new Rect2(rect.End.X - 4f, rect.Position.Y + 1f, 3f, rect.Size.Y - 2f), EnamelShade);
        DrawRect(new Rect2(rect.Position.X + 1f, rect.End.Y - 2f, 2f, 1f), Ui.Ink);
        DrawRect(new Rect2(rect.End.X - 3f, rect.End.Y - 2f, 2f, 1f), Ui.Ink);
    }

    private void DrawSocket(float x)
    {
        var rect = new Rect2(x, 2f, ToothWidth, ToothHeight);
        Ui.Panel(this, rect, Socket);

        // A jagged crack, so a lost tooth is legible even in one colour.
        for (var i = 0; i < 5; i++)
        {
            var cx = rect.Position.X + 4f + (i % 2 == 0 ? 0f : 3f);
            DrawRect(new Rect2(cx, rect.Position.Y + 3f + i * 2.5f, 2f, 2f), Crack);
        }
    }

    /// <summary>The moment of loss: the tooth jolts, tips, and drops out of frame.</summary>
    private void DrawBreaking(float x)
    {
        DrawSocket(x);

        var t = _breakAge;
        var fall = t * t * 26f;
        var jitter = t < 0.25f ? _rng.RandfRange(-1.5f, 1.5f) : 0f;

        DrawSetTransform(new Vector2(x + jitter, fall), t * 0.9f, Vector2.One);
        DrawTooth(0f, 0f);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
