# Croc Art, Cosmetics and Music — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kill the static feel — new animation tags for the croc and every rival, a procedural motion layer, buff and coin sprites, shop cosmetics that are actually drawn, and the new theme looping under the whole game.

**Architecture:** All character art is **generated, never painted**. `Art/Tools/cast_gen.py` authors each sprite as 16-column half-rows mirrored to 32x32, composes animation tags from pose patches, and emits an Aseprite batch script that bakes frames, durations and tags into the `.aseprite` sources; `food_gen.py` does the same at 16x16 for props. A new animation is a pose patch plus a row in `animations()`. The scene layer adds a procedural motion layer on top that needs no art at all.

**Tech Stack:** Python 3 (no dependencies — the generators write PNG bytes directly with `zlib`), Aseprite at `/usr/bin/aseprite` for the sheet bake, Godot 4.7.2 mono, C#.

**Spec:** `docs/superpowers/specs/2026-08-29-croc-three-phase-bouts-design.md` (sections 5, 6, 7)

**Depends on:** `docs/superpowers/plans/2026-08-29-croc-three-phase-bouts-mechanics.md`. That plan defines `BuffKind`, the `power` column in `food.json`, and the `coin` item this plan draws. Task 4 below is the only task that hard-depends on it; Tasks 1–3 and 6–8 can land first.

## Global Constraints

- **Characters:** 32x32, transparent background, 1px black outline, **exactly 5 flat colours**, mirror-symmetric — authored as 16-column half-rows and reflected. `mirror()` asserts symmetry, so an asymmetric row is a build failure, not a visual bug.
- **Props and food:** 16x16, same treatment, 5 colours.
- **Palettes are fixed per character** and listed in `Art/README.md`. No colour may be introduced that is not already in that character's five, and no colour outside the NES-ish flat set anywhere.
- **Nothing is hand-edited on top of generated output.** Every art change is a change to a generator, followed by a regeneration.
- **Integer scale only.** The croc renders at 2x; no sprite is ever drawn at a fractional scale.
- **Music sits under the effects.** SFX are peak-limited to 0.32 (`Art/Tools/sfx_gen.py`); the music bed must be quieter still and the player cannot turn it down.
- **`CrocGame.Core` gains nothing in this plan.** Every change here is scene layer, art, or tooling.
- **Verification is by eye and ear**, using the harness in `docs/running-the-game.md`. There are no automated tests for anything in this plan.

**The regeneration command**, referred to throughout as *regenerate the cast*:

```bash
cd Art/Tools
python3 cast_gen.py ../ExportedSprites ../RawSprites
aseprite --batch --script ../ExportedSprites/build.lua
cd ../..
GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
"$GODOT" --headless --import --path .
```

---

### Task 1: Croc reaction tags — `flinch`, `gulp`, `taunt`

**Files:**
- Modify: `Art/Tools/cast_gen.py`
- Regenerates: `Art/ExportedSprites/croc_sheet.png`, `croc_sheet.json`, `Art/RawSprites/croc.aseprite`

**Interfaces:**
- Consumes: the existing `POSES`, `FEET`, `pose()`, `bob()`, `hop()`, `animations()` machinery.
- Produces: three new tags on every character sheet — `flinch` (3 frames), `gulp` (4), `taunt` (5) — appended after `eat`, so the existing frame indices 0–17 and the `idle`/`celebrate`/`eat` ranges are unchanged and nothing that reads them breaks.

- [ ] **Step 1: Add the pose patches**

In `Art/Tools/cast_gen.py`, each entry of `POSES` currently carries `cheer`, `eat_chew`, `eat_open`. Add three more to the croc's entry. Each patch is a dict of `row index -> 16-character half-row`, replacing that row of the base.

For `CROC` (palette `K` black, `d` dark green, `g` green, `y` yellow, `W` white):

```python
        # Eyes screwed shut and the head pulled back: what a bomb looks like.
        'hurt': {6: '.....KgggKKKgggg', 7: '.....KggKKKKKggg', 8: '.....KgggKKKgggg',
                 14: '.....KggKKKKKKKK'},
        # Mouth clamped, throat bulging - the frame that says something went down.
        'gulp': {13: '.....KggKKKKKKKK', 14: '.....KggKKKKKKKK', 15: '.....KggKKKKKKKK',
                 17: '......Kgggggdddg', 18: '.....KKKKgggddgg'},
        # Every tooth showing. Used before the bell and after a phase won.
        'grin': {13: '.....KggKKKKKKKK', 14: '.....KggKWKWKWKW', 15: '.....KggKWKWKWKW',
                 16: '......KggKKKKKKK'},
```

Add the equivalent three to `PENGUIN`, `CAT`, `ROBOT` and `SLIME` using each character's own five colours. The rule for each: `hurt` closes or crosses the eyes and drops the head one row; `gulp` closes the mouth and thickens the neck/body row below it; `grin` opens the mouth wide with the character's brightest colour. Every patch row must be 16 characters — `pose()` asserts this, so a wrong length fails loudly at generation time rather than producing a wrong sprite.

- [ ] **Step 2: Compose the tags**

In `animations()`, add the three poses and three tags. Append them **after** `eat` so existing frame indices do not move:

```python
def animations(name, base):
    feet = FEET[name]
    p = POSES[name]
    cheer = pose(base, p['cheer'])
    chew = pose(base, p['eat_chew'])
    open_ = pose(base, p['eat_open'])
    hurt = pose(base, p['hurt'])
    gulp_ = pose(base, p['gulp'])
    grin = pose(base, p['grin'])
    return [
        ('idle', [(bob(base, feet, dy), 180) for dy in (0, -1, 0, 1)]),
        ('celebrate', [
            (base, 90), (bob(base, feet, 1), 90),
            (hop(cheer, -1), 90), (hop(cheer, -2), 110), (hop(cheer, -2), 110),
            (hop(cheer, -1), 90), (bob(base, feet, 1), 90), (base, 110),
        ]),
        ('eat', [
            (base, 140), (bob(open_, feet, -1), 120), (open_, 120),
            (chew, 120), (bob(chew, feet, 1), 120), (base, 140),
        ]),
        # New tags append after frame 17, so idle/celebrate/eat keep their indices and
        # every existing reader stays correct.
        ('flinch', [
            (hop(hurt, 1), 70), (hop(hurt, 2), 110), (hurt, 90),
        ]),
        ('gulp', [
            (gulp_, 90), (bob(gulp_, feet, -1), 110), (bob(gulp_, feet, 1), 110), (base, 120),
        ]),
        ('taunt', [
            (base, 130), (bob(grin, feet, -1), 110), (grin, 150),
            (bob(grin, feet, 1), 110), (base, 130),
        ]),
    ]
```

- [ ] **Step 3: Regenerate and eyeball**

Run the regeneration command from Global Constraints. Expected console output per character: `croc 5 colours, 30 frames, idle=1-4 celebrate=5-12 eat=13-18 flinch=19-21 gulp=22-25 taunt=26-30`.

Open `Art/ExportedSprites/_preview_croc_8x.png` and look at it. Expected: 30 frames in a strip; the three new groups read as distinct poses at 8x, the outline stays 1px everywhere, and no frame has gained a sixth colour.

- [ ] **Step 4: Update the art README**

In `Art/README.md`, extend the animations table:

```markdown
| `flinch` | 18-20 | knocked back two rows, eyes screwed shut — a bomb or a lost tooth |
| `gulp` | 21-24 | mouth clamped, throat bulging, one swallow — a banked pot or a buff |
| `taunt` | 25-29 | every tooth showing — before the bell and after a phase won |
```

and change "Each character .aseprite holds 18 frames with three tags" to "30 frames with six tags".

- [ ] **Step 5: Commit**

```bash
git add Art/Tools/cast_gen.py Art/ExportedSprites Art/RawSprites Art/README.md
git commit -m "Add flinch, gulp and taunt animations to every character"
```

---

### Task 2: Play the new croc tags

**Files:**
- Modify: `Scripts/CrocView.cs`, `Scripts/GameRoot.cs`

**Interfaces:**
- Consumes: the six tags from Task 1.
- Produces: `CrocView.PlayFlinch()`, `PlayGulp()`, `PlayTaunt()`.

- [ ] **Step 1: Register the new animations**

In `Scripts/CrocView.cs`, the frame layout comment and `_Ready` both hard-code the 18-frame layout. Update both:

```csharp
/// <summary>
/// The jaws. Frame layout of croc_sheet.png, from croc_sheet.json:
/// idle 0-3, celebrate 4-11, eat 12-17, flinch 18-20, gulp 21-24, taunt 25-29,
/// all 32x32 in a single row.
/// </summary>
```

```csharp
        AddAnimation(frames, sheet, "idle", first: 0, count: 4, fps: 5f, loop: true);
        AddAnimation(frames, sheet, "celebrate", first: 4, count: 8, fps: 12f, loop: false);
        AddAnimation(frames, sheet, "eat", first: 12, count: 6, fps: 18f, loop: false);
        AddAnimation(frames, sheet, "flinch", first: 18, count: 3, fps: 14f, loop: false);
        AddAnimation(frames, sheet, "gulp", first: 21, count: 4, fps: 12f, loop: false);
        AddAnimation(frames, sheet, "taunt", first: 25, count: 5, fps: 9f, loop: false);
```

```csharp
    public void PlayFlinch() => Play("flinch");

    public void PlayGulp() => Play("gulp");

    public void PlayTaunt() => Play("taunt");
```

- [ ] **Step 2: Drive them from events**

In `Scripts/GameRoot.cs`, in the event switch:

- `StrikeAdded` and `ChompedAir` → `_croc.PlayFlinch()`. A miss and a hit currently play the same `eat` animation with only flash and shake to tell them apart; now the sprite itself says which happened.
- `PotBanked` and `BuffTaken` → `_croc.PlayGulp()`.
- `PhaseStarted` for index 0 → `_croc.PlayTaunt()` during the countdown, and `PhaseEnded` where the player led → `_croc.PlayTaunt()`.

- [ ] **Step 3: Verify**

```bash
dotnet build
GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
"$GODOT" --path . --autoplay --shots=/tmp/shots-tags
```

Read the shots. Expected: the croc is visibly in different poses across the run rather than the same open-jaw frame every time.

- [ ] **Step 4: Commit**

```bash
git add Scripts/CrocView.cs Scripts/GameRoot.cs
git commit -m "Play flinch, gulp and taunt so the croc reads what happened"
```

---

### Task 3: Rivals react

**Files:**
- Modify: `Scripts/RivalView.cs`, `Scripts/GameRoot.cs`, `Scripts/Bark.cs`

**Interfaces:**
- Consumes: the six tags from Task 1, which every character now has.
- Produces: no new API. `RivalView.Ate` / `Gloat` / `Rattle` / `Panic` keep their signatures and gain the animations they should always have played, plus a `_panic` shake.

**Why this matters most:** the rival is on screen for the entire bout. `RivalView` today builds all three existing animations but only ever plays `idle` and `eat`, so `celebrate` has been generated, exported and shipped without ever being seen. This task spends animation that already exists before adding any.

- [ ] **Step 1: Register and expose the new tags**

In `RivalView.BuildSprite`, add the three new animations alongside the existing three, with the same frame ranges as `CrocView`. Then:

`RivalView` already exposes `Ate(int)`, `Gloat(string)`, `Rattle(string)` and
`Panic(string)`, and `GameRoot` already calls all four. Do **not** add a parallel API:
give each existing method the animation it should always have had.

```csharp
    public void Ate(int score)
    {
        // ... existing score text and bite pulse ...
        _sprite.Play("eat");
    }

    public void Gloat(string line)
    {
        // ... existing bark ...
        _sprite.Play("celebrate");   // generated, exported, and until now never played
    }

    public void Rattle(string line)
    {
        Say(line, Bark.Mood.Rattled);
        _sprite.Play("flinch");
    }

    public void Panic(string line)
    {
        // ... existing bark ...
        _sprite.Play("taunt");
        _panic = 1f;
    }
```

`_panic` drives a shake in `_Process`, decaying at the same rate as the screen shake so the two read as one event:

```csharp
    private float _panic;

    public override void _Process(double delta)
    {
        // ... existing bite pulse ...

        if (_panic > 0f)
        {
            _panic = Mathf.Max(0f, _panic - (float)delta * 4f);
            _sprite.Position = new Vector2(Mathf.Round(_shakeRng.RandfRange(-2f, 2f) * _panic), 0f);
        }
    }
```

- [ ] **Step 2: Give the new bout moments a reaction**

The existing call sites in `GameRoot` now animate for free. Add the ones the three-phase bout introduced:

- `HungerStarted` → `Panic(def.LinePanic)` (the mechanics plan already wires this call).
- `PotBanked` where `Paid > 100` → `Rattle(def.LineLosing)`.
- `PhaseEnded` where the rival led → `Gloat(def.LineWinning)`.
- `PhaseKnockout` → `Gloat(def.LineWinning)`; the rival has just been handed free seconds.

Keep the existing `BarkCooldown` gating so reactions do not fire continuously — a rival who reacts to everything is wallpaper, which is the rule `Bark.cs` already encodes.

- [ ] **Step 3: Verify**

```bash
dotnet build
GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
"$GODOT" --path . --autoplay --shots=/tmp/shots-rival
```

Read the shots. Expected: the rival is in more than one pose across the run, and is visibly shaking on at least one frenzy or hunger shot.

- [ ] **Step 4: Commit**

```bash
git add Scripts/RivalView.cs Scripts/GameRoot.cs Scripts/Bark.cs
git commit -m "Rivals react: they flinch, gloat and panic instead of only chewing"
```

---

### Task 4: Buff and coin sprites

**Files:**
- Modify: `Art/Tools/food_gen.py`
- Creates: `Art/ExportedSprites/slow.png`, `shield.png`, `magnet.png`, `goldtooth.png`, `coin.png`

**Depends on:** the mechanics plan Task 5, which adds these ids to `Resources/food.json`. Until that lands they render as the magenta placeholder.

**Interfaces:**
- Consumes: `food_gen.py`'s existing 16x16 authoring conventions.
- Produces: five 16x16 PNGs whose ids match the `power` column exactly.

- [ ] **Step 1: Author the five props**

Each must read at 16px against a busy belt, and — critically — **must not read as food**, or a player will bite a buff expecting points. Give all five the same cue the golden item already uses: a hard white 1px highlight the food sprites do not have.

| Id | Reads as | 5 colours |
|---|---|---|
| `slow` | an hourglass, sand halfway | `#000000` `#f8f8f8` `#6878c8` `#383870` `#f8d878` |
| `shield` | a single large white tooth | `#000000` `#f8f8f8` `#bcbcbc` `#7c7c7c` `#58d854` |
| `magnet` | a horseshoe magnet, red and grey | `#000000` `#f83800` `#bcbcbc` `#7c7c7c` `#f8f8f8` |
| `goldtooth` | a small gold fang with a glint | `#000000` `#f8d878` `#f8b838` `#a84400` `#f8f8f8` |
| `coin` | a flat gold disc with a rim | `#000000` `#f8d878` `#f8b838` `#a84400` `#f8f8f8` |

Note the widths in `food.json`: `slow` is 16 and `shield` 14, so those fill the tile; `magnet` is 10 and `goldtooth` 8, so those must be drawn narrow — **the sprite has to match the hitbox**, because the food's own width *is* the timing window and a sprite wider than its hitbox is the window lying about its size.

The coin is drawn plain and uncluttered in the middle: the mechanics plan draws the pot's value on top of it, and that number has to be legible at `Ui.Small`.

- [ ] **Step 2: Regenerate and check the widths**

```bash
cd Art/Tools && python3 food_gen.py ../ExportedSprites ../RawSprites && cd ../..
python3 - <<'EOF'
import json, struct, zlib, os
# Confirm each buff's drawn extent matches the width declared in food.json.
rows = json.load(open('Resources/food.json'))
declared = {r['id']: r['width'] for r in rows if r.get('power')}
for name, want in declared.items():
    path = f'Art/ExportedSprites/{name}.png'
    print(name, 'declared', want, 'exists', os.path.exists(path))
EOF
```

Then open each PNG and confirm by eye that `magnet` occupies about 10 of the 16 columns and `goldtooth` about 8, centred.

- [ ] **Step 3: Import and verify in game**

```bash
GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
"$GODOT" --headless --import --path .
"$GODOT" --path . --autoplay --shots=/tmp/shots-buffs
```

Read the shots. Expected: no magenta placeholders on the belt, and buffs are distinguishable from food at a glance.

- [ ] **Step 4: Document and commit**

Add the five to the food table in `Art/README.md` under a new "Power-ups" heading, with their palettes.

```bash
git add Art/Tools/food_gen.py Art/ExportedSprites Art/RawSprites Art/README.md
git commit -m "Draw the four power-ups and the cash-out coin"
```

---

### Task 5: Shop cosmetics, drawn and worn

**Files:**
- Modify: `Core/Career.cs`, `Scripts/ShopScreen.cs`, `Scripts/CrocView.cs`, `Scripts/GameRoot.cs`
- Create: `Art/Tools/cosmetic_gen.py`
- Creates: `Art/ExportedSprites/Cosmetics/skin_chef.png`, `skin_gold.png`, `skin_shadow.png`, `skin_neon.png`

**Interfaces:**
- Consumes: `ShopItem` (existing), `Career.EquippedSkin` (existing).
- Produces: `ShopItem.SpriteId` replacing `ShopItem.Tint`; `CrocView.SetCosmetic(string spriteId)`.

**The defect:** `ShopItem` carries a hex tint, the card shows a colour swatch, and the croc is multiplied by that colour. Nothing is drawn, and a multiply over a flat five-colour palette mostly just makes the croc muddy — it also violates "everything comes from one palette", because a multiply produces colours that are in no palette at all. `design-principles.md` says to show the thing, not its name; a swatch is halfway there and a drawn object is the rest.

- [ ] **Step 1: Change the model, keeping the ids**

In `Core/Career.cs`:

```csharp
/// <summary>
/// A cosmetic the croc wears. A drawn object, not a colour multiply: tinting a flat
/// five-colour sprite produces colours that are in no palette, and sells the player a
/// word rather than a thing.
/// </summary>
public sealed record ShopItem(string Id, string Name, int Cost, string SpriteId);
```

```csharp
    public static readonly IReadOnlyList<ShopItem> Shop = new[]
    {
        new ShopItem("skin_chef",   "CHEF HAT",   30,  "skin_chef"),
        new ShopItem("skin_gold",   "GOLD TOOTH", 80,  "skin_gold"),
        new ShopItem("skin_shadow", "SHADES",     150, "skin_shadow"),
        new ShopItem("skin_neon",   "NEON CROWN", 250, "skin_neon"),
    };
```

**The ids are unchanged on purpose**, so an existing save keeps whatever it bought. The costs are unchanged too: the ladder pays 375 and the shop costs 510, and that scarcity is what makes the purchase a choice.

- [ ] **Step 2: Draw the four cosmetics**

Create `Art/Tools/cosmetic_gen.py`, following `food_gen.py`'s structure. Each is 16x16 with a transparent background, a 1px black outline, and at most 5 colours drawn from the existing palettes:

| Id | Object | Colours |
|---|---|---|
| `skin_chef` | a chef's toque, sitting on the snout | `#000000` `#f8f8f8` `#bcbcbc` |
| `skin_gold` | one oversized gold fang with a white glint | `#000000` `#f8d878` `#f8b838` `#f8f8f8` |
| `skin_shadow` | wraparound shades across the eyes | `#000000` `#383870` `#6878c8` `#f8f8f8` |
| `skin_neon` | a small crown | `#000000` `#58f8d8` `#58d854` `#f8f8f8` |

Each is authored to sit at a fixed anchor over the croc's 32x32 frame — the hat and crown on the top of the head, the shades across the eye row, the fang on the jaw line. Write the anchor into the script as a constant per cosmetic and print it, so the C# side uses the same number rather than a guessed one.

- [ ] **Step 3: Wear it**

In `Scripts/CrocView.cs`, replace tinting with a child sprite:

```csharp
    private Sprite2D? _cosmetic;

    /// <summary>
    /// The worn cosmetic, as a child sprite so it inherits the squash-and-stretch. An
    /// accessory that does not deform with the head reads as a sticker on the screen.
    /// </summary>
    public void SetCosmetic(string spriteId)
    {
        _cosmetic?.QueueFree();
        _cosmetic = null;

        if (spriteId == "") return;

        var texture = ResourceLoader.Load<Texture2D>(
            $"res://Art/ExportedSprites/Cosmetics/{spriteId}.png");

        if (texture is null)
        {
            GD.PushWarning($"Missing cosmetic {spriteId}");
            return;
        }

        _cosmetic = new Sprite2D { Texture = texture, Position = CosmeticAnchor(spriteId), ZIndex = 1 };
        AddChild(_cosmetic);
    }

    /// <summary>Where each cosmetic sits on the 32x32 frame. These numbers come from
    /// cosmetic_gen.py, which prints them — they are not estimated here.</summary>
    private static Vector2 CosmeticAnchor(string spriteId) => spriteId switch
    {
        "skin_chef" => new Vector2(0, -11),
        "skin_shadow" => new Vector2(0, -4),
        "skin_gold" => new Vector2(3, 3),
        _ => new Vector2(0, -12),
    };
```

Change `SetGlow` so it no longer multiplies by a skin tint — it takes the glow amount alone and modulates from white. Update `GameRoot.ApplySkin` to call `_croc.SetCosmetic(Career.EquippedSkin(_save)?.SpriteId ?? "")`.

- [ ] **Step 4: Show the object on the shop card**

In `Scripts/ShopScreen.cs`, replace the colour swatch with a `Sprite2D` (or a `DrawTexture` in `_Draw`) showing the actual cosmetic PNG, at 1x in the 26px gutter each card already reserves on its left. The name still reads beside it.

- [ ] **Step 5: Verify**

```bash
cd Art/Tools && python3 cosmetic_gen.py ../ExportedSprites/Cosmetics && cd ../..
GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
"$GODOT" --headless --import --path .
dotnet build
"$GODOT" --path . --autoplay --shots=/tmp/shots-shop
```

Read the shop shot. Expected: four cards each showing a drawn object, and — after a purchase — the croc visibly wearing it, with the accessory deforming along with the head on a bite rather than floating.

- [ ] **Step 6: Commit**

```bash
git add Art/Tools/cosmetic_gen.py Art/ExportedSprites Core/Career.cs Scripts/ShopScreen.cs Scripts/CrocView.cs Scripts/GameRoot.cs Art/README.md
git commit -m "Draw the shop cosmetics and wear them instead of tinting the croc"
```

---

### Task 6: The procedural motion layer

**Files:**
- Modify: `Scripts/CrocView.cs`, `Scripts/BeltView.cs`, `Scripts/GameRoot.cs`, `Scripts/Backdrop.cs`

**Interfaces:**
- Consumes: `PhaseStarted` (mechanics plan).
- Produces: `Backdrop.SetPhase(int index)`.

This is the cheapest variation per unit of work and needs no art at all.

- [ ] **Step 1: Vary the bite**

In `CrocView`, alternate the bite so twenty bites are not twenty identical events. Add a counter and offset the punch:

```csharp
    private int _biteCount;

    public void PlayEat()
    {
        _biteCount++;

        // Alternating lead: the head tips slightly one way then the other. Identical
        // repeated feedback stops registering as feedback at all.
        var lean = (_biteCount % 2 == 0) ? 1f : -1f;
        _leanTarget = lean * 0.06f;

        Play("eat");
    }
```

Apply `_lean` as `Rotation` in `_Process`, easing back to zero, and clamp it so the sprite never leaves the pixel grid by more than a pixel at 2x.

- [ ] **Step 2: Give the idle a life**

```csharp
    private float _idleAge;
    private Vector2 _restPosition;

    /// <summary>
    /// A sprite that holds perfectly still between events reads as a paused game. One
    /// pixel of drift is enough - and it has to be a whole pixel, or the grid breaks
    /// and the 1px outline goes soft.
    /// </summary>
    private void DriftWhileIdle(float dt)
    {
        if (_restPosition == Vector2.Zero) _restPosition = Position;

        _idleAge += dt;
        Position = _restPosition + new Vector2(0f, Mathf.Round(Mathf.Sin(_idleAge * 2.1f)));
    }
```

Call it from `_Process` when `_punch <= 0f` and the current animation is `idle`.

- [ ] **Step 3: Make food tumble and land**

Food that slides flat is the largest source of the static feel on the belt itself.

```csharp
    /// <summary>Seconds an item spends settling after it appears.</summary>
    private const float LandingSeconds = 0.2f;

    private void PoseItem(Sprite2D sprite, FoodItem item, float beltSpeed)
    {
        // Rolling, not sliding. Tying the tumble to belt speed makes a fast belt look
        // fast even in a still frame.
        sprite.Rotation = item.X / 16f * (beltSpeed / 120f) * 0.35f;

        // One overshoot on arrival, so an item lands rather than materialising.
        var settle = Mathf.Clamp(item.Age / LandingSeconds, 0f, 1f);
        var hop = (1f - settle) * Mathf.Sin(settle * Mathf.Pi) * 5f;

        sprite.Position = new Vector2(Mathf.Round(item.X), Mathf.Round(GameRoot.BeltY - hop));
    }
```

`Mathf.Round` on both axes is not optional: a sprite on a fractional pixel resamples and loses its outline.

- [ ] **Step 4: Anticipate the speed changes**

In `GameRoot`, before a phase transition and before a frenzy, hold a beat: `_croc` crouches (a brief negative punch), the camera pushes in via the existing `_zoom`, then releases. A speed change that is jumped into reads as a glitch; one that is led into reads as a gear change.

- [ ] **Step 5: Shift the arena per phase**

In `Scripts/Backdrop.cs`:

```csharp
    /// <summary>
    /// The arena changes between acts, so which phase this is can be read without a
    /// word. PLAIN is lit plainly, HAZARD drops the lights and reddens the banner,
    /// FEAST fills the stands and turns everything up.
    /// </summary>
    public void SetPhase(int index)
    {
        _phase = index;
        QueueRedraw();
    }
```

Vary the crowd density, the banner colour and the overall value between the three, staying inside the rule that the background must lose to the foreground — every phase's backdrop must be darker and less saturated than the belt and the croc.

- [ ] **Step 6: Verify**

```bash
dotnet build
GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
"$GODOT" --path . --autoplay --shots=/tmp/shots-motion
```

Read consecutive shots from within one phase. Expected: the croc is not in an identical position twice, food sits at varied rotations, and the three phases have visibly different backdrops.

- [ ] **Step 7: Commit**

```bash
git add Scripts/CrocView.cs Scripts/BeltView.cs Scripts/GameRoot.cs Scripts/Backdrop.cs
git commit -m "Procedural motion: bite variants, idle life, tumbling food, phase arenas"
```

---

### Task 7: Loop the theme

**Files:**
- Create: `Scripts/MusicPlayer.cs`
- Modify: `Scripts/GameRoot.cs`, `project.godot`
- Uses: `Music/croc.mp3` (72.8s, 256 kbps, already imported)

**Interfaces:**
- Consumes: nothing.
- Produces: `MusicPlayer` with `void Begin()`, `void Duck(bool quiet)`, `void SetFrenzy(bool active)`.

- [ ] **Step 1: Add an audio bus for music**

The theme must sit under the effects. SFX are peak-limited to 0.32 because stacked square waves get painful within a minute, and the player has no volume control — so the music bed goes quieter still.

In `project.godot`, add a `Music` bus routed to Master. Set its volume to **-14 dB** as a starting point; this is a tuning number and is expected to change once it has been heard against the chiptune.

- [ ] **Step 2: Write the player**

`Scripts/MusicPlayer.cs`:

```csharp
using Godot;

namespace CrocGame;

/// <summary>
/// The theme, looping under the whole game.
///
/// It is started once and never restarted, so the track runs continuously across the
/// title, the bouts, the interludes and the shop rather than cutting on every screen
/// change. A theme that restarts at each transition tells the player the game is a
/// series of menus.
///
/// It also carries state, the same way the chomp's pitch already does: it ducks for
/// dialogue, where the point is that someone is talking, and lifts during a frenzy.
/// </summary>
public partial class MusicPlayer : AudioStreamPlayer
{
    private const string TrackPath = "res://Music/croc.mp3";

    /// <summary>Under the effects, which are themselves peak-limited to 0.32.</summary>
    private const float FullDb = -6f;
    private const float DuckedDb = -18f;

    private float _targetDb = FullDb;

    public override void _Ready()
    {
        Bus = "Music";

        var stream = ResourceLoader.Load<AudioStreamMP3>(TrackPath);

        if (stream is null)
        {
            GD.PushWarning($"Missing {TrackPath}; the game runs silent.");
            return;
        }

        // A theme that stops after 73 seconds is worse than no theme.
        stream.Loop = true;
        Stream = stream;
        VolumeDb = FullDb;
    }

    public void Begin()
    {
        if (Stream is not null && !Playing) Play();
    }

    /// <summary>Pulls the bed down so a line of dialogue is the thing being heard.</summary>
    public void Duck(bool quiet) => _targetDb = quiet ? DuckedDb : FullDb;

    public void SetFrenzy(bool active) => PitchScale = active ? 1.06f : 1f;

    public override void _Process(double delta)
    {
        // Eased, not switched: a volume that jumps is more noticeable than the change
        // it is trying to make.
        if (!Mathf.IsEqualApprox(VolumeDb, _targetDb))
        {
            VolumeDb = Mathf.MoveToward(VolumeDb, _targetDb, (float)delta * 24f);
        }
    }
}
```

- [ ] **Step 3: Wire it up**

In `GameRoot._Ready`, add the node before the first screen is shown, and call `Begin()` on the first press rather than in `_Ready` — some platforms will not start audio before a user gesture:

```csharp
        _music = new MusicPlayer();
        AddChild(_music);
```

Duck on entering `Phase.Intro`, `Phase.Interlude` and `Phase.Shop`; unduck on `Phase.Countdown` and `Phase.Fighting`. Call `_music.SetFrenzy(true)` on `FrenzyStarted` and `false` on `FrenzyEnded`.

- [ ] **Step 4: Check the loop point by ear**

Play the game normally for at least 80 seconds and listen at the wrap:

```bash
GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
"$GODOT" --path .
```

If the wrap clicks or the track has an intro that should not repeat, set `stream.LoopOffset` to the seconds where the repeating section begins. Find that number by listening, not by guessing — inspect the track first if it helps:

```bash
ffprobe -v error -show_entries format=duration -of default=nw=1 Music/croc.mp3
ffplay -autoexit -ss 65 Music/croc.mp3
```

Record whatever offset is chosen, and why, in a comment next to the constant.

- [ ] **Step 5: Balance it against the effects**

Play a full bout and listen specifically for whether the chomp still cuts through during FEAST, when the belt is fastest and chomps are most frequent. If the music covers it, lower `FullDb`; the effects win, always. The chomp is information and the music is atmosphere.

- [ ] **Step 6: Commit**

```bash
git add Scripts/MusicPlayer.cs Scripts/GameRoot.cs project.godot Music
git commit -m "Loop the theme under the game, ducked for dialogue"
```

---

### Task 8: Document the audio and update the principles

**Files:**
- Modify: `Art/Audio/README.md`, `docs/design-principles.md`, `docs/running-the-game.md`

- [ ] **Step 1: Document the music**

In `Art/Audio/README.md`, add a section noting that `Music/croc.mp3` is the theme, that it is author-supplied rather than generated by `sfx_gen.py`, that it plays on the `Music` bus at its configured dB, and what its loop offset is and why.

- [ ] **Step 2: Record the principles**

Add to `docs/design-principles.md` under "Art":

```markdown
**Animation is generated, not painted.** Every character frame comes from
`Tools/cast_gen.py` — pose patches composed into tags and baked through Aseprite. A
frame painted by hand on top of that is lost at the next regeneration, and the
generator's symmetry and palette assertions are what keep thirty frames consistent.

**New tags append, never insert.** `flinch`, `gulp` and `taunt` were added after frame
17 so `idle`, `celebrate` and `eat` keep their indices. Inserting a tag silently
renumbers every frame every reader depends on.

**Cosmetics are objects, not tints.** Multiplying a flat five-colour sprite by a colour
produces colours in no palette and mostly just makes the sprite muddy. A hat is a hat.

**An accessory must deform with what it sits on.** The worn cosmetic is a child of the
croc sprite, so it squashes with the bite. One that holds still reads as a sticker on
the screen.
```

Add under "Audio":

```markdown
**One continuous bed.** The theme starts once and never restarts across screens. Music
that restarts at every transition tells the player the game is a series of menus.

**The effects always win.** The chomp is information and the music is atmosphere. If
the theme covers the chomp during FEAST, the theme comes down.
```

- [ ] **Step 3: Update the run doc**

In `docs/running-the-game.md`, note the new regeneration command from this plan's Global Constraints, and that a bout is now ~40 seconds including interludes, so the autoplay harness quits after 55.

- [ ] **Step 4: Commit**

```bash
git add Art/Audio/README.md docs/design-principles.md docs/running-the-game.md
git commit -m "Document the music bed and the generated-animation principles"
```

---

## Definition of done

- `dotnet build` is clean and `dotnet test` is still green — this plan touches Core only in Task 5's `ShopItem` record.
- Every character sheet has 30 frames and six tags; `_preview_<name>_8x.png` shows all six as distinct poses with a 1px outline and no sixth colour.
- No magenta placeholder appears anywhere in a full autoplay run:
  the belt's buffs and coin, and all four shop cards, render real art.
- The croc visibly wears a purchased cosmetic, and it deforms with the bite.
- The theme plays continuously from the title through a whole bout into the shop, wraps without a click, ducks under dialogue, and never covers the chomp.
- `grep -rn 'Tint' Core Scripts` returns nothing.
