"""Chiptune sound effects for the croc eating contest.

Synthesised, not AI-generated. The art is NES-style, so the audio is built from the
same vocabulary a NES had: pulse waves with variable duty, an LFSR noise channel, and
a triangle channel. That is what makes the sound sit *with* the pixels instead of
beside them.

Everything is short on purpose - the longest cue here is the victory jingle. A timing
game fires its core sound many times a second, so a chomp that outstays 80ms turns
into mud, and anything that peaks hot becomes painful within a minute of play.

Run from anywhere:  python3 sfx_gen.py [out_dir]
Writes 16-bit mono WAVs at 22050 Hz.
"""
import math, os, struct, sys, wave

RATE = 22050
PEAK = 0.32          # headroom: these stack, and stacked square waves get harsh fast


def env(n, attack=0.002, decay=0.0, sustain=1.0, release=0.03):
    """Linear ADSR over n samples, returned as a list of gains."""
    a = max(1, int(attack * RATE))
    d = int(decay * RATE)
    r = max(1, int(release * RATE))
    s = max(0, n - a - d - r)
    out = []
    out += [i / a for i in range(a)]
    out += [1.0 + (sustain - 1.0) * (i / max(1, d)) for i in range(d)]
    out += [sustain] * s
    out += [sustain * (1.0 - i / r) for i in range(r)]
    return (out + [0.0] * n)[:n]


def pulse(freq, dur, duty=0.5, vol=1.0, slide=0.0, **kw):
    """A pulse wave. `slide` is a multiplier applied to the frequency across the sound,
    which is how a NES made things dive or swoop."""
    n = int(dur * RATE)
    g = env(n, **kw)
    out = []
    phase = 0.0
    for i in range(n):
        f = freq * (1.0 + slide * (i / n))
        phase += f / RATE
        out.append((1.0 if (phase % 1.0) < duty else -1.0) * g[i] * vol)
    return out


def noise(dur, vol=1.0, period=1, **kw):
    """15-bit LFSR noise, the NES noise channel. `period` divides the shift rate, so a
    larger value is a lower, grittier rumble."""
    n = int(dur * RATE)
    g = env(n, **kw)
    reg = 0x7FFF
    out = []
    hold = 0.0
    for i in range(n):
        hold += 1.0 / period
        if hold >= 1.0:
            hold -= 1.0
            bit = ((reg ^ (reg >> 1)) & 1)
            reg = (reg >> 1) | (bit << 14)
        out.append((1.0 if (reg & 1) else -1.0) * g[i] * vol)
    return out


def tri(freq, dur, vol=1.0, **kw):
    n = int(dur * RATE)
    g = env(n, **kw)
    out = []
    phase = 0.0
    for i in range(n):
        phase += freq / RATE
        p = phase % 1.0
        out.append((4.0 * abs(p - 0.5) - 1.0) * g[i] * vol)
    return out


def seq(*parts):
    out = []
    for p in parts:
        out += p
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


def arp(notes, each, duty=0.5, vol=1.0, **kw):
    return seq(*[pulse(f, each, duty=duty, vol=vol, **kw) for f in notes])


def write(path, samples):
    peak = max(1e-9, max(abs(s) for s in samples))
    scale = PEAK / peak
    frames = b''.join(struct.pack('<h', int(max(-1.0, min(1.0, s * scale)) * 32767))
                      for s in samples)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(frames)
    return len(samples) / RATE


# ------------------------------------------------------------------ the sounds

def s_chomp():
    """Two fast bites. Short, dry, with a noise transient for the crunch."""
    return mix(
        seq(pulse(520, 0.028, duty=0.25, slide=-0.45, release=0.01),
            pulse(400, 0.034, duty=0.5, slide=-0.5, release=0.015)),
        noise(0.035, vol=0.55, period=2, release=0.02))


def s_crunch():
    """The bigger bite used on a combo. Same shape, fatter and a touch lower."""
    return mix(
        seq(pulse(420, 0.032, duty=0.25, slide=-0.45, release=0.012),
            pulse(300, 0.045, duty=0.5, slide=-0.5, release=0.02)),
        noise(0.05, vol=0.7, period=3, release=0.03))


def s_whiff():
    """Jaws closing on nothing: airy, pitched down, no crunch."""
    return noise(0.09, vol=0.5, period=6, attack=0.004, release=0.07)


def s_pass():
    """Food slipping by. Soft and low so it reads as a loss, not an impact."""
    return pulse(180, 0.09, duty=0.125, vol=0.7, slide=-0.35, release=0.06)


def s_strike():
    """Harsh descending two-tone. The one sound allowed to be unpleasant."""
    return mix(
        seq(pulse(300, 0.09, duty=0.5, release=0.02),
            pulse(200, 0.14, duty=0.5, slide=-0.25, release=0.08)),
        noise(0.06, vol=0.3, period=4, release=0.04))


def s_coin():
    """Classic two-note pickup."""
    return seq(pulse(988, 0.045, duty=0.5, release=0.01),
               pulse(1319, 0.14, duty=0.5, release=0.1))


def s_blip():
    """Menu tick. Deliberately tiny."""
    return pulse(880, 0.035, duty=0.5, vol=0.8, release=0.02)


def s_frenzy():
    """Rising arpeggio that says something just changed. Leads into frenzy mode."""
    return mix(
        arp([523, 659, 784, 1047, 1319], 0.05, duty=0.25, release=0.01),
        noise(0.25, vol=0.18, period=8, attack=0.15, release=0.1))


def s_win():
    """Victory fanfare: major arpeggio, then a held top note."""
    return seq(
        arp([523, 659, 784], 0.075, duty=0.5, release=0.015),
        mix(pulse(1047, 0.34, duty=0.5, release=0.22),
            tri(523, 0.34, vol=0.5, release=0.22)))


def s_lose():
    """Defeat: the same shape falling instead of rising."""
    return seq(
        arp([392, 349, 294], 0.1, duty=0.25, release=0.02),
        mix(pulse(196, 0.42, duty=0.125, slide=-0.15, release=0.3),
            tri(98, 0.42, vol=0.6, release=0.3)))


SOUNDS = [
    ('chomp', s_chomp), ('crunch', s_crunch), ('whiff', s_whiff), ('pass', s_pass),
    ('strike', s_strike), ('coin', s_coin), ('blip', s_blip), ('frenzy', s_frenzy),
    ('win', s_win), ('lose', s_lose),
]


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(out, exist_ok=True)
    for name, fn in SOUNDS:
        dur = write(os.path.join(out, name + '.wav'), fn())
        print(f'{name:8s} {dur * 1000:6.0f} ms')


if __name__ == '__main__':
    main()
