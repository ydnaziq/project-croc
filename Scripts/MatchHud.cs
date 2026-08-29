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
    private StrikeMeter _teeth = null!;
    private float _leadFlash;
    private bool _wasAhead = true;
    private float _knobBob;
    private bool _timeLow;

    private int _phaseIndex;
    private Label _pot = null!;
    private float _hungerCharge;
    private bool _hungerActive;
    private int _carried;

    public override void _Ready()
    {
        ZIndex = 20;

        _clock = MakeLabel(new Vector2(0, 6), Ui.Body, Ui.Paper, HorizontalAlignment.Center);
        _playerScore = MakeLabel(new Vector2(0, GameRoot.BeltY + 42), Ui.Body, Ui.Green, HorizontalAlignment.Center);
        _combo = MakeLabel(new Vector2(0, GameRoot.BeltY + 62), Ui.Small, Ui.Gold, HorizontalAlignment.Center);
        _money = MakeLabel(new Vector2(-6, 8), Ui.Small, Ui.Gold, HorizontalAlignment.Right);

        // The pot rides hard right, clear of the teeth on the left and the combo in
        // the middle. At 180px wide every readout needs its own lane.
        _pot = MakeLabel(new Vector2(-6, GameRoot.BeltY + 62), Ui.Small, Ui.Gold,
                         HorizontalAlignment.Right);

        _teeth = new StrikeMeter { Position = new Vector2(8, GameRoot.ViewportHeight - 24) };
        AddChild(_teeth);
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

    /// <summary>
    /// Which act is live, shown only as pips. The name itself is announced by the
    /// banner at each phase start - a second permanent copy of it competed with the
    /// rival's name plate for the same row.
    /// </summary>
    public void SetPhase(int index, string name)
    {
        _phaseIndex = index;
        QueueRedraw();
    }

    /// <summary>
    /// The wager, shown as what it would pay rather than what has accrued - the
    /// decision is about the payout, so that is the number on screen. It turns red
    /// once declining a coin is risking more than the bout is currently worth.
    /// </summary>
    public void SetPot(int amount, int multiplier, int scoreSoFar)
    {
        _pot.Text = amount == 0 ? "" : $"POT {amount * multiplier} x{multiplier}";
        _pot.LabelSettings.FontColor = amount * multiplier > scoreSoFar ? Ui.Red : Ui.Gold;
    }

    /// <summary>Hunger has to be visible while it fills, not only when it fires: a
    /// meter the player only ever sees full teaches them nothing about why.</summary>
    public void SetHunger(float charge, bool active)
    {
        _hungerCharge = charge;
        _hungerActive = active;
        QueueRedraw();
    }

    /// <summary>The carried bout total, which is the number that decides the bout.</summary>
    public void SetCarried(int score) => _carried = score;

    public void SetShield(bool has) => _teeth.SetShield(has);

    public void Update(MatchState state, int rivalScore, float frenzyFraction, int money)
    {
        _clock.Text = Mathf.CeilToInt(state.TimeRemaining).ToString();
        _clock.LabelSettings.FontColor = state.TimeRemaining <= 5f ? Ui.Red : Ui.Paper;
        _timeLow = state.TimeRemaining <= 5f;

        _playerScore.Text = state.Score.ToString();
        _money.Text = $"${money}";
        _strikes = state.Strikes;
        _teeth.SetStrikes(state.Strikes);
        _frenzyFraction = frenzyFraction;

        var total = state.Score + rivalScore;
        var raw = total <= 0 ? 0.5f : (float)state.Score / total;

        // Never let either side vanish completely. A bar pinned hard to one end stops
        // carrying information and just looks broken.
        _targetShare = Mathf.Clamp(raw, 0.06f, 0.94f);

        var ahead = state.Score >= rivalScore;
        if (ahead != _wasAhead) _leadFlash = 1f;
        _wasAhead = ahead;

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

    public void ResetForNewMatch()
    {
        _teeth.Reset();
        _playerShare = 0.5f;
        _targetShare = 0.5f;
        _wasAhead = true;
        _leadFlash = 0f;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        // The bar eases toward the true share so it reads as momentum swinging.
        _playerShare = Mathf.Lerp(_playerShare, _targetShare, 1f - Mathf.Exp(-8f * dt));
        _leadFlash = Mathf.Max(0f, _leadFlash - dt * 2f);
        _knobBob += dt;

        // The clock beats once a second in the closing five, which is felt before it
        // is read.
        if (_timeLow)
        {
            var beat = Mathf.Abs(Mathf.Sin(_knobBob * Mathf.Pi));
            _clock.Scale = Vector2.One * (1f + 0.35f * beat);
            _clock.Position = new Vector2(-GameRoot.ViewportWidth * (_clock.Scale.X - 1f) / 2f, 6);
        }
        else if (_clock.Scale != Vector2.One)
        {
            _clock.Scale = Vector2.One;
            _clock.Position = new Vector2(0, 6);
        }

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

        // A knob riding the boundary, so the eye has something to track as the lead
        // moves rather than watching a colour edge slide.
        var knobX = bar.Position.X + 1f + playerWidth;
        var bob = Mathf.Sin(_knobBob * 8f) * (_leadFlash > 0f ? 1.5f : 0f);
        Ui.Panel(this, new Rect2(knobX - 3f, BarY - 3f + bob, 6f, BarHeight + 6f),
                 _playerShare >= 0.5f ? PlayerColor : RivalColor);

        // The whole bar flares white the instant the lead changes hands.
        if (_leadFlash > 0f)
        {
            DrawRect(bar, new Color(1f, 1f, 1f, _leadFlash * 0.5f));
        }

        if (_frenzyFraction > 0f)
        {
            Ui.Meter(this, new Rect2(6, FrenzyBarY - 1, w - 12, 5f), _frenzyFraction,
                     FrenzyColor, new Color("2a2a3a"));
        }

        // Three pips beside the clock: which act, and how many are left.
        for (var i = 0; i < Career.Phases.Count; i++)
        {
            Ui.Panel(this, new Rect2(6 + i * 7, 4, 5, 5),
                     i < _phaseIndex ? Ui.Dim : i == _phaseIndex ? Ui.Gold : Ui.PanelFill);
        }

        // Hunger, along the bottom edge. Red because it only ever means one thing.
        if (_hungerCharge > 0f || _hungerActive)
        {
            var meter = new Rect2(6, GameRoot.ViewportHeight - 6f, w - 12, 4f);
            Ui.Meter(this, meter, _hungerActive ? 1f : _hungerCharge,
                     _hungerActive ? Ui.Paper : Ui.Red, new Color("2a2a3a"));
        }

        // Strikes are drawn by the StrikeMeter child, not here.
    }
}
