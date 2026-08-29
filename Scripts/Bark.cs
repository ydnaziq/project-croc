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
    private const int MaxCharsPerLine = 22;

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

        // Wrap by hand before building the label. A Godot Label clamps its own Size up
        // to whatever its content needs, so sizing a panel from a width you *asked*
        // for is unreliable - the label wins and the text spills out of the bubble.
        var wrapped = Wrap(_text, MaxCharsPerLine);

        _label = new Label
        {
            Text = wrapped,
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = Ui.Text(Ui.Small, TextColor, outline: false),
        };
        AddChild(_label);

        // Ask the label how big it actually is, then build the bubble around that.
        var needed = _label.GetCombinedMinimumSize();
        _width = Mathf.Max(44f, needed.X + 12f);
        _height = Mathf.Max(16f, needed.Y + 6f);

        _label.Size = needed;
        _label.Position = new Vector2(-needed.X / 2f, -7f - _height + (_height - needed.Y) / 2f);
    }

    /// <summary>Breaks a line on word boundaries so a long taunt becomes two rows.</summary>
    private static string Wrap(string text, int limit)
    {
        var words = text.Split(' ');
        var lines = new System.Collections.Generic.List<string>();
        var line = "";

        foreach (var word in words)
        {
            if (line.Length == 0) line = word;
            else if (line.Length + 1 + word.Length <= limit) line += " " + word;
            else { lines.Add(line); line = word; }
        }

        if (line.Length > 0) lines.Add(line);
        return string.Join("\n", lines);
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
            _label.Position = _label.Position with { X = -_label.Size.X / 2f + shake };
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
