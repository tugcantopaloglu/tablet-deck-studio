"""Create original synthesized demo sounds, not sampled recordings."""
from pathlib import Path
import math
import random
import struct
import wave

destination = Path(__file__).resolve().parents[1] / "companion" / "Sounds"
destination.mkdir(parents=True, exist_ok=True)
rate = 44100

def save(name, samples):
    peak = max(abs(v) for v in samples) or 1
    pcm = b"".join(struct.pack("<h", round(v / peak * 14000)) for v in samples)
    with wave.open(str(destination / name), "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(rate)
        stream.writeframes(pcm)

horn = []
for index in range(int(rate * 1.25)):
    t = index / rate
    envelope = min(1, t / .035) * min(1, (1.25 - t) / .12)
    tone = sum(math.sin(2 * math.pi * frequency * harmonic * t) / harmonic
               for frequency in (349.23, 440, 523.25) for harmonic in (1, 2, 3))
    horn.append(tone * envelope * (.94 + .06 * math.sin(2 * math.pi * 19 * t)))
save("air-horn.wav", horn)

rng = random.Random(73591)
applause = [0.] * (rate * 3)
for clap in range(66):
    start = int(rng.uniform(.03, 2.65) * rate)
    duration = int(rng.uniform(.025, .085) * rate)
    gain = rng.uniform(.2, .8)
    previous = 0.
    for index in range(duration):
        noise = rng.uniform(-1, 1)
        transient = (noise - previous * .65) * math.exp(-index / (duration * .22))
        previous = noise
        applause[start + index] += gain * transient
save("applause.wav", applause)
print("Generated two original synthesized demo sounds.")
