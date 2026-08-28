# Croc Timing Game — Design

Date: 2026-08-28
Status: **superseded in part.** The timing mechanic below still stands exactly as
written. The structure around it does not: the game is now a 1v1 eating-contest
career, not an endless run. See "Revision: the contest" at the end of this document.

## 1. What this is

An endless arcade timing game. The croc's jaws sit fixed on screen while food rides
a conveyor belt past them. The player presses one button to chomp. Landing the chomp
while food is between the teeth scores; letting edible food pass, or chomping empty
air, costs one of three strikes. Three strikes ends the run.

Difficulty escalates continuously within a run along four levers: belt speed, tighter
and more irregular spacing, new food movement behaviors, and inedible items introduced
partway through.

The repository is named `physics-game` and the Godot project is named `PhysicsGame`
for historical reasons. This is not a physics game. The C# assembly, solution, and
namespaces are renamed to `CrocGame` as part of this work; the repository name is left
alone.

### Non-goals

- No level geometry, no traversal, no platforming. The existing `Art/tileset.png` is
  background decoration at most.
- No music-synced rhythm. Timing is judged against object position, not an audio clock.
- No power upgrades. Progression is cosmetic only, so every run's score stays
  comparable to every other run's.
- No multiplayer, no leaderboards, no accessibility/assist modes in v1.

## 2. Platform and project settings

Target: desktop and mobile from one codebase. A single `chomp` input action is bound
to Space, left mouse button, and screen touch. Development and testing happen on
desktop; mobile export is expected to work without game-code changes.

Base viewport is **320x180**, landscape, with the existing `canvas_items` stretch mode
and `expand` aspect. Food sprites are **16x16**; the croc is 32x32, so food reads as
half the height of the jaws and roughly twenty tiles of belt are visible at once,
which gives the player usable lead time.

Three corrections to `project.godot`, all leftovers from the abandoned physics framing:

| Setting | Change | Reason |
|---|---|---|
| `physics/3d/physics_engine` | remove | No 3D in this game. |
| `rendering/textures/canvas_textures/default_texture_filter` | set to `0` (Nearest) | NES art blurs when scaled otherwise. `Art/README.md` already warns about this. |
| `display/window/size/viewport_width` / `_height` | set to `320` / `180` | Establish the pixel canvas. |

The Mobile renderer stays. `config/name` and `dotnet/project/assembly_name` become
`CrocGame`.

## 3. Architecture

Three C# projects in one solution:

```
CrocGame.csproj                    Godot-generated. Scenes, input, audio, rendering, juice.
Core/CrocGame.Core.csproj          Pure C# class library. No GodotSharp reference.
Tests/CrocGame.Core.Tests.csproj   xUnit. References Core only.
```

**The boundary rule: `CrocGame.Core` does not reference GodotSharp.** Not by
convention — the reference is absent, so the compiler rejects any use of a Godot type.
This is what allows the entire rule set to be tested with `dotnet test` and a fake
clock, on a machine with no Godot binary and no export templates.

Core never reads a wall clock and never reads input. It receives `dt` and discrete
commands, and returns events.

Data flow is one-directional per frame:

```
Godot _Process(delta) --> GameSession.Tick(dt) --> IReadOnlyList<GameEvent>
                                                        |
                                                        v
                                    scene layer: sprites, animation, audio, shake
```

The scene layer never queries Core mid-frame for state it could have read from an
event, and mutates Core only through `Chomp()`.

## 4. Core model

All types live in `CrocGame.Core`.

- **`FoodItem`** — `Id`, `TypeId`, `X`, `HalfWidth`, `IsEdible`, `Movement`. The
  half-width *is* the timing window; a narrow item at high belt speed is a hard press,
  and that relationship is visible on screen rather than hidden in a tolerance
  constant.
- **`IMovement`** — per-item position strategy. Implementations: `Constant`,
  `Stutter` (brief pauses), `Bounce` (surges forward then settles, so the item crosses
  the zone faster than the belt speed implies). All movement is along X only: the jaw
  zone is a one-dimensional overlap test, and no behavior may depend on a vertical
  position the judge does not read.
- **`Belt`** — owns live items, advances each by its movement strategy and the belt
  speed, retires items past the jaws.
- **`JawZone`** — a center X and a half-width. "Between the teeth" is an overlap test
  against this and nothing else.
- **`SpawnDirector`** — decides the next spawn time and food type from the current
  `Difficulty`. Takes an injected `IRandomSource`, so a seed reproduces a run exactly.
- **`ChompJudge`** — resolves a chomp to `Hit(item)` when an item overlaps the jaw
  zone, or `Air` when none does. Resolves an edible item retiring unchomped to
  `Passed`.
- **`RunState`** — score, combo, strikes, lifetime eaten this run, elapsed time. Ends
  at three strikes.
- **`Difficulty`** — a pure function of items eaten, returning belt speed, spacing
  bounds, movement-behavior weights, and inedible probability.
- **`GameSession`** — the façade. `Tick(float dt)` and `Chomp()`, each returning
  events.
- **`ISaveStore`** — `Load()` / `Save(SaveData)`. Implemented in the Godot layer
  against `user://crocgame.save`; tests use an in-memory implementation.

### Outcome rules

| Situation | Result |
|---|---|
| Chomp overlapping edible food | Hit: score + combo increment |
| Chomp overlapping inedible item | Strike, combo reset |
| Chomp with nothing in the zone | Strike, combo reset |
| Edible food retires unchomped | Strike, combo reset |
| Inedible item retires unchomped | Nothing; correct play |
| Third strike | Run ends |

If a chomp overlaps more than one item, the item whose center is nearest the jaw
center is judged.

### Events

`Spawned`, `Chomped` (hit or air, carrying the item and resulting combo), `Passed`,
`StrikeAdded`, `RunEnded`, `MilestoneReached`. The scene layer's whole job is to
render these.

## 5. Difficulty and tuning

One function, one table. Every number below is a starting target expected to change
once the game is playable; they exist so tuning starts from something rather than
from nothing.

| Lever | Start | End | Reached by |
|---|---|---|---|
| Belt speed | 40 px/s | 220 px/s (cap) | ~60 items eaten |
| Spacing between items | 1.1 s | 0.35 s | ~60 items eaten |
| Spacing jitter | ±10% | ±35% | ~40 items eaten |
| `Stutter` behavior weight | 0 | introduced at 15 items | — |
| `Bounce` behavior weight | 0 | introduced at 30 items | — |
| Inedible probability | 0 | 20% (cap), from 25 items | ~70 items eaten |

Belt speed and spacing interpolate smoothly. Behaviors and inedibles switch on at
their thresholds and then ramp their weights.

## 6. Scene layer and feel

`Main.tscn` runs a small state machine over Title, Run, and GameOver screens. The Run
screen holds `BeltView`, `CrocView`, and `Hud`.

- **`BeltView`** creates a sprite node on `Spawned`, copies positions from Core each
  frame, frees the node when the item is eaten or retires.
- **`CrocView`** — `AnimatedSprite2D` driven by `Art/ExportedSprites/croc_sheet.png`
  and its Aseprite JSON, which already ships three tagged animations: `idle` (4
  frames), `eat` (6), `celebrate` (8). `eat` plays on a successful chomp and on an air
  chomp alike — the jaws close either way — with the hit/miss distinction carried by
  flash, shake, and audio rather than by a separate animation. `celebrate` plays on a
  combo milestone and on a new best score. No flinch animation exists; if misses need
  their own read after playtesting, that is a new tag added to the sheet, not a code
  change.
- **`Hud`** — score, combo, three strike pips.

Feel pass, all driven by events: ~60 ms hit-stop on a successful chomp, screen shake
and red flash on a strike, combo popup scaling with the streak, crumb particle burst,
and SFX on chomp / air / pass / strike / game over.

**Hit-stop freezes the Core tick rather than slowing it.** Because Core is `dt`-driven,
this means not calling `Tick` for those frames, so the pause can never consume part of
a later timing window.

Input is read in `_UnhandledInput` and buffered to the next tick, so a press is never
dropped between frames.

## 7. Content data

The food table is the contract between the art and the rules. It lives at
`Resources/food.json`, is loaded by Core, and is the only thing that needs to change
to add a food:

```json
{
  "id": "fish",
  "width": 16,
  "edible": true,
  "movement": "constant",
  "score": 10,
  "minEatenToAppear": 0
}
```

The Godot layer maps `id` to a texture at `Art/ExportedSprites/Food/<id>.png`. Adding a
food is one row plus one PNG; no code changes.

`minEatenToAppear` gates which food types are eligible to spawn. It does not control
how often inedibles appear — that is the inedible probability in `Difficulty`, which is
the single source of truth for spawn rates. The food table decides what may appear;
`Difficulty` decides how often.

## 8. Asset pipeline

Assets are generated once and committed as artifacts. Nothing is generated at build
time or at runtime.

### Sound effects — Artificial Studio

Artificial Studio is registered as an HTTP MCP server, scoped to this project:

```
claude mcp add --transport http artificial-studio https://api.artificialstudio.ai/mcp
```

This is the preferred route: the MCP tools handle the request and any job polling, so
no key ever needs to reach a shell command or a script in this repo.

**Current state: the server reports `Needs authentication`, and its tools are therefore
not yet callable.** Authentication is resolved before this part of the implementation
starts, either through the session's `/mcp` login flow or by re-registering with an
`Authorization` header. Registering with a header must be done outside the agent
session so the key does not enter a transcript. MCP tools are enumerated at session
start, so a reconnect or restart is required after authenticating.

Fallback if the MCP route cannot be authenticated: the direct HTTP API, with the key
supplied through a gitignored `.env` at the repo root and sourced before a generation
run. Never committed, never written into `settings.json`.

```
POST https://api.artificialstudio.ai/api/run
Authorization: <key>
{ "tool": "sound-effects",
  "input": { "prompt": "...", "duration": "5s", "prompt_influence": "0.1" } }
```

**Unverified either way:** whether a generation call returns audio directly or a job
handle to poll. The first action in this part of the implementation is a single
throwaway call to learn the response shape; the fetch logic is written against what
comes back, not against an assumption.

The prompt set is checked in either way, so the audio is reproducible: chomp/crunch,
air-snap (whiff), item-passed thud, strike sting, game over, and menu blip. Raw returns are trimmed, peak-normalized, and converted to `.ogg`
with `ffmpeg` (installed), then committed to `Art/Audio/`. The prompt for each file is
recorded next to it in `Art/Audio/README.md`.

### Food sprites — Aseprite MCP

Five 16x16 edible foods already exist — hotdog, pizza, burger, donut, pie — generated
by `Art/Tools/food_gen.py` and exported to `Art/ExportedSprites/`, alongside a
`food.png` strip. They match the cast's treatment and confirm the 16x16 / 320x180
decision in section 2. No further edible art is required for v1.

Remaining art work is the inedible items (section 5 introduces them at 25 items eaten)
and the unlockable croc skins. These are produced through the Aseprite MCP server
already configured in this environment (`aseprite` is at `/usr/bin/aseprite`), matching
the same treatment: 16x16, transparent background, 1px black outline, flat NES palette
colors.

Two routes are acceptable, chosen per sprite:

1. **Authored directly** via the MCP drawing primitives, or by extending
   `Art/Tools/food_gen.py`, which already produces the edible set this way.
   Deterministic and consistent.
2. **Generated large, then reduced** via `downsample_image` and `quantize_palette`,
   followed by `apply_outline`. Used when a food shape is fussier than hand-authoring
   justifies.

Whichever route, the result is checked against the existing croc sprite for palette and
outline consistency before it is accepted. Sources go in `Art/RawSprites/Food/`,
exports in `Art/ExportedSprites/Food/`.

`Art/README.md` is updated to cover the food set and the audio directory.

## 9. Progression and save

Cosmetic unlocks only, keyed to thresholds on lifetime food eaten and best score.
Unlockables are croc skins (palette variants, cheap to produce through the same
generation path as the cast) and belt backdrops. Nothing unlocked affects difficulty,
scoring, or the timing window.

`SaveData` holds best score, lifetime food eaten, and a set of unlocked ids, stored as
JSON at `user://crocgame.save`.

## 10. Testing

Core is developed test-first with xUnit:

- Overlap judged exactly at both edges of the jaw zone, and just outside each.
- All six outcome rules in the table above, including inedible-passed being safe.
- Nearest-center tie-break when two items overlap the zone.
- Run ends on the third strike and not before.
- Difficulty increases monotonically and respects every cap.
- A seeded `SpawnDirector` reproduces an identical item sequence.
- Milestones fire once each, not repeatedly.
- `SaveData` round-trips, including an empty unlock set.
- Hit-stop: suspending `Tick` for N frames does not alter any subsequent judgment.

The scene layer is verified by playing it. No engine-dependent automated tests.

## 11. Failure handling

- Missing or corrupt save file: log once, start from fresh defaults, never crash.
- Unknown food id in the save's unlock list: ignored on load.
- Missing food texture: render a magenta placeholder and log a warning, so the gap is
  obvious in the editor rather than silently invisible.
- Asset generation API failure: the generation script fails loudly and changes nothing
  on disk. It is a development-time tool, not a runtime dependency.

## 12. Verification items

Two things are asserted by this design but not yet confirmed on this machine. Both are
build-configuration questions, not design risks:

1. **Target framework.** Only the .NET 10 SDK is installed; Godot 4.7 C# projects
   normally target `net8.0`. Confirm on first compile which target framework the
   generated project uses and whether the targeting pack needs to be fetched.
2. **Godot binary.** Not on this machine's `PATH` (`~/.local/share/godot/export_templates`
   is empty, and `project.godot` pins a Windows d3d12 driver), so the editor is presumably
   driven elsewhere. `dotnet test` covers the rules without it. A Godot path is needed only
   if headless builds or exports are wanted from this machine.

## 13. Deferred

- Herdr multi-agent orchestration. Setting it up requires this session to run inside a
  Herdr pane (`HERDR_ENV=1`, currently unset) and the skill installed at
  `~/.claude/skills/herdr/SKILL.md`. Worth doing as its own workflow task; this game's
  work does not parallelize enough to depend on it.
- Alternate modes and rulesets, settings menu, pause, accessibility assists.


---

# Revision: the contest

The premise, from the author: *a homeless crocodile is starving, enters an eating
contest, and faces contestants 1v1 until he is champion.*

That replaces endless escalation. What survives unchanged is everything about the
press itself - the jaw zone, position-authoritative judging, the movement strategies,
the belt, the spawn director, the difficulty curve. What changed is the frame around
it.

## What the structure is now

A career ladder of four rivals drawn from the existing cast: PIP the penguin, MOCHI
the cat, UNIT-7 the robot, BLORP the slime. Each bout is a timed match, 30 to 38
seconds. Both eat; the higher score at the bell wins. Three strikes disqualifies
regardless of score. Winning pays prize money, which is spent in a shop between bouts.

The rival is not simulated on a belt of their own. They consume at a rate with jitter,
because the player never sees their timing - only their score climbing. Modelling more
than that would be work no one can observe.

## Frenzy

A combo of eight tips the match into a frenzy: six seconds of double score and a belt
40% faster, refreshed by continued eating and killed instantly by a strike. This is
the peak the previous design lacked. A pure escalation curve has no shape - it only
gets harder - whereas frenzy makes good play compound, and losing it hurt.

## Money and cosmetics

Prizes run 25 / 50 / 100 / 200. The shop sells four croc skins at 30 / 80 / 150 / 250,
so the ladder does not quite fund everything in one pass. Skins are tints on the
existing sprite: no new art, no balance risk, and the choice is real because you cannot
have them all at once.

## What this replaced, and why the earlier version failed

The first build was assessed as: incoherent art, cheesy particles, an empty
background, ear-hurting audio, and a loop too predictable to be fun. The causes and
the fixes:

- **Empty background** - nothing on screen except the sprites came from the tileset.
  The arena is now built from `tileset.png` tiles, with the rest of the cast drawn
  dimmed as spectators.
- **Croc too small** - it rendered 32px tall on a 180px-wide canvas. It is now drawn at
  2x integer scale.
- **Cheesy particles** - Godot's default particles are soft round dots, which read as a
  different medium sitting on pixel art. Replaced with hard-edged square chunks snapped
  to the pixel grid, coloured from the food palette.
- **Painful audio** - AI-generated realistic sound against NES art is a mismatch, and
  every cue ran about a second, which turns to mud when the core sound fires several
  times a second. Replaced with synthesised chiptune from `Art/Tools/sfx_gen.py`: pulse,
  noise, and triangle channels, 35-720ms, peak-limited to 0.32.
- **Predictable loop** - no opponent, no clock, no peaks. Now: a rival whose score you
  watch climbing, a bell, frenzy, and a shop.
