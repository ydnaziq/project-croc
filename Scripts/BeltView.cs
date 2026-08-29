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

            sprite.Position = new Vector2(item.X, GameRoot.BeltY);

            // Pop in over the first fraction of a second, and give the golden bite a
            // constant shimmer so the rare item announces itself on the belt.
            var age = _ages.TryGetValue(item.Id, out var a) ? a : 1f;
            var pop = Mathf.Min(1f, age * 8f);
            var overshoot = 1f + 0.35f * Mathf.Sin(pop * Mathf.Pi);
            sprite.Scale = Vector2.One * pop * overshoot;

            if (item.TypeId == "golden")
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

    public void Remove(int id)
    {
        _ages.Remove(id);
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
