namespace CrocGame.Core;

/// <summary>
/// Everything the scene layer needs to render a frame. The scene layer never asks the
/// session a question mid-frame; it reads these.
/// </summary>
public abstract record GameEvent;

public sealed record Spawned(FoodItem Item) : GameEvent;

/// <summary>A chomp landed on an item. Edible or not is on the item.</summary>
public sealed record Chomped(FoodItem Item, int Combo, int ScoreAwarded, bool DuringFrenzy) : GameEvent;

public sealed record ChompedAir : GameEvent;

/// <summary>An edible item rode past the jaws unchomped.</summary>
public sealed record Passed(FoodItem Item) : GameEvent;

public sealed record StrikeAdded(int Strikes) : GameEvent;

/// <summary>The rival took a bite. Carries their running total.</summary>
public sealed record OpponentAte(int OpponentScore) : GameEvent;

public sealed record FrenzyStarted : GameEvent;

public sealed record FrenzyEnded : GameEvent;

public sealed record MatchEnded(
    MatchResult Result,
    int PlayerScore,
    int OpponentScore,
    int Prize,
    int BestCombo,
    int Eaten) : GameEvent;
