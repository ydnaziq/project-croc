namespace CrocGame.Core;

/// <summary>
/// The push-your-luck layer, and the only place in the game where the player makes a
/// decision rather than a press.
///
/// Every bite adds its points to the pot as well as to the score. The pot pays out
/// only when a cash-out coin is bitten, at a multiplier that climbs with the combo -
/// and a strike wipes it. Riding past a coin to let the pot grow is the risk; the
/// multiplier is the reward.
///
/// The score itself is never at stake. The pot is upside stacked on top of ordinary
/// scoring, so a player who banks every coin the instant it arrives is playing a safe,
/// viable game rather than a punished one.
/// </summary>
public sealed class Pot
{
    public int Amount { get; private set; }

    public bool IsEmpty => Amount == 0;

    public void Add(int points)
    {
        if (points > 0) Amount += points;
    }

    /// <summary>What banking right now would pay. Drawn on the coin, so the size of the
    /// wager is on screen at the moment the decision is made.</summary>
    public int PendingAt(int combo) => Amount * MultiplierForCombo(combo);

    /// <summary>Pays out and empties. Returns what was paid.</summary>
    public int Bank(int combo)
    {
        var paid = PendingAt(combo);
        Amount = 0;
        return paid;
    }

    public void Wipe() => Amount = 0;

    /// <summary>
    /// Steps rather than a curve: a player has to be able to see the multiplier tick
    /// over and decide, which a continuously rising number does not let them do.
    /// </summary>
    public static int MultiplierForCombo(int combo)
    {
        if (combo >= 15) return 5;
        if (combo >= 10) return 3;
        if (combo >= 5) return 2;
        return 1;
    }
}
