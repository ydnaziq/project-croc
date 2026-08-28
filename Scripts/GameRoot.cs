using System;
using System.Collections.Generic;
using System.Linq;
using CrocGame.Core;
using Godot;

namespace CrocGame;

public partial class GameRoot : Node2D
{
    public const float ViewportWidth = 180f;
    public const float ViewportHeight = 320f;

    /// <summary>The croc stands in the middle of the portrait screen.</summary>
    public const float JawCenterX = ViewportWidth / 2f;
    public const float JawHalfWidth = 12f;

    public const float SpawnX = -20f;
    public const float RetireX = ViewportWidth + 12f;

    /// <summary>The line food travels along - level with the croc's mouth.</summary>
    public const float BeltY = 232f;

    /// <summary>The rival's stage, high on the screen.</summary>
    public const float RivalY = 70f;

    /// <summary>
    /// The croc's mouth sits about 3px above the sprite centre; at 2x scale that is 6.
    /// </summary>
    public const float CrocMouthOffsetY = 6f;

    private const float HitStopSeconds = 0.055f;
    private const float StrikeShake = 5f;
    private const float ChompShake = 1.4f;
    private const float ShakeDecay = 16f;
    private const float FlashDecay = 4.5f;
    private const float IntroSeconds = 1.8f;

    private enum Phase { Title, Intro, Fighting, Result, Shop }

    private ISaveStore _saveStore = null!;
    private FoodTable _foodTable = null!;
    private SaveData _save = new();
    private MatchSession? _match;

    private Node2D _world = null!;
    private Backdrop _backdrop = null!;
    private ConveyorView _conveyor = null!;
    private BeltView _belt = null!;
    private CrocView _croc = null!;
    private RivalView _rival = null!;
    private Crumbs _crumbs = null!;
    private FrenzyOverlay _frenzy = null!;
    private ColorRect _flash = null!;
    private MatchHud _hud = null!;
    private ScreenOverlay _overlay = null!;
    private ShopScreen _shop = null!;
    private Sfx _sfx = null!;

    private Phase _phase = Phase.Title;
    private bool _chompQueued;
    private float _hitStop;
    private float _shake;
    private float _flashAlpha;
    private float _introTimer;

    private readonly RandomNumberGenerator _shakeRng = new();

    // Rival reactions. A bark needs breathing room or the rival becomes wallpaper,
    // so ordinary reactions share a cooldown; a panic is allowed to cut in.
    private const float BarkCooldown = 3.2f;
    private float _barkCooldown;
    private bool _playerWasAhead;
    private int _lastBarkCombo;
    private bool _clutchBarked;

    public override void _Ready()
    {
        _saveStore = new GodotSaveStore();
        _save = _saveStore.Load();
        _foodTable = LoadFoodTable();

        _sfx = new Sfx();
        AddChild(_sfx);

        _world = new Node2D();
        AddChild(_world);

        _backdrop = new Backdrop();
        _world.AddChild(_backdrop);

        _rival = new RivalView { Position = new Vector2(JawCenterX, RivalY) };
        _world.AddChild(_rival);

        _croc = new CrocView { Position = new Vector2(JawCenterX, BeltY + CrocMouthOffsetY) };
        _world.AddChild(_croc);

        _conveyor = new ConveyorView();
        _world.AddChild(_conveyor);

        _belt = new BeltView();
        _world.AddChild(_belt);

        _crumbs = new Crumbs();
        _world.AddChild(_crumbs);

        _frenzy = new FrenzyOverlay();
        AddChild(_frenzy);

        _flash = new ColorRect
        {
            Size = new Vector2(ViewportWidth, ViewportHeight),
            Color = new Color("f83800"),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = Colors.White with { A = 0f },
            ZIndex = 18,
        };
        AddChild(_flash);

        _hud = new MatchHud { Visible = false };
        AddChild(_hud);

        _overlay = new ScreenOverlay();
        AddChild(_overlay);

        _shop = new ShopScreen();
        _shop.BuyRequested += OnBuy;
        _shop.ContinueRequested += StartNextMatch;
        AddChild(_shop);

        ApplySkin();
        ShowTitle();
    }

    private static FoodTable LoadFoodTable()
    {
        using var file = FileAccess.Open("res://Resources/food.json", FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PushError("Could not read res://Resources/food.json");
            return FoodTable.FromJson("[]");
        }

        return FoodTable.FromJson(file.GetAsText());
    }

    // ------------------------------------------------------------------ phases

    private void ShowTitle()
    {
        _phase = Phase.Title;
        _hud.Visible = false;

        var line = Career.IsChampion(_save)
            ? $"champion  ${_save.Money}\npress to eat again"
            : Career.Progress(_save) > 0
                ? $"{Career.Progress(_save)} of {Career.Ladder.Count} beaten\npress to continue"
                : "a starving croc\nenters the contest";

        _overlay.Show("CROC", line);
    }

    /// <summary>The rival struts and taunts before the bell. A beat of anticipation.</summary>
    private void StartIntro()
    {
        var next = Career.NextMatch(_save);
        if (next is null)
        {
            _save.DefeatedIds.Clear();   // champion: the ladder loops for another run
            next = Career.NextMatch(_save)!;
        }

        _match = new MatchSession(
            _foodTable, new SeededRandom((int)(Time.GetTicksMsec() & 0x7FFFFFFF)),
            new JawZone(JawCenterX, JawHalfWidth), SpawnX, RetireX, next);

        _rival.Setup(next.Opponent);
        _belt.Clear();
        _crumbs.Visible = true;
        _hud.Visible = true;
        _phase = Phase.Intro;
        _introTimer = IntroSeconds;

        _barkCooldown = 0f;
        _playerWasAhead = false;
        _lastBarkCombo = 0;
        _clutchBarked = false;

        var round = Career.Progress(_save) + 1;
        _overlay.Show($"ROUND {round}", $"{next.Opponent.Name}\nof {Career.Ladder.Count} challengers", Ui.Gold);
        _rival.Gloat(next.Opponent.Taunt);
        _sfx.Play(Sfx.Blip);
    }

    private void StartNextMatch()
    {
        _shop.Close();
        StartIntro();
    }

    private void EndMatch(MatchEnded ended)
    {
        _phase = Phase.Result;
        _hitStop = 0f;
        _frenzy.SetAmount(0f);
        _conveyor.SetFrenzy(0f);
        _belt.Clear();

        if (ended.Result == MatchResult.Won)
        {
            Career.RecordWin(_save, ended);
            _croc.PlayCelebrate();
            _sfx.Play(Sfx.Win);
            if (ended.Prize > 0) _sfx.Play(Sfx.Coin);

            _overlay.Show("WINNER",
                $"{ended.PlayerScore} to {ended.OpponentScore}\n+${ended.Prize}  best x{ended.BestCombo}",
                Ui.Green);
        }
        else
        {
            Career.RecordLoss(_save, ended);
            _sfx.Play(Sfx.Lose);

            var headline = ended.Result == MatchResult.Disqualified ? "DISQUALIFIED" : "BEATEN";
            var detail = ended.Result == MatchResult.Disqualified
                ? "three strikes\npress to try again"
                : $"{ended.PlayerScore} to {ended.OpponentScore}\npress to try again";

            _overlay.Show(headline, detail, Ui.Red);
        }

        _saveStore.Save(_save);
        ApplySkin();
    }

    private void OpenShop()
    {
        _phase = Phase.Shop;
        _hud.Visible = false;
        _overlay.Hide();
        _shop.Open(_save);
    }

    private void OnBuy(string itemId)
    {
        var owned = _save.OwnedSkinIds.Contains(itemId);
        var result = owned ? PurchaseResult.AlreadyOwned : Career.Buy(_save, itemId);

        if (result == PurchaseResult.Bought)
        {
            _sfx.Play(Sfx.Coin);
        }
        else if (owned)
        {
            Career.Equip(_save, itemId);   // already yours: tapping wears it
            _sfx.Play(Sfx.Blip);
        }
        else
        {
            _sfx.Play(Sfx.Whiff);          // cannot afford it
        }

        _saveStore.Save(_save);
        ApplySkin();
        _shop.Refresh();
    }

    private void ApplySkin()
    {
        var skin = Career.EquippedSkin(_save);
        _skinTint = skin is null ? Colors.White : new Color(skin.Tint);
        _croc.SetGlow(0f, _skinTint);
    }

    private Color _skinTint = Colors.White;

    // ------------------------------------------------------------------ loop

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_phase == Phase.Shop) return;   // the shop reads its own input
        if (@event.IsActionPressed("chomp")) _chompQueued = true;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        DecayEffects(dt);

        switch (_phase)
        {
            case Phase.Title:
                if (TakeChomp()) { _sfx.Play(Sfx.Blip); _overlay.Hide(); StartIntro(); }
                return;

            case Phase.Intro:
                _introTimer -= dt;
                if (_introTimer <= 0f || TakeChomp())
                {
                    _overlay.Hide();
                    _phase = Phase.Fighting;
                }
                return;

            case Phase.Result:
                if (TakeChomp()) OpenShop();
                return;

            case Phase.Shop:
                return;
        }

        Fight(dt);
    }

    private bool TakeChomp()
    {
        if (!_chompQueued) return false;

        _chompQueued = false;
        return true;
    }

    private void Fight(float dt)
    {
        if (_match is null) return;

        // Hit-stop freezes the session rather than slowing it: because Core is
        // dt-driven, not calling Tick cannot consume part of a later timing window.
        if (_hitStop > 0f)
        {
            _hitStop -= dt;
            return;
        }

        if (TakeChomp()) Render(_match.Chomp());
        if (_phase != Phase.Fighting) return;

        Render(_match.Tick(dt));
        if (_phase != Phase.Fighting) return;

        var frenzy = _match.Frenzy.Fraction;
        _conveyor.Advance(_match.BeltSpeed, dt);
        _conveyor.SetFrenzy(frenzy);
        _frenzy.SetAmount(frenzy);
        _croc.SetGlow(frenzy, _skinTint);

        _belt.Sync(_match.Items);
        _belt.PruneMissing(_match.Items);
        _hud.Update(_match.State, _match.OpponentScore, frenzy, _save.Money);

        UpdateRivalReactions(dt);
    }

    private void DecayEffects(float dt)
    {
        if (_shake > 0f)
        {
            _shake = Mathf.Max(0f, _shake - ShakeDecay * dt);
            _world.Position = new Vector2(
                _shakeRng.RandfRange(-_shake, _shake),
                _shakeRng.RandfRange(-_shake, _shake));
        }
        else if (_world.Position != Vector2.Zero)
        {
            _world.Position = Vector2.Zero;
        }

        if (_flashAlpha > 0f)
        {
            _flashAlpha = Mathf.Max(0f, _flashAlpha - FlashDecay * dt);
            _flash.Modulate = Colors.White with { A = _flashAlpha };
        }
    }

    private void Render(IReadOnlyList<GameEvent> events)
    {
        foreach (var evt in events)
        {
            switch (evt)
            {
                case Chomped chomped when chomped.Item.IsEdible:
                    OnEaten(chomped);
                    break;
                case Chomped chomped:
                    _croc.PlayEat();
                    _belt.Remove(chomped.Item.Id);
                    break;
                case ChompedAir:
                    _croc.PlayEat();
                    _sfx.Play(Sfx.Whiff);
                    break;
                case Passed passed:
                    _belt.Remove(passed.Item.Id);
                    _sfx.Play(Sfx.Pass);
                    break;
                case OpponentAte ate:
                    _rival.Ate(ate.OpponentScore);
                    break;
                case FrenzyStarted:
                    _sfx.Play(Sfx.Frenzy);
                    _shake = 3f;
                    _overlay.Flash("FRENZY!");
                    _rival.Panic(RivalLine(r => r.LinePanic));
                    _barkCooldown = BarkCooldown;
                    break;
                case StrikeAdded:
                    OnStrike();
                    break;
                case MatchEnded ended:
                    EndMatch(ended);
                    return;
            }
        }
    }

    private void OnEaten(Chomped chomped)
    {
        _croc.PlayEat();
        _belt.Remove(chomped.Item.Id);

        _hitStop = HitStopSeconds;
        _shake = Math.Max(_shake, ChompShake + Math.Min(chomped.Combo, 6) * 0.25f);
        _crumbs.Burst(new Vector2(JawCenterX, BeltY), chomped.DuringFrenzy ? 12 : 8,
                      chomped.DuringFrenzy ? 1.3f : 1f);

        _sfx.Play(chomped.Combo >= 4 || chomped.DuringFrenzy ? Sfx.Crunch : Sfx.Chomp,
                  1f + 0.03f * Math.Min(chomped.Combo, 8));

        _hud.PulseCombo();
        AddChild(ComboPopup.Create(new Vector2(JawCenterX, BeltY - 22f),
                                   chomped.ScoreAwarded, chomped.Combo, chomped.DuringFrenzy));
    }

    /// <summary>
    /// Watches the shape of the match and lets the rival react to it. Reacting to the
    /// lead changing, to a long combo, and to the closing seconds is what turns a
    /// climbing number into an opponent.
    /// </summary>
    private void UpdateRivalReactions(float dt)
    {
        if (_match is null) return;

        _barkCooldown = Mathf.Max(0f, _barkCooldown - dt);

        var state = _match.State;
        var ahead = state.Score > _match.OpponentScore;

        if (_barkCooldown <= 0f)
        {
            if (ahead && !_playerWasAhead)
            {
                _rival.Rattle(RivalLine(r => r.LineLosing));
                _barkCooldown = BarkCooldown;
            }
            else if (!ahead && _playerWasAhead)
            {
                _rival.Gloat(RivalLine(r => r.LineWinning));
                _barkCooldown = BarkCooldown;
            }
            else if (state.Combo >= 12 && state.Combo >= _lastBarkCombo + 6)
            {
                _lastBarkCombo = state.Combo;
                _rival.Rattle(RivalLine(r => r.LineLosing));
                _barkCooldown = BarkCooldown;
            }
        }

        // The closing seconds, with the match already lost for them.
        if (!_clutchBarked && state.TimeRemaining <= 8f
            && state.Score > _match.OpponentScore * 1.25f)
        {
            _clutchBarked = true;
            _rival.Panic(RivalLine(r => r.LinePanic));
            _barkCooldown = BarkCooldown;
        }

        _playerWasAhead = ahead;
    }

    private string RivalLine(Func<OpponentDef, string> pick) =>
        _match is null ? "" : pick(_match.Def.Opponent);

    private void OnStrike()
    {
        _shake = StrikeShake;
        _flashAlpha = 0.5f;
        _sfx.Play(Sfx.Strike);
        _frenzy.SetAmount(0f);
        _conveyor.SetFrenzy(0f);
    }
}
