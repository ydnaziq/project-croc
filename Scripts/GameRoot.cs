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

    /// <summary>Items spawn and retire off-screen so nothing pops in or out in view.</summary>
    public const float SpawnX = -20f;
    public const float RetireX = ViewportWidth + 12f;

    /// <summary>The line food travels along - level with the croc's mouth.</summary>
    public const float BeltY = 160f;

    /// <summary>
    /// The croc sprite is a standing body 32px tall whose mouth sits about 3px above
    /// the sprite centre, so the sprite is offset down to put the mouth on the belt.
    /// </summary>
    public const float CrocMouthOffsetY = 3f;

    // Feel. Timing games live or die on feedback, so these are core, not polish.
    private const float HitStopSeconds = 0.06f;
    private const float StrikeShake = 4f;
    private const float ShakeDecay = 14f;
    private const float FlashDecay = 4.5f;

    private enum Phase { Title, Running, GameOver }

    private GameSession _session = null!;
    private ISaveStore _saveStore = null!;
    private FoodTable _foodTable = null!;

    private Node2D _world = null!;
    private ConveyorView _conveyorView = null!;
    private BeltView _beltView = null!;
    private CrocView _crocView = null!;
    private CpuParticles2D _crumbs = null!;
    private ColorRect _flash = null!;
    private Hud _hud = null!;
    private ScreenOverlay _overlay = null!;
    private Sfx _sfx = null!;

    private Phase _phase = Phase.Title;
    private bool _chompQueued;

    private float _hitStop;
    private float _shake;
    private float _flashAlpha;

    private readonly RandomNumberGenerator _shakeRng = new();

    /// <summary>
    /// Cosmetic skins are a tint on the existing croc sprite rather than new art, so
    /// unlocking one costs nothing to produce. Later entries win when several are
    /// unlocked, matching the order they are earned in UnlockCatalog.All.
    /// </summary>
    private static readonly Dictionary<string, Color> CrocSkins = new()
    {
        ["croc_gold"] = new Color("f8d878"),
        ["croc_blue"] = new Color("8898e8"),
        ["croc_pink"] = new Color("f8a8d8"),
        ["croc_ghost"] = new Color("ffffff", 0.65f),
    };

    public override void _Ready()
    {
        _saveStore = new GodotSaveStore();
        _foodTable = LoadFoodTable();

        _sfx = new Sfx();
        AddChild(_sfx);

        // Everything inside _world shakes together; the HUD and overlay do not.
        _world = new Node2D();
        AddChild(_world);

        _crocView = new CrocView { Position = new Vector2(JawCenterX, BeltY + CrocMouthOffsetY) };
        _world.AddChild(_crocView);

        // The conveyor draws in front of the croc, so the croc stands behind the belt.
        _conveyorView = new ConveyorView();
        _world.AddChild(_conveyorView);

        _beltView = new BeltView();
        _world.AddChild(_beltView);

        _crumbs = BuildCrumbs();
        _world.AddChild(_crumbs);

        _flash = new ColorRect
        {
            Size = new Vector2(ViewportWidth, ViewportHeight),
            Color = new Color("f83800"),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = Colors.White with { A = 0f },
        };
        AddChild(_flash);

        _hud = new Hud();
        AddChild(_hud);

        _overlay = new ScreenOverlay();
        AddChild(_overlay);

        var save = _saveStore.Load();
        ApplySkin(save);

        _overlay.Show("CROC", save.BestScore > 0
            ? $"best {save.BestScore}\npress to start"
            : "press to start");
    }

    private static CpuParticles2D BuildCrumbs() => new()
    {
        Position = new Vector2(JawCenterX, BeltY),
        Emitting = false,
        OneShot = true,
        Explosiveness = 1f,
        Amount = 10,
        Lifetime = 0.45,
        Direction = new Vector2(0, -1),
        Spread = 55f,
        InitialVelocityMin = 30f,
        InitialVelocityMax = 70f,
        Gravity = new Vector2(0, 220f),
        ScaleAmountMin = 1f,
        ScaleAmountMax = 2f,
        Color = new Color("f8d878"),
    };

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

    private void StartRun()
    {
        var seed = (int)(Time.GetTicksMsec() & 0x7FFFFFFF);

        _session = new GameSession(
            _foodTable,
            new SeededRandom(seed),
            new JawZone(JawCenterX, JawHalfWidth),
            SpawnX,
            RetireX);

        _hud.Set(0, 0, 0);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Buffered rather than acted on immediately, so a press between frames is
        // never dropped and is judged against the same tick the player saw.
        if (@event.IsActionPressed("chomp")) _chompQueued = true;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        DecayEffects(dt);

        if (_phase != Phase.Running)
        {
            if (!_chompQueued) return;

            _chompQueued = false;
            _sfx.Play(Sfx.Blip);
            _overlay.Hide();
            StartRun();
            _phase = Phase.Running;
            return;
        }

        // Hit-stop freezes the session rather than slowing it: because Core is
        // dt-driven, not calling Tick cannot consume part of a later timing window.
        if (_hitStop > 0f)
        {
            _hitStop -= dt;
            return;
        }

        if (_chompQueued)
        {
            _chompQueued = false;
            Render(_session.Chomp());
        }

        if (_phase != Phase.Running) return;

        Render(_session.Tick(dt));

        _conveyorView.Advance(Difficulty.ForEaten(_session.State.Eaten).BeltSpeed, dt);
        _beltView.Sync(_session.Items);
        _beltView.PruneMissing(_session.Items);
        _hud.Set(_session.State.Score, _session.State.Combo, _session.State.Strikes);
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
                    // An inedible in the jaws: the bite lands, the news is bad.
                    _crocView.PlayEat();
                    _beltView.Remove(chomped.Item.Id);
                    break;
                case ChompedAir:
                    _crocView.PlayEat();
                    _sfx.Play(Sfx.Whiff);
                    break;
                case Passed passed:
                    _beltView.Remove(passed.Item.Id);
                    _sfx.Play(Sfx.Pass);
                    break;
                case StrikeAdded:
                    OnStrike();
                    break;
                case RunEnded ended:
                    EndRun(ended);
                    break;
            }
        }
    }

    private void OnEaten(Chomped chomped)
    {
        _crocView.PlayEat();
        _beltView.Remove(chomped.Item.Id);

        _hitStop = HitStopSeconds;
        _crumbs.Restart();

        // Pitch climbs with the combo so a streak audibly builds, capped so it never
        // turns into a squeak.
        _sfx.Play(Sfx.Chomp, 1f + 0.04f * Math.Min(chomped.Combo, 8));

        AddChild(ComboPopup.Create(
            new Vector2(JawCenterX, BeltY - 14f), chomped.ScoreAwarded, chomped.Combo));
    }

    private void OnStrike()
    {
        _shake = StrikeShake;
        _flashAlpha = 0.55f;
        _sfx.Play(Sfx.Strike);
    }

    private void EndRun(RunEnded ended)
    {
        var data = _saveStore.Load();
        var isBest = ended.FinalScore > data.BestScore;

        data.BestScore = Math.Max(data.BestScore, ended.FinalScore);
        data.LifetimeEaten += ended.Eaten;

        var earned = UnlockCatalog.Apply(data);
        _saveStore.Save(data);
        ApplySkin(data);

        if (isBest || earned.Count > 0) _crocView.PlayCelebrate();

        _sfx.Play(Sfx.GameOver);
        _beltView.Clear();
        _hitStop = 0f;
        _phase = Phase.GameOver;

        _overlay.Show($"{ended.FinalScore}", Subtitle(data, isBest, earned));
    }

    private static string Subtitle(SaveData data, bool isBest, IReadOnlyList<Milestone> earned)
    {
        if (earned.Count > 0)
        {
            var names = string.Join(", ", earned.Select(m => m.Label));
            return $"unlocked {names}!\npress to retry";
        }

        return isBest ? "new best!\npress to retry" : $"best {data.BestScore}\npress to retry";
    }

    /// <summary>Wears the most recently earned skin the save has unlocked.</summary>
    private void ApplySkin(SaveData data)
    {
        var tint = Colors.White;

        foreach (var milestone in UnlockCatalog.All)
        {
            if (data.UnlockedIds.Contains(milestone.Id) && CrocSkins.TryGetValue(milestone.Id, out var color))
            {
                tint = color;
            }
        }

        _crocView.Modulate = tint;
    }
}
