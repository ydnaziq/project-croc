using Godot;

namespace CrocGame;

/// <summary>A score number that pops from the jaws, rises, and fades. Frees itself.</summary>
public partial class ComboPopup : Node2D
{
    private const float LifeSeconds = 0.65f;
    private const float RiseDistance = 24f;

    private Label _label = null!;
    private float _age;
    private Vector2 _origin;
    private string _text = "";
    private Color _tint = Ui.Paper;
    private int _fontSize = Ui.Small;

    public static ComboPopup Create(Vector2 position, int score, int combo, bool duringFrenzy)
    {
        var popup = new ComboPopup { Position = position };
        popup._origin = position;
        popup._text = combo >= 2 ? $"+{score} x{combo}" : $"+{score}";

        // A frenzy bite should look different from a good bite, not just louder.
        popup._tint = duringFrenzy ? Ui.Red : combo >= 5 ? Ui.Gold : Ui.Paper;
        popup._fontSize = duringFrenzy || combo >= 5 ? Ui.Body : Ui.Small;

        return popup;
    }

    public override void _Ready()
    {
        ZIndex = 25;

        _label = new Label
        {
            Text = _text,
            Position = new Vector2(-52f, -9),
            Size = new Vector2(104f, 20),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = Ui.Text(_fontSize, _tint),
        };
        AddChild(_label);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;

        var t = _age / LifeSeconds;
        if (t >= 1f)
        {
            QueueFree();
            return;
        }

        // Fast at first, then drifting: a pop rather than a constant slide.
        Position = _origin with { Y = _origin.Y - RiseDistance * Mathf.Sqrt(t) };
        Modulate = Colors.White with { A = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f };
    }
}
