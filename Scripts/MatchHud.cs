using CrocGame.Core;
using Godot;

namespace CrocGame;

/// <summary>
/// Clock, the score tug-of-war, combo, strikes, frenzy meter, and money.
///
/// The tug-of-war bar is the important one: a raw pair of numbers makes the player do
/// arithmetic under time pressure, whereas a bar that leans tells them whether they
/// are winning at a glance.
/// </summary>
public partial class MatchHud : Node2D
{
    private const float BarY = 130f;
    private const float BarHeight = 9f;
    private const float FrenzyBarY = 144f;

    private static readonly Color PlayerColor = new("58d854");
    private static readonly Color RivalColor = new("f87858");
    private static readonly Color FrameColor = new("000000");
    private static readonly Color FrenzyColor = new("f8d878");

    private Label _clock = null!;
    private Label _playerScore = null!;
    private Label _combo = null!;
    private Label _money = null!;

    private float _playerShare = 0.5f;
    private float _targetShare = 0.5f;
    private float _frenzyFraction;
    private int _strikes;
    private float _comboPulse;

    public override void _Ready()
    {
        ZIndex = 20;

        _clock = MakeLabel(new Vector2(0, 6), Ui.Body, Ui.Paper, HorizontalAlignment.Center);
        _playerScore = MakeLabel(new Vector2(0, GameRoot.BeltY + 42), Ui.Body, Ui.Green, HorizontalAlignment.Center);
        _combo = MakeLabel(new Vector2(0, GameRoot.BeltY + 62), Ui.Small, Ui.Gold, HorizontalAlignment.Center);
        _money = MakeLabel(new Vector2(-6, 8), Ui.Small, Ui.Gold, HorizontalAlignment.Right);
    }

    private Label MakeLabel(Vector2 position, int size, Color color, HorizontalAlignment align)
    {
        var label = new Label
        {
            Position = position,
            Size = new Vector2(GameRoot.ViewportWidth, size + 4),
            HorizontalAlignment = align,
            LabelSettings = Ui.Text(size, color),
        };
        AddChild(label);
        return label;
    }

    public void Update(MatchState state, int rivalScore, float frenzyFraction, int money)
    {
        _clock.Text = Mathf.CeilToInt(state.TimeRemaining).ToString();
        _clock.LabelSettings.FontColor = state.TimeRemaining <= 5f ? Ui.Red : Ui.Paper;

        _playerScore.Text = state.Score.ToString();
        _money.Text = $"${money}";
        _strikes = state.Strikes;
        _frenzyFraction = frenzyFraction;

        var total = state.Score + rivalScore;
        _targetShare = total <= 0 ? 0.5f : (float)state.Score / total;

        if (state.Combo >= 2)
        {
            _combo.Text = $"x{state.Combo}";
            _combo.Visible = true;
        }
        else
        {
            _combo.Visible = false;
        }

        QueueRedraw();
    }

    public void PulseCombo() => _comboPulse = 1f;

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        // The bar eases toward the true share so it reads as momentum swinging.
        _playerShare = Mathf.Lerp(_playerShare, _targetShare, 1f - Mathf.Exp(-8f * dt));

        if (_comboPulse > 0f)
        {
            _comboPulse = Mathf.Max(0f, _comboPulse - dt * 4f);
            _combo.Scale = Vector2.One * (1f + 0.5f * _comboPulse);
            _combo.Position = new Vector2(
                -GameRoot.ViewportWidth * (_combo.Scale.X - 1f) / 2f, GameRoot.BeltY + 62);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var w = GameRoot.ViewportWidth;

        // Tug of war: one framed bar split by where the score actually sits, so the
        // player reads who is ahead without comparing two numbers under time pressure.
        var bar = new Rect2(6, BarY - 1, w - 12, BarHeight + 2);
        Ui.Panel(this, bar, RivalColor);

        var inner = bar.Size.X - 2f;
        var playerWidth = Mathf.Clamp(inner * _playerShare, 0f, inner);
        if (playerWidth >= 1f) DrawRect(new Rect2(bar.Position.X + 1, BarY, playerWidth, BarHeight), PlayerColor);

        // A tick at the halfway mark: the line you are trying to stay right of.
        DrawRect(new Rect2(bar.Position.X + 1 + inner / 2f, BarY - 1, 1, BarHeight + 2), FrameColor);

        if (_frenzyFraction > 0f)
        {
            Ui.Meter(this, new Rect2(6, FrenzyBarY - 1, w - 12, 5f), _frenzyFraction,
                     FrenzyColor, new Color("2a2a3a"));
        }

        // Strike pips, bottom left: filled means spent.
        for (var i = 0; i < MatchState.MaxStrikes; i++)
        {
            Ui.Panel(this, new Rect2(6 + i * 10, GameRoot.ViewportHeight - 14, 8, 8),
                     i < _strikes ? Ui.Red : new Color("585858"));
        }
    }
}
