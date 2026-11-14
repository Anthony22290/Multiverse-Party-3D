"""Original synthesized music and cues for Carreras Locas (no samples). Writes 16-bit mono WAVs to
Assets/003_Scripts/CrazyRacers/Resources/RaceAudio. Usage: python Tools/generate_race_audio.py"""
import os, wave
import numpy as np

SR = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "003_Scripts", "CrazyRacers", "Resources", "RaceAudio")
rng = np.random.default_rng(7)


def save(name, x, gain=.85):
    x = np.asarray(x, dtype=np.float64)
    peak = np.max(np.abs(x)) or 1.0
    x = np.clip(x / peak * gain, -1, 1)
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())


def note_hz(n):  # MIDI note number
    return 440.0 * 2 ** ((n - 69) / 12)


def tone(freq, sec, kind="square", duty=.5, decay=6.0, attack=.004):
    n = int(SR * sec); tt = np.arange(n) / SR
    ph = (freq * tt) % 1
    if kind == "square": w = np.where(ph < duty, 1.0, -1.0)
    elif kind == "tri": w = 4 * np.abs(ph - .5) - 1
    elif kind == "saw": w = 2 * ph - 1
    else: w = np.sin(2 * np.pi * ph)
    e = np.exp(-decay * tt)
    a = max(1, int(SR * attack)); e[:a] *= np.linspace(0, 1, a)
    r = max(1, int(SR * .01)); e[-r:] *= np.linspace(1, 0, r)
    return w * e


def add(buf, start, x):
    s = int(start * SR); e = min(len(buf), s + len(x))
    if s < len(buf): buf[s:e] += x[:e - s]


# ---- music: 140 bpm, 16 bars, seamless loop
BPM = 140; BEAT = 60 / BPM; BAR = 4 * BEAT; BARS = 16
music = np.zeros(int(SR * BAR * BARS))
# progression (A major): A  E  F#m  D, twice per 8 bars; second half lifts to D E A A
prog = [57, 52, 54, 50, 57, 52, 54, 50, 50, 52, 57, 57, 50, 52, 54, 52]
minor = {54}
for bar, root in enumerate(prog):
    t0 = bar * BAR
    third = 3 if root in minor else 4
    chord = [root, root + third, root + 7]
    for b in range(4):  # kick + snare
        add(music, t0 + b * BEAT, tone(55, .16, "sine", decay=18)); add(music, t0 + b * BEAT, tone(110, .05, "sine", decay=40) * .5)
        if b % 2 == 1:
            nz = rng.standard_normal(int(SR * .12)) * np.exp(-30 * np.arange(int(SR * .12)) / SR)
            add(music, t0 + b * BEAT, nz * .45)
    for e8 in range(8):  # hats
        n = int(SR * .03)
        add(music, t0 + e8 * BEAT / 2 + (BEAT / 4 if bar % 2 else 0) * 0, rng.standard_normal(n) * np.exp(-90 * np.arange(n) / SR) * .18)
    for e8 in range(8):  # bass octave bounce
        n = root - 12 + (12 if e8 % 2 else 0)
        add(music, t0 + e8 * BEAT / 2, tone(note_hz(n), BEAT / 2 * .9, "tri", decay=3) * .55)
    for s16 in range(16):  # arpeggio
        n = chord[s16 % 3] + 12
        add(music, t0 + s16 * BEAT / 4, tone(note_hz(n), BEAT / 4 * .8, "square", duty=.25, decay=14) * .12)
# lead melody: pentatonic phrases per 2 bars
scale = [69, 71, 73, 76, 78, 81, 83, 85]
rhythm = [0, 1, 1.5, 2, 3, 3.5, 4, 5, 6, 6.5, 7]
for phrase in range(BARS // 2):
    if phrase in (3, 7): continue  # breathing room
    idx = rng.integers(1, 5)
    for i, r in enumerate(rhythm):
        idx = int(np.clip(idx + rng.choice([-2, -1, 1, 1, 2]), 0, len(scale) - 1))
        dur = (rhythm[i + 1] - r if i + 1 < len(rhythm) else 1) * BEAT * .85
        add(music, phrase * 2 * BAR + r * BEAT, tone(note_hz(scale[idx]), dur, "square", duty=.5, decay=2.5) * .2)
save("music", music, .7)

# ---- cues
save("beep", tone(note_hz(69), .22, "square", duty=.5, decay=8))
go = tone(note_hz(81), .5, "square", duty=.5, decay=3) + tone(note_hz(76), .5, "square", duty=.25, decay=3) * .5
save("go", go)
lap = np.zeros(int(SR * .5))
for i, n in enumerate([76, 81]): add(lap, i * .12, tone(note_hz(n), .3, "square", duty=.25, decay=6))
save("lap", lap)
final = np.zeros(int(SR * .9))
for i, n in enumerate([69, 73, 76, 81, 85]): add(final, i * .1, tone(note_hz(n), .4, "square", duty=.5, decay=5))
save("final", final)
win = np.zeros(int(SR * 2.0))
for i, n in enumerate([72, 76, 79, 84]): add(win, i * .14, tone(note_hz(n), .35, "square", duty=.5, decay=5))
for n in [72, 76, 79, 84]: add(win, .65, tone(note_hz(n), 1.2, "square", duty=.25, decay=2) * .5)
save("win", win)
lose = np.zeros(int(SR * 1.2))
for i, n in enumerate([72, 71, 69, 67]): add(lose, i * .18, tone(note_hz(n), .4, "tri", decay=4))
save("finish", lose)
rec = np.zeros(int(SR * .8))
for i, n in enumerate([84, 88, 91, 96]): add(rec, i * .07, tone(note_hz(n), .3, "square", duty=.125, decay=7))
save("record", rec)
slip_n = int(SR * .45); tt = np.arange(slip_n) / SR
slip = rng.standard_normal(slip_n) * np.sin(np.pi * tt / tt[-1]) * (.4 + .6 * np.sin(2 * np.pi * 9 * tt) ** 2)
save("slip", slip, .5)
# ---- kart-feel cues (2026-11-10 b)
hop = np.zeros(int(SR * .18)); tt = np.arange(len(hop)) / SR
hop = np.sin(2 * np.pi * np.cumsum(np.linspace(300, 700, len(hop))) / SR) * np.exp(-14 * tt)
save("hop", hop, .6)
for i, base in enumerate([76, 79, 84]):
    x = np.zeros(int(SR * .3))
    add(x, 0, tone(note_hz(base), .12, "square", duty=.25, decay=10)); add(x, .07, tone(note_hz(base + 7), .2, "square", duty=.25, decay=9))
    save("tier%d" % (i + 1), x, .6)
save("tick", tone(note_hz(93), .045, "square", duty=.5, decay=40), .5)
land = np.zeros(int(SR * .4))
for i, n in enumerate([81, 88]): add(land, i * .08, tone(note_hz(n), .25, "square", duty=.5, decay=8))
save("itemland", land, .6)
n = int(SR * 1.6); tt = np.arange(n) / SR
crack = rng.standard_normal(n) * np.exp(-9 * tt)
rumble = np.convolve(rng.standard_normal(n), np.ones(120) / 120, mode="same") * 6 * np.exp(-2.2 * tt)
save("thunder", crack * .6 + rumble, .8)
print("ok", OUT)
