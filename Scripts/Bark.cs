using Godot;

namespace CrocGame;

/// <summary>
/// A speech bubble over the rival.
///
/// The rival reacting is the cheapest way to make a score number feel like an
/// opponent: the same points landing while something on screen panics reads
/// completely differently from the same points landing in silence.
/// </summary>
public partial class Bark : Node2D
{
    public enum Mood { Smug, Rattled, Panicked }

    private const float LifeSeconds = 2.0f;

    private Label _label = null!;
    private float _age;
    private float _width;
    private Mood _mood = Mood.Smug;
    private string _text = "";

    public static Bark Create(Vector2 position, string text, Mood mood) =>
        new() { Position = position, _text = text, _mood = mood };

    public override void _Ready()
    {
        ZIndex = 22;

        // Silkscreen is a fixed-width pixel font, so the bubble can be sized from the
        // character count instead of measuring at runtime.
        _width = Mathf.Max(40f, _text.Length * 5f + 12f);

        _label = new Label
        {
            Text = _text,
            Position = new Vector2(-_width / 2f + 6f, -22f),
            Size = new Vector2(_width - 12f, 12f),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = Ui.Text(Ui.Small, TextColor, outline: false),
        };
        AddChild(_label);
    }

    private Color TextColor => _mood switch
    {
        Mood.Panicked => Ui.Red,
        Mood.Rattled => new Color("f8d878"),
        _ => Ui.Ink,
    };

    private Color BubbleFill => _mood switch
    {
        Mood.Panicked => new Color("281018"),
        Mood.Rattled => new Color("2a2418"),
        _ => Ui.Paper,
    };

    public override void _Process(double delta)
    {
        _age += (float)delta;

        if (_age >= LifeSeconds)
        {
            QueueFree();
            return;
        }

        // A panicked rival's bubble shakes.
        if (_mood == Mood.Panicked)
        {
            var shake = Mathf.Sin(_age * 40f) * 1f;
            _label.Position = new Vector2(-_width / 2f + 6f + shake, -22f);
        }

        Modulate = Colors.White with { A = _age > LifeSeconds - 0.35f ? (LifeSeconds - _age) / 0.35f : 1f };
        QueueRedraw();
    }

    public override void _Draw()
    {
        var pop = Mathf.Min(1f, _age / 0.09f);          // snaps open rather than fading in
        var w = _width * pop;
        var h = 16f * pop;

        Ui.Panel(this, new Rect2(-w / 2f, -24f, w, h), BubbleFill);

        if (pop < 1f) return;

        // Tail, pointing down at whoever is talking. Drawn as stepped 1px rows so it
        // keeps the same edge weight as the box.
        for (var i = 0; i < 4; i++)
        {
            DrawRect(new Rect2(-3f + i, -8f + i, 7f - i * 2f, 1f), Ui.Ink);
            if (i < 3) DrawRect(new Rect2(-2f + i, -8f + i, 5f - i * 2f, 1f), BubbleFill);
        }
    }
}
