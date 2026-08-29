using System;
using System.Collections.Generic;
using Godot;

namespace CrocGame;

/// <summary>
/// The pre-match exchange: croc on the left, rival on the right, whoever is speaking
/// lit and scaled up while the other dims back.
///
/// The staging does the work of saying who is talking, so the box never has to. That
/// matters at 180px wide, where a name plate plus a line plus a portrait would leave
/// no room for the line.
/// </summary>
public partial class DialogueScene : Node2D
{
    public readonly record struct Line(bool FromPlayer, string Speaker, string Text);

    // The box occupies the bottom of the screen. One number, easy to retune.
    private const float BoxMinHeight = 56f;
    private const float BoxBottomY = GameRoot.ViewportHeight - Margin;
    private const float Margin = 6f;
    private const float Padding = 8f;

    private const float ActorY = 116f;
    private const float PlayerX = 46f;
    private const float RivalX = 134f;

    private const float SpeakingScale = 2.5f;
    private const float ListeningScale = 1.8f;

    private static readonly Color Lit = Colors.White;
    private static readonly Color Dimmed = new("6a6a86");

    public Action? Finished;

    private Sprite2D _player = null!;
    private Sprite2D _rival = null!;
    private Label _name = null!;
    private Label _body = null!;

    private readonly List<Line> _lines = new();
    private int _index;
    private float _age;
    private float _typed;
    private float _boxTop = BoxBottomY - BoxMinHeight;
    private float _nameRailY;

    public override void _Ready()
    {
        ZIndex = 32;
        Visible = false;

        _player = new Sprite2D { Position = new Vector2(PlayerX, ActorY) };
        AddChild(_player);

        _rival = new Sprite2D { Position = new Vector2(RivalX, ActorY), FlipH = true };
        AddChild(_rival);

        var width = GameRoot.ViewportWidth - Margin * 2f - Padding * 2f;

        _name = new Label
        {
            Size = new Vector2(width, Ui.Body + 4),
            LabelSettings = Ui.Text(Ui.Body, Ui.Gold),
        };
        AddChild(_name);

        _body = Ui.WrappedLabel("", Ui.Small, Ui.Paper, width);
        AddChild(_body);
    }

    public void Play(string playerSprite, string rivalSprite, IReadOnlyList<Line> lines)
    {
        _player.Texture = ResourceLoader.Load<Texture2D>($"res://Art/ExportedSprites/{playerSprite}.png");
        _rival.Texture = ResourceLoader.Load<Texture2D>($"res://Art/ExportedSprites/{rivalSprite}.png");

        _lines.Clear();
        _lines.AddRange(lines);
        _index = 0;
        _age = 0f;
        Visible = true;

        ShowLine();
    }

    /// <summary>Advances to the next line, or finishes. Returns true if it consumed the press.</summary>
    public bool Advance()
    {
        if (!Visible) return false;

        // First press completes the typing rather than skipping the line.
        if (_typed < _lines[_index].Text.Length)
        {
            _typed = _lines[_index].Text.Length;
            _body.Text = _lines[_index].Text;
            return true;
        }

        _index++;

        if (_index >= _lines.Count)
        {
            Visible = false;
            Finished?.Invoke();
            return true;
        }

        ShowLine();
        return true;
    }

    private void ShowLine()
    {
        var line = _lines[_index];

        _name.Text = line.Speaker;
        _name.LabelSettings.FontColor = line.FromPlayer ? Ui.Green : Ui.Rival;
        _body.Text = "";
        _typed = 0f;

        _player.Modulate = line.FromPlayer ? Lit : Dimmed;
        _rival.Modulate = line.FromPlayer ? Dimmed : Lit;
        _player.Scale = Vector2.One * (line.FromPlayer ? SpeakingScale : ListeningScale);
        _rival.Scale = Vector2.One * (line.FromPlayer ? ListeningScale : SpeakingScale);

        // The body wraps to however many lines it needs and the box grows upward to
        // hold them - measured, never estimated. The full text is set first so the
        // label reports its real line count, then cleared for the typing-on.
        _body.Text = line.Text;

        var content = Ui.ColumnHeight(new List<Label> { _name, _body }, gap: 8f);
        _boxTop = BoxBottomY - Mathf.Max(BoxMinHeight, content + Padding * 2f + 8f);

        Ui.LayoutColumn(
            new List<Label> { _name, _body },
            new Rect2(Margin + Padding, _boxTop + Padding,
                      GameRoot.ViewportWidth - (Margin + Padding) * 2f,
                      BoxBottomY - _boxTop - Padding * 2f),
            gap: 8f);

        _nameRailY = _name.Position.Y + Ui.WrappedHeight(_name) + 3f;

        _body.Text = "";

        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        var dt = (float)delta;
        _age += dt;

        var line = _lines[_index];

        // Typing on: the line arrives at reading pace instead of appearing whole.
        if (_typed < line.Text.Length)
        {
            _typed = Mathf.Min(line.Text.Length, _typed + dt * 42f);
            _body.Text = line.Text[..(int)_typed];
        }

        // The speaker breathes slightly so the pair never looks like a still image.
        var bob = Mathf.Sin(_age * 4f) * 1.5f;
        _player.Position = new Vector2(PlayerX, ActorY + (line.FromPlayer ? bob : 0f));
        _rival.Position = new Vector2(RivalX, ActorY + (line.FromPlayer ? 0f : bob));

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible) return;

        var w = GameRoot.ViewportWidth;
        var h = GameRoot.ViewportHeight;

        // The arena stays visible behind, knocked back so the pair reads clearly.
        DrawRect(new Rect2(0, 0, w, h), new Color(0f, 0f, 0f, 0.20f));

        // A floor line under the actors so they are standing somewhere.
        DrawRect(new Rect2(0, ActorY + 34f, w, 1), new Color("000000", 0.5f));

        Ui.RaisedPanel(this, new Rect2(Margin, _boxTop, w - Margin * 2f, BoxBottomY - _boxTop),
                       new Color("101020", 0.97f), new Color("4a4a70"));

        // Rail under the name, separating speaker from speech.
        DrawRect(new Rect2(Margin + Padding, _nameRailY, w - (Margin + Padding) * 2f, 1),
                 new Color("4a4a70"));

        // Blinking prompt, once the line has finished arriving.
        if (_typed >= _lines[_index].Text.Length && Mathf.Sin(_age * 6f) > 0f)
        {
            DrawRect(new Rect2(w - Margin - 14f, h - Margin - 12f, 5, 5), Ui.Gold);
        }
    }
}
