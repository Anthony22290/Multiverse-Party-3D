"""Audio original sintetizado (sin muestras) para Pico, Pala y Tijera (2026-11-17).
Uso: python Tools/generate_mina_audio.py [carpeta_salida]
Salida por defecto: Assets/003_Scripts/Excavacion/Resources/MinaAudio/*.wav"""
import os, sys, wave
import numpy as np
SR = 22050
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "Assets", "003_Scripts", "Excavacion", "Resources", "MinaAudio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(71)

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
save("dirt", M(lp(noise(.18, 18), 12) * .9, tone(140, .12, "sine", 20, f2=90) * .5))
st = M(lp(noise(.16, 22), 3) * .6, tone(1400, .08, "square", 40, duty=.3) * .25, tone(220, .14, "tri", 18, f2=160) * .5)
save("stone", st)
save("crack", M(lp(noise(.1, 30), 2) * .7, tone(1800, .05, "square", 50, duty=.2) * .3))
lv = np.zeros(int(SR * .22))
for k in range(2): add(lv, k * .07, M(lp(noise(.06, 50), 2) * .5, tone(3000, .04, "square", 60, duty=.15) * .25))
add(lv, .02, lp(noise(.18, 14), 8) * .4)
save("leaves", lv)
g = np.zeros(int(SR * .5))
for i, n in enumerate([84, 88, 91, 96]): add(g, i * .06, tone(hz(n), .2, "sine", 10) * .5)
save("gold", g)
save("wrong", M(tone(150, .3, "square", 7, duty=.5) * .45, tone(155, .3, "square", 7, duty=.5) * .45, lp(noise(.12, 25), 3) * .4, tone(2200, .1, "sine", 30) * .2))
save("select", tone(1200, .05, "square", 50, duty=.25) * .5)
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

# Música: polca minera alegre (do mayor, 150 bpm, 16 compases en bucle)
bpm = 150; beat = 60 / bpm; bars = 16; L = beat * 4 * bars
mus = np.zeros(int(SR * L) + SR)
prog = [48, 48, 53, 48, 43, 43, 48, 48, 48, 48, 53, 53, 43, 43, 48, 48]
for bar, root in enumerate(prog):
    third = 4 if root != 43 else 4
    for k in range(4):
        n = root - 12 if k % 2 == 0 else root + (7 if k == 1 else 12)
        add(mus, (bar * 4 + k) * beat, tone(hz(n), beat * .45, "square", 10, duty=.4) * (.28 if k % 2 == 0 else .16))
        if k % 2 == 1: add(mus, (bar * 4 + k) * beat, M(tone(hz(root + third + 12), beat * .3, "square", 14, duty=.25) * .12, tone(hz(root + 7 + 12), beat * .3, "square", 14, duty=.25) * .12))
        add(mus, (bar * 4 + k) * beat, lp(noise(.05, 60), 2) * (.18 if k % 2 else .1))
mel = [76, 74, 72, 74, 76, 76, 76, None, 74, 74, 74, None, 76, 79, 79, None,
       76, 74, 72, 74, 76, 76, 76, 76, 74, 74, 76, 74, 72, None, None, None,
       79, 81, 79, 77, 76, 77, 79, None, 74, 76, 77, None, 76, 77, 79, None,
       79, 81, 79, 77, 76, 74, 72, 76, 74, 74, 76, 74, 72, None, None, None]
for i, n in enumerate(mel):
    if n is not None: add(mus, i * beat, M(tone(hz(n), beat * .8, "tri", 4) * .3, tone(hz(n), beat * .8, "square", 6, duty=.12) * .07))
mus = mus[:int(SR * L)]
fade = int(SR * .02); mus[:fade] *= np.linspace(0, 1, fade); mus[-fade:] *= np.linspace(1, 0, fade)
save("music", mus, .7)
print("OK", sorted(os.listdir(OUT)))
