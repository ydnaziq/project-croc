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

    /// <summary>
    /// Screens. The four states a bout passes through - taunt, countdown, belt,
    /// interlude - are BoutDirector's business and are not visible here.
    /// </summary>
    private enum Phase { Title, Bout, Result, Shop }

    private ISaveStore _saveStore = null!;
    private FoodTable _foodTable = null!;
    private SaveData _save = new();
    private BoutDirector _director = null!;

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
    private MusicPlayer _music = null!;

    private Phase _phase = Phase.Title;

    /// <summary>
    /// Input is read in _UnhandledInput and buffered to the next tick, so a press is
    /// never dropped between frames. The bout keeps its own buffer for the same reason.
    /// </summary>
    private bool _chompQueued;
    private ColorRect _gold = null!;

    public override void _Ready()
    {
        _saveStore = new GodotSaveStore();
        _save = _saveStore.Load();
        _foodTable = LoadFoodTable();

        _sfx = new Sfx();
        AddChild(_sfx);

        _music = new MusicPlayer();
        AddChild(_music);

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
        AddChild(_dialogue);

        _shop = new ShopScreen();
        _shop.BuyRequested += OnBuy;
        _shop.ContinueRequested += StartNextMatch;
        AddChild(_shop);

        _director = new BoutDirector(
            new BoutDirector.Views(_world, _backdrop, _crowd, _rival, _croc, _conveyor,
                                   _belt, _crumbs, _frenzy, _flash, _gold, _hud,
                                   _overlay, _dialogue, _sfx, _music),
            _foodTable);
        _director.Finished += EndBout;
        AddChild(_director);

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
    /// <summary>
    /// Picks the next rung and hands it to the director. Which rival comes next is
    /// career business; how a bout is run is not.
    /// </summary>
    private void StartBout()
    {
        var next = Career.NextMatch(_save);
        if (next is null)
        {
            _save.DefeatedIds.Clear();   // champion: the ladder loops for another run
            next = Career.NextMatch(_save)!;
        }

        _phase = Phase.Bout;
        _crumbs.Visible = true;
        _director.Begin(next, _save.Money, Career.Progress(_save));
    }

    private void StartNextMatch()
    {
        _shop.Close();
        StartBout();
    }

    private void EndBout(BoutEnded ended)
    {
        _phase = Phase.Result;
        _frenzy.SetAmount(0f);
        _conveyor.SetFrenzy(0f);
        _belt.Clear();
        _hud.Visible = true;

        if (ended.Result == BoutResult.Won)
        {
            Career.RecordWin(_save, ended);
            _crowd.Lift(1f);
            _crowd.Spike(1f);
            _sfx.Play(Sfx.Cheer);
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
            _sfx.Play(Sfx.Aww, 1f, -6f);
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
    private void OpenShop()
    {
        _phase = Phase.Shop;
        _music.Duck(true);
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

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_phase == Phase.Shop) return;   // the shop reads its own input
        if (@event.IsActionPressed("chomp")) _chompQueued = true;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        switch (_phase)
        {
            case Phase.Title:
                if (TakeChomp())
                {
                    _music.Begin();
                    _sfx.Play(Sfx.Blip);
                    _overlay.Hide();
                    StartBout();
                }
                return;

            case Phase.Bout:
                if (TakeChomp()) _director.Press();
                _director.Tick(dt);
                return;

            case Phase.Result:
                if (TakeChomp()) OpenShop();
                return;

            case Phase.Shop:
                return;
        }
    }

    // ---------------------------------------------------------------- autoplay
    // Narrow hooks for the --autoplay smoke test. They expose only what a player
    // could see and do, so the harness cannot pass a test the real game would fail.

    /// <summary>True while a match is actually running.</summary>
    public bool AutoPlayInMatch => _director.InMatch;

    /// <summary>Whether a competent player would press right now.</summary>
    public bool AutoPlayShouldPress()
    {
        var phase = _director.CurrentPhase;
        if (phase is null) return true;   // every other screen wants a press

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

}