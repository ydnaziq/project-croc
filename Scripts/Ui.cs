using Godot;

namespace CrocGame;

/// <summary>
/// Shared look-and-feel: the pixel font, and drawing helpers that keep every line in
/// the interface exactly one pixel thick.
///
/// Uniform line weight is the rule that makes hand-drawn pixel art and generated UI
/// look like one thing. Anything that changes colour gets a 1px black separator - the
/// same treatment the sprites already use - so nothing on screen has a soft or
/// double-thick edge.
/// </summary>
public static class Ui
{
    public const string FontPath = "res://Art/Fonts/Silkscreen-Regular.ttf";
    public const string BoldFontPath = "res://Art/Fonts/Silkscreen-Bold.ttf";

    // Silkscreen is drawn on an 8px grid, so only multiples of 8 render as crisp
    // pixels. Any other size resamples and goes fuzzy.
    public const int Small = 8;
    public const int Body = 16;
    public const int Title = 24;

    public static readonly Color Ink = new("000000");
    public static readonly Color Paper = new("f8f8f8");
    public static readonly Color Gold = new("f8d878");
    public static readonly Color Green = new("58d854");
    public static readonly Color Red = new("f83800");
    public static readonly Color Rival = new("f87858");
    public static readonly Color Dim = new("9090a8");
    public static readonly Color PanelFill = new("1c1c2e");
    public static readonly Color PanelFillLit = new("30304e");

    private static FontFile? _regular;
    private static FontFile? _bold;

    public static FontFile? Regular => _regular ??= LoadFont(FontPath);
    public static FontFile? Bold => _bold ??= LoadFont(BoldFontPath);

    private static FontFile? LoadFont(string path)
    {
        var font = ResourceLoader.Load<FontFile>(path);

        if (font is null)
        {
            GD.PushWarning($"Missing {path}; falling back to the default font.");
            return null;
        }

        // A pixel font must not be smoothed, or every glyph edge turns grey.
        font.Antialiasing = TextServer.FontAntialiasing.None;
        font.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
        font.Hinting = TextServer.Hinting.None;
        font.MultichannelSignedDistanceField = false;

        return font;
    }

    /// <summary>Label styling. Outlines are on by default so text keeps the same 1px
    /// edge as everything else and stays readable over the arena.</summary>
    public static LabelSettings Text(int size, Color color, bool outline = true, bool bold = false)
    {
        var settings = new LabelSettings
        {
            Font = bold ? Bold : Regular,
            FontSize = size,
            FontColor = color,
        };

        if (outline)
        {
            settings.OutlineSize = 1;
            settings.OutlineColor = Ink;
        }

        return settings;
    }

    /// <summary>A filled rectangle with a 1px black border.</summary>
    public static void Panel(CanvasItem canvas, Rect2 rect, Color fill)
    {
        canvas.DrawRect(rect, Ink);
        canvas.DrawRect(new Rect2(rect.Position + Vector2.One, rect.Size - Vector2.One * 2f), fill);
    }

    /// <summary>A panel with a highlight along its top edge, for anything raised.</summary>
    public static void RaisedPanel(CanvasItem canvas, Rect2 rect, Color fill, Color highlight)
    {
        Panel(canvas, rect, fill);
        canvas.DrawRect(new Rect2(rect.Position.X + 1, rect.Position.Y + 1, rect.Size.X - 2, 1), highlight);
    }

    /// <summary>
    /// The real width and height of a string, asked of the font rather than guessed
    /// from the character count. Every overflowing box in this project came from
    /// estimating this instead of measuring it.
    /// </summary>
    public static Vector2 Measure(string text, int size)
    {
        var font = Regular;

        if (font is null)
        {
            // Only reached if the font failed to load; keeps callers from dividing by
            // zero rather than pretending to be accurate.
            return new Vector2(text.Length * size * 0.6f, size + 2);
        }

        var widest = 0f;
        var lines = text.Split('\n');

        foreach (var line in lines)
        {
            widest = Mathf.Max(widest, font.GetStringSize(line, HorizontalAlignment.Left, -1, size).X);
        }

        return new Vector2(widest, lines.Length * (size + 2));
    }

    /// <summary>
    /// A label that wraps inside a given width and reports how tall it actually became,
    /// so a panel can be drawn around it instead of under it.
    /// </summary>
    public static Label WrappedLabel(string text, int size, Color color, float width,
                                     HorizontalAlignment align = HorizontalAlignment.Left)
    {
        return new Label
        {
            Text = text,
            Size = new Vector2(width, size + 4),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = align,
            LabelSettings = Text(size, color),
            ClipText = false,
        };
    }

    /// <summary>Height a wrapped label occupies once it is inside the tree.</summary>
    public static float WrappedHeight(Label label) =>
        Mathf.Max(label.GetLineCount(), 1) * label.GetLineHeight();

    /// <summary>A horizontal meter with a 1px frame, filled from the left.</summary>
    public static void Meter(CanvasItem canvas, Rect2 rect, float fraction, Color fill, Color empty)
    {
        Panel(canvas, rect, empty);

        var inner = rect.Size.X - 2f;
        var filled = Mathf.Clamp(fraction, 0f, 1f) * inner;

        if (filled >= 1f)
        {
            canvas.DrawRect(new Rect2(rect.Position.X + 1, rect.Position.Y + 1, filled, rect.Size.Y - 2), fill);
        }
    }
}
