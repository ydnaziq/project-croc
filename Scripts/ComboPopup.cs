using Godot;

namespace CrocGame;

/// <summary>A score number that floats up from the jaws and fades. Frees itself.</summary>
public partial class ComboPopup : Node2D
{
    private const float LifeSeconds = 0.6f;
    private const float RiseDistance = 18f;

    private Label _label = null!;
    private float _age;
    private Vector2 _origin;

    public static ComboPopup Create(Vector2 position, int score, int combo)
    {
        var popup = new ComboPopup { Position = position };
        popup._origin = position;
        popup._text = combo >= 2 ? $"+{score}  x{combo}" : $"+{score}";
        popup._tint = combo >= 5 ? new Color("f8d878") : Colors.White;
        return popup;
    }

    private string _text = "";
    private Color _tint = Colors.White;

    public override void _Ready()
    {
        _label = new Label
        {
            Text = _text,
            Position = new Vector2(-24, -8),
            Size = new Vector2(48, 12),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 9, FontColor = _tint },
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

        Position = _origin with { Y = _origin.Y - RiseDistance * t };
        Modulate = Colors.White with { A = 1f - t };
    }
}
