"""Audio original sintetizado (sin muestras) para Rodillos del Calabozo (2026-11-17).
Uso: python Tools/generate_rodillos_audio.py [carpeta_salida]
Salida por defecto: Assets/003_Scripts/Rodillos/Resources/RodillosAudio/*.wav"""
import os, sys, wave
import numpy as np
SR = 22050
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "Assets", "003_Scripts", "Rodillos", "Resources", "RodillosAudio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(67)

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
save("jump", M(tone(320, .22, "square", 9, f2=760, duty=.3) * .6, tone(640, .18, "sine", 10, f2=1300) * .4))
save("duck", tone(500, .16, "tri", 12, f2=230) * .8)
save("land", M(lp(noise(.12, 30), 30) * .7, tone(110, .1, "sine", 25) * .5))
b = M(tone(180, .35, "square", 8, f2=70, duty=.4) * .5, lp(noise(.3, 14), 6) * .5, tone(900, .12, "sine", 30, f2=300) * .4)
save("hit", b)
save("bonk", M(tone(260, .25, "sine", 12, f2=120), tone(520, .1, "tri", 25) * .4))
fall = np.zeros(int(SR * 1.3)); add(fall, 0, tone(900, 1.1, "sine", 1.5, f2=180) * .5); add(fall, .95, lp(noise(.35, 8), 4) * .9)
save("eliminated", fall)
splash = M(lp(noise(.9, 4), 5) * .7, tone(80, .8, "sine", 4) * .6)
for k in range(6): add(splash, .08 + k * .09, tone(rng.uniform(300, 700), .12, "sine", 18, f2=rng.uniform(800, 1400)) * .25)
save("lava", splash)
cr = np.zeros(int(SR * 1.1))
for k in range(14): add(cr, rng.uniform(0, .8), lp(noise(rng.uniform(.06, .2), 20), int(rng.uniform(3, 12))) * rng.uniform(.3, .8))
add(cr, .55, tone(70, .5, "sine", 5) * .8)
save("crumble", cr)
sh = np.zeros(int(SR * .6))
for k in range(10): add(sh, k * .055, lp(noise(.05, 40), 4) * .5)
save("shake", sh)
cl = np.zeros(int(SR * .9))
for k in range(3): add(cl, k * .22, M(tone(1200, .18, "square", 14, duty=.25) * .4, tone(600, .2, "saw", 10) * .3))
save("reverse", cl)
al = np.zeros(int(SR * 1.4))
for k in range(4): add(al, k * .3, tone(880 if k % 2 == 0 else 660, .26, "square", 4, duty=.5) * .5)
save("alarm", al)
save("beep", tone(880, .16, "square", 10, duty=.5) * .6)
save("go", tone(hz(84), .5, "square", 4, duty=.5) * .5 + tone(hz(79), .5, "square", 4, duty=.25) * .3)
w = np.zeros(int(SR * 1.6))
for i, n in enumerate([72, 76, 79, 84, 79, 84, 88]): add(w, i * .13, tone(hz(n), .3, "square", 7, duty=.5) * .4 + tone(hz(n - 12), .3, "tri", 7) * .3)
save("win", w)
l = np.zeros(int(SR * 1.4))
for i, n in enumerate([67, 63, 60, 55]): add(l, i * .22, tone(hz(n), .38, "tri", 5) * .6)
save("lose", l)
# Zumbido de los rodillos (bucle de 1 s)
tt = t(1.0); hum = np.sin(2*np.pi*55*tt) * .5 + np.sin(2*np.pi*110*tt + .4) * .25 + lp(rng.standard_normal(len(tt)), 40) * .6
hum *= .75 + .25 * np.sin(2*np.pi*6*tt)
save("rumble", hum, .6)
lava_loop = lp(rng.standard_normal(len(t(2.0))), 60) * 1.0
for k in range(10): add(lava_loop, rng.uniform(0, 1.8), tone(rng.uniform(90, 200), .2, "sine", 12, f2=rng.uniform(40, 80)) * .6)
save("lava_loop", lava_loop, .5)

# Música: mazmorra con prisa (re menor, 132 bpm, 16 compases en bucle)
bpm = 132; beat = 60 / bpm; bars = 16; L = beat * 4 * bars
mus = np.zeros(int(SR * L) + SR)
prog = [50, 50, 46, 48, 50, 50, 53, 52, 50, 50, 46, 48, 45, 45, 46, 49]  # raíces
for bar, root in enumerate(prog):
    for k in range(8):
        n = root - 12 + (0, 12, 7, 12, 0, 12, 7, 10)[k]
        add(mus, (bar * 4 + k * .5) * beat, tone(hz(n), beat * .5, "square", 9, duty=.3) * .22)
    for k in range(4):
        add(mus, (bar * 4 + k) * beat, lp(noise(.12, 30), 3) * (.35 if k % 2 else .2))
        if k % 2 == 0: add(mus, (bar * 4 + k) * beat, tone(60, .18, "sine", 18, f2=35) * .6)
mel = [74, None, 72, 74, 77, None, 76, 74, 72, None, 69, 70, 72, None, None, None,
       74, None, 72, 74, 77, None, 79, 81, 79, 77, 76, None, 74, None, None, None,
       77, 76, 74, None, 72, 74, 70, None, 72, 70, 69, None, 67, None, 69, None,
       74, 76, 77, 79, 81, None, 79, 77, 76, 74, 73, None, 74, None, None, None]
for i, n in enumerate(mel * 2):
    if n is not None: add(mus, i * beat * .5 * 2, tone(hz(n), beat * .9, "tri", 3.5) * .3 + tone(hz(n), beat * .9, "square", 5, duty=.12) * .08)
mus = mus[:int(SR * L)]
fade = int(SR * .02); mus[:fade] *= np.linspace(0, 1, fade); mus[-fade:] *= np.linspace(1, 0, fade)
save("music", mus, .7)
print("OK", sorted(os.listdir(OUT)))
