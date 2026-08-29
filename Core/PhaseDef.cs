namespace CrocGame.Core;

/// <summary>
/// One act of a bout. Phases are data rather than branches so the variation between
/// them is a table anyone can retune, not control flow anyone has to read.
///
/// HazardScale multiplies Difficulty.InedibleChance, which is how PLAIN stays clean no
/// matter how far along the difficulty curve a late-career bout starts.
/// </summary>
public sealed record PhaseDef(
    string Name,
    float DurationSeconds,
    int DifficultyOffset,
    int ScoreMultiplier,
    float HazardScale,
    bool PowerUpsEnabled,

    /// <summary>Seconds between cash-out coins. Zero means the pot is not live at all.</summary>
    float CoinIntervalSeconds);
