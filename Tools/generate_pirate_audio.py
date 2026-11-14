"""Generate original, small arcade sound effects for the pirate minigame."""
from pathlib import Path
import math
import random
import struct
import wave

RATE = 22050
OUT = Path(__file__).resolve().parents[1] / "Assets/003_Scripts/PirateArena/Resources/PirateAudio"
OUT.mkdir(parents=True, exist_ok=True)


def render(name, duration, pitch, noise, decay, pulse=0):
    rng = random.Random(name)
    count = int(RATE * duration)
    phase = 0.0
    low_noise = 0.0
    samples = []
    for index in range(count):
        t = index / count
        freq = pitch[0] * (pitch[1] / pitch[0]) ** t
        phase += 2 * math.pi * freq / RATE
        low_noise = low_noise * .84 + rng.uniform(-1, 1) * .16
        grain = low_noise if name in ("splash", "shark_leap", "throw") else rng.uniform(-1, 1)
        gate = 1.0 if not pulse else (.45 + .55 * max(0, math.sin(t * math.pi * pulse)))
        envelope = (1 - math.exp(-t * 90)) * math.exp(-decay * t) * gate
        signal = (math.sin(phase) * (1 - noise) + grain * noise) * envelope
        samples.append(max(-1, min(1, signal * .78)))
    peak = max(abs(v) for v in samples) or 1
    data = b"".join(struct.pack("<h", int(v / peak * 22000)) for v in samples)
    with wave.open(str(OUT / f"{name}.wav"), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(data)


SOUNDS = {
    "step": (.12, (135, 75), .55, 7, 0),
    "grab": (.18, (380, 165), .30, 4, 0),
    "push": (.28, (125, 55), .60, 4, 0),
    "throw": (.36, (540, 135), .80, 2.8, 0),
    "jump": (.24, (220, 440), .18, 3.5, 0),
    "land": (.26, (115, 48), .70, 4, 0),
    "impact": (.31, (150, 62), .77, 3.6, 0),
    "cannon_warn": (.56, (720, 560), .16, 1.8, 7),
    "cannon_shot": (.45, (110, 45), .72, 2.5, 0),
    "cannon_blast": (.85, (94, 28), .83, 2.1, 0),
    "shark_warn": (.48, (290, 120), .45, 1.8, 5),
    "shark_leap": (.62, (170, 320), .84, 2.3, 0),
    "splash": (.86, (230, 80), .96, 2.6, 0),
}
for label, arguments in SOUNDS.items():
    render(label, *arguments)
print(f"Generated {len(SOUNDS)} clips in {OUT}")
