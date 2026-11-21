"""Audio original sintetizado (sin muestras) para Propulsores (2026-11-17).
Uso: python Tools/generate_flap_audio.py [carpeta_salida]
Salida por defecto: Assets/003_Scripts/Propulsores/Resources/FlapAudio/*.wav"""
import os, sys, wave
import numpy as np
SR = 22050
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "Assets", "003_Scripts", "Propulsores", "Resources", "FlapAudio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(79)

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
save("flap", M(lp(noise(.18, 16), 3) * .6, tone(180, .14, "square", 18, f2=320, duty=.3) * .35))
pt = np.zeros(int(SR * .3)); add(pt, 0, tone(hz(88), .1, "square", 20, duty=.25) * .4); add(pt, .07, tone(hz(93), .18, "square", 14, duty=.25) * .4)
save("point", pt)
save("crash", M(lp(noise(.5, 7), 3) * .8, tone(220, .5, "square", 6, f2=60, duty=.4) * .5, tone(900, .15, "sine", 25, f2=200) * .3))
rs = np.zeros(int(SR * .5))
for i, n in enumerate([72, 79, 84]): add(rs, i * .07, tone(hz(n), .2, "tri", 9) * .5)
save("respawn", rs)
save("beep", tone(880, .16, "square", 10, duty=.5) * .6)
save("go", M(tone(hz(84), .5, "square", 4, duty=.5) * .5, tone(hz(79), .5, "square", 4, duty=.25) * .3))
al = np.zeros(int(SR * 1.2))
for k in range(4): add(al, k * .26, tone(990 if k % 2 == 0 else 740, .2, "square", 5, duty=.5) * .45)
save("alarm", al)
w = np.zeros(int(SR * 1.8))
for i, n in enumerate([72, 76, 79, 84, 83, 84, 88, 91]): add(w, i * .12, M(tone(hz(n), .3, "square", 7, duty=.5) * .4, tone(hz(n - 12), .3, "tri", 7) * .3))
save("win", w)
l = np.zeros(int(SR * 1.4))
for i, n in enumerate([67, 64, 62, 60]): add(l, i * .22, tone(hz(n), .38, "tri", 5) * .6)
save("lose", l)

# Música: chiptune veloz (fa mayor, 160 bpm, 16 compases)
bpm = 160; beat = 60 / bpm; bars = 16; L = beat * 4 * bars
mus = np.zeros(int(SR * L) + SR)
prog = [53, 53, 50, 50, 46, 46, 48, 48, 53, 53, 50, 50, 46, 48, 53, 53]
for bar, root in enumerate(prog):
    for k in range(8):
        n = root - 12 + (0, 12, 7, 12, 4 if root != 50 else 3, 12, 7, 12)[k]
        add(mus, (bar * 4 + k * .5) * beat, tone(hz(n), beat * .45, "square", 10, duty=.25) * .14)
    for k in range(4):
        add(mus, (bar * 4 + k) * beat, tone(hz(root - 24), beat * .6, "tri", 6) * .35)
        add(mus, (bar * 4 + k + .5) * beat, lp(noise(.05, 60), 2) * .14)
        if k % 2 == 0: add(mus, (bar * 4 + k) * beat, tone(55, .15, "sine", 20, f2=35) * .5)
mel = [77, 79, 81, None, 84, None, 81, 79, 77, None, 74, None, 77, None, None, None,
       77, 79, 81, None, 84, 86, 84, 81, 79, None, 81, 79, 77, None, None, None,
       74, 77, 79, None, 81, None, 79, 77, 74, None, 72, None, 74, None, 77, None,
       79, 81, 84, None, 81, 79, 77, 79, 81, None, 79, None, 77, None, None, None]
for i, n in enumerate(mel):
    if n is not None: add(mus, i * beat * .5 * 2, M(tone(hz(n), beat * .9, "tri", 3.5) * .28, tone(hz(n), beat * .9, "square", 5, duty=.12) * .07))
mus = mus[:int(SR * L)]
fade = int(SR * .02); mus[:fade] *= np.linspace(0, 1, fade); mus[-fade:] *= np.linspace(1, 0, fade)
save("music", mus, .7)
print("OK", sorted(os.listdir(OUT)))
