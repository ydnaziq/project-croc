using System.Collections.Generic;
using System.Linq;

namespace CrocGame.Core;

/// <summary>
/// A cosmetic reward and what it costs to earn. Nothing here touches difficulty,
/// scoring, or the timing window, so an early run and a late run stay comparable.
/// </summary>
public sealed record Milestone(
    string Id,
    string Label,
    int LifetimeEatenRequired,
    int BestScoreRequired);

/// <summary>
/// Which cosmetics the player has earned.
///
/// Unlocks are evaluated here rather than emitted by GameSession, because they depend
/// on lifetime totals held in the save file. GameSession only knows about the run in
/// front of it, and giving it a save-file dependency to fire one event would cost more
/// than the event is worth.
/// </summary>
public static class UnlockCatalog
{
    public static readonly IReadOnlyList<Milestone> All = new[]
    {
        new Milestone("croc_gold", "gold croc", LifetimeEatenRequired: 100, BestScoreRequired: 0),
        new Milestone("croc_blue", "blue croc", LifetimeEatenRequired: 0, BestScoreRequired: 500),
        new Milestone("croc_pink", "pink croc", LifetimeEatenRequired: 500, BestScoreRequired: 0),
        new Milestone("croc_ghost", "ghost croc", LifetimeEatenRequired: 0, BestScoreRequired: 1500),
    };

    /// <summary>Milestones the save now qualifies for but has not recorded yet.</summary>
    public static IReadOnlyList<Milestone> NewlyUnlocked(SaveData data) =>
        All.Where(m => Qualifies(data, m) && !data.UnlockedIds.Contains(m.Id)).ToList();

    /// <summary>Records the newly earned milestones on the save and returns them.</summary>
    public static IReadOnlyList<Milestone> Apply(SaveData data)
    {
        var earned = NewlyUnlocked(data);

        foreach (var milestone in earned) data.UnlockedIds.Add(milestone.Id);

        return earned;
    }

    private static bool Qualifies(SaveData data, Milestone m) =>
        data.LifetimeEaten >= m.LifetimeEatenRequired && data.BestScore >= m.BestScoreRequired;
}
