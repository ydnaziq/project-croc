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

    "$GODOT" --path . --autoplay --shots=/tmp/shots

`--autoplay` builds `Scripts/AutoPlay.cs`, which advances every screen and bites
whenever edible food is inside the jaw zone - the same information a player has, so it
cannot pass something a human would fail. It quits after 45 seconds, which is long
enough to reach the shop. `--shots=<dir>` writes PNGs at fixed times. Without the flag
the harness never instantiates.

Note that this opens a real window on the current display; there is no Xvfb here and
Godot's `--headless` mode uses a dummy renderer that cannot produce images.

## Reading a screenshot

The window is not an integer multiple of the 180x320 canvas, so when cropping a region
compute the scale as `width / 180.0`, not with integer division - rounding it to 3x
instead of 3.99x samples the wrong rows entirely.

## Tests

    dotnet test

Covers `CrocGame.Core` only, which is where the rules live. The scene layer is checked
with the harness above.
