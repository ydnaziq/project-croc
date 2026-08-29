using System;

namespace CrocGame.Core;

/// <summary>
/// The comeback super, and the one mechanic in the game that is unambiguously a
/// crutch. It charges only while the player is behind, faster the further behind, and
/// it fires by itself - a player who needs it is not asked to also press a button to
/// get it.
///
/// Frenzy is what a winning croc earns; Hunger is what a losing croc is given. That
/// pairing is the premise: he is starving, and it is the hunger that makes him
/// dangerous.
///
/// The widened jaw zone is reported here and drawn at this width by the view. A
/// secretly wider window would be the rubber-banding this design rejected: the player
/// has to be able to see that the game is helping.
/// </summary>
public sealed class Hunger
{
    /// <summary>Seconds to fill from empty at a total deficit. A partial deficit is
    /// proportionally slower.</summary>
    public const float ChargeSeconds = 7f;

    public const float DurationSeconds = 5f;
    public const float BeltSpeedMultiplier = 0.8f;
    public const float JawWidthMultiplier = 1.6f;

    /// <summary>Falling apart accelerates desperation.</summary>
    public const float StrikeCharge = 0.15f;

    public float Charge { get; private set; }
    public float Remaining { get; private set; }
    public bool HasFiredThisPhase { get; private set; }

    public bool IsActive => Remaining > 0f;

    public float SpeedMultiplier => IsActive ? BeltSpeedMultiplier : 1f;

    public float JawMultiplier => IsActive ? JawWidthMultiplier : 1f;

    /// <summary>Advances the meter for one frame at the current scoreline.</summary>
    public void Update(float dt, int playerScore, int opponentScore)
    {
        if (IsActive || HasFiredThisPhase) return;

        var deficit = opponentScore - playerScore;
        if (deficit <= 0) return;

        // Deficit as a fraction of the rival's total, so "behind by 40" means much more
        // early in a bout than it does late in one.
        var fraction = Math.Clamp(deficit / (float)Math.Max(opponentScore, 1), 0f, 1f);

        Charge = Math.Min(1f, Charge + fraction * dt / ChargeSeconds);
    }

    public void OnStrike()
    {
        if (IsActive || HasFiredThisPhase) return;

        Charge = Math.Min(1f, Charge + StrikeCharge);
    }

    /// <summary>Fires if the meter is full and it has not already fired this phase.</summary>
    public bool TryFire()
    {
        if (IsActive || HasFiredThisPhase || Charge < 1f) return false;

        Remaining = DurationSeconds;
        HasFiredThisPhase = true;
        Charge = 0f;
        return true;
    }

    /// <summary>Returns true on the frame the window closes.</summary>
    public bool Tick(float dt)
    {
        if (!IsActive) return false;

        Remaining -= dt;
        if (Remaining > 0f) return false;

        Remaining = 0f;
        return true;
    }

    public void ResetForPhase()
    {
        Charge = 0f;
        Remaining = 0f;
        HasFiredThisPhase = false;
    }
}
