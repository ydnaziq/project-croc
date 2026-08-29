using Godot;

namespace CrocGame;

/// <summary>
/// The stands, drawn every frame instead of baked into arena.png, because a crowd that
/// cannot move is furniture.
///
/// The crowd is a neutral judge: it reacts to how the player is playing, not to who is
/// winning. Good play lifts it, mistakes drop it, and the whole thing decays back
/// toward a low idle so a single good phase does not leave the room permanently loud.
///
/// It is deliberately on the player's side. An ordinary mistake gets a sympathetic
/// "aww" - a room wincing with you - and the boo is reserved for losing every tooth
/// inside one phase. A game whose whole point is that anyone can finish it should not
/// spend its harshest sound on the player who is already having the worst time.
///
/// Everything here is presentation, so none of it lives in Core.
/// </summary>
public partial class Crowd : Node2D
{
    // The stand runs y=140..172 in arena.png. These are the two staggered rows that
    // used to be painted into it, at the same y and the same spacing.
    private const float BackRowY = 150f;
    private const float FrontRowY = 162f;
    private const int Spacing = 21;

    private const float BackRowStartX = 8f;
    private const float FrontRowStartX = -2f;

    /// <summary>Hype never falls to nothing: an empty room is not a punishment, it is
    /// just a dead screen.</summary>
    private const float Floor = 0.18f;

    private const float DecayPerSecond = 0.11f;

    private static readonly Color Back = new("383860");
    private static readonly Color Front = new("4a4a78");
    private static readonly Color Excited = new("6a6ab0");

    /// <summary>0 to 1. Drives bob height, bob speed, brightness, and how many of them
    /// are on their feet - four things at once, so the stand reads as a dial rather
    /// than a switch.</summary>
    public float Hype { get; private set; } = Floor;

    private float _time;

    /// <summary>A momentary jolt on top of the mood: the whole stand leaves its seat at
    /// once. Decays fast, so it punctuates rather than lingers.</summary>
    private float _spike;

    /// <summary>Negative spike. The room sinks rather than jumps.</summary>
    private float _slump;

    public override void _Ready() => ZIndex = -9;

    public void Lift(float amount) => Hype = Mathf.Clamp(Hype + amount, Floor, 1f);

    public void Drop(float amount) => Hype = Mathf.Clamp(Hype - amount, Floor, 1f);

    public void Spike(float amount) => _spike = Mathf.Max(_spike, amount);

    public void Slump(float amount) => _slump = Mathf.Max(_slump, amount);

    /// <summary>
    /// Each phase starts the room near neutral. FEAST should have to earn its noise
    /// rather than inherit it from a good PLAIN.
    /// </summary>
    public void ResetForPhase()
    {
        Hype = Floor + 0.12f;
        _spike = 0f;
        _slump = 0f;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _time += dt;

        Hype = Mathf.Max(Floor, Hype - DecayPerSecond * dt);
        _spike = Mathf.Max(0f, _spike - dt * 2.6f);
        _slump = Mathf.Max(0f, _slump - dt * 2.2f);

        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRow(BackRowStartX, BackRowY, Back, seed: 0);
        DrawRow(FrontRowStartX, FrontRowY, Front, seed: 7);
    }

    private void DrawRow(float startX, float y, Color baseColor, int seed)
    {
        // Brightness climbs with the mood, so a loud room is visibly lighter without
        // anything on the stand changing shape.
        var color = baseColor.Lerp(Excited, Hype * 0.75f);

        var amplitude = 1f + Hype * 2f;
        var speed = 2f + Hype * 4f;

        var index = 0;
        for (var x = startX; x < GameRoot.ViewportWidth + Spacing; x += Spacing, index++)
        {
            // A fixed per-person offset makes the bob travel along the row as a wave.
            // In unison it reads as one object scaling, which is not what a crowd is.
            var offset = (index * 37 + seed * 13) % 17 / 17f * Mathf.Tau;
            var bob = Mathf.Sin(_time * speed + offset) * amplitude;

            // Whether this one is on their feet. Threshold per person, so the stand
            // fills up gradually rather than everyone standing on the same frame.
            var standing = Hype > 0.35f + (index * 29 % 11) / 11f * 0.55f;
            var lift = (standing ? 3f : 0f) + _spike * 5f - _slump * 3f;

            // Whole pixels only. A silhouette on a fractional pixel resamples and the
            // 1px edge this whole project is built on goes soft.
            DrawPerson(Mathf.Round(x), Mathf.Round(y - bob - lift), color,
                       armsUp: standing && (Hype > 0.6f || _spike > 0.3f));
        }
    }

    /// <summary>
    /// Head and shoulders, the same eight-pixel silhouette arena_gen.py used to bake in.
    /// Detail here would compete with the food, which is what the player has to read
    /// under time pressure.
    /// </summary>
    private void DrawPerson(float cx, float cy, Color c, bool armsUp)
    {
        DrawRect(new Rect2(cx - 4, cy - 4, 8, 8), c);
        DrawRect(new Rect2(cx - 5, cy - 2, 1, 5), c);
        DrawRect(new Rect2(cx + 4, cy - 2, 1, 5), c);
        DrawRect(new Rect2(cx - 7, cy + 4, 14, 6), c);

        if (!armsUp) return;

        // Arms are two pixels above the shoulder line. At this size that is the whole
        // difference between sitting and celebrating.
        DrawRect(new Rect2(cx - 7, cy - 5, 2, 5), c);
        DrawRect(new Rect2(cx + 5, cy - 5, 2, 5), c);
    }
}
