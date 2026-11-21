"""Audio original sintetizado (sin muestras) para Suelo que se Cae, Pintura Loca, Rey de la Colina y Memoria Musical (2026-11-19).
Uso: python Tools/generate_nuevos4_audio.py
Salida: Assets/003_Scripts/<Juego>/Resources/<Juego>Audio/*.wav"""
import os, sys, wave
import numpy as np
SR = 22050
HERE = os.path.dirname(os.path.abspath(__file__))


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

ROOT = os.path.join(HERE, "..", "Assets", "003_Scripts")
OUT = ROOT
def use(game):
    global OUT
    OUT = os.path.join(ROOT, game, "Resources", game + "Audio"); os.makedirs(OUT, exist_ok=True)

def common(base):
    save("beep", tone(hz(81 + base), .12, "square", 25, duty=.5) * .35)
    save("go", M(tone(hz(84 + base), .4, "square", 5, duty=.5) * .4, tone(hz(79 + base), .4, "square", 5, duty=.25) * .3))
    w = np.zeros(int(SR * 1.8))
    for i, n in enumerate([72, 76, 79, 84, 83, 84, 88, 91]): add(w, i * .12, M(tone(hz(n + base), .3, "square", 7, duty=.5) * .4, tone(hz(n - 12 + base), .3, "tri", 7) * .3))
    save("win", w)
    l = np.zeros(int(SR * 1.4))
    for i, n in enumerate([67, 64, 62, 60]): add(l, i * .22, tone(hz(n + base), .38, "tri", 5) * .6)
    save("lose", l)

def music(bpm, prog, scale, lead="square", bass="tri", drums=True, seed=1, bars=16, swing=0.0):
    r = np.random.default_rng(seed); beat = 60 / bpm; L = beat * 4 * bars
    mus = np.zeros(int(SR * L) + SR)
    for b in range(bars):
        root = prog[b % len(prog)]; t0 = b * beat * 4
        for k in range(4):
            add(mus, t0 + k * beat, tone(hz(root - 12 + (7 if k % 2 else 0)), beat * .9, bass, 5) * .35)
            if drums:
                add(mus, t0 + k * beat, tone(110, .15, "sine", 30, f2=45) * (.5 if k % 2 == 0 else .2))
                if k % 2: add(mus, t0 + k * beat, lp(noise(.12, 30), 2) * .18)
                add(mus, t0 + k * beat + beat / 2 + swing, lp(noise(.04, 90), 1) * .07)
            add(mus, t0 + k * beat + beat / 2, M(tone(hz(root + 12), beat * .4, "tri", 9), tone(hz(root + 16 if root % 12 in (0, 5, 7) else root + 15), beat * .4, "tri", 9)) * .09)
        for k in range(8):
            if r.random() < .7:
                n = root + 12 + scale[r.integers(len(scale))]
                add(mus, t0 + k * beat / 2, tone(hz(n), beat * .45, lead, 7, duty=.3) * .16)
    n = int(SR * L); x = mus[:n]; f = int(SR * .02); x[:f] *= np.linspace(0, 1, f); x[-f:] *= np.linspace(1, 0, f)
    save("music", x, .6)

# ---------------- Suelo que se Cae
use("Suelo"); common(0)
save("crack", M(lp(noise(.25, 18), 2) * .6, tone(900, .08, "square", 60, f2=300) * .2, lp(noise(.05, 80), 1) * .5))
sp = np.zeros(int(SR * 1.2)); add(sp, 0, lp(noise(1.0, 4), 8) * .8); add(sp, 0, tone(160, .6, "sine", 6, f2=60) * .6)
for i in range(6): add(sp, .1 + i * .12, tone(300 + 120 * i, .15, "sine", 20, f2=900) * .15)
save("splash", sp)
music(132, [57, 57, 53, 55], [0, 3, 5, 7, 10, 12], "square", seed=3)

# ---------------- Pintura Loca
use("Pintura"); common(2)
save("splat", M(lp(noise(.18, 22), 5) * .7, tone(260, .12, "sine", 25, f2=120) * .5))
save("bucket", M(tone(hz(84), .12, "sine", 18) * .4, tone(hz(91), .2, "sine", 12) * .4, tone(hz(96), .3, "sine", 9) * .3))
bs = np.zeros(int(SR * .9)); add(bs, 0, lp(noise(.7, 6), 6) * .9); add(bs, 0, tone(220, .4, "sine", 8, f2=70) * .7); add(bs, .05, lp(noise(.3, 12), 3) * .4)
save("splash", bs)
save("bump", M(tone(150, .2, "sine", 15, f2=70) * .7, lp(noise(.1, 40), 3) * .4))
music(124, [60, 65, 67, 60], [0, 2, 4, 7, 9, 12], "square", seed=5, swing=.03)

# ---------------- Rey de la Colina
use("Colina"); common(-2)
fan = np.zeros(int(SR * 1.0))
for i, n in enumerate([67, 72, 76, 79]): add(fan, i * .1, M(tone(hz(n), .5, "saw", 6) * .25, tone(hz(n) / 2, .5, "square", 6, duty=.5) * .15))
save("move", lp(fan, 3))
sp = np.zeros(int(SR * 1.2)); add(sp, 0, lp(noise(1.0, 4), 8) * .8); add(sp, 0, tone(160, .6, "sine", 6, f2=60) * .6)
save("splash", sp)
save("slip", M(tone(300, .5, "sine", 5, f2=1400) * .4, tone(1400, .3, "tri", 8, f2=200) * .25))
save("tick", tone(hz(96), .05, "sine", 60) * .4)
save("bump", M(tone(150, .2, "sine", 15, f2=70) * .7, lp(noise(.1, 40), 3) * .4))
bm = np.zeros(int(SR * 1.4)); add(bm, 0, lp(noise(1.3, 3.5), 6) * 1.0); add(bm, 0, tone(90, .9, "sine", 4, f2=35) * .9); add(bm, 0, lp(noise(.08, 60), 1) * .8)
save("boom", bm, .95)
save("throw", M(lp(noise(.25, 10), 4) * .5, tone(500, .2, "sine", 12, f2=260) * .25))
music(140, [62, 62, 67, 69], [0, 2, 4, 7, 9, 12], "saw", seed=7)

# ---------------- Memoria Musical
use("Simon"); common(0)
pads = [76, 72, 67, 64]   # rojo, azul, verde, amarillo: notas de un acorde (como el juguete clásico, pero sin copiar su sonido)
for i, n in enumerate(pads):
    save("pad%d" % i, M(tone(hz(n), .45, "tri", 5) * .6, tone(hz(n) * 2, .3, "sine", 9) * .15, tone(hz(n), .45, "square", 7, duty=.25) * .12))
save("ok", M(tone(hz(84), .12, "sine", 15) * .5, tone(hz(88), .14, "sine", 12) * .5, tone(hz(91), .3, "sine", 7) * .5))
f = np.zeros(int(SR * .8)); add(f, 0, tone(hz(50), .7, "saw", 4, f2=hz(46)) * .5); add(f, 0, tone(hz(51), .7, "square", 4, duty=.5, f2=hz(47)) * .3)
save("fail", lp(f, 4))
save("judge", M(tone(hz(64), .25, "tri", 9) * .5, tone(hz(60), .4, "tri", 6) * .4))
rd = np.zeros(int(SR * .7))
for i, n in enumerate([72, 76, 79]): add(rd, i * .08, tone(hz(n), .3, "sine", 10) * .4)
save("round", rd)
music(116, [57, 53, 60, 55], [0, 3, 5, 7, 10], "tri", seed=11, swing=.02)
print("ok")
