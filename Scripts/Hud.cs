using Godot;

namespace CrocGame;

public partial class Hud : Node2D
{
    private Label _label = null!;

    public override void _Ready()
    {
        _label = new Label
        {
            Position = new Vector2(4, 2),
            LabelSettings = new LabelSettings { FontSize = 12 },
        };
        AddChild(_label);
    }

    public void Set(int score, int combo, int strikes)
    {
        var pips = new string('X', strikes) + new string('.', RunStateMaxStrikes - strikes);
        _label.Text = $"{score}   x{combo}   [{pips}]";
    }

    private const int RunStateMaxStrikes = 3;
}
