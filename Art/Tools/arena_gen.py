"""The arena backdrop, authored as one 180x320 image.

The previous background tiled a single brick tile across the whole screen, which is
why it read as "repeating brown squares with lighter specks": a 16x16 pattern repeated
~200 times has no structure at any scale larger than 16px. A backdrop needs large
shapes (the stage, the floor line, the curtain) so the eye has something to land on,
and it needs to sit *behind* the action tonally - darker and less saturated than the
sprites, or the food stops reading.

Conventions, shared with the rest of the art: NES-ish flat colours, and every shape
change carries a 1px black separator so line weight is uniform everywhere.

Run from anywhere:  python3 arena_gen.py [out_dir]
"""
import os, sys, zlib, struct

W, H = 180, 320

C = {
    'void':    (0x08, 0x08, 0x10),
    'wall_a':  (0x28, 0x28, 0x48),
    'wall_b':  (0x30, 0x30, 0x56),
    'wall_lo': (0x1a, 0x1a, 0x30),
    'stone':   (0x7c, 0x7c, 0x7c),
    'stone_d': (0x50, 0x50, 0x50),
    'stone_l': (0xbc, 0xbc, 0xbc),
    'wood':    (0xa8, 0x44, 0x00),
    'wood_l':  (0xc8, 0x78, 0x28),
    'wood_d':  (0x70, 0x2c, 0x00),
    'red':     (0xd8, 0x28, 0x00),
    'gold':    (0xf8, 0xd8, 0x78),
    'green':   (0x58, 0xd8, 0x54),
    'blue':    (0x68, 0x78, 0xc8),
    'crowd':   (0x38, 0x38, 0x60),
    'crowd_l': (0x4a, 0x4a, 0x78),
    'black':   (0x00, 0x00, 0x00),
    'white':   (0xf8, 0xf8, 0xf8),
}

px = [[C['wall_a'] for _ in range(W)] for _ in range(H)]


def rect(x, y, w, h, c):
    for yy in range(max(0, y), min(H, y + h)):
        for xx in range(max(0, x), min(W, x + w)):
            px[yy][xx] = C[c] if isinstance(c, str) else c


def hline(y, x0, x1, c):
    rect(x0, y, x1 - x0, 1, c)


def outline_box(x, y, w, h, fill, edge='black'):
    """A filled box with a 1px border - the uniform line weight rule, in one call."""
    rect(x, y, w, h, edge)
    rect(x + 1, y + 1, w - 2, h - 2, fill)


# ------------------------------------------------------------------ back wall
# Broad vertical panels rather than small repeating tiles: the eye reads columns,
# which is structure, instead of noise.
for i in range(0, W, 30):
    rect(i, 0, 15, 210, 'wall_b')
    hline_x = i + 15
    rect(hline_x, 0, 1, 210, 'wall_lo')

rect(0, 0, W, 26, 'wall_lo')          # darker band up top, under the HUD
hline(26, 0, W, 'black')

# ------------------------------------------------------------------ bunting
# A row of triangular flags. Cheap, and it breaks the flat top edge.
flag_colors = ['red', 'gold', 'green', 'blue']
hline(27, 0, W, 'stone_d')
for i, x in enumerate(range(4, W - 8, 16)):
    col = flag_colors[i % len(flag_colors)]
    for row in range(7):
        half = 7 - row
        rect(x + 7 - half, 28 + row, half * 2, 1, col)
    for row in range(7):
        half = 7 - row
        px[28 + row][max(0, x + 7 - half - 1)] = C['black']
        px[28 + row][min(W - 1, x + 7 + half)] = C['black']

# ------------------------------------------------------------------ banner
outline_box(26, 44, 128, 22, 'red')
rect(28, 46, 124, 3, 'wood_l')
rect(28, 61, 124, 3, 'wood_d')

# "EAT OFF" in blocky 5x7 letters, drawn as bars so no font is needed here.
GLYPHS = {
    'E': ["111", "100", "111", "100", "111"],
    'A': ["111", "101", "111", "101", "101"],
    'T': ["111", "010", "010", "010", "010"],
    'O': ["111", "101", "101", "101", "111"],
    'F': ["111", "100", "111", "100", "100"],
    ' ': ["000", "000", "000", "000", "000"],
}
word = "EAT OFF"
start = (W - (len(word) * 8)) // 2
for gi, ch in enumerate(word):
    g = GLYPHS[ch]
    for ry, row in enumerate(g):
        for rx, bit in enumerate(row):
            if bit == '1':
                rect(start + gi * 8 + rx * 2, 49 + ry * 2, 2, 2, 'gold')

# ------------------------------------------------------------------ stage shelf
# The horizontal that separates rival from player. A strong line, once.
rect(0, 118, W, 10, 'stone')
hline(118, 0, W, 'black')
hline(127, 0, W, 'black')
for x in range(0, W, 20):
    rect(x, 119, 1, 8, 'stone_d')
    rect(x + 1, 119, 3, 2, 'stone_l')

# ------------------------------------------------------------------ crowd
# Silhouettes only: heads and shoulders, two staggered rows, no detail. They are
# scenery, and detail here would compete with the food.
def head(cx, cy, c):
    rect(cx - 4, cy - 4, 8, 8, c)
    rect(cx - 5, cy - 2, 1, 5, c)
    rect(cx + 4, cy - 2, 1, 5, c)
    rect(cx - 7, cy + 4, 14, 6, c)

for i, x in enumerate(range(8, W, 21)):
    head(x, 150, 'crowd_l')
for i, x in enumerate(range(-2, W, 21)):
    head(x, 162, 'crowd')

hline(172, 0, W, 'black')
rect(0, 173, W, 37, 'wall_lo')

# ------------------------------------------------------------------ player wall
# Lighter directly behind the croc so the sprite separates from the background.
rect(0, 200, W, 56, 'wall_a')
for i in range(0, W, 30):
    rect(i + 15, 200, 1, 56, 'wall_lo')
hline(200, 0, W, 'black')

# ------------------------------------------------------------------ floor
rect(0, 268, W, H - 268, 'wood')
hline(268, 0, W, 'black')
for y in range(272, H, 10):
    hline(y, 0, W, 'wood_d')
# Staggered plank seams so the floor does not read as stripes.
for row, y in enumerate(range(268, H, 10)):
    offset = 0 if row % 2 == 0 else 22
    for x in range(offset, W, 44):
        rect(x, y + 1, 1, 9, 'wood_d')
rect(0, 268, W, 2, 'wood_l')


def write_png(path, rows, scale=1):
    w, h = len(rows[0]) * scale, len(rows) * scale
    raw = bytearray()
    for row in rows:
        line = bytearray()
        for c in row:
            line += bytes(c + (255,)) * scale
        for _ in range(scale):
            raw.append(0)
            raw += line
    ck = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n'
                + ck(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
                + ck(b'IDAT', zlib.compress(bytes(raw), 9))
                + ck(b'IEND', b''))


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(out, exist_ok=True)
    write_png(os.path.join(out, 'arena.png'), px)
    write_png(os.path.join(out, '_preview_arena_2x.png'), px, scale=2)
    print(f'arena.png {W}x{H}')


if __name__ == '__main__':
    main()
