# Croc Three-Phase Bouts — Mechanics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single-block bout with three rule-varying phases separated by dialogue, add the Pot/coin wager, belt power-ups, and the automatic Hunger comeback super, and make every text box size and centre itself around its content.

**Architecture:** All rules stay in `CrocGame.Core`, which cannot reference GodotSharp, so every mechanic below is developed test-first against a fake clock. `MatchSession` narrows to a single phase and is renamed `PhaseSession`; a new `BoutSession` owns the three phases, the carried score, and the rival, and becomes the only surface the scene layer talks to. The scene layer renders events and nothing else.

**Tech Stack:** C# / .NET 10, xUnit, Godot 4.7.2 mono. `dotnet test` for Core; `Scripts/AutoPlay.cs` with `--autoplay --shots=` for the scene layer.

**Spec:** `docs/superpowers/specs/2026-08-29-croc-three-phase-bouts-design.md`

## Global Constraints

- **`CrocGame.Core` must never reference GodotSharp.** The reference is absent from `Core/CrocGame.Core.csproj` and must stay absent. Core reads no wall clock and no input: it receives `dt` and discrete commands and returns events.
- **Target framework is `net10.0`** for both `Core` and `Tests`.
- **All randomness goes through `IRandomSource`.** A seed must reproduce a whole bout identically, coins and buffs included.
- **One verb.** No new input is added. Every mechanic is driven by the existing single `chomp` press.
- **Judge what the player can see.** Any change to the effective jaw width must be reported to the view at the same value the judge uses. The sole permitted exception is MAGNET, which suspends judging visibly (Task 15).
- **Base score is never at risk.** The Pot is additive upside only; no mechanic in this plan may reduce a score already awarded.
- **Silkscreen renders only at 8, 16, 24** (`Ui.Small`, `Ui.Body`, `Ui.Title`). No other font size may be used.
- **Every edge is 1px black.** Use `Ui.Panel` / `Ui.RaisedPanel` / `Ui.Text`; nothing draws its own border.
- **Baseline:** `dotnet test` currently reports `Passed: 116`. It must never be left red at the end of a task.

---

### Task 1: `PhaseDef` and the phase list

**Files:**
- Create: `Core/PhaseDef.cs`
- Modify: `Core/Career.cs`
- Test: `Tests/PhaseDefTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `PhaseDef(string Name, float DurationSeconds, int DifficultyOffset, int ScoreMultiplier, float HazardScale, bool PowerUpsEnabled, float CoinIntervalSeconds)`; `Career.Phases` as `IReadOnlyList<PhaseDef>` of length 3.

- [ ] **Step 1: Write the failing test**

`Tests/PhaseDefTests.cs`:

```csharp
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class PhaseDefTests
{
    [Fact]
    public void ABoutIsExactlyThreePhases()
    {
        Assert.Equal(3, Career.Phases.Count);
    }

    [Fact]
    public void PlainIsCleanAndCarriesNoWager()
    {
        var plain = Career.Phases[0];

        Assert.Equal("PLAIN", plain.Name);
        Assert.Equal(0f, plain.HazardScale, precision: 4);
        Assert.False(plain.PowerUpsEnabled);
        Assert.Equal(0f, plain.CoinIntervalSeconds, precision: 4);
        Assert.Equal(1, plain.ScoreMultiplier);
    }

    [Fact]
    public void FeastIsTheHardestAndPaysDouble()
    {
        var hazard = Career.Phases[1];
        var feast = Career.Phases[2];

        Assert.True(feast.DifficultyOffset > hazard.DifficultyOffset);
        Assert.True(feast.HazardScale > hazard.HazardScale);
        Assert.True(feast.CoinIntervalSeconds < hazard.CoinIntervalSeconds);
        Assert.Equal(2, feast.ScoreMultiplier);
    }

    [Fact]
    public void PhaseLengthsMatchTheSpec()
    {
        Assert.Equal(8f, Career.Phases[0].DurationSeconds, precision: 3);
        Assert.Equal(9f, Career.Phases[1].DurationSeconds, precision: 3);
        Assert.Equal(10f, Career.Phases[2].DurationSeconds, precision: 3);
    }
}
```

- [ ] **Step 2: Run it to make sure it fails**

Run: `dotnet test --filter FullyQualifiedName~PhaseDefTests`
Expected: FAIL — `PhaseDef` and `Career.Phases` do not exist (compile error CS0246 / CS0117).

- [ ] **Step 3: Write the minimal implementation**

`Core/PhaseDef.cs`:

```csharp
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
```

In `Core/Career.cs`, add above `Durations`:

```csharp
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
```

- [ ] **Step 4: Run the tests and make sure they pass**

Run: `dotnet test`
Expected: PASS — 120 total, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Core/PhaseDef.cs Core/Career.cs Tests/PhaseDefTests.cs
git commit -m "Add PhaseDef and the three-phase bout table"
```

---

### Task 2: `Pot` — the wager

**Files:**
- Create: `Core/Pot.cs`
- Test: `Tests/PotTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Pot` with `int Amount`, `bool IsEmpty`, `void Add(int points)`, `int PendingAt(int combo)`, `int Bank(int combo)`, `void Wipe()`, and `static int MultiplierForCombo(int combo)`.

- [ ] **Step 1: Write the failing test**

`Tests/PotTests.cs`:

```csharp
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class PotTests
{
    [Fact]
    public void StartsEmpty()
    {
        var pot = new Pot();

        Assert.True(pot.IsEmpty);
        Assert.Equal(0, pot.Amount);
    }

    [Fact]
    public void AddingAccrues()
    {
        var pot = new Pot();
        pot.Add(10);
        pot.Add(15);

        Assert.Equal(25, pot.Amount);
        Assert.False(pot.IsEmpty);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(9, 2)]
    [InlineData(10, 3)]
    [InlineData(14, 3)]
    [InlineData(15, 5)]
    [InlineData(40, 5)]
    public void MultiplierStepsOnComboBoundaries(int combo, int expected)
    {
        Assert.Equal(expected, Pot.MultiplierForCombo(combo));
    }

    [Fact]
    public void BankingPaysTheAmountTimesTheMultiplierAndEmptiesThePot()
    {
        var pot = new Pot();
        pot.Add(30);

        Assert.Equal(90, pot.PendingAt(combo: 12));
        Assert.Equal(90, pot.Bank(combo: 12));
        Assert.True(pot.IsEmpty);
    }

    [Fact]
    public void BankingAnEmptyPotPaysNothing()
    {
        var pot = new Pot();

        Assert.Equal(0, pot.Bank(combo: 20));
    }

    [Fact]
    public void WipingLosesEverythingUnbanked()
    {
        var pot = new Pot();
        pot.Add(100);
        pot.Wipe();

        Assert.True(pot.IsEmpty);
        Assert.Equal(0, pot.Bank(combo: 15));
    }
}
```

- [ ] **Step 2: Run it to make sure it fails**

Run: `dotnet test --filter FullyQualifiedName~PotTests`
Expected: FAIL — `Pot` does not exist (CS0246).

- [ ] **Step 3: Write the minimal implementation**

`Core/Pot.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests and make sure they pass**

Run: `dotnet test`
Expected: PASS — 133 total, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Core/Pot.cs Tests/PotTests.cs
git commit -m "Add the Pot: bites accrue a wager banked by biting a coin"
```

---

### Task 3: `Hunger` — the comeback super

**Files:**
- Create: `Core/Hunger.cs`
- Test: `Tests/HungerTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Hunger` with `float Charge`, `bool IsActive`, `float Remaining`, `bool HasFiredThisPhase`, `void Update(float dt, int playerScore, int opponentScore)`, `bool TryFire()`, `bool Tick(float dt)`, `void OnStrike()`, `void ResetForPhase()`, `float SpeedMultiplier`, `float JawMultiplier`; constants `ChargeSeconds`, `DurationSeconds`, `BeltSpeedMultiplier`, `JawWidthMultiplier`, `StrikeCharge`.

- [ ] **Step 1: Write the failing test**

`Tests/HungerTests.cs`:

```csharp
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class HungerTests
{
    /// <summary>Runs the meter for a while at a fixed scoreline.</summary>
    private static void Feed(Hunger hunger, int player, int opponent, float seconds)
    {
        for (var t = 0f; t < seconds; t += 0.05f) hunger.Update(0.05f, player, opponent);
    }

    [Fact]
    public void StartsEmptyAndInactive()
    {
        var hunger = new Hunger();

        Assert.Equal(0f, hunger.Charge, precision: 4);
        Assert.False(hunger.IsActive);
        Assert.Equal(1f, hunger.SpeedMultiplier, precision: 4);
        Assert.Equal(1f, hunger.JawMultiplier, precision: 4);
    }

    [Fact]
    public void DoesNotChargeWhileAhead()
    {
        var hunger = new Hunger();
        Feed(hunger, player: 500, opponent: 100, seconds: 20f);

        Assert.Equal(0f, hunger.Charge, precision: 4);
        Assert.False(hunger.TryFire());
    }

    [Fact]
    public void DoesNotChargeWhenLevel()
    {
        var hunger = new Hunger();
        Feed(hunger, player: 200, opponent: 200, seconds: 20f);

        Assert.Equal(0f, hunger.Charge, precision: 4);
    }

    [Fact]
    public void ChargesFasterTheFurtherBehind()
    {
        var near = new Hunger();
        Feed(near, player: 90, opponent: 100, seconds: 2f);

        var far = new Hunger();
        Feed(far, player: 0, opponent: 100, seconds: 2f);

        Assert.True(far.Charge > near.Charge);
    }

    [Fact]
    public void AStrikeAdvancesTheMeter()
    {
        var hunger = new Hunger();
        hunger.OnStrike();

        Assert.Equal(Hunger.StrikeCharge, hunger.Charge, precision: 4);
    }

    [Fact]
    public void FiresOnlyOnceAFullMeterIsReached()
    {
        var hunger = new Hunger();
        Assert.False(hunger.TryFire());

        Feed(hunger, player: 0, opponent: 100, seconds: Hunger.ChargeSeconds + 1f);

        Assert.True(hunger.TryFire());
        Assert.True(hunger.IsActive);
        Assert.Equal(Hunger.DurationSeconds, hunger.Remaining, precision: 3);
    }

    [Fact]
    public void FiresAtMostOncePerPhase()
    {
        var hunger = new Hunger();
        Feed(hunger, player: 0, opponent: 100, seconds: Hunger.ChargeSeconds + 1f);
        Assert.True(hunger.TryFire());

        for (var i = 0; i < 200; i++) hunger.Tick(0.05f);
        Feed(hunger, player: 0, opponent: 100, seconds: Hunger.ChargeSeconds + 1f);

        Assert.True(hunger.HasFiredThisPhase);
        Assert.False(hunger.TryFire());
    }

    [Fact]
    public void ResettingForAPhaseAllowsItToFireAgain()
    {
        var hunger = new Hunger();
        Feed(hunger, player: 0, opponent: 100, seconds: Hunger.ChargeSeconds + 1f);
        Assert.True(hunger.TryFire());

        hunger.ResetForPhase();

        Assert.False(hunger.HasFiredThisPhase);
        Assert.False(hunger.IsActive);
        Assert.Equal(0f, hunger.Charge, precision: 4);
    }

    [Fact]
    public void WhileActiveItSlowsTheBeltAndWidensTheJaws()
    {
        var hunger = new Hunger();
        Feed(hunger, player: 0, opponent: 100, seconds: Hunger.ChargeSeconds + 1f);
        hunger.TryFire();

        Assert.Equal(Hunger.BeltSpeedMultiplier, hunger.SpeedMultiplier, precision: 4);
        Assert.Equal(Hunger.JawWidthMultiplier, hunger.JawMultiplier, precision: 4);
        Assert.True(hunger.JawMultiplier > 1f);
        Assert.True(hunger.SpeedMultiplier < 1f);
    }

    [Fact]
    public void TickReportsTheFrameItExpiresAndThenStopsHelping()
    {
        var hunger = new Hunger();
        Feed(hunger, player: 0, opponent: 100, seconds: Hunger.ChargeSeconds + 1f);
        hunger.TryFire();

        var ended = false;
        for (var t = 0f; t < Hunger.DurationSeconds + 1f; t += 0.05f)
        {
            if (hunger.Tick(0.05f)) ended = true;
        }

        Assert.True(ended);
        Assert.False(hunger.IsActive);
        Assert.Equal(1f, hunger.JawMultiplier, precision: 4);
    }
}
```

- [ ] **Step 2: Run it to make sure it fails**

Run: `dotnet test --filter FullyQualifiedName~HungerTests`
Expected: FAIL — `Hunger` does not exist (CS0246).

- [ ] **Step 3: Write the minimal implementation**

`Core/Hunger.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests and make sure they pass**

Run: `dotnet test`
Expected: PASS — 143 total, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Core/Hunger.cs Tests/HungerTests.cs
git commit -m "Add Hunger: an automatic comeback window that charges from a deficit"
```

---

### Task 4: `ActiveBuffs` — power-up effects and their timers

**Files:**
- Create: `Core/PowerUp.cs`
- Test: `Tests/ActiveBuffsTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `enum BuffKind { Slow, Shield, Magnet, GoldTooth }`; `static class PowerUp` with `BuffKind? Parse(string id)`; `ActiveBuffs` with `void Take(BuffKind)`, `IReadOnlyList<BuffKind> Tick(float dt)`, `bool ConsumeShield()`, `bool ConsumeMagnetBite()`, `bool HasShield`, `int MagnetBitesRemaining`, `float SlowRemaining`, `float GoldToothRemaining`, `float SpeedMultiplier`, `int ScoreMultiplier`, `void ResetForPhase()`; constants `SlowSeconds`, `SlowSpeedMultiplier`, `GoldToothSeconds`, `GoldToothMultiplier`, `MagnetBites`.

- [ ] **Step 1: Write the failing test**

`Tests/ActiveBuffsTests.cs`:

```csharp
using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class ActiveBuffsTests
{
    [Fact]
    public void ParsesTheFourPowerIds()
    {
        Assert.Equal(BuffKind.Slow, PowerUp.Parse("slow"));
        Assert.Equal(BuffKind.Shield, PowerUp.Parse("shield"));
        Assert.Equal(BuffKind.Magnet, PowerUp.Parse("magnet"));
        Assert.Equal(BuffKind.GoldTooth, PowerUp.Parse("goldtooth"));
        Assert.Null(PowerUp.Parse(""));
        Assert.Null(PowerUp.Parse("pizza"));
    }

    [Fact]
    public void StartsWithNothing()
    {
        var buffs = new ActiveBuffs();

        Assert.False(buffs.HasShield);
        Assert.Equal(0, buffs.MagnetBitesRemaining);
        Assert.Equal(1f, buffs.SpeedMultiplier, precision: 4);
        Assert.Equal(1, buffs.ScoreMultiplier);
    }

    [Fact]
    public void SlowReducesBeltSpeedUntilItExpires()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.Slow);

        Assert.Equal(ActiveBuffs.SlowSpeedMultiplier, buffs.SpeedMultiplier, precision: 4);

        var expired = Run(buffs, ActiveBuffs.SlowSeconds + 0.5f);

        Assert.Contains(BuffKind.Slow, expired);
        Assert.Equal(1f, buffs.SpeedMultiplier, precision: 4);
    }

    [Fact]
    public void GoldToothTriplesScoreUntilItExpires()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.GoldTooth);

        Assert.Equal(ActiveBuffs.GoldToothMultiplier, buffs.ScoreMultiplier);

        var expired = Run(buffs, ActiveBuffs.GoldToothSeconds + 0.5f);

        Assert.Contains(BuffKind.GoldTooth, expired);
        Assert.Equal(1, buffs.ScoreMultiplier);
    }

    [Fact]
    public void ShieldAbsorbsExactlyOneStrike()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.Shield);

        Assert.True(buffs.HasShield);
        Assert.True(buffs.ConsumeShield());
        Assert.False(buffs.HasShield);
        Assert.False(buffs.ConsumeShield());
    }

    [Fact]
    public void MagnetAppliesToExactlyThreeBites()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.Magnet);

        Assert.Equal(ActiveBuffs.MagnetBites, buffs.MagnetBitesRemaining);

        for (var i = 0; i < ActiveBuffs.MagnetBites; i++) Assert.True(buffs.ConsumeMagnetBite());

        Assert.False(buffs.ConsumeMagnetBite());
        Assert.Equal(0, buffs.MagnetBitesRemaining);
    }

    [Fact]
    public void MagnetDoesNotExpireOnTime()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.Magnet);

        Run(buffs, 60f);

        Assert.Equal(ActiveBuffs.MagnetBites, buffs.MagnetBitesRemaining);
    }

    [Fact]
    public void TakingTheSameBuffTwiceRefreshesRatherThanStacks()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.Slow);
        Run(buffs, ActiveBuffs.SlowSeconds * 0.5f);
        buffs.Take(BuffKind.Slow);

        Assert.Equal(ActiveBuffs.SlowSeconds, buffs.SlowRemaining, precision: 2);

        buffs.Take(BuffKind.Magnet);
        buffs.ConsumeMagnetBite();
        buffs.Take(BuffKind.Magnet);

        Assert.Equal(ActiveBuffs.MagnetBites, buffs.MagnetBitesRemaining);
    }

    [Fact]
    public void ResettingForAPhaseClearsEverything()
    {
        var buffs = new ActiveBuffs();
        buffs.Take(BuffKind.Slow);
        buffs.Take(BuffKind.Shield);
        buffs.Take(BuffKind.Magnet);
        buffs.Take(BuffKind.GoldTooth);

        buffs.ResetForPhase();

        Assert.False(buffs.HasShield);
        Assert.Equal(0, buffs.MagnetBitesRemaining);
        Assert.Equal(1f, buffs.SpeedMultiplier, precision: 4);
        Assert.Equal(1, buffs.ScoreMultiplier);
    }

    private static System.Collections.Generic.List<BuffKind> Run(ActiveBuffs buffs, float seconds)
    {
        var expired = new System.Collections.Generic.List<BuffKind>();
        for (var t = 0f; t < seconds; t += 0.05f) expired.AddRange(buffs.Tick(0.05f));
        return expired;
    }
}
```

- [ ] **Step 2: Run it to make sure it fails**

Run: `dotnet test --filter FullyQualifiedName~ActiveBuffsTests`
Expected: FAIL — `BuffKind`, `PowerUp`, `ActiveBuffs` do not exist (CS0246).

- [ ] **Step 3: Write the minimal implementation**

`Core/PowerUp.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests and make sure they pass**

Run: `dotnet test`
Expected: PASS — 153 total, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Core/PowerUp.cs Tests/ActiveBuffsTests.cs
git commit -m "Add power-up buffs: slow, shield, magnet, gold tooth"
```

---

### Task 5: Carry `power` through the food table and onto items

**Files:**
- Modify: `Core/FoodTable.cs`, `Core/FoodItem.cs`, `Core/SpawnDirector.cs`, `Resources/food.json`
- Test: `Tests/FoodTableTests.cs`

**Interfaces:**
- Consumes: `BuffKind`, `PowerUp.Parse` from Task 4.
- Produces: `FoodType` gains `string Power = ""`; `FoodItem` constructor gains a trailing `string power = ""` parameter and a `string Power { get; }` property; `FoodTable.PowerUps(int eaten)` returning `IReadOnlyList<FoodType>`.

- [ ] **Step 1: Write the failing test**

Append to `Tests/FoodTableTests.cs` (inside the existing class):

```csharp
    [Fact]
    public void RowsWithoutAPowerColumnAreOrdinaryFood()
    {
        var table = FoodTable.FromJson(
            """[{ "id":"pizza","width":16,"edible":true,"movement":"constant","score":10,"minEatenToAppear":0 }]""");

        Assert.Equal("", table.Types[0].Power);
    }

    [Fact]
    public void PowerRowsAreReadAndAreNotOfferedAsOrdinaryFood()
    {
        var table = FoodTable.FromJson(
            """
            [
              { "id":"pizza","width":16,"edible":true,"movement":"constant","score":10,"minEatenToAppear":0 },
              { "id":"slow","width":16,"edible":true,"movement":"constant","score":0,"minEatenToAppear":0,"power":"slow" }
            ]
            """);

        Assert.Equal("slow", table.Types[1].Power);

        // Available() feeds ordinary spawning, which must never pick a buff by accident.
        Assert.Single(table.Available(eaten: 0, edible: true));
        Assert.Equal("pizza", table.Available(eaten: 0, edible: true)[0].Id);

        var powers = table.PowerUps(eaten: 0);
        Assert.Single(powers);
        Assert.Equal("slow", powers[0].Id);
    }

    [Fact]
    public void PowerUpsRespectMinEatenToAppear()
    {
        var table = FoodTable.FromJson(
            """[{ "id":"magnet","width":10,"edible":true,"movement":"constant","score":0,"minEatenToAppear":6,"power":"magnet" }]""");

        Assert.Empty(table.PowerUps(eaten: 5));
        Assert.Single(table.PowerUps(eaten: 6));
    }

    [Fact]
    public void TheShippedTableDefinesAllFourBuffs()
    {
        var json = System.IO.File.ReadAllText(
            System.IO.Path.Combine(RepoRoot(), "Resources", "food.json"));
        var table = FoodTable.FromJson(json);

        var powers = table.Types.Where(t => t.Power != "").Select(t => t.Power).ToList();

        Assert.Contains("slow", powers);
        Assert.Contains("shield", powers);
        Assert.Contains("magnet", powers);
        Assert.Contains("goldtooth", powers);
        Assert.All(table.Types.Where(t => t.Power != ""),
                   t => Assert.NotNull(PowerUp.Parse(t.Power)));
    }

    /// <summary>Walks up from the test binary to the repo root, so the shipped
    /// food.json is checked rather than a copy that can drift from it.</summary>
    private static string RepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);

        while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CrocGame.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
```

Add `using System.Linq;` to the top of `Tests/FoodTableTests.cs` if it is not already there.

- [ ] **Step 2: Run it to make sure it fails**

Run: `dotnet test --filter FullyQualifiedName~FoodTableTests`
Expected: FAIL — `FoodType.Power` and `FoodTable.PowerUps` do not exist (CS1061).

- [ ] **Step 3: Write the minimal implementation**

In `Core/FoodTable.cs`, extend the record and add the query:

```csharp
public sealed record FoodType(
    string Id,
    float Width,
    bool Edible,
    string Movement,
    int Score,
    int MinEatenToAppear,
    int Weight = 1,

    /// <summary>A buff id from PowerUp.Parse, or "" for ordinary food. Buffs are never
    /// offered to ordinary spawning; the director asks for them by name.</summary>
    string Power = "");
```

and inside `FoodTable`:

```csharp
    /// <summary>Types eligible to spawn right now. May legitimately be empty.</summary>
    public IReadOnlyList<FoodType> Available(int eaten, bool edible) =>
        _types.Where(t => t.Power == "" && t.Edible == edible && eaten >= t.MinEatenToAppear).ToList();

    /// <summary>Buffs eligible to spawn right now.</summary>
    public IReadOnlyList<FoodType> PowerUps(int eaten) =>
        _types.Where(t => t.Power != "" && eaten >= t.MinEatenToAppear).ToList();

    /// <summary>One row by id, or null. Used by the director to place guarded spawns.</summary>
    public FoodType? ById(string id) => _types.FirstOrDefault(t => t.Id == id);
```

In `Core/FoodItem.cs`, add the parameter and property:

```csharp
    public FoodItem(int id, string typeId, float x, float halfWidth,
                    bool isEdible, int score, IMovement movement, string power = "")
    {
        Id = id;
        TypeId = typeId;
        X = x;
        HalfWidth = halfWidth;
        IsEdible = isEdible;
        Score = score;
        Movement = movement;
        Power = power;
    }
```

```csharp
    /// <summary>The buff this item grants when bitten, or "" for ordinary food.</summary>
    public string Power { get; }
```

In `Core/SpawnDirector.cs`, pass it through in `Tick`:

```csharp
        return new FoodItem(
            id: _nextId++,
            typeId: type.Id,
            x: _spawnX,
            halfWidth: type.Width / 2f,
            isEdible: type.Edible,
            score: type.Score,
            movement: SelectMovement(type, difficulty),
            power: type.Power);
```

In `Resources/food.json`, append the four buff rows before the closing bracket (add a comma to the `boot` line):

```json
  { "id": "slow",      "width": 16, "edible": true, "movement": "constant", "score": 0, "minEatenToAppear": 0, "weight": 4, "power": "slow" },
  { "id": "shield",    "width": 14, "edible": true, "movement": "constant", "score": 0, "minEatenToAppear": 0, "weight": 3, "power": "shield" },
  { "id": "magnet",    "width": 10, "edible": true, "movement": "constant", "score": 0, "minEatenToAppear": 0, "weight": 2, "power": "magnet" },
  { "id": "goldtooth", "width": 8,  "edible": true, "movement": "bounce",   "score": 0, "minEatenToAppear": 0, "weight": 1, "power": "goldtooth" }
```

- [ ] **Step 4: Run the tests and make sure they pass**

Run: `dotnet test`
Expected: PASS — 157 total, 0 failed. The existing `SpawnDirectorTests` must still pass: buffs are excluded from `Available`, so ordinary spawning is unchanged.

- [ ] **Step 5: Commit**

```bash
git add Core/FoodTable.cs Core/FoodItem.cs Core/SpawnDirector.cs Resources/food.json Tests/FoodTableTests.cs
git commit -m "Carry a power column through the food table onto belt items"
```

---

### Task 6: Phase-aware spawning — hazard scale, guarded buffs, coins

**Files:**
- Modify: `Core/SpawnDirector.cs`
- Test: `Tests/SpawnDirectorTests.cs`

**Interfaces:**
- Consumes: `PhaseDef` (Task 1), `FoodTable.PowerUps` / `ById` (Task 5).
- Produces: `SpawnDirector.Tick(float dt, int eaten, PhaseDef phase)` replacing the two-argument overload; `SpawnDirector.MakeCoin(float halfWidth)` returning a `FoodItem` with `TypeId == "coin"` and `Power == "coin"`.

**Note for the implementer:** the existing `SpawnDirectorTests` call `Tick(dt, eaten)`. Update every call site to pass a `PhaseDef`. Use `Career.Phases[1]` (HAZARD) where the old behaviour is expected, since it has `HazardScale: 1f`, and `Career.Phases[0]` (PLAIN) for the clean-belt assertions.

- [ ] **Step 1: Write the failing test**

Append to `Tests/SpawnDirectorTests.cs`:

```csharp
    /// <summary>Drains a whole phase's worth of spawns.</summary>
    private static List<FoodItem> SpawnMany(SpawnDirector director, PhaseDef phase, int eaten, float seconds)
    {
        var items = new List<FoodItem>();
        for (var t = 0f; t < seconds; t += 0.02f)
        {
            var item = director.Tick(0.02f, eaten, phase);
            if (item is not null) items.Add(item);
        }
        return items;
    }

    [Fact]
    public void APhaseWithNoHazardScaleNeverSpawnsSomethingInedible()
    {
        var table = FoodTable.FromJson(System.IO.File.ReadAllText(FoodJsonPath()));
        var director = new SpawnDirector(table, new SeededRandom(7), spawnX: 0f);

        // Eaten is high enough that the difficulty curve would happily produce bombs.
        var items = SpawnMany(director, Career.Phases[0], eaten: 60, seconds: 60f);

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.True(i.IsEdible));
    }

    [Fact]
    public void APhaseWithPowerUpsDisabledNeverSpawnsABuff()
    {
        var table = FoodTable.FromJson(System.IO.File.ReadAllText(FoodJsonPath()));
        var director = new SpawnDirector(table, new SeededRandom(11), spawnX: 0f);

        var items = SpawnMany(director, Career.Phases[0], eaten: 60, seconds: 60f);

        Assert.All(items, i => Assert.Equal("", i.Power));
    }

    [Fact]
    public void APhaseWithPowerUpsEnabledEventuallySpawnsEachBuff()
    {
        var table = FoodTable.FromJson(System.IO.File.ReadAllText(FoodJsonPath()));
        var director = new SpawnDirector(table, new SeededRandom(3), spawnX: 0f);

        var items = SpawnMany(director, Career.Phases[2], eaten: 40, seconds: 400f);
        var powers = items.Select(i => i.Power).Where(p => p != "").Distinct().ToList();

        Assert.Contains("slow", powers);
        Assert.Contains("shield", powers);
        Assert.Contains("magnet", powers);
        Assert.Contains("goldtooth", powers);
    }

    [Fact]
    public void TheStrongestBuffsArriveGuardedByBombs()
    {
        var table = FoodTable.FromJson(System.IO.File.ReadAllText(FoodJsonPath()));
        var director = new SpawnDirector(table, new SeededRandom(3), spawnX: 0f);

        var items = SpawnMany(director, Career.Phases[2], eaten: 40, seconds: 400f);

        // A gold tooth is always the middle of bomb / tooth / bomb, and a shield always
        // arrives directly after a bomb. Strength is paid for in what surrounds it.
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Power == "goldtooth")
            {
                Assert.True(i >= 1 && i + 1 < items.Count);
                Assert.False(items[i - 1].IsEdible);
                Assert.False(items[i + 1].IsEdible);
            }

            if (items[i].Power == "shield")
            {
                Assert.True(i >= 1);
                Assert.False(items[i - 1].IsEdible);
            }
        }
    }

    [Fact]
    public void BuffsAreAMinorityOfWhatArrives()
    {
        var table = FoodTable.FromJson(System.IO.File.ReadAllText(FoodJsonPath()));
        var director = new SpawnDirector(table, new SeededRandom(21), spawnX: 0f);

        var items = SpawnMany(director, Career.Phases[2], eaten: 40, seconds: 400f);
        var buffs = items.Count(i => i.Power != "");

        // A belt made mostly of power-ups is not a timing game any more.
        Assert.True(buffs < items.Count / 5, $"{buffs} buffs out of {items.Count} items");
    }

    [Fact]
    public void CoinsCarryTheCoinPowerAndAUniqueId()
    {
        var table = FoodTable.FromJson(System.IO.File.ReadAllText(FoodJsonPath()));
        var director = new SpawnDirector(table, new SeededRandom(5), spawnX: 0f);

        var first = director.MakeCoin(halfWidth: 8f);
        var second = director.MakeCoin(halfWidth: 8f);

        Assert.Equal("coin", first.TypeId);
        Assert.Equal("coin", first.Power);
        Assert.True(first.IsEdible);
        Assert.NotEqual(first.Id, second.Id);
    }

    private static string FoodJsonPath()
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CrocGame.slnx")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return System.IO.Path.Combine(dir!.FullName, "Resources", "food.json");
    }
```

Ensure the file's usings include `System.Collections.Generic`, `System.Linq`, `CrocGame.Core`, `Xunit`.

- [ ] **Step 2: Run it to make sure it fails**

Run: `dotnet test --filter FullyQualifiedName~SpawnDirectorTests`
Expected: FAIL — `Tick` has no three-argument overload and `MakeCoin` does not exist (CS1501 / CS1061).

- [ ] **Step 3: Write the minimal implementation**

Replace the body of `Core/SpawnDirector.cs` from the `Tick` method down to (and including) `ScheduleNext`, keeping `Spacing`, `PickWeighted` and `SelectMovement` as they are, and add the queue field:

```csharp
    /// <summary>
    /// Type ids forced to spawn next, in order. This is how a buff arrives guarded:
    /// the guards and the prize are queued together so they cannot be separated by a
    /// random gap, and the player reads the whole shape as one decision.
    /// </summary>
    private readonly Queue<string> _forced = new();

    /// <summary>Returns an item to place on the belt, or null if it is not time yet.</summary>
    public FoodItem? Tick(float dt, int eaten, PhaseDef phase)
    {
        _secondsUntilNext -= dt;
        if (_secondsUntilNext > 0f) return null;

        var difficulty = Difficulty.ForEaten(eaten);
        ScheduleNext(difficulty);

        if (_forced.Count > 0)
        {
            // A queued spawn is back-to-back with the one before it, so a guarded buff
            // reads as a cluster rather than as three unrelated items.
            _secondsUntilNext = difficulty.SpacingMin * 0.42f;

            var forcedType = _table.ById(_forced.Dequeue());
            if (forcedType is not null) return Make(forcedType, difficulty);
        }

        if (phase.PowerUpsEnabled && _rng.NextFloat() < PowerUpChance)
        {
            var candidates = _table.PowerUps(eaten);
            if (candidates.Count > 0)
            {
                var buff = PickWeighted(candidates);
                return OpenBuffSpawn(buff, difficulty);
            }
        }

        var hazardChance = difficulty.InedibleChance * phase.HazardScale;
        var wantInedible = hazardChance > 0f && _rng.NextFloat() < hazardChance;

        var pool = _table.Available(eaten, edible: !wantInedible);
        if (pool.Count == 0)
        {
            pool = _table.Available(eaten, edible: true);
            if (pool.Count == 0) return null;
        }

        return Make(PickWeighted(pool), difficulty);
    }

    /// <summary>
    /// How often a spawn opportunity becomes a buff instead of food. Low on purpose:
    /// a buff has to feel like something that turned up, not like part of the pace.
    /// </summary>
    private const float PowerUpChance = 0.07f;

    /// <summary>
    /// Places a buff, with the guards its strength has to be paid for. The rule is that
    /// buff strength is inversely proportional to window width and the strongest ones
    /// spawn guarded - so SLOW arrives alone and free, and a GOLD TOOTH has to be taken
    /// out from between two bombs.
    /// </summary>
    private FoodItem OpenBuffSpawn(FoodType buff, Difficulty difficulty)
    {
        switch (buff.Power)
        {
            case "goldtooth":
                _forced.Enqueue(buff.Id);
                _forced.Enqueue("bomb");
                _secondsUntilNext = difficulty.SpacingMin * 0.42f;
                return Make(_table.ById("bomb") ?? buff, difficulty);

            case "magnet":
                _forced.Enqueue(buff.Id);
                _secondsUntilNext = difficulty.SpacingMin * 0.42f;
                return Make(_table.ById("bomb") ?? buff, difficulty);

            case "shield":
                _forced.Enqueue(buff.Id);
                _secondsUntilNext = difficulty.SpacingMin * 0.42f;
                return Make(_table.ById("bomb") ?? buff, difficulty);

            default:
                return Make(buff, difficulty);
        }
    }

    private FoodItem Make(FoodType type, Difficulty difficulty) =>
        new(id: _nextId++,
            typeId: type.Id,
            x: _spawnX,
            halfWidth: type.Width / 2f,
            isEdible: type.Edible,
            score: type.Score,
            movement: SelectMovement(type, difficulty),
            power: type.Power);

    /// <summary>
    /// A cash-out coin. Its value is decided by the pot rather than by the food table,
    /// so it is built here only to keep item ids unique across the whole belt.
    /// </summary>
    public FoodItem MakeCoin(float halfWidth) =>
        new(id: _nextId++,
            typeId: "coin",
            x: _spawnX,
            halfWidth: halfWidth,
            isEdible: true,
            score: 0,
            movement: Movement.Constant,
            power: "coin");
```

Delete the old two-argument `Tick`. Keep `ScheduleNext`, `Spacing`, `PickWeighted`, `SelectMovement` unchanged. Add `using System.Collections.Generic;` if absent.

Then update every existing call in `Tests/SpawnDirectorTests.cs` from `Tick(dt, eaten)` to `Tick(dt, eaten, Career.Phases[1])`.

- [ ] **Step 4: Run the tests and make sure they pass**

Run: `dotnet test`
Expected: PASS — 163 total, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Core/SpawnDirector.cs Tests/SpawnDirectorTests.cs
git commit -m "Make spawning phase-aware: hazard scale, guarded buffs, coins"
```

---

### Task 7: New events

**Files:**
- Modify: `Core/GameEvent.cs`
- Test: none of its own — these are data carriers, exercised by Tasks 8–10.

**Interfaces:**
- Consumes: `BuffKind` (Task 4), `MatchResult` (existing).
- Produces: `PhaseStarted`, `PhaseEnded`, `PhaseKnockout`, `CoinSpawned`, `PotBanked`, `PotWiped`, `BuffTaken`, `BuffExpired`, `HungerCharged`, `HungerStarted`, `HungerEnded`, `BoutEnded`, and `enum BoutResult`. `MatchEnded` is removed.

- [ ] **Step 1: Add the events**

Append to `Core/GameEvent.cs`:

```csharp
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
```

Delete the `MatchEnded` record from the same file.

- [ ] **Step 2: Run the build to see what breaks**

Run: `dotnet build`
Expected: FAIL — `Core/MatchSession.cs`, `Core/Career.cs`, `Scripts/GameRoot.cs` and `Tests/MatchSessionTests.cs` reference `MatchEnded`. That breakage is the map for Tasks 8–11 and is expected here.

- [ ] **Step 3: Commit the events alone**

The tree does not build at this commit, which is acceptable for a pure data-definition step immediately followed by its consumers.

```bash
git add Core/GameEvent.cs
git commit -m "Add phase, pot, buff, hunger and bout events"
```

---

### Task 8: `PhaseSession` — one phase of a bout

**Files:**
- Rename: `Core/MatchSession.cs` → `Core/PhaseSession.cs`
- Rename: `Tests/MatchSessionTests.cs` → `Tests/PhaseSessionTests.cs`
- Modify: `Core/MatchState.cs`

**Interfaces:**
- Consumes: `PhaseDef` (1), `Pot` (2), `Hunger` (3), `ActiveBuffs` (4), phase-aware `SpawnDirector` (6), events (7).
- Produces: `PhaseSession` with constructor `(FoodTable table, IRandomSource rng, JawZone jaw, float spawnX, float retireX, PhaseDef phase, int difficultyOffset)`; properties `MatchState State`, `Frenzy Frenzy`, `Pot Pot`, `Hunger Hunger`, `ActiveBuffs Buffs`, `PhaseDef Phase`, `IReadOnlyList<FoodItem> Items`, `float BeltSpeed`, `JawZone EffectiveJaw`, `int PhaseScore`, `bool KnockedOut`; methods `IReadOnlyList<GameEvent> Tick(float dt, int carriedPlayerScore, int opponentScore)` and `IReadOnlyList<GameEvent> Chomp(int carriedPlayerScore)`.
- `MatchState` loses `Settle` and `MatchResult`; `MaxStrikes` and the knockout flag stay.

- [ ] **Step 1: Write the failing test**

Rename the file, then replace its contents with `Tests/PhaseSessionTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class PhaseSessionTests
{
    private static FoodTable Table() => FoodTable.FromJson(
        """
        [
          { "id":"pizza","width":16,"edible":true,"movement":"constant","score":10,"minEatenToAppear":0 },
          { "id":"bomb","width":16,"edible":false,"movement":"constant","score":0,"minEatenToAppear":0 },
          { "id":"slow","width":16,"edible":true,"movement":"constant","score":0,"minEatenToAppear":0,"power":"slow" },
          { "id":"shield","width":14,"edible":true,"movement":"constant","score":0,"minEatenToAppear":0,"power":"shield" }
        ]
        """);

    private static PhaseSession Make(PhaseDef? phase = null) =>
        new(Table(), new SeededRandom(1), new JawZone(90f, 17f),
            spawnX: -20f, retireX: 200f,
            phase: phase ?? Career.Phases[1], difficultyOffset: 0);

    /// <summary>Puts an item exactly in the jaws so a chomp is guaranteed to land.</summary>
    private static FoodItem PlaceInJaws(PhaseSession session, string typeId, bool edible,
                                        int score, string power = "")
    {
        var item = new FoodItem(999, typeId, 90f, 8f, edible, score, Movement.Constant, power);
        session.Place(item);
        return item;
    }

    [Fact]
    public void AThirdStrikeKnocksThePlayerOutOfThePhaseAndConcedesTheRest()
    {
        var session = Make();
        var events = new List<GameEvent>();

        for (var i = 0; i < 3; i++) events.AddRange(session.Chomp(0));

        Assert.True(session.KnockedOut);

        var knockout = events.OfType<PhaseKnockout>().Single();
        Assert.True(knockout.SecondsConceded > 0f);
    }

    [Fact]
    public void AKnockedOutPhaseStopsScoringButKeepsTicking()
    {
        var session = Make();
        for (var i = 0; i < 3; i++) session.Chomp(0);

        var before = session.PhaseScore;
        PlaceInJaws(session, "pizza", edible: true, score: 10);
        session.Chomp(0);

        Assert.Equal(before, session.PhaseScore);
        Assert.Empty(session.Items);
    }

    [Fact]
    public void AShieldAbsorbsAStrikeInsteadOfTheTeeth()
    {
        var session = Make();
        PlaceInJaws(session, "shield", edible: true, score: 0, power: "shield");
        session.Chomp(0);

        Assert.True(session.Buffs.HasShield);

        session.Chomp(0);   // air: nothing else is on the belt

        Assert.Equal(0, session.State.Strikes);
        Assert.False(session.Buffs.HasShield);
    }

    [Fact]
    public void APhaseMultiplierAppliesToBites()
    {
        var plain = Make(Career.Phases[1]);
        PlaceInJaws(plain, "pizza", edible: true, score: 10);
        plain.Chomp(0);

        var feast = Make(Career.Phases[2]);
        PlaceInJaws(feast, "pizza", edible: true, score: 10);
        feast.Chomp(0);

        Assert.Equal(plain.PhaseScore * 2, feast.PhaseScore);
    }

    [Fact]
    public void BitesAccrueToThePotOnlyWhereCoinsAreLive()
    {
        var hazard = Make(Career.Phases[1]);
        PlaceInJaws(hazard, "pizza", edible: true, score: 10);
        hazard.Chomp(0);
        Assert.False(hazard.Pot.IsEmpty);

        var plain = Make(Career.Phases[0]);
        PlaceInJaws(plain, "pizza", edible: true, score: 10);
        plain.Chomp(0);
        Assert.True(plain.Pot.IsEmpty);
    }

    [Fact]
    public void BitingACoinBanksThePotAndNeverReducesTheScore()
    {
        var session = Make();
        PlaceInJaws(session, "pizza", edible: true, score: 10);
        session.Chomp(0);

        var scoreBeforeCoin = session.PhaseScore;
        var pot = session.Pot.Amount;
        Assert.True(pot > 0);

        PlaceInJaws(session, "coin", edible: true, score: 0, power: "coin");
        var events = session.Chomp(0);

        Assert.True(session.Pot.IsEmpty);
        Assert.True(session.PhaseScore > scoreBeforeCoin);
        Assert.Single(events.OfType<PotBanked>());
    }

    [Fact]
    public void AStrikeWipesThePot()
    {
        var session = Make();
        PlaceInJaws(session, "pizza", edible: true, score: 10);
        session.Chomp(0);
        Assert.False(session.Pot.IsEmpty);

        var events = session.Chomp(0);   // air

        Assert.True(session.Pot.IsEmpty);
        Assert.Single(events.OfType<PotWiped>());
    }

    [Fact]
    public void HungerWidensTheJawZoneItReportsToTheView()
    {
        var session = Make();
        var narrow = session.EffectiveJaw.HalfWidth;

        for (var t = 0f; t < Hunger.ChargeSeconds + 2f; t += 0.05f)
        {
            session.Tick(0.05f, carriedPlayerScore: 0, opponentScore: 400);
        }

        Assert.True(session.Hunger.IsActive);
        Assert.Equal(narrow * Hunger.JawWidthMultiplier, session.EffectiveJaw.HalfWidth, precision: 3);
    }

    [Fact]
    public void HungerMakesEverythingOnTheBeltEdible()
    {
        var session = Make();
        var bomb = PlaceInJaws(session, "bomb", edible: false, score: 0);

        for (var t = 0f; t < Hunger.ChargeSeconds + 2f; t += 0.05f)
        {
            session.Tick(0.05f, carriedPlayerScore: 0, opponentScore: 400);
        }

        Assert.True(session.Hunger.IsActive);
        Assert.True(session.IsEdibleNow(bomb));
    }
}
```

- [ ] **Step 2: Run it to make sure it fails**

Run: `dotnet test --filter FullyQualifiedName~PhaseSessionTests`
Expected: FAIL — `PhaseSession`, `Place`, `IsEdibleNow`, `PhaseScore`, `KnockedOut`, `EffectiveJaw` do not exist.

- [ ] **Step 3: Write the implementation**

`git mv Core/MatchSession.cs Core/PhaseSession.cs`, then replace its contents:

```csharp
using System;
using System.Collections.Generic;

namespace CrocGame.Core;

/// <summary>
/// One act of a bout: a belt, a clock, three teeth, and everything the player is
/// carrying while they last.
///
/// A phase does not know it is part of a bout. It is handed the carried scoreline each
/// frame - which is all Hunger needs to know how far behind the player is - and reports
/// what happened. BoutSession does the joining up.
/// </summary>
public sealed class PhaseSession
{
    private readonly Belt _belt;
    private readonly SpawnDirector _director;
    private readonly JawZone _jaw;
    private readonly PhaseDef _phase;
    private readonly int _difficultyOffset;
    private readonly List<GameEvent> _events = new();

    private float _grace;
    private float _secondsUntilCoin;

    public PhaseSession(FoodTable table, IRandomSource rng, JawZone jaw,
                        float spawnX, float retireX, PhaseDef phase, int difficultyOffset)
    {
        _jaw = jaw;
        _phase = phase;
        _difficultyOffset = difficultyOffset;
        _belt = new Belt(retireX);
        _director = new SpawnDirector(table, rng, spawnX);

        State = new MatchState(phase.DurationSeconds);
        Frenzy = new Frenzy();
        Pot = new Pot();
        Hunger = new Hunger();
        Buffs = new ActiveBuffs();

        _secondsUntilCoin = phase.CoinIntervalSeconds;
    }

    public MatchState State { get; }
    public Frenzy Frenzy { get; }
    public Pot Pot { get; }
    public Hunger Hunger { get; }
    public ActiveBuffs Buffs { get; }
    public PhaseDef Phase => _phase;
    public IReadOnlyList<FoodItem> Items => _belt.Items;

    /// <summary>Points scored in this phase alone.</summary>
    public int PhaseScore => State.Score;

    public bool KnockedOut { get; private set; }

    /// <summary>
    /// A bite that lands buys this long of forgiveness, so a double-tap on one item is
    /// not charged as a strike.
    /// </summary>
    public const float ChompGraceSeconds = 0.18f;

    /// <summary>Belt speed right now, including every multiplier acting on it.</summary>
    public float BeltSpeed =>
        Difficulty.ForEaten(State.Eaten + _difficultyOffset).BeltSpeed
        * Frenzy.SpeedMultiplier * Buffs.SpeedMultiplier * Hunger.SpeedMultiplier;

    /// <summary>
    /// The window as it is actually judged - and the width the view is required to draw.
    /// Nothing may widen this without the player seeing it widen.
    /// </summary>
    public JawZone EffectiveJaw => _jaw with { HalfWidth = _jaw.HalfWidth * Hunger.JawMultiplier };

    /// <summary>During Hunger the croc eats anything, bombs included.</summary>
    public bool IsEdibleNow(FoodItem item) => item.IsEdible || Hunger.IsActive;

    /// <summary>Puts an item straight on the belt. Used by tests and by coin spawning.</summary>
    public void Place(FoodItem item) => _belt.Add(item);

    public IReadOnlyList<GameEvent> Tick(float dt, int carriedPlayerScore, int opponentScore)
    {
        _events.Clear();
        if (State.IsOver) return _events;

        if (_grace > 0f) _grace = MathF.Max(0f, _grace - dt);

        if (Frenzy.Tick(dt)) _events.Add(new FrenzyEnded());
        if (Hunger.Tick(dt)) _events.Add(new HungerEnded());

        foreach (var kind in Buffs.Tick(dt)) _events.Add(new BuffExpired(kind));

        Hunger.Update(dt, carriedPlayerScore + State.Score, opponentScore);
        if (Hunger.TryFire())
        {
            _events.Add(new HungerStarted(Hunger.JawMultiplier, Hunger.DurationSeconds));
        }

        if (!KnockedOut)
        {
            AdvanceBelt(dt);
            SpawnCoin(dt);

            var spawned = _director.Tick(dt, State.Eaten + _difficultyOffset, _phase);
            if (spawned is not null)
            {
                _belt.Add(spawned);
                _events.Add(new Spawned(spawned));
            }
        }

        // The clock is settled last so the final frame's bites still count.
        if (State.AdvanceClock(dt)) State.Finish();

        return _events;
    }

    private void AdvanceBelt(float dt)
    {
        foreach (var retired in _belt.Advance(BeltSpeed, dt))
        {
            // Correct play: hazards are supposed to ride past, and a coin declined is
            // the whole point of the coin.
            if (!retired.IsEdible || retired.Power != "") continue;

            _events.Add(new Passed(retired));
            State.BreakCombo();
            Frenzy.Reset();
        }
    }

    private void SpawnCoin(float dt)
    {
        if (_phase.CoinIntervalSeconds <= 0f) return;

        _secondsUntilCoin -= dt;
        if (_secondsUntilCoin > 0f) return;

        _secondsUntilCoin = _phase.CoinIntervalSeconds;

        // No pot, no wager, no coin. A coin worth nothing is a strike waiting to happen.
        if (Pot.IsEmpty) return;

        var coin = _director.MakeCoin(halfWidth: 8f);
        _belt.Add(coin);
        _events.Add(new CoinSpawned(coin, Pot.PendingAt(State.Combo)));
    }

    public IReadOnlyList<GameEvent> Chomp(int carriedPlayerScore)
    {
        _events.Clear();
        if (State.IsOver || KnockedOut) return _events;

        var result = ChompJudge.Judge(EffectiveJaw, _belt.Items);

        // The magnet suspends judging for three bites: it takes whatever is nearest.
        if (result.Outcome == ChompOutcome.Air && Buffs.MagnetBitesRemaining > 0
            && _belt.Items.Count > 0 && Buffs.ConsumeMagnetBite())
        {
            result = new ChompResult(ChompOutcome.Hit, Nearest());
        }

        if (result.Outcome == ChompOutcome.Air)
        {
            if (_grace > 0f) return _events;

            _events.Add(new ChompedAir());
            AddStrike();
            return _events;
        }

        var item = result.Item!;
        _belt.Remove(item);

        if (item.Power == "coin")
        {
            BankPot();
            return _events;
        }

        if (!IsEdibleNow(item))
        {
            _events.Add(new Chomped(item, 0, 0, false));
            AddStrike();
            return _events;
        }

        _grace = ChompGraceSeconds;

        var buff = PowerUp.Parse(item.Power);
        if (buff is not null)
        {
            Buffs.Take(buff.Value);
            _events.Add(new BuffTaken(buff.Value));
            return _events;
        }

        ScoreBite(item);
        return _events;
    }

    private FoodItem Nearest()
    {
        var best = _belt.Items[0];
        var bestDistance = float.MaxValue;

        foreach (var item in _belt.Items)
        {
            var distance = MathF.Abs(item.X - _jaw.Center);
            if (distance >= bestDistance) continue;

            best = item;
            bestDistance = distance;
        }

        return best;
    }

    private void ScoreBite(FoodItem item)
    {
        var wasFrenzied = Frenzy.IsActive;
        var multiplier = Frenzy.Multiplier * Buffs.ScoreMultiplier * _phase.ScoreMultiplier;
        var points = State.RegisterHit(item.Score, multiplier);

        // The pot is upside stacked on the score, never a slice taken out of it.
        if (_phase.CoinIntervalSeconds > 0f) Pot.Add(points);

        _events.Add(new Chomped(item, State.Combo, points, wasFrenzied));

        if (!wasFrenzied && State.Combo >= Frenzy.ComboToTrigger)
        {
            Frenzy.Trigger();
            _events.Add(new FrenzyStarted());
        }
        else if (wasFrenzied)
        {
            Frenzy.Trigger();
        }
    }

    private void BankPot()
    {
        var amount = Pot.Amount;
        var multiplier = Pot.MultiplierForCombo(State.Combo);
        var paid = Pot.Bank(State.Combo);

        State.AddScore(paid);
        _events.Add(new PotBanked(amount, multiplier, paid));
    }

    private void AddStrike()
    {
        Hunger.OnStrike();

        if (Buffs.ConsumeShield())
        {
            _events.Add(new BuffExpired(BuffKind.Shield));
            return;
        }

        if (!Pot.IsEmpty)
        {
            _events.Add(new PotWiped(Pot.PendingAt(State.Combo)));
            Pot.Wipe();
        }

        State.RegisterStrike();
        Frenzy.Reset();
        _events.Add(new StrikeAdded(State.Strikes));

        if (State.Strikes < MatchState.MaxStrikes) return;

        KnockedOut = true;
        _belt.Clear();
        _events.Add(new PhaseKnockout(0, State.TimeRemaining));
    }
}
```

Add to `Core/Belt.cs`:

```csharp
    /// <summary>Clears the belt. A knockout stops the phase dead.</summary>
    public void Clear() => _items.Clear();
```

In `Core/MatchState.cs`: delete `Settle` and the `MatchResult` enum, and add:

```csharp
    /// <summary>True once the clock has run out.</summary>
    public bool IsOver { get; private set; }

    /// <summary>Marks the phase finished at the bell.</summary>
    public void Finish() => IsOver = true;

    /// <summary>Adds points that did not come from a bite, such as a banked pot.</summary>
    public void AddScore(int points) => Score += points;
```

Replace the existing `Result` property and `IsOver => Result != MatchResult.InProgress` with the above, and change `RegisterStrike` so it no longer sets a result — it only increments and resets the combo. Make `Score` settable privately (`public int Score { get; private set; }` already is).

- [ ] **Step 4: Run the tests and make sure they pass**

Run: `dotnet test`
Expected: PASS. `Tests/MatchSessionTests.cs` no longer exists; total is 172, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add -A Core Tests
git commit -m "Narrow MatchSession to PhaseSession: one phase, pot, buffs, hunger"
```

---

### Task 9: `BoutSession` — the three-phase façade

**Files:**
- Create: `Core/BoutSession.cs`
- Test: `Tests/BoutSessionTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1–8.
- Produces: `BoutSession` with constructor `(FoodTable table, IRandomSource rng, JawZone jaw, float spawnX, float retireX, MatchDef def, IReadOnlyList<PhaseDef> phases)`; properties `int PhaseIndex`, `PhaseDef Phase`, `PhaseSession Current`, `int PlayerScore`, `int OpponentScore`, `int BestCombo`, `int Eaten`, `BoutResult Result`, `bool AwaitingInterlude`, `MatchDef Def`; methods `IReadOnlyList<GameEvent> Start()`, `IReadOnlyList<GameEvent> Tick(float dt)`, `IReadOnlyList<GameEvent> Chomp()`, `IReadOnlyList<GameEvent> BeginNextPhase()`.

- [ ] **Step 1: Write the failing test**

`Tests/BoutSessionTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class BoutSessionTests
{
    private static FoodTable Table() => FoodTable.FromJson(
        """
        [
          { "id":"pizza","width":16,"edible":true,"movement":"constant","score":10,"minEatenToAppear":0 },
          { "id":"bomb","width":16,"edible":false,"movement":"constant","score":0,"minEatenToAppear":0 }
        ]
        """);

    private static MatchDef Def() =>
        new(Career.Ladder[0], DurationSeconds: 0f, DifficultyOffset: 0);

    private static BoutSession Make(int seed = 1) =>
        new(Table(), new SeededRandom(seed), new JawZone(90f, 17f),
            spawnX: -20f, retireX: 200f, def: Def(), phases: Career.Phases);

    /// <summary>Runs a whole bout, advancing through each interlude the moment it opens.</summary>
    private static List<GameEvent> RunToTheBell(BoutSession bout)
    {
        var events = new List<GameEvent>(bout.Start());

        for (var i = 0; i < 20000 && bout.Result == BoutResult.InProgress; i++)
        {
            if (bout.AwaitingInterlude) events.AddRange(bout.BeginNextPhase());
            else events.AddRange(bout.Tick(0.02f));
        }

        return events;
    }

    [Fact]
    public void ABoutRunsExactlyThreePhases()
    {
        var bout = Make();
        var events = RunToTheBell(bout);

        Assert.Equal(3, events.OfType<PhaseStarted>().Count());
        Assert.Equal(3, events.OfType<PhaseEnded>().Count());
        Assert.Equal(new[] { 0, 1, 2 }, events.OfType<PhaseStarted>().Select(e => e.PhaseIndex));
    }

    [Fact]
    public void ABoutEndsOnceAndOnlyAtTheBell()
    {
        var bout = Make();
        var events = RunToTheBell(bout);

        Assert.Single(events.OfType<BoutEnded>());
        Assert.NotEqual(BoutResult.InProgress, bout.Result);
    }

    [Fact]
    public void ScoreCarriesAcrossPhases()
    {
        var bout = Make();
        bout.Start();

        // Bite an item placed straight in the jaws during phase one.
        bout.Current.Place(new FoodItem(500, "pizza", 90f, 8f, true, 10, Movement.Constant));
        bout.Chomp();

        var afterPhaseOne = bout.PlayerScore;
        Assert.True(afterPhaseOne > 0);

        while (!bout.AwaitingInterlude) bout.Tick(0.02f);
        bout.BeginNextPhase();

        Assert.Equal(afterPhaseOne, bout.PlayerScore);
        Assert.Equal(0, bout.Current.PhaseScore);
    }

    [Fact]
    public void StrikesResetAtEachPhaseBoundary()
    {
        var bout = Make();
        bout.Start();

        bout.Chomp();
        bout.Chomp();
        Assert.Equal(2, bout.Current.State.Strikes);

        while (!bout.AwaitingInterlude) bout.Tick(0.02f);
        bout.BeginNextPhase();

        Assert.Equal(0, bout.Current.State.Strikes);
    }

    [Fact]
    public void AKnockoutEndsThePhaseNotTheBout()
    {
        var bout = Make();
        bout.Start();

        for (var i = 0; i < 3; i++) bout.Chomp();

        Assert.True(bout.Current.KnockedOut);
        Assert.Equal(BoutResult.InProgress, bout.Result);

        while (!bout.AwaitingInterlude) bout.Tick(0.02f);
        bout.BeginNextPhase();

        Assert.Equal(1, bout.PhaseIndex);
        Assert.False(bout.Current.KnockedOut);
    }

    [Fact]
    public void AKnockedOutPhaseStillAdvancesTheRival()
    {
        var bout = Make();
        bout.Start();
        for (var i = 0; i < 3; i++) bout.Chomp();

        var before = bout.OpponentScore;
        while (!bout.AwaitingInterlude) bout.Tick(0.02f);

        Assert.True(bout.OpponentScore > before);
    }

    [Fact]
    public void ThePotDoesNotSurviveAPhaseBoundary()
    {
        var bout = Make();
        bout.Start();
        while (!bout.AwaitingInterlude) bout.Tick(0.02f);
        bout.BeginNextPhase();   // now in HAZARD, where the pot is live

        bout.Current.Place(new FoodItem(501, "pizza", 90f, 8f, true, 10, Movement.Constant));
        bout.Chomp();
        Assert.False(bout.Current.Pot.IsEmpty);

        while (!bout.AwaitingInterlude) bout.Tick(0.02f);
        bout.BeginNextPhase();

        Assert.True(bout.Current.Pot.IsEmpty);
    }

    [Fact]
    public void TheBoutIsDecidedOnCarriedTotalScore()
    {
        var bout = Make();
        RunToTheBell(bout);

        var expected = bout.PlayerScore > bout.OpponentScore ? BoutResult.Won : BoutResult.Lost;
        Assert.Equal(expected, bout.Result);
    }

    [Fact]
    public void ASeedReproducesAWholeBoutIdentically()
    {
        var first = RunToTheBell(Make(seed: 4242));
        var second = RunToTheBell(Make(seed: 4242));

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first.Select(e => e.GetType().Name), second.Select(e => e.GetType().Name));
    }
}
```

- [ ] **Step 2: Run it to make sure it fails**

Run: `dotnet test --filter FullyQualifiedName~BoutSessionTests`
Expected: FAIL — `BoutSession` does not exist (CS0246).

- [ ] **Step 3: Write the implementation**

`Core/BoutSession.cs`:

```csharp
using System.Collections.Generic;

namespace CrocGame.Core;

/// <summary>
/// A whole bout: three phases, a rival who eats through all of them, and one carried
/// scoreline. This is the only surface the scene layer talks to.
///
/// The rival lives here rather than in a phase because their score has to survive a
/// phase boundary and has to keep climbing through a knockout - conceding seconds is
/// what makes a third strike hurt now that it can no longer end the run.
/// </summary>
public sealed class BoutSession
{
    private readonly FoodTable _table;
    private readonly IRandomSource _rng;
    private readonly JawZone _jaw;
    private readonly float _spawnX;
    private readonly float _retireX;
    private readonly MatchDef _def;
    private readonly IReadOnlyList<PhaseDef> _phases;
    private readonly OpponentEater _opponent;
    private readonly List<GameEvent> _events = new();

    private PhaseSession _current = null!;
    private int _carriedScore;
    private int _carriedEaten;

    public BoutSession(FoodTable table, IRandomSource rng, JawZone jaw,
                       float spawnX, float retireX, MatchDef def, IReadOnlyList<PhaseDef> phases)
    {
        _table = table;
        _rng = rng;
        _jaw = jaw;
        _spawnX = spawnX;
        _retireX = retireX;
        _def = def;
        _phases = phases;
        _opponent = new OpponentEater(def.Opponent, rng);
    }

    public int PhaseIndex { get; private set; }
    public PhaseDef Phase => _phases[PhaseIndex];
    public PhaseSession Current => _current;
    public MatchDef Def => _def;
    public BoutResult Result { get; private set; } = BoutResult.InProgress;
    public int OpponentScore => _opponent.Score;
    public int BestCombo { get; private set; }

    /// <summary>Carried total, including whatever the live phase has scored so far.</summary>
    public int PlayerScore => _carriedScore + (_current?.PhaseScore ?? 0);

    public int Eaten => _carriedEaten + (_current?.State.Eaten ?? 0);

    /// <summary>True when a phase has finished and the interlude is owed. The scene
    /// layer plays its dialogue and then calls BeginNextPhase.</summary>
    public bool AwaitingInterlude { get; private set; }

    public IReadOnlyList<GameEvent> Start()
    {
        _events.Clear();
        PhaseIndex = 0;
        OpenPhase();
        return _events;
    }

    public IReadOnlyList<GameEvent> Tick(float dt)
    {
        _events.Clear();
        if (Result != BoutResult.InProgress || AwaitingInterlude) return _events;

        // The rival eats through everything, knockouts included.
        if (_opponent.Tick(dt)) _events.Add(new OpponentAte(_opponent.Score));

        _events.AddRange(_current.Tick(dt, _carriedScore, _opponent.Score));

        if (!_current.State.IsOver) return _events;

        ClosePhase();
        return _events;
    }

    public IReadOnlyList<GameEvent> Chomp()
    {
        _events.Clear();
        if (Result != BoutResult.InProgress || AwaitingInterlude) return _events;

        _events.AddRange(_current.Chomp(_carriedScore));
        return _events;
    }

    /// <summary>Called once the interlude's dialogue has been read.</summary>
    public IReadOnlyList<GameEvent> BeginNextPhase()
    {
        _events.Clear();
        if (!AwaitingInterlude) return _events;

        AwaitingInterlude = false;
        PhaseIndex++;
        OpenPhase();
        return _events;
    }

    private void OpenPhase()
    {
        _current = new PhaseSession(
            _table, _rng, _jaw, _spawnX, _retireX,
            _phases[PhaseIndex], _def.DifficultyOffset + _phases[PhaseIndex].DifficultyOffset);

        _events.Add(new PhaseStarted(PhaseIndex, _phases[PhaseIndex]));
    }

    private void ClosePhase()
    {
        _carriedScore += _current.PhaseScore;
        _carriedEaten += _current.State.Eaten;

        if (_current.State.BestCombo > BestCombo) BestCombo = _current.State.BestCombo;

        _events.Add(new PhaseEnded(PhaseIndex, _current.KnockedOut, PlayerScore, _opponent.Score));

        if (PhaseIndex + 1 < _phases.Count)
        {
            AwaitingInterlude = true;
            return;
        }

        Result = _carriedScore > _opponent.Score ? BoutResult.Won : BoutResult.Lost;

        _events.Add(new BoutEnded(
            Result,
            _carriedScore,
            _opponent.Score,
            Prize: Result == BoutResult.Won ? _def.Opponent.PrizeMoney : 0,
            BestCombo: BestCombo,
            Eaten: _carriedEaten));
    }
}
```

Note: `PlayerScore` reads `_current.PhaseScore` while a phase is live and `_carriedScore` after `ClosePhase` has folded it in. `ClosePhase` sets `_carriedScore` **before** building `PhaseEnded`, and `_current` is not replaced until `OpenPhase`, so `PlayerScore` would double-count for the moment between. Guard it by clearing the live phase's contribution:

```csharp
    private bool _phaseFolded;

    public int PlayerScore => _carriedScore + (_phaseFolded || _current is null ? 0 : _current.PhaseScore);
```

Set `_phaseFolded = true` at the top of `ClosePhase` and `_phaseFolded = false` at the end of `OpenPhase`. Apply the same guard to `Eaten`.

- [ ] **Step 4: Run the tests and make sure they pass**

Run: `dotnet test`
Expected: PASS — 181 total, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add Core/BoutSession.cs Tests/BoutSessionTests.cs
git commit -m "Add BoutSession: three phases, one carried scoreline, one rival"
```

---

### Task 10: Career takes a bout result, and rivals get interlude lines

**Files:**
- Modify: `Core/Career.cs`, `Core/Opponent.cs`
- Test: `Tests/CareerTests.cs`

**Interfaces:**
- Consumes: `BoutEnded`, `BoutResult` (Task 7).
- Produces: `Career.RecordWin(SaveData, BoutEnded)` and `Career.RecordLoss(SaveData, BoutEnded)`; `OpponentDef` gains `string Interlude1Ahead`, `Interlude1Behind`, `Interlude2Ahead`, `Interlude2Behind` (all defaulting to `""`); `Career.InterludeLine(OpponentDef def, int phaseIndex, bool rivalAhead)`.

- [ ] **Step 1: Write the failing test**

Append to `Tests/CareerTests.cs`:

```csharp
    [Fact]
    public void EveryRivalHasAllFourInterludeLines()
    {
        Assert.All(Career.Ladder, def =>
        {
            Assert.NotEqual("", def.Interlude1Ahead);
            Assert.NotEqual("", def.Interlude1Behind);
            Assert.NotEqual("", def.Interlude2Ahead);
            Assert.NotEqual("", def.Interlude2Behind);
        });
    }

    [Fact]
    public void TheInterludeLinePicksOnPhaseAndWhoIsAhead()
    {
        var def = Career.Ladder[0];

        Assert.Equal(def.Interlude1Ahead, Career.InterludeLine(def, phaseIndex: 0, rivalAhead: true));
        Assert.Equal(def.Interlude1Behind, Career.InterludeLine(def, phaseIndex: 0, rivalAhead: false));
        Assert.Equal(def.Interlude2Ahead, Career.InterludeLine(def, phaseIndex: 1, rivalAhead: true));
        Assert.Equal(def.Interlude2Behind, Career.InterludeLine(def, phaseIndex: 1, rivalAhead: false));
    }

    [Fact]
    public void RecordingAWinPaysThePrizeAndAdvancesTheLadder()
    {
        var save = new SaveData();
        var ended = new BoutEnded(BoutResult.Won, PlayerScore: 400, OpponentScore: 300,
                                  Prize: 25, BestCombo: 9, Eaten: 30);

        Career.RecordWin(save, ended);

        Assert.Equal(1, Career.Progress(save));
        Assert.Equal(25, save.Money);
        Assert.Equal(400, save.BestScore);
        Assert.Equal(30, save.LifetimeEaten);
    }

    [Fact]
    public void RecordingALossKeepsProgressAndPaysNothing()
    {
        var save = new SaveData();
        var ended = new BoutEnded(BoutResult.Lost, PlayerScore: 120, OpponentScore: 300,
                                  Prize: 0, BestCombo: 3, Eaten: 12);

        Career.RecordLoss(save, ended);

        Assert.Equal(0, Career.Progress(save));
        Assert.Equal(0, save.Money);
        Assert.Equal(120, save.BestScore);
    }
```

- [ ] **Step 2: Run it to make sure it fails**

Run: `dotnet test --filter FullyQualifiedName~CareerTests`
Expected: FAIL — `Interlude1Ahead` and `Career.InterludeLine` do not exist; `RecordWin` still takes `MatchEnded` (CS1061 / CS1503).

- [ ] **Step 3: Write the implementation**

In `Core/Opponent.cs`, extend `OpponentDef` with four trailing parameters:

```csharp
public sealed record OpponentDef(
    string Id,
    string Name,
    string SpriteId,
    float SecondsPerBite,
    float BiteJitter,
    int PointsPerBite,
    int PrizeMoney,
    string Taunt,
    string LineLosing = "",
    string LineWinning = "",
    string LinePanic = "",

    /// <summary>What they say between PLAIN and HAZARD, by who is ahead.</summary>
    string Interlude1Ahead = "",
    string Interlude1Behind = "",

    /// <summary>What they say between HAZARD and FEAST, by who is ahead.</summary>
    string Interlude2Ahead = "",
    string Interlude2Behind = "");
```

In `Core/Career.cs`, fill them in on each rung:

```csharp
        new OpponentDef("penguin", "PIP", "penguin",
            SecondsPerBite: 1.70f, BiteJitter: 0.25f, PointsPerBite: 34,
            PrizeMoney: 25, Taunt: "you look hungry, pal",
            LineLosing: "hey! slow down!", LineWinning: "too easy", LinePanic: "what IS this",
            Interlude1Ahead: "round one to me. try harder",
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
```

Add the selector:

```csharp
    /// <summary>The rival's line for the interlude after the given phase.</summary>
    public static string InterludeLine(OpponentDef def, int phaseIndex, bool rivalAhead) =>
        phaseIndex == 0
            ? rivalAhead ? def.Interlude1Ahead : def.Interlude1Behind
            : rivalAhead ? def.Interlude2Ahead : def.Interlude2Behind;
```

Change the two record methods to take `BoutEnded`:

```csharp
    public static void RecordWin(SaveData data, BoutEnded ended)
    public static void RecordLoss(SaveData data, BoutEnded ended)
```

Their bodies are otherwise unchanged.

- [ ] **Step 4: Run the tests and make sure they pass**

Run: `dotnet test`
Expected: PASS — 185 total, 0 failed. `Core` and `Tests` build clean; `CrocGame.csproj` (the Godot layer) is still broken, which Task 12 fixes.

- [ ] **Step 5: Commit**

```bash
git add Core/Career.cs Core/Opponent.cs Tests/CareerTests.cs
git commit -m "Career records a bout result; rivals get interlude lines"
```

---

### Task 11: `Ui.Column` — boxes sized and centred around their text

**Files:**
- Modify: `Scripts/Ui.cs`
- Verify: `Scripts/ScreenOverlay.cs`, `Scripts/DialogueScene.cs`

**Interfaces:**
- Consumes: `Ui.Measure`, `Ui.WrappedLabel` (existing).
- Produces: `Ui.ColumnHeight(IReadOnlyList<Label> labels, float gap)` and `Ui.LayoutColumn(IReadOnlyList<Label> labels, Rect2 box, float gap)`.

This task and Tasks 12–15 are scene-layer work with no automated tests. Verification is the screenshot harness described in `docs/running-the-game.md`.

- [ ] **Step 1: Add the layout helper**

Append to `Scripts/Ui.cs`:

```csharp
    /// <summary>
    /// Total height a stack of labels occupies, measured rather than assumed.
    ///
    /// The defect this exists to kill: boxes were a fixed size with text at fixed
    /// offsets inside them, so a one-line subtitle floated in dead space and a
    /// three-line one ran off the bottom. Nothing was centred; it was positioned to
    /// look centred for one particular string.
    /// </summary>
    public static float ColumnHeight(IReadOnlyList<Label> labels, float gap)
    {
        var total = 0f;

        for (var i = 0; i < labels.Count; i++)
        {
            if (!labels[i].Visible) continue;

            total += WrappedHeight(labels[i]);
            if (i < labels.Count - 1) total += gap;
        }

        return total;
    }

    /// <summary>
    /// Stacks the labels as one block, centred vertically in the box and each label
    /// spanning its full width so its own HorizontalAlignment centres it horizontally.
    /// Returns the block's height.
    /// </summary>
    public static float LayoutColumn(IReadOnlyList<Label> labels, Rect2 box, float gap)
    {
        var height = ColumnHeight(labels, gap);
        var y = box.Position.Y + Mathf.Round((box.Size.Y - height) / 2f);

        foreach (var label in labels)
        {
            if (!label.Visible) continue;

            label.Size = new Vector2(box.Size.X, WrappedHeight(label));
            label.Position = new Vector2(box.Position.X, y);
            y += WrappedHeight(label) + gap;
        }

        return height;
    }
```

Add `using System.Collections.Generic;` to the top of `Scripts/Ui.cs`.

- [ ] **Step 2: Size the result card to its content**

In `Scripts/ScreenOverlay.cs`, replace the fixed `CardHeight` constant with a measured one. Change the field declarations:

```csharp
    private const float CardMinHeight = 68f;
    private const float CardPaddingY = 12f;
    private float _cardHeight = CardMinHeight;
```

Delete `private const float CardHeight = 92f;`.

At the end of `Show(string, string, Color)`, after `_subtitle.Text = subtitle;`, add:

```csharp
        // The card is built around the text, not the text dropped into the card.
        _title.Size = new Vector2(TextWidth, size + 6);
        _cardHeight = Mathf.Max(
            CardMinHeight,
            Ui.ColumnHeight(new List<Label> { _title, _subtitle }, gap: 6f) + CardPaddingY * 2f);
```

Add `using System.Collections.Generic;` at the top.

In `_Draw`, replace the two uses of `CardHeight` with `_cardHeight`, and after the `if (height < CardHeight) return;` line (now `if (height < _cardHeight) return;`) add the layout call before the accent rails:

```csharp
        Ui.LayoutColumn(
            new List<Label> { _title, _subtitle },
            new Rect2(rect.Position.X + CardPadding, rect.Position.Y + CardPaddingY,
                      rect.Size.X - CardPadding * 2f, rect.Size.Y - CardPaddingY * 2f),
            gap: 6f);
```

Delete the two fixed `Position` assignments for `_title` and `_subtitle` in `_Ready`; `LayoutColumn` now owns both.

- [ ] **Step 3: Size the dialogue box to its line**

In `Scripts/DialogueScene.cs`, replace the fixed `BoxTopY` with a measured top edge. Add:

```csharp
    private const float BoxMinHeight = 56f;
    private const float BoxBottomY = GameRoot.ViewportHeight - Margin;
    private float _boxTop = BoxBottomY - BoxMinHeight;
```

Delete `private const float BoxTopY = 186f;`.

At the end of `ShowLine()`, add:

```csharp
        // The body wraps to however many lines it needs, and the box grows upward to
        // hold them - measured, never estimated.
        _body.Text = line.Text;
        var height = Ui.ColumnHeight(new List<Label> { _name, _body }, gap: 8f) + Padding * 2f + 8f;
        _boxTop = BoxBottomY - Mathf.Max(BoxMinHeight, height);

        Ui.LayoutColumn(
            new List<Label> { _name, _body },
            new Rect2(Margin + Padding, _boxTop + Padding,
                      GameRoot.ViewportWidth - (Margin + Padding) * 2f,
                      BoxBottomY - _boxTop - Padding * 2f),
            gap: 8f);

        _body.Text = "";
```

Replace every remaining `BoxTopY` in `_Draw` with `_boxTop`, and change the panel rect's height to `BoxBottomY - _boxTop`. Move the name rail to `_boxTop + Padding + Ui.WrappedHeight(_name) + 3f`.

- [ ] **Step 4: Verify by screenshot**

```bash
GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
"$GODOT" --path . --autoplay --shots=/tmp/shots-centring
```

Then read `/tmp/shots-centring/*.png`. Expected: the title card's text block is optically centred with equal space above and below; a one-line subtitle no longer floats high; the dialogue box hugs its line rather than leaving a gap under short lines.

- [ ] **Step 5: Commit**

```bash
git add Scripts/Ui.cs Scripts/ScreenOverlay.cs Scripts/DialogueScene.cs
git commit -m "Size text boxes around their content and centre the block inside"
```

---

### Task 12: `GameRoot` drives a bout instead of a match

**Files:**
- Modify: `Scripts/GameRoot.cs`, `Scripts/AutoPlay.cs`

**Interfaces:**
- Consumes: `BoutSession`, `BoutEnded`, `BoutResult`, `PhaseStarted`, `PhaseEnded`, `PhaseKnockout`, `Career.Phases`, `Career.InterludeLine`.
- Produces: nothing further tasks consume.

- [ ] **Step 1: Swap the session type**

In `Scripts/GameRoot.cs`:

- Replace the field `private MatchSession? _match;` with `private BoutSession? _bout;`.
- Add `Interlude` to the phase enum: `private enum Phase { Title, Intro, Countdown, Fighting, Interlude, Result, Shop }`.
- In `StartIntro`, replace the `MatchSession` construction with:

```csharp
        _bout = new BoutSession(
            _foodTable, new SeededRandom((int)(Time.GetTicksMsec() & 0x7FFFFFFF)),
            new JawZone(JawCenterX, JawHalfWidth), SpawnX, RetireX, next, Career.Phases);
```

- In `OnDialogueFinished`, branch on why the dialogue was playing. Add a field `private bool _dialogueWasInterlude;` and:

```csharp
    private void OnDialogueFinished()
    {
        _overlay.Hide();

        if (_dialogueWasInterlude)
        {
            _dialogueWasInterlude = false;
            HandleEvents(_bout!.BeginNextPhase());
            _phase = Phase.Countdown;
            _countdown = 1.6f;        // shorter than the pre-bout beat: hands are already set
            _countdownShown = -1;
            return;
        }

        _hud.ResetForNewMatch();
        if (_bout is not null)
        {
            HandleEvents(_bout.Start());
            _hud.Update(_bout.Current.State, 0, 0f, _save.Money);
        }

        _phase = Phase.Countdown;
        _countdown = 2.6f;
        _countdownShown = -1;
    }
```

- Everywhere the fighting tick reads `_match`, read `_bout` instead: `_bout.Tick(dt)`, `_bout.Chomp()`, `_bout.Current.State`, `_bout.Current.Frenzy`, `_bout.Current.Items`, `_bout.Current.BeltSpeed`, `_bout.OpponentScore`.
- Replace `EndMatch(MatchEnded ended)` with `EndBout(BoutEnded ended)`, changing `ended.Result == MatchResult.Won` to `ended.Result == BoutResult.Won` and deleting the `MatchResult.Disqualified` branch entirely — a bout can only be won or lost now. The loss card reads:

```csharp
            _overlay.Show("BEATEN",
                $"{ended.PlayerScore} to {ended.OpponentScore}\npress to try again", Ui.Red);
```

- [ ] **Step 2: Handle the new events**

In the event-handling switch, add cases:

```csharp
            case PhaseStarted started:
                _overlay.Flash(started.Phase.Name);
                _sfx.Play(Sfx.Blip, 1.1f);
                _zoom = 1f;
                _hud.SetPhase(started.PhaseIndex, started.Phase.Name);
                break;

            case PhaseEnded ended:
                StartInterlude(ended);
                break;

            case PhaseKnockout:
                _overlay.Flash("OUT!");
                _sfx.Play(Sfx.Lose);
                _shake = StrikeShake * 1.6f;
                _flashAlpha = 1f;
                break;

            case BoutEnded bout:
                EndBout(bout);
                break;
```

and:

```csharp
    /// <summary>
    /// The valley between two phases. The belt is empty, the rival says something about
    /// how it is going, and the player gets their hands back before the next act.
    /// </summary>
    private void StartInterlude(PhaseEnded ended)
    {
        if (_bout is null || !_bout.AwaitingInterlude)
        {
            return;   // the third phase ends the bout instead; BoutEnded follows.
        }

        _phase = Phase.Interlude;
        _belt.Clear();
        _hud.Visible = false;
        _dialogueWasInterlude = true;

        var def = _bout.Def.Opponent;
        var rivalAhead = ended.OpponentScore > ended.PlayerScore;

        _dialogue.Play("croc", def.SpriteId, new[]
        {
            new DialogueScene.Line(false, def.Name,
                                   Career.InterludeLine(def, ended.PhaseIndex, rivalAhead)),
            new DialogueScene.Line(true, "CROC", CrocInterludeReply(rivalAhead, ended.KnockedOut)),
        });
    }

    private static string CrocInterludeReply(bool rivalAhead, bool knockedOut) =>
        knockedOut ? "*spits out a bomb*"
        : rivalAhead ? "*stomach growls, louder*"
        : "*licks the plate clean*";
```

- [ ] **Step 3: Update the autoplay harness**

In `Scripts/AutoPlay.cs`, wherever it reaches into the session for the jaw zone or the item list, read `_root` through the same accessors `GameRoot` now exposes for the bout. Extend `ShotTimes` so it lands inside each phase and each interlude:

```csharp
    private static readonly float[] ShotTimes =
        { 0.8f, 2.6f, 4.4f, 7.0f, 10.5f, 13.0f, 16.5f, 20.0f, 24.0f, 28.0f, 33.0f, 38.0f, 44.0f, 50.0f };

    private const float QuitAfter = 55f;
```

A bout is now ~27s of play plus interludes, so 45s no longer reaches the shop.

- [ ] **Step 4: Build and play it**

```bash
dotnet build
GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
"$GODOT" --path . --autoplay --shots=/tmp/shots-bout
```

Expected: the build is clean; the shots show three distinct phase banners (PLAIN, HAZARD, FEAST), two interludes with rival dialogue, and a single result card. Read the shots to confirm.

- [ ] **Step 5: Commit**

```bash
git add Scripts/GameRoot.cs Scripts/AutoPlay.cs
git commit -m "Drive a three-phase bout with dialogue interludes from GameRoot"
```

---

### Task 13: HUD — phase pips, the pot, and the hunger meter

**Files:**
- Modify: `Scripts/MatchHud.cs`, `Scripts/StrikeMeter.cs`

**Interfaces:**
- Consumes: `PhaseSession` state, `Pot`, `Hunger`, `ActiveBuffs`.
- Produces: `MatchHud.SetPhase(int index, string name)`; `MatchHud.Update(MatchState state, int opponentScore, float frenzyFraction, int money, Pot pot, Hunger hunger, ActiveBuffs buffs, int carriedScore)`; `StrikeMeter.SetShield(bool has)`.

- [ ] **Step 1: Show which act this is**

In `Scripts/MatchHud.cs` add a phase indicator — three pips beside the clock, the live one filled — plus the phase name, drawn with `Ui.Text(Ui.Small, Ui.Dim)`. Three pips answer "how much of this bout is left" without arithmetic, which is the rule the tug-of-war bar already follows.

```csharp
    private int _phaseIndex;
    private string _phaseName = "";

    public void SetPhase(int index, string name)
    {
        _phaseIndex = index;
        _phaseName = name;
        QueueRedraw();
    }
```

and in `_Draw`, beside the existing clock:

```csharp
        for (var i = 0; i < Career.Phases.Count; i++)
        {
            var pip = new Rect2(6 + i * 7, 4, 5, 5);
            Ui.Panel(this, pip, i <= _phaseIndex ? Ui.Gold : Ui.PanelFill);
        }
```

- [ ] **Step 2: Show the pot as money at risk**

Add a pot readout under the score: the amount, its multiplier, and a colour that says it is not banked yet. Use `Ui.Gold` while it is small and `Ui.Red` once it exceeds the current score, because that is the point at which declining a coin is a real gamble.

```csharp
    public void SetPot(int amount, int multiplier, int scoreSoFar)
    {
        _potText = amount == 0 ? "" : $"POT {amount}x{multiplier}";
        _potColor = amount * multiplier > scoreSoFar ? Ui.Red : Ui.Gold;
        QueueRedraw();
    }
```

- [ ] **Step 3: Show hunger filling**

Add a meter along the bottom edge using the existing `Ui.Meter`, filled with `Ui.Red`, visible only while `hunger.Charge > 0f`. Label it HUNGER at `Ui.Small`. It has to be visible while filling, not only when it fires — a meter the player only ever sees full teaches them nothing about why it happened.

- [ ] **Step 4: Show a shield as a fourth tooth**

In `Scripts/StrikeMeter.cs` add `SetShield(bool has)`, drawing a fourth tooth in `Ui.Green` to the right of the three. Answering "what am I carrying" through the meter that already exists is better than adding a new indicator, and it reads the right way round: a present tooth is something you have.

- [ ] **Step 5: Verify and commit**

```bash
dotnet build
GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
"$GODOT" --path . --autoplay --shots=/tmp/shots-hud
```

Read the shots: three phase pips with the live one lit, a POT readout while a wager stands, a hunger meter that fills when the rival pulls ahead, and a fourth green tooth after a shield is taken.

```bash
git add Scripts/MatchHud.cs Scripts/StrikeMeter.cs Scripts/GameRoot.cs
git commit -m "HUD: phase pips, the pot at risk, the hunger meter, the shield tooth"
```

---

### Task 14: Coin and buff presentation on the belt

**Files:**
- Modify: `Scripts/BeltView.cs`, `Scripts/GameRoot.cs`

**Interfaces:**
- Consumes: `CoinSpawned`, `PotBanked`, `PotWiped`, `BuffTaken`, `BuffExpired`.
- Produces: `BeltView.SetCoinValue(int itemId, int value)`.

- [ ] **Step 1: Draw the coin's value on the coin**

`BeltView` creates a sprite per `Spawned`. Add a parallel path for `CoinSpawned` that creates the coin sprite plus a `Label` child showing the value, at `Ui.Small` in `Ui.Ink` over the gold. The number is the wager; it is the one piece of text that must be on the belt rather than in the HUD, because the decision is made by looking at the coin.

```csharp
    public void SetCoinValue(int itemId, int value)
    {
        if (!_labels.TryGetValue(itemId, out var label)) return;

        label.Text = value.ToString();
        label.Size = Ui.Measure(label.Text, Ui.Small);
        label.Position = -label.Size / 2f;   // centred on the coin, measured not guessed
    }
```

- [ ] **Step 2: React to banking and to losing the pot**

In `GameRoot`'s event switch:

```csharp
            case PotBanked banked:
                _sfx.Play(Sfx.Coin, 1f + 0.04f * banked.Multiplier);
                _goldFlash = 1f;
                _hitStop = HitStopSeconds * 2f;
                _overlay.Flash($"+{banked.Paid}");
                _crumbs.Burst(new Vector2(JawCenterX, BeltY), count: 14, force: 1.4f);
                break;

            case PotWiped wiped when wiped.Lost > 0:
                _sfx.Play(Sfx.Whiff, 0.7f);
                _overlay.Flash($"-{wiped.Lost}");
                break;

            case BuffTaken taken:
                _sfx.Play(Sfx.Frenzy, BuffPitch(taken.Kind));
                _overlay.Flash(BuffLabel(taken.Kind));
                _croc.Punch(1.2f);
                break;
```

```csharp
    private static string BuffLabel(BuffKind kind) => kind switch
    {
        BuffKind.Slow => "SLOW",
        BuffKind.Shield => "SHIELD",
        BuffKind.Magnet => "MAGNET",
        _ => "GOLD TOOTH",
    };

    private static float BuffPitch(BuffKind kind) => kind switch
    {
        BuffKind.Slow => 0.8f,
        BuffKind.Shield => 1.0f,
        BuffKind.Magnet => 1.2f,
        _ => 1.5f,
    };
```

Banking gets the full treatment — hit-stop, gold wash, a burst, its own sound — because it is the rare moment, and `design-principles.md` says to spend everything on those.

- [ ] **Step 3: Verify and commit**

```bash
dotnet build
GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
"$GODOT" --path . --autoplay --shots=/tmp/shots-coin
```

Read the shots: coins carry a legible number, banking produces a gold wash and a `+N` banner. Buff art does not exist yet and will render as the magenta placeholder — that is expected and is Plan 2's job.

```bash
git add Scripts/BeltView.cs Scripts/GameRoot.cs
git commit -m "Draw the wager on the coin and give banking its own moment"
```

---

### Task 15: Hunger and magnet made visible

**Files:**
- Modify: `Scripts/GameRoot.cs`, `Scripts/ConveyorView.cs`, `Scripts/CrocView.cs`

**Interfaces:**
- Consumes: `HungerStarted`, `HungerEnded`, `PhaseSession.EffectiveJaw`.
- Produces: nothing further tasks consume.

- [ ] **Step 1: Draw the real jaw width, always**

`ConveyorView` currently draws the bite zone from `GameRoot.JawHalfWidth`. Change it to take the width each frame from `_bout.Current.EffectiveJaw.HalfWidth`, so the drawn window and the judged window are the same number by construction rather than by agreement. This is the whole reason Hunger is allowed to widen it at all.

```csharp
    public void SetJawHalfWidth(float halfWidth)
    {
        if (Mathf.IsEqualApprox(_jawHalfWidth, halfWidth)) return;

        _jawHalfWidth = halfWidth;
        QueueRedraw();
    }
```

Call it from `GameRoot`'s fighting tick, before drawing.

- [ ] **Step 2: Announce hunger**

```csharp
            case HungerStarted:
                _overlay.Flash("HUNGRY");
                _sfx.Play(Sfx.Frenzy, 0.6f);
                _flashAlpha = 0.6f;
                _zoom = 1.4f;
                _rival.Panic(_bout!.Def.Opponent.LinePanic);
                break;

            case HungerEnded:
                _sfx.Play(Sfx.Blip, 0.6f);
                break;
```

`RivalView.Panic(string)` already exists and is currently reached only from a long
combo; hunger is the other thing that deserves it. The rival panicking is what tells the
player this is a big deal — a banner alone is a colour swap on a small element, which `design-principles.md` says will be missed while the player is watching the belt.

- [ ] **Step 3: Hold the jaws open during a magnet**

In `CrocView`, add:

```csharp
    /// <summary>
    /// The magnet suspends judging for three bites. The jaws hold visibly open for the
    /// duration, because the one thing that must never happen is the window quietly
    /// lying about its size - if the game is taking a bite for you, you have to be able
    /// to see it doing that.
    /// </summary>
    public void SetMagnet(bool active)
    {
        if (_magnet == active) return;

        _magnet = active;
        if (active) Play("eat");
        SpeedScale = active ? 0f : 1f;   // freeze on the open-jaw frame
    }
```

Drive it from `GameRoot` off `_bout.Current.Buffs.MagnetBitesRemaining > 0`.

- [ ] **Step 4: Verify and commit**

```bash
dotnet build
GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
"$GODOT" --path . --autoplay --shots=/tmp/shots-hunger
```

Read the shots: when the hunger banner fires, the drawn bite zone is visibly wider than it was a shot earlier, and the rival is in its panic pose.

```bash
git add Scripts/GameRoot.cs Scripts/ConveyorView.cs Scripts/CrocView.cs
git commit -m "Draw the true jaw width and make hunger and magnet visible"
```

---

### Task 16: Split the bout state machine out of `GameRoot`

**Files:**
- Create: `Scripts/BoutDirector.cs`
- Modify: `Scripts/GameRoot.cs`

**Interfaces:**
- Consumes: `BoutSession`, every event type, and the view nodes `GameRoot` already owns.
- Produces: `BoutDirector` with `void Begin(MatchDef def, FoodTable table, SaveData save)`, `void Tick(float dt)`, `void Chomp()`, `Action<BoutEnded>? Finished`, and `bool AwaitingInterlude`.

`GameRoot` was 637 lines before this plan, and Tasks 12-15 added a phase state machine, interlude sequencing, coin and buff presentation, and hunger handling to it. The spec calls for this split explicitly, and the reason is not tidiness: a file this size is one where edits stop being reliable, and every task in the art plan also touches it.

This is deliberately the last task. Extracting before the behaviour is settled would mean drawing the boundary around a moving target.

- [ ] **Step 1: Move the bout state machine**

Create `Scripts/BoutDirector.cs` holding everything that is about *running a bout*: the `_bout` field, the Countdown / Fighting / Interlude transitions, the countdown timer, the event switch, and the interlude sequencing. It takes the view nodes by constructor injection rather than creating them, because they outlive a bout and `GameRoot` still owns the screens.

```csharp
using System;
using CrocGame.Core;
using Godot;

namespace CrocGame;

/// <summary>
/// Runs one bout: three phases, the countdowns between them, the interlude dialogue,
/// and the translation of every Core event into something on screen.
///
/// Separate from GameRoot because GameRoot is about *screens* - title, shop, results -
/// and this is about *a match*. They change for different reasons and at different
/// times, which is the line worth splitting on.
/// </summary>
public partial class BoutDirector : Node
{
    public Action<BoutEnded>? Finished;

    // The fields, Begin/Tick/Chomp, and the event switch moved from GameRoot.
}
```

- [ ] **Step 2: Leave `GameRoot` owning only the screens**

What stays: node construction in `_Ready`, the save store, the food table, `ShowTitle`, `OpenShop`, `OnBuy`, `ApplySkin`, `EndBout`, and input routing. It creates a `BoutDirector`, subscribes to `Finished`, and forwards `_Process` and presses to it while a bout is live.

- [ ] **Step 3: Verify nothing changed**

The point of an extraction is that behaviour is identical. Capture shots and compare against Task 15's:

```bash
GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
dotnet build
"$GODOT" --path . --autoplay --shots=/tmp/shots-after-split
```

Expected: the same sequence of screens as `/tmp/shots-hunger` - three phase banners, two interludes, one result card. `dotnet test` is unaffected; nothing in `Core` moved.

- [ ] **Step 4: Confirm the file came down**

```bash
wc -l Scripts/GameRoot.cs Scripts/BoutDirector.cs
```

Expected: `GameRoot.cs` under 350 lines. If it is not, the bout state machine is still partly in it.

- [ ] **Step 5: Commit**

```bash
git add Scripts/BoutDirector.cs Scripts/GameRoot.cs
git commit -m "Split the bout state machine out of GameRoot"
```

---

### Task 17: Update the principles document

**Files:**
- Modify: `docs/design-principles.md`

- [ ] **Step 1: Record what this work learned**

Add under "Game mechanics":

```markdown
**Three acts, not one block.** A bout is PLAIN, HAZARD, FEAST, with dialogue between.
Short bouts still matters - each phase is 8-10 seconds, a single burst of concentration
- but a single undifferentiated block has no shape to talk over and nothing to
introduce.

**A strike ends a phase, never a run.** Three strikes knocks the croc out of the
current phase and hands the rival its remaining seconds. Losing that badly still hurts,
visibly, but a beginner who blows the first act still plays the other two and can still
win. A mechanic that can end a run is a ceiling.

**Give the losing player something, automatically.** Hunger charges only from a
deficit and fires by itself. A comeback mechanic that has to be earned or activated is
no use to the player who needs it, because that player is the one already struggling
with the inputs.

**The wager uses the verb you already have.** The pot is banked by biting a coin, so
push-your-luck costs no new button and no menu: the decision is a position on the belt
and a number drawn on the sprite.

**Never risk what the player already scored.** The pot is upside stacked on the score.
Banking every coin on sight is safe, viable play - greed is an option, not a tax.

**Pay for strength with window width.** SLOW is 16px wide and arrives alone; a GOLD
TOOTH is 8px and arrives between two bombs. Risk and reward are the same axis, and the
axis is one the player can see from across the screen.
```

Add under "UI":

```markdown
**Measure height too, not just width.** Boxes used to be a fixed size with text at
fixed offsets, so nothing was ever actually centred - it was positioned to look centred
for one particular string. `Ui.LayoutColumn` sizes the box around the block and centres
the block in the box.
```

- [ ] **Step 2: Commit**

```bash
git add docs/design-principles.md
git commit -m "Record the phase, assist and layout principles this work established"
```

---

## Definition of done

- `dotnet test` reports 0 failures and no fewer than 185 tests.
- `dotnet build` is clean for `CrocGame.csproj`, `CrocGame.Core.csproj` and the test project.
- A screenshot run reaches the shop and shows: three phase banners, two interludes, a coin carrying a number, a hunger meter that fills, and a widened bite zone while hunger is live.
- `Scripts/GameRoot.cs` is under 350 lines.
- No `MatchEnded`, `MatchResult`, or `MatchSession` identifier remains in the tree:
  `grep -rn 'MatchEnded\|MatchResult\|MatchSession' Core Scripts Tests` returns nothing.
