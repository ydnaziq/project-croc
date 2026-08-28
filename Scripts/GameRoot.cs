using System.Collections.Generic;
using CrocGame.Core;
using Godot;

namespace CrocGame;

public partial class GameRoot : Node2D
{
    public const float JawCenterX = 100f;
    public const float JawHalfWidth = 12f;
    public const float SpawnX = -20f;
    public const float RetireX = 340f;
    public const float BeltY = 120f;

    private enum Phase { Title, Running, GameOver }

    private GameSession _session = null!;
    private ISaveStore _saveStore = null!;
    private FoodTable _foodTable = null!;

    private BeltView _beltView = null!;
    private CrocView _crocView = null!;
    private Hud _hud = null!;
    private ScreenOverlay _overlay = null!;

    private Phase _phase = Phase.Title;
    private bool _chompQueued;

    public override void _Ready()
    {
        _saveStore = new GodotSaveStore();
        _foodTable = LoadFoodTable();

        _beltView = new BeltView();
        AddChild(_beltView);

        _crocView = new CrocView { Position = new Vector2(JawCenterX, BeltY) };
        AddChild(_crocView);

        _hud = new Hud();
        AddChild(_hud);

        _overlay = new ScreenOverlay();
        AddChild(_overlay);

        var best = _saveStore.Load().BestScore;
        _overlay.Show("CROC", best > 0 ? $"best {best} - press to start" : "press to start");
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
        if (_phase != Phase.Running)
        {
            if (!_chompQueued) return;

            _chompQueued = false;
            _overlay.Hide();
            StartRun();
            _phase = Phase.Running;
            return;
        }

        if (_chompQueued)
        {
            _chompQueued = false;
            Render(_session.Chomp());
        }

        Render(_session.Tick((float)delta));

        _beltView.Sync(_session.Items);
        _beltView.PruneMissing(_session.Items);
        _hud.Set(_session.State.Score, _session.State.Combo, _session.State.Strikes);
    }

    private void Render(IReadOnlyList<GameEvent> events)
    {
        foreach (var evt in events)
        {
            switch (evt)
            {
                case Chomped chomped:
                    _crocView.PlayEat();
                    _beltView.Remove(chomped.Item.Id);
                    break;
                case ChompedAir:
                    _crocView.PlayEat();
                    break;
                case Passed passed:
                    _beltView.Remove(passed.Item.Id);
                    break;
                case RunEnded ended:
                    EndRun(ended);
                    break;
            }
        }
    }

    private void EndRun(RunEnded ended)
    {
        var data = _saveStore.Load();
        var isBest = ended.FinalScore > data.BestScore;

        data.BestScore = System.Math.Max(data.BestScore, ended.FinalScore);
        data.LifetimeEaten += ended.Eaten;
        _saveStore.Save(data);

        if (isBest) _crocView.PlayCelebrate();

        _beltView.Clear();
        _phase = Phase.GameOver;
        _overlay.Show(
            $"{ended.FinalScore}",
            isBest ? "new best - press to retry" : $"best {data.BestScore} - press to retry");
    }
}
