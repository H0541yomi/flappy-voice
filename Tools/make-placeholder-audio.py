#!/usr/bin/env python3
"""Synthesises the placeholder audio in Flappy Voice/Assets/Audio.

These are stand-ins, not final audio: the point is that every hook in the game
has something audible wired to it, so the mix can be judged and the real files
can be dropped in over the top without touching code. Re-run this to change
them; nothing here is hand-edited.

    python3 Tools/make-placeholder-audio.py

Stdlib only, by necessity - this machine has no ffmpeg, no numpy and no audio
tooling of any kind.
"""

import math
import os
import random
import struct
import wave

RATE = 44100
OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       "Flappy Voice", "Assets", "Audio")


def midi_hz(midi):
    return 440.0 * (2.0 ** ((midi - 69) / 12.0))


def envelope(i, count, attack, decay, sustain_level, release):
    """Linear ADSR in seconds, evaluated per sample."""
    t = i / RATE
    total = count / RATE
    if t < attack:
        return t / attack if attack > 0 else 1.0
    if t < attack + decay:
        k = (t - attack) / decay if decay > 0 else 1.0
        return 1.0 + k * (sustain_level - 1.0)
    if t > total - release:
        k = (total - t) / release if release > 0 else 0.0
        return max(0.0, k) * sustain_level
    return sustain_level


def triangle(phase):
    # Softer than a square and far softer than a saw: placeholder music has to
    # survive being looped for minutes without becoming abrasive.
    p = phase % 1.0
    return 4.0 * abs(p - 0.5) - 1.0


def sine(phase):
    return math.sin(2.0 * math.pi * phase)


def add_tone(buf, start_sec, dur_sec, hz, gain, wave_fn=triangle,
             attack=0.01, decay=0.06, sustain=0.7, release=0.05, detune=0.0):
    start = int(start_sec * RATE)
    count = int(dur_sec * RATE)
    for i in range(count):
        index = start + i
        if index >= len(buf):
            break
        phase = hz * (i / RATE)
        value = wave_fn(phase)
        if detune:
            value = 0.5 * value + 0.5 * wave_fn(phase * (1.0 + detune))
        buf[index] += value * gain * envelope(i, count, attack, decay, sustain, release)


def add_noise(buf, start_sec, dur_sec, gain, rng, low_pass=0.35):
    start = int(start_sec * RATE)
    count = int(dur_sec * RATE)
    state = 0.0
    for i in range(count):
        index = start + i
        if index >= len(buf):
            break
        # One-pole low pass, so the burst reads as a thud rather than a hiss.
        state += low_pass * ((rng.random() * 2.0 - 1.0) - state)
        buf[index] += state * gain * envelope(i, count, 0.001, 0.12, 0.25, 0.12)


def add_sweep(buf, start_sec, dur_sec, hz_from, hz_to, gain, wave_fn=triangle):
    start = int(start_sec * RATE)
    count = int(dur_sec * RATE)
    phase = 0.0
    for i in range(count):
        index = start + i
        if index >= len(buf):
            break
        k = i / max(1, count - 1)
        hz = hz_from * ((hz_to / hz_from) ** k)
        phase += hz / RATE
        buf[index] += wave_fn(phase) * gain * envelope(i, count, 0.004, 0.1, 0.55, 0.25)


def write_wav(name, buf, peak=0.89):
    high = max(abs(v) for v in buf) or 1.0
    scale = (peak / high) * 32767.0
    frames = b"".join(struct.pack("<h", int(max(-32768, min(32767, v * scale)))) for v in buf)

    path = os.path.join(OUT_DIR, name)
    with wave.open(path, "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(RATE)
        handle.writeframes(frames)
    print(f"{name}: {len(buf) / RATE:.2f}s, {os.path.getsize(path) // 1024} KiB")


def buffer(seconds):
    return [0.0] * int(seconds * RATE)


# ---------------------------------------------------------------- score point

def score():
    """Two rising blips. Short enough to fire on consecutive pipes at speed."""
    buf = buffer(0.20)
    add_tone(buf, 0.00, 0.07, midi_hz(79), 0.55, sine, attack=0.002, decay=0.03,
             sustain=0.5, release=0.03)
    add_tone(buf, 0.06, 0.13, midi_hz(84), 0.60, sine, attack=0.002, decay=0.05,
             sustain=0.45, release=0.07)
    # A touch of the octave above on the second blip for sparkle.
    add_tone(buf, 0.06, 0.10, midi_hz(96), 0.16, sine, attack=0.002, decay=0.04,
             sustain=0.3, release=0.05)
    write_wav("sfx_score.wav", buf)


# --------------------------------------------------------------------- crash

def crash():
    """Detuned descending honk plus a filtered thump - a trumpet being hit."""
    rng = random.Random(7)
    buf = buffer(0.55)
    add_sweep(buf, 0.0, 0.42, midi_hz(59), midi_hz(40), 0.5)
    add_tone(buf, 0.0, 0.30, midi_hz(47), 0.35, triangle, attack=0.004, decay=0.12,
             sustain=0.4, release=0.14, detune=0.02)
    add_noise(buf, 0.0, 0.26, 0.45, rng)
    write_wav("sfx_crash.wav", buf)


# ------------------------------------------------------- music: start + game

def bgm_game():
    """Four bars at 104 BPM, C major pentatonic. Loops on the bar.

    Used for both the start screen and the run, so it must be pleasant at zero
    tension: no drums, no build, nothing that wants to resolve.
    """
    bpm = 104.0
    beat = 60.0 / bpm
    bars = 4
    total = bars * 4 * beat
    buf = buffer(total)

    # Bass, one note per bar: C - A - F - G
    for bar, midi in enumerate((36, 33, 41, 43)):
        add_tone(buf, bar * 4 * beat, 4 * beat, midi_hz(midi), 0.34, triangle,
                 attack=0.02, decay=0.5, sustain=0.55, release=0.3)

    # Pentatonic arpeggio over the top, eighth notes, phrase per bar.
    phrases = (
        (72, 76, 79, 76, 84, 79, 76, 72),
        (69, 72, 76, 72, 81, 76, 72, 69),
        (77, 81, 84, 81, 88, 84, 81, 77),
        (79, 83, 86, 83, 79, 76, 72, 74),
    )
    for bar, phrase in enumerate(phrases):
        for step, midi in enumerate(phrase):
            at = (bar * 4 + step * 0.5) * beat
            add_tone(buf, at, beat * 0.46, midi_hz(midi), 0.20, sine,
                     attack=0.006, decay=0.10, sustain=0.42, release=0.10)

    # Sparse high counter-melody every other bar, so the loop does not feel
    # like a metronome once it has gone round three times.
    for bar, midi in ((1, 88), (3, 91)):
        add_tone(buf, (bar * 4 + 2) * beat, beat * 1.6, midi_hz(midi), 0.10, sine,
                 attack=0.05, decay=0.4, sustain=0.35, release=0.4)

    write_wav("bgm_game.wav", buf)


# --------------------------------------------------------- music: game over

def bgm_gameover():
    """Two slow bars, A minor, falling. Loops, but is meant to be short-lived."""
    bpm = 76.0
    beat = 60.0 / bpm
    total = 2 * 4 * beat
    buf = buffer(total)

    for bar, root in enumerate((33, 28)):
        add_tone(buf, bar * 4 * beat, 4 * beat, midi_hz(root), 0.32, triangle,
                 attack=0.03, decay=0.6, sustain=0.5, release=0.5)

    # Am triad falling into E: the cadence does the "you lost" work.
    for at, midi, dur in (
        (0.0, 72, 1.6), (1.5, 69, 1.4), (3.0, 64, 1.0),
        (4.0, 68, 1.8), (5.6, 64, 2.2),
    ):
        add_tone(buf, at * beat, dur * beat, midi_hz(midi), 0.19, sine,
                 attack=0.02, decay=0.35, sustain=0.4, release=0.45)

    write_wav("bgm_gameover.wav", buf)


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    score()
    crash()
    bgm_game()
    bgm_gameover()
