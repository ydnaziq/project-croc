using System.Collections.Generic;
using Godot;

namespace CrocGame;

/// <summary>
/// A burst of square food chunks.
///
/// Drawn as hard-edged squares on the pixel grid rather than Godot's default soft
/// round particles, which read as a different medium sitting on top of pixel art.
/// Colours are sampled from the NES palette the food was drawn in.
/// </summary>
public partial class Crumbs : Node2D
{
    private struct Chunk
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Size;
        public Color Color;
    }

    private static readonly Color[] Palette =
    {
        new("f8d878"), new("f8b878"), new("c87828"), new("f87858"), new("f8f8f8"),
    };

    private const float Gravity = 420f;

    private readonly List<Chunk> _chunks = new();
    private readonly RandomNumberGenerator _rng = new();

    /// <summary>Throws a burst upward and outward from a point.</summary>
    public void Burst(Vector2 origin, int count = 8, float force = 1f)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = _rng.RandfRange(-2.7f, -0.45f);      // upward fan
            var speed = _rng.RandfRange(50f, 130f) * force;

            _chunks.Add(new Chunk
            {
                Position = origin + new Vector2(_rng.RandfRange(-6f, 6f), _rng.RandfRange(-4f, 4f)),
                Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                Life = 0f,
                MaxLife = _rng.RandfRange(0.28f, 0.5f),
                Size = _rng.RandiRange(1, 3),
                Color = Palette[_rng.RandiRange(0, Palette.Length - 1)],
            });
        }

        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_chunks.Count == 0) return;

        var dt = (float)delta;

        for (var i = _chunks.Count - 1; i >= 0; i--)
        {
            var chunk = _chunks[i];
            chunk.Life += dt;

            if (chunk.Life >= chunk.MaxLife)
            {
                _chunks.RemoveAt(i);
                continue;
            }

            chunk.Velocity += new Vector2(0f, Gravity * dt);
            chunk.Position += chunk.Velocity * dt;
            _chunks[i] = chunk;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var chunk in _chunks)
        {
            // Snapping to whole pixels keeps the chunks on the same grid as the art.
            var x = Mathf.Floor(chunk.Position.X);
            var y = Mathf.Floor(chunk.Position.Y);
            var fade = 1f - chunk.Life / chunk.MaxLife;

            DrawRect(new Rect2(x, y, chunk.Size, chunk.Size),
                     chunk.Color with { A = fade < 0.35f ? fade / 0.35f : 1f });
        }
    }
}
