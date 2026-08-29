using System.Collections.Generic;
using CrocGame.Core;
using Godot;

namespace CrocGame;

/// <summary>Mirrors Core's item list into sprites. Owns no game state.</summary>
public partial class BeltView : Node2D
{
    private readonly Dictionary<int, Sprite2D> _sprites = new();
    private readonly Dictionary<string, Texture2D> _textures = new();
    private readonly Dictionary<int, float> _ages = new();
    private readonly Dictionary<int, Label> _coinLabels = new();

    /// <summary>Seconds an item spends settling after it appears.</summary>
    private const float LandingSeconds = 0.2f;

    /// <summary>Belt speed this frame, so the tumble can be tied to it.</summary>
    public float BeltSpeed { get; set; } = 120f;

    public void Sync(IReadOnlyList<FoodItem> items)
    {
        foreach (var item in items)
        {
            if (!_sprites.TryGetValue(item.Id, out var sprite))
            {
                sprite = new Sprite2D { Texture = TextureFor(item.TypeId) };
                AddChild(sprite);
                _sprites[item.Id] = sprite;
                _ages[item.Id] = 0f;
            }

            // Rolling, not sliding. Tying the tumble to belt speed makes a fast belt
            // look fast even in a still frame. Whole pixels only: a sprite on a
            // fractional pixel resamples and loses its outline.
            var settle = Mathf.Clamp(item.Age / LandingSeconds, 0f, 1f);
            var hop = (1f - settle) * Mathf.Sin(settle * Mathf.Pi) * 5f;

            sprite.Position = new Vector2(Mathf.Round(item.X), Mathf.Round(GameRoot.BeltY - hop));

            // Pop in over the first fraction of a second, and give the golden bite a
            // constant shimmer so the rare item announces itself on the belt.
            var age = _ages.TryGetValue(item.Id, out var a) ? a : 1f;
            var pop = Mathf.Min(1f, age * 8f);
            var overshoot = 1f + 0.35f * Mathf.Sin(pop * Mathf.Pi);
            sprite.Scale = Vector2.One * pop * overshoot;

            // Food tumbles as it rides; buffs and the coin stay upright so they read
            // as objects rather than more food going past.
            if (item.Power == "")
            {
                sprite.Rotation = item.X / 16f * (BeltSpeed / 120f) * 0.30f;
            }

            if (item.Power == "coin")
            {
                // The coin pulses so an unbanked wager keeps asking to be taken.
                var beat = 0.85f + 0.15f * Mathf.Sin(age * 7f);
                sprite.Modulate = new Color(beat, beat, 1f, 1f);
            }
            else if (item.TypeId == "golden")
            {
                var shimmer = 0.75f + 0.25f * Mathf.Sin(age * 9f);
                sprite.Modulate = new Color(1f + shimmer * 0.5f, 1f + shimmer * 0.35f, 1f, 1f);
                sprite.Rotation = Mathf.Sin(age * 3f) * 0.12f;
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_ages.Count == 0) return;

        var dt = (float)delta;
        foreach (var id in new List<int>(_ages.Keys)) _ages[id] += dt;
    }

    /// <summary>
    /// Writes the wager on the coin itself. This is the one number that belongs on the
    /// belt rather than in the HUD: the decision is made by looking at the coin, so the
    /// stake has to be there when the eye is.
    /// </summary>
    public void SetCoinValue(int itemId, int value)
    {
        if (!_sprites.TryGetValue(itemId, out var sprite)) return;

        if (!_coinLabels.TryGetValue(itemId, out var label))
        {
            label = new Label
            {
                LabelSettings = Ui.Text(Ui.Small, Ui.Ink, outline: false),
                HorizontalAlignment = HorizontalAlignment.Center,
                ZIndex = 1,
            };
            sprite.AddChild(label);
            _coinLabels[itemId] = label;
        }

        label.Text = value.ToString();

        // Measured, not guessed: an off-centre number on a 16px coin is illegible.
        var size = Ui.Measure(label.Text, Ui.Small);
        label.Size = size;
        label.Position = -size / 2f;
    }

    public void Remove(int id)
    {
        _ages.Remove(id);
        _coinLabels.Remove(id);
        if (!_sprites.Remove(id, out var sprite)) return;
        sprite.QueueFree();
    }

    /// <summary>Prunes sprites whose items left the belt without an explicit Remove.</summary>
    public void PruneMissing(IReadOnlyList<FoodItem> items)
    {
        var live = new HashSet<int>();
        foreach (var item in items) live.Add(item.Id);

        var stale = new List<int>();
        foreach (var id in _sprites.Keys)
        {
            if (!live.Contains(id)) stale.Add(id);
        }

        foreach (var id in stale) Remove(id);
    }

    /// <summary>Drops every sprite. Called when a run ends so the next run starts clean.</summary>
    public void Clear()
    {
        foreach (var sprite in _sprites.Values) sprite.QueueFree();
        _sprites.Clear();
        _ages.Clear();
        _coinLabels.Clear();
    }

    private Texture2D TextureFor(string typeId)
    {
        if (_textures.TryGetValue(typeId, out var cached)) return cached;

        var path = $"res://Art/ExportedSprites/{typeId}.png";
        var texture = ResourceLoader.Load<Texture2D>(path);

        if (texture is null)
        {
            GD.PushWarning($"Missing food texture {path}; using a placeholder.");
            var image = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
            image.Fill(Colors.Magenta);
            texture = ImageTexture.CreateFromImage(image);
        }

        _textures[typeId] = texture;
        return texture;
    }
}
