using Godot;

namespace CrocGame;

/// <summary>
/// The jaws. Frame layout of croc_sheet.png, from croc_sheet.json:
/// idle 0-3, celebrate 4-11, eat 12-17, flinch 18-20, gulp 21-24, taunt 25-29,
/// all 32x32 in a single row.
/// </summary>
public partial class CrocView : AnimatedSprite2D
{
    public override void _Ready()
    {
        var sheet = ResourceLoader.Load<Texture2D>("res://Art/ExportedSprites/croc_sheet.png");
        if (sheet is null)
        {
            GD.PushError("Missing res://Art/ExportedSprites/croc_sheet.png");
            return;
        }

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        AddAnimation(frames, sheet, "idle", first: 0, count: 4, fps: 5f, loop: true);
        AddAnimation(frames, sheet, "celebrate", first: 4, count: 8, fps: 12f, loop: false);
        AddAnimation(frames, sheet, "eat", first: 12, count: 6, fps: 18f, loop: false);
        AddAnimation(frames, sheet, "flinch", first: 18, count: 3, fps: 14f, loop: false);
        AddAnimation(frames, sheet, "gulp", first: 21, count: 4, fps: 12f, loop: false);
        AddAnimation(frames, sheet, "taunt", first: 25, count: 5, fps: 9f, loop: false);

        SpriteFrames = frames;

        // The croc is the star of the screen and was far too small at 1:1 on a
        // 180px-wide canvas. Integer scale keeps every pixel square.
        Scale = Vector2.One * BaseScale;

        AnimationFinished += () => Play("idle");
        Play("idle");
    }

    private static void AddAnimation(SpriteFrames frames, Texture2D sheet, string name,
                                     int first, int count, float fps, bool loop)
    {
        frames.AddAnimation(name);
        frames.SetAnimationSpeed(name, fps);
        frames.SetAnimationLoopMode(name,
            loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);

        for (var i = 0; i < count; i++)
        {
            frames.AddFrame(name, new AtlasTexture
            {
                Atlas = sheet,
                Region = new Rect2((first + i) * 32, 0, 32, 32),
            });
        }
    }

    private float _punch;
    private const float BaseScale = 2f;

    /// <summary>
    /// Squash and stretch on a bite. A sprite that only swaps animation frames reads as
    /// a flipbook; a sprite that deforms reads as something with weight behind it.
    /// </summary>
    public void Punch(float amount = 1f) => _punch = Mathf.Max(_punch, amount);

    public override void _Process(double delta)
    {
        if (_punch <= 0f)
        {
            if (Scale != Vector2.One * BaseScale) Scale = Vector2.One * BaseScale;
            return;
        }

        _punch = Mathf.Max(0f, _punch - (float)delta * 6f);

        // Wide and short at the moment of the bite, easing back to square.
        Scale = new Vector2(
            BaseScale * (1f + 0.28f * _punch),
            BaseScale * (1f - 0.22f * _punch));
    }

    public void PlayEat()
    {
        if (_magnet) return;   // the jaws are being held open

        Play("eat");
    }

    private bool _magnet;

    /// <summary>
    /// The magnet suspends judging for three bites, so the jaws hold visibly open for
    /// the duration. The one thing that must never happen is the window quietly lying
    /// about its size: if the game is taking a bite for the player, they have to be
    /// able to see it doing that.
    /// </summary>
    public void SetMagnet(bool active)
    {
        if (_magnet == active) return;

        _magnet = active;

        if (active)
        {
            Play("eat");
            Frame = 2;          // the open-jaw frame
            SpeedScale = 0f;
        }
        else
        {
            SpeedScale = 1f;
            Play("idle");
        }
    }

    public void PlayCelebrate() => Play("celebrate");

    /// <summary>
    /// A miss. Until now a hit and a miss played the same "eat" animation and were told
    /// apart only by flash and shake; the sprite itself now says which happened.
    /// </summary>
    public void PlayFlinch()
    {
        if (_magnet) return;

        Play("flinch");
    }

    /// <summary>Something went down: a banked pot, or a buff taken.</summary>
    public void PlayGulp()
    {
        if (_magnet) return;

        Play("gulp");
    }

    public void PlayTaunt()
    {
        if (_magnet) return;

        Play("taunt");
    }

    /// <summary>
    /// Frenzy glow. Godot has no cheap per-sprite outline, so the croc is brightened
    /// on a pulse instead - readable at this scale and costs nothing.
    /// </summary>
    public void SetGlow(float amount)
    {
        // Gentle: a strong multiply blows the sprite out to pale green and costs
        // the black outline that holds the pixel art together.
        var glow = 1f + 0.3f * amount;
        Modulate = new Color(glow, glow, glow, 1f);
    }

    private Sprite2D? _cosmetic;

    /// <summary>
    /// The cosmetic the croc is wearing, as a child sprite so it inherits the
    /// squash-and-stretch. An accessory that does not deform with the head reads as a
    /// sticker stuck on the screen rather than something the croc has on.
    /// </summary>
    public void SetCosmetic(string spriteId)
    {
        _cosmetic?.QueueFree();
        _cosmetic = null;

        if (spriteId == "") return;

        var texture = ResourceLoader.Load<Texture2D>(
            $"res://Art/ExportedSprites/Cosmetics/{spriteId}.png");

        if (texture is null)
        {
            GD.PushWarning($"Missing cosmetic {spriteId}");
            return;
        }

        _cosmetic = new Sprite2D { Texture = texture, Position = CosmeticAnchor(spriteId), ZIndex = 1 };
        AddChild(_cosmetic);
    }

    /// <summary>
    /// Where each cosmetic sits on the 32x32 frame. These come from cosmetic_gen.py,
    /// which prints them on every run - they are not estimated here.
    /// </summary>
    private static Vector2 CosmeticAnchor(string spriteId) => spriteId switch
    {
        "skin_chef" => new Vector2(0, -11),
        "skin_gold" => new Vector2(4, 4),
        "skin_shadow" => new Vector2(0, -4),
        _ => new Vector2(0, -12),
    };
}
