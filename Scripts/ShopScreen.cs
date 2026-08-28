using System;
using System.Collections.Generic;
using CrocGame.Core;
using Godot;

namespace CrocGame;

/// <summary>
/// The valley between matches: spend the prize money, then walk back out.
///
/// Items are cards carrying a swatch of the actual colour the croc will wear, because
/// a shop that sells "MIDNIGHT" without showing midnight is asking the player to buy
/// a word. Rows are tappable directly - the game is built for a phone, and a hidden
/// cursor is one more thing to explain - with keyboard kept for desktop play.
/// </summary>
public partial class ShopScreen : Node2D
{
    private const float CardHeight = 30f;
    private const float CardGap = 4f;
    private const float FirstCardY = 104f;
    private const float ContinueY = 254f;
    private const float Margin = 10f;

    public Action<string>? BuyRequested;
    public Action? ContinueRequested;

    private SaveData _data = new();
    private int _cursor;
    private float _pulse;

    private Label _title = null!;
    private Label _money = null!;
    private Label _hint = null!;
    private Label _continue = null!;
    private readonly List<Label> _names = new();
    private readonly List<Label> _prices = new();

    public override void _Ready()
    {
        ZIndex = 30;
        Visible = false;

        _title = Make(new Vector2(0, 52), Ui.Title, Ui.Paper, HorizontalAlignment.Center);
        _title.Text = "SHOP";

        _money = Make(new Vector2(0, 82), Ui.Body, Ui.Gold, HorizontalAlignment.Center);

        for (var i = 0; i < Career.Shop.Count; i++)
        {
            var y = FirstCardY + i * (CardHeight + CardGap);
            _names.Add(Make(new Vector2(Margin + 26, y + 5), Ui.Small, Ui.Paper, HorizontalAlignment.Left));
            _prices.Add(Make(new Vector2(-Margin - 4, y + 15), Ui.Small, Ui.Gold, HorizontalAlignment.Right));
        }

        _continue = Make(new Vector2(0, ContinueY + 6), Ui.Body, Ui.Green, HorizontalAlignment.Center);
        _hint = Make(new Vector2(0, ContinueY + 30), Ui.Small, Ui.Dim, HorizontalAlignment.Center);
        _hint.Text = "tap to buy or wear";
    }

    private Label Make(Vector2 position, int size, Color color, HorizontalAlignment align)
    {
        var label = new Label
        {
            Position = position,
            Size = new Vector2(GameRoot.ViewportWidth - Margin * 2f, size + 6),
            HorizontalAlignment = align,
            LabelSettings = Ui.Text(size, color),
        };
        AddChild(label);
        return label;
    }

    public void Open(SaveData data)
    {
        _data = data;
        Visible = true;
        Refresh();
    }

    public void Close() => Visible = false;

    public void Refresh()
    {
        _money.Text = $"${_data.Money}";

        for (var i = 0; i < Career.Shop.Count; i++)
        {
            var item = Career.Shop[i];
            var owned = _data.OwnedSkinIds.Contains(item.Id);
            var worn = _data.EquippedSkinId == item.Id;
            var affordable = _data.Money >= item.Cost;

            _names[i].Text = item.Name;
            _names[i].LabelSettings.FontColor = owned ? Ui.Paper : affordable ? Ui.Paper : Ui.Dim;

            _prices[i].Text = worn ? "WORN" : owned ? "WEAR" : $"${item.Cost}";
            _prices[i].LabelSettings.FontColor =
                worn ? Ui.Green : owned ? Ui.Paper : affordable ? Ui.Gold : new Color("806060");
        }

        var next = Career.NextMatch(_data);
        _continue.Text = next is null ? "CHAMPION" : $"FIGHT {next.Opponent.Name}";
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        _pulse += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible) return;

        var w = GameRoot.ViewportWidth;

        DrawRect(new Rect2(0, 0, w, GameRoot.ViewportHeight), new Color("0c0c16", 0.94f));
        Ui.Panel(this, new Rect2(6, 44, w - 12, 56), new Color("18182c"));

        for (var i = 0; i < Career.Shop.Count; i++)
        {
            var item = Career.Shop[i];
            var owned = _data.OwnedSkinIds.Contains(item.Id);
            var worn = _data.EquippedSkinId == item.Id;
            var affordable = _data.Money >= item.Cost;
            var y = FirstCardY + i * (CardHeight + CardGap);
            var card = new Rect2(Margin - 4, y, w - (Margin - 4) * 2f, CardHeight);

            var fill = i == _cursor ? Ui.PanelFillLit : Ui.PanelFill;
            Ui.RaisedPanel(this, card, fill, new Color("4a4a70"));

            // The swatch: the colour the croc actually turns.
            var swatch = new Rect2(card.Position.X + 6, y + 8, 14, 14);
            Ui.Panel(this, swatch, new Color(item.Tint));

            if (worn)
            {
                // A worn item gets a full accent rail rather than a checkmark glyph,
                // which would need a font character that may not exist.
                DrawRect(new Rect2(card.Position.X + 1, card.End.Y - 2, card.Size.X - 2, 1), Ui.Green);
            }
            else if (!owned && !affordable)
            {
                DrawRect(card, new Color("000000", 0.35f));
            }
        }

        // Continue button, pulsing gently so it reads as the way out.
        var glow = 0.5f + 0.5f * Mathf.Sin(_pulse * 4f);
        var button = new Rect2(Margin - 4, ContinueY, w - (Margin - 4) * 2f, 28);
        Ui.RaisedPanel(this, button, new Color("1e3a1e"), new Color("58d854", 0.4f + 0.6f * glow));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;

        if (@event is InputEventMouseButton { Pressed: true } click) { Activate(click.Position); return; }
        if (@event is InputEventScreenTouch { Pressed: true } touch) { Activate(touch.Position); return; }

        if (@event.IsActionPressed("ui_down")) { _cursor = (_cursor + 1) % Career.Shop.Count; QueueRedraw(); }
        else if (@event.IsActionPressed("ui_up")) { _cursor = (_cursor + Career.Shop.Count - 1) % Career.Shop.Count; QueueRedraw(); }
        else if (@event.IsActionPressed("ui_accept")) BuyRequested?.Invoke(Career.Shop[_cursor].Id);
        else if (@event.IsActionPressed("chomp")) ContinueRequested?.Invoke();
    }

    private void Activate(Vector2 position)
    {
        if (position.Y >= ContinueY && position.Y <= ContinueY + 28)
        {
            ContinueRequested?.Invoke();
            return;
        }

        for (var i = 0; i < Career.Shop.Count; i++)
        {
            var top = FirstCardY + i * (CardHeight + CardGap);
            if (position.Y < top || position.Y > top + CardHeight) continue;

            _cursor = i;
            BuyRequested?.Invoke(Career.Shop[i].Id);
            return;
        }
    }
}
