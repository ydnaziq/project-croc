"""NES-style cast generator.

Every character is authored as 16-column half-rows (x = 0..15) and mirrored to
32x32, so the sprites are symmetric by construction rather than by eye.  Each
uses black for the outline, a transparent background, and 4-5 flat colours.

Run from anywhere:  python3 cast_gen.py [out_dir]
Writes <out_dir>/<name>.png at native resolution plus a contact sheet.
"""
import os, sys, zlib, struct

# ---------------------------------------------------------------- characters

CROC = dict(
    palette={'K': '#000000', 'd': '#007800', 'g': '#58d854', 'y': '#f8d878', 'W': '#f8f8f8'},
    half=[
        '................', '................',
        '.........KKKKKKK', '.......KKggggggg', '......Kggggggggg', '.....Kgggggggggg',
        '.....KgggKKKgggg', '.....KgggKKKgggg', '.....KgggKKKgggg', '.....Kgggggggggg',
        '.....Kgggggggggg', '.....KgggggggKgg', '.....Kgggggggggg', '.....KggKKKKKKKK',
        '.....KggKWKKWKKW', '.....KggKKKKKKKK', '......Kggggggggg', '.......Kgggggggg',
        '.....KKKKggggggg', '.....Kdddggyyyyy', '.....Kdddggyyyyy', '.....Kdddggyyyyy',
        '.....Kdddggyyyyy', '.....Kdddggyyyyy', '.....Kdddggyyyyy', '.....Kdddgggyyyy',
        '.....KgggggggggK', '.....KgggggggggK', '.....KggKggKgggK', '.....KKKKKKKKKKK',
        '................', '................',
    ])

PENGUIN = dict(
    palette={'K': '#000000', 'B': '#383870', 'b': '#6878c8', 'W': '#f8f8f8', 'O': '#f87800'},
    half=[
        '................', '................',
        '..........KKKKKK', '........KKBBBBBB', '.......KBBBBBBBB', '......KBBBBBBBBB',
        '......KBBBBWWWWW', '......KBBBBWKKWW', '......KBBBBWKKWW', '......KBBBBWWWWW',
        '......KBBBBWWOOO', '......KBBBBWWWOO', '......KBBBBWWWWW', '.....KbbBBBWWWWW',
        '.....KbbBBBWWWWW', '.....KbbBBBWWWWW', '.....KbbBBBWWWWW', '.....KbbBBBWWWWW',
        '.....KbbBBBWWWWW', '.....KbbBBBWWWWW', '.....KbbBBBWWWWW', '.....KbbBBBWWWWW',
        '.....KbbBBBWWWWW', '.....KbbBBBWWWWW', '.....KbbBBBWWWWW', '.....KbbBBBBWWWW',
        '.....KBBBBBBBBBB', '.....KOOOOOOOKKK', '.....KOOOOOOOK..', '.....KKKKKKKKK..',
        '................', '................',
    ])

CAT = dict(
    palette={'K': '#000000', 'o': '#a84400', 'O': '#f87858', 'W': '#f8d8b8', 'P': '#f878b8'},
    half=[
        '................', '................',
        '.......KK.......', '......KOOK......', '.....KOOOOK.....', '.....KOOOOOKKKKK',
        '.....KOOOOOOOOOO', '.....KOOOOOOOOOO', '.....KOOOKKOOOOO', '.....KOOOKKOOOOO',
        '.....KOOOOOOOOOO', '.....KOOOOOWWWPP', '.....KOOOOOWWWKK', '.....KOOOOOWKWWW',
        '.....KOOOOOWWWWW', '.....KOOOOOOOOOO', '......KOOOOOOOOO', '.......KOOOOOOOO',
        '.....KKKKOOOOOOO', '.....KoooOOWWWWW', '.....KoooOOWWWWW', '.....KoooOOWWWWW',
        '.....KoooOOWWWWW', '.....KoooOOWWWWW', '.....KoooOOWWWWW', '.....KoooOOOWWWW',
        '.....KOOOOOOOOOK', '.....KOOOOOOOOOK', '.....KOOKOOKOOOK', '.....KKKKKKKKKKK',
        '................', '................',
    ])

ROBOT = dict(
    palette={'K': '#000000', 's': '#7c7c7c', 'S': '#bcbcbc', 'C': '#58f8d8', 'R': '#f83800'},
    half=[
        '................', '................', '..............KK', '..............KR',
        '..............KS', '.....KKKKKKKKKKK', '.....KSSSSSSSSSS', '.....KSSKCCCCCCC',
        '.....KSSKCCKKCCC', '.....KSSKCCCCCCC', '.....KSSKKKKKKKK', '.....KSSSSSSSSSS',
        '.....KSSSKssssss', '.....KSSSKssssss', '.....KSSSKKKKKKK', '.....KSSSSSSSSSS',
        '......KSSSSSSSSS', '.......KSSSSSSSS', '.....KKKKSSSSSSS', '.....KsssSSCCCCC',
        '.....KsssSSCCCCC', '.....KsssSSCCCRR', '.....KsssSSCCCCC', '.....KsssSSCCCCC',
        '.....KsssSSCCCCC', '.....KsssSSSCCCC', '.....KSSSSSSSSSK', '.....KSSSSSSSSSK',
        '.....KSSKSSKSSSK', '.....KKKKKKKKKKK', '................', '................',
    ])

SLIME = dict(
    palette={'K': '#000000', 'v': '#6800a8', 'V': '#b840f8', 'p': '#e0a8f8', 'W': '#f8f8f8'},
    half=[
        '................', '................', '................', '................',
        '................', '................',
        '..........KKKKKK', '........KKVVVVVV', '.......KVVVVVVpp', '......KVVVVVVVpp',
        '......KVVVVVVVVV', '.....KVVVVVVVVVV', '.....KVVVWWWVVVV', '.....KVVVWKKVVVV',
        '.....KVVVWKKVVVV', '.....KVVVWWWVVVV', '.....KVVVVVVVVVV', '.....KVVVVVVVKVV',
        '.....KVVVVVVVVKK', '.....KVVVVVVVVVV', '....KVVVVVVVVVVV', '....KVVVVVVVVVVV',
        '....KvVVVVVVVVVV', '....KvvVVVVVVVVV', '....KvvvVVVVVVVV', '....KvvvvVVVVVVV',
        '....KvvvvvvvVVVV', '....Kvvvvvvvvvvv', '....Kvvvvvvvvvvv', '....KKKKKKKKKKKK',
        '................', '................',
    ])

CAST = [('croc', CROC), ('penguin', PENGUIN), ('cat', CAT), ('robot', ROBOT), ('slime', SLIME)]

# ------------------------------------------------------------------- tileset

TILE = 16
TILE_COLS, TILE_ROWS = 4, 5
TILE_PALETTE = {
    'K': '#000000', 'b': '#4c2800', 'B': '#a05000', 'n': '#d08040',
    'd': '#007800', 'g': '#58d854', 'h': '#a8f858',
    's': '#7c7c7c', 'S': '#bcbcbc', 'w': '#0058f8', 'W': '#3cbcfc',
    'y': '#f8d878',
}


def blank_tile(ch='.'):
    return [[ch] * TILE for _ in range(TILE)]


def rect(t, x0, y0, x1, y1, ch):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if 0 <= x < TILE and 0 <= y < TILE:
                t[y][x] = ch


def speckle(t, coords, ch):
    for x, y in coords:
        t[y][x] = ch


def t_dirt(top=False, left=False, right=False):
    t = blank_tile('B')
    speckle(t, [(3, 6), (4, 6), (10, 9), (11, 9), (6, 12), (13, 4)], 'n')
    speckle(t, [(2, 11), (8, 7), (12, 13)], 'b')
    if top:
        rect(t, 0, 0, 15, 2, 'g')
        rect(t, 0, 3, 15, 3, 'd')
        for x in (1, 4, 5, 9, 12, 13):
            t[0][x] = 'h'
        rect(t, 0, 4, 15, 4, 'b')
    if left:
        rect(t, 0, 4 if top else 0, 0, 15, 'b')
    if right:
        rect(t, 15, 4 if top else 0, 15, 15, 'b')
    return t


def t_stone():
    t = blank_tile('s')
    rect(t, 0, 0, 15, 0, 'S')
    rect(t, 0, 0, 0, 15, 'S')
    rect(t, 0, 15, 15, 15, 'K')
    rect(t, 15, 0, 15, 15, 'K')
    speckle(t, [(5, 5), (6, 5), (10, 10), (4, 11)], 'K')
    return t


def t_brick():
    t = blank_tile('B')
    for y in (0, 8):
        rect(t, 0, y, 15, y, 'b')
    rect(t, 7, 1, 7, 7, 'b')
    rect(t, 15, 9, 15, 15, 'b')
    speckle(t, [(3, 4), (11, 12), (5, 11)], 'n')
    return t


def t_platform(part):
    t = blank_tile()
    rect(t, 0, 3, 15, 9, 'S')
    rect(t, 0, 3, 15, 3, 'K')
    rect(t, 0, 8, 15, 9, 's')
    rect(t, 0, 10, 15, 10, 'K')
    if part == 'l':
        rect(t, 0, 3, 0, 10, 'K')
    if part == 'r':
        rect(t, 15, 3, 15, 10, 'K')
    return t


def t_ladder():
    t = blank_tile()
    for x in (3, 4, 11, 12):
        rect(t, x, 0, x, 15, 'B')
    for y in (2, 3, 10, 11):
        rect(t, 3, y, 12, y, 'n')
    return t


def t_spikes():
    t = blank_tile()
    for cx in (3, 11):
        for y in range(4, 13):
            w = min(3, (y - 4) // 2)      # 1px tip widening to a 7px base
            rect(t, cx - w, y, cx + w, y, 'S')
            rect(t, cx + 1, y, cx + w, y, 's')
    rect(t, 0, 13, 15, 14, 's')
    rect(t, 0, 13, 15, 13, 'S')
    rect(t, 0, 15, 15, 15, 'K')
    return t


def t_water(top=False):
    t = blank_tile('w')
    speckle(t, [(2, 5), (3, 5), (9, 9), (10, 9), (6, 12), (13, 3)], 'W')
    if top:
        rect(t, 0, 0, 15, 1, 'W')
        for x in range(0, 16, 4):
            t[2][x] = 'W'
            t[2][x + 1] = 'W'
    return t


def t_crate():
    t = blank_tile('B')
    rect(t, 0, 0, 15, 0, 'K'); rect(t, 0, 15, 15, 15, 'K')
    rect(t, 0, 0, 0, 15, 'K'); rect(t, 15, 0, 15, 15, 'K')
    rect(t, 1, 1, 14, 1, 'n'); rect(t, 1, 1, 1, 14, 'n')
    for i in range(1, 15):
        t[i][i] = 'b'
        t[i][15 - i] = 'b'
    return t


def t_coin():
    t = blank_tile()
    rect(t, 5, 2, 10, 13, 'y'); rect(t, 3, 4, 12, 11, 'y')
    rect(t, 4, 3, 11, 12, 'y')
    rect(t, 5, 1, 10, 1, 'K'); rect(t, 5, 14, 10, 14, 'K')
    rect(t, 2, 4, 2, 11, 'K'); rect(t, 13, 4, 13, 11, 'K')
    for x, y in [(3, 2), (4, 2), (11, 2), (12, 2), (3, 3), (12, 3),
                 (3, 12), (12, 12), (3, 13), (4, 13), (11, 13), (12, 13),
                 (2, 3), (13, 3), (2, 12), (13, 12)]:
        t[y][x] = 'K'
    rect(t, 7, 4, 8, 11, 'n')
    return t


def t_bg():
    t = blank_tile('b')
    speckle(t, [(2, 3), (9, 2), (13, 7), (5, 10), (11, 13), (3, 12)], 'B')
    return t


TILESET = [
    t_dirt(top=True, left=True), t_dirt(top=True), t_dirt(top=True, right=True), t_stone(),
    t_dirt(left=True), t_dirt(), t_dirt(right=True), t_brick(),
    t_platform('l'), t_platform('m'), t_platform('r'), t_ladder(),
    t_water(top=True), t_water(), t_spikes(), t_crate(),
    t_coin(), t_bg(), t_dirt(top=True, left=True, right=True), t_dirt(left=True, right=True),
]


# ------------------------------------------------------------------ animation
#
# Poses are sparse patches over a character's base half-rows: {row: half_row}.
# Everything downstream works on half-rows, so no transform can break symmetry.

FEET = {'croc': 26, 'penguin': 27, 'cat': 26, 'robot': 26, 'slime': 20}

POSES = {
    'croc': {
        'cheer': {6: '.....Kgggggggggg', 8: '.....Kgggggggggg',
                  18: '..KKKKKKKggggggg', 19: '..Kddddddggyyyyy', 20: '..Kddddddggyyyyy',
                  21: '..KKKKgggggyyyyy', 22: '.....Kgggggyyyyy', 23: '.....Kgggggyyyyy',
                  24: '.....Kgggggyyyyy', 25: '.....Kggggggyyyy'},
        'eat_open': {16: '......KgKKKKKKKK', 17: '.......KgKWKKWKK'},
        'eat_chew': {6: '.....Kgggggggggg', 8: '.....Kgggggggggg',
                     14: '.....KggKKKKKKKK', 15: '.....Kgggggggggg'},
    },
    'penguin': {
        'cheer': {7: '......KBBBBWWWWW',
                  13: '..KKKKKKKBBWWWWW', 14: '..KbbbbbbBBWWWWW', 15: '..KbbbbbbBBWWWWW',
                  16: '..KKKKBBBBBWWWWW', 17: '.....KBBBBBWWWWW', 18: '.....KBBBBBWWWWW',
                  19: '.....KBBBBBWWWWW', 20: '.....KBBBBBWWWWW', 21: '.....KBBBBBWWWWW',
                  22: '.....KBBBBBWWWWW', 23: '.....KBBBBBWWWWW', 24: '.....KBBBBBWWWWW',
                  25: '.....KBBBBBBWWWW'},
        'eat_open': {9: '......KBBBBWWOOO', 10: '......KBBBBWWWOO', 11: '......KBBBBWWKKK',
                     12: '......KBBBBWWOOO', 13: '.....KbbBBBWWWOO'},
        'eat_chew': {7: '......KBBBBWWWWW',
                     10: '......KBBBBWWWOO', 11: '......KBBBBWWOOO', 12: '......KBBBBWWWWW'},
    },
    'cat': {
        'cheer': {8: '.....KOOOOOOOOOO',
                  18: '..KKKKKKKOOOOOOO', 19: '..KooooooOOWWWWW', 20: '..KooooooOOWWWWW',
                  21: '..KKKKOOOOOWWWWW', 22: '.....KOOOOOWWWWW', 23: '.....KOOOOOWWWWW',
                  24: '.....KOOOOOWWWWW', 25: '.....KOOOOOOWWWW'},
        'eat_open': {13: '.....KOOOOOWKKKK', 14: '.....KOOOOOWKKKK', 15: '.....KOOOOOOKKKK'},
        'eat_chew': {8: '.....KOOOOOOOOOO', 13: '.....KOOOOOWKKKK', 14: '.....KOOOOOWWWWW'},
    },
    'robot': {
        'cheer': {7: '.....KSSKCCKKCCC',
                  18: '..KKKKKKKSSSSSSS', 19: '..KssssssSSCCCCC', 20: '..KssssssSSCCCCC',
                  21: '..KKKKSSSSSCCCRR', 22: '.....KSSSSSCCCCC', 23: '.....KSSSSSCCCCC',
                  24: '.....KSSSSSCCCCC', 25: '.....KSSSSSSCCCC'},
        'eat_open': {11: '.....KSSSKKKKKKK', 14: '.....KSSSKssssss', 15: '.....KSSSKKKKKKK'},
        'eat_chew': {12: '.....KSSSKKKKKKK', 14: '.....KSSSKKKKKKK'},
    },
    'slime': {
        'cheer': {12: '.....KVVVVVVVVVV', 13: '.....KVVVKKKVVVV', 14: '.....KVVVVVVVVVV',
                  15: '.....KVVVVVVVVVV',
                  17: '.....KVVVVVVKKKK', 18: '.....KVVVVVVKKKK', 19: '.....KVVVVVVVKKK'},
        'eat_open': {16: '.....KVVVVVVVVKK', 17: '.....KVVVVVVKKKK', 18: '.....KVVVVVVKKKK',
                     19: '.....KVVVVVVVKKK'},
        'eat_chew': {13: '.....KVVVKKKVVVV', 14: '.....KVVVVVVVVVV',
                     17: '.....KVVVVVVVVKK', 18: '.....KVVVVVVVVVV'},
    },
}

BLANK = '.' * 16


def pose(base, patch):
    rows = list(base)
    for y, r in patch.items():
        assert len(r) == 16, (y, r)
        rows[y] = r
    return rows


def bob(rows, feet, dy):
    """Squash/stretch the body by dy while the feet below `feet` stay planted."""
    out = []
    for y in range(len(rows)):
        if y < feet:
            out.append(rows[max(0, min(feet - 1, y - dy))])
        else:
            out.append(rows[y])
    return out


def hop(rows, dy):
    """Move the whole sprite by dy rows (negative is up)."""
    return [rows[y - dy] if 0 <= y - dy < len(rows) else BLANK for y in range(len(rows))]


def animations(name, base):
    feet = FEET[name]
    p = POSES[name]
    cheer = pose(base, p['cheer'])
    chew = pose(base, p['eat_chew'])
    open_ = pose(base, p['eat_open'])
    return [
        ('idle', [(bob(base, feet, dy), 180) for dy in (0, -1, 0, 1)]),
        ('celebrate', [
            (base, 90), (bob(base, feet, 1), 90),
            (hop(cheer, -1), 90), (hop(cheer, -2), 110), (hop(cheer, -2), 110),
            (hop(cheer, -1), 90), (bob(base, feet, 1), 90), (base, 110),
        ]),
        ('eat', [
            (base, 140), (bob(open_, feet, -1), 120), (open_, 120),
            (chew, 120), (bob(chew, feet, 1), 120), (base, 140),
        ]),
    ]

# ---------------------------------------------------------------------- png


def mirror(half):
    grid = [list(h + h[::-1]) for h in half]
    assert len(grid) == 32 and all(len(r) == 32 for r in grid), 'rows must be 16 chars'
    assert all(r == r[::-1] for r in grid), 'sprite is not symmetric'
    return grid


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


def hexrgb(h):
    return int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else '.'
    raw_dir = sys.argv[2] if len(sys.argv) > 2 else out
    frames_dir = os.path.join(out, 'frames')
    os.makedirs(frames_dir, exist_ok=True)

    sheet = [['.'] * (32 * len(CAST)) for _ in range(32)]
    sheet_pal = {}
    jobs = []
    for i, (name, spec) in enumerate(CAST):
        assert len(spec['palette']) >= 5, name
        base = spec['half']
        grid = mirror(base)
        write_png(os.path.join(out, name + '.png'), grid, spec['palette'])

        # one PNG per animation frame, plus the tag ranges Aseprite needs
        anims, files, durs, tags, n = animations(name, base), [], [], [], 0
        strip = []
        for tag, frames in anims:
            tags.append((tag, n + 1, n + len(frames)))
            for rows in [f[0] for f in frames]:
                g = mirror(rows)
                path = os.path.join(frames_dir, f'{name}_{n:02d}.png')
                write_png(path, g, spec['palette'])
                files.append(path)
                strip.append(g)
                n += 1
            durs += [f[1] for f in frames]
        jobs.append(dict(name=name, files=files, durs=durs, tags=tags,
                         palette=[hexrgb(c) for c in spec['palette'].values()],
                         out=os.path.join(raw_dir, name + '.aseprite')))

        # 8x contact strip of every frame, for eyeballing the animation
        wide = [[strip[k][y][x] for k in range(len(strip)) for x in range(32)] for y in range(32)]
        write_png(os.path.join(out, f'_preview_{name}_8x.png'), wide, spec['palette'], scale=8)

        for k, v in spec['palette'].items():
            sheet_pal[name[0] + k] = v
        for y in range(32):
            for x in range(32):
                if grid[y][x] != '.':
                    sheet[y][i * 32 + x] = name[0] + grid[y][x]
        print(f'{name:8s} {len(spec["palette"])} colours, {n} frames, '
              + ' '.join(f'{t}={a}-{b}' for t, a, b in tags))

    tiles = [['.'] * (TILE * TILE_COLS) for _ in range(TILE * TILE_ROWS)]
    for i, t in enumerate(TILESET):
        ox, oy = (i % TILE_COLS) * TILE, (i // TILE_COLS) * TILE
        for y in range(TILE):
            for x in range(TILE):
                tiles[oy + y][ox + x] = t[y][x]
    write_png(os.path.join(out, 'tileset.png'), tiles, TILE_PALETTE)
    print(f'tileset  {len(TILE_PALETTE)} colours, {len(TILESET)} tiles @ {TILE}px')

    write_png(os.path.join(out, '_preview_cast_8x.png'), sheet, sheet_pal, scale=8)
    write_png(os.path.join(out, '_preview_tiles_8x.png'), tiles, TILE_PALETTE, scale=8)
    write_lua(os.path.join(out, 'build.lua'), jobs)


def write_lua(path, jobs):
    """Emit an Aseprite batch script: frames -> cels, durations, tags, palette."""
    lua = ['local jobs = {']
    for j in jobs:
        lua.append('  {')
        lua.append(f'    out = {j["out"]!r},')
        lua.append('    files = {' + ', '.join(repr(f) for f in j['files']) + '},')
        lua.append('    durs = {' + ', '.join(str(d) for d in j['durs']) + '},')
        lua.append('    tags = {' + ', '.join('{%r, %d, %d}' % t for t in j['tags']) + '},')
        lua.append('    pal = {' + ', '.join('{%d,%d,%d}' % c for c in j['palette']) + '},')
        lua.append('  },')
    lua.append('}')
    lua.append(r"""
for _, job in ipairs(jobs) do
  local spr = Sprite(32, 32, ColorMode.RGB)
  local lay = spr.layers[1]
  lay.name = "sprite"
  while #spr.frames < #job.files do spr:newEmptyFrame() end
  for i, f in ipairs(job.files) do
    local src = app.open(f)
    local cel = src.cels[1]
    spr:newCel(lay, spr.frames[i], Image(cel.image), cel.position)
    src:close()
    spr.frames[i].duration = job.durs[i] / 1000.0
  end
  for _, t in ipairs(job.tags) do
    local tag = spr:newTag(t[2], t[3])
    tag.name = t[1]
    tag.aniDir = AniDir.FORWARD
  end
  local pal = Palette(#job.pal)
  for i, c in ipairs(job.pal) do
    pal:setColor(i - 1, Color{ r = c[1], g = c[2], b = c[3] })
  end
  spr:setPalette(pal)
  spr:saveAs(job.out)
  spr:close()
  print("wrote " .. job.out .. " (" .. #job.files .. " frames)")
end
""")
    open(path, 'w').write(chr(10).join(lua))


if __name__ == '__main__':
    main()
