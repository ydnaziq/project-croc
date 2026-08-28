using Godot;

namespace CrocGame;

/// <summary>Full-screen title and game-over text. No buttons: any chomp input advances.</summary>
public partial class ScreenOverlay : Node2D
{
    private Label _title = null!;
    private Label _subtitle = null!;

    public override void _Ready()
    {
        _title = new Label
        {
            Position = new Vector2(0, 60),
            Size = new Vector2(320, 20),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 16 },
        };
        AddChild(_title);

        _subtitle = new Label
        {
            Position = new Vector2(0, 90),
            Size = new Vector2(320, 20),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 10 },
        };
        AddChild(_subtitle);
    }

    public void Show(string title, string subtitle)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        Visible = true;
    }
}
