using System.Linq;
using Godot;

namespace CrocGame;

/// <summary>
/// The arena, built entirely from tiles in Art/ExportedSprites/tileset.png.
///
/// Everything on screen that is not a character comes from the same 16x16 tileset the
/// cast was drawn against. That is the whole point: a backdrop of flat rectangles in
/// arbitrary colours is what makes pixel art look like a prototype.
/// </summary>
public partial class Backdrop : Node2D
{
    private const int Tile = 16;

    // Tile ids from Art/README.md, laid out row-major in a 4-wide grid.
    private const int TileStone = 3;
    private const int TileDirt = 5;
    private const int TileBrick = 7;
    private const int TileBackground = 17;
    private const int TileGround = 18;

    private Texture2D _tileset = null!;
    private Texture2D[] _crowd = System.Array.Empty<Texture2D>();

    public override void _Ready()
    {
        _tileset = ResourceLoader.Load<Texture2D>("res://Art/ExportedSprites/tileset.png");
        if (_tileset is null) GD.PushError("Missing tileset.png; the arena will be blank.");

        _crowd = new[] { "penguin", "cat", "robot", "slime" }
            .Select(id => ResourceLoader.Load<Texture2D>($"res://Art/ExportedSprites/{id}.png"))
            .Where(t => t is not null)
            .ToArray();

        ZIndex = -10;
    }

    public override void _Draw()
    {
        if (_tileset is null) return;

        var w = (int)GameRoot.ViewportWidth;
        var h = (int)GameRoot.ViewportHeight;

        // Back wall behind everything.
        FillRegion(0, 0, w, h, TileBackground);

        // The rival's stage sits high; a brick shelf separates the two halves and
        // sits below the rival's score rather than through it.
        FillRegion(0, 120, w, 32, TileBrick);
        FillRegion(0, 120, w, Tile, TileStone);

        // The croc's own floor: brick wall, then a dirt strip under the conveyor.
        FillRegion(0, (int)GameRoot.BeltY + 24, w, 48, TileBrick);
        FillRegion(0, h - 32, w, Tile, TileGround);
        FillRegion(0, h - 16, w, Tile, TileDirt);

        DrawCrowd();
    }

    /// <summary>Spectators: the rest of the cast, dimmed and small, watching the bout.</summary>
    private void DrawCrowd()
    {
        if (_crowd.Length == 0) return;

        var dim = new Color(0.45f, 0.45f, 0.6f, 1f);

        for (var i = 0; i < 6; i++)
        {
            var texture = _crowd[i % _crowd.Length];
            var x = 6 + i * 30;
            var y = 158 + (i % 2 == 0 ? 0 : 3);  // uneven heads read as a crowd, not a row
            DrawTextureRect(texture, new Rect2(x, y, 20, 20), false, dim);
        }
    }

    private void FillRegion(int x, int y, int width, int height, int tileId)
    {
        var src = new Rect2(tileId % 4 * Tile, tileId / 4 * Tile, Tile, Tile);

        for (var ty = y; ty < y + height; ty += Tile)
        {
            for (var tx = x; tx < x + width; tx += Tile)
            {
                DrawTextureRectRegion(_tileset, new Rect2(tx, ty, Tile, Tile), src);
            }
        }
    }
}
