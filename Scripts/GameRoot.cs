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
    public const float JawHalfWidth = 17f;

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

    private enum Phase { Title, Intro, Countdown, Fighting, Interlude, Result, Shop }

    /// <summary>Where the world zooms from, so a punch scales about the action.</summary>
    private static readonly Vector2 ZoomPivot = new(JawCenterX, 190f);

    private ISaveStore _saveStore = null!;
    private FoodTable _foodTable = null!;
    private SaveData _save = new();
    private BoutSession? _bout;

    private Node2D _world = null!;
    private Backdrop _backdrop = null!;
    private Crowd _crowd = null!;
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
    private bool _dialogueWasInterlude;

    /// <summary>
    /// The crowd needs breathing room for exactly the reason the rival's barks do: a
    /// reaction to everything is wallpaper. The mood still moves silently on every
    /// bite - only the sound is rationed.
    /// </summary>
    private const float CrowdCooldown = 1.2f;
    private float _crowdCooldown;
    private int _lastCheeredCombo;

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

        _crowd = new Crowd();
        _world.AddChild(_crowd);

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

        var autoPlay = AutoPlay.TryCreate(this);
        if (autoPlay is not null) AddChild(autoPlay);
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

        _bout = new BoutSession(
            _foodTable, new SeededRandom((int)(Time.GetTicksMsec() & 0x7FFFFFFF)),
            new JawZone(JawCenterX, JawHalfWidth), SpawnX, RetireX, next, Career.Phases);

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

        if (_dialogueWasInterlude)
        {
            // Coming back from an interlude the hands are already set, so the beat
            // before the bell is shorter than the one before the bout.
            _dialogueWasInterlude = false;
            _hud.Visible = true;
            Render(_bout!.BeginNextPhase());

            _phase = Phase.Countdown;
            _countdown = 1.6f;
            _countdownShown = -1;
            return;
        }

        _hud.ResetForNewMatch();

        // A beat of anticipation before the bell. Dropping straight from a taunt into
        // a moving belt gives the player no moment to set their hands.
        if (_bout is not null)
        {
            Render(_bout.Start());
            _hud.Update(_bout.Current.State, 0, 0f, _save.Money);
        }

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
        _croc.PlayTaunt();
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

    private void EndBout(BoutEnded ended)
    {
        _phase = Phase.Result;
        _hitStop = 0f;
        _frenzy.SetAmount(0f);
        _conveyor.SetFrenzy(0f);
        _belt.Clear();
        _hud.Visible = true;

        if (ended.Result == BoutResult.Won)
        {
            Career.RecordWin(_save, ended);
            _crowd.Lift(1f);
            _crowd.Spike(1f);
            CrowdSay(Sfx.Cheer, volumeDb: 0f, urgent: true);
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

            // Losing on points is not a disgrace; the room is sorry, not hostile.
            CrowdSay(Sfx.Aww, volumeDb: -6f, urgent: true);
            _sfx.Play(Sfx.Lose);

            // There is no disqualification any more: a knockout costs a phase, and the
            // bout is always decided on points at the bell.
            _overlay.Show("BEATEN",
                $"{ended.PlayerScore} to {ended.OpponentScore}\npress to try again", Ui.Red);
        }

        _saveStore.Save(_save);
        ApplySkin();
    }

    /// <summary>
    /// The valley between two phases. The belt is empty, the rival says something about
    /// how it is going, and the player gets their hands back before the next act.
    /// </summary>
    private void StartInterlude(PhaseEnded ended)
    {
        if (_bout is null || !_bout.AwaitingInterlude) return;   // the last phase ends the bout

        _phase = Phase.Interlude;
        _belt.Clear();
        _hud.Visible = false;
        _dialogueWasInterlude = true;
        _sfx.Play(Sfx.Blip);

        var def = _bout.Def.Opponent;
        var rivalAhead = ended.OpponentScore > ended.PlayerScore;

        _dialogue.Play("croc", def.SpriteId, new[]
        {
            new DialogueScene.Line(false, def.Name,
                                   Career.InterludeLine(def, ended.PhaseIndex, rivalAhead)),
            new DialogueScene.Line(true, "CROC", CrocInterludeReply(rivalAhead, ended.KnockedOut)),
        });
    }

    private static string CrocInterludeReply(bool rivalAhead, bool knockedOut) =>
        knockedOut ? "*spits out a bomb*"
        : rivalAhead ? "*stomach growls, louder*"
        : "*licks the plate clean*";

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
        _croc.SetCosmetic(Career.EquippedSkin(_save)?.SpriteId ?? "");
        _croc.SetGlow(0f);
    }

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
            case Phase.Interlude:
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

    // ---------------------------------------------------------------- autoplay
    // Narrow hooks for the --autoplay smoke test. They expose only what a player
    // could see and do, so the harness cannot pass a test the real game would fail.

    /// <summary>True while a match is actually running.</summary>
    public bool AutoPlayInMatch => _phase == Phase.Fighting;

    /// <summary>Whether a competent player would press right now.</summary>
    public bool AutoPlayShouldPress()
    {
        if (_phase != Phase.Fighting) return true;   // every other screen wants a press
        if (_bout is null) return false;

        var phase = _bout.Current;
        var jaw = phase.EffectiveJaw;

        foreach (var item in phase.Items)
        {
            // Bite anything edible on sight - which now includes buffs and the coin -
            // and leave hazards alone, as a player should.
            if (phase.IsEdibleNow(item) && jaw.Overlaps(item)) return true;
        }

        return false;
    }

    public void AutoPlayPress() => _chompQueued = true;

    private bool TakeChomp()
    {
        if (!_chompQueued) return false;

        _chompQueued = false;
        return true;
    }

    private void Fight(float dt)
    {
        if (_bout is null) return;

        // Hit-stop freezes the session rather than slowing it: because Core is
        // dt-driven, not calling Tick cannot consume part of a later timing window.
        if (_hitStop > 0f)
        {
            _hitStop -= dt;
            return;
        }

        if (TakeChomp()) Render(_bout.Chomp());
        if (_phase != Phase.Fighting) return;

        Render(_bout.Tick(dt));
        if (_phase != Phase.Fighting) return;

        var phase = _bout.Current;
        var frenzy = phase.Frenzy.Fraction;

        _conveyor.Advance(phase.BeltSpeed, dt);
        _conveyor.SetFrenzy(frenzy);

        // Ask the phase for the zone the judge is actually using, so what lights up is
        // exactly what scores - including while hunger has it held open.
        var jaw = phase.EffectiveJaw;
        _conveyor.SetJawHalfWidth(jaw.HalfWidth);

        var occupied = false;
        foreach (var item in phase.Items)
        {
            if (!jaw.Overlaps(item)) continue;
            occupied = true;
            break;
        }

        _conveyor.SetZoneOccupied(occupied);
        _frenzy.SetAmount(frenzy);
        _croc.SetGlow(frenzy);

        _belt.Sync(phase.Items);
        _belt.PruneMissing(phase.Items);
        _hud.Update(phase.State, _bout.OpponentScore, frenzy, _save.Money);
        _hud.SetPot(phase.Pot.Amount, Pot.MultiplierForCombo(phase.State.Combo), _bout.PlayerScore);
        _hud.SetHunger(phase.Hunger.Charge, phase.Hunger.IsActive);
        _hud.SetCarried(_bout.PlayerScore);
        _hud.SetShield(phase.Buffs.HasShield);
        _croc.SetMagnet(phase.Buffs.MagnetBitesRemaining > 0);

        UpdateRivalReactions(dt);
    }

    private void DecayEffects(float dt)
    {
        if (_crowdCooldown > 0f) _crowdCooldown = Mathf.Max(0f, _crowdCooldown - dt);
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
                    _croc.PlayFlinch();   // a bomb: the sprite says so, not just the flash
                    _belt.Remove(chomped.Item.Id);
                    break;
                case ChompedAir:
                    _croc.PlayFlinch();
                    _sfx.Play(Sfx.Whiff);
                    break;
                case Passed passed:
                    _belt.Remove(passed.Item.Id);
                    _sfx.Play(Sfx.Pass);
                    _crowd.Drop(0.06f);   // silent: food goes past far too often to voice
                    break;
                case OpponentAte ate:
                    _rival.Ate(ate.OpponentScore);
                    break;
                case FrenzyStarted:
                    CrowdCheer(lift: 0.40f, spike: 0.9f, pitch: 1.05f);
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
                    CrowdCommiserate(0.30f);
                    break;
                case PhaseStarted started:
                    _crowd.ResetForPhase();
                    _lastCheeredCombo = 0;
                    _overlay.Flash(started.Phase.Name);
                    _sfx.Play(Sfx.Blip, 1.1f);
                    _zoom = 1f;
                    _hud.SetPhase(started.PhaseIndex, started.Phase.Name);
                    _backdrop.SetPhase(started.PhaseIndex);
                    break;
                case PhaseEnded ended:
                    StartInterlude(ended);
                    break;
                case PhaseKnockout:
                    // The one thing the crowd actually boos: every tooth gone inside a
                    // single phase. Urgent, so it is never swallowed by the cooldown.
                    _crowd.Drop(0.6f);
                    _crowd.Slump(1f);
                    CrowdSay(Sfx.Boo, volumeDb: -4f, urgent: true);
                    _overlay.Flash("OUT!");
                    _sfx.Play(Sfx.Lose);
                    _shake = StrikeShake * 1.6f;
                    _flashAlpha = 1f;
                    break;
                case CoinSpawned coin:
                    _belt.SetCoinValue(coin.Item.Id, coin.Value);
                    _sfx.Play(Sfx.Blip, 1.4f);
                    break;
                case PotBanked banked:
                    OnBanked(banked);
                    break;
                case PotWiped wiped when wiped.Lost > 0:
                    _sfx.Play(Sfx.Whiff, 0.7f);
                    _overlay.Flash($"-{wiped.Lost}");
                    break;
                case BuffTaken taken:
                    _croc.PlayGulp();
                    _crowd.Lift(0.15f);
                    _crowd.Spike(0.4f);
                    _sfx.Play(Sfx.Frenzy, BuffPitch(taken.Kind));
                    _overlay.Flash(BuffLabel(taken.Kind));
                    _croc.Punch(1.2f);
                    break;
                case HungerStarted:
                    CrowdCheer(lift: 0.45f, spike: 1f, pitch: 0.95f);
                    _overlay.Flash("HUNGRY");
                    _sfx.Play(Sfx.Frenzy, 0.6f);
                    _flashAlpha = 0.6f;
                    _zoom = 1.4f;
                    _rival.Panic(RivalLine(r => r.LinePanic));
                    _barkCooldown = BarkCooldown;
                    break;
                case HungerEnded:
                    _sfx.Play(Sfx.Blip, 0.6f);
                    break;
                case BoutEnded bout:
                    EndBout(bout);
                    return;
            }
        }
    }

    /// <summary>
    /// Banking the pot is the rare moment this design has, so it gets everything at
    /// once - hit-stop, a gold wash, a burst, its own sound. Effects that fire
    /// constantly stop being events.
    /// </summary>
    private void OnBanked(PotBanked banked)
    {
        _sfx.Play(Sfx.Coin, 1f + 0.04f * banked.Multiplier);
        _goldFlash = 1f;
        _zoom = 1f;
        _hitStop = HitStopSeconds * 2f;
        _croc.Punch(1.4f);
        _croc.PlayGulp();
        _overlay.Flash($"+{banked.Paid}");
        _crumbs.Burst(new Vector2(JawCenterX, BeltY), 18, 1.6f);

        // A bigger payout is a bigger noise, up to a point.
        CrowdCheer(lift: 0.20f + Mathf.Min(banked.Paid, 300) / 1000f,
                   spike: 0.8f, pitch: 1f + 0.02f * banked.Multiplier);

        if (banked.Paid > 100)
        {
            _rival.Rattle(RivalLine(r => r.LineLosing));
            _barkCooldown = BarkCooldown;
        }
    }

    private static string BuffLabel(BuffKind kind) => kind switch
    {
        BuffKind.Slow => "SLOW",
        BuffKind.Shield => "SHIELD",
        BuffKind.Magnet => "MAGNET",
        _ => "GOLD TOOTH",
    };

    /// <summary>Pitch climbs with the buff's strength, so which one landed is audible
    /// before the banner is legible.</summary>
    private static float BuffPitch(BuffKind kind) => kind switch
    {
        BuffKind.Slow => 0.8f,
        BuffKind.Shield => 1.0f,
        BuffKind.Magnet => 1.2f,
        _ => 1.5f,
    };

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

        // Every bite lifts the room a little; only the milestones are allowed a voice.
        _crowd.Lift(0.05f);

        if (chomped.Combo >= 5 && chomped.Combo % 5 == 0 && chomped.Combo != _lastCheeredCombo)
        {
            _lastCheeredCombo = chomped.Combo;
            CrowdCheer(lift: 0.25f, spike: 0.7f, pitch: 1f + 0.03f * (chomped.Combo / 5));
        }

        _hud.PulseCombo();
        AddChild(ComboPopup.Create(new Vector2(JawCenterX + 28f, BeltY - 34f),
                                   chomped.ScoreAwarded, chomped.Combo, chomped.DuringFrenzy || golden));
    }

    /// <summary>
    /// Watches the shape of the match and lets the rival react to it. Reacting to the
    /// lead changing, to a long combo, and to the closing seconds is what turns a
    /// climbing number into an opponent.
    /// </summary>
    private void UpdateRivalReactions(float dt)
    {
        if (_bout is null) return;

        _barkCooldown = Mathf.Max(0f, _barkCooldown - dt);

        var state = _bout.Current.State;
        var ahead = _bout.PlayerScore > _bout.OpponentScore;

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
        if (!_clutchBarked && state.TimeRemaining <= 4f
            && _bout.PlayerScore > _bout.OpponentScore * 1.25f)
        {
            _clutchBarked = true;
            _rival.Panic(RivalLine(r => r.LinePanic));
            _barkCooldown = BarkCooldown;
        }

        _playerWasAhead = ahead;
    }

    private string RivalLine(Func<OpponentDef, string> pick) =>
        _bout is null ? "" : pick(_bout.Def.Opponent);

    /// <summary>
    /// The crowd's voice, rationed. Volume is set per cue rather than baked into the
    /// files, which are all normalised to the same peak: the cheer is the loudest thing
    /// in the room and the commiseration is not.
    /// </summary>
    private void CrowdSay(string sound, float volumeDb, float pitch = 1f, bool urgent = false)
    {
        if (!urgent && _crowdCooldown > 0f) return;

        _crowdCooldown = CrowdCooldown;
        _sfx.Play(sound, pitch, volumeDb);
    }

    private void CrowdCheer(float lift, float spike, float pitch = 1f)
    {
        _crowd.Lift(lift);
        _crowd.Spike(spike);
        CrowdSay(Sfx.Cheer, volumeDb: 0f, pitch: pitch);
    }

    /// <summary>
    /// An ordinary mistake. The room winces with the player rather than at them - this
    /// game's whole premise is that anyone can finish it, so its harshest sound is not
    /// the one aimed at whoever is already struggling.
    /// </summary>
    private void CrowdCommiserate(float drop)
    {
        _crowd.Drop(drop);
        _crowd.Slump(0.5f);
        CrowdSay(Sfx.Aww, volumeDb: -7f);
    }

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
