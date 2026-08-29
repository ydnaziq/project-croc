namespace CrocGame.Core;

/// <summary>
/// One item riding the belt. HalfWidth is the timing window: a narrow item at
/// high belt speed is a hard press, and that relationship is visible on screen.
/// </summary>
public sealed class FoodItem
{
    public FoodItem(int id, string typeId, float x, float halfWidth,
                    bool isEdible, int score, IMovement movement, string power = "")
    {
        Id = id;
        TypeId = typeId;
        X = x;
        HalfWidth = halfWidth;
        IsEdible = isEdible;
        Score = score;
        Movement = movement;
        Power = power;
    }

    public int Id { get; }
    public string TypeId { get; }
    public float X { get; set; }
    public float HalfWidth { get; }
    public bool IsEdible { get; }
    public int Score { get; }
    public IMovement Movement { get; }

    /// <summary>The buff this item grants when bitten, or "" for ordinary food.</summary>
    public string Power { get; }

    /// <summary>Seconds this item has been on the belt. Drives movement behaviors.</summary>
    public float Age { get; set; }
}
