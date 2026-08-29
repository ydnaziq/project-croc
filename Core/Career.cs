using System.Collections.Generic;
using System.Linq;

namespace CrocGame.Core;

/// <summary>A cosmetic the croc can buy with prize money. Purely a tint on the sprite.</summary>
public sealed record ShopItem(string Id, string Name, int Cost, string Tint);

public enum PurchaseResult
{
    Bought,
    AlreadyOwned,
    TooExpensive,
    NoSuchItem,
}

/// <summary>
/// The contest ladder and the shop between bouts.
///
/// The rhythm this exists to create: a hard match, then a moment of reward where the
/// money means something, then a harder match. Without the shop the prizes are just a
/// number going up, and the valleys between matches are dead air.
/// </summary>
public static class Career
{
    public static readonly IReadOnlyList<OpponentDef> Ladder = new[]
    {
        new OpponentDef("penguin", "PIP", "penguin",
            SecondsPerBite: 1.70f, BiteJitter: 0.25f, PointsPerBite: 34,
            PrizeMoney: 25, Taunt: "you look hungry, pal",
            LineLosing: "hey! slow down!", LineWinning: "too easy", LinePanic: "what IS this"),

        new OpponentDef("cat", "MOCHI", "cat",
            SecondsPerBite: 1.45f, BiteJitter: 0.22f, PointsPerBite: 40,
            PrizeMoney: 50, Taunt: "i eat, i nap, i win",
            LineLosing: "unacceptable", LineWinning: "yawn", LinePanic: "hiss!!"),

        new OpponentDef("robot", "UNIT-7", "robot",
            SecondsPerBite: 1.25f, BiteJitter: 0.15f, PointsPerBite: 46,
            PrizeMoney: 100, Taunt: "consumption rate: optimal",
            LineLosing: "recalculating", LineWinning: "as projected", LinePanic: "ERROR ERROR"),

        new OpponentDef("slime", "BLORP", "slime",
            SecondsPerBite: 1.05f, BiteJitter: 0.20f, PointsPerBite: 52,
            PrizeMoney: 200, Taunt: "i am mostly stomach",
            LineLosing: "you have a hole in you", LineWinning: "glorp", LinePanic: "IMPOSSIBLE"),
    };

    // Bouts are short on purpose. A match is a single burst of concentration; past
    // about half a minute the timing stops being exciting and starts being work.
    private static readonly float[] Durations = { 20f, 22f, 24f, 26f };
    private static readonly int[] DifficultyOffsets = { 0, 8, 18, 28 };

    public static readonly IReadOnlyList<ShopItem> Shop = new[]
    {
        new ShopItem("skin_chef", "CHEF WHITE", 30, "f8f8f8"),
        new ShopItem("skin_gold", "GOLD TOOTH", 80, "f8d878"),
        new ShopItem("skin_shadow", "MIDNIGHT", 150, "7878b8"),
        new ShopItem("skin_neon", "NEON", 250, "58f8d8"),
    };

    /// <summary>How many bouts the croc has won, which is also the next rung's index.</summary>
    public static int Progress(SaveData data) => data.DefeatedIds.Count;

    public static bool IsChampion(SaveData data) => Progress(data) >= Ladder.Count;

    /// <summary>The next bout, or null once the croc is champion.</summary>
    public static MatchDef? NextMatch(SaveData data)
    {
        var index = Progress(data);
        if (index >= Ladder.Count) return null;

        return new MatchDef(Ladder[index], Durations[index], DifficultyOffsets[index]);
    }

    /// <summary>Records a win and pays out. A repeat win over the same rival pays nothing.</summary>
    public static void RecordWin(SaveData data, MatchEnded ended)
    {
        var index = Progress(data);
        if (index >= Ladder.Count) return;

        data.DefeatedIds.Add(Ladder[index].Id);
        data.Money += ended.Prize;
        data.LifetimeEaten += ended.Eaten;

        if (ended.PlayerScore > data.BestScore) data.BestScore = ended.PlayerScore;
    }

    /// <summary>Records a loss. Progress is kept; only the prize is missed.</summary>
    public static void RecordLoss(SaveData data, MatchEnded ended)
    {
        data.LifetimeEaten += ended.Eaten;
        if (ended.PlayerScore > data.BestScore) data.BestScore = ended.PlayerScore;
    }

    public static PurchaseResult Buy(SaveData data, string itemId)
    {
        var item = Shop.FirstOrDefault(i => i.Id == itemId);

        if (item is null) return PurchaseResult.NoSuchItem;
        if (data.OwnedSkinIds.Contains(item.Id)) return PurchaseResult.AlreadyOwned;
        if (data.Money < item.Cost) return PurchaseResult.TooExpensive;

        data.Money -= item.Cost;
        data.OwnedSkinIds.Add(item.Id);
        data.EquippedSkinId = item.Id;
        return PurchaseResult.Bought;
    }

    public static bool Equip(SaveData data, string itemId)
    {
        if (itemId != "" && !data.OwnedSkinIds.Contains(itemId)) return false;

        data.EquippedSkinId = itemId;
        return true;
    }

    public static ShopItem? EquippedSkin(SaveData data) =>
        Shop.FirstOrDefault(i => i.Id == data.EquippedSkinId);
}
