"""NES-style competitive-eating food generator.

Five 16x16 props authored as full 16-char rows (no mirroring — a hotdog and a
pizza slice have no reason to be symmetric).  Each uses black for the outline,
a transparent background, and 5 flat colours.

Run from anywhere:  python3 food_gen.py [out_dir]
Writes <out_dir>/<name>.png plus food.png (the 5-item strip) and an 8x preview.
"""
import os, sys, zlib, struct

# --------------------------------------------------------------------- items

HOTDOG = dict(
    palette={'K': '#000000', 'b': '#f8b878', 'n': '#c87828', 's': '#c84c0c', 'M': '#f8d878'},
    rows=[
        '................', '................', '................',
        '...KKKKKKKKKK...', '..KssssssssssK..', '.KsMssMssMssMsK.', '.KssMssMssMssMK.',
        '.KssssssssssssK.', 'KbKKKKKKKKKKKKbK', 'KbbbbbbbbbbbbbbK', 'KbbbbbbbbbbbbbbK',
        'KnnnnnnnnnnnnnnK', '.KnnnnnnnnnnnnK.', '..KKKKKKKKKKKK..',
        '................', '................',
    ])

PIZZA = dict(
    palette={'K': '#000000', 'c': '#f8b838', 'y': '#f8d878', 'P': '#d82800', 'B': '#f8b878'},
    rows=[
        '................', '.......KK.......', '......KccK......', '......KcyK......',
        '.....KccccK.....', '.....KcPPcK.....', '....KccccccK....', '....KcycccyK....',
        '...KcPPcccccK...', '...KcPPccPPcK...', '..KcycccccccyK..', '..KcccPPcccccK..',
        '.KBBBBBBBBBBBBK.', '.KBBByBBBByBBBK.', '.KKKKKKKKKKKKKK.', '................',
    ])

PIE = dict(
    palette={'K': '#000000', 'W': '#fcfcfc', 'R': '#d82800', 't': '#f8b878', 'T': '#a84400'},
    rows=[
        '................', '................', '.......KK.......', '......KRRK......',
        '......KWWK......', '....KKWWWWKK....', '...KWWWWWWWWK...', '..KWWWWWWWWWWK..',
        '.KWWWWWWWWWWWWK.', 'KWWWWWWWWWWWWWWK', 'KKKKKKKKKKKKKKKK', 'KttttttttttttttK',
        '.KttttttttttttK.', '..KTTTTTTTTTTK..', '...KKKKKKKKKK...', '................',
    ])

BURGER = dict(
    palette={'K': '#000000', 'b': '#f8b878', 'G': '#58d854', 'C': '#f8d878', 'M': '#a84400'},
    rows=[
        '................', '................',
        '...KKKKKKKKKK...', '..KbbbbbbbbbbK..', '.KbbCbbbbCbbbbK.', 'KbbbbbbbCbbbbbbK',
        'KbbbbbbbbbbbbbbK', 'KGGGGGGGGGGGGGGK', 'KGCCGCCGCCGCCGCK', 'KCCCCCCCCCCCCCCK',
        'KMMMMMMMMMMMMMMK', 'KMMMMMMMMMMMMMMK', 'KbbbbbbbbbbbbbbK', '.KbbbbbbbbbbbbK.',
        '..KKKKKKKKKKKK..', '................',
    ])

DONUT = dict(
    palette={'K': '#000000', 'P': '#f878b8', 'd': '#f8b878', 's': '#fcfcfc', 'y': '#f8d878'},
    rows=[
        '.....KKKKKK.....', '...KKPPPPPPKK...', '..KPPsPPPPyPPK..', '.KPPPyPPPPsPPPK.',
        '.KPPPPPsPPyPPPK.', 'KPPPPPPKKPPPPPPK', 'KPsPPPKKKKPPPyPK', 'KPPsPK....KPyPPK',
        'KPPPPK....KPPsPK', 'KPPyPPK..KPsPPPK', 'KPPddPPKKPPddPPK', '.KdPPddddddPPdK.',
        '..KddddddddddK..', '..KddddddddddK..', '...KKddddddKK...', '.....KKKKKK.....',
    ])

FOOD = [('hotdog', HOTDOG), ('pizza', PIZZA), ('pie', PIE),
        ('burger', BURGER), ('donut', DONUT)]

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
    assert len(spec['palette']) == 5, f'{name}: {len(spec["palette"])} colours, want 5'
    for i, r in enumerate(rows):
        assert len(r) == 16, f'{name} row {i}: {len(r)} chars, want 16'
        for ch in r:
            assert ch == '.' or ch in spec['palette'], f'{name} row {i}: unknown "{ch}"'
    return rows


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(out, exist_ok=True)

    sheet = [['.'] * (16 * len(FOOD)) for _ in range(16)]
    sheet_pal = {}
    for i, (name, spec) in enumerate(FOOD):
        rows = check(name, spec)
        write_png(os.path.join(out, name + '.png'), rows, spec['palette'])
        for k, v in spec['palette'].items():
            sheet_pal[name[0] + k] = v
        for y in range(16):
            for x in range(16):
                if rows[y][x] != '.':
                    sheet[y][i * 16 + x] = name[0] + rows[y][x]
        print(f'{name:8s} 5 colours, 16x16')

    write_png(os.path.join(out, 'food.png'), sheet, sheet_pal)
    write_png(os.path.join(out, '_preview_food_8x.png'), sheet, sheet_pal, scale=8)
    print(f'food.png  {16 * len(FOOD)}x16, {len(FOOD)} items @ 16px')


if __name__ == '__main__':
    main()
