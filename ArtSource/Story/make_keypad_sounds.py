"""Keypad sounds for KeypadLock: a key beep, a wrong-code buzz, and an unlock chime + clunk.

    python -I ArtSource/Story/make_keypad_sounds.py  ->  Assets/sfx/keypad *.wav
"""
import wave
from pathlib import Path

import numpy as np

OUT = Path(__file__).parent.parent.parent / "Assets/sfx"
RATE = 44100


def t(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def tone(freq, seconds, square=0.0, decay=6.0):
    x = t(seconds)
    s = np.sin(2 * np.pi * freq * x)
    s = (1 - square) * s + square * np.sign(s)
    env = np.exp(-decay * x) * np.minimum(1, x / 0.004)  # no click at the start
    return s * env


def silence(seconds):
    return np.zeros(int(RATE * seconds))


def save(name, s, volume):
    s = s / np.max(np.abs(s)) * volume
    with wave.open(str(OUT / name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes((s * 32767).astype(np.int16).tobytes())


rng = np.random.default_rng(7)
# A cheap keypad's short beep.
save("keypad key.wav", tone(1500, 0.08, square=0.35, decay=25) + 0.3 * tone(3000, 0.08, decay=40), 0.5)
# Two low, harsh buzzes: no.
buzz = tone(160, 0.16, square=0.9, decay=2) + 0.5 * tone(240, 0.16, square=0.9, decay=2)
save("keypad wrong.wav", np.concatenate([buzz, silence(0.06), buzz]), 0.6)
# Two rising beeps, then the heavy bolt sliding back.
noise = rng.standard_normal(int(RATE * 0.25))
clunk = np.convolve(noise, np.ones(40) / 40, mode="same") * np.exp(-14 * t(0.25)) + 0.8 * tone(70, 0.25, decay=12)
save("keypad unlock.wav", np.concatenate([tone(988, 0.1, 0.3, 12), silence(0.03), tone(1319, 0.16, 0.3, 9), silence(0.1), clunk]), 0.7)
print("written to", OUT)
