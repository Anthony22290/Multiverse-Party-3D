"""Audio original sintetizado (sin muestras) para ¡Atrápalo! (2026-11-17).
Uso: python Tools/generate_atrapa_audio.py [carpeta_salida]
Salida por defecto: Assets/003_Scripts/Atrapa/Resources/AtrapaAudio/*.wav"""
import os, sys, wave
import numpy as np
SR = 22050
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "Assets", "003_Scripts", "Atrapa", "Resources", "AtrapaAudio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(173)

def save(name, x, gain=.85):
    x = np.asarray(x, dtype=np.float64); peak = np.max(np.abs(x)) or 1
    x = np.clip(x / peak * gain, -1, 1)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((x * 32767).astype(np.int16).tobytes())
def hz(n): return 440 * 2 ** ((n - 69) / 12)
def t(sec): return np.arange(int(SR * sec)) / SR
def tone(f, sec, kind="sine", decay=6, attack=.004, duty=.5, f2=None):
    tt = t(sec); freq = np.linspace(f, f2 if f2 else f, len(tt)); ph = np.cumsum(freq) / SR % 1
    w = {"sine": np.sin(2*np.pi*ph), "square": np.where(ph < duty, 1., -1.), "saw": 2*ph-1, "tri": 4*np.abs(ph-.5)-1}[kind]
    e = np.exp(-decay*tt); a = max(1, int(SR*attack)); e[:a] *= np.linspace(0, 1, a); r = max(1, int(SR*.01)); e[-r:] *= np.linspace(1, 0, r)
    return w * e
def M(*xs):
    n = max(len(x) for x in xs); out = np.zeros(n)
    for x in xs: out[:len(x)] += x
    return out
def noise(sec, decay=8): tt = t(sec); return rng.standard_normal(len(tt)) * np.exp(-decay*tt)
def lp(x, k=20): return np.convolve(x, np.ones(k)/k, mode="same")
def add(buf, at, x):
    s = int(at*SR); e = min(len(buf), s+len(x))
    if s < len(buf): buf[s:e] += x[:e-s]

# Efectos
la = np.zeros(int(SR * .8))
add(la, 0, lp(noise(.5, 6), 5) * .8); add(la, 0, tone(110, .35, "sine", 10, f2=55) * .8); add(la, .02, tone(700, .25, "sine", 12, f2=1400) * .15)
save("launch", la)
save("press", M(tone(hz(84), .07, "square", 40, duty=.5) * .3, tone(hz(91), .05, "sine", 40) * .2))
save("buzz", M(tone(110, .45, "square", 6, duty=.5) * .35, tone(116, .45, "square", 6, duty=.5) * .3))
ct = np.zeros(int(SR * .6))
for i, n in enumerate([79, 84, 88]): add(ct, i * .06, tone(hz(n), .22, "tri", 10) * .4)
add(ct, 0, lp(noise(.06, 50), 2) * .3)
save("catch", ct)
gd = np.zeros(int(SR * 1.2))
for i, n in enumerate([84, 88, 91, 96, 100, 103]): add(gd, i * .06, M(tone(hz(n), .45, "sine", 6) * .3, tone(hz(n) * 2, .3, "sine", 10) * .08))
save("gold", gd)
save("bonk", M(tone(220, .3, "sine", 12, f2=90) * .6, lp(noise(.1, 40), 2) * .4, tone(1300, .15, "sine", 20, f2=900) * .1))
bm = np.zeros(int(SR * 1.4))
add(bm, 0, lp(noise(1.2, 3.5), 6) * 1.0); add(bm, 0, tone(90, .9, "sine", 4, f2=35) * .9)
save("boom", bm, .95)
save("beep", tone(hz(81), .12, "square", 25, duty=.5) * .35)
save("go", M(tone(hz(84), .4, "square", 5, duty=.5) * .4, tone(hz(79), .4, "square", 5, duty=.25) * .3))
w = np.zeros(int(SR * 1.8))
for i, n in enumerate([72, 76, 79, 84, 83, 84, 88, 91]): add(w, i * .12, M(tone(hz(n), .3, "square", 7, duty=.5) * .4, tone(hz(n - 12), .3, "tri", 7) * .3))
save("win", w)
l = np.zeros(int(SR * 1.4))
for i, n in enumerate([67, 64, 62, 60]): add(l, i * .22, tone(hz(n), .38, "tri", 5) * .6)
save("lose", l)

# Música: concurso de la tele (si bemol mayor, 4/4, 128 bpm), metales cortos y bajo saltarín
bpm = 128; beat = 60 / bpm; bars = 16; L = beat * 4 * bars
mus = np.zeros(int(SR * L) + SR)
prog = [46, 51, 53, 46, 46, 51, 53, 53] * 2
chords = {46: (70, 74, 77), 51: (70, 75, 79), 53: (69, 72, 77)}
for bar, root in enumerate(prog):
    for k in range(4):
        add(mus, (bar * 4 + k) * beat, tone(hz(root - 12 + (7 if k % 2 else 0)), beat * .45, "tri", 8) * .42)
        add(mus, (bar * 4 + k) * beat, lp(noise(.05, 60), 1) * (.22 if k % 2 == 0 else .12))
    for k in (.5, 1.5, 2.5, 3.5):
        for n in chords[root]: add(mus, (bar * 4 + k) * beat, tone(hz(n), beat * .22, "square", 18, duty=.35) * .045)
mel = [77, 77, 79, 81, 82, None, 81, 79, 77, None, 74, None, 72, 74, 77, None, 79, 79, 81, 82, 84, None, 82, 81, 79, None, 77, None, 74, None, None, None]
for rep in range(2):
    for i, n in enumerate(mel):
        if n is not None: add(mus, (rep * 32 + i) * beat, M(tone(hz(n), beat * .8, "square", 6, duty=.4) * .1, tone(hz(n - 12), beat * .6, "tri", 6) * .07))
mus = mus[:int(SR * L)]
fade = int(SR * .02); mus[:fade] *= np.linspace(0, 1, fade); mus[-fade:] *= np.linspace(1, 0, fade)
save("music", mus, .6)
print("OK", sorted(os.listdir(OUT)))
