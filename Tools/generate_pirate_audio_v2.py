"""Original procedural sound bank for Arena del Caos (v2).

Everything is synthesised from scratch (no samples): layered transients, modal resonances for wood,
filtered noise for water and wind, Karplus-Strong plucks for the music. Each gameplay sound gets
several variations so repeated actions don't sound identical.

Output: Assets/003_Scripts/PirateArena/Resources/PirateAudio/v2/*.wav (44.1 kHz, 16-bit mono)
Run:    python Tools/generate_pirate_audio_v2.py
"""
from pathlib import Path
import sys
import numpy as np
from scipy.signal import butter, lfilter
import wave

SR = 44100
OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "Assets/003_Scripts/PirateArena/Resources/PirateAudio/v2"
OUT.mkdir(parents=True, exist_ok=True)


# ----------------------------------------------------------------------------- building blocks
def t_axis(dur):
    return np.arange(int(SR * dur)) / SR


def noise(dur, rng):
    return rng.uniform(-1, 1, int(SR * dur))


def bp(x, lo, hi, order=2):
    b, a = butter(order, [lo / (SR / 2), min(hi / (SR / 2), .99)], btype="band")
    return lfilter(b, a, x)


def lp(x, f, order=2):
    b, a = butter(order, min(f / (SR / 2), .99), btype="low")
    return lfilter(b, a, x)


def hp(x, f, order=2):
    b, a = butter(order, f / (SR / 2), btype="high")
    return lfilter(b, a, x)


def env_exp(dur, attack, decay):
    t = t_axis(dur)
    e = (1 - np.exp(-t / max(attack, 1e-4))) * np.exp(-t / decay)
    return declick(e)


def declick(x, ms=12):
    """Smoothly close any layer that is cut before it has fully decayed (avoids end clicks)."""
    n = min(len(x), int(SR * ms / 1000))
    if n > 1: x = x.copy(); x[-n:] *= .5 + .5 * np.cos(np.linspace(0, np.pi, n))
    return x


def sweep(dur, f0, f1, curve=1.0):
    t = t_axis(dur)
    k = (t / dur) ** curve
    f = f0 * (f1 / f0) ** k
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def modal(dur, freqs, decays, amps, rng, detune=0.0):
    t = t_axis(dur)
    out = np.zeros_like(t)
    for f, d, a in zip(freqs, decays, amps):
        f *= 1 + rng.uniform(-detune, detune)
        out += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t / d)
    return declick(out)


def pad(x, dur):
    n = int(SR * dur)
    return np.pad(x, (0, max(0, n - len(x))))[:n]


def mix(*parts):
    n = max(len(p) for p in parts)
    return sum(np.pad(p, (0, n - len(p))) for p in parts)


def at(x, seconds, dur):
    """Place clip x starting at `seconds` inside a buffer of `dur`."""
    out = np.zeros(int(SR * dur)); i = int(SR * seconds)
    seg = declick(x[: max(0, len(out) - i)], 6); out[i:i + len(seg)] += seg; return out


def soft(x, drive=1.5):
    return np.tanh(x * drive) / np.tanh(drive)


def room(x, wet=.18, size=1.0):
    """Tiny Schroeder reverb (IIR combs + allpasses): glues layers without washing out the transient."""
    tail = int(SR * .6 * size)
    src = np.pad(x, (0, tail))
    wetsum = np.zeros_like(src)
    for delay, g in ((.0297, .74), (.0371, .71), (.0411, .69), (.0437, .67)):
        d = int(delay * size * SR); a = np.zeros(d + 1); a[0] = 1; a[d] = -g
        wetsum += lfilter([1.0], a, src)
    y = wetsum / 4
    for delay, g in ((.005, .7), (.0017, .7)):
        d = int(delay * SR); b = np.zeros(d + 1); b[0] = -g; b[d] = 1; a = np.zeros(d + 1); a[0] = 1; a[d] = -g
        y = lfilter(b, a, y)
    out = src + y * wet
    return out


def fade_tail(x, ms=25):
    n = int(SR * ms / 1000)
    if len(x) > n: x[-n:] *= np.linspace(1, 0, n)
    return x


def save(name, x, peak=.89):
    x = np.asarray(x, dtype=np.float64)
    x = x - np.mean(x[: min(len(x), 2000)]) * 0  # keep DC-free filters; no offset correction needed
    m = np.max(np.abs(x)) or 1
    x = fade_tail(x / m * peak)
    data = (np.clip(x, -1, 1) * 32767).astype("<i2").tobytes()
    with wave.open(str(OUT / f"{name}.wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data)
    return name


# ----------------------------------------------------------------------------- gameplay sounds
def punch(rng, v):
    d = .32
    thump = sweep(.18, 120 + v * 10, 42, .6) * env_exp(.18, .001, .05)
    smack = bp(noise(.08, rng), 700 + v * 120, 2600) * env_exp(.08, .0005, .018)
    click = hp(noise(.01, rng), 3000) * env_exp(.01, .0001, .002)
    cloth = bp(noise(.2, rng), 1500, 5000) * env_exp(.2, .004, .05) * .25
    return room(soft(mix(thump * 1.2, smack * .9, click * .5, cloth), 2.2), .12)


def body_impact(rng, v):
    d = .55
    thud = sweep(.3, 85 + v * 8, 34, .5) * env_exp(.3, .002, .09)
    knock = modal(.5, [160, 410, 690, 1210], [.12, .07, .05, .03], [1, .6, .4, .2], rng, .06)
    rattle = bp(noise(.35, rng), 400, 2200) * env_exp(.35, .002, .06) * .5
    return room(soft(mix(thud * 1.3, knock * .8, rattle), 1.8), .16)


def wood_hit(rng, v):
    freqs = [210 + v * 25, 530 + v * 40, 950 + v * 60, 1650]
    tone = modal(.45, freqs, [.14, .08, .05, .025], [1, .7, .45, .25], rng, .05)
    click = bp(noise(.03, rng), 1500, 6000) * env_exp(.03, .0002, .006)
    low = sweep(.15, 95, 45) * env_exp(.15, .001, .04)
    return room(mix(tone, click * .6, low * .7), .15)


def whoosh(rng, v):
    d = .38 + v * .04
    n = noise(d, rng); t = t_axis(d)
    # Moving band-pass: three overlapping bands faded along the swing.
    centers = [500, 1100, 2100]
    out = np.zeros_like(t)
    shape = np.sin(np.pi * np.clip(t / d, 0, 1)) ** 2
    for i, c in enumerate(centers):
        band = bp(n, c * .7, c * 1.4)
        w = np.exp(-((t / d - (.3 + .2 * i)) ** 2) / .03)
        out += band * w
    return out * shape


def grab(rng, v):
    rustle = bp(noise(.22, rng), 1800, 6500) * env_exp(.22, .003, .05)
    rustle *= 1 + .6 * np.sin(2 * np.pi * (35 + v * 6) * t_axis(.22))
    pat = sweep(.09, 260, 140) * env_exp(.09, .001, .025)
    return mix(rustle * .7, pat)


def jump(rng, v):
    d = .26
    t = t_axis(d)
    f = (210 + v * 25) * (1 + 1.1 * (t / d) ** .7)
    voice = np.sin(2 * np.pi * np.cumsum(f) / SR)
    voice = lp(soft(voice * 1.5, 1.3) + .3 * np.sin(2 * np.pi * np.cumsum(f * 2) / SR), 2500) * env_exp(d, .01, .09)
    cloth = bp(noise(d, rng), 2000, 6000) * env_exp(d, .002, .04) * .3
    return mix(voice * .55, cloth)


def land(rng, v):
    thump = sweep(.22, 95, 38) * env_exp(.22, .001, .06)
    boards = modal(.4, [140, 330, 620], [.1, .06, .04], [1, .5, .3], rng, .08)
    dust = bp(noise(.3, rng), 300, 1800) * env_exp(.3, .003, .07) * .5
    return room(soft(mix(thump * 1.3, boards * .6, dust), 1.6), .12)


def splash(rng, v, big=True):
    d = 1.5 if big else .8
    t = t_axis(d)
    burst = lp(noise(d, rng), 2600) * env_exp(d, .004, .22 if big else .12)
    hiss = bp(noise(d, rng), 2500, 9000) * env_exp(d, .01, .35) * .35
    body = sweep(.35, 160, 55) * env_exp(.35, .002, .1) * .8
    bubbles = np.zeros_like(t)
    for _ in range(26 if big else 12):
        s = rng.uniform(.06, d * .8); bd = rng.uniform(.02, .06); f0 = rng.uniform(380, 1300)
        b = sweep(bd, f0, f0 * rng.uniform(1.3, 1.9)) * env_exp(bd, .001, bd / 3) * rng.uniform(.15, .45)
        bubbles += at(b, s, d)
    return room(mix(burst, hiss, body, bubbles), .22, 1.3)


def cannon_shot(rng, v):
    d = 1.8
    boom = sweep(.9, 62, 26, .5) * env_exp(.9, .002, .28)
    crack = lp(noise(.25, rng), 3500) * env_exp(.25, .0005, .04)
    roar = lp(noise(d, rng), 600) * env_exp(d, .01, .45)
    return room(soft(mix(boom * 1.4, crack * 1.1, roar * .9), 2.8), .3, 1.6)


def cannon_blast(rng, v):
    d = 2.2
    boom = sweep(1.0, 55, 22, .5) * env_exp(1.0, .003, .35)
    roar = lp(noise(d, rng), 900) * env_exp(d, .01, .55)
    crackle = np.zeros(int(SR * d))
    for _ in range(40):
        s = rng.uniform(.02, 1.2); cd = rng.uniform(.005, .02)
        crackle += at(hp(noise(cd, rng), 1500) * env_exp(cd, .0002, cd / 3) * rng.uniform(.2, .8), s, d)
    debris = np.zeros(int(SR * d))
    for _ in range(7):
        debris += at(wood_hit(rng, rng.integers(0, 3)) * rng.uniform(.15, .35), rng.uniform(.25, 1.1), d)
    return room(soft(mix(boom * 1.5, roar, crackle * .5, debris), 2.5), .3, 1.7)


def cannon_warn(rng, v):
    d = 1.6
    t = t_axis(d)
    fizz = bp(noise(d, rng), 3000, 9500) * (.55 + .45 * np.abs(np.sin(2 * np.pi * 9 * t + rng.uniform(0, 3))))
    pops = np.zeros_like(t)
    for _ in range(35):
        pops += at(hp(noise(.006, rng), 2000) * rng.uniform(.3, 1), rng.uniform(0, d - .02), d)
    fizz = fizz * np.minimum(1, t / .05)
    # Two bright warning dings, the "watch out" cue.
    ding = modal(.5, [1568, 3920, 5200], [.25, .1, .05], [1, .3, .15], rng)
    return mix(fizz * .45, pops * .35, at(ding * .8, 0, d), at(ding * .8, .42, d))


def shark_warn(rng, v):
    d = 1.4
    t = t_axis(d)
    rumble = lp(noise(d, rng), 180) * (0.5 + .5 * np.sin(2 * np.pi * 3 * t)) * env_exp(d, .2, .8)
    bubbles = np.zeros_like(t)
    for _ in range(30):
        bd = rng.uniform(.02, .05); f0 = rng.uniform(300, 900)
        bubbles += at(sweep(bd, f0, f0 * 1.8) * env_exp(bd, .001, bd / 3) * rng.uniform(.2, .5), rng.uniform(0, d - .1), d)
    return mix(rumble * 1.3, bubbles * .7)


def shark_leap(rng, v):
    d = 1.2
    burst = splash(rng, v, big=False)
    chomp = mix(sweep(.12, 300, 90) * env_exp(.12, .001, .03), hp(noise(.03, rng), 1200) * env_exp(.03, .0003, .006))
    return mix(pad(burst, d), at(chomp * 1.2, .35, d))


def step(rng, v):
    thump = sweep(.08, 140 + v * 12, 70) * env_exp(.08, .001, .018)
    creak = bp(noise(.09, rng), 500 + v * 80, 1900) * env_exp(.09, .001, .02)
    return mix(thump, creak * .6)


def ko_bonk(rng, v):
    d = 1.3
    bonk = modal(.6, [520, 1290, 2120, 3460], [.22, .12, .08, .05], [1, .5, .3, .15], rng) * np.exp(-t_axis(.6) * 2)
    bonk = mix(bonk, sweep(.15, 700, 260) * env_exp(.15, .001, .05))
    # Cartoon birdies: short fast chirps circling.
    birds = np.zeros(int(SR * d))
    for i in range(6):
        cd = .08; f0 = 2600 + (i % 3) * 300
        chirp = sweep(cd, f0, f0 * 1.35) * env_exp(cd, .004, .03)
        birds += at(chirp * .35, .35 + i * .13, d)
    return room(mix(pad(bonk, d), birds), .18)


def countdown(rng, v, high=False):
    d = .35
    f = 1318.5 if high else 880
    tone = (np.sin(2 * np.pi * f * t_axis(d)) + .25 * np.sin(2 * np.pi * f * 2 * t_axis(d))) * env_exp(d, .003, .09)
    return room(tone, .2)


def ship_bell(rng, v):
    d = 2.4
    f = 587.3
    tone = modal(d, [f, f * 2.0, f * 2.76, f * 5.4, f * 8.9], [1.1, .6, .45, .2, .1], [1, .55, .4, .2, .1], rng)
    strike = hp(noise(.01, rng), 2500) * env_exp(.01, .0001, .002)
    return room(mix(tone, strike * .6, at(tone * .55, .55, d)), .25, 1.4)


def note_freq(n):
    return 440.0 * 2 ** ((n - 69) / 12)


def pluck(freq, dur, rng, bright=.5):
    n = int(SR * dur); p = max(2, int(SR / freq))
    buf = rng.uniform(-1, 1, p) * 1.0
    out = np.zeros(n); damp = .996 - (1 - bright) * .01
    idx = 0
    for i in range(n):
        out[i] = buf[idx]
        nxt = (idx + 1) % p
        buf[idx] = damp * .5 * (buf[idx] + buf[nxt])
        idx = nxt
    return out * env_exp(dur, .002, dur * .6)


def brass(freq, dur, rng):
    t = t_axis(dur)
    saw = 2 * ((freq * t + rng.uniform(0, 1)) % 1) - 1
    saw2 = 2 * ((freq * 1.004 * t) % 1) - 1
    e = np.minimum(1, t / .04) * np.exp(-np.maximum(0, t - dur * .7) / .08)
    return (lp(saw + saw2, 1400) * .6 + lp(saw + saw2, 3200) * .4) * e * .5


def victory(rng, v):
    d = 3.0
    seq = [(62, 0, .18), (66, .18, .18), (69, .36, .18), (74, .54, .7), (71, 1.3, .2), (74, 1.5, 1.2)]
    out = np.zeros(int(SR * d))
    for n, s, l in seq:
        out += at(brass(note_freq(n), l, rng) + brass(note_freq(n - 12), l, rng) * .5, s, d)
    drum = np.zeros(int(SR * d))
    for s in (0, .54, 1.3, 1.5):
        drum += at(sweep(.25, 110, 50) * env_exp(.25, .001, .08), s, d)
    cym = at(hp(noise(1.4, rng), 4000) * env_exp(1.4, .002, .4) * .25, 1.5, d)
    return room(mix(out, drum * .9, cym), .25, 1.3)


def eliminated(rng, v):
    d = 1.6
    seq = [(55, 0, .3), (54, .32, .3), (53, .64, .8)]
    out = np.zeros(int(SR * d))
    for n, s, l in seq:
        t = t_axis(l); f = note_freq(n) * (1 + .03 * np.sin(2 * np.pi * 6 * t)) * (1 - .06 * (t / l) if l > .5 else 1)
        saw = np.sin(2 * np.pi * np.cumsum(f) / SR)
        wah = .5 + .5 * np.abs(np.sin(2 * np.pi * 3 * t))
        tone = (lp(soft(saw * 2.5, 2), 800) * (1 - wah) + lp(soft(saw * 2.5, 2), 1800) * wah) * env_exp(l, .02, l * .9)
        out += at(tone, s, d)
    return room(out, .2)


# ----------------------------------------------------------------------------- ambience & music
def sea_loop(rng, dur=16.0):
    t = t_axis(dur)
    base = lp(noise(dur, rng), 700) * .6 + bp(noise(dur, rng), 900, 3500) * .25
    swell = np.zeros_like(t)
    for f, ph in ((1 / 8, 0.0), (1 / 4, 1.3), (1 / 16, 2.2)):  # periods divide 16 s: seamless loop
        swell += (np.sin(2 * np.pi * f * t + ph) + 1) / 2
    swell = .35 + .65 * swell / 3
    lap = np.zeros_like(t)
    for s in np.arange(.5, dur - 1.2, 2.0) + rng.uniform(-.3, .3, int((dur - 1.7) / 2) + 1)[:len(np.arange(.5, dur - 1.2, 2.0))]:
        ld = rng.uniform(.6, 1.1)
        lap += at(bp(noise(ld, rng), 400, 2600) * env_exp(ld, .15, .3) * rng.uniform(.3, .6), s, dur)
    x = base * swell + lap
    # Crossfade end into start for a seamless loop.
    n = int(SR * .5); x[:n] = x[:n] * np.linspace(0, 1, n) + x[-n:] * np.linspace(1, 0, n)
    return x[:-n] if False else x


def creak(rng, v):
    d = 1.0 + v * .15
    t = t_axis(d)
    rate = 38 + 25 * np.sin(np.pi * t / d) + rng.uniform(-4, 4)
    phase = np.cumsum(rate) / SR
    pulses = (np.diff(np.floor(phase), prepend=0) > 0).astype(float) * rng.uniform(.6, 1, len(t))
    body = bp(pulses, 450 + v * 90, 1100 + v * 150, 2) * 6
    return room(body * np.sin(np.pi * t / d) ** .5, .25)


def gull(rng, v):
    d = 1.3
    out = np.zeros(int(SR * d))
    for i in range(2 + v % 2):
        cd = rng.uniform(.22, .34); t = t_axis(cd)
        f = (1500 + v * 90) * (1 - .35 * (t / cd)) * (1 + .05 * np.sin(2 * np.pi * 28 * t))
        ph = np.cumsum(f) / SR
        cry = (np.sin(2 * np.pi * ph) + .5 * np.sin(4 * np.pi * ph) + .25 * np.sin(6 * np.pi * ph))
        cry = bp(cry, 800, 5000) * env_exp(cd, .02, cd * .5)
        out += at(cry, .05 + i * rng.uniform(.3, .38), d)
    return room(out, .3, 1.6)


def shanty_loop(rng):
    """Original jig-like loop in D dorian, 6/8 at ~112 dotted-quarter... 16 bars."""
    bpm = 112  # eighth-note grid: one bar = 6 eighths
    eighth = 60 / bpm / 2
    bars = 16
    dur = bars * 6 * eighth
    out = np.zeros(int(SR * dur))
    D = 62
    # Melody in scale degrees of D dorian (D E F G A B C). Each bar: six eighths (None = rest, '-' = hold).
    scale = [0, 2, 3, 5, 7, 9, 10, 12, 14, 15, 17]
    melody = [
        [4, '-', 2, 0, 2, 4], [5, 4, 2, 1, '-', None], [4, '-', 2, 0, 1, 2], [3, 2, 1, 0, '-', None],
        [4, 5, 7, 5, 4, 2], [3, '-', 2, 1, 0, 1], [2, 4, 2, 1, 0, -1], [0, '-', '-', None, 0, 1],
        [7, '-', 5, 4, 5, 7], [8, 7, 5, 4, '-', None], [7, '-', 5, 4, 2, 4], [5, 4, 2, 1, '-', None],
        [4, 5, 7, 8, 7, 5], [4, '-', 2, 1, 2, 4], [2, 1, 0, -1, 0, 1], [0, '-', '-', None, None, None],
    ]
    chords = [0, 3, 0, 4, 0, 3, 6, 0, 3, 4, 3, 4, 0, 3, 6, 0]  # roots as scale degrees (i, IV, v, VII)
    def deg(d):
        o, i = divmod(d, 7)
        return D + scale[i] + 12 * o
    for b in range(bars):
        start = b * 6 * eighth
        # Melody pluck (fiddle/mandolin-ish).
        notes = melody[b]
        for i, n in enumerate(notes):
            if n is None or n == '-': continue
            hold = 1
            while i + hold < 6 and notes[i + hold] == '-': hold += 1
            f = note_freq(deg(n) + 12)
            out += at(pluck(f, eighth * hold + .15, rng, .8) * .55, start + i * eighth, dur)
        # Bass on beats 1 and 4.
        root = deg(chords[b]) - 24
        out += at(pluck(note_freq(root), eighth * 3, rng, .3) * .7, start, dur)
        out += at(pluck(note_freq(root + 7), eighth * 3, rng, .3) * .5, start + 3 * eighth, dur)
        # Squeezebox chord stabs on 2-3 and 5-6.
        for off in (1, 4):
            for iv in (0, 2, 4):
                f = note_freq(deg(chords[b] + iv))
                out += at(brass(f, eighth * 1.6, rng) * .12, start + off * eighth, dur)
        # Frame drum: low on 1 & 4, tap on others, shaker on every eighth.
        for k in range(6):
            s = start + k * eighth
            if k in (0, 3):
                out += at(sweep(.22, 120, 55) * env_exp(.22, .001, .07) * .8, s, dur)
            elif k in (2, 5):
                out += at(bp(noise(.08, rng), 300, 1200) * env_exp(.08, .001, .02) * .4, s, dur)
            out += at(hp(noise(.05, rng), 5000) * env_exp(.05, .002, .012) * .07, s, dur)
    out = room(out, .14)[: int(SR * dur)]
    n = int(SR * .03); out[:n] *= np.linspace(0, 1, n)
    return out


# ----------------------------------------------------------------------------- render
def main():
    made = []
    gen = {
        "punch": punch, "impact": body_impact, "wood": wood_hit, "whoosh": whoosh, "grab": grab,
        "jump": jump, "land": land, "step": step,
    }
    for key, fn in gen.items():
        for v in range(3):
            made.append(save(f"{key}_{v}", fn(np.random.default_rng(sum(map(ord, key)) * 13 + v * 17), v)))
    for v in range(2):
        made.append(save(f"splash_{v}", splash(np.random.default_rng(300 + v), v)))
    singles = {
        "cannon_shot": cannon_shot, "cannon_blast": cannon_blast, "cannon_warn": cannon_warn,
        "shark_warn": shark_warn, "shark_leap": shark_leap, "ko": ko_bonk,
        "count": lambda r, v: countdown(r, v, False), "go": ship_bell, "victory": victory, "eliminated": eliminated,
    }
    for key, fn in singles.items():
        made.append(save(key, fn(np.random.default_rng(len(key) * 31), 0)))
    for v in range(3):
        made.append(save(f"creak_{v}", creak(np.random.default_rng(500 + v), v), .6))
        made.append(save(f"gull_{v}", gull(np.random.default_rng(600 + v), v), .6))
    made.append(save("amb_sea", sea_loop(np.random.default_rng(7)), .7))
    made.append(save("music_shanty", shanty_loop(np.random.default_rng(11)), .8))
    print(f"{len(made)} clips -> {OUT}")


if __name__ == "__main__":
    main()
