using System;
using System.Collections.Generic;
using CrocGame.Core;
using Godot;

namespace CrocGame;

/// <summary>
/// Runs one bout: the taunt before the bell, three phases with a countdown each, the
/// interlude dialogue between them, and the translation of every Core event into
/// something on screen.
///
/// This is separate from GameRoot because GameRoot is about *screens* - title, result,
/// shop - and this is about *a match*. They change for different reasons and at
/// different times, which is the line worth splitting on. GameRoot builds and owns the
/// view nodes, because they outlive any single bout; this borrows them.
/// </summary>
public partial class BoutDirector : Node
{
    /// <summary>
    /// The view nodes a bout drives. Passed in rather than created here: they outlive
    /// the bout, and GameRoot still needs them for the title, result and shop screens.
    /// </summary>
    public sealed record Views(
        Node2D World,
        Backdrop Backdrop,
        Crowd Crowd,
        RivalView Rival,
        CrocView Croc,
        ConveyorView Conveyor,
        BeltView Belt,
        Crumbs Crumbs,
        FrenzyOverlay Frenzy,
        ColorRect Flash,
        ColorRect Gold,
        MatchHud Hud,
        ScreenOverlay Overlay,
        DialogueScene Dialogue,
        Sfx Sfx,
        MusicPlayer Music);

    /// <summary>Raised once, when the bell settles the bout.</summary>
    public Action<BoutEnded>? Finished;

    private enum Stage { Idle, Intro, Countdown, Fighting, Interlude }

    private readonly Views _v;
    private readonly FoodTable _foodTable;

    private Stage _stage = Stage.Idle;
    private BoutSession? _bout;
    private int _money;
    private int _round;

    public BoutDirector(Views views, FoodTable foodTable)
    {
        _v = views;
        _foodTable = foodTable;
        _v.Dialogue.Finished += OnDialogueFinished;
    }

    /// <summary>True while the belt is actually running.</summary>
    public bool InMatch => _stage == Stage.Fighting;

    /// <summary>True while a bout is on screen at all, dialogue and countdown included.</summary>
    public bool Active => _stage != Stage.Idle;

    public PhaseSession? CurrentPhase => _stage == Stage.Fighting ? _bout?.Current : null;

    /// <summary>Starts a bout. Money is only carried so the HUD can show it.</summary>
    public void Begin(MatchDef def, int money, int round)
    {
        _money = money;
        _round = round;
        StartIntro(def);
    }

    /// <summary>A press. Returns true if the bout consumed it.</summary>
    public bool Press()
    {
        switch (_stage)
        {
            case Stage.Intro:
            case Stage.Interlude:
                _v.Dialogue.Advance();
                return true;

            case Stage.Fighting:
                _chompQueued = true;
                return true;

            default:
                return _stage != Stage.Idle;
        }
    }

    public void Tick(float dt)
    {
        DecayEffects(dt);

        switch (_stage)
        {
            case Stage.Idle:
            case Stage.Intro:
            case Stage.Interlude:
                return;

            case Stage.Countdown:
                TickCountdown(dt);
                return;
        }

        Fight(dt);
    }

    private bool _chompQueued;

    private bool TakeChomp()
    {
        if (!_chompQueued) return false;

        _chompQueued = false;
        return true;
    }

    private const float HitStopSeconds = 0.055f;
    private const float StrikeShake = 5f;
    private const float ChompShake = 1.4f;
    private const float ShakeDecay = 16f;
    private const float FlashDecay = 4.5f;

    /// <summary>Where the world zooms from, so a punch scales about the action.</summary>
    private static readonly Vector2 ZoomPivot = new(GameRoot.JawCenterX, 190f);

    private float _hitStop;
    private float _shake;
    private float _flashAlpha;
    private float _zoom;
    private float _goldFlash;
    private float _countdown;
    private int _countdownShown = -1;
    private bool _dialogueWasInterlude;

    private readonly RandomNumberGenerator _shakeRng = new();

    // Rival reactions. A bark needs breathing room or the rival becomes wallpaper,
    // so ordinary reactions share a cooldown; a panic is allowed to cut in.
    private const float BarkCooldown = 3.2f;
    private float _barkCooldown;
    private bool _playerWasAhead;
    private int _lastBarkCombo;
    private bool _clutchBarked;

    /// <summary>
    /// The crowd needs breathing room for exactly the reason the rival's barks do: a
    /// reaction to everything is wallpaper. The mood still moves silently on every
    /// bite - only the sound is rationed.
    /// </summary>
    private const float CrowdCooldown = 1.2f;
    private float _crowdCooldown;
    private int _lastCheeredCombo;

    private void StartIntro(MatchDef next)
    {
        _bout = new BoutSession(
            _foodTable, new SeededRandom((int)(Time.GetTicksMsec() & 0x7FFFFFFF)),
            new JawZone(GameRoot.JawCenterX, GameRoot.JawHalfWidth), GameRoot.SpawnX, GameRoot.RetireX, next, Career.Phases);

        _v.Rival.Setup(next.Opponent);
        _v.Belt.Clear();
        _v.Crumbs.Visible = true;
        _v.Hud.Visible = true;
        _stage = Stage.Intro;

        _barkCooldown = 0f;
        _playerWasAhead = false;
        _lastBarkCombo = 0;
        _clutchBarked = false;

        _v.Sfx.Play(Sfx.Blip);
        _v.Music.Duck(true);

        // The croc never speaks in words - he is starving, not chatty - so his half of
        // the exchange is body language. It still gives him a turn in the frame.
        _v.Dialogue.Play("croc", next.Opponent.SpriteId, new[]
        {
            new DialogueScene.Line(false, next.Opponent.Name, next.Opponent.Taunt),
            new DialogueScene.Line(true, "CROC", CrocReply(_round)),
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
        _v.Overlay.Hide();

        if (_dialogueWasInterlude)
        {
            // Coming back from an interlude the hands are already set, so the beat
            // before the bell is shorter than the one before the bout.
            _dialogueWasInterlude = false;
            _v.Hud.Visible = true;
            Render(_bout!.BeginNextPhase());

            _stage = Stage.Countdown;
            _countdown = 1.6f;
            _countdownShown = -1;
            return;
        }

        _v.Hud.ResetForNewMatch();

        // A beat of anticipation before the bell. Dropping straight from a taunt into
        // a moving belt gives the player no moment to set their hands.
        if (_bout is not null)
        {
            Render(_bout.Start());
            _v.Hud.Update(_bout.Current.State, 0, 0f, _money);
        }

        _stage = Stage.Countdown;
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
                _v.Overlay.Flash(remaining.ToString());
                _v.Sfx.Play(Sfx.Blip, 0.8f + 0.1f * (3 - remaining));
            }
        }

        if (_countdown > 0f) return;

        _v.Music.Duck(false);
        _v.Overlay.Flash("EAT!");
        _v.Croc.Anticipate();
        _v.Croc.PlayTaunt();
        _v.Sfx.Play(Sfx.Frenzy);
        _zoom = 1f;

        // Drop anything pressed during the countdown. Otherwise an eager player buys a
        // strike on the first frame of the match for a press they made before the bell.
        _chompQueued = false;
        _stage = Stage.Fighting;
    }


    private void StartInterlude(PhaseEnded ended)
    {
        if (_bout is null || !_bout.AwaitingInterlude) return;   // the last phase ends the bout

        _stage = Stage.Interlude;
        _v.Music.Duck(true);
        _v.Belt.Clear();
        _v.Hud.Visible = false;
        _dialogueWasInterlude = true;
        _v.Sfx.Play(Sfx.Blip);

        var def = _bout.Def.Opponent;
        var rivalAhead = ended.OpponentScore > ended.PlayerScore;

        _v.Dialogue.Play("croc", def.SpriteId, new[]
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
        if (_stage != Stage.Fighting) return;

        Render(_bout.Tick(dt));
        if (_stage != Stage.Fighting) return;

        var phase = _bout.Current;
        var frenzy = phase.Frenzy.Fraction;

        _v.Conveyor.Advance(phase.BeltSpeed, dt);
        _v.Conveyor.SetFrenzy(frenzy);

        // Ask the phase for the zone the judge is actually using, so what lights up is
        // exactly what scores - including while hunger has it held open.
        var jaw = phase.EffectiveJaw;
        _v.Conveyor.SetJawHalfWidth(jaw.HalfWidth);

        var occupied = false;
        foreach (var item in phase.Items)
        {
            if (!jaw.Overlaps(item)) continue;
            occupied = true;
            break;
        }

        _v.Conveyor.SetZoneOccupied(occupied);
        _v.Frenzy.SetAmount(frenzy);
        _v.Croc.SetGlow(frenzy);

        _v.Belt.BeltSpeed = phase.BeltSpeed;
        _v.Belt.Sync(phase.Items);
        _v.Belt.PruneMissing(phase.Items);
        _v.Hud.Update(phase.State, _bout.OpponentScore, frenzy, _money);
        _v.Hud.SetPot(phase.Pot.Amount, Pot.MultiplierForCombo(phase.State.Combo), _bout.PlayerScore);
        _v.Hud.SetHunger(phase.Hunger.Charge, phase.Hunger.IsActive);
        _v.Hud.SetCarried(_bout.PlayerScore);
        _v.Hud.SetShield(phase.Buffs.HasShield);
        _v.Croc.SetMagnet(phase.Buffs.MagnetBitesRemaining > 0);

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

        _v.World.Scale = Vector2.One * scale;
        _v.World.Position = offset + ZoomPivot * (1f - scale);

        if (_flashAlpha > 0f)
        {
            _flashAlpha = Mathf.Max(0f, _flashAlpha - FlashDecay * dt);
            _v.Flash.Modulate = Colors.White with { A = _flashAlpha };
        }

        if (_goldFlash > 0f)
        {
            _goldFlash = Mathf.Max(0f, _goldFlash - dt * 3f);
            _v.Gold.Modulate = Colors.White with { A = _goldFlash * 0.5f };
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
                    _v.Croc.PlayFlinch();   // a bomb: the sprite says so, not just the flash
                    _v.Belt.Remove(chomped.Item.Id);
                    break;
                case ChompedAir:
                    _v.Croc.PlayFlinch();
                    _v.Sfx.Play(Sfx.Whiff);
                    break;
                case Passed passed:
                    _v.Belt.Remove(passed.Item.Id);
                    _v.Sfx.Play(Sfx.Pass);
                    _v.Crowd.Drop(0.06f);   // silent: food goes past far too often to voice
                    break;
                case OpponentAte ate:
                    _v.Rival.Ate(ate.OpponentScore);
                    break;
                case FrenzyStarted:
                    _v.Music.SetFrenzy(true);
                    _v.Croc.Anticipate();
                    CrowdCheer(lift: 0.40f, spike: 0.9f, pitch: 1.05f);
                    _v.Sfx.Play(Sfx.Frenzy);
                    _shake = 3f;
                    _zoom = 1f;
                    _v.Croc.Punch(1.2f);
                    _v.Overlay.Flash("FRENZY!");
                    _v.Rival.Panic(RivalLine(r => r.LinePanic));
                    _barkCooldown = BarkCooldown;
                    break;
                case FrenzyEnded:
                    _v.Music.SetFrenzy(false);
                    break;
                case StrikeAdded:
                    OnStrike();
                    CrowdCommiserate(0.30f);
                    break;
                case PhaseStarted started:
                    _v.Music.SetFrenzy(false);
                    _v.Crowd.ResetForPhase();
                    _lastCheeredCombo = 0;
                    _v.Overlay.Flash(started.Phase.Name);
                    _v.Sfx.Play(Sfx.Blip, 1.1f);
                    _zoom = 1f;
                    _v.Hud.SetPhase(started.PhaseIndex, started.Phase.Name);
                    _v.Backdrop.SetPhase(started.PhaseIndex);
                    break;
                case PhaseEnded ended:
                    StartInterlude(ended);
                    break;
                case PhaseKnockout:
                    // The one thing the crowd actually boos: every tooth gone inside a
                    // single phase. Urgent, so it is never swallowed by the cooldown.
                    _v.Crowd.Drop(0.6f);
                    _v.Crowd.Slump(1f);
                    CrowdSay(Sfx.Boo, volumeDb: -4f, urgent: true);
                    _v.Overlay.Flash("OUT!");
                    _v.Sfx.Play(Sfx.Lose);
                    _shake = StrikeShake * 1.6f;
                    _flashAlpha = 1f;
                    break;
                case CoinSpawned coin:
                    _v.Belt.SetCoinValue(coin.Item.Id, coin.Value);
                    _v.Sfx.Play(Sfx.Blip, 1.4f);
                    break;
                case PotBanked banked:
                    OnBanked(banked);
                    break;
                case PotWiped wiped when wiped.Lost > 0:
                    _v.Sfx.Play(Sfx.Whiff, 0.7f);
                    _v.Overlay.Flash($"-{wiped.Lost}");
                    break;
                case BuffTaken taken:
                    _v.Croc.PlayGulp();
                    _v.Crowd.Lift(0.15f);
                    _v.Crowd.Spike(0.4f);
                    _v.Sfx.Play(Sfx.Frenzy, BuffPitch(taken.Kind));
                    _v.Overlay.Flash(BuffLabel(taken.Kind));
                    _v.Croc.Punch(1.2f);
                    break;
                case HungerStarted:
                    CrowdCheer(lift: 0.45f, spike: 1f, pitch: 0.95f);
                    _v.Overlay.Flash("HUNGRY");
                    _v.Sfx.Play(Sfx.Frenzy, 0.6f);
                    _flashAlpha = 0.6f;
                    _zoom = 1.4f;
                    _v.Rival.Panic(RivalLine(r => r.LinePanic));
                    _barkCooldown = BarkCooldown;
                    break;
                case HungerEnded:
                    _v.Sfx.Play(Sfx.Blip, 0.6f);
                    break;
                case BoutEnded bout:
                    _stage = Stage.Idle;
                    Finished?.Invoke(bout);
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
        _v.Sfx.Play(Sfx.Coin, 1f + 0.04f * banked.Multiplier);
        _goldFlash = 1f;
        _zoom = 1f;
        _hitStop = HitStopSeconds * 2f;
        _v.Croc.Punch(1.4f);
        _v.Croc.PlayGulp();
        _v.Overlay.Flash($"+{banked.Paid}");
        _v.Crumbs.Burst(new Vector2(GameRoot.JawCenterX, GameRoot.BeltY), 18, 1.6f);

        // A bigger payout is a bigger noise, up to a point.
        CrowdCheer(lift: 0.20f + Mathf.Min(banked.Paid, 300) / 1000f,
                   spike: 0.8f, pitch: 1f + 0.02f * banked.Multiplier);

        if (banked.Paid > 100)
        {
            _v.Rival.Rattle(RivalLine(r => r.LineLosing));
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

        _v.Croc.PlayEat();
        _v.Croc.Punch(golden ? 1.4f : 0.7f + 0.05f * Math.Min(chomped.Combo, 6));
        _v.Belt.Remove(chomped.Item.Id);

        _hitStop = golden ? HitStopSeconds * 2.2f : HitStopSeconds;
        _shake = Math.Max(_shake, ChompShake + Math.Min(chomped.Combo, 6) * 0.25f + (golden ? 3f : 0f));
        _v.Crumbs.Burst(new Vector2(GameRoot.JawCenterX, GameRoot.BeltY),
                      golden ? 22 : chomped.DuringFrenzy ? 12 : 8,
                      golden ? 1.8f : chomped.DuringFrenzy ? 1.3f : 1f);

        if (golden)
        {
            // The rare bite is the one moment worth spending every effect at once.
            _zoom = 1f;
            _goldFlash = 0.7f;
            _v.Sfx.Play(Sfx.Coin);
            _v.Overlay.Flash("GOLDEN!");
            _v.Rival.Rattle(RivalLine(r => r.LineLosing));
        }
        else
        {
            _v.Sfx.Play(chomped.Combo >= 4 || chomped.DuringFrenzy ? Sfx.Crunch : Sfx.Chomp,
                      1f + 0.03f * Math.Min(chomped.Combo, 8));
        }

        // Every bite lifts the room a little; only the milestones are allowed a voice.
        _v.Crowd.Lift(0.05f);

        if (chomped.Combo >= 5 && chomped.Combo % 5 == 0 && chomped.Combo != _lastCheeredCombo)
        {
            _lastCheeredCombo = chomped.Combo;
            CrowdCheer(lift: 0.25f, spike: 0.7f, pitch: 1f + 0.03f * (chomped.Combo / 5));
        }

        _v.Hud.PulseCombo();
        AddChild(ComboPopup.Create(new Vector2(GameRoot.JawCenterX + 28f, GameRoot.BeltY - 34f),
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
                _v.Rival.Rattle(RivalLine(r => r.LineLosing));
                _barkCooldown = BarkCooldown;
            }
            else if (!ahead && _playerWasAhead)
            {
                _v.Rival.Gloat(RivalLine(r => r.LineWinning));
                _barkCooldown = BarkCooldown;
            }
            else if (state.Combo >= 12 && state.Combo >= _lastBarkCombo + 6)
            {
                _lastBarkCombo = state.Combo;
                _v.Rival.Rattle(RivalLine(r => r.LineLosing));
                _barkCooldown = BarkCooldown;
            }
        }

        // The closing seconds, with the match already lost for them.
        if (!_clutchBarked && state.TimeRemaining <= 4f
            && _bout.PlayerScore > _bout.OpponentScore * 1.25f)
        {
            _clutchBarked = true;
            _v.Rival.Panic(RivalLine(r => r.LinePanic));
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
        _v.Sfx.Play(sound, pitch, volumeDb);
    }

    private void CrowdCheer(float lift, float spike, float pitch = 1f)
    {
        _v.Crowd.Lift(lift);
        _v.Crowd.Spike(spike);
        CrowdSay(Sfx.Cheer, volumeDb: 0f, pitch: pitch);
    }

    /// <summary>
    /// An ordinary mistake. The room winces with the player rather than at them - this
    /// game's whole premise is that anyone can finish it, so its harshest sound is not
    /// the one aimed at whoever is already struggling.
    /// </summary>
    private void CrowdCommiserate(float drop)
    {
        _v.Crowd.Drop(drop);
        _v.Crowd.Slump(0.5f);
        CrowdSay(Sfx.Aww, volumeDb: -7f);
    }


    private void OnStrike()
    {
        _shake = StrikeShake;
        _zoom = 0.6f;
        _v.Croc.Punch(0.5f);
        _flashAlpha = 0.5f;
        _v.Sfx.Play(Sfx.Strike);
        _v.Frenzy.SetAmount(0f);
        _v.Conveyor.SetFrenzy(0f);
    }
}
