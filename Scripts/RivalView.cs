using CrocGame.Core;
using Godot;

namespace CrocGame;

/// <summary>
/// The opponent on their own stage: sprite, name, and a score you can watch climbing.
/// The rival's bites are the pressure in the match, so they have to be visible.
/// </summary>
public partial class RivalView : Node2D
{
    private AnimatedSprite2D _sprite = null!;
    private Label _name = null!;
    private Label _score = null!;
    private float _bitePulse;

    public void Setup(OpponentDef def)
    {
        BuildSprite(def.SpriteId);
        _name.Text = def.Name;
        _score.Text = "0";
    }

    public override void _Ready()
    {
        _name = new Label
        {
            Position = new Vector2(0, -48),
            Size = new Vector2(GameRoot.ViewportWidth, 12),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = Ui.Text(Ui.Small, Ui.Paper),
        };
        AddChild(_name);

        _score = new Label
        {
            Position = new Vector2(0, 36),
            Size = new Vector2(GameRoot.ViewportWidth, 18),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = Ui.Text(Ui.Body, Ui.Rival),
        };
        AddChild(_score);
    }

    private void BuildSprite(string spriteId)
    {
        _sprite?.QueueFree();

        var sheet = ResourceLoader.Load<Texture2D>($"res://Art/ExportedSprites/{spriteId}_sheet.png");
        if (sheet is null)
        {
            GD.PushWarning($"Missing {spriteId}_sheet.png; the rival will be invisible.");
            return;
        }

        // Same layout as the croc: idle 0-3, celebrate 4-11, eat 12-17.
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        AddAnimation(frames, sheet, "idle", 0, 4, 5f, true);
        AddAnimation(frames, sheet, "celebrate", 4, 8, 12f, false);
        AddAnimation(frames, sheet, "eat", 12, 6, 18f, false);

        _sprite = new AnimatedSprite2D { SpriteFrames = frames, Scale = new Vector2(2, 2) };
        _sprite.AnimationFinished += () => _sprite.Play("idle");
        AddChild(_sprite);
        _sprite.Play("idle");
    }

    private static void AddAnimation(SpriteFrames frames, Texture2D sheet, string name,
                                     int first, int count, float fps, bool loop)
    {
        frames.AddAnimation(name);
        frames.SetAnimationSpeed(name, fps);
        frames.SetAnimationLoopMode(name, loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);

        for (var i = 0; i < count; i++)
        {
            frames.AddFrame(name, new AtlasTexture
            {
                Atlas = sheet,
                Region = new Rect2((first + i) * 32, 0, 32, 32),
            });
        }
    }

    /// <summary>The rival took a bite: play the animation and punch the score.</summary>
    public void Ate(int score)
    {
        _score.Text = score.ToString();
        _bitePulse = 1f;

        // Never interrupt a reaction with a chew; the reaction is the point.
        if (_sprite is not null && _sprite.Animation != "celebrate") _sprite.Play("eat");
    }

    /// <summary>The rival gloats, with the animation to match.</summary>
    public void Gloat(string line)
    {
        _sprite?.Play("celebrate");
        Say(line, Bark.Mood.Smug);
    }

    /// <summary>The rival is losing ground.</summary>
    public void Rattle(string line) => Say(line, Bark.Mood.Rattled);

    /// <summary>The rival is being demolished.</summary>
    public void Panic(string line)
    {
        _panicShake = 1f;
        Say(line, Bark.Mood.Panicked);
    }

    private void Say(string line, Bark.Mood mood)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        _bark?.QueueFree();
        _bark = Bark.Create(new Vector2(0, -34), line, mood);
        AddChild(_bark);
    }

    private Bark? _bark;
    private float _panicShake;

    public override void _Process(double delta)
    {
        if (_panicShake > 0f && _sprite is not null)
        {
            _panicShake = Mathf.Max(0f, _panicShake - (float)delta * 1.6f);
            _sprite.Position = new Vector2(Mathf.Sin(_panicShake * 60f) * 2f * _panicShake, 0f);
        }

        if (_bitePulse <= 0f) return;

        _bitePulse = Mathf.Max(0f, _bitePulse - (float)delta * 5f);
        _score.Scale = Vector2.One * (1f + 0.25f * _bitePulse);
        _score.Position = new Vector2(-GameRoot.ViewportWidth * (_score.Scale.X - 1f) / 2f, 36);
    }
}
