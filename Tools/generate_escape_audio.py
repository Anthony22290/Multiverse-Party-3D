"""Original procedural sound effects and music loop for the Torre del Multiverso escape room.
Writes 16-bit mono WAVs to the folder given as argv[1] (default: ./EscapeAudio)."""
import os, sys
import numpy as np
from scipy.io import wavfile
from scipy.signal import lfilter, butter

SR = 44100
rng = np.random.default_rng(42)
out = sys.argv[1] if len(sys.argv) > 1 else "EscapeAudio"
os.makedirs(out, exist_ok=True)


def t(d):
    return np.arange(int(SR * d)) / SR


def env(n, a=.005, r=.2, curve=4.0):
    x = np.arange(n) / SR
    e = np.minimum(1, x / max(a, 1e-4))
    total = n / SR
    return e * np.exp(-curve * np.maximum(0, x - a) / max(r, 1e-4)) * np.clip((total - x) / .01, 0, 1)


def tone(f, d, kind="sine"):
    ph = 2 * np.pi * np.cumsum(np.broadcast_to(f, t(d).shape)) / SR
    if kind == "sine": return np.sin(ph)
    if kind == "tri": return 2 / np.pi * np.arcsin(np.sin(ph))
    if kind == "square": return np.sign(np.sin(ph)) * .6
    if kind == "saw": return 2 * ((ph / (2 * np.pi)) % 1) - 1
    raise ValueError(kind)


def noise(d):
    return rng.standard_normal(int(SR * d))


def lp(x, fc, order=2):
    b, a = butter(order, min(fc / (SR / 2), .99))
    return lfilter(b, a, x)


def hp(x, fc, order=2):
    b, a = butter(order, max(fc / (SR / 2), .001), "high")
    return lfilter(b, a, x)


def bp(x, lo, hi):
    return hp(lp(x, hi), lo)


def reverb(x, mix=.25, size=1.0):
    y = np.copy(x)
    for d, g in [(1557, .78), (1617, .76), (1491, .77), (1422, .75)]:
        d = int(d * size)
        b = np.zeros(d + 1); b[0] = 1
        a = np.zeros(d + 1); a[0] = 1; a[-1] = -g
        y = y + lfilter(b, a, x) * .25
    for d, g in [(225, .5), (556, .5)]:
        b = np.zeros(d + 1); b[0] = -g; b[-1] = 1
        a = np.zeros(d + 1); a[0] = 1; a[-1] = -g
        y = lfilter(b, a, y)
    return x * (1 - mix) + y * mix


def pad(x, tail=.3):
    return np.concatenate([x, np.zeros(int(SR * tail))])


def save(name, x, peak=.85):
    x = np.asarray(x, dtype=np.float64)
    fade = min(len(x), int(SR * .004))
    x[:fade] *= np.linspace(0, 1, fade)
    x[-fade:] *= np.linspace(1, 0, fade)
    m = np.max(np.abs(x)) or 1
    wavfile.write(os.path.join(out, name + ".wav"), SR, (x / m * peak * 32767).astype(np.int16))


def mix(*parts):
    n = max(len(p) for p in parts)
    y = np.zeros(n)
    for p in parts: y[:len(p)] += p
    return y


def at(x, start):
    return np.concatenate([np.zeros(int(SR * start)), x])


# --- door: heavy stone slide with rumble and a final thud
d = 1.2
rumble = lp(noise(d), 180) * (0.6 + .4 * np.sin(2 * np.pi * 7 * t(d))) * env(int(SR * d), .08, 1.0, 1.5)
grind = bp(noise(d), 300, 1400) * .25 * env(int(SR * d), .05, .9, 2)
thud = lp(noise(.3), 120) * env(int(SR * .3), .002, .12, 5) * 1.6
save("door", reverb(pad(mix(rumble, grind, at(thud, .95))), .3))

# --- lever: ratchet clicks then a clunk
clicks = np.zeros(int(SR * .5))
for i in range(6):
    s = int(SR * (.03 + i * .045))
    c = hp(noise(.012), 2000) * env(int(SR * .012), .0005, .005, 3)
    clicks[s:s + len(c)] += c * (.6 + .1 * i)
clunk = (tone(140, .25) + .5 * tone(95, .25)) * env(int(SR * .25), .002, .08, 4) + lp(noise(.25), 600) * env(int(SR * .25), .001, .04, 6)
save("lever", reverb(pad(mix(clicks, at(clunk, .3))), .2))

# --- plates
save("plate_on", pad(mix((tone(520, .18, "tri") + .6 * tone(780, .18)) * env(int(SR * .18), .003, .1, 3), lp(noise(.05), 900) * env(int(SR * .05), .001, .02))), .7)
save("plate_off", pad((tone(390, .14, "tri")) * env(int(SR * .14), .003, .08, 3)), .5)

# --- success: bright arpeggio + shimmer
notes = [523.25, 659.25, 783.99, 1046.5]
seq = mix(*[at(tone(f, .5, "tri") * env(int(SR * .5), .004, .35, 3) * .6 + at(tone(f * 2, .5) * env(int(SR * .5), .004, .2, 4) * .2, 0), i * .08) for i, f in enumerate(notes)])
save("success", reverb(pad(seq, .6), .35))

# --- error buzzer
d = .45
buzz = lp(tone(110, d, "square") + tone(116, d, "square"), 1800) * env(int(SR * d), .005, .4, 1.2)
save("error", pad(buzz, .1), .7)

# --- zap: falling electric chirp with crackle
d = .4
f = 2400 * np.exp(-t(d) * 9) + 120
zap = tone(f, d, "saw") * env(int(SR * d), .001, .3, 3) * .6 + hp(noise(d), 3000) * env(int(SR * d), .001, .15, 4) * .5
save("zap", reverb(pad(zap, .2), .2))

# --- chest creak + pop
d = .7
creak = bp(tone(180 + 60 * np.sin(2 * np.pi * 3 * t(d)), d, "saw"), 200, 2000) * env(int(SR * d), .05, .5, 2) * .6
pop = lp(noise(.1), 1500) * env(int(SR * .1), .001, .04, 5)
save("chest", reverb(pad(mix(creak, at(pop, .5))), .25))

# --- key pickup sparkle
spark = mix(*[at(tone(f, .35) * env(int(SR * .35), .002, .25, 4) * .5, i * .05) for i, f in enumerate([1318.5, 1568, 2093, 2637])])
save("key", reverb(pad(spark, .4), .4))

# --- unlock: two clicks and a bright clack
u = mix(hp(noise(.02), 1500) * env(int(SR * .02), .0005, .01), at(hp(noise(.02), 1500) * env(int(SR * .02), .0005, .01), .12),
        at((tone(880, .3, "tri") + tone(1320, .3)) * env(int(SR * .3), .002, .2, 4) * .5, .2))
save("unlock", reverb(pad(u, .3), .25))

# --- spring (boing)
d = .7
fb = 220 + 180 * np.exp(-t(d) * 4) * np.sin(2 * np.pi * 9 * t(d))
save("spring", pad(tone(fb, d, "tri") * env(int(SR * d), .002, .6, 2), .2))

# --- fire ignite: whoosh + crackle
d = .9
whoosh = bp(noise(d), 300, 3000) * env(int(SR * d), .08, .6, 2)
crackle = np.zeros(int(SR * d))
for i in range(25):
    s = rng.integers(0, len(crackle) - 400)
    crackle[s:s + 300] += hp(noise(300 / SR), 2500)[:300] * env(300, .0003, .002) * rng.uniform(.3, 1)
save("fire", reverb(pad(mix(whoosh, crackle * .6), .2), .2))

# --- keypad beep
save("beep", pad(tone(1200, .09, "square") * env(int(SR * .09), .002, .07, 2) * .5, .05), .5)

# --- tile crack
d = .5
crack = hp(noise(d), 700) * env(int(SR * d), .001, .12, 5) + lp(noise(d), 300) * env(int(SR * d), .002, .3, 3) * .8
save("crack", reverb(pad(crack, .2), .2))

# --- fall: descending whistle
d = 1.0
fw = 900 * np.exp(-t(d) * 1.6) + 150
save("fall", reverb(pad(tone(fw, d) * env(int(SR * d), .02, .8, 2) * .6, .2), .3))

# --- cell insert: power-up sweep
d = .8
fu = 200 + 900 * t(d) / d
save("cell", reverb(pad(tone(fu, d, "saw") * .3 * env(int(SR * d), .02, .7, 1.5) + tone(fu * 1.5, d) * .3 * env(int(SR * d), .02, .7, 2), .3), .3))

# --- portal opening: deep swell with shimmering chord
d = 3.0
chord = sum(tone(f * (1 + .003 * np.sin(2 * np.pi * .7 * t(d) + k)), d, "tri") for k, f in enumerate([110, 165, 220, 277.2, 329.6]))
sw = np.minimum(1, t(d) / 1.2) * np.exp(-np.maximum(0, t(d) - 1.6) * 1.2)
save("portal", reverb(pad(chord * sw * .4 + hp(noise(d), 4000) * sw * .05, .6), .45, 1.3))

# --- win fanfare
melody = [(523.25, 0, .18), (659.25, .18, .18), (783.99, .36, .18), (1046.5, .54, .55), (987.77, 1.1, .18), (1046.5, 1.28, .8)]
fan = mix(*[at((tone(f, dd + .3, "saw") * .25 + tone(f, dd + .3, "tri") * .5) * env(int(SR * (dd + .3)), .01, dd, 2), s) for f, s, dd in melody])
fan = lp(fan, 5000)
bass = mix(*[at(tone(f, .5, "tri") * env(int(SR * .5), .01, .4, 2) * .5, s) for f, s in [(130.8, 0), (196, .54), (130.8, 1.28)]])
save("win", reverb(pad(mix(fan, bass), .8), .35))

# --- lose: sad descending trombone-ish
lose = mix(*[at(lp(tone(f * (1 + .01 * np.sin(2 * np.pi * 5 * t(.55))), .55, "saw"), 1500) * env(int(SR * .55), .02, .45, 1.5), i * .5) for i, f in enumerate([392, 370, 349.2, 329.6])])
save("lose", reverb(pad(lose, .8), .3))

# --- music loop: mysterious, gentle (A minor), 120 BPM, 16 bars of 2 s = 32 s, seamless
bpm = 96
beat = 60 / bpm
bars = 16
total = bars * 4 * beat
n = int(SR * total)
music = np.zeros(n)
chords = [(220, 261.6, 329.6), (174.6, 220, 261.6), (196, 246.9, 293.7), (164.8, 207.7, 246.9)]  # Am F G E
for bar in range(bars):
    ch = chords[bar % 4]
    start = bar * 4 * beat
    # soft pad
    padn = tone(ch[0] / 2, 4 * beat, "tri") * .25 + sum(tone(f, 4 * beat) * .12 for f in ch)
    padn *= np.minimum(1, t(4 * beat) / .3) * np.minimum(1, (4 * beat - t(4 * beat)) / .3)
    s = int(SR * start); music[s:s + len(padn)] += padn[:n - s]
    # arpeggio (eighths)
    for i in range(8):
        f = ch[[0, 1, 2, 1, 0, 2, 1, 2][i]] * 2
        if bar % 2 == 1 and i in (6, 7): f *= 1.5
        note = tone(f, beat * .5, "tri") * env(int(SR * beat * .5), .003, .25, 3) * .22
        s = int(SR * (start + i * beat * .5)); music[s:s + len(note)] += note[:n - s]
    # muted kick + shaker
    for i in range(4):
        k = tone(60 * np.exp(-t(.25) * 8) + 45, .25) * env(int(SR * .25), .001, .15, 4) * .35
        s = int(SR * (start + i * beat)); music[s:s + len(k)] += k[:n - s]
        sh = hp(noise(.05), 6000) * env(int(SR * .05), .001, .03, 3) * .06
        s2 = int(SR * (start + i * beat + beat * .5)); music[s2:s2 + len(sh)] += sh[:n - s2]
wet = reverb(np.concatenate([music, music]), .3)  # reverb over two cycles, keep the second so the tail wraps
music = wet[n:2 * n]
save("music", music, .6)
print("ok", len(os.listdir(out)))
