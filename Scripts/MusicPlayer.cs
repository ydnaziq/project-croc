using Godot;

namespace CrocGame;

/// <summary>
/// The theme, looping under the whole game.
///
/// It is started once and never restarted, so the track runs continuously across the
/// title, the bouts, the interludes and the shop rather than cutting on every screen
/// change. A theme that restarts at each transition tells the player the game is a
/// series of menus.
///
/// It also carries state, the same way the chomp's pitch already does: it ducks for
/// dialogue, where the point is that somebody is talking, and lifts during a frenzy.
/// </summary>
public partial class MusicPlayer : AudioStreamPlayer
{
    /// <summary>
    /// The author-supplied Music/croc.mp3 has 6.5 seconds of silence on the end, which
    /// would be a dead gap on every loop. croc_loop.ogg is that file trimmed to its
    /// musical end with a short fade; the mp3 stays in the repo untouched as the source.
    /// </summary>
    private const string TrackPath = "res://Music/croc_loop.ogg";

    /// <summary>
    /// Under the effects, which are themselves peak-limited to 0.32. The chomp is
    /// information and the music is atmosphere, so the music loses.
    /// </summary>
    private const float FullDb = -11f;
    private const float DuckedDb = -22f;

    private float _targetDb = FullDb;

    public override void _Ready()
    {
        var stream = ResourceLoader.Load<AudioStream>(TrackPath);

        if (stream is null)
        {
            GD.PushWarning($"Missing {TrackPath}; the game runs without music.");
            return;
        }

        // A theme that stops after a minute is worse than no theme.
        if (stream is AudioStreamOggVorbis ogg) ogg.Loop = true;

        Stream = stream;
        VolumeDb = FullDb;
    }

    public void Begin()
    {
        if (Stream is not null && !Playing) Play();
    }

    /// <summary>Pulls the bed down so a line of dialogue is the thing being heard.</summary>
    public void Duck(bool quiet) => _targetDb = quiet ? DuckedDb : FullDb;

    public void SetFrenzy(bool active) => PitchScale = active ? 1.06f : 1f;

    public override void _Process(double delta)
    {
        if (Stream is null) return;

        // Eased, not switched: a volume that jumps is more noticeable than the change
        // it is trying to make.
        if (!Mathf.IsEqualApprox(VolumeDb, _targetDb))
        {
            VolumeDb = Mathf.MoveToward(VolumeDb, _targetDb, (float)delta * 24f);
        }
    }
}
