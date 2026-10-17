"""Night on the dock, for the start of the ending: a low, slowly shifting drone in D minor,
wind, and water lapping at the pier. One seamless loop, synthesised (no samples).

    python -I ArtSource/Story/make_ambience.py  ->  Assets/music/ending_ambience.ogg
"""
import subprocess
import tempfile
import wave
from pathlib import Path

import numpy as np

SR, LENGTH = 44100, 48.0
N = int(SR * LENGTH)
OUT = Path(__file__).parent.parent.parent / "Assets/music/ending_ambience.ogg"
rng = np.random.default_rng(7)
t = np.arange(N) / SR


def loop_hz(hz):
    """Nearest frequency that fits a whole number of cycles in the loop, so it never clicks."""
    return max(1, round(hz * LENGTH)) / LENGTH


def lfo(cycles_per_loop, phase=0.0):
    return 0.5 + 0.5 * np.sin(2 * np.pi * cycles_per_loop * t / LENGTH + phase)


def coloured_noise(lo, hi):
    """Noise band-passed between lo and hi Hz, periodic over the loop."""
    spec = np.fft.rfft(rng.standard_normal(N))
    f = np.fft.rfftfreq(N, 1 / SR)
    spec *= np.exp(-((np.log(np.maximum(f, 1)) - np.log(np.sqrt(lo * hi))) ** 2) / (2 * np.log(hi / lo) ** 2 / 4))
    x = np.fft.irfft(spec, N)
    return x / np.abs(x).max()


# The drone: D minor (D2 A2 D3 F3), each voice a few detuned partials breathing in and out.
pad = np.zeros(N)
for i, (note_hz, weight) in enumerate([(73.42, 1.0), (110.0, 0.7), (146.83, 0.55), (174.61, 0.4), (220.0, 0.25)]):
    voice = np.zeros(N)
    for k, amp in ((1, 1.0), (2, 0.35), (3, 0.12)):
        for det in (-0.15, 0.0, 0.15):
            voice += amp * np.sin(2 * np.pi * loop_hz(note_hz * k + det) * t + rng.uniform(0, 6.28))
    pad += weight * voice * (0.55 + 0.45 * lfo(2 + i, rng.uniform(0, 6.28)))
pad /= np.abs(pad).max()

wind = coloured_noise(250, 1400) * (0.35 + 0.65 * lfo(3, 1.0) * lfo(5, 2.0))
water = np.zeros(N)
for start in np.arange(0, LENGTH, 2.6) + rng.uniform(0, 1.2, int(LENGTH / 2.6) + 1)[: len(np.arange(0, LENGTH, 2.6))]:
    s = int(start * SR) % N
    dur = int(rng.uniform(0.5, 0.9) * SR)
    idx = (s + np.arange(dur)) % N
    env = np.sin(np.pi * np.arange(dur) / dur) ** 2
    water[idx] += env * rng.uniform(0.5, 1.0)
water *= coloured_noise(120, 700)

mix = pad * 0.55 + wind * 0.22 + water * 0.18
left = mix + 0.08 * np.roll(wind, int(0.013 * SR))
right = mix + 0.08 * np.roll(water, int(0.009 * SR))
stereo = np.stack([left, right])
stereo = stereo / np.abs(stereo).max() * 0.7

with tempfile.TemporaryDirectory() as d:
    wav = Path(d) / "x.wav"
    with wave.open(str(wav), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((stereo.T * 32767).astype(np.int16).tobytes())
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-c:a", "libvorbis", "-q:a", "4", str(OUT)], check=True)
print("wrote", OUT)
