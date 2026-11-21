"""Audio original sintetizado (sin muestras) para Patata Caliente (2026-11-17).
Uso: python Tools/generate_patata_audio.py [carpeta_salida]
Salida por defecto: Assets/003_Scripts/Patata/Resources/PatataAudio/*.wav"""
import os, sys, wave
import numpy as np
SR = 22050
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "Assets", "003_Scripts", "Patata", "Resources", "PatataAudio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(97)

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
save("tick", M(tone(2400, .025, "square", 120, duty=.5) * .35, lp(noise(.02, 200), 1) * .3))
save("pass", M(lp(noise(.18, 14), 3) * .4, tone(500, .18, "sine", 12, f2=1100) * .5, tone(hz(84), .12, "tri", 20) * .3))
bm = np.zeros(int(SR * 1.6))
add(bm, 0, lp(noise(1.5, 3.2), 6) * 1.0)
add(bm, 0, tone(90, 1.0, "sine", 4, f2=35) * .9)
add(bm, 0, lp(noise(.08, 60), 1) * .8)
save("boom", bm, .95)
save("jump", tone(420, .14, "sine", 18, f2=760) * .5)
save("boing", M(tone(180, .45, "sine", 6, f2=520) * .6, tone(360, .3, "tri", 10, f2=900) * .2))
save("dash", M(lp(noise(.22, 12), 4) * .6, tone(300, .2, "sine", 12, f2=150) * .2))
save("beep", tone(hz(81), .12, "square", 25, duty=.5) * .35)
save("go", M(tone(hz(84), .4, "square", 5, duty=.5) * .4, tone(hz(79), .4, "square", 5, duty=.25) * .3))
bl = np.zeros(int(SR * 1.2))
for i, n in enumerate([84, 91]): add(bl, i * .09, M(tone(hz(n), .9, "sine", 4) * .45, tone(hz(n) * 2.76, .5, "sine", 8) * .12))
save("bell", bl)
w = np.zeros(int(SR * 1.8))
for i, n in enumerate([72, 76, 79, 84, 83, 84, 88, 91]): add(w, i * .12, M(tone(hz(n), .3, "square", 7, duty=.5) * .4, tone(hz(n - 12), .3, "tri", 7) * .3))
save("win", w)
l = np.zeros(int(SR * 1.4))
for i, n in enumerate([67, 64, 62, 60]): add(l, i * .22, tone(hz(n), .38, "tri", 5) * .6)
save("lose", l)

# Música: polka de feria (do mayor, 2/4, 150 bpm), oom-pah y melodía saltarina
bpm = 150; beat = 60 / bpm; bars = 32; L = beat * 2 * bars
mus = np.zeros(int(SR * L) + SR)
prog = [48, 43, 48, 43, 48, 43, 50, 43, 48, 43, 48, 43, 53, 43, 48, 48] * 2
chords = {48: (64, 67), 43: (62, 65), 50: (62, 65), 53: (65, 69)}
for bar, root in enumerate(prog):
    add(mus, bar * 2 * beat, tone(hz(root - 12), beat * .5, "tri", 8) * .45)
    for k in (.5, 1.5): 
        for n in chords[root]: add(mus, (bar * 2 + k) * beat, tone(hz(n), beat * .3, "square", 14, duty=.3) * .06)
    add(mus, (bar * 2 + 1) * beat, tone(hz(root - 5), beat * .5, "tri", 8) * .35)
mel = [72, 74, 76, 72, 76, None, 79, None, 77, 76, 74, 71, 74, None, 67, None, 72, 74, 76, 77, 79, None, 84, None, 83, 81, 79, 77, 76, 74, 72, None,
       76, 77, 79, 76, 79, None, 84, None, 83, 81, 79, 77, 79, None, 74, None, 72, 76, 79, 84, 81, 77, 74, 71, 72, None, 79, None, 72, None, None, None]
step = beat * .5 * 2
for rep in range(2):
    for i, n in enumerate(mel):
        if n is not None: add(mus, rep * len(mel) * step + i * step, M(tone(hz(n), step * .8, "square", 6, duty=.25) * .11, tone(hz(n + 12), step * .5, "sine", 9) * .05))
mus = mus[:int(SR * L)]
fade = int(SR * .02); mus[:fade] *= np.linspace(0, 1, fade); mus[-fade:] *= np.linspace(1, 0, fade)
save("music", mus, .6)
print("OK", sorted(os.listdir(OUT)))
