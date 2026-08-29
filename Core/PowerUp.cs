using System.Collections.Generic;

namespace CrocGame.Core;

public enum BuffKind
{
    Slow,
    Shield,
    Magnet,
    GoldTooth,
}

/// <summary>Maps the `power` column of food.json onto a buff.</summary>
public static class PowerUp
{
    public static BuffKind? Parse(string id) => id switch
    {
        "slow" => BuffKind.Slow,
        "shield" => BuffKind.Shield,
        "magnet" => BuffKind.Magnet,
        "goldtooth" => BuffKind.GoldTooth,
        _ => null,
    };
}

/// <summary>
/// What the croc is currently carrying, and for how long.
///
/// The buffs are deliberately lopsided. SLOW and SHIELD are wide, common and
/// defensive - that is the beginner's lane, and a player who bites indiscriminately
/// collects them at roughly the rate they need them. MAGNET and GOLD TOOTH are narrow,
/// guarded by bombs and offensive: they are the reward for choosing to take a hard
/// press when an easy one was available.
///
/// Duplicates refresh rather than stack. Stacking would let one lucky cluster decide a
/// phase, which is exactly the kind of unearned swing that makes RNG feel cheap
/// instead of generous.
/// </summary>
public sealed class ActiveBuffs
{
    public const float SlowSeconds = 4f;
    public const float SlowSpeedMultiplier = 0.6f;
    public const float GoldToothSeconds = 5f;
    public const int GoldToothMultiplier = 3;
    public const int MagnetBites = 3;

    private readonly List<BuffKind> _expired = new();

    public float SlowRemaining { get; private set; }
    public float GoldToothRemaining { get; private set; }
    public int MagnetBitesRemaining { get; private set; }
    public bool HasShield { get; private set; }

    public float SpeedMultiplier => SlowRemaining > 0f ? SlowSpeedMultiplier : 1f;

    public int ScoreMultiplier => GoldToothRemaining > 0f ? GoldToothMultiplier : 1;

    public void Take(BuffKind kind)
    {
        switch (kind)
        {
            case BuffKind.Slow: SlowRemaining = SlowSeconds; break;
            case BuffKind.Shield: HasShield = true; break;
            case BuffKind.Magnet: MagnetBitesRemaining = MagnetBites; break;
            case BuffKind.GoldTooth: GoldToothRemaining = GoldToothSeconds; break;
        }
    }

    /// <summary>Advances the timed buffs. Returns the kinds that ran out this frame.</summary>
    public IReadOnlyList<BuffKind> Tick(float dt)
    {
        _expired.Clear();

        if (SlowRemaining > 0f)
        {
            SlowRemaining -= dt;
            if (SlowRemaining <= 0f)
            {
                SlowRemaining = 0f;
                _expired.Add(BuffKind.Slow);
            }
        }

        if (GoldToothRemaining > 0f)
        {
            GoldToothRemaining -= dt;
            if (GoldToothRemaining <= 0f)
            {
                GoldToothRemaining = 0f;
                _expired.Add(BuffKind.GoldTooth);
            }
        }

        return _expired;
    }

    /// <summary>True when a shield was spent absorbing a strike.</summary>
    public bool ConsumeShield()
    {
        if (!HasShield) return false;

        HasShield = false;
        return true;
    }

    /// <summary>True when this bite was taken by the magnet rather than by aim.</summary>
    public bool ConsumeMagnetBite()
    {
        if (MagnetBitesRemaining <= 0) return false;

        MagnetBitesRemaining--;
        return true;
    }

    public void ResetForPhase()
    {
        SlowRemaining = 0f;
        GoldToothRemaining = 0f;
        MagnetBitesRemaining = 0;
        HasShield = false;
        _expired.Clear();
    }
}
