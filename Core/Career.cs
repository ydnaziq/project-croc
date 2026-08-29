using System.Collections.Generic;
using System.Linq;

namespace CrocGame.Core;

/// <summary>
/// A cosmetic the croc can buy and wear. A drawn object, not a colour multiply:
/// tinting a flat five-colour sprite produces colours that are in no palette, mostly
/// just makes the croc muddy, and sells the player a word instead of a thing.
/// </summary>
public sealed record ShopItem(string Id, string Name, int Cost, string SpriteId);

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
            LineLosing: "hey! slow down!", LineWinning: "too easy", LinePanic: "what IS this",
            Interlude1Ahead: "one to me. try harder",
            Interlude1Behind: "ok. ok. that was a warm-up",
            Interlude2Ahead: "one more and you go home",
            Interlude2Behind: "how are you still eating"),

        new OpponentDef("cat", "MOCHI", "cat",
            SecondsPerBite: 1.45f, BiteJitter: 0.22f, PointsPerBite: 40,
            PrizeMoney: 50, Taunt: "i eat, i nap, i win",
            LineLosing: "unacceptable", LineWinning: "yawn", LinePanic: "hiss!!",
            Interlude1Ahead: "i was not even awake for that",
            Interlude1Behind: "you got lucky. once",
            Interlude2Ahead: "the last round is my favourite",
            Interlude2Behind: "fine. no more napping"),

        new OpponentDef("robot", "UNIT-7", "robot",
            SecondsPerBite: 1.25f, BiteJitter: 0.15f, PointsPerBite: 46,
            PrizeMoney: 100, Taunt: "consumption rate: optimal",
            LineLosing: "recalculating", LineWinning: "as projected", LinePanic: "ERROR ERROR",
            Interlude1Ahead: "phase one: within tolerance",
            Interlude1Behind: "anomaly logged. adjusting",
            Interlude2Ahead: "final phase. outcome certain",
            Interlude2Behind: "certainty: dropping"),

        new OpponentDef("slime", "BLORP", "slime",
            SecondsPerBite: 1.05f, BiteJitter: 0.20f, PointsPerBite: 52,
            PrizeMoney: 200, Taunt: "i am mostly stomach",
            LineLosing: "you have a hole in you", LineWinning: "glorp", LinePanic: "IMPOSSIBLE",
            Interlude1Ahead: "i have three more stomachs",
            Interlude1Behind: "glorp?? glorp.",
            Interlude2Ahead: "the last one is always mine",
            Interlude2Behind: "i am running out of stomachs"),
    };

    /// <summary>
    /// The three acts every bout runs through. PLAIN teaches the rival's pace on a
    /// clean belt, HAZARD introduces everything that can go wrong, and FEAST is worth
    /// double so a bout lost in the first two acts is still winnable in the third.
    /// </summary>
    public static readonly IReadOnlyList<PhaseDef> Phases = new[]
    {
        new PhaseDef("PLAIN",  8f,  DifficultyOffset: 0,  ScoreMultiplier: 1,
                     HazardScale: 0f,   PowerUpsEnabled: false, CoinIntervalSeconds: 0f),

        new PhaseDef("HAZARD", 9f,  DifficultyOffset: 12, ScoreMultiplier: 1,
                     HazardScale: 1f,   PowerUpsEnabled: true,  CoinIntervalSeconds: 4.5f),

        new PhaseDef("FEAST",  10f, DifficultyOffset: 24, ScoreMultiplier: 2,
                     HazardScale: 1.3f, PowerUpsEnabled: true,  CoinIntervalSeconds: 3f),
    };

    // Bouts are short on purpose. A match is a single burst of concentration; past
    // about half a minute the timing stops being exciting and starts being work.
    private static readonly float[] Durations = { 20f, 22f, 24f, 26f };
    private static readonly int[] DifficultyOffsets = { 0, 8, 18, 28 };

    public static readonly IReadOnlyList<ShopItem> Shop = new[]
    {
        // The ids never change: an existing save keeps whatever it bought. The costs
        // do not change either - the ladder pays 375 and the shop asks 510, and that
        // gap is what makes buying one of these a choice.
        new ShopItem("skin_chef", "CHEF HAT", 30, "skin_chef"),
        new ShopItem("skin_gold", "GOLD TOOTH", 80, "skin_gold"),
        new ShopItem("skin_shadow", "SHADES", 150, "skin_shadow"),
        new ShopItem("skin_neon", "NEON CROWN", 250, "skin_neon"),
    };

    /// <summary>The rival's line for the interlude after the given phase.</summary>
    public static string InterludeLine(OpponentDef def, int phaseIndex, bool rivalAhead) =>
        phaseIndex == 0
            ? rivalAhead ? def.Interlude1Ahead : def.Interlude1Behind
            : rivalAhead ? def.Interlude2Ahead : def.Interlude2Behind;

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
    public static void RecordWin(SaveData data, BoutEnded ended)
    {
        var index = Progress(data);
        if (index >= Ladder.Count) return;

        data.DefeatedIds.Add(Ladder[index].Id);
        data.Money += ended.Prize;
        data.LifetimeEaten += ended.Eaten;

        if (ended.PlayerScore > data.BestScore) data.BestScore = ended.PlayerScore;
    }

    /// <summary>Records a loss. Progress is kept; only the prize is missed.</summary>
    public static void RecordLoss(SaveData data, BoutEnded ended)
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
