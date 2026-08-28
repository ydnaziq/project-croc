using System.Collections.Generic;

namespace CrocGame.Core;

public enum ChompOutcome
{
    Hit,
    Air,
}

/// <summary>What the jaws closed on. Item is null only for an air chomp.</summary>
public readonly record struct ChompResult(ChompOutcome Outcome, FoodItem? Item);

/// <summary>
/// Resolves a chomp against whatever is in the jaw zone. Reports what was bitten;
/// deciding whether that is good news belongs to GameSession.
/// </summary>
public static class ChompJudge
{
    public static ChompResult Judge(JawZone jaw, IReadOnlyList<FoodItem> items)
    {
        FoodItem? best = null;
        var bestDistance = float.MaxValue;

        foreach (var item in items)
        {
            if (!jaw.Overlaps(item)) continue;

            var distance = jaw.DistanceFromCenter(item);
            if (distance >= bestDistance) continue;

            best = item;
            bestDistance = distance;
        }

        return best is null
            ? new ChompResult(ChompOutcome.Air, null)
            : new ChompResult(ChompOutcome.Hit, best);
    }
}
