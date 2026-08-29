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

**Cosmetics are objects, not tints.** Multiplying a flat five-colour sprite by a colour
produces colours that are in no palette and mostly just makes the sprite muddy. A hat
is a hat.

**An accessory must deform with what it sits on.** The worn cosmetic is a child of the
croc sprite, so it squashes with the bite. One that holds still reads as a sticker on
the screen.

**Animation is generated, not painted.** Every character frame comes from
`Tools/cast_gen.py` - pose patches composed into tags, baked through Aseprite. A frame
painted by hand on top is lost at the next regeneration, and the generator's symmetry
and palette assertions are what keep thirty frames consistent.

**New tags append, never insert.** `flinch`, `gulp` and `taunt` went after frame 17 so
`idle`, `celebrate` and `eat` keep their indices. Inserting a tag silently renumbers
every frame every reader depends on.

**A sprite must match its hitbox.** Each power-up's drawn width equals the width
declared in `food.json`, because a food's own width *is* its timing window. `food_gen.py`
prints the used column count so a mismatch is caught at generation time.

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

**Show the window.** The bite zone is drawn on the belt from the same constants the
judge uses, and lights up when something is inside it. An invisible timing window makes
a game feel unfair rather than hard, and the player cannot learn from a mistake they
could not see themselves make.

**Punish mistakes, not the clock.** Food riding past costs the streak and the points,
never a strike. Strikes are for biting nothing or biting a bomb. If the belt can
disqualify someone who is playing correctly, difficulty stops being a skill question.

**Forgive the follow-through.** A bite that lands buys 180ms where a second press
costs nothing. Hands double-tap when a hit feels good; charging for that reads as the
game being fussy.

**Judge what the player can see.** The timing window is the food's own width against
the jaw zone - a position overlap, not a hidden tolerance. Nothing may influence
judging that is not on screen, which is why every movement behaviour is along X only.

**Peaks, not just slope.** Escalation alone only gets harder. Frenzy makes good play
compound - double score, faster belt, killed instantly by a strike - so the match has a
shape instead of a ramp.

**Deny rhythm.** Gaps between items vary by up to 85% at high difficulty. A steady gap
lets the player lock into a metronome and stop reacting to what they see.

**Vary the shape, not just the speed.** Bursts of three near-simultaneous items, rare
high-value bites with tighter windows, and hazards that must be let past all change
what the player is doing rather than how fast. A change of pace has to stay a minority:
cluster odds cap at 22%, because if most food arrives in bursts, bursts become the pace.

**Three acts, not one block.** A bout is PLAIN, HAZARD, FEAST, with dialogue between.
Short bouts still matters - each phase is 8-10 seconds, a single burst of concentration
- but one undifferentiated block has no shape to talk over and nothing to introduce.

**A strike ends a phase, never a run.** Three strikes knocks the croc out of the
current phase and hands the rival its remaining seconds. Losing that badly still hurts,
visibly, but a beginner who blows the first act still plays the other two and can still
win. Anything that can end a run is a ceiling.

**Give the losing player something, automatically.** Hunger charges only from a deficit
and fires by itself. A comeback mechanic that has to be earned or activated is no use
to the player who needs it, because that player is the one already struggling with the
inputs.

**The wager uses the verb you already have.** The pot is banked by biting a coin, so
push-your-luck costs no new button and no menu: the decision is a position on the belt
and a number drawn on the sprite.

**Never risk what the player already scored.** The pot is upside stacked on the score.
Banking every coin on sight is safe, viable play - greed is an option, not a tax.

**Pay for strength with window width.** SLOW is 16px wide and arrives alone; a GOLD
TOOTH is 8px and arrives between two bombs. Risk and reward are the same axis, and it
is one the player can read from across the screen.

**Short bouts.** A phase runs 8-10 seconds and a whole bout is 27 seconds of play,
broken by two interludes. Past about half a minute of *continuous* timing the pressure
stops being exciting and starts being work, which is why the breaks are load-bearing
rather than decorative.

**The crowd is on your side.** It reacts to how the player is playing, not to who is
winning, and an ordinary mistake gets a sympathetic "aww" rather than a boo. Booing is
reserved for losing every tooth inside one phase. A game whose whole premise is that
anyone can finish it must not aim its harshest sound at the player already having the
worst time.

**A mood is a dial, not a switch.** Crowd hype drives bob height, bob speed,
brightness, and how many are on their feet, all at once - so the stand is always saying
something without ever shouting. It decays back to a floor, and resets each phase, so
FEAST has to earn its noise instead of inheriting it from a good PLAIN.

**Only moments get a voice.** Every bite moves the crowd's mood; only milestones,
frenzy, hunger and a banked pot make a sound, behind the same cooldown that keeps the
rival from becoming wallpaper. A crowd that reacts to everything is static.

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

**A meter must read the right way round.** Strikes were three pips that filled red as
they were spent, so a lit pip looked like something you *had*. Health-shaped UI has to
show what remains, not what is gone - the teeth are present or knocked out.

**Every state change gets a moment.** Losing a tooth animates, the lead changing flares
the bar, a rival's bite flashes their sprite, the clock beats in the closing seconds. A
state change communicated only by a colour swap on a small element will be missed while
the player is watching the belt.

**Colour before words.** Win cards are green, loss cards red, frenzy popups a different
colour and size from ordinary ones. The reading is a confirmation, not the message.

**Show the thing, not its name.** Shop cards carry the actual object the croc will
wear, drawn at 1:1. This started as a colour swatch, which was halfway there; selling
"MIDNIGHT" as a word is asking someone to buy a label.

**One verb.** A press anywhere advances every screen, because a press is the game's
whole vocabulary. Shop rows are tappable directly - no hidden cursor to explain.

**Measure text; never estimate it.** Every overflowing box in this project came from
sizing a panel by character count instead of asking the font. `Ui.Measure` and
`Ui.WrappedLabel` exist so a box is built around its text, and titles step down a size
rather than running off the card.

**Staging says who is talking.** In dialogue the speaker is lit and scaled up while the
listener greys back. At 180px wide there is no room for a portrait, a name plate, and a
line, so the picture carries what the box would otherwise have to.

**Measure height too, not just width.** Boxes used to be a fixed size with text at
fixed offsets, so nothing was ever actually centred - it was positioned to look centred
for one particular string. `Ui.LayoutColumn` sizes the box around the block and centres
the block in the box.

**Lay out before you draw, never during.** `ScreenOverlay` positioned its labels inside
`_Draw`. Changing a Control's rect during the draw pass re-enters Godot's layout and
hangs the frame with no error at all. Drawing draws; it does not decide where things go.

**Every readout needs its own lane.** At 180px wide, a new HUD element will land on an
existing one. The phase name was added at the same y as the rival's name plate; the fix
was not to move it but to delete it, because the pips and the phase banner already said
it.

**Pixel font at native sizes.** Silkscreen renders on an 8px grid, so only 8/16/24 are
used. Any other size resamples and goes soft, which breaks the line-weight rule.

## Overall

**Core knows the rules; the scene layer knows how it looks.** `CrocGame.Core` cannot
reference Godot - the compiler enforces it - so the rules are testable with a fake
clock and no engine. Feedback lives entirely on the other side of that line.

**Deform, don't just swap frames.** The croc squashes wide on a bite and eases back.
Animation frames alone read as a flipbook; deformation reads as weight.

**Spend everything on the rare moment.** The golden bite gets a longer hit-stop, a zoom
punch, a gold wash, a bigger burst, its own sound and a rival reaction all at once.
Effects that fire constantly stop being events.

**Feedback is core, not polish.** Hit-stop, shake, flash, and popups were built with
the mechanic, not after it. A timing game with no feedback is unfixable by tuning.

**Freeze, never slow.** Hit-stop stops calling `Tick`. Because Core is dt-driven, a
pause can never eat part of a later timing window.

**Tune last, and expect to.** Every number in `Difficulty.cs` and `Career.cs` is a
guess until somebody plays it. They are gathered in one place for exactly that reason.
