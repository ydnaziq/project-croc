"""NES-style inedible props for the croc timing game.

Two 16x16 hazards that ride the same conveyor as the food. Chomping one costs a
strike; letting one pass is correct play, so they have to read as obviously
*not food* at a glance and at speed.

Same conventions as food_gen.py: black outline, transparent background, 5 flat
colours, authored as character rows. Rows are padded to 16x16 here rather than
hand-counted.

Run from anywhere:  python3 inedible_gen.py [out_dir]
"""
import os, sys, zlib, struct

BOMB = dict(
    palette={'K': '#000000', 'B': '#585858', 'H': '#a4a4a4', 'F': '#c87828', 'S': '#f8d878'},
    rows=[
        '',
        '.............S',
        '............SFS',
        '...........SF',
        '..........FF',
        '....KKKK..F',
        '..KKBBBBKK',
        '.KBBHHBBBK',
        '.KBHHBBBBBK',
        '.KBHBBBBBBK',
        '.KBBBBBBBBK',
        '.KBBBBBBBBK',
        '..KBBBBBBK',
        '...KKKKKK',
    ])

BOOT = dict(
    palette={'K': '#000000', 'L': '#a84400', 'l': '#c87828', 'S': '#585858', 'H': '#f8b878'},
    rows=[
        '',
        '....KKKKK',
        '...KlllllK',
        '...KlHLllK',
        '...KlllllK',
        '...KlllllK',
        '...KlllllK',
        '...KlllllKKKK',
        '...KlllllllllK',
        '..KllllllllllK',
        '..KllllllllllK',
        '..KSSSSSSSSSSK',
        '..KSSSSSSSSSSK',
        '...KKKKKKKKKK',
    ])

ITEMS = [('bomb', BOMB), ('boot', BOOT)]


def normalise(spec):
    """Pad every row to 16 chars and the sprite to 16 rows, so the art can be
    authored as ragged strings without hand-counting dots."""
    rows = [r.ljust(16, '.')[:16] for r in spec['rows']]
    while len(rows) < 16:
        rows.append('.' * 16)
    return rows[:16]


def write_png(path, rows, palette, scale=1):
    hx = lambda s: (int(s[1:3], 16), int(s[3:5], 16), int(s[5:7], 16))
    w, h = len(rows[0]) * scale, len(rows) * scale
    raw = bytearray()
    for row in rows:
        line = bytearray()
        for ch in row:
            px = bytes((0, 0, 0, 0)) if ch == '.' else bytes(hx(palette[ch]) + (255,))
            line += px * scale
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

    for name, spec in ITEMS:
        rows = normalise(spec)
        assert len(spec['palette']) == 5, f'{name}: want 5 colours'
        for i, r in enumerate(rows):
            for ch in r:
                assert ch == '.' or ch in spec['palette'], f'{name} row {i}: unknown "{ch}"'

        write_png(os.path.join(out, name + '.png'), rows, spec['palette'])
        write_png(os.path.join(out, f'_preview_{name}_8x.png'), rows, spec['palette'], scale=8)
        print(f'{name:8s} 5 colours, 16x16')


if __name__ == '__main__':
    main()
