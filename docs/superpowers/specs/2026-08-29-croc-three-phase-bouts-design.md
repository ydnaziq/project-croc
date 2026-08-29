# Croc — Three-Phase Bouts, Assists, and the Art Pass

Date: 2026-08-29
Status: active. Supersedes the match structure in
`2026-08-28-croc-timing-game-design.md` (the "Revision: the contest" section). The
timing mechanic itself — jaw zone, position-authoritative judging, movement
strategies, belt, spawn director, difficulty curve — is unchanged and stays
authoritative.

## 0. Why this exists

Four problems with the game as it plays today, in the author's words:

1. **It is too static.** Motion comes almost entirely from the belt. The croc swaps
   frames and squashes; the rivals are single unmoving PNGs; the arena never changes
   across a match.
2. **A bout is one undifferentiated block.** 20–26 seconds of the same thing, so a
   match has no acts and nothing to talk over.
3. **It is purely skill-based, so it has an unsurmountable ceiling.** A player who
   cannot hit the window cannot win, and no amount of retrying changes that. The goal
   is that anyone, at any skill level, can beat the game.
4. **Reward is not tied to risk.** Everything on the belt is worth roughly the same
   for roughly the same difficulty.

Plus two smaller ones: text sits at fixed offsets inside fixed-size boxes and so is
never actually centred, and the shop sells cosmetics that were never drawn.

The through-line for every decision below: **the ceiling comes down without the floor
coming up.** A beginner gets systems that work in their favour without being asked to
press better. An expert gets systems that reward pressing better. Neither is a
difficulty setting, and nothing purchasable touches either — `design-principles.md`'s
"progression must not inflate skill" still holds.

## 1. Bout structure

A bout becomes three phases with dialogue between them:

```
intro dialogue → P1 PLAIN 8s → 💬 → P2 HAZARD 9s → 💬 → P3 FEAST 10s → result
```

27 seconds of play, up from 20–26, but broken into three bursts. `design-principles.md`
says bouts stay short because "past about half a minute the timing stops being exciting
and starts being work." That is about *continuous* timing. Three 8–10s bursts separated
by a beat of dialogue honours the principle better than one 26s block does: each phase
is a single burst of concentration, which is exactly what the principle asks for.

Phases are data, not branches. A new `PhaseDef` record:

| Phase | Name | Seconds | Difficulty offset | Score × | Hazard scale | Power-ups | Coins |
|---|---|---|---|---|---|---|---|
| 0 | PLAIN | 8 | +0 | ×1 | 0 | off | off |
| 1 | HAZARD | 9 | +12 | ×1 | 1.0 | on | on |
| 2 | FEAST | 10 | +24 | ×2 | 1.3 | on | frequent |

The phase's difficulty offset **adds** to the rival's `Career.DifficultyOffsets`
(0/8/18/28), so BLORP's FEAST phase is the hardest belt in the game and PIP's PLAIN
phase is the gentlest. `HazardScale` multiplies `Difficulty.InedibleChance`, which is
how PLAIN is guaranteed clean regardless of where the curve has got to — a phase flag
overriding the curve, rather than the curve being bent to accommodate phases.

Score and rival score **carry across all three phases**. Phase 3 scoring double is what
keeps a comeback live: losing the first two phases is recoverable, so the interludes
are never dead air being read by someone who has already lost.

### Strikes and disqualification

**Strikes reset at the start of each phase.** Three teeth per phase, not per bout.

**Disqualification knocks the player out of the phase, not the bout.** On the third
strike in a phase: the belt clears and stops, the player scores nothing more, and the
rival keeps eating for the phase's remaining seconds unopposed. Then the interlude
runs and the next phase begins with fresh teeth.

This is the single biggest change to the ceiling. Strikes keep their sting — handing a
rival free seconds is a real, visible, painful cost — but they can no longer end a run.
A beginner who blows PLAIN still sees HAZARD, still sees FEAST, still sees the
dialogue, and can still win on points.

The bout is decided only at the bell, on total score. `MatchResult.Disqualified`
survives as a per-phase outcome and stops being a bout outcome, so `Career.RecordWin`
and `Career.RecordLoss` take a bout result rather than a match result and no longer
have a disqualification case to handle.

## 2. Three assists, three distinct slots

Three mechanics that help the player already exist or are being added. The risk of
three is that they all become "the thing that saves you" and compete for the same
moment. Each therefore gets a slot nothing else occupies:

| Mechanic | Occupies | Triggered by | Who it serves |
|---|---|---|---|
| Frenzy | the skill peak | a long combo | experts |
| Hunger | the deficit peak | falling behind | beginners |
| Pot + coin | the standing decision | always live | both, differently |
| Power-ups | moment-to-moment variety | RNG on the belt | both, differently |

### 2.1 Frenzy — unchanged

Combo 5 → 6s of ×2 score and a 1.4× belt, refreshed by continued eating, killed by a
strike. Already built (`Core/Frenzy.cs`). It now resets between phases along with the
combo.

### 2.2 Power-ups on the belt

Buffs ride the belt and are taken by biting them, so they cost nothing to explain and
need no new input. New optional `power` field in `Resources/food.json`.

The governing rule, which is the whole risk/reward design in one line:
**buff strength is inversely proportional to window width, and the strongest buffs
spawn guarded.**

| Buff | Effect | Width | Movement | Spawn rule |
|---|---|---|---|---|
| `slow` | belt to 60% for 4s | 16 | constant | common, alone |
| `shield` | absorbs the next strike | 14 | constant | beside one bomb |
| `magnet` | next 3 bites auto-hit | 10 | constant | inside a cluster |
| `goldtooth` | ×3 score for 5s | 8 | bounce | inside a 2-bomb cluster |

The wide, common, unguarded buffs are the *defensive* ones — that is deliberately the
beginner's lane, and a player who bites indiscriminately will collect SLOW and SHIELD
by accident at roughly the rate they need them. The narrow, guarded buffs are
*offensive*, and taking a GOLD TOOTH out from between two bombs at FEAST belt speed is
the hardest and best-paid press in the game.

A shield is visible as a fourth tooth in the strike meter, so "what do I have" is
answered by the meter that already exists rather than by a new indicator.

**MAGNET is the one place this design breaks "judge what the player can see."** For
three bites, judging is suspended and the nearest item is taken regardless of overlap.
That is mitigated, not hidden: it is announced with a banner, the jaws visibly hold
open, and a 1px tether is drawn from the jaws to the item about to be taken. The player
must be able to see that the game is doing something *for* them; what is unacceptable
is the window silently lying about its size.

### 2.3 The Pot and the cash-out coin

The push-your-luck layer, built to use the game's only verb.

- The pot is inactive during PLAIN. It accrues only in phases where coins spawn, so
  the player is never accruing a wager they have no way to bank.
- Every hit adds its points to a **pot** as well as to the score.
- The pot's multiplier steps with the combo: ×1 at combo 1–4, ×2 at 5–9, ×3 at 10–14,
  ×5 at 15+.
- Once the pot is non-empty, a **COIN** spawns on the belt on an interval. Biting it
  banks `pot × multiplier` into the score and empties the pot. Letting it ride past
  keeps the pot, and the next coin is worth more.
- **A strike wipes the pot.** So does the end of a phase — unbanked money does not
  carry across the interlude.
- The coin sprite carries **the current pot value drawn on it**, so the size of the
  wager is on screen at the moment the decision is made.

The critical property: **the base score is never at risk.** The pot is pure upside on
top of normal scoring. Biting every coin the instant it arrives is safe, viable,
beginner-correct play that still pays. Greed is an option, not a requirement. A player
who never understands the system loses nothing they would otherwise have had.

This satisfies "one verb" (no hold, no second button), "judge what the player can see"
(the wager is a sprite at a position), and "show state, don't ask for arithmetic" (the
number is on the coin, not derived from a meter).

### 2.4 Hunger — the comeback super

The one unambiguous crutch, and it is automatic.

- A meter fills only while the player is **behind on total bout score**, at a rate
  proportional to the size of the deficit. Further behind fills faster.
- A strike adds a bump to it — falling apart accelerates desperation.
- At full it **fires by itself**, with a banner and the rival reacting.
- For 5 seconds: every item on the belt becomes edible (bombs included — a starving
  croc eats anything, and they visibly change), the belt drops to 80%, and the jaw
  zone widens to 1.6×.
- **The widened jaw zone is drawn at its true width.** The whole point of drawing the
  bite window is that the player can learn from a miss; a secretly wider window is the
  rubber-banding this design rejected.
- At most once per phase, and it cannot charge while ahead.

Frenzy is what a winning croc earns; Hunger is what a losing croc is given. The pairing
is the game's premise — he is starving, and it is the hunger that makes him dangerous.
A player good enough to stay ahead will never once see it fire.

## 3. Dialogue interludes

Each interlude is two lines, chosen on who is actually ahead when the phase ends.
`OpponentDef` gains four fields: `Interlude1Ahead`, `Interlude1Behind`,
`Interlude2Ahead`, `Interlude2Behind`. The existing `LineLosing` / `LineWinning` /
`LinePanic` stay for in-match barks.

The croc still never speaks in words. His half of each exchange is a stage direction
in asterisks, as it already is in `GameRoot.CrocReply`, and those escalate with how the
bout is going rather than only with which rival it is.

Interludes are also where the game breathes: the belt is empty, the music ducks, and
the two eaters are staged large. This is the valley the phases are the peaks of.

## 4. Text centring

The defect: boxes are a fixed size and text sits at fixed offsets inside them.
`ScreenOverlay` builds a 92px card with the title at `+12` and the subtitle at `+48`,
so a one-line subtitle floats in dead space and a three-line one runs off the bottom.
`DialogueScene` does the same with `BoxTopY + 30`. Nothing is centred; it is only
positioned to look centred for one particular string length.

The fix is a layout helper in `Ui`:

```
Ui.Column(labels, width) -> measures every label, returns total height,
                            positions them as a block centred in a given rect
```

Boxes are then sized to their content and the content is centred in them, vertically
and horizontally. Applied to `ScreenOverlay`, `DialogueScene`, `ShopScreen`, and
`MatchHud`. This is the same lesson `design-principles.md` already records — "measure
text; never estimate it" — extended from width, where it was learned, to height and
position, where it was not.

## 5. Shop cosmetics, drawn

Today `ShopItem` carries a hex `Tint`, the card shows a colour swatch, and the croc is
multiplied by that colour. Nothing is drawn, and a colour multiply over a flat NES
palette mostly just makes the croc muddy.

Cosmetics become **drawn accessories**: a small sprite worn over the croc, and shown on
the shop card as the actual object.

| Id (unchanged) | Was | Becomes |
|---|---|---|
| `skin_chef` | white tint | a chef's toque |
| `skin_gold` | gold tint | a gold fang that glints |
| `skin_shadow` | blue tint | shades |
| `skin_neon` | cyan tint | a neon crown |

**The ids do not change**, so existing saves keep what they bought. `ShopItem.Tint` is
replaced by `SpriteId`; `Career.Shop` costs and the 375-earned / 510-to-buy scarcity
relationship are untouched.

An accessory is one 16x16 PNG plus an anchor offset per croc animation frame, drawn as
a child of `CrocView` so it inherits the squash-and-stretch. That is far less art than
four full recoloured 18-frame croc sheets, and it survives the new animation tags in
§6 without four sheets needing to be kept in sync.

## 6. Animation and art

The static feel is the oldest complaint and gets the largest pass. Two layers:

### 6.1 Procedural — no new art required

- **Bite variants.** Alternate a slight head tilt and a leading/trailing jaw offset per
  bite, so twenty bites in a row are not twenty identical events.
- **Idle life.** Slow positional drift, occasional blinks, a tail-weight sway.
- **Anticipation.** A crouch and a held beat before a phase transition and before a
  frenzy, so speed changes are led into rather than jumped to.
- **Food physics.** Items tumble as they ride and land with a small bounce on spawn,
  instead of sliding flat.
- **Camera.** A push-in on each phase transition, a shove on a knock-out, a settle on
  the bell.
- **Per-phase backdrop.** The arena shifts across the three phases — lights, crowd
  density, banner — so the act change is visible without a word.

### 6.2 Authored — new sprite work through the Aseprite MCP

- **Croc:** new tags added to `croc_sheet.png` — `chew`, `flinch`, `gulp`, `taunt`,
  `exhausted`. The existing `idle` / `eat` / `celebrate` tags stay.

All character art is generated, not painted: `Art/Tools/cast_gen.py` authors each
sprite as 16-column half-rows mirrored to 32x32, composes animation tags from pose
patches, and emits an Aseprite batch script that bakes the frames, durations and tags
into the `.aseprite` sources. A new tag is a pose patch plus a row in `animations()`,
which is why adding five of them is tractable. Nothing is hand-edited on top of the
generated output.
- **Rivals:** all four already have 18-frame sheets with `idle` / `celebrate` / `eat`,
  built by `Art/Tools/cast_gen.py`; what they lack is the reactive vocabulary the bout
  now needs. Each gains `react` and `panic` tags. `RivalView` today only ever plays
  `idle` and `eat`, so part of this is spending animation that already exists.
- **Buff-taken poses:** the croc gets a distinct frame per buff so SLOW and GOLD TOOTH
  are told apart by the sprite, not only by a banner.
- **Buff sprites:** four new 16x16 items (§2.2) plus the coin (§2.3).
- **Accessories:** four 16x16 cosmetics (§5).

Everything obeys the existing treatment: 16x16 (32x32 for the croc), transparent
background, 1px black outline, flat NES palette, no colour picked outside it. Sources
in `Art/RawSprites/`, exports in `Art/ExportedSprites/`.

## 7. Music

`Music/croc.mp3` — 72.8s, 256 kbps, already imported by Godot — becomes the theme, on a
loop under the whole game.

- A `MusicPlayer` node owned by `GameRoot`, started at the title and never restarted
  between screens, so the track is continuous rather than cutting on every transition.
- `AudioStreamMP3.Loop = true`. If the track has an intro that should not repeat, the
  loop point is set with `LoopOffset` — determined by ear, not guessed.
- It sits **well under the effects**. `design-principles.md` peak-limits SFX to 0.32
  precisely because stacked square waves get painful; music underneath them has to be
  quieter still, and the player cannot turn it down.
- The music carries state, the same way the chomp's pitch already does: it ducks and
  low-passes during dialogue interludes and the shop, and pitches up slightly during
  Frenzy. That is variation the player feels without anything new on screen.

## 8. Architecture

The Core/Godot boundary is unchanged and stays enforced by the absent GodotSharp
reference. Everything in §1–3 is Core and therefore testable with a fake clock;
everything in §4–7 is scene layer and verified by playing it.

New in `CrocGame.Core`:

| Type | Responsibility |
|---|---|
| `PhaseDef` | one phase's name, length, offsets, and flags (§1) |
| `BoutSession` | owns the phase list, the interludes, and the carried score; drives a `MatchSession` per phase |
| `Pot` | the wager: accrual, multiplier steps, banking, wiping (§2.3) |
| `Hunger` | the comeback meter and its window (§2.4) |
| `PowerUp` / `ActiveBuffs` | buff definitions and their live timers (§2.2) |

`MatchSession` narrows to **one phase** rather than a whole bout. `BoutSession` becomes
the façade the scene layer talks to, and `MatchState` splits: bout-level score, eaten,
and rival score live on the bout; strikes, combo, and the clock live on the phase.

New events: `PhaseStarted`, `PhaseEnded`, `PhaseKnockout`, `CoinSpawned`, `PotBanked`,
`PotWiped`, `BuffTaken`, `BuffExpired`, `HungerCharged`, `HungerStarted`,
`HungerEnded`. The scene layer's job remains rendering events and nothing else.

`GameRoot` is 637 lines today and this adds materially to it. The phase state machine,
the interlude sequencing, and the buff presentation move out into their own nodes as
part of the work — not as unrelated refactoring, but because the file being changed is
already at the size where changes stop being reliable.

## 9. Testing

Core is developed test-first, extending the existing xUnit suite:

- A bout runs exactly three phases and settles on carried total score, not per-phase.
- Strikes reset at each phase boundary; three strikes knocks out of the phase only.
- A knocked-out phase still advances the rival for its remaining seconds and still
  reaches the next phase.
- `HazardScale` of 0 produces no inedibles regardless of difficulty offset.
- Phase 3's ×2 applies to base score and to banked pot alike.
- Pot: accrual, each multiplier step boundary, banking empties it, a strike wipes it,
  a phase boundary wipes it, and the base score is never reduced by any of these.
- A coin cannot spawn while the pot is empty, and neither the pot nor a coin is ever
  live during PLAIN.
- Hunger charges only while behind, never while ahead, fires at most once per phase,
  and its window is reported at the same width the view is told to draw.
- Buffs: each effect applies and expires; shield absorbs exactly one strike; magnet
  consumes exactly three bites; two buffs of the same kind refresh rather than stack.
- Seeded RNG still reproduces an entire bout identically, buffs and coins included.
- Existing tests continue to pass, adjusted only where the bout/phase split moved a
  field.

The scene layer, the art, and the music are verified by playing the game.

## 10. Sequencing

Two implementation plans, because the two halves share almost no surface and the art
pass is roughly as large as everything above it:

**Plan 1 — mechanics.** §1, §2, §3, §4. Core-first and test-driven; `dotnet test`
proves the rules before anything is drawn. Ships playable with placeholder art for the
new buff and coin items.

**Plan 2 — art, cosmetics, and music.** §5, §6, §7. Aseprite MCP work, the accessory
system, and the music layer. Verified by eye and ear.

Plan 1 does not depend on Plan 2. Plan 2 depends on Plan 1 only for knowing which
sprites the buffs and coin need.

## 11. Non-goals

- No difficulty setting or assist toggle. The accessibility work is in the mechanics,
  where it is the same game for everyone, not in a menu that asks a player to declare
  themselves bad at it.
- No change to the timing judgement itself. The jaw zone, the overlap test, and the
  X-only movement rule are untouched.
- No new input. One verb, still.
- Nothing purchasable affects difficulty, scoring, or the window.
