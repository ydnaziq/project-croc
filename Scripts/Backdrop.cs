using Godot;

namespace CrocGame;

/// <summary>
/// The arena, drawn from one authored image (Art/Tools/arena_gen.py) plus a few
/// animated lights on top.
///
/// It used to tile a single brick tile across the whole screen. A 16x16 pattern
/// repeated two hundred times has no structure above 16px, which is why it read as
/// noise. One image with real shapes - bunting, a banner, a stage shelf, a crowd, a
/// plank floor - gives the eye somewhere to land.
/// </summary>
public partial class Backdrop : Node2D
{
    private static readonly Color BulbOn = new("f8d878");
    private static readonly Color BulbOff = new("a88030");
    private static readonly Color Ink = new("000000");

    private Texture2D? _arena;
    private float _time;

    private int _phase;

    /// <summary>
    /// The arena changes between acts, so which phase this is can be read without a
    /// word. Every variant stays darker and flatter than the belt and the croc - the
    /// background has to lose to the foreground, whatever else it is doing.
    /// </summary>
    public void SetPhase(int index)
    {
        if (_phase == index) return;

        _phase = index;
        QueueRedraw();
    }

    /// <summary>Wash laid over the arena, one per act: plain, dimmed, then hot.</summary>
    private Color PhaseWash => _phase switch
    {
        0 => new Color(0f, 0f, 0f, 0f),
        1 => new Color("100818", 0.35f),
        _ => new Color("380810", 0.30f),
    };

    public override void _Ready()
    {
        ZIndex = -10;

        _arena = ResourceLoader.Load<Texture2D>("res://Art/ExportedSprites/arena.png");
        if (_arena is null) GD.PushError("Missing arena.png; run Art/Tools/arena_gen.py.");
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_arena is not null)
        {
            DrawTextureRect(_arena, new Rect2(0, 0, GameRoot.ViewportWidth, GameRoot.ViewportHeight), false);
        }

        // The act's wash. Each phase sits at a different value so the change of act is
        // visible without a word, while everything stays behind the foreground.
        var wash = PhaseWash;
        if (wash.A > 0f)
        {
            DrawRect(new Rect2(0, 0, GameRoot.ViewportWidth, GameRoot.ViewportHeight), wash);
        }

        DrawBulbs();
    }

    /// <summary>
    /// Marquee bulbs along the stage shelf, chasing in sequence. The backdrop is
    /// otherwise completely still, and one moving thing keeps it from feeling dead.
    /// </summary>
    private void DrawBulbs()
    {
        const int count = 9;

        // The marquee chases faster each act, so the arena itself gets more urgent.
        var phase = (int)(_time * (6f + _phase * 3f));

        for (var i = 0; i < count; i++)
        {
            var x = 6 + i * 20;
            // More bulbs lit as the bout escalates: a filling house.
            var lit = (i + phase) % (3 - Mathf.Min(_phase, 1)) == 0;

            DrawRect(new Rect2(x - 1, 112, 5, 5), Ink);
            DrawRect(new Rect2(x, 113, 3, 3), lit ? BulbOn : BulbOff);
        }
    }
}
