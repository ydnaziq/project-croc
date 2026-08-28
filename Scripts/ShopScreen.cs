using System;
using System.Collections.Generic;
using CrocGame.Core;
using Godot;

namespace CrocGame;

/// <summary>
/// The valley between matches: spend the prize money, then walk back out.
///
/// Rows are tappable directly rather than driven by a cursor, because the game is
/// built for a phone and a hidden selection state is one more thing to explain.
/// Keyboard still works for desktop play.
/// </summary>
public partial class ShopScreen : Node2D
{
    private const float RowHeight = 26f;
    private const float FirstRowY = 110f;
    private const float ContinueY = 250f;

    public Action<string>? BuyRequested;
    public Action? ContinueRequested;

    private SaveData _data = new();
    private int _cursor;

    private Label _title = null!;
    private Label _money = null!;
    private Label _hint = null!;
    private readonly List<Label> _rows = new();
    private Label _continue = null!;

    public override void _Ready()
    {
        ZIndex = 30;
        Visible = false;

        _title = Make(new Vector2(0, 60), 16, "f8f8f8");
        _title.Text = "SHOP";

        _money = Make(new Vector2(0, 82), 11, "f8d878");

        for (var i = 0; i < Career.Shop.Count; i++) _rows.Add(Make(new Vector2(0, FirstRowY + i * RowHeight), 10, "f8f8f8"));

        _continue = Make(new Vector2(0, ContinueY), 12, "58d854");
        _hint = Make(new Vector2(0, ContinueY + 22), 8, "9090a8");
        _hint.Text = "tap an item to buy or wear it";
    }

    private Label Make(Vector2 position, int size, string color)
    {
        var label = new Label
        {
            Position = position,
            Size = new Vector2(GameRoot.ViewportWidth, size + 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = size, FontColor = new Color(color) },
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

            _rows[i].Text = worn ? $"{item.Name}  - WORN"
                          : owned ? $"{item.Name}  - OWNED"
                          : $"{item.Name}  ${item.Cost}";

            _rows[i].LabelSettings.FontColor =
                worn ? new Color("58d854")
                : owned ? new Color("f8f8f8")
                : _data.Money >= item.Cost ? new Color("f8d878")
                : new Color("707080");
        }

        var next = Career.NextMatch(_data);
        _continue.Text = next is null ? "YOU ARE THE CHAMPION" : $"FIGHT {next.Opponent.Name}";
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible) return;

        DrawRect(new Rect2(0, 0, GameRoot.ViewportWidth, GameRoot.ViewportHeight),
                 new Color("101018", 0.92f));

        for (var i = 0; i < Career.Shop.Count; i++)
        {
            var highlight = i == _cursor;
            DrawRect(new Rect2(8, FirstRowY + i * RowHeight - 4, GameRoot.ViewportWidth - 16, 20),
                     highlight ? new Color("30304a") : new Color("1c1c2a"));
        }

        DrawRect(new Rect2(8, ContinueY - 4, GameRoot.ViewportWidth - 16, 20), new Color("1e3a1e"));
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
        if (position.Y >= ContinueY - 6 && position.Y <= ContinueY + 20)
        {
            ContinueRequested?.Invoke();
            return;
        }

        for (var i = 0; i < Career.Shop.Count; i++)
        {
            var top = FirstRowY + i * RowHeight - 6;
            if (position.Y < top || position.Y > top + 24) continue;

            _cursor = i;
            BuyRequested?.Invoke(Career.Shop[i].Id);
            return;
        }
    }
}
