"""Audio original sintetizado (sin muestras) para Arma tu Nave (2026-11-17).
Uso: python Tools/generate_nave_audio.py [carpeta_salida]
Salida por defecto: Assets/003_Scripts/Naves/Resources/NaveAudio/*.wav"""
import os, sys, wave
import numpy as np
SR = 22050
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "Assets", "003_Scripts", "Naves", "Resources", "NaveAudio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(73)

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
p = np.zeros(int(SR * .35))
for i, n in enumerate([79, 84, 88]): add(p, i * .05, tone(hz(n), .18, "square", 12, duty=.3) * .4)
save("pick", p)
ins = np.zeros(int(SR * .9))
add(ins, 0, lp(noise(.15, 20), 3) * .5); add(ins, .02, tone(220, .2, "square", 14, f2=160, duty=.4) * .5)
for i, n in enumerate([72, 76, 79, 84, 88]): add(ins, .12 + i * .07, tone(hz(n), .22, "tri", 8) * .5)
save("install", ins)
save("drop", M(tone(400, .2, "tri", 12, f2=180) * .6, lp(noise(.1, 30), 4) * .4))
save("whoosh", lp(noise(.25, 10), 6) * np.sin(np.linspace(0, np.pi, int(SR * .25))))
save("bonk", M(tone(260, .25, "sine", 12, f2=120), tone(520, .1, "tri", 25) * .4, lp(noise(.08, 40), 3) * .4))
save("jump", M(tone(330, .16, "square", 12, f2=620, duty=.3) * .5, tone(660, .12, "sine", 14, f2=1100) * .3))
rd = np.zeros(int(SR * 1.2))
for k in range(3): add(rd, k * .3, M(tone(1500, .25, "sine", 6, f2=1800) * .4, tone(750, .25, "sine", 6) * .2))
save("radar", rd)
la = M(lp(noise(2.2, .8), 3) * .7, tone(70, 2.2, "saw", .6, f2=40) * .4)
for i, n in enumerate([60, 64, 67, 72, 76, 79, 84]): add(la, .3 + i * .12, tone(hz(n), .3, "square", 6, duty=.3) * .25)
save("launch", la)
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

# Música: aventura espacial alegre (sol mayor, 128 bpm, 16 compases)
bpm = 128; beat = 60 / bpm; bars = 16; L = beat * 4 * bars
mus = np.zeros(int(SR * L) + SR)
prog = [55, 55, 52, 52, 48, 48, 50, 50, 55, 55, 52, 52, 48, 50, 55, 55]
for bar, root in enumerate(prog):
    minor = root == 52
    for k in range(8):
        n = root + (0, 7, 12, 7, 0 + 12, 7, 12, 16 if not minor else 15)[k] - 12
        add(mus, (bar * 4 + k * .5) * beat, tone(hz(n), beat * .45, "square", 9, duty=.25) * .14)
    for k in range(4):
        add(mus, (bar * 4 + k) * beat, tone(hz(root - 24), beat * .8, "tri", 5) * .35)
        add(mus, (bar * 4 + k) * beat, lp(noise(.06, 50), 2) * (.22 if k % 2 else .12))
mel = [79, None, 83, 86, 84, None, 83, 81, 79, None, 76, None, 79, 81, 83, None,
       84, None, 83, 81, 79, None, 76, 74, 76, None, 79, None, None, None, None, None,
       79, None, 83, 86, 88, None, 86, 84, 83, None, 79, None, 81, 83, 84, None,
       86, None, 84, 83, 81, None, 79, 78, 79, None, None, None, None, None, None, None]
for i, n in enumerate(mel):
    if n is not None: add(mus, i * beat * .5 * 2, M(tone(hz(n), beat * .9, "tri", 3.5) * .28, tone(hz(n), beat * .9, "square", 5, duty=.12) * .07))
mus = mus[:int(SR * L)]
fade = int(SR * .02); mus[:fade] *= np.linspace(0, 1, fade); mus[-fade:] *= np.linspace(1, 0, fade)
save("music", mus, .7)
print("OK", sorted(os.listdir(OUT)))
