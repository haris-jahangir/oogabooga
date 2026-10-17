"""Two original piano pieces for the endings, synthesised from scratch (no samples, no licences needed).

    python -I ArtSource/Story/make_piano.py  ->  Assets/music/ending_light_piano.ogg, ending_dark_piano.ogg

Each piece is one seamless loop: notes and reverb that ring past the end wrap round to the start.
"""
import subprocess
import tempfile
import wave
from pathlib import Path

import numpy as np

SR = 44100
OUT = Path(__file__).parent.parent.parent / "Assets/music"
rng = np.random.default_rng(1)
NAMES = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def freq(note):
    name, octave = note[:-1], int(note[-1])
    return 440.0 * 2 ** ((NAMES[name] + 12 * (octave + 1) - 69) / 12)


def piano(f, seconds, vel, dark):
    """One struck piano note: two detuned strings, stretched overtones, two-stage decay, a hammer knock."""
    t = np.arange(int(seconds * SR)) / SR
    out = np.zeros_like(t)
    bright = 0.55 + 0.45 * vel - (0.25 if dark else 0)
    sustain = 1.4 * (261.6 / f) ** 0.45                      # low notes ring longer
    for k in range(1, 11):
        fk = k * f * np.sqrt(1 + 0.00035 * k * k)
        if fk > 12000:
            break
        amp = vel * bright ** (k - 1) / k ** 0.9
        rate = (1.0 + 0.45 * k) / sustain
        env = 0.65 * np.exp(-t * rate * 3.2) + 0.35 * np.exp(-t * rate * 0.55)
        for detune in (-0.12, 0.12):                           # two strings, slightly apart: a slow shimmer
            out += amp * env * np.sin(2 * np.pi * (fk + detune) * t + rng.uniform(0, 6.28))
    out *= np.minimum(1, t / 0.003)                            # 3 ms hammer attack
    knock = rng.standard_normal(min(len(t), int(0.02 * SR))) * np.exp(-np.arange(min(len(t), int(0.02 * SR))) / (0.004 * SR))
    out[:len(knock)] += knock * 0.02 * vel
    return out


def render(events, length, dark, wet, room):
    """events: (start seconds, note, duration, velocity, pan). Loops: everything wraps at `length`."""
    n = int(length * SR)
    mix = np.zeros((2, n))
    for start, note, dur, vel, pan in events:
        s = piano(freq(note), dur + 2.5, vel, dark)
        s[int(dur * SR):] *= np.exp(-np.arange(len(s) - int(dur * SR)) / (0.35 * SR))  # damper after the pedal lifts
        idx = (int(start * SR) + np.arange(len(s))) % n
        np.add.at(mix[0], idx, s * (1 - pan) * 2 ** 0.5 / 2 * 1.2)
        np.add.at(mix[1], idx, s * (1 + pan) * 2 ** 0.5 / 2 * 1.2)
    # Hall reverb: circular convolution with decaying noise, so the tail wraps into the loop's start.
    ir_len = int(room * SR)
    out = np.zeros_like(mix)
    for c in range(2):
        ir = rng.standard_normal(ir_len) * np.exp(-np.arange(ir_len) / (room / 5 * SR))
        ir = np.convolve(ir, np.ones(30) / 30, mode="same")      # soften the highs
        ir /= np.sqrt((ir ** 2).sum())
        pad = np.zeros(n)
        pad[:ir_len] = ir[:n]
        wetc = np.real(np.fft.ifft(np.fft.fft(mix[c]) * np.fft.fft(pad)))
        out[c] = mix[c] * (1 - wet) + wetc * wet * 2.2
    return out / np.abs(out).max() * 0.8


def light():
    bpm = 66
    beat = 60 / bpm
    bar = 4 * beat
    chords = [["C2", "G2", "C3", "E3", "G3", "E3", "C3", "G2"], ["A1", "E2", "A2", "C3", "E3", "C3", "A2", "E2"],
              ["F1", "C2", "F2", "A2", "C3", "A2", "F2", "C2"], ["G1", "D2", "G2", "B2", "D3", "B2", "G2", "D2"],
              ["C2", "G2", "C3", "E3", "G3", "E3", "C3", "G2"], ["E2", "B2", "E3", "G3", "B3", "G3", "E3", "B2"],
              ["F1", "C2", "F2", "A2", "C3", "A2", "F2", "C2"], ["G1", "D2", "G2", "C3", "D3", "B2", "G2", "D2"]]
    melody = [[("E5", 2), ("G5", 1), ("E5", 1)], [("C5", 2), ("E5", 2)], [("A5", 2), ("G5", 1), ("F5", 1)], [("D5", 3), ("G4", 1)],
              [("E5", 2), ("G5", 1), ("C6", 1)], [("B5", 2), ("G5", 2)], [("A5", 1), ("G5", 1), ("F5", 1), ("E5", 1)], [("D5", 2), ("G5", 2)]]
    ev = []
    for b, (arp, mel) in enumerate(zip(chords, melody)):
        t0 = b * bar
        for i, note in enumerate(arp):                          # rolling eighth notes, softly
            ev.append((t0 + i * beat / 2, note, bar - i * beat / 2, 0.32 + 0.06 * (i == 0), -0.3))
        t = t0
        for note, beats in mel:
            ev.append((t + 0.015, note, beats * beat, 0.42, 0.25))
            t += beats * beat
    return render(ev, 8 * bar, dark=False, wet=0.32, room=2.6)


def dark():
    bpm = 50
    beat = 60 / bpm
    bar = 4 * beat
    bass = [("D1", "D2"), ("Bb0", "Bb1"), ("G1", "G2"), ("A1", "A2"), ("D1", "D2"), ("Bb0", "Bb1"), ("Gb1", "Gb2"), ("A1", "A2")]
    mid = [["F3", "A3"], ["F3", "D4"], ["Bb3", "D4"], ["C#4", "E4"], ["F3", "A3"], ["F3", "D4"], ["A3", "Eb4"], ["C#4", "G4"]]
    high = [[("A5", 0, 2), ("Bb5", 2.5, 1.5)], [("F5", 1, 3)], [("D5", 0, 2), ("Eb5", 2, 2)], [("C#5", 1, 3)],
            [("A5", 0, 1.5), ("Bb5", 1.5, 1), ("A5", 2.5, 1.5)], [("D6", 1, 3)], [("Eb5", 0, 2), ("D5", 2, 2)], [("C#5", 0, 2), ("E5", 2, 2)]]
    ev = []
    for b in range(8):
        t0 = b * bar
        lo, oc = bass[b]
        ev.append((t0, lo, bar, 0.55, -0.2))                     # the low toll on the downbeat
        ev.append((t0 + 0.01, oc, bar, 0.38, -0.2))
        for i, note in enumerate(mid[b]):
            ev.append((t0 + beat * (1 + i), note, bar - beat, 0.24, 0.0))
        for note, at, dur in high[b]:
            ev.append((t0 + at * beat, note, dur * beat, 0.3, 0.3))
    return render(ev, 8 * bar, dark=True, wet=0.45, room=3.6)


def save(stereo, name):
    with tempfile.TemporaryDirectory() as d:
        wav = Path(d) / "x.wav"
        with wave.open(str(wav), "wb") as w:
            w.setnchannels(2)
            w.setsampwidth(2)
            w.setframerate(SR)
            w.writeframes((stereo.T * 32767).astype(np.int16).tobytes())
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-c:a", "libvorbis", "-q:a", "5", str(OUT / name)], check=True)
    print("wrote", OUT / name, f"{stereo.shape[1] / SR:.1f}s")


save(light(), "ending_light_piano.ogg")
save(dark(), "ending_dark_piano.ogg")
