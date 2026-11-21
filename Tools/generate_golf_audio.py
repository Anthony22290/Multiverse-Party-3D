"""Audio original sintetizado (sin muestras) para Minigolf Loco (2026-11-17).
Uso: python Tools/generate_golf_audio.py [carpeta_salida]
Salida por defecto: Assets/003_Scripts/Minigolf/Resources/GolfAudio/*.wav"""
import os, sys, wave
import numpy as np
SR = 22050
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "Assets", "003_Scripts", "Minigolf", "Resources", "GolfAudio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(83)

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
save("putt", M(tone(1800, .06, "sine", 60) * .6, lp(noise(.05, 80), 2) * .5, tone(600, .1, "tri", 40) * .3))
cup = np.zeros(int(SR * 1.0))
for i, k in enumerate([0, .09, .16, .21]): add(cup, k, tone(500 - i * 60, .08, "sine", 40) * (.6 - i * .1))
for i, n in enumerate([79, 84, 88, 91]): add(cup, .3 + i * .08, tone(hz(n), .25, "tri", 8) * .4)
save("cup", cup)
save("splash", M(lp(noise(.6, 6), 4) * .8, tone(300, .3, "sine", 10, f2=120) * .3))
save("poof", M(lp(noise(.4, 10), 8) * .7, tone(500, .3, "sine", 8, f2=200) * .3))
it = np.zeros(int(SR * .8))
for i, n in enumerate([72, 76, 79]): add(it, i * .12, tone(hz(n), .3, "tri", 7) * .5)
save("intro", it)
save("go", M(tone(hz(84), .4, "square", 5, duty=.5) * .4, tone(hz(79), .4, "square", 5, duty=.25) * .3))
en = np.zeros(int(SR * .9))
for i, n in enumerate([79, 76, 72, 79]): add(en, i * .14, tone(hz(n), .25, "tri", 8) * .5)
save("end", en)
al = np.zeros(int(SR * 1.2))
for k in range(4): add(al, k * .26, tone(990 if k % 2 == 0 else 740, .2, "square", 5, duty=.5) * .45)
save("alarm", al)
w = np.zeros(int(SR * 1.8))
for i, n in enumerate([72, 76, 79, 84, 83, 84, 88, 91]): add(w, i * .12, M(tone(hz(n), .3, "square", 7, duty=.5) * .4, tone(hz(n - 12), .3, "tri", 7) * .3))
save("win", w)
l = np.zeros(int(SR * 1.4))
for i, n in enumerate([67, 64, 62, 60]): add(l, i * .22, tone(hz(n), .38, "tri", 5) * .6)
save("lose", l)

# Música: vals tranquilo de club de golf (re mayor, 3/4, 120 bpm)
bpm = 120; beat = 60 / bpm; bars = 24; L = beat * 3 * bars
mus = np.zeros(int(SR * L) + SR)
prog = [50, 50, 55, 55, 57, 57, 50, 50, 50, 50, 55, 55, 57, 57, 50, 57, 52, 52, 57, 57, 50, 55, 57, 50]
for bar, root in enumerate(prog):
    add(mus, (bar * 3) * beat, tone(hz(root - 12), beat * .9, "tri", 3) * .4)
    for k in (1, 2):
        for n in (root + 4 if root != 52 else root + 3, root + 7): add(mus, (bar * 3 + k) * beat, tone(hz(n), beat * .5, "sine", 6) * .12)
mel = [66, 69, 74, 73, 71, 69, 71, 74, 78, 76, 74, 73, 74, None, None, 69, 71, 73, 74, 76, 78, 79, 78, 76, 74, None, None,
       74, 76, 78, 79, 81, 79, 78, 76, 74, 73, 71, 69, 71, None, None, 69, 74, 73, 71, 73, 76, 74, None, None]
for i, n in enumerate(mel):
    if n is not None: add(mus, i * beat, M(tone(hz(n), beat * 1.1, "tri", 2.5) * .26, tone(hz(n + 12), beat * .8, "sine", 5) * .05))
mus = mus[:int(SR * L)]
fade = int(SR * .02); mus[:fade] *= np.linspace(0, 1, fade); mus[-fade:] *= np.linspace(1, 0, fade)
save("music", mus, .65)
print("OK", sorted(os.listdir(OUT)))
