# Audio

Thirteen chiptune sound effects, synthesised by `../Tools/sfx_gen.py` and committed as Ogg.

## Why these are synthesised rather than AI-generated

The first pass used the Artificial Studio `sound-effects` tool. The results were
technically fine and wrong for this game: realistic recorded-sounding audio against
NES pixel art reads as two different products, and every cue ran about a second, which
turns to mud in a game that fires its core sound several times a second.

These are built from the vocabulary a NES actually had - pulse waves with variable
duty, a 15-bit LFSR noise channel, and a triangle channel - so they sit *with* the art
instead of beside it. They are also short, deterministic, and free to regenerate.

Everything is normalised to 0.32 peak rather than full scale. These sounds stack, and
stacked square waves get painful fast.

Because normalisation gives every file the same peak, relative loudness cannot be baked
in and is set at playback instead - `Sfx.Play` takes a `volumeDb`. The crowd depends on
that: the cheer plays at full, the "aww" at -7dB and the boo at -4dB, so the room is
loudest when it is pleased with you.

The three crowd cues deliberately break the "keep it short" rule above. That rule
exists because the chomp fires several times a second and turns to mud; the crowd fires
every few seconds at most, and a room full of people cannot say anything in 80ms.

| File | ms | Played when |
|---|---|---|
| `chomp.ogg` | 62 | a chomp lands on food |
| `crunch.ogg` | 77 | a chomp lands during a combo or frenzy |
| `whiff.ogg` | 90 | a chomp closes on empty air |
| `pass.ogg` | 90 | food rides past unchomped |
| `strike.ogg` | 230 | a strike is added |
| `coin.ogg` | 185 | prize money is awarded |
| `blip.ogg` | 35 | menu or screen advance |
| `frenzy.ogg` | 250 | frenzy mode starts |
| `win.ogg` | 565 | the croc wins a match |
| `lose.ogg` | 720 | the croc loses a match |
| `cheer.ogg` | 480 | the crowd approves: a combo milestone, frenzy, hunger, a banked pot, a win |
| `aww.ogg` | 400 | the crowd commiserates: a strike, or losing on points |
| `boo.ogg` | 480 | the crowd turns: every tooth lost inside one phase, and nothing else |

## Regenerating

    python3 Art/Tools/sfx_gen.py /tmp/sfx
    for f in /tmp/sfx/*.wav; do ffmpeg -y -i "$f" -c:a libvorbis -q:a 5 "Art/Audio/$(basename $f .wav).ogg"; done

Edit the synth functions in `sfx_gen.py` to change how anything sounds.
