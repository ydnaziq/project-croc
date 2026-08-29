using Godot;

namespace CrocGame;

/// <summary>
/// The jaws. Frame layout of croc_sheet.png, from croc_sheet.json:
/// idle 0-3, celebrate 4-11, eat 12-17, all 32x32 in a single row.
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

    public void PlayEat() => Play("eat");

    public void PlayCelebrate() => Play("celebrate");

    /// <summary>
    /// Frenzy glow. Godot has no cheap per-sprite outline, so the croc is brightened
    /// on a pulse instead - readable at this scale and costs nothing.
    /// </summary>
    public void SetGlow(float amount, Color skinTint)
    {
        var glow = 1f + 0.9f * amount;
        Modulate = new Color(skinTint.R * glow, skinTint.G * glow, skinTint.B * glow, skinTint.A);
    }
}
