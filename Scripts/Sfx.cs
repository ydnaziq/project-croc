using System.Collections.Generic;
using Godot;

namespace CrocGame;

/// <summary>
/// One player per sound. Retriggering a sound restarts it rather than layering, which
/// is what you want for a game where the same chomp fires many times a second.
/// </summary>
public partial class Sfx : Node
{
    public const string Chomp = "chomp";
    public const string Whiff = "whiff";
    public const string Pass = "pass";
    public const string Strike = "strike";
    public const string GameOver = "gameover";
    public const string Blip = "blip";

    private static readonly string[] Names = { Chomp, Whiff, Pass, Strike, GameOver, Blip };

    private readonly Dictionary<string, AudioStreamPlayer> _players = new();

    public override void _Ready()
    {
        foreach (var name in Names)
        {
            var path = $"res://Art/Audio/{name}.ogg";
            var stream = ResourceLoader.Load<AudioStream>(path);

            if (stream is null)
            {
                GD.PushWarning($"Missing sound {path}; it will be silent.");
                continue;
            }

            var player = new AudioStreamPlayer { Stream = stream, Bus = "Master" };
            AddChild(player);
            _players[name] = player;
        }
    }

    /// <summary>Plays a sound, or does nothing if it failed to load. Never throws.</summary>
    public void Play(string name, float pitch = 1f)
    {
        if (!_players.TryGetValue(name, out var player)) return;

        player.PitchScale = pitch;
        player.Play();
    }
}
