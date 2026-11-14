"""Original synthesized sounds for Invasión Zombi (no samples). Writes 16-bit mono WAVs to
Assets/003_Scripts/ZombieSurvival/Resources/SurvivalAudio. Usage: python Tools/generate_zombie_audio.py"""
import os, wave
import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "003_Scripts", "ZombieSurvival", "Resources", "SurvivalAudio")
rng = np.random.default_rng(13)


def save(name, x, gain=.9):
    x = np.asarray(x, dtype=np.float64)
    peak = np.max(np.abs(x)) or 1.0
    x = np.clip(x / peak * gain, -1, 1)
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())


def t(sec):
    return np.arange(int(SR * sec)) / SR


def env(n, attack=.02, release=.3):
    e = np.ones(n)
    a = max(1, int(SR * attack)); r = max(1, int(SR * release))
    e[:a] = np.linspace(0, 1, a)
    e[-r:] *= np.linspace(1, 0, r)
    return e


def lowpass(x, cutoff):
    # One-pole low-pass, cutoff in Hz (scalar or per-sample array).
    cutoff = np.broadcast_to(cutoff, x.shape)
    y = np.zeros_like(x); s = 0.0
    for i in range(len(x)):
        a = 1 - np.exp(-2 * np.pi * cutoff[i] / SR)
        s += a * (x[i] - s); y[i] = s
    return y


def formant(x, freq, q=6.0):
    # Simple resonant band-pass (biquad) for vowel-ish growls.
    w0 = 2 * np.pi * freq / SR; alpha = np.sin(w0) / (2 * q)
    b0, b2 = alpha, -alpha; a0, a1, a2 = 1 + alpha, -2 * np.cos(w0), 1 - alpha
    y = np.zeros_like(x); x1 = x2 = y1 = y2 = 0.0
    for i in range(len(x)):
        y0 = (b0 * x[i] + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1, y2, y1 = x1, x[i], y1, y0; y[i] = y0
    return y


def growl(sec, f0, f1, rough=.6, vowels=(500, 900)):
    tt = t(sec)
    f = np.linspace(f0, f1, len(tt)) * (1 + .05 * np.sin(2 * np.pi * 5.5 * tt))
    phase = np.cumsum(f) / SR
    saw = 2 * (phase % 1) - 1
    am = 1 + rough * np.sign(np.sin(2 * np.pi * 31 * tt)) * .5  # vocal fry flutter
    src = saw * am + .35 * rng.standard_normal(len(tt))
    y = formant(src, vowels[0], 5) + .7 * formant(src, vowels[1], 7)
    return y * env(len(tt), .06, sec * .45)


def main():
    save("zgroan_0", growl(.85, 95, 70, .7, (420, 820)), .75)
    save("zgroan_1", growl(1.0, 120, 80, .5, (560, 1000)), .75)
    save("zgroan_2", growl(.7, 80, 105, .8, (380, 760)), .75)
    # Boss roar: two detuned growls + sub rumble.
    r = growl(1.6, 70, 52, .9, (330, 700)) + .8 * growl(1.6, 74, 55, .9, (300, 650))
    tt = t(1.6); r += .9 * np.sin(2 * np.pi * 42 * tt) * env(len(tt), .1, .8)
    save("zroar", r, .95)
    # Defeat: falling "bloop" + a soft puff.
    tt = t(.55)
    f = 420 * np.exp(-tt * 5) + 90
    bloop = np.sin(2 * np.pi * np.cumsum(f) / SR) * env(len(tt), .005, .3)
    puff = lowpass(rng.standard_normal(len(tt)), 1800) * np.exp(-tt * 9)
    save("zdie", bloop + .6 * puff, .7)
    # Engine starting: cranking chugs, then a rising idle.
    tt = t(2.4)
    rate = np.where(tt < .9, 7.0, 7 + (tt - .9) * 30)
    pulses = np.sin(2 * np.pi * np.cumsum(rate) / SR)
    chug = np.maximum(0, pulses) ** 3
    body = np.sin(2 * np.pi * np.cumsum(rate * 6) / SR) * .6 + lowpass(rng.standard_normal(len(tt)), 900) * .5
    save("zengine", chug * body * env(len(tt), .02, .5), .85)
    # Music: 16 s spooky but bouncy loop in D minor (bass ostinato + plucky arpeggio + soft pad).
    bpm = 120; beat = 60 / bpm; bars = 8; total = bars * 4 * beat
    tt = t(total); y = np.zeros(len(tt))
    def note(midi): return 440 * 2 ** ((midi - 69) / 12)
    prog = [(50, [62, 65, 69]), (46, [62, 65, 70]), (48, [60, 64, 67]), (45, [61, 64, 69])]
    for bar in range(bars):
        root, chord = prog[bar % 4]
        for step in range(8):  # eighth-note bass
            start = int(SR * (bar * 4 * beat + step * beat / 2)); n = int(SR * beat / 2 * .9)
            seg = t(beat / 2 * .9)
            m = root + (12 if step % 4 == 3 else 0)
            tone = np.sign(np.sin(2 * np.pi * note(m - 12) * seg)) * .5 + np.sin(2 * np.pi * note(m - 12) * seg)
            y[start:start + n] += .22 * tone * np.exp(-seg * 6)
        for step in range(16):  # sixteenth arpeggio
            start = int(SR * (bar * 4 * beat + step * beat / 4)); seg = t(beat / 4)
            m = chord[step % 3] + (12 if step % 8 >= 6 else 0)
            y[start:start + len(seg)] += .08 * np.sin(2 * np.pi * note(m) * seg) * np.exp(-seg * 18)
        start = int(SR * bar * 4 * beat); seg = t(4 * beat)
        pad = sum(np.sin(2 * np.pi * note(m - 12) * seg * (1 + .003 * k)) for k, m in enumerate(chord))
        y[start:start + len(seg)] += .05 * pad * np.minimum(1, seg * 2) * np.minimum(1, (4 * beat - seg) * 2)
        for b in range(4):  # soft kick + hat
            start = int(SR * (bar * 4 * beat + b * beat)); seg = t(.18)
            y[start:start + len(seg)] += .35 * np.sin(2 * np.pi * (120 * np.exp(-seg * 20) + 40) * seg) * np.exp(-seg * 14)
            hs = start + int(SR * beat / 2); hseg = t(.05)
            y[hs:hs + len(hseg)] += .04 * rng.standard_normal(len(hseg)) * np.exp(-hseg * 60)
    save("zmusic", y, .8)
    print("OK ->", os.path.abspath(OUT))


if __name__ == "__main__":
    main()
