using Godot;

namespace CrocGame;

/// <summary>
/// What frenzy looks like: a hot wash over the arena and horizontal speed lines tearing
/// across it. Drawn rather than tweened so it can key off the same fraction the meter
/// uses and die the instant the frenzy does.
/// </summary>
public partial class FrenzyOverlay : Node2D
{
    private static readonly Color Wash = new("f83800");
    private static readonly Color Line = new("f8d878");

    private float _amount;
    private float _scroll;
    private readonly RandomNumberGenerator _rng = new();
    private readonly float[] _lineY = new float[7];

    public override void _Ready()
    {
        ZIndex = 15;
        for (var i = 0; i < _lineY.Length; i++) _lineY[i] = _rng.RandfRange(0f, GameRoot.ViewportHeight);
    }

    public void SetAmount(float amount) => _amount = amount;

    public override void _Process(double delta)
    {
        if (_amount <= 0f)
        {
            if (Visible) { Visible = false; QueueRedraw(); }
            return;
        }

        Visible = true;
        _scroll += (float)delta * 900f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_amount <= 0f) return;

        var w = GameRoot.ViewportWidth;
        var h = GameRoot.ViewportHeight;

        // Edge wash: strongest at the top and bottom, clear through the middle so it
        // never fights with the food the player is trying to read.
        var strength = _amount * 0.30f;
        for (var i = 0; i < 6; i++)
        {
            var a = strength * (1f - i / 6f);
            DrawRect(new Rect2(0, i * 5, w, 5), Wash with { A = a });
            DrawRect(new Rect2(0, h - (i + 1) * 5, w, 5), Wash with { A = a });
        }

        // Speed lines.
        for (var i = 0; i < _lineY.Length; i++)
        {
            var x = (_scroll * (0.6f + 0.1f * i)) % (w + 60f) - 60f;
            DrawRect(new Rect2(x, _lineY[i], 26f + i * 4f, 1f), Line with { A = _amount * 0.5f });
        }
    }
}
