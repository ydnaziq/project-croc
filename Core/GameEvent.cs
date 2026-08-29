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

/// <summary>How a bout finished. There is no disqualified case: a knockout ends a
/// phase, never the bout.</summary>
public enum BoutResult
{
    InProgress,
    Won,
    Lost,
}

public sealed record PhaseStarted(int PhaseIndex, PhaseDef Phase) : GameEvent;

/// <summary>A phase reached its bell. Scores are the carried bout totals.</summary>
public sealed record PhaseEnded(int PhaseIndex, bool KnockedOut, int PlayerScore, int OpponentScore) : GameEvent;

/// <summary>Third strike in a phase. The player is out of this phase, not the bout,
/// and the rival eats the remaining seconds unopposed.</summary>
public sealed record PhaseKnockout(int PhaseIndex, float SecondsConceded) : GameEvent;

/// <summary>A cash-out coin is on the belt, carrying what banking it would pay.</summary>
public sealed record CoinSpawned(FoodItem Item, int Value) : GameEvent;

public sealed record PotBanked(int Amount, int Multiplier, int Paid) : GameEvent;

public sealed record PotWiped(int Lost) : GameEvent;

public sealed record BuffTaken(BuffKind Kind) : GameEvent;

public sealed record BuffExpired(BuffKind Kind) : GameEvent;

/// <summary>The hunger meter is full and about to fire. Fraction is 0..1.</summary>
public sealed record HungerCharged(float Fraction) : GameEvent;

public sealed record HungerStarted(float JawMultiplier, float Seconds) : GameEvent;

public sealed record HungerEnded : GameEvent;

public sealed record BoutEnded(
    BoutResult Result,
    int PlayerScore,
    int OpponentScore,
    int Prize,
    int BestCombo,
    int Eaten) : GameEvent;
