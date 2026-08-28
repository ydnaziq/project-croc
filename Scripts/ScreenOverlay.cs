using Godot;

namespace CrocGame;

/// <summary>
/// Title, taunt, and result text, plus the transient banner used for FRENZY.
/// No buttons: a press anywhere advances, which is the same verb the game runs on.
/// </summary>
public partial class ScreenOverlay : Node2D
{
    private Label _title = null!;
    private Label _subtitle = null!;
    private Label _banner = null!;
    private float _bannerAge;

    public override void _Ready()
    {
        ZIndex = 28;

        _title = new Label
        {
            Position = new Vector2(0, 130),
            Size = new Vector2(GameRoot.ViewportWidth, 24),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 20, FontColor = new Color("f8f8f8") },
        };
        AddChild(_title);

        _subtitle = new Label
        {
            Position = new Vector2(0, 162),
            Size = new Vector2(GameRoot.ViewportWidth, 46),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 9, FontColor = new Color("c8c8d8") },
        };
        AddChild(_subtitle);

        _banner = new Label
        {
            Position = new Vector2(0, 100),
            Size = new Vector2(GameRoot.ViewportWidth, 24),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 18, FontColor = new Color("f8d878") },
            Visible = false,
        };
        AddChild(_banner);
    }

    public void Show(string title, string subtitle)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        _title.Visible = true;
        _subtitle.Visible = true;
        QueueRedraw();
    }

    public new void Hide()
    {
        _title.Visible = false;
        _subtitle.Visible = false;
        QueueRedraw();
    }

    /// <summary>A banner that announces itself and gets out of the way.</summary>
    public void Flash(string text)
    {
        _banner.Text = text;
        _banner.Visible = true;
        _bannerAge = 0f;
    }

    public override void _Process(double delta)
    {
        if (!_banner.Visible) return;

        _bannerAge += (float)delta;

        if (_bannerAge >= 1.1f)
        {
            _banner.Visible = false;
            return;
        }

        var pop = _bannerAge < 0.12f ? _bannerAge / 0.12f : 1f;
        _banner.Scale = Vector2.One * (0.6f + 0.4f * pop);
        _banner.Position = new Vector2(
            -GameRoot.ViewportWidth * (_banner.Scale.X - 1f) / 2f, 100);
        _banner.Modulate = Colors.White with { A = _bannerAge > 0.8f ? 1f - (_bannerAge - 0.8f) / 0.3f : 1f };
    }

    public override void _Draw()
    {
        if (!_title.Visible) return;

        // A dim band behind the text so it stays readable over the arena.
        DrawRect(new Rect2(0, 120, GameRoot.ViewportWidth, 96), new Color("101018", 0.82f));
    }
}
