# Audio

Six sound effects, generated once through the Artificial Studio MCP server
(`sound-effects` tool, model `elevenlabs-sound-effects`, 1 credit per second) and
committed as artifacts. Nothing is generated at build time or at runtime.

Each was trimmed of leading and trailing silence and loudness-normalised to
`I=-16 TP=-1.5 LRA=11` with ffmpeg, then encoded to Ogg Vorbis for Godot.

| File | Played when | Prompt |
|---|---|---|
| `chomp.ogg` | a chomp lands on edible food | short crisp retro 8-bit video game chomp, single quick bite crunch blip, chiptune, dry, no reverb |
| `whiff.ogg` | a chomp closes on empty air | short retro 8-bit whiff miss sound, jaws snapping on empty air, quick descending chiptune blip, dry |
| `pass.ogg` | edible food rides past unchomped | short dull soft retro 8-bit thud, low muted blip, something slipping past, dry chiptune |
| `strike.ogg` | a strike is added | short harsh retro 8-bit arcade error buzz, descending two tone penalty sting, chiptune, dry |
| `gameover.ogg` | the third strike ends the run | retro 8-bit arcade game over jingle, short sad descending chiptune melody, dry, no reverb |
| `blip.ogg` | title or game-over screen advances | very short bright retro 8-bit menu select blip, ascending chirp, chiptune, dry |

All six used `prompt_influence: 0.7`; durations were `1s` except `gameover.ogg` at `2s`.

## Regenerating

Re-run the same prompts through the `sound-effects` tool and repeat the ffmpeg step:

    ffmpeg -i in.mp3 -af "silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.01,areverse,silenceremove=start_periods=1:start_threshold=-50dB,areverse,loudnorm=I=-16:TP=-1.5:LRA=11" -c:a libvorbis -q:a 4 -ar 44100 out.ogg
