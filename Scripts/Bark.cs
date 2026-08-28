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
    private float _height = 16f;
    private Mood _mood = Mood.Smug;
    private string _text = "";

    public static Bark Create(Vector2 position, string text, Mood mood) =>
        new() { Position = position, _text = text, _mood = mood };

    public override void _Ready()
    {
        ZIndex = 22;

        // Measured, not estimated: the bubble is built around the text.
        const float maxWidth = GameRoot.ViewportWidth - 24f;
        var measured = Ui.Measure(_text, Ui.Small);

        _width = Mathf.Clamp(measured.X + 12f, 40f, maxWidth);

        _label = Ui.WrappedLabel(_text, Ui.Small, TextColor, _width - 10f, HorizontalAlignment.Center);
        _label.Position = new Vector2(-_width / 2f + 5f, -22f);
        AddChild(_label);

        // Once it is in the tree the label knows how many lines it wrapped to, so the
        // bubble can grow to fit rather than clipping.
        _height = Mathf.Max(16f, Ui.WrappedHeight(_label) + 6f);
        _label.Position = new Vector2(-_width / 2f + 5f, -8f - _height);
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
            _label.Position = new Vector2(-_width / 2f + 5f + shake, -8f - _height);
        }

        Modulate = Colors.White with { A = _age > LifeSeconds - 0.35f ? (LifeSeconds - _age) / 0.35f : 1f };
        QueueRedraw();
    }

    public override void _Draw()
    {
        var pop = Mathf.Min(1f, _age / 0.09f);          // snaps open rather than fading in
        var w = _width * pop;
        var h = _height * pop;

        Ui.Panel(this, new Rect2(-w / 2f, -10f - h, w, h), BubbleFill);

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
