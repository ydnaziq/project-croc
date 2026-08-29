using System.Collections.Generic;
using System.Linq;
using CrocGame.Core;
using Godot;

namespace CrocGame;

/// <summary>
/// A self-playing smoke test, enabled only by the <c>--autoplay</c> command line flag.
///
/// It exists because this project is developed on a machine with no Godot editor open,
/// so every layout decision was arithmetic rather than something anyone had looked at.
/// This drives the game the way a competent player would - advance the screens, bite
/// whenever something is inside the jaw zone - and writes screenshots at fixed times,
/// which makes "does this actually look right" a question with an answer.
///
/// It never runs during normal play: no flag, no node.
/// </summary>
public partial class AutoPlay : Node
{
    private const string Flag = "--autoplay";
    private const string ShotDirArg = "--shots=";

    private GameRoot _root = null!;
    private string _shotDir = "";
    private float _elapsed;
    private int _nextShot;
    private float _pressCooldown;

    /// <summary>Seconds at which to capture, chosen to land on each distinct screen.</summary>
    private static readonly float[] ShotTimes =
        { 0.8f, 2.6f, 4.4f, 7.0f, 10.5f, 13.0f, 16.5f, 20.0f, 24.0f, 28.0f, 33.0f, 38.0f, 44.0f, 50.0f };

    // A bout is now ~27s of play plus two interludes, so 45s no longer reaches the shop.
    private const float QuitAfter = 55f;

    public static bool Requested => OS.GetCmdlineUserArgs().Contains(Flag)
                                    || new List<string>(OS.GetCmdlineArgs()).Contains(Flag);

    public static AutoPlay? TryCreate(GameRoot root)
    {
        if (!Requested) return null;

        var shots = "";
        foreach (var arg in OS.GetCmdlineArgs())
        {
            if (arg.StartsWith(ShotDirArg)) shots = arg[ShotDirArg.Length..];
        }
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith(ShotDirArg)) shots = arg[ShotDirArg.Length..];
        }

        return new AutoPlay { _root = root, _shotDir = shots };
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _elapsed += dt;
        _pressCooldown -= dt;

        Drive();
        Capture();

        if (_elapsed >= QuitAfter)
        {
            GD.Print($"autoplay: finished after {_elapsed:F1}s");
            GetTree().Quit();
        }
    }

    /// <summary>Plays: advances any screen that is waiting, and bites on sight.</summary>
    private void Drive()
    {
        if (_pressCooldown > 0f) return;

        if (_root.AutoPlayShouldPress())
        {
            _root.AutoPlayPress();
            _pressCooldown = _root.AutoPlayInMatch ? 0.05f : 1.8f;
        }
    }

    private void Capture()
    {
        if (_shotDir == "" || _nextShot >= ShotTimes.Length) return;
        if (_elapsed < ShotTimes[_nextShot]) return;

        var index = _nextShot++;
        var image = GetViewport().GetTexture().GetImage();
        var path = $"{_shotDir}/shot_{index:D2}.png";

        var error = image.SavePng(path);
        GD.Print(error == Error.Ok
            ? $"autoplay: wrote {path} at {_elapsed:F1}s"
            : $"autoplay: FAILED to write {path}: {error}");
    }
}
