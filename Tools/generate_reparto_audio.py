"""Audio original sintetizado (sin muestras) para Reparto Loco (2026-11-17).
Uso: python Tools/generate_reparto_audio.py [carpeta_salida]
Salida por defecto: Assets/003_Scripts/Reparto/Resources/RepartoAudio/*.wav"""
import os, sys, wave
import numpy as np
SR = 22050
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "Assets", "003_Scripts", "Reparto", "Resources", "RepartoAudio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(151)

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
save("honk", M(tone(420, .35, "square", 4, duty=.5) * .3, tone(530, .35, "square", 4, duty=.5) * .25))
save("honk2", M(tone(300, .5, "square", 3, duty=.4) * .32, tone(380, .5, "square", 3, duty=.4) * .25))
cr = np.zeros(int(SR * 1.0))
add(cr, 0, lp(noise(.6, 7), 4) * .9); add(cr, 0, tone(140, .4, "sine", 9, f2=60) * .7); add(cr, .05, lp(noise(.25, 30), 2) * .5)
add(cr, .1, M(tone(1800, .15, "square", 25, duty=.5) * .12, tone(2400, .12, "square", 30, duty=.5) * .1))
save("crash", cr)
save("pick", M(tone(hz(79), .12, "tri", 18) * .4, tone(300, .08, "sine", 30) * .2))
dl = np.zeros(int(SR * .7))
for i, n in enumerate([79, 83, 86, 91]): add(dl, i * .07, tone(hz(n), .25, "tri", 9) * .4)
add(dl, .05, tone(2000, .3, "sine", 10) * .08)
save("deliver", dl)
save("jump", tone(420, .14, "sine", 18, f2=760) * .5)
save("dash", M(lp(noise(.22, 12), 4) * .6, tone(300, .2, "sine", 12, f2=150) * .2))
save("beep", tone(hz(81), .12, "square", 25, duty=.5) * .35)
save("go", M(tone(hz(84), .4, "square", 5, duty=.5) * .4, tone(hz(79), .4, "square", 5, duty=.25) * .3))
w = np.zeros(int(SR * 1.8))
for i, n in enumerate([72, 76, 79, 84, 83, 84, 88, 91]): add(w, i * .12, M(tone(hz(n), .3, "square", 7, duty=.5) * .4, tone(hz(n - 12), .3, "tri", 7) * .3))
save("win", w)
l = np.zeros(int(SR * 1.4))
for i, n in enumerate([67, 64, 62, 60]): add(l, i * .22, tone(hz(n), .38, "tri", 5) * .6)
save("lose", l)

# Música: funk urbano ligero (la menor, 4/4, 112 bpm), bajo sincopado y acordes cortos
bpm = 112; beat = 60 / bpm; bars = 16; L = beat * 4 * bars
mus = np.zeros(int(SR * L) + SR)
prog = [45, 45, 50, 52] * 4
chords = {45: (69, 72, 76), 50: (69, 74, 77), 52: (68, 71, 76)}
bass = [0, None, 0, 12, None, 7, None, 10]
for bar, root in enumerate(prog):
    for k, b in enumerate(bass):
        if b is not None: add(mus, (bar * 4 + k * .5) * beat, tone(hz(root - 12 + b), beat * .4, "tri", 9) * .42)
    for k in (1, 3):
        for n in chords[root]: add(mus, (bar * 4 + k) * beat, tone(hz(n), beat * .2, "square", 20, duty=.3) * .05)
    add(mus, (bar * 4) * beat, lp(noise(.08, 40), 1) * .25); add(mus, (bar * 4 + 2) * beat, lp(noise(.08, 40), 1) * .25)
    for k in range(8): add(mus, (bar * 4 + k * .5) * beat, noise(.03, 120) * .05)
mel = [76, None, 79, 76, 74, None, 72, None, 76, None, 79, 81, 79, None, None, None, 77, None, 76, 74, 72, None, 74, None, 76, None, 72, None, 69, None, None, None]
for rep in range(4):
    for i, n in enumerate(mel):
        if n is not None: add(mus, (rep * 16 + i * .5) * beat * 1, M(tone(hz(n), beat * .45, "square", 8, duty=.25) * .09, tone(hz(n + 12), beat * .3, "sine", 10) * .04))
mus = mus[:int(SR * L)]
fade = int(SR * .02); mus[:fade] *= np.linspace(0, 1, fade); mus[-fade:] *= np.linspace(1, 0, fade)
save("music", mus, .6)
print("OK", sorted(os.listdir(OUT)))
