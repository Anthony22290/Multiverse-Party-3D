"""Audio original sintetizado (sin muestras) para Gallinas Locas (2026-11-17).
Uso: python Tools/generate_granja_audio.py [carpeta_salida]
Salida por defecto: Assets/003_Scripts/Granja/Resources/GranjaAudio/*.wav"""
import os, sys, wave
import numpy as np
SR = 22050
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "Assets", "003_Scripts", "Granja", "Resources", "GranjaAudio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(131)

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
def cluck(base, n):
    out = np.zeros(int(SR * (.1 + n * .11)))
    for k in range(n):
        f = base * (1 + .08 * k)
        add(out, k * .11, M(tone(f, .08, "square", 30, f2=f * .7, duty=.35) * .35, lp(noise(.06, 40), 2) * .15))
    return out
save("cluck", cluck(520, 3))
save("cluck2", M(tone(700, .25, "square", 8, f2=950, duty=.3) * .3, lp(noise(.2, 10), 2) * .1))
save("grab", M(cluck(640, 2) * .8, tone(300, .08, "sine", 30) * .3))
save("drop", M(tone(400, .1, "sine", 25, f2=250) * .4, lp(noise(.08, 30), 3) * .3))
pn = np.zeros(int(SR * .5))
for i, n in enumerate([76, 83]): add(pn, i * .08, tone(hz(n), .25, "tri", 10) * .45)
save("pen", pn)
gd = np.zeros(int(SR * 1.2))
for i, n in enumerate([84, 88, 91, 96, 100]): add(gd, i * .07, M(tone(hz(n), .4, "sine", 6) * .3, tone(hz(n) * 2, .3, "sine", 10) * .08))
save("gold", gd)
save("tick", M(tone(1800, .03, "square", 90, duty=.5) * .35, lp(noise(.02, 200), 1) * .2))
al = np.zeros(int(SR * .9))
for k in range(4): add(al, k * .2, tone(1100 if k % 2 == 0 else 820, .15, "square", 6, duty=.5) * .4)
save("alarm", al)
bm = np.zeros(int(SR * 1.6))
add(bm, 0, lp(noise(1.5, 3.2), 6) * 1.0); add(bm, 0, tone(90, 1.0, "sine", 4, f2=35) * .9); add(bm, 0, lp(noise(.08, 60), 1) * .8)
add(bm, .15, cluck(900, 4) * .4)
save("boom", bm, .95)
save("bump", M(tone(160, .18, "sine", 14, f2=90) * .7, lp(noise(.12, 25), 3) * .4))
save("jump", tone(420, .14, "sine", 18, f2=760) * .5)
save("dash", M(lp(noise(.22, 12), 4) * .6, tone(300, .2, "sine", 12, f2=150) * .2))
save("beep", tone(hz(81), .12, "square", 25, duty=.5) * .35)
gw = np.zeros(int(SR * 1.0))
add(gw, 0, tone(1300, .7, "sine", 3, f2=1500) * .35); add(gw, .05, tone(2600, .6, "sine", 4) * .08)
save("go", gw)
w = np.zeros(int(SR * 1.8))
for i, n in enumerate([67, 72, 76, 79, 76, 79, 84]): add(w, i * .13, M(tone(hz(n), .3, "square", 7, duty=.5) * .35, tone(hz(n - 12), .3, "tri", 7) * .3))
save("win", w)
l = np.zeros(int(SR * 1.4))
for i, n in enumerate([67, 64, 62, 60]): add(l, i * .22, tone(hz(n), .38, "tri", 5) * .6)
save("lose", l)

# Música: polca de granja (sol mayor, 2/4, 160 bpm) con banjo picado y bajo alterno
bpm = 160; beat = 60 / bpm; bars = 32; L = beat * 2 * bars
mus = np.zeros(int(SR * L) + SR)
prog = [43, 43, 48, 48, 43, 43, 50, 50, 43, 43, 48, 48, 50, 50, 43, 43] * 2
chords = {43: (59, 62, 67), 48: (60, 64, 67), 50: (62, 66, 69)}
for bar, root in enumerate(prog):
    add(mus, bar * 2 * beat, tone(hz(root - 12), beat * .5, "tri", 8) * .45)
    add(mus, (bar * 2 + 1) * beat, tone(hz(root - 5), beat * .5, "tri", 8) * .38)
    for k in range(4):
        n = chords[root][k % 3]
        add(mus, (bar * 2 + k * .5) * beat, tone(hz(n + 12), beat * .25, "square", 22, duty=.2) * .055)
mel = [71, 74, 79, 74, 76, 74, 71, None, 72, 76, 79, 76, 74, None, 71, None, 71, 74, 79, 81, 83, 81, 79, 76, 74, 76, 74, 72, 71, None, 67, None,
       79, 81, 83, 79, 81, 79, 76, None, 72, 74, 76, 72, 74, None, 71, None, 74, 76, 78, 81, 79, 78, 76, 74, 79, None, 74, None, 67, None, None, None]
step = beat
for i, n in enumerate(mel):
    if n is not None: add(mus, i * step, M(tone(hz(n), step * .7, "tri", 7) * .2, tone(hz(n + 12), step * .4, "square", 12, duty=.2) * .04))
mus = mus[:int(SR * L)]
fade = int(SR * .02); mus[:fade] *= np.linspace(0, 1, fade); mus[-fade:] *= np.linspace(1, 0, fade)
save("music", mus, .6)
print("OK", sorted(os.listdir(OUT)))
