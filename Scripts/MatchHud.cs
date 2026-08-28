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

        _clock = MakeLabel(new Vector2(0, 4), 14, "f8f8f8", HorizontalAlignment.Center);
        _playerScore = MakeLabel(new Vector2(0, GameRoot.BeltY + 42), 16, "58d854", HorizontalAlignment.Center);
        _combo = MakeLabel(new Vector2(0, GameRoot.BeltY + 62), 11, "f8d878", HorizontalAlignment.Center);
        _money = MakeLabel(new Vector2(-6, 4), 9, "f8d878", HorizontalAlignment.Right);
    }

    private Label MakeLabel(Vector2 position, int size, string color, HorizontalAlignment align)
    {
        var label = new Label
        {
            Position = position,
            Size = new Vector2(GameRoot.ViewportWidth, size + 4),
            HorizontalAlignment = align,
            LabelSettings = new LabelSettings { FontSize = size, FontColor = new Color(color) },
        };
        AddChild(label);
        return label;
    }

    public void Update(MatchState state, int rivalScore, float frenzyFraction, int money)
    {
        _clock.Text = Mathf.CeilToInt(state.TimeRemaining).ToString();
        _clock.LabelSettings.FontColor = state.TimeRemaining <= 5f ? new Color("f83800") : new Color("f8f8f8");

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

        // Tug of war.
        DrawRect(new Rect2(6, BarY - 1, w - 12, BarHeight + 2), FrameColor);
        var inner = w - 14;
        var playerWidth = Mathf.Max(1f, inner * _playerShare);
        DrawRect(new Rect2(7, BarY, playerWidth, BarHeight), PlayerColor);
        DrawRect(new Rect2(7 + playerWidth, BarY, inner - playerWidth, BarHeight), RivalColor);

        // Frenzy meter, only while it matters.
        if (_frenzyFraction > 0f)
        {
            DrawRect(new Rect2(6, FrenzyBarY - 1, w - 12, 5f), FrameColor);
            DrawRect(new Rect2(7, FrenzyBarY, (w - 14) * _frenzyFraction, 3f), FrenzyColor);
        }

        // Strike pips, bottom left: filled means spent.
        for (var i = 0; i < MatchState.MaxStrikes; i++)
        {
            var box = new Rect2(6 + i * 9, GameRoot.ViewportHeight - 14, 7, 7);
            DrawRect(box, FrameColor);
            DrawRect(new Rect2(box.Position + Vector2.One, box.Size - Vector2.One * 2f),
                     i < _strikes ? new Color("f83800") : new Color("585858"));
        }
    }
}
