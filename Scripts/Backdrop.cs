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

        DrawBulbs();
    }

    /// <summary>
    /// Marquee bulbs along the stage shelf, chasing in sequence. The backdrop is
    /// otherwise completely still, and one moving thing keeps it from feeling dead.
    /// </summary>
    private void DrawBulbs()
    {
        const int count = 9;
        var phase = (int)(_time * 6f);

        for (var i = 0; i < count; i++)
        {
            var x = 6 + i * 20;
            var lit = (i + phase) % 3 == 0;

            DrawRect(new Rect2(x - 1, 112, 5, 5), Ink);
            DrawRect(new Rect2(x, 113, 3, 3), lit ? BulbOn : BulbOff);
        }
    }
}
