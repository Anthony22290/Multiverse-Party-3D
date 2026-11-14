"""Original synthesized audio (no samples) for Código Mortal, Invasión Zombi, Sala de Escape and
Arena del Caos upgrades (2026-11-10). Usage: python Tools/generate_minigame_audio.py"""
import os, wave
import numpy as np
SR = 22050
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "003_Scripts")
rng = np.random.default_rng(31)

def save(folder, name, x, gain=.85):
    x = np.asarray(x, dtype=np.float64); peak = np.max(np.abs(x)) or 1
    x = np.clip(x / peak * gain, -1, 1); path = os.path.join(ROOT, folder); os.makedirs(path, exist_ok=True)
    with wave.open(os.path.join(path, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((x * 32767).astype(np.int16).tobytes())

def hz(n): return 440 * 2 ** ((n - 69) / 12)
def t(sec): return np.arange(int(SR * sec)) / SR
def tone(f, sec, kind="sine", decay=6, attack=.004, duty=.5, f2=None):
    tt = t(sec); freq = np.linspace(f, f2 if f2 else f, len(tt)); ph = np.cumsum(freq) / SR % 1
    w = {"sine": np.sin(2*np.pi*ph), "square": np.where(ph < duty, 1., -1.), "saw": 2*ph-1, "tri": 4*np.abs(ph-.5)-1}[kind]
    e = np.exp(-decay*tt); a = max(1, int(SR*attack)); e[:a] *= np.linspace(0, 1, a); r = max(1, int(SR*.01)); e[-r:] *= np.linspace(1, 0, r)
    return w * e
def noise(sec, decay=8): tt = t(sec); return rng.standard_normal(len(tt)) * np.exp(-decay*tt)
def lp(x, k=20): return np.convolve(x, np.ones(k)/k, mode="same")
def add(buf, at, x):
    s = int(at*SR); e = min(len(buf), s+len(x))
    if s < len(buf): buf[s:e] += x[:e-s]
def seq(notes, step, kind="square", decay=6, length=None, duty=.5):
    buf = np.zeros(int(SR*(length or step*len(notes)+.4)))
    for i, n in enumerate(notes):
        if n is not None: add(buf, i*step, tone(hz(n), step*1.6, kind, decay, duty=duty))
    return buf

# ---------------- Código Mortal (spy / neon)
M = "MortalCode/Resources/MortalAudio"
BPM = 96; B = 60/BPM; BAR = 4*B; bars = 16
music = np.zeros(int(SR*BAR*bars))
prog = [45, 45, 41, 43, 45, 45, 41, 40, 45, 45, 41, 43, 38, 40, 41, 40]   # A minor, tense
for bar, root in enumerate(prog):
    t0 = bar*BAR
    for e in range(8):   # pulsing bass
        add(music, t0+e*B/2, tone(hz(root-12+(7 if e % 4 == 3 else 0)), B/2*.9, "saw", 5) * .35)
    for b in range(4):
        add(music, t0+b*B, tone(52, .2, "sine", 16) * .9)
        if b % 2: add(music, t0+b*B, lp(noise(.15, 22), 3) * .3)
    for s in range(16): add(music, t0+s*B/4, noise(.02, 120) * (.12 if s % 2 else .06))
    for s in range(8):   # glassy arpeggio
        n = [root+24, root+27, root+31, root+34][s % 4]
        add(music, t0+s*B/2+B/4, tone(hz(n), B*.4, "tri", 7) * .12)
for ph in range(0, bars, 4):  # sparse lead
    for i, (beat, n) in enumerate([(0, 69), (1.5, 72), (3, 71), (6, 67), (8, 69), (9.5, 76), (11, 74), (14, 72)]):
        add(music, ph*BAR + beat*B, tone(hz(n), B*1.2, "square", 3, duty=.3) * .1)
save(M, "music", music, .7)
save(M, "beep", tone(hz(76), .15, "square", 12, duty=.25))
save(M, "go", tone(hz(88), .45, "square", 4, duty=.25) + tone(hz(81), .45, "square", 4) * .4)
save(M, "ping", seq([84, 91], .07, "sine", 8))
save(M, "key", tone(hz(96), .05, "square", 40, duty=.25), .45)
save(M, "access", seq([72, 76, 79, 84], .06, "square", 7, duty=.25))
save(M, "denied", tone(110, .4, "saw", 4) + tone(116, .4, "saw", 4))
save(M, "shield_on", tone(300, .5, "sine", 3, f2=900) + tone(600, .5, "tri", 4, f2=1800)*.3)
save(M, "shield_off", tone(900, .35, "sine", 5, f2=250))
z = noise(.7, 5) * (np.sin(2*np.pi*35*t(.7)) > 0) + tone(1800, .7, "square", 6, f2=120) * .5
save(M, "eliminated", z)
al = np.zeros(int(SR*1.6))
for i in range(4): add(al, i*.4, tone(880 if i % 2 == 0 else 660, .3, "square", 3, duty=.5))
save(M, "alarm", al, .6)
save(M, "pickup", seq([79, 86, 91], .05, "sine", 9))
save(M, "scan", tone(200, .8, "sine", 2, f2=2400) * (.6 + .4*np.sin(2*np.pi*18*t(.8))))
save(M, "speed", tone(150, .6, "saw", 3, f2=900) * .6 + noise(.6, 6) * .2)
save(M, "win", seq([69, 72, 76, 81, None, 84], .14, "square", 4, duty=.25))
save(M, "lose", seq([69, 68, 67, 64], .22, "tri", 3))

# ---------------- Invasión Zombi power-ups
Z = "ZombieSurvival/Resources/SurvivalAudio"
mk = seq([72, 79, 84], .07, "sine", 6); add(mk, 0, seq([76, 83, 88], .07, "tri", 6) * .4); save(Z, "zmedkit", mk)
save(Z, "zrage", lp(noise(.9, 3), 4) * .6 + tone(80, .9, "saw", 2, f2=160) * .8)
bomb = lp(noise(1.4, 3.5), 6) * 1.2 + tone(60, 1.4, "sine", 3, f2=30) * 1.4
save(Z, "zbomb", bomb)
save(Z, "zdrop", seq([88, 95], .05, "sine", 12), .5)

# ---------------- Sala de Escape
E = "EscapeRoom/Resources/EscapeAudio"
save(E, "record", seq([72, 76, 79, 84, 88], .08, "square", 6, duty=.25))
ch = lp(noise(1.2, 2), 3) * .5; add(ch, 0, seq([79, 84, 88], .1, "tri", 5) * .6); save(E, "cheer", ch)

# ---------------- Arena del Caos kraken
A = "PirateArena/Resources/PirateAudio/v2"
tt = t(1.6)
bub = lp(noise(1.6, 1.2), 8) * (.5 + .5*np.sin(2*np.pi*7*tt)) + tone(55, 1.6, "sine", .8, f2=90) * .9
save(A, "kraken_rise", bub)
slam = lp(noise(.9, 5), 5) * 1.3 + tone(70, .9, "sine", 4, f2=35) * 1.5
save(A, "kraken_slam", slam)
print("ok")
