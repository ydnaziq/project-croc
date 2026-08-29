"""Cosmetics the croc wears, generated the same way as everything else.

Four 16x16 accessories, 1px black outline, transparent background, colours taken from
palettes already in use elsewhere in the game.

These replaced a colour tint. Multiplying a flat five-colour sprite by a colour
produces colours that are in no palette, mostly just makes the croc muddy, and sells
the player a word rather than a thing - a shop card showing "MIDNIGHT" is asking
someone to buy a label.

Each carries an anchor: where it sits on the croc's 32x32 frame, measured from the
sprite's centre. The anchors are printed on every run, and Scripts/CrocView.cs uses
those same numbers rather than guessing them.

Run from anywhere:  python3 cosmetic_gen.py [out_dir]
"""
import os, sys, zlib, struct

# --------------------------------------------------------------------- items

CHEF = dict(   # a toque, sitting on top of the head
    anchor=(0, -11),
    palette={'K': '#000000', 'W': '#f8f8f8', 'S': '#bcbcbc'},
    rows=[
        '................', '................', '....KKK..KKK....',
        '...KWWWKKWWWK...', '..KWWWWWWWWWWK..', '..KWWWWWWWWWWK..',
        '..KWWWWWWWWWWK..', '...KWWWWWWWWK...', '...KWWWWWWWWK...',
        '..KKKKKKKKKKKK..', '..KWWWWWWWWWWK..', '..KSSSSSSSSSSK..',
        '..KKKKKKKKKKKK..', '................', '................',
        '................',
    ])

GOLD = dict(   # one oversized gold fang, hanging on the jaw line
    anchor=(4, 4),
    palette={'K': '#000000', 'y': '#f8d878', 'o': '#f8b838', 'W': '#f8f8f8'},
    rows=[
        '................', '................', '................',
        '................', '................', '......KKKK......',
        '......KWyoK.....', '......KyyoK.....', '......KyyoK.....',
        '.......KyoK.....', '.......KyoK.....', '........KK......',
        '................', '................', '................',
        '................',
    ])

SHADES = dict(  # wraparound shades across the eye row
    anchor=(0, -4),
    palette={'K': '#000000', 'B': '#383870', 'b': '#6878c8', 'W': '#f8f8f8'},
    rows=[
        '................', '................', '................',
        '................', '..KKKKKKKKKKKK..', '..KBBBBKKBBBBK..',
        '..KBWBBKKBBBBK..', '..KBBBBKKBBBBK..', '..KKKKKKKKKKKK..',
        '...KbK......KbK.', '................', '................',
        '................', '................', '................',
        '................',
    ])

NEON = dict(   # a small crown
    anchor=(0, -12),
    palette={'K': '#000000', 'C': '#58f8d8', 'G': '#58d854', 'W': '#f8f8f8'},
    rows=[
        '................', '................', '................',
        '..K..K....K..K..', '..KK.KK..KK.KK..', '..KCKKCKKCKKCK..',
        '..KCCCCCCCCCCK..', '..KCWCCCCCCCCK..', '..KCCCCCCCCCCK..',
        '..KGGGGGGGGGGK..', '..KKKKKKKKKKKK..', '................',
        '................', '................', '................',
        '................',
    ])

COSMETICS = [('skin_chef', CHEF), ('skin_gold', GOLD),
             ('skin_shadow', SHADES), ('skin_neon', NEON)]

# ---------------------------------------------------------------------- png


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


def check(name, spec):
    rows = spec['rows']
    assert len(rows) == 16, f'{name}: {len(rows)} rows, want 16'
    assert len(spec['palette']) <= 5, f'{name}: {len(spec["palette"])} colours, want 5 or fewer'
    for i, r in enumerate(rows):
        assert len(r) == 16, f'{name} row {i}: {len(r)} chars, want 16'
        for ch in r:
            assert ch == '.' or ch in spec['palette'], f'{name} row {i}: unknown "{ch}"'
    return rows


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(out, exist_ok=True)

    for name, spec in COSMETICS:
        rows = check(name, spec)
        write_png(os.path.join(out, name + '.png'), rows, spec['palette'])
        ax, ay = spec['anchor']
        print(f'{name:12s} {len(spec["palette"])} colours, anchor ({ax}, {ay})')

    print('anchors above must match CrocView.CosmeticAnchor')


if __name__ == '__main__':
    main()
