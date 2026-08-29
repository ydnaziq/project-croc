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

    private enum Phase { Title, Intro, Countdown, Fighting, Result, Shop }

    /// <summary>Where the world zooms from, so a punch scales about the action.</summary>
    private static readonly Vector2 ZoomPivot = new(JawCenterX, 190f);

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
    private DialogueScene _dialogue = null!;
    private Sfx _sfx = null!;

    private Phase _phase = Phase.Title;
    private bool _chompQueued;
    private float _hitStop;
    private float _shake;
    private float _flashAlpha;
    private float _zoom;
    private float _goldFlash;
    private ColorRect _gold = null!;
    private float _countdown;
    private int _countdownShown = -1;

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

        _gold = new ColorRect
        {
            Size = new Vector2(ViewportWidth, ViewportHeight),
            Color = Ui.Gold,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = Colors.White with { A = 0f },
            ZIndex = 17,
        };
        AddChild(_gold);

        _hud = new MatchHud { Visible = false };
        AddChild(_hud);

        _overlay = new ScreenOverlay();
        AddChild(_overlay);

        _dialogue = new DialogueScene();
        _dialogue.Finished += OnDialogueFinished;
        AddChild(_dialogue);

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

        _barkCooldown = 0f;
        _playerWasAhead = false;
        _lastBarkCombo = 0;
        _clutchBarked = false;

        _sfx.Play(Sfx.Blip);

        // The croc never speaks in words - he is starving, not chatty - so his half of
        // the exchange is body language. It still gives him a turn in the frame.
        _dialogue.Play("croc", next.Opponent.SpriteId, new[]
        {
            new DialogueScene.Line(false, next.Opponent.Name, next.Opponent.Taunt),
            new DialogueScene.Line(true, "CROC", CrocReply(Career.Progress(_save))),
        });
    }

    private static string CrocReply(int round) => round switch
    {
        0 => "*stomach growls loudly*",
        1 => "*has not eaten since tuesday*",
        2 => "*cracks knuckles, somehow*",
        _ => "*grins with every tooth*",
    };

    private void OnDialogueFinished()
    {
        _overlay.Hide();
        _hud.ResetForNewMatch();

        // A beat of anticipation before the bell. Dropping straight from a taunt into
        // a moving belt gives the player no moment to set their hands.
        _phase = Phase.Countdown;
        _countdown = 2.6f;
        _countdownShown = -1;
    }

    private void TickCountdown(float dt)
    {
        _countdown -= dt;

        var remaining = Mathf.CeilToInt(_countdown);

        if (remaining != _countdownShown)
        {
            _countdownShown = remaining;

            if (remaining > 0)
            {
                _overlay.Flash(remaining.ToString());
                _sfx.Play(Sfx.Blip, 0.8f + 0.1f * (3 - remaining));
            }
        }

        if (_countdown > 0f) return;

        _overlay.Flash("EAT!");
        _sfx.Play(Sfx.Frenzy);
        _zoom = 1f;

        // Drop anything pressed during the countdown. Otherwise an eager player buys a
        // strike on the first frame of the match for a press they made before the bell.
        _chompQueued = false;
        _phase = Phase.Fighting;
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
                if (TakeChomp()) _dialogue.Advance();
                return;

            case Phase.Countdown:
                TickCountdown(dt);
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
        if (_shake > 0f) _shake = Mathf.Max(0f, _shake - ShakeDecay * dt);
        if (_zoom > 0f) _zoom = Mathf.Max(0f, _zoom - dt * 4.5f);

        // Shake and zoom both want the world's transform, so they are resolved
        // together: scale about a pivot near the jaws, then offset by the shake.
        // Node2D has no separate offset, so the pivot correction goes into Position.
        var scale = 1f + 0.05f * _zoom;
        var offset = _shake > 0f
            ? new Vector2(_shakeRng.RandfRange(-_shake, _shake), _shakeRng.RandfRange(-_shake, _shake))
            : Vector2.Zero;

        _world.Scale = Vector2.One * scale;
        _world.Position = offset + ZoomPivot * (1f - scale);

        if (_flashAlpha > 0f)
        {
            _flashAlpha = Mathf.Max(0f, _flashAlpha - FlashDecay * dt);
            _flash.Modulate = Colors.White with { A = _flashAlpha };
        }

        if (_goldFlash > 0f)
        {
            _goldFlash = Mathf.Max(0f, _goldFlash - dt * 3f);
            _gold.Modulate = Colors.White with { A = _goldFlash * 0.5f };
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
                    _zoom = 1f;
                    _croc.Punch(1.2f);
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
        var golden = chomped.Item.TypeId == "golden";

        _croc.PlayEat();
        _croc.Punch(golden ? 1.4f : 0.7f + 0.05f * Math.Min(chomped.Combo, 6));
        _belt.Remove(chomped.Item.Id);

        _hitStop = golden ? HitStopSeconds * 2.2f : HitStopSeconds;
        _shake = Math.Max(_shake, ChompShake + Math.Min(chomped.Combo, 6) * 0.25f + (golden ? 3f : 0f));
        _crumbs.Burst(new Vector2(JawCenterX, BeltY),
                      golden ? 22 : chomped.DuringFrenzy ? 12 : 8,
                      golden ? 1.8f : chomped.DuringFrenzy ? 1.3f : 1f);

        if (golden)
        {
            // The rare bite is the one moment worth spending every effect at once.
            _zoom = 1f;
            _goldFlash = 0.7f;
            _sfx.Play(Sfx.Coin);
            _overlay.Flash("GOLDEN!");
            _rival.Rattle(RivalLine(r => r.LineLosing));
        }
        else
        {
            _sfx.Play(chomped.Combo >= 4 || chomped.DuringFrenzy ? Sfx.Crunch : Sfx.Chomp,
                      1f + 0.03f * Math.Min(chomped.Combo, 8));
        }

        _hud.PulseCombo();
        AddChild(ComboPopup.Create(new Vector2(JawCenterX, BeltY - 22f),
                                   chomped.ScoreAwarded, chomped.Combo, chomped.DuringFrenzy || golden));
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
        _zoom = 0.6f;
        _croc.Punch(0.5f);
        _flashAlpha = 0.5f;
        _sfx.Play(Sfx.Strike);
        _frenzy.SetAmount(0f);
        _conveyor.SetFrenzy(0f);
    }
}
