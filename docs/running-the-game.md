# Running and screenshotting the game

The project is developed on a machine with no Godot editor open, so there is a
self-playing harness that runs the real game and writes screenshots. Use it to check
anything visual instead of reasoning about coordinates.

## Engine

Godot 4.7.2 **mono** (the C# build). If it is not already at
`~/.cache/godot-bin/`, fetch it:

    mkdir -p ~/.cache/godot-bin && cd ~/.cache/godot-bin
    curl -sSL -o godot.zip https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_linux_x86_64.zip
    unzip -oq godot.zip

    GODOT=~/.cache/godot-bin/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64

## Import once after adding art

    "$GODOT" --headless --import --path .

## Play it normally

    "$GODOT" --path .

## Autoplay with screenshots

    LIBGL_ALWAYS_SOFTWARE=1 "$GODOT" --path . --autoplay --rendering-driver opengl3 --shots=/tmp/shots

**The two extra flags are not optional on this machine.** Rendering to a window on the
Intel Iris Xe GPU stalls after about five seconds: the main thread blocks in
`drm_syncobj_array_wait_timeout`, a kernel DRM fence wait, and never comes back. The
run dies with four or five screenshots written and no error message of any kind.

It is a driver-level stall, not a game bug. The evidence, if it ever needs re-checking:

- It reproduces on any commit, including ones from before this harness had a fault.
- It happens on both the Vulkan and the OpenGL renderer, and under both the Wayland
  and the X11 display driver - so it is neither renderer- nor compositor-specific.
- `--headless` runs to completion at ~145fps, because it never touches the GPU.
- The blocked thread's `wchan` is the DRM fence wait above, and CPU time barely
  advances while it is stuck.

`LIBGL_ALWAYS_SOFTWARE=1` puts Mesa on `swrast`, so frames are rasterised on the CPU
and no GPU fence is ever waited on. The whole 55-second run then completes at around
45fps, which is more than enough for the harness.

To check game logic without needing images at all, `--headless` is faster:

    "$GODOT" --path . --autoplay --headless

It drives every screen exactly as the windowed run does, but its dummy renderer cannot
produce screenshots.

`--autoplay` builds `Scripts/AutoPlay.cs`, which advances every screen and bites
whenever edible food is inside the jaw zone - the same information a player has, so it
cannot pass something a human would fail. It quits after 55 seconds, which is long
enough to reach the shop now that a bout is three phases plus two interludes. `--shots=<dir>` writes PNGs at fixed times. Without the flag
the harness never instantiates.

Note that this opens a real window on the current display; there is no Xvfb here, and
Godot's `--headless` mode uses a dummy renderer that cannot produce images.

## Reading a screenshot

The window is not an integer multiple of the 180x320 canvas, so when cropping a region
compute the scale as `width / 180.0`, not with integer division - rounding it to 3x
instead of 3.99x samples the wrong rows entirely.

## Regenerating assets

Art and audio are generated and committed as artifacts; nothing is generated at build
or run time. See `Art/README.md` for the cast, food, power-up and cosmetic pipelines,
and `Art/Audio/README.md` for the sound effects and the music loop.

    cd Art/Tools
    python3 cast_gen.py ../ExportedSprites ../RawSprites   # characters, 30 frames each
    python3 food_gen.py ../ExportedSprites                 # food, power-ups, the coin
    python3 cosmetic_gen.py ../ExportedSprites/Cosmetics   # what the croc wears
    python3 arena_gen.py ../ExportedSprites                # the arena, minus the crowd
    python3 sfx_gen.py /tmp/sfx                            # sound effects, as wav

After changing any of them, re-import before running:

    "$GODOT" --headless --import --path .

## Tests

    dotnet test

Covers `CrocGame.Core` only, which is where the rules live. The scene layer is checked
with the harness above.
