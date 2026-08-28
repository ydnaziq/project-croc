using Godot;

namespace CrocGame;

/// <summary>
/// Title, round cards, and results, presented on a framed panel rather than a dim
/// wash. Also owns the transient banner used for FRENZY.
///
/// Everything here is built from Ui.Panel so the interface carries the same 1px black
/// edge as the sprites do.
/// </summary>
public partial class ScreenOverlay : Node2D
{
    private const float CardY = 120f;
    private const float CardHeight = 92f;

    private Label _title = null!;
    private Label _subtitle = null!;
    private Label _banner = null!;
    private float _bannerAge;
    private float _cardAge;
    private Color _accent = Ui.Paper;

    public override void _Ready()
    {
        ZIndex = 28;

        _title = new Label
        {
            Position = new Vector2(0, CardY + 14),
            Size = new Vector2(GameRoot.ViewportWidth, 28),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = Ui.Text(Ui.Title, Ui.Paper),
        };
        AddChild(_title);

        _subtitle = new Label
        {
            Position = new Vector2(0, CardY + 50),
            Size = new Vector2(GameRoot.ViewportWidth, 40),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = Ui.Text(Ui.Small, new Color("c8c8d8")),
        };
        AddChild(_subtitle);

        _banner = new Label
        {
            Position = new Vector2(0, 96),
            Size = new Vector2(GameRoot.ViewportWidth, 30),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = Ui.Text(Ui.Title, Ui.Gold),
            Visible = false,
        };
        AddChild(_banner);
    }

    public void Show(string title, string subtitle) => Show(title, subtitle, Ui.Paper);

    /// <summary>The accent colours the card's edge, so a win and a loss are told apart
    /// before a single word is read.</summary>
    public void Show(string title, string subtitle, Color accent)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        _accent = accent;
        _title.LabelSettings.FontColor = accent;
        _title.Visible = true;
        _subtitle.Visible = true;
        _cardAge = 0f;
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
        var dt = (float)delta;

        if (_title.Visible && _cardAge < 1f)
        {
            _cardAge = Mathf.Min(1f, _cardAge + dt * 6f);
            QueueRedraw();
        }

        if (!_banner.Visible) return;

        _bannerAge += dt;

        if (_bannerAge >= 1.1f)
        {
            _banner.Visible = false;
            return;
        }

        var pop = _bannerAge < 0.12f ? _bannerAge / 0.12f : 1f;
        _banner.Scale = Vector2.One * (0.6f + 0.4f * pop);
        _banner.Position = new Vector2(-GameRoot.ViewportWidth * (_banner.Scale.X - 1f) / 2f, 96);
        _banner.Modulate = Colors.White with { A = _bannerAge > 0.8f ? 1f - (_bannerAge - 0.8f) / 0.3f : 1f };
    }

    public override void _Draw()
    {
        if (!_title.Visible) return;

        // The card wipes open vertically, which reads as a card being dealt rather
        // than a menu appearing.
        var height = CardHeight * Mathf.Min(1f, _cardAge);
        var y = CardY + (CardHeight - height) / 2f;
        var rect = new Rect2(8, y, GameRoot.ViewportWidth - 16, height);

        Ui.Panel(this, rect, new Color("101020", 0.95f));

        if (height < CardHeight) return;

        // Accent rails top and bottom, one pixel thick like everything else.
        DrawRect(new Rect2(rect.Position.X + 1, rect.Position.Y + 1, rect.Size.X - 2, 1), _accent);
        DrawRect(new Rect2(rect.Position.X + 1, rect.End.Y - 2, rect.Size.X - 2, 1), _accent);
    }
}
