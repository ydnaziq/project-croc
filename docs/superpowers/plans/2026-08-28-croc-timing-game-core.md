# Croc Timing Game — Core Rules and Playable Slice — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the complete timing rule set as an engine-independent C# library with full unit tests, then wire it to a minimal playable Godot scene — belt, jaws, chomp, strikes, score, restart.

**Architecture:** All game rules live in `CrocGame.Core`, a class library with no reference to GodotSharp, driven by an injected `dt` and returning a list of events per tick. The Godot layer renders those events and sends one input command back. This boundary is what makes the timing rules testable with `dotnet test` on a machine with no Godot binary installed.

**Tech Stack:** C#, .NET (Core targets `net8.0`, tests target `net10.0`), xUnit, Godot 4.7 Mobile renderer.

**Spec:** `docs/superpowers/specs/2026-08-28-croc-timing-game-design.md`

## Global Constraints

- `CrocGame.Core` MUST NOT reference GodotSharp. Task 1 adds a test that enforces this; it must never be deleted or skipped.
- Core targets `net8.0`. Tests target `net10.0`. Verified working: a net10.0 test project can reference a net8.0 class library, and only the .NET 10 runtime is installed on this machine, so net8.0 test projects cannot execute.
- Core never reads a wall clock, never reads input, never does file IO directly. Time arrives as a `float dt` parameter; persistence goes through `ISaveStore`.
- Base viewport is 320x180. Food sprites are 16x16. The croc is 32x32.
- Namespaces are `CrocGame.Core` and `CrocGame` (scene layer). The Godot assembly name is `CrocGame`.
- Three strikes ends a run. Strike sources: chomping empty air, chomping an inedible, letting an edible item retire unchomped.
- All movement is along X only. No behavior may depend on a vertical position, because the judge does not read one.
- Run `dotnet test` from the repository root. It must pass at the end of every task.

## File Structure

| File | Responsibility |
|---|---|
| `CrocGame.slnx` | Ties the three projects together. |
| `Core/CrocGame.Core.csproj` | The rules library. net8.0, no Godot. |
| `Core/JawZone.cs` | The overlap test that defines "between the teeth". |
| `Core/FoodItem.cs` | One item on the belt. |
| `Core/Movement.cs` | `IMovement` and the three strategies. |
| `Core/Belt.cs` | Holds items, advances them, retires them. |
| `Core/Difficulty.cs` | Pure function of items eaten to every escalation lever. |
| `Core/RandomSource.cs` | `IRandomSource` and a seeded implementation. |
| `Core/FoodTable.cs` | Food type definitions loaded from JSON. |
| `Core/SpawnDirector.cs` | Decides when and what to spawn. |
| `Core/ChompJudge.cs` | Resolves a chomp to a hit or air. |
| `Core/RunState.cs` | Score, combo, strikes, eaten count. |
| `Core/GameEvent.cs` | The event types the scene layer renders. |
| `Core/GameSession.cs` | Façade: `Tick(dt)` and `Chomp()`. |
| `Core/SaveData.cs` | `SaveData`, `ISaveStore`, in-memory implementation. |
| `Tests/CrocGame.Core.Tests.csproj` | net10.0, xUnit, references Core only. |
| `Tests/*Tests.cs` | One test file per Core file. |
| `Resources/food.json` | The five edible foods that already have sprites. |
| `Scripts/*.cs`, `Scenes/*.tscn` | Godot layer, Tasks 11-13. |

---

### Task 1: Solution scaffold, rename to CrocGame, project settings

**Files:**
- Create: `CrocGame.slnx`, `Core/CrocGame.Core.csproj`, `Tests/CrocGame.Core.Tests.csproj`
- Create: `Tests/BoundaryTests.cs`
- Modify: `project.godot`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: nothing.
- Produces: a solution where `dotnet test` runs from the repo root; `CrocGame.Core` assembly; the namespace `CrocGame.Core`.

- [x] **Step 1: Create the three projects**

```bash
cd /home/ydnaiq/Projects/physics-game
dotnet new classlib -o Core -n CrocGame.Core
dotnet new xunit    -o Tests -n CrocGame.Core.Tests
rm -f Core/Class1.cs Tests/UnitTest1.cs
dotnet new sln -n CrocGame   # .NET 10 writes CrocGame.slnx, not .sln
dotnet sln add Core/CrocGame.Core.csproj Tests/CrocGame.Core.Tests.csproj
dotnet add Tests/CrocGame.Core.Tests.csproj reference Core/CrocGame.Core.csproj
```

- [x] **Step 2: Set the target frameworks**

Core must be net8.0 so the Godot project can consume it. Tests must be net10.0 because only the .NET 10 runtime is installed.

```bash
sed -i 's|<TargetFramework>net10.0</TargetFramework>|<TargetFramework>net8.0</TargetFramework>|' Core/CrocGame.Core.csproj
grep -q 'net10.0' Tests/CrocGame.Core.Tests.csproj || echo "TESTS SHOULD BE net10.0"
```

Verify `Core/CrocGame.Core.csproj` contains `<TargetFramework>net8.0</TargetFramework>` and `Tests/CrocGame.Core.Tests.csproj` contains `<TargetFramework>net10.0</TargetFramework>`.

- [x] **Step 3: Write the failing boundary test**

This test is the enforcement mechanism for the most important constraint in the plan. Create `Tests/BoundaryTests.cs`:

```csharp
using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class BoundaryTests
{
    [Fact]
    public void CoreAssemblyDoesNotReferenceGodot()
    {
        var referenced = typeof(JawZone).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();

        Assert.DoesNotContain(referenced, name =>
            name.Contains("Godot", System.StringComparison.OrdinalIgnoreCase));
    }
}
```

- [x] **Step 4: Run the test to verify it fails**

Run: `dotnet test`
Expected: FAIL — compile error, `JawZone` does not exist yet.

- [x] **Step 5: Create the minimal JawZone so the boundary test compiles**

Create `Core/JawZone.cs`:

```csharp
namespace CrocGame.Core;

/// <summary>The bite window, as a one-dimensional interval on the belt's X axis.</summary>
public readonly record struct JawZone(float Center, float HalfWidth);
```

- [x] **Step 6: Run the test to verify it passes**

Run: `dotnet test`
Expected: PASS — 1 test.

- [x] **Step 7: Correct project.godot**

Apply these four edits to `project.godot`:

```bash
sed -i 's|config/name="PhysicsGame"|config/name="CrocGame"|' project.godot
sed -i 's|project/assembly_name="PhysicsGame"|project/assembly_name="CrocGame"|' project.godot
sed -i '/^3d\/physics_engine="Jolt Physics"$/d' project.godot
```

Then remove the now-empty `[physics]` section header if nothing remains under it, and add to the `[display]` section:

```
window/size/viewport_width=320
window/size/viewport_height=180
```

And add to the `[rendering]` section:

```
textures/canvas_textures/default_texture_filter=0
```

- [x] **Step 8: Ignore build output**

```bash
printf '\n# .NET build output\nbin/\nobj/\n' >> .gitignore
```

- [x] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: scaffold CrocGame solution and correct Godot project settings

Core targets net8.0 for Godot compatibility; tests target net10.0 because
only the .NET 10 runtime is installed. A boundary test enforces that Core
never references GodotSharp."
```

---

### Task 2: The jaw zone overlap test

**Files:**
- Modify: `Core/JawZone.cs`
- Create: `Core/FoodItem.cs`, `Core/Movement.cs`
- Create: `Tests/JawZoneTests.cs`

**Interfaces:**
- Consumes: `JawZone` from Task 1.
- Produces: `FoodItem` (class, mutable `X` and `Age`), `IMovement.DeltaX(beltSpeed, dt, age)`, `Movement.Constant`, `JawZone.Overlaps(FoodItem)`, `JawZone.DistanceFromCenter(FoodItem)`.

- [x] **Step 1: Write the failing tests**

Touching counts as overlapping — a bite that just grazes the edge of the food is a bite. Create `Tests/JawZoneTests.cs`:

```csharp
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class JawZoneTests
{
    private static FoodItem Item(float x) =>
        new FoodItem(id: 1, typeId: "fish", x: x, halfWidth: 8f,
                     isEdible: true, score: 10, movement: Movement.Constant);

    private static readonly JawZone Jaw = new JawZone(Center: 100f, HalfWidth: 12f);

    [Fact]
    public void ItemAtCenterOverlaps() =>
        Assert.True(Jaw.Overlaps(Item(100f)));

    [Theory]
    [InlineData(80f)]   // exactly touching on the left:  |80-100| == 20 == 12+8
    [InlineData(120f)]  // exactly touching on the right
    public void ItemExactlyTouchingOverlaps(float x) =>
        Assert.True(Jaw.Overlaps(Item(x)));

    [Theory]
    [InlineData(79.9f)]
    [InlineData(120.1f)]
    public void ItemJustOutsideDoesNotOverlap(float x) =>
        Assert.False(Jaw.Overlaps(Item(x)));

    [Fact]
    public void DistanceFromCenterIsAbsolute()
    {
        Assert.Equal(5f, Jaw.DistanceFromCenter(Item(105f)), precision: 4);
        Assert.Equal(5f, Jaw.DistanceFromCenter(Item(95f)), precision: 4);
    }
}
```

- [x] **Step 2: Run to verify it fails**

Run: `dotnet test`
Expected: FAIL — `FoodItem` and `Movement` do not exist.

- [x] **Step 3: Write IMovement and the Constant strategy**

`FoodItem` needs a movement strategy to construct, so the interface and the simplest
implementation land here. The other two strategies come in Task 3.

Create `Core/Movement.cs`:

```csharp
namespace CrocGame.Core;

/// <summary>
/// How far an item travels this frame. All movement is along X only: the jaw zone is
/// a one-dimensional overlap test, so a behavior on any other axis would be invisible
/// to the judge and would make the game look like it was lying.
/// </summary>
public interface IMovement
{
    float DeltaX(float beltSpeed, float dt, float age);
}

public static class Movement
{
    public static readonly IMovement Constant = new ConstantMovement();

    private sealed class ConstantMovement : IMovement
    {
        public float DeltaX(float beltSpeed, float dt, float age) => beltSpeed * dt;
    }
}
```

- [x] **Step 4: Write FoodItem**

Create `Core/FoodItem.cs`:

```csharp
namespace CrocGame.Core;

/// <summary>
/// One item riding the belt. HalfWidth is the timing window: a narrow item at
/// high belt speed is a hard press, and that relationship is visible on screen.
/// </summary>
public sealed class FoodItem
{
    public FoodItem(int id, string typeId, float x, float halfWidth,
                    bool isEdible, int score, IMovement movement)
    {
        Id = id;
        TypeId = typeId;
        X = x;
        HalfWidth = halfWidth;
        IsEdible = isEdible;
        Score = score;
        Movement = movement;
    }

    public int Id { get; }
    public string TypeId { get; }
    public float X { get; set; }
    public float HalfWidth { get; }
    public bool IsEdible { get; }
    public int Score { get; }
    public IMovement Movement { get; }

    /// <summary>Seconds this item has been on the belt. Drives movement behaviors.</summary>
    public float Age { get; set; }
}
```

- [x] **Step 5: Extend JawZone**

Replace `Core/JawZone.cs` with:

```csharp
using System;

namespace CrocGame.Core;

/// <summary>The bite window, as a one-dimensional interval on the belt's X axis.</summary>
public readonly record struct JawZone(float Center, float HalfWidth)
{
    /// <summary>
    /// True when the item's interval touches or crosses the jaw interval.
    /// Touching counts: a bite that grazes the edge of the food is a bite.
    /// </summary>
    public bool Overlaps(FoodItem item) =>
        MathF.Abs(item.X - Center) <= HalfWidth + item.HalfWidth;

    public float DistanceFromCenter(FoodItem item) => MathF.Abs(item.X - Center);
}
```

- [x] **Step 6: Run to verify it passes**

Run: `dotnet test`
Expected: PASS — every test green, including the new ones from this task and the boundary test from Task 1.

- [x] **Step 7: Commit**

```bash
git add Core/JawZone.cs Core/FoodItem.cs Core/Movement.cs Tests/JawZoneTests.cs
git commit -m "feat: jaw zone overlap test, FoodItem, and constant movement"
```

---

### Task 3: Stutter and Bounce movement strategies

**Files:**
- Modify: `Core/Movement.cs`
- Create: `Tests/MovementTests.cs`

**Interfaces:**
- Consumes: `IMovement`, `Movement.Constant` from Task 2.
- Produces: `Movement.Stutter`, `Movement.Bounce`, `Movement.ByName(string)`.

- [x] **Step 1: Write the failing tests**

Create `Tests/MovementTests.cs`:

```csharp
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class MovementTests
{
    [Fact]
    public void ConstantAdvancesBySpeedTimesDelta()
    {
        Assert.Equal(5f, Movement.Constant.DeltaX(beltSpeed: 100f, dt: 0.05f, age: 0f), precision: 4);
    }

    [Fact]
    public void ConstantIgnoresAge()
    {
        var a = Movement.Constant.DeltaX(100f, 0.05f, age: 0f);
        var b = Movement.Constant.DeltaX(100f, 0.05f, age: 12.5f);
        Assert.Equal(a, b, precision: 4);
    }

    [Fact]
    public void StutterPausesEarlyInEachCycle()
    {
        // Cycle is 1 second; the first 0.15s of every cycle is a pause.
        Assert.Equal(0f, Movement.Stutter.DeltaX(100f, 0.05f, age: 0.00f), precision: 4);
        Assert.Equal(0f, Movement.Stutter.DeltaX(100f, 0.05f, age: 1.10f), precision: 4);
    }

    [Fact]
    public void StutterMovesAtFullSpeedOutsideThePause()
    {
        Assert.Equal(5f, Movement.Stutter.DeltaX(100f, 0.05f, age: 0.50f), precision: 4);
        Assert.Equal(5f, Movement.Stutter.DeltaX(100f, 0.05f, age: 1.60f), precision: 4);
    }

    [Fact]
    public void BounceNeverMovesBackwards()
    {
        for (var age = 0f; age < 4f; age += 0.01f)
        {
            Assert.True(Movement.Bounce.DeltaX(100f, 0.05f, age) >= 0f);
        }
    }

    [Fact]
    public void BounceSurgesAboveBeltSpeedAtItsPeak()
    {
        // Peak of the surge is at age where sin(age * 6) == 1, i.e. age = pi/12.
        var peak = Movement.Bounce.DeltaX(100f, 0.05f, age: MathF.PI / 12f);
        Assert.True(peak > 5f, $"expected a surge above the 5f constant baseline, got {peak}");
    }

    [Theory]
    [InlineData("constant")]
    [InlineData("stutter")]
    [InlineData("bounce")]
    public void ByNameResolvesKnownStrategies(string name)
    {
        Assert.NotNull(Movement.ByName(name));
    }

    [Fact]
    public void ByNameFallsBackToConstantForUnknownNames()
    {
        Assert.Same(Movement.Constant, Movement.ByName("teleport"));
    }
}
```

Add `using System;` at the top of the test file for `MathF`.

- [x] **Step 2: Run to verify it fails**

Run: `dotnet test`
Expected: FAIL — `Movement.Stutter`, `Movement.Bounce`, `Movement.ByName` do not exist.

- [x] **Step 3: Implement the two strategies**

Replace the `Movement` static class in `Core/Movement.cs` with:

```csharp
public static class Movement
{
    public static readonly IMovement Constant = new ConstantMovement();
    public static readonly IMovement Stutter = new StutterMovement();
    public static readonly IMovement Bounce = new BounceMovement();

    /// <summary>Resolves a name from food.json. Unknown names fall back to Constant.</summary>
    public static IMovement ByName(string name) => name switch
    {
        "stutter" => Stutter,
        "bounce" => Bounce,
        _ => Constant,
    };

    private sealed class ConstantMovement : IMovement
    {
        public float DeltaX(float beltSpeed, float dt, float age) => beltSpeed * dt;
    }

    /// <summary>Halts for the first 0.15s of every 1s cycle, then resumes full speed.</summary>
    private sealed class StutterMovement : IMovement
    {
        private const float CycleSeconds = 1f;
        private const float PauseSeconds = 0.15f;

        public float DeltaX(float beltSpeed, float dt, float age)
        {
            var phase = age % CycleSeconds;
            return phase < PauseSeconds ? 0f : beltSpeed * dt;
        }
    }

    /// <summary>
    /// Surges forward and settles, so the item crosses the jaw zone faster than the
    /// belt speed implies. Never negative: the belt only ever moves one way.
    /// </summary>
    private sealed class BounceMovement : IMovement
    {
        public float DeltaX(float beltSpeed, float dt, float age)
        {
            var surge = MathF.Max(0f, MathF.Sin(age * 6f));
            return beltSpeed * dt * (1f + 0.8f * surge);
        }
    }
}
```

Add `using System;` at the top of `Core/Movement.cs`.

- [x] **Step 4: Run to verify it passes**

Run: `dotnet test`
Expected: PASS — every test green, including the new ones from this task.

- [x] **Step 5: Commit**

```bash
git add Core/Movement.cs Tests/MovementTests.cs
git commit -m "feat: stutter and bounce movement strategies"
```

---

### Task 4: The belt

**Files:**
- Create: `Core/Belt.cs`
- Create: `Tests/BeltTests.cs`

**Interfaces:**
- Consumes: `FoodItem`, `IMovement` from Task 2.
- Produces: `Belt(float retireX)`, `Belt.Items`, `Belt.Add(FoodItem)`, `Belt.Remove(FoodItem)`, `Belt.Advance(float beltSpeed, float dt)` returning `IReadOnlyList<FoodItem>` of items retired this frame.

- [x] **Step 1: Write the failing tests**

Create `Tests/BeltTests.cs`:

```csharp
using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class BeltTests
{
    private static FoodItem Item(int id, float x, bool edible = true) =>
        new FoodItem(id, "fish", x, halfWidth: 8f, isEdible: edible, score: 10,
                     movement: Movement.Constant);

    [Fact]
    public void AdvanceMovesItemsByBeltSpeed()
    {
        var belt = new Belt(retireX: 400f);
        var item = Item(1, x: 0f);
        belt.Add(item);

        belt.Advance(beltSpeed: 100f, dt: 0.1f);

        Assert.Equal(10f, item.X, precision: 4);
    }

    [Fact]
    public void AdvanceAccumulatesItemAge()
    {
        var belt = new Belt(retireX: 400f);
        var item = Item(1, x: 0f);
        belt.Add(item);

        belt.Advance(100f, 0.1f);
        belt.Advance(100f, 0.1f);

        Assert.Equal(0.2f, item.Age, precision: 4);
    }

    [Fact]
    public void ItemsPastRetireXAreReturnedAndRemoved()
    {
        var belt = new Belt(retireX: 50f);
        var stays = Item(1, x: 0f);
        var goes = Item(2, x: 49f);
        belt.Add(stays);
        belt.Add(goes);

        var retired = belt.Advance(beltSpeed: 100f, dt: 0.1f);

        Assert.Single(retired);
        Assert.Equal(2, retired[0].Id);
        Assert.Single(belt.Items);
        Assert.Equal(1, belt.Items[0].Id);
    }

    [Fact]
    public void RetirementUsesTrailingEdgeSoAnItemIsFullyPastTheJaws()
    {
        // An item centered exactly on retireX still has half its body before the line.
        var belt = new Belt(retireX: 50f);
        var item = Item(1, x: 50f);
        belt.Add(item);

        var retired = belt.Advance(beltSpeed: 0f, dt: 0.1f);

        Assert.Empty(retired);
    }

    [Fact]
    public void RemoveTakesAnItemOffTheBelt()
    {
        var belt = new Belt(retireX: 400f);
        var item = Item(1, x: 0f);
        belt.Add(item);

        belt.Remove(item);

        Assert.Empty(belt.Items);
    }

    [Fact]
    public void AdvanceOnAnEmptyBeltReturnsNothing()
    {
        var belt = new Belt(retireX: 400f);
        Assert.Empty(belt.Advance(100f, 0.1f));
    }
}
```

- [x] **Step 2: Run to verify it fails**

Run: `dotnet test`
Expected: FAIL — `Belt` does not exist.

- [x] **Step 3: Implement Belt**

Create `Core/Belt.cs`:

```csharp
using System.Collections.Generic;

namespace CrocGame.Core;

/// <summary>
/// Holds the items currently riding past the croc, advances them, and reports the
/// ones that have travelled fully past the jaws.
/// </summary>
public sealed class Belt
{
    private readonly List<FoodItem> _items = new();
    private readonly List<FoodItem> _retiredThisFrame = new();

    public Belt(float retireX) => RetireX = retireX;

    /// <summary>An item is retired once its trailing edge passes this X.</summary>
    public float RetireX { get; }

    public IReadOnlyList<FoodItem> Items => _items;

    public void Add(FoodItem item) => _items.Add(item);

    public void Remove(FoodItem item) => _items.Remove(item);

    public IReadOnlyList<FoodItem> Advance(float beltSpeed, float dt)
    {
        _retiredThisFrame.Clear();

        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            item.X += item.Movement.DeltaX(beltSpeed, dt, item.Age);
            item.Age += dt;

            if (item.X - item.HalfWidth > RetireX)
            {
                _retiredThisFrame.Add(item);
                _items.RemoveAt(i);
            }
        }

        return _retiredThisFrame;
    }
}
```

- [x] **Step 4: Run to verify it passes**

Run: `dotnet test`
Expected: PASS — every test green, including the new ones from this task.

- [x] **Step 5: Commit**

```bash
git add Core/Belt.cs Tests/BeltTests.cs
git commit -m "feat: belt advances and retires items"
```

---

### Task 5: The difficulty curve

**Files:**
- Create: `Core/Difficulty.cs`
- Create: `Tests/DifficultyTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Difficulty` readonly record struct with `BeltSpeed`, `SpacingMin`, `SpacingMax`, `StutterWeight`, `BounceWeight`, `InedibleChance`; and `Difficulty.ForEaten(int eaten)`.

All numbers here come from section 5 of the spec and are expected to change after playtesting. The tests assert shape — monotonic, capped, gated — not exact values, so tuning does not break the suite.

- [x] **Step 1: Write the failing tests**

Create `Tests/DifficultyTests.cs`:

```csharp
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class DifficultyTests
{
    [Fact]
    public void StartsAtTheOpeningBeltSpeed()
    {
        Assert.Equal(40f, Difficulty.ForEaten(0).BeltSpeed, precision: 2);
    }

    [Fact]
    public void BeltSpeedIncreasesMonotonically()
    {
        var previous = 0f;
        for (var eaten = 0; eaten <= 200; eaten++)
        {
            var speed = Difficulty.ForEaten(eaten).BeltSpeed;
            Assert.True(speed >= previous, $"belt speed dropped at {eaten} eaten");
            previous = speed;
        }
    }

    [Fact]
    public void BeltSpeedIsCapped()
    {
        Assert.Equal(220f, Difficulty.ForEaten(10_000).BeltSpeed, precision: 2);
    }

    [Fact]
    public void SpacingNarrowsMonotonicallyAndIsFloored()
    {
        var previous = float.MaxValue;
        for (var eaten = 0; eaten <= 200; eaten++)
        {
            var min = Difficulty.ForEaten(eaten).SpacingMin;
            Assert.True(min <= previous, $"spacing widened at {eaten} eaten");
            previous = min;
        }

        Assert.Equal(0.35f, Difficulty.ForEaten(10_000).SpacingMin, precision: 2);
    }

    [Fact]
    public void SpacingMaxIsNeverBelowSpacingMin()
    {
        for (var eaten = 0; eaten <= 200; eaten++)
        {
            var d = Difficulty.ForEaten(eaten);
            Assert.True(d.SpacingMax >= d.SpacingMin, $"inverted spacing at {eaten} eaten");
        }
    }

    [Fact]
    public void StutterIsAbsentUntilFifteenEaten()
    {
        Assert.Equal(0f, Difficulty.ForEaten(14).StutterWeight, precision: 4);
        Assert.True(Difficulty.ForEaten(15).StutterWeight > 0f);
    }

    [Fact]
    public void BounceIsAbsentUntilThirtyEaten()
    {
        Assert.Equal(0f, Difficulty.ForEaten(29).BounceWeight, precision: 4);
        Assert.True(Difficulty.ForEaten(30).BounceWeight > 0f);
    }

    [Fact]
    public void InediblesAreAbsentUntilTwentyFiveEatenAndCapAtTwentyPercent()
    {
        Assert.Equal(0f, Difficulty.ForEaten(24).InedibleChance, precision: 4);
        Assert.True(Difficulty.ForEaten(25).InedibleChance > 0f);
        Assert.Equal(0.20f, Difficulty.ForEaten(10_000).InedibleChance, precision: 4);
    }

    [Fact]
    public void NegativeEatenIsTreatedAsZero()
    {
        Assert.Equal(Difficulty.ForEaten(0), Difficulty.ForEaten(-5));
    }
}
```

- [x] **Step 2: Run to verify it fails**

Run: `dotnet test`
Expected: FAIL — `Difficulty` does not exist.

- [x] **Step 3: Implement Difficulty**

Create `Core/Difficulty.cs`:

```csharp
using System;

namespace CrocGame.Core;

/// <summary>
/// Every escalation lever, as a pure function of how many items the player has eaten.
/// This is the single tuning surface for the whole game.
/// </summary>
public readonly record struct Difficulty(
    float BeltSpeed,
    float SpacingMin,
    float SpacingMax,
    float StutterWeight,
    float BounceWeight,
    float InedibleChance)
{
    public static Difficulty ForEaten(int eaten)
    {
        var e = MathF.Max(0f, eaten);

        var speed = Lerp(40f, 220f, e / 60f);
        var spacingMin = Lerp(1.10f, 0.35f, e / 60f);
        var jitter = Lerp(0.10f, 0.35f, e / 40f);

        return new Difficulty(
            BeltSpeed: speed,
            SpacingMin: spacingMin,
            SpacingMax: spacingMin * (1f + jitter),
            StutterWeight: e < 15 ? 0f : Lerp(0.15f, 0.45f, (e - 15f) / 45f),
            BounceWeight: e < 30 ? 0f : Lerp(0.10f, 0.35f, (e - 30f) / 40f),
            InedibleChance: e < 25 ? 0f : Lerp(0.04f, 0.20f, (e - 25f) / 45f));
    }

    /// <summary>Linear interpolation clamped to [a, b]. t below 0 or above 1 saturates.</summary>
    private static float Lerp(float a, float b, float t) =>
        a + (b - a) * Math.Clamp(t, 0f, 1f);
}
```

- [x] **Step 4: Run to verify it passes**

Run: `dotnet test`
Expected: PASS — every test green, including the new ones from this task.

- [x] **Step 5: Commit**

```bash
git add Core/Difficulty.cs Tests/DifficultyTests.cs
git commit -m "feat: difficulty curve as a pure function of items eaten"
```

---

### Task 6: Random source and the food table

**Files:**
- Create: `Core/RandomSource.cs`, `Core/FoodTable.cs`, `Resources/food.json`
- Create: `Tests/FoodTableTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `IRandomSource` with `float NextFloat()` and `int NextInt(int maxExclusive)`; `SeededRandom(int seed)`; `FoodType` record; `FoodTable.FromJson(string)`, `FoodTable.Types`, `FoodTable.Available(int eaten, bool edible)`.

- [x] **Step 1: Write the food table data**

The five edible foods already have 16x16 sprites in `Art/ExportedSprites/`. Widths are the full sprite width; `HalfWidth` is derived later by the spawn director. Create `Resources/food.json`:

```json
[
  { "id": "hotdog", "width": 16, "edible": true,  "movement": "constant", "score": 10, "minEatenToAppear": 0 },
  { "id": "pizza",  "width": 16, "edible": true,  "movement": "constant", "score": 10, "minEatenToAppear": 0 },
  { "id": "burger", "width": 16, "edible": true,  "movement": "constant", "score": 10, "minEatenToAppear": 0 },
  { "id": "donut",  "width": 16, "edible": true,  "movement": "constant", "score": 15, "minEatenToAppear": 5 },
  { "id": "pie",    "width": 16, "edible": true,  "movement": "constant", "score": 15, "minEatenToAppear": 5 }
]
```

Inedible entries are added in the follow-up plan, once their sprites exist. `FoodTable.Available(eaten, edible: false)` returning empty is a supported state and the spawn director must handle it.

- [x] **Step 2: Write the failing tests**

Create `Tests/FoodTableTests.cs`:

```csharp
using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class FoodTableTests
{
    private const string Json = """
    [
      { "id": "hotdog", "width": 16, "edible": true,  "movement": "constant", "score": 10, "minEatenToAppear": 0 },
      { "id": "donut",  "width": 16, "edible": true,  "movement": "constant", "score": 15, "minEatenToAppear": 5 },
      { "id": "boot",   "width": 16, "edible": false, "movement": "stutter",  "score": 0,  "minEatenToAppear": 25 }
    ]
    """;

    [Fact]
    public void ParsesEveryEntry()
    {
        Assert.Equal(3, FoodTable.FromJson(Json).Types.Count);
    }

    [Fact]
    public void ReadsFieldsOffAnEntry()
    {
        var hotdog = FoodTable.FromJson(Json).Types.Single(t => t.Id == "hotdog");

        Assert.Equal(16f, hotdog.Width, precision: 2);
        Assert.True(hotdog.Edible);
        Assert.Equal("constant", hotdog.Movement);
        Assert.Equal(10, hotdog.Score);
        Assert.Equal(0, hotdog.MinEatenToAppear);
    }

    [Fact]
    public void AvailableGatesOnMinEatenToAppear()
    {
        var table = FoodTable.FromJson(Json);

        Assert.Single(table.Available(eaten: 0, edible: true));
        Assert.Equal(2, table.Available(eaten: 5, edible: true).Count);
    }

    [Fact]
    public void AvailableSeparatesEdibleFromInedible()
    {
        var table = FoodTable.FromJson(Json);

        Assert.All(table.Available(eaten: 100, edible: true), t => Assert.True(t.Edible));
        Assert.All(table.Available(eaten: 100, edible: false), t => Assert.False(t.Edible));
    }

    [Fact]
    public void AvailableIsEmptyWhenNothingQualifiesYet()
    {
        var table = FoodTable.FromJson(Json);
        Assert.Empty(table.Available(eaten: 0, edible: false));
    }

    [Fact]
    public void SeededRandomIsReproducible()
    {
        var a = new SeededRandom(1234);
        var b = new SeededRandom(1234);

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(a.NextFloat(), b.NextFloat(), precision: 6);
            Assert.Equal(a.NextInt(100), b.NextInt(100));
        }
    }

    [Fact]
    public void SeededRandomStaysInRange()
    {
        var rng = new SeededRandom(99);

        for (var i = 0; i < 500; i++)
        {
            var f = rng.NextFloat();
            Assert.InRange(f, 0f, 1f);
            Assert.InRange(rng.NextInt(7), 0, 6);
        }
    }
}
```

- [x] **Step 3: Run to verify it fails**

Run: `dotnet test`
Expected: FAIL — `FoodTable` and `SeededRandom` do not exist.

- [x] **Step 4: Implement the random source**

Injecting randomness is what makes a run reproducible from a seed, which is what makes the spawn director testable at all. Create `Core/RandomSource.cs`:

```csharp
using System;

namespace CrocGame.Core;

/// <summary>Randomness as a dependency, so a seed reproduces a run exactly.</summary>
public interface IRandomSource
{
    /// <summary>A value in [0, 1).</summary>
    float NextFloat();

    /// <summary>A value in [0, maxExclusive).</summary>
    int NextInt(int maxExclusive);
}

public sealed class SeededRandom : IRandomSource
{
    private readonly Random _random;

    public SeededRandom(int seed) => _random = new Random(seed);

    public float NextFloat() => (float)_random.NextDouble();

    public int NextInt(int maxExclusive) => _random.Next(maxExclusive);
}
```

- [x] **Step 5: Implement the food table**

Create `Core/FoodTable.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CrocGame.Core;

/// <summary>
/// One row of food.json. The table decides what may appear; Difficulty decides how
/// often. Movement here is the type's default; the spawn director may substitute a
/// harder strategy as difficulty rises.
/// </summary>
public sealed record FoodType(
    string Id,
    float Width,
    bool Edible,
    string Movement,
    int Score,
    int MinEatenToAppear);

public sealed class FoodTable
{
    private static readonly JsonSerializerOptions Options =
        new() { PropertyNameCaseInsensitive = true };

    private readonly List<FoodType> _types;

    private FoodTable(List<FoodType> types) => _types = types;

    public IReadOnlyList<FoodType> Types => _types;

    public static FoodTable FromJson(string json) =>
        new(JsonSerializer.Deserialize<List<FoodType>>(json, Options) ?? new List<FoodType>());

    /// <summary>Types eligible to spawn right now. May legitimately be empty.</summary>
    public IReadOnlyList<FoodType> Available(int eaten, bool edible) =>
        _types.Where(t => t.Edible == edible && eaten >= t.MinEatenToAppear).ToList();
}
```

- [x] **Step 6: Run to verify it passes**

Run: `dotnet test`
Expected: PASS — every test green, including the new ones from this task.

- [x] **Step 7: Commit**

```bash
git add Core/RandomSource.cs Core/FoodTable.cs Resources/food.json Tests/FoodTableTests.cs
git commit -m "feat: seeded random source and food table"
```

---

### Task 7: The spawn director

**Files:**
- Create: `Core/SpawnDirector.cs`
- Create: `Tests/SpawnDirectorTests.cs`

**Interfaces:**
- Consumes: `FoodTable`, `IRandomSource`, `Difficulty`, `FoodItem`, `Movement.ByName`.
- Produces: `SpawnDirector(FoodTable table, IRandomSource rng, float spawnX)`, `SpawnDirector.Tick(float dt, int eaten)` returning `FoodItem?`.

**Movement selection rule:** the director rolls once per spawn. Below `StutterWeight` the item gets `Movement.Stutter`; below `StutterWeight + BounceWeight` it gets `Movement.Bounce`; otherwise it keeps the movement its `FoodType` declares. This is why a food's declared movement is a default rather than a guarantee.

- [x] **Step 1: Write the failing tests**

Create `Tests/SpawnDirectorTests.cs`:

```csharp
using System.Collections.Generic;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class SpawnDirectorTests
{
    private const string Json = """
    [
      { "id": "hotdog", "width": 16, "edible": true,  "movement": "constant", "score": 10, "minEatenToAppear": 0 },
      { "id": "boot",   "width": 16, "edible": false, "movement": "constant", "score": 0,  "minEatenToAppear": 25 }
    ]
    """;

    private static SpawnDirector Director(int seed = 7) =>
        new(FoodTable.FromJson(Json), new SeededRandom(seed), spawnX: -20f);

    private static List<FoodItem> RunFor(SpawnDirector director, float seconds, int eaten, float dt = 1f / 60f)
    {
        var spawned = new List<FoodItem>();
        for (var t = 0f; t < seconds; t += dt)
        {
            var item = director.Tick(dt, eaten);
            if (item is not null) spawned.Add(item);
        }
        return spawned;
    }

    [Fact]
    public void SpawnsNothingOnTheVeryFirstTick()
    {
        Assert.Null(Director().Tick(1f / 60f, eaten: 0));
    }

    [Fact]
    public void EventuallySpawns()
    {
        Assert.NotEmpty(RunFor(Director(), seconds: 10f, eaten: 0));
    }

    [Fact]
    public void SpawnsAtTheSpawnX()
    {
        var items = RunFor(Director(), seconds: 10f, eaten: 0);
        Assert.All(items, i => Assert.Equal(-20f, i.X, precision: 2));
    }

    [Fact]
    public void AssignsHalfWidthFromTypeWidth()
    {
        var items = RunFor(Director(), seconds: 10f, eaten: 0);
        Assert.All(items, i => Assert.Equal(8f, i.HalfWidth, precision: 2));
    }

    [Fact]
    public void AssignsUniqueIds()
    {
        var items = RunFor(Director(), seconds: 30f, eaten: 0);
        Assert.Equal(items.Count, new HashSet<int>(items.ConvertAll(i => i.Id)).Count);
    }

    [Fact]
    public void SameSeedProducesTheSameRun()
    {
        var a = RunFor(Director(seed: 42), seconds: 30f, eaten: 30);
        var b = RunFor(Director(seed: 42), seconds: 30f, eaten: 30);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].TypeId, b[i].TypeId);
            Assert.Equal(a[i].IsEdible, b[i].IsEdible);
        }
    }

    [Fact]
    public void DifferentSeedsDiverge()
    {
        var a = RunFor(Director(seed: 1), seconds: 60f, eaten: 40);
        var b = RunFor(Director(seed: 2), seconds: 60f, eaten: 40);

        var sameTypes = a.Count == b.Count;
        if (sameTypes)
        {
            var identical = true;
            for (var i = 0; i < a.Count && identical; i++)
            {
                identical = a[i].TypeId == b[i].TypeId;
            }
            Assert.False(identical, "two different seeds produced an identical sequence");
        }
    }

    [Fact]
    public void NeverSpawnsInediblesBeforeTheirThreshold()
    {
        var items = RunFor(Director(), seconds: 60f, eaten: 10);
        Assert.All(items, i => Assert.True(i.IsEdible));
    }

    [Fact]
    public void SpawnsFasterAtHigherDifficulty()
    {
        var early = RunFor(Director(seed: 5), seconds: 30f, eaten: 0).Count;
        var late = RunFor(Director(seed: 5), seconds: 30f, eaten: 60).Count;

        Assert.True(late > early, $"expected more spawns when escalated: {early} then {late}");
    }
}
```

- [x] **Step 2: Run to verify it fails**

Run: `dotnet test`
Expected: FAIL — `SpawnDirector` does not exist.

- [x] **Step 3: Implement SpawnDirector**

Create `Core/SpawnDirector.cs`:

```csharp
using System;

namespace CrocGame.Core;

/// <summary>
/// Decides when the next item appears and what it is. All randomness comes through
/// IRandomSource, so a seed reproduces a run exactly.
/// </summary>
public sealed class SpawnDirector
{
    private readonly FoodTable _table;
    private readonly IRandomSource _rng;
    private readonly float _spawnX;

    private float _secondsUntilNext;
    private int _nextId = 1;

    public SpawnDirector(FoodTable table, IRandomSource rng, float spawnX)
    {
        _table = table;
        _rng = rng;
        _spawnX = spawnX;
        _secondsUntilNext = Difficulty.ForEaten(0).SpacingMax;
    }

    /// <summary>Returns an item to place on the belt, or null if it is not time yet.</summary>
    public FoodItem? Tick(float dt, int eaten)
    {
        _secondsUntilNext -= dt;
        if (_secondsUntilNext > 0f) return null;

        var difficulty = Difficulty.ForEaten(eaten);
        _secondsUntilNext = Spacing(difficulty);

        var wantInedible = difficulty.InedibleChance > 0f
                           && _rng.NextFloat() < difficulty.InedibleChance;

        var candidates = _table.Available(eaten, edible: !wantInedible);
        if (candidates.Count == 0)
        {
            candidates = _table.Available(eaten, edible: true);
            if (candidates.Count == 0) return null;
        }

        var type = candidates[_rng.NextInt(candidates.Count)];

        return new FoodItem(
            id: _nextId++,
            typeId: type.Id,
            x: _spawnX,
            halfWidth: type.Width / 2f,
            isEdible: type.Edible,
            score: type.Score,
            movement: SelectMovement(type, difficulty));
    }

    private float Spacing(Difficulty d) =>
        d.SpacingMin + (d.SpacingMax - d.SpacingMin) * _rng.NextFloat();

    /// <summary>
    /// The type's declared movement is a default. As difficulty rises, the director
    /// substitutes a harder strategy.
    /// </summary>
    private IMovement SelectMovement(FoodType type, Difficulty d)
    {
        var roll = _rng.NextFloat();

        if (roll < d.StutterWeight) return Movement.Stutter;
        if (roll < d.StutterWeight + d.BounceWeight) return Movement.Bounce;

        return Movement.ByName(type.Movement);
    }
}
```

- [x] **Step 4: Run to verify it passes**

Run: `dotnet test`
Expected: PASS — every test green, including the new ones from this task.

- [x] **Step 5: Commit**

```bash
git add Core/SpawnDirector.cs Tests/SpawnDirectorTests.cs
git commit -m "feat: seeded spawn director driven by the difficulty curve"
```

---

### Task 8: Chomp judging and run state

**Files:**
- Create: `Core/ChompJudge.cs`, `Core/RunState.cs`
- Create: `Tests/ChompJudgeTests.cs`, `Tests/RunStateTests.cs`

**Interfaces:**
- Consumes: `JawZone`, `FoodItem`, `Belt`.
- Produces: `ChompOutcome` enum (`Hit`, `Air`), `ChompResult(ChompOutcome Outcome, FoodItem? Item)`, `ChompJudge.Judge(JawZone, IReadOnlyList<FoodItem>)`; `RunState` with `Score`, `Combo`, `Strikes`, `Eaten`, `Elapsed`, `IsOver`, `MaxStrikes`, `AddElapsed(float)`, `RegisterHit(int)`, `RegisterStrike()`.

**Scoring rule:** a hit increments the combo first, then adds `item.Score * min(Combo, 5)`. The multiplier caps at 5 so a long run's score stays a function of survival rather than of one unbroken streak.

- [x] **Step 1: Write the failing judge tests**

Create `Tests/ChompJudgeTests.cs`:

```csharp
using System.Collections.Generic;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class ChompJudgeTests
{
    private static readonly JawZone Jaw = new JawZone(Center: 100f, HalfWidth: 12f);

    private static FoodItem Item(int id, float x, bool edible = true) =>
        new FoodItem(id, "hotdog", x, halfWidth: 8f, isEdible: edible, score: 10,
                     movement: Movement.Constant);

    [Fact]
    public void EmptyBeltIsAnAirChomp()
    {
        var result = ChompJudge.Judge(Jaw, new List<FoodItem>());

        Assert.Equal(ChompOutcome.Air, result.Outcome);
        Assert.Null(result.Item);
    }

    [Fact]
    public void ItemOutsideTheZoneIsAnAirChomp()
    {
        var result = ChompJudge.Judge(Jaw, new List<FoodItem> { Item(1, x: 10f) });

        Assert.Equal(ChompOutcome.Air, result.Outcome);
    }

    [Fact]
    public void ItemInsideTheZoneIsAHit()
    {
        var result = ChompJudge.Judge(Jaw, new List<FoodItem> { Item(1, x: 100f) });

        Assert.Equal(ChompOutcome.Hit, result.Outcome);
        Assert.Equal(1, result.Item!.Id);
    }

    [Fact]
    public void InedibleItemInTheZoneIsStillAHit()
    {
        // The judge reports what was bitten. Whether that costs a strike is the
        // session's decision, not the judge's.
        var result = ChompJudge.Judge(Jaw, new List<FoodItem> { Item(1, x: 100f, edible: false) });

        Assert.Equal(ChompOutcome.Hit, result.Outcome);
        Assert.False(result.Item!.IsEdible);
    }

    [Fact]
    public void NearestToCentreWinsWhenTwoItemsOverlap()
    {
        var far = Item(1, x: 108f);
        var near = Item(2, x: 102f);

        var result = ChompJudge.Judge(Jaw, new List<FoodItem> { far, near });

        Assert.Equal(2, result.Item!.Id);
    }

    [Fact]
    public void NearestToCentreWinsRegardlessOfListOrder()
    {
        var near = Item(2, x: 102f);
        var far = Item(1, x: 108f);

        var result = ChompJudge.Judge(Jaw, new List<FoodItem> { near, far });

        Assert.Equal(2, result.Item!.Id);
    }
}
```

- [x] **Step 2: Run to verify it fails**

Run: `dotnet test`
Expected: FAIL — `ChompJudge` does not exist.

- [x] **Step 3: Implement ChompJudge**

Create `Core/ChompJudge.cs`:

```csharp
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
```

- [x] **Step 4: Run to verify it passes**

Run: `dotnet test`
Expected: PASS — every test green, including the new ones from this task.

- [x] **Step 5: Write the failing run state tests**

Create `Tests/RunStateTests.cs`:

```csharp
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class RunStateTests
{
    [Fact]
    public void StartsEmpty()
    {
        var state = new RunState();

        Assert.Equal(0, state.Score);
        Assert.Equal(0, state.Combo);
        Assert.Equal(0, state.Strikes);
        Assert.Equal(0, state.Eaten);
        Assert.False(state.IsOver);
    }

    [Fact]
    public void HitIncrementsComboAndEatenAndScores()
    {
        var state = new RunState();

        state.RegisterHit(10);

        Assert.Equal(1, state.Combo);
        Assert.Equal(1, state.Eaten);
        Assert.Equal(10, state.Score);
    }

    [Fact]
    public void ComboMultipliesTheScore()
    {
        var state = new RunState();

        state.RegisterHit(10);  // combo 1 -> +10
        state.RegisterHit(10);  // combo 2 -> +20

        Assert.Equal(30, state.Score);
    }

    [Fact]
    public void ComboMultiplierCapsAtFive()
    {
        var state = new RunState();

        for (var i = 0; i < 8; i++) state.RegisterHit(10);

        // 10 + 20 + 30 + 40 + 50 + 50 + 50 + 50
        Assert.Equal(300, state.Score);
        Assert.Equal(8, state.Combo);
    }

    [Fact]
    public void StrikeResetsTheCombo()
    {
        var state = new RunState();
        state.RegisterHit(10);
        state.RegisterHit(10);

        state.RegisterStrike();

        Assert.Equal(0, state.Combo);
        Assert.Equal(1, state.Strikes);
    }

    [Fact]
    public void StrikeDoesNotReduceTheScoreOrEatenCount()
    {
        var state = new RunState();
        state.RegisterHit(10);

        state.RegisterStrike();

        Assert.Equal(10, state.Score);
        Assert.Equal(1, state.Eaten);
    }

    [Fact]
    public void ThirdStrikeEndsTheRun()
    {
        var state = new RunState();

        state.RegisterStrike();
        state.RegisterStrike();
        Assert.False(state.IsOver);

        state.RegisterStrike();
        Assert.True(state.IsOver);
    }

    [Fact]
    public void ElapsedAccumulates()
    {
        var state = new RunState();

        state.AddElapsed(0.5f);
        state.AddElapsed(0.25f);

        Assert.Equal(0.75f, state.Elapsed, precision: 4);
    }
}
```

- [x] **Step 6: Run to verify it fails**

Run: `dotnet test`
Expected: FAIL — `RunState` does not exist.

- [x] **Step 7: Implement RunState**

Create `Core/RunState.cs`:

```csharp
using System;

namespace CrocGame.Core;

/// <summary>Everything that resets when a run starts over.</summary>
public sealed class RunState
{
    public const int MaxStrikes = 3;
    private const int MaxComboMultiplier = 5;

    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int Strikes { get; private set; }
    public int Eaten { get; private set; }
    public float Elapsed { get; private set; }

    public bool IsOver => Strikes >= MaxStrikes;

    public void AddElapsed(float dt) => Elapsed += dt;

    public void RegisterHit(int score)
    {
        Combo++;
        Eaten++;
        Score += score * Math.Min(Combo, MaxComboMultiplier);
    }

    public void RegisterStrike()
    {
        Strikes++;
        Combo = 0;
    }
}
```

- [x] **Step 8: Run to verify it passes**

Run: `dotnet test`
Expected: PASS — every test green, including the new ones from this task.

- [x] **Step 9: Commit**

```bash
git add Core/ChompJudge.cs Core/RunState.cs Tests/ChompJudgeTests.cs Tests/RunStateTests.cs
git commit -m "feat: chomp judging and run state scoring"
```

---

### Task 9: The GameSession façade

**Files:**
- Create: `Core/GameEvent.cs`, `Core/GameSession.cs`
- Create: `Tests/GameSessionTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 2-8.
- Produces: `GameEvent` and its subtypes; `GameSession(FoodTable, IRandomSource, JawZone, float spawnX, float retireX)`, `GameSession.State`, `GameSession.Items`, `GameSession.Tick(float dt)`, `GameSession.Chomp()`, both returning `IReadOnlyList<GameEvent>`.

- [x] **Step 1: Write the event types**

Create `Core/GameEvent.cs`:

```csharp
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
```

- [x] **Step 2: Write the failing tests**

Create `Tests/GameSessionTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class GameSessionTests
{
    private const string Json = """
    [ { "id": "hotdog", "width": 16, "edible": true, "movement": "constant", "score": 10, "minEatenToAppear": 0 } ]
    """;

    private static GameSession Session(int seed = 3) =>
        new(FoodTable.FromJson(Json), new SeededRandom(seed),
            jaw: new JawZone(Center: 100f, HalfWidth: 12f),
            spawnX: -20f, retireX: 200f);

    /// <summary>Runs the session until an item is sitting inside the jaw zone.</summary>
    private static GameSession SessionWithItemInJaws()
    {
        var session = Session();
        for (var i = 0; i < 10_000; i++)
        {
            session.Tick(1f / 60f);
            if (session.Items.Any(item => new JawZone(100f, 12f).Overlaps(item))) return session;
            if (session.State.IsOver) break;
        }

        Assert.Fail("no item reached the jaw zone");
        return session;
    }

    [Fact]
    public void TickEmitsSpawnedWhenAnItemAppears()
    {
        var session = Session();
        var spawned = new List<GameEvent>();

        for (var i = 0; i < 600; i++) spawned.AddRange(session.Tick(1f / 60f));

        Assert.Contains(spawned, e => e is Spawned);
    }

    [Fact]
    public void ChompingAirCostsAStrike()
    {
        var session = Session();

        var events = session.Chomp();

        Assert.Contains(events, e => e is ChompedAir);
        Assert.Contains(events, e => e is StrikeAdded);
        Assert.Equal(1, session.State.Strikes);
    }

    [Fact]
    public void ChompingAnItemInTheJawsScores()
    {
        var session = SessionWithItemInJaws();

        var events = session.Chomp();

        Assert.Contains(events, e => e is Chomped);
        Assert.DoesNotContain(events, e => e is StrikeAdded);
        Assert.True(session.State.Score > 0);
        Assert.Equal(1, session.State.Eaten);
    }

    [Fact]
    public void AChompedItemLeavesTheBelt()
    {
        var session = SessionWithItemInJaws();
        var before = session.Items.Count;

        session.Chomp();

        Assert.Equal(before - 1, session.Items.Count);
    }

    [Fact]
    public void LettingEdibleFoodPassCostsAStrike()
    {
        var session = Session();
        var events = new List<GameEvent>();

        for (var i = 0; i < 3_000 && session.State.Strikes == 0; i++)
        {
            events.AddRange(session.Tick(1f / 60f));
        }

        Assert.Contains(events, e => e is Passed);
        Assert.Equal(1, session.State.Strikes);
    }

    [Fact]
    public void ThreeStrikesEndsTheRunAndEmitsRunEndedOnce()
    {
        var session = Session();
        var events = new List<GameEvent>();

        events.AddRange(session.Chomp());
        events.AddRange(session.Chomp());
        Assert.False(session.State.IsOver);

        events.AddRange(session.Chomp());

        Assert.True(session.State.IsOver);
        Assert.Single(events.OfType<RunEnded>());
    }

    [Fact]
    public void TickDoesNothingAfterTheRunEnds()
    {
        var session = Session();
        session.Chomp();
        session.Chomp();
        session.Chomp();

        var elapsedAtEnd = session.State.Elapsed;
        var events = session.Tick(1f / 60f);

        Assert.Empty(events);
        Assert.Equal(elapsedAtEnd, session.State.Elapsed, precision: 4);
    }

    [Fact]
    public void ChompDoesNothingAfterTheRunEnds()
    {
        var session = Session();
        session.Chomp();
        session.Chomp();
        session.Chomp();

        Assert.Empty(session.Chomp());
        Assert.Equal(3, session.State.Strikes);
    }

    [Fact]
    public void SuspendingTicksDoesNotChangeJudgingWhenTicksResume()
    {
        // This is the hit-stop guarantee: the scene layer freezes the session by not
        // calling Tick. Because Core has no wall clock, a gap cannot consume part of
        // a later timing window.
        var a = Session(seed: 11);
        var b = Session(seed: 11);

        for (var i = 0; i < 300; i++) a.Tick(1f / 60f);

        for (var i = 0; i < 150; i++) b.Tick(1f / 60f);
        // b is "frozen" here: no Tick calls at all for a while.
        for (var i = 0; i < 150; i++) b.Tick(1f / 60f);

        Assert.Equal(a.Items.Count, b.Items.Count);
        for (var i = 0; i < a.Items.Count; i++)
        {
            Assert.Equal(a.Items[i].X, b.Items[i].X, precision: 3);
            Assert.Equal(a.Items[i].TypeId, b.Items[i].TypeId);
        }
    }
}
```

- [x] **Step 3: Run to verify it fails**

Run: `dotnet test`
Expected: FAIL — `GameSession` does not exist.

- [x] **Step 4: Implement GameSession**

Create `Core/GameSession.cs`:

```csharp
using System.Collections.Generic;

namespace CrocGame.Core;

/// <summary>
/// The only surface the scene layer touches. Time arrives as dt; input arrives as
/// Chomp(). Everything that happened comes back as events.
/// </summary>
public sealed class GameSession
{
    private readonly Belt _belt;
    private readonly SpawnDirector _director;
    private readonly JawZone _jaw;
    private readonly List<GameEvent> _events = new();

    public GameSession(FoodTable table, IRandomSource rng, JawZone jaw,
                       float spawnX, float retireX)
    {
        _jaw = jaw;
        _belt = new Belt(retireX);
        _director = new SpawnDirector(table, rng, spawnX);
        State = new RunState();
    }

    public RunState State { get; }

    public IReadOnlyList<FoodItem> Items => _belt.Items;

    public IReadOnlyList<GameEvent> Tick(float dt)
    {
        _events.Clear();
        if (State.IsOver) return _events;

        State.AddElapsed(dt);

        var difficulty = Difficulty.ForEaten(State.Eaten);

        foreach (var retired in _belt.Advance(difficulty.BeltSpeed, dt))
        {
            if (!retired.IsEdible) continue;  // correct play: inedibles should pass

            _events.Add(new Passed(retired));
            AddStrike();
            if (State.IsOver) return _events;
        }

        var spawned = _director.Tick(dt, State.Eaten);
        if (spawned is not null)
        {
            _belt.Add(spawned);
            _events.Add(new Spawned(spawned));
        }

        return _events;
    }

    public IReadOnlyList<GameEvent> Chomp()
    {
        _events.Clear();
        if (State.IsOver) return _events;

        var result = ChompJudge.Judge(_jaw, _belt.Items);

        if (result.Outcome == ChompOutcome.Air)
        {
            _events.Add(new ChompedAir());
            AddStrike();
            return _events;
        }

        var item = result.Item!;
        _belt.Remove(item);

        if (item.IsEdible)
        {
            var before = State.Score;
            State.RegisterHit(item.Score);
            _events.Add(new Chomped(item, State.Combo, State.Score - before));
        }
        else
        {
            _events.Add(new Chomped(item, 0, 0));
            AddStrike();
        }

        return _events;
    }

    private void AddStrike()
    {
        State.RegisterStrike();
        _events.Add(new StrikeAdded(State.Strikes));

        if (State.IsOver)
        {
            _events.Add(new RunEnded(State.Score, State.Eaten));
        }
    }
}
```

- [x] **Step 5: Run to verify it passes**

Run: `dotnet test`
Expected: PASS — every test green, including the new ones from this task.

- [x] **Step 6: Commit**

```bash
git add Core/GameEvent.cs Core/GameSession.cs Tests/GameSessionTests.cs
git commit -m "feat: GameSession facade emitting events for the scene layer"
```

---

### Task 10: Persistence

**Files:**
- Create: `Core/SaveData.cs`
- Create: `Tests/SaveDataTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `SaveData` (mutable class: `BestScore`, `LifetimeEaten`, `UnlockedIds`), `SaveData.ToJson()`, `SaveData.FromJson(string?)`, `ISaveStore` with `Load()`/`Save(SaveData)`, `InMemorySaveStore`.

`SaveData.FromJson` must never throw. A corrupt or missing save returns defaults, because losing a high score is annoying and crashing on launch is fatal.

- [x] **Step 1: Write the failing tests**

Create `Tests/SaveDataTests.cs`:

```csharp
using CrocGame.Core;
using Xunit;

namespace CrocGame.Core.Tests;

public class SaveDataTests
{
    [Fact]
    public void DefaultsAreEmpty()
    {
        var data = new SaveData();

        Assert.Equal(0, data.BestScore);
        Assert.Equal(0, data.LifetimeEaten);
        Assert.Empty(data.UnlockedIds);
    }

    [Fact]
    public void RoundTripsThroughJson()
    {
        var data = new SaveData { BestScore = 420, LifetimeEaten = 99 };
        data.UnlockedIds.Add("skin_gold");

        var restored = SaveData.FromJson(data.ToJson());

        Assert.Equal(420, restored.BestScore);
        Assert.Equal(99, restored.LifetimeEaten);
        Assert.Contains("skin_gold", restored.UnlockedIds);
    }

    [Fact]
    public void RoundTripsAnEmptyUnlockSet()
    {
        var restored = SaveData.FromJson(new SaveData { BestScore = 7 }.ToJson());

        Assert.Equal(7, restored.BestScore);
        Assert.Empty(restored.UnlockedIds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{ \"bestScore\": ")]
    [InlineData("[1,2,3]")]
    public void CorruptOrMissingSaveYieldsDefaultsInsteadOfThrowing(string? json)
    {
        var data = SaveData.FromJson(json);

        Assert.Equal(0, data.BestScore);
        Assert.Empty(data.UnlockedIds);
    }

    [Fact]
    public void InMemoryStoreReturnsWhatWasSaved()
    {
        var store = new InMemorySaveStore();
        store.Save(new SaveData { BestScore = 55 });

        Assert.Equal(55, store.Load().BestScore);
    }

    [Fact]
    public void InMemoryStoreStartsWithDefaults()
    {
        Assert.Equal(0, new InMemorySaveStore().Load().BestScore);
    }
}
```

- [x] **Step 2: Run to verify it fails**

Run: `dotnet test`
Expected: FAIL — `SaveData` does not exist.

- [x] **Step 3: Implement SaveData**

Create `Core/SaveData.cs`:

```csharp
using System.Collections.Generic;
using System.Text.Json;

namespace CrocGame.Core;

/// <summary>What survives between runs. Cosmetic unlocks only: nothing here changes
/// difficulty, scoring, or the timing window.</summary>
public sealed class SaveData
{
    public int BestScore { get; set; }
    public int LifetimeEaten { get; set; }
    public List<string> UnlockedIds { get; set; } = new();

    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>
    /// Never throws. A corrupt or missing save costs the player their high score;
    /// crashing on launch costs them the game.
    /// </summary>
    public static SaveData FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new SaveData();

        try
        {
            return JsonSerializer.Deserialize<SaveData>(json) ?? new SaveData();
        }
        catch (JsonException)
        {
            return new SaveData();
        }
    }
}

public interface ISaveStore
{
    SaveData Load();
    void Save(SaveData data);
}

/// <summary>Test double. The Godot layer supplies the user:// implementation.</summary>
public sealed class InMemorySaveStore : ISaveStore
{
    private SaveData _data = new();

    public SaveData Load() => _data;

    public void Save(SaveData data) => _data = data;
}
```

- [x] **Step 4: Run to verify it passes**

Run: `dotnet test`
Expected: PASS — every test green, including the new ones from this task.

- [x] **Step 5: Commit**

```bash
git add Core/SaveData.cs Tests/SaveDataTests.cs
git commit -m "feat: save data that degrades to defaults instead of throwing"
```

---

## Godot layer (Tasks 11-13)

**A note on how these tasks are built.** There is no Godot binary on the development
machine, so hand-authored `.tscn` text cannot be verified before you open the editor.
To keep that risk small, the scene tree is built **in C# at runtime** and `Main.tscn`
is a single node with one script attached. Every task below therefore ends with a
manual verification step you run in the Godot editor, not an automated one.

Godot's own C# project file is normally generated by the editor on first build. If
`CrocGame.csproj` already exists when you start Task 11, do not overwrite it — add
only the `ProjectReference` to Core.

---

### Task 11: Wire Godot to Core and boot a session

**Files:**
- Create: `CrocGame.csproj`, `Scenes/Main.tscn`, `Scripts/GameRoot.cs`, `Scripts/GodotSaveStore.cs`
- Modify: `project.godot`, `CrocGame.slnx`

**Interfaces:**
- Consumes: `GameSession`, `FoodTable`, `SeededRandom`, `JawZone`, `ISaveStore`, `SaveData` from Core.
- Produces: `CrocGame.GameRoot` (Godot `Node2D`), `CrocGame.GodotSaveStore : ISaveStore`.

- [x] **Step 1: Create the Godot C# project file**

Create `CrocGame.csproj`:

```xml
<Project Sdk="Godot.NET.Sdk/4.7.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <RootNamespace>CrocGame</RootNamespace>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <!--
    This project file lives at the repo root, so the SDK's default globbing would
    otherwise compile Core/, Tests/, and Godot's generated sources under .godot/ into
    this assembly - producing duplicate assembly attributes and dragging xunit in as
    a game dependency. The scene layer is Scripts/ and nothing else.
  -->
  <ItemGroup>
    <Compile Remove="Core/**" />
    <Compile Remove="Tests/**" />
    <Compile Remove=".godot/**" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="Core/CrocGame.Core.csproj" />
  </ItemGroup>
</Project>
```

- [x] **Step 2: Verify it restores**

Run: `dotnet restore CrocGame.csproj`
Expected: restore succeeds, pulling `Godot.NET.Sdk` from nuget.org.

If restore fails because the machine is offline, stop and resolve that before
continuing — but do **not** add `CrocGame.csproj` to `CrocGame.slnx` in that case, so
that `dotnet test` keeps working for the Core suite.

If it restores cleanly, add it to the solution:

```bash
dotnet sln CrocGame.slnx add CrocGame.csproj
dotnet test    # the Core suite must still pass
```

- [x] **Step 3: Make food.json reachable at runtime**

Core loads the table from a string, so the Godot layer reads the file. Godot only
exports files it knows about; `Resources/food.json` is inside the project directory,
so `res://Resources/food.json` resolves. Confirm the file is at
`/home/ydnaiq/Projects/physics-game/Resources/food.json` from Task 6.

- [x] **Step 4: Write the save store**

Create `Scripts/GodotSaveStore.cs`:

```csharp
using CrocGame.Core;
using Godot;

namespace CrocGame;

/// <summary>Persists to user://crocgame.save. Never throws: a bad save is a fresh save.</summary>
public sealed class GodotSaveStore : ISaveStore
{
    private const string Path = "user://crocgame.save";

    public SaveData Load()
    {
        if (!FileAccess.FileExists(Path)) return new SaveData();

        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PushWarning($"Could not open {Path}; starting from defaults.");
            return new SaveData();
        }

        return SaveData.FromJson(file.GetAsText());
    }

    public void Save(SaveData data)
    {
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushWarning($"Could not write {Path}; progress not saved.");
            return;
        }

        file.StoreString(data.ToJson());
    }
}
```

- [x] **Step 5: Write the game root**

The belt runs across the lower third of the 320x180 canvas. The jaws sit at x=100,
which is `JawZone.Center`; items spawn off-screen left and retire off-screen right.

Create `Scripts/GameRoot.cs`:

```csharp
using CrocGame.Core;
using Godot;

namespace CrocGame;

public partial class GameRoot : Node2D
{
    public const float JawCenterX = 100f;
    public const float JawHalfWidth = 12f;
    public const float SpawnX = -20f;
    public const float RetireX = 340f;
    public const float BeltY = 120f;

    private GameSession _session = null!;
    private ISaveStore _saveStore = null!;
    private FoodTable _foodTable = null!;

    public override void _Ready()
    {
        _saveStore = new GodotSaveStore();
        _foodTable = LoadFoodTable();
        StartRun();
    }

    private static FoodTable LoadFoodTable()
    {
        using var file = FileAccess.Open("res://Resources/food.json", FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PushError("Could not read res://Resources/food.json");
            return FoodTable.FromJson("[]");
        }

        return FoodTable.FromJson(file.GetAsText());
    }

    private void StartRun()
    {
        var seed = (int)(Time.GetTicksMsec() & 0x7FFFFFFF);

        _session = new GameSession(
            _foodTable,
            new SeededRandom(seed),
            new JawZone(JawCenterX, JawHalfWidth),
            SpawnX,
            RetireX);

        GD.Print($"Run started. Best so far: {_saveStore.Load().BestScore}");
    }

    public override void _Process(double delta)
    {
        foreach (var evt in _session.Tick((float)delta))
        {
            GD.Print(evt);
        }
    }
}
```

- [x] **Step 6: Create the main scene**

Create `Scenes/Main.tscn`:

```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/GameRoot.cs" id="1"]

[node name="Main" type="Node2D"]
script = ExtResource("1")
```

- [x] **Step 7: Register the main scene**

Add to the `[application]` section of `project.godot`:

```
run/main_scene="res://Scenes/Main.tscn"
```

- [ ] **Step 8: Verify in the Godot editor**

Open the project in Godot and press Play. Expected: the window runs at 320x180 scaled
up, and the output panel prints a `Run started` line followed by a stream of
`Spawned { ... }` and eventually `Passed { ... }` / `StrikeAdded { ... }` records,
ending with `RunEnded`. Nothing is drawn yet — that is Task 12.

If the C# build fails inside Godot, the most likely cause is the target framework:
confirm `CrocGame.csproj` and `Core/CrocGame.Core.csproj` both say `net8.0`.

- [x] **Step 9: Commit**

```bash
git add CrocGame.csproj CrocGame.sln Scenes/Main.tscn Scripts/GameRoot.cs Scripts/GodotSaveStore.cs project.godot
git commit -m "feat: boot a Core game session from Godot"
```

---

### Task 12: Render the belt, the croc, and the HUD

**Files:**
- Create: `Scripts/BeltView.cs`, `Scripts/CrocView.cs`, `Scripts/Hud.cs`
- Modify: `Scripts/GameRoot.cs`
- Modify: `project.godot`

**Interfaces:**
- Consumes: `GameEvent` subtypes, `GameSession.Items` from Core; `GameRoot` constants.
- Produces: `CrocGame.BeltView` with `Sync(IReadOnlyList<FoodItem>)` and `Remove(int id)`; `CrocGame.CrocView` with `PlayEat()`, `PlayCelebrate()`; `CrocGame.Hud` with `Set(int score, int combo, int strikes)`.

**Frame layout of `croc_sheet.png`:** 18 frames of 32x32 in one row, in tag order —
`idle` at frames 0-3, `celebrate` at 4-11, `eat` at 12-17. This ordering comes from
`Art/ExportedSprites/croc_sheet.json`.

- [x] **Step 1: Add the chomp input action**

Add to `project.godot` (create the `[input]` section if it does not exist):

```
[input]

chomp={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":32,"physical_keycode":0,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null)
, Object(InputEventMouseButton,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"button_mask":0,"position":Vector2(0, 0),"global_position":Vector2(0, 0),"factor":1.0,"button_index":1,"canceled":false,"pressed":false,"double_click":false,"script":null)
, Object(InputEventScreenTouch,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"index":0,"position":Vector2(0, 0),"canceled":false,"pressed":false,"double_tap":false,"script":null)
]
}
```

If Godot rejects this literal on load, delete the block and add the three events by
hand in Project Settings, Input Map, action name `chomp`: Space, Left Mouse Button,
and a touch event.

- [x] **Step 2: Write the belt view**

Create `Scripts/BeltView.cs`:

```csharp
using System.Collections.Generic;
using CrocGame.Core;
using Godot;

namespace CrocGame;

/// <summary>Mirrors Core's item list into sprites. Owns no game state.</summary>
public partial class BeltView : Node2D
{
    private readonly Dictionary<int, Sprite2D> _sprites = new();
    private readonly Dictionary<string, Texture2D> _textures = new();

    public void Sync(IReadOnlyList<FoodItem> items)
    {
        foreach (var item in items)
        {
            if (!_sprites.TryGetValue(item.Id, out var sprite))
            {
                sprite = new Sprite2D { Texture = TextureFor(item.TypeId) };
                AddChild(sprite);
                _sprites[item.Id] = sprite;
            }

            sprite.Position = new Vector2(item.X, GameRoot.BeltY);
        }
    }

    public void Remove(int id)
    {
        if (!_sprites.Remove(id, out var sprite)) return;
        sprite.QueueFree();
    }

    /// <summary>Prunes sprites whose items left the belt without an explicit Remove.</summary>
    public void PruneMissing(IReadOnlyList<FoodItem> items)
    {
        var live = new HashSet<int>();
        foreach (var item in items) live.Add(item.Id);

        var stale = new List<int>();
        foreach (var id in _sprites.Keys)
        {
            if (!live.Contains(id)) stale.Add(id);
        }

        foreach (var id in stale) Remove(id);
    }

    private Texture2D TextureFor(string typeId)
    {
        if (_textures.TryGetValue(typeId, out var cached)) return cached;

        var path = $"res://Art/ExportedSprites/{typeId}.png";
        var texture = ResourceLoader.Load<Texture2D>(path);

        if (texture is null)
        {
            GD.PushWarning($"Missing food texture {path}; using a placeholder.");
            var image = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
            image.Fill(Colors.Magenta);
            texture = ImageTexture.CreateFromImage(image);
        }

        _textures[typeId] = texture;
        return texture;
    }
}
```

- [x] **Step 3: Write the croc view**

Create `Scripts/CrocView.cs`:

```csharp
using Godot;

namespace CrocGame;

/// <summary>
/// The jaws. Frame layout of croc_sheet.png, from croc_sheet.json:
/// idle 0-3, celebrate 4-11, eat 12-17, all 32x32 in a single row.
/// </summary>
public partial class CrocView : AnimatedSprite2D
{
    public override void _Ready()
    {
        var sheet = ResourceLoader.Load<Texture2D>("res://Art/ExportedSprites/croc_sheet.png");
        if (sheet is null)
        {
            GD.PushError("Missing res://Art/ExportedSprites/croc_sheet.png");
            return;
        }

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        AddAnimation(frames, sheet, "idle", first: 0, count: 4, fps: 5f, loop: true);
        AddAnimation(frames, sheet, "celebrate", first: 4, count: 8, fps: 12f, loop: false);
        AddAnimation(frames, sheet, "eat", first: 12, count: 6, fps: 18f, loop: false);

        SpriteFrames = frames;
        AnimationFinished += () => Play("idle");
        Play("idle");
    }

    private static void AddAnimation(SpriteFrames frames, Texture2D sheet, string name,
                                     int first, int count, float fps, bool loop)
    {
        frames.AddAnimation(name);
        frames.SetAnimationSpeed(name, fps);
        frames.SetAnimationLoop(name, loop);

        for (var i = 0; i < count; i++)
        {
            var atlas = new AtlasTexture
            {
                Atlas = sheet,
                Region = new Rect2((first + i) * 32, 0, 32, 32),
            };
            frames.AddFrame(name, atlas);
        }
    }

    public void PlayEat() => Play("eat");

    public void PlayCelebrate() => Play("celebrate");
}
```

- [x] **Step 4: Write the HUD**

Create `Scripts/Hud.cs`:

```csharp
using Godot;

namespace CrocGame;

public partial class Hud : Node2D
{
    private Label _label = null!;

    public override void _Ready()
    {
        _label = new Label
        {
            Position = new Vector2(4, 2),
            LabelSettings = new LabelSettings { FontSize = 12 },
        };
        AddChild(_label);
    }

    public void Set(int score, int combo, int strikes)
    {
        var pips = new string('X', strikes) + new string('.', 3 - strikes);
        _label.Text = $"{score}   x{combo}   [{pips}]";
    }
}
```

- [x] **Step 5: Wire them into GameRoot**

Replace `_Ready` and `_Process` in `Scripts/GameRoot.cs`, and add `_UnhandledInput`:

```csharp
    private BeltView _beltView = null!;
    private CrocView _crocView = null!;
    private Hud _hud = null!;
    private bool _chompQueued;

    public override void _Ready()
    {
        _saveStore = new GodotSaveStore();
        _foodTable = LoadFoodTable();

        _beltView = new BeltView();
        AddChild(_beltView);

        _crocView = new CrocView { Position = new Vector2(JawCenterX, BeltY) };
        AddChild(_crocView);

        _hud = new Hud();
        AddChild(_hud);

        StartRun();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Buffered rather than acted on immediately, so a press between frames is
        // never dropped and is judged against the same tick the player saw.
        if (@event.IsActionPressed("chomp")) _chompQueued = true;
    }

    public override void _Process(double delta)
    {
        if (_chompQueued)
        {
            _chompQueued = false;
            Render(_session.Chomp());
        }

        Render(_session.Tick((float)delta));

        _beltView.Sync(_session.Items);
        _beltView.PruneMissing(_session.Items);
        _hud.Set(_session.State.Score, _session.State.Combo, _session.State.Strikes);
    }

    private void Render(IReadOnlyList<GameEvent> events)
    {
        foreach (var evt in events)
        {
            switch (evt)
            {
                case Chomped chomped:
                    _crocView.PlayEat();
                    _beltView.Remove(chomped.Item.Id);
                    break;
                case ChompedAir:
                    _crocView.PlayEat();
                    break;
                case Passed passed:
                    _beltView.Remove(passed.Item.Id);
                    break;
                case RunEnded ended:
                    GD.Print($"Run ended: {ended.FinalScore} points, {ended.Eaten} eaten.");
                    break;
            }
        }
    }
```

Add `using System.Collections.Generic;` to the top of `GameRoot.cs`.

- [ ] **Step 6: Verify in the Godot editor**

Press Play. Expected:
- Food sprites slide left to right across the lower third of the screen.
- The croc idles at x=100 and plays its eat animation when you press Space, click, or tap.
- The HUD shows score, combo, and three strike pips.
- Chomping food in the jaws removes it and raises the score; chomping air adds a strike.
- Three strikes prints the run-ended line and everything stops.

If sprites look blurry, `default_texture_filter=0` did not take — recheck Task 1 Step 7.

- [x] **Step 7: Commit**

```bash
git add Scripts/BeltView.cs Scripts/CrocView.cs Scripts/Hud.cs Scripts/GameRoot.cs project.godot
git commit -m "feat: render belt, croc, and HUD from Core events"
```

---

### Task 13: Title, game over, and restart

**Files:**
- Modify: `Scripts/GameRoot.cs`
- Create: `Scripts/ScreenOverlay.cs`

**Interfaces:**
- Consumes: `SaveData`, `ISaveStore`, `RunEnded` from Core.
- Produces: `CrocGame.ScreenOverlay` with `Show(string title, string subtitle)` and `Hide()`; `GameRoot` gains a `Phase` enum (`Title`, `Running`, `GameOver`).

- [x] **Step 1: Write the overlay**

Create `Scripts/ScreenOverlay.cs`:

```csharp
using Godot;

namespace CrocGame;

/// <summary>Full-screen title and game-over text. No buttons: any chomp input advances.</summary>
public partial class ScreenOverlay : Node2D
{
    private Label _title = null!;
    private Label _subtitle = null!;

    public override void _Ready()
    {
        _title = new Label
        {
            Position = new Vector2(0, 60),
            Size = new Vector2(320, 20),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 16 },
        };
        AddChild(_title);

        _subtitle = new Label
        {
            Position = new Vector2(0, 90),
            Size = new Vector2(320, 20),
            HorizontalAlignment = HorizontalAlignment.Center,
            LabelSettings = new LabelSettings { FontSize = 10 },
        };
        AddChild(_subtitle);
    }

    public void Show(string title, string subtitle)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        Visible = true;
    }

    public void Hide() => Visible = false;
}
```

- [x] **Step 2: Add the phase machine to GameRoot**

Add the field and enum to `GameRoot`:

```csharp
    private enum Phase { Title, Running, GameOver }

    private Phase _phase = Phase.Title;
    private ScreenOverlay _overlay = null!;
```

In `_Ready`, after the HUD is added, replace the `StartRun();` call with:

```csharp
        _overlay = new ScreenOverlay();
        AddChild(_overlay);

        var best = _saveStore.Load().BestScore;
        _overlay.Show("CROC", best > 0 ? $"best {best} - press to start" : "press to start");
```

- [x] **Step 3: Gate the loop on the phase**

Replace `_Process` with:

```csharp
    public override void _Process(double delta)
    {
        if (_phase != Phase.Running)
        {
            if (_chompQueued)
            {
                _chompQueued = false;
                _overlay.Hide();
                StartRun();
                _phase = Phase.Running;
            }
            return;
        }

        if (_chompQueued)
        {
            _chompQueued = false;
            Render(_session.Chomp());
        }

        Render(_session.Tick((float)delta));

        _beltView.Sync(_session.Items);
        _beltView.PruneMissing(_session.Items);
        _hud.Set(_session.State.Score, _session.State.Combo, _session.State.Strikes);
    }
```

- [x] **Step 4: Handle the end of a run**

Replace the `case RunEnded ended:` arm in `Render` with:

```csharp
                case RunEnded ended:
                    EndRun(ended);
                    break;
```

And add:

```csharp
    private void EndRun(RunEnded ended)
    {
        var data = _saveStore.Load();
        var isBest = ended.FinalScore > data.BestScore;

        data.BestScore = System.Math.Max(data.BestScore, ended.FinalScore);
        data.LifetimeEaten += ended.Eaten;
        _saveStore.Save(data);

        if (isBest) _crocView.PlayCelebrate();

        _beltView.Clear();
        _phase = Phase.GameOver;
        _overlay.Show(
            $"{ended.FinalScore}",
            isBest ? "new best - press to retry" : $"best {data.BestScore} - press to retry");
    }
```

- [x] **Step 5: Add BeltView.Clear**

Add to `Scripts/BeltView.cs`:

```csharp
    /// <summary>Drops every sprite. Called when a run ends so the next run starts clean.</summary>
    public void Clear()
    {
        foreach (var sprite in _sprites.Values) sprite.QueueFree();
        _sprites.Clear();
    }
```

- [ ] **Step 6: Verify in the Godot editor**

Press Play. Expected:
- The title screen reads `CROC` / `press to start`.
- A press starts a run; the overlay disappears.
- Three strikes shows the final score and a retry prompt, and the belt clears.
- A press restarts from zero, with the belt empty and strikes reset.
- Quit and relaunch: the title screen now shows your best score, proving the save
  round-tripped through `user://crocgame.save`.

- [x] **Step 7: Full verification**

Run: `dotnet test`
Expected: PASS — the full Core suite, unchanged in size from Task 10. The Godot layer added no Core behavior.

- [x] **Step 8: Commit**

```bash
git add Scripts/ScreenOverlay.cs Scripts/GameRoot.cs Scripts/BeltView.cs
git commit -m "feat: title, game over, restart, and persisted best score"
```

---

## Status

Code-complete as of 2026-08-28; every task's deliverable is on disk and the Core suite
passes with 91 tests. The three unticked steps are all the same thing: nobody has run
the game in the Godot editor yet. The tasks were executed in batches rather than one
commit per task, so the git history has four commits rather than thirteen.

## Done when

- `dotnet test` passes, and the boundary test still proves Core has no Godot reference.
- The game is playable end to end: title, run, three strikes, game over, retry.
- A best score survives a relaunch.

## Phase two — delivered

Built directly rather than as a separate plan, at the user's direction, along with a
switch to a 9:16 portrait viewport (180x320) with the croc centred:

- **Conveyor**: `Scripts/ConveyorView.cs` draws the belt and scrolls its treads at the
  speed Core is already using, so the surface visibly carries the food.
- **Feel pass**: hit-stop, screen shake, damage flash, combo popups, and a crumb
  particle burst, all in `Scripts/GameRoot.cs` and `Scripts/ComboPopup.cs`.
- **Audio**: six effects generated through the Artificial Studio MCP server and
  committed to `Art/Audio/`, played by `Scripts/Sfx.cs`. Prompts recorded in
  `Art/Audio/README.md`.
- **Inedibles**: `bomb` and `boot` from `Art/Tools/inedible_gen.py`, added to
  `Resources/food.json`. Core already implemented the rule.
- **Unlocks**: `Core/UnlockCatalog.cs` with tests, four cosmetic croc skins applied as
  tints on the existing sprite.

One deliberate departure from the spec: unlocks are evaluated in `UnlockCatalog`
rather than emitted as a `MilestoneReached` event from `GameSession`. Milestones
depend on lifetime totals held in the save file, and giving `GameSession` a
save-file dependency to fire one event would cost more than the event is worth.

## Still out of scope

- Tuning. Every number in `Core/Difficulty.cs` is still a guess.
- A skin picker: the croc automatically wears the most recently earned skin.
- Backdrops, settings, pause, and accessibility assists.
