namespace CrocGame.Core;

/// <summary>
/// Everything the scene layer needs to render a frame. The scene layer never asks the
/// session a question mid-frame; it reads these.
/// </summary>
public abstract record GameEvent;

public sealed record Spawned(FoodItem Item) : GameEvent;

/// <summary>A chomp landed on an item. Edible or not is on the item.</summary>
public sealed record Chomped(FoodItem Item, int Combo, int ScoreAwarded) : GameEvent;

public sealed record ChompedAir : GameEvent;

/// <summary>An edible item rode past the jaws unchomped.</summary>
public sealed record Passed(FoodItem Item) : GameEvent;

public sealed record StrikeAdded(int Strikes) : GameEvent;

public sealed record RunEnded(int FinalScore, int Eaten) : GameEvent;
