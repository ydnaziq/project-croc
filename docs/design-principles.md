# Croc — design principles

Rules this project follows, written down because most of them were learned by getting
them wrong first. Each one names the failure it prevents.

## Art

**One line weight, everywhere.** Every edge is 1px black - sprites, UI panels, meters,
bubbles, text outlines. Mixed line weights are the single loudest tell that pixel art
was assembled rather than drawn. `Ui.Panel` and `Ui.Text` exist so nothing can quietly
opt out.

**Everything on screen comes from one palette and one grid.** NES-ish flat colours,
16px alignment, integer scaling only. A sprite at 1.5x, a soft gradient, or a colour
picked outside the palette will look wrong even when nobody can say why.

**Backgrounds need structure at more than one scale.** The first arena tiled a single
16x16 brick two hundred times; it read as noise, because it had no shape larger than a
tile. A backdrop needs large forms - a stage, a floor line, a banner - then detail
inside them.

**The background must lose to the foreground.** Darker, less saturated, lower contrast.
The croc and the food are what the player reads under time pressure; if the backdrop
competes, the game gets harder for the wrong reason.

**Effects must be made of the same stuff as the art.** Godot's default particles are
soft round dots and looked like a different medium pasted on top. Bursts are now
hard-edged squares snapped to whole pixels, coloured from the food palette.

**Characters need presence.** The croc is drawn at 2x and stands nearly a third of the
screen. A protagonist rendered at 32px on a 180px canvas is set dressing.

## Audio

**Match the medium.** AI-generated realistic sound against NES pixel art is a mismatch
no amount of processing fixes. Everything is synthesised chiptune - pulse, noise,
triangle - from `Art/Tools/sfx_gen.py`.

**Short.** The core sound fires several times a second; anything past ~100ms turns to
mud. Chomp is 62ms. Only jingles run long.

**Leave headroom.** Peak-limited to 0.32, not full scale. Stacked square waves get
painful within a minute, and the player cannot turn you down.

**Pitch carries state.** The chomp climbs with the combo, so a streak is audible before
it is legible.

## Game mechanics

**Judge what the player can see.** The timing window is the food's own width against
the jaw zone - a position overlap, not a hidden tolerance. Nothing may influence
judging that is not on screen, which is why every movement behaviour is along X only.

**Peaks, not just slope.** Escalation alone only gets harder. Frenzy makes good play
compound - double score, faster belt, killed instantly by a strike - so the match has a
shape instead of a ramp.

**Deny rhythm.** Gaps between items vary by up to 85% at high difficulty. A steady gap
lets the player lock into a metronome and stop reacting to what they see.

**An opponent, not a target score.** The rival eats visibly, reacts to the lead
changing, panics at a long combo. The same points landing beside a reacting rival feel
completely different from the same points landing in silence.

**Progression must not inflate skill.** Money buys cosmetics only. Nothing purchasable
touches difficulty, scoring, or the timing window, so every result stays comparable.

**Scarcity makes a choice.** The ladder pays 375; the shop costs 510. If you can buy
everything, you never chose anything.

## UI

**Show state, don't ask for arithmetic.** The tug-of-war bar tells you who is winning
at a glance. Two numbers make the player do subtraction while food goes past.

**Colour before words.** Win cards are green, loss cards red, frenzy popups a different
colour and size from ordinary ones. The reading is a confirmation, not the message.

**Show the thing, not its name.** Shop cards carry a swatch of the actual colour the
croc turns. Selling "MIDNIGHT" as a word is asking someone to buy a label.

**One verb.** A press anywhere advances every screen, because a press is the game's
whole vocabulary. Shop rows are tappable directly - no hidden cursor to explain.

**Pixel font at native sizes.** Silkscreen renders on an 8px grid, so only 8/16/24 are
used. Any other size resamples and goes soft, which breaks the line-weight rule.

## Overall

**Core knows the rules; the scene layer knows how it looks.** `CrocGame.Core` cannot
reference Godot - the compiler enforces it - so the rules are testable with a fake
clock and no engine. Feedback lives entirely on the other side of that line.

**Feedback is core, not polish.** Hit-stop, shake, flash, and popups were built with
the mechanic, not after it. A timing game with no feedback is unfixable by tuning.

**Freeze, never slow.** Hit-stop stops calling `Tick`. Because Core is dt-driven, a
pause can never eat part of a later timing window.

**Tune last, and expect to.** Every number in `Difficulty.cs` and `Career.cs` is a
guess until somebody plays it. They are gathered in one place for exactly that reason.
