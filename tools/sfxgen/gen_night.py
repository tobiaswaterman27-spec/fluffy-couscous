"""Sounds for the longer game: the standby generator (pull cord, coughs, start, running loop) and the fire door's
alarm bell. Own seed, so rendering these never changes any other sound. Run: python3 gen_night.py"""
import os
import numpy as np
from dsp import *
from gen_steps import thump

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "AfterHours", "Resources", "Audio")


def cord(rng, dur=0.55):
    """The starter rope ripped out: a rising nylon zip, the recoil ratchet, the rope snapping back in."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    zip_ = bp(noise(dur, rng), 900, 5000) * np.sin(np.pi * np.clip(t / (dur * 0.7), 0, 1)) ** 0.5
    zip_ *= (t < dur * 0.7)
    x = silence(dur + 0.3)
    place(x, (zip_ * 0.35).astype(np.float32), 0)
    for k in range(int(dur * 60)):                              # ratchet pawls
        place(x, strike_object(rng, "steel", 0.15, 0.9, 0.12, 0.02, damp=0.2), k / 60 * 0.7)
    place(x, strike_object(rng, "plastic", 0.5, 0.7, 0.5, 0.08), dur * 0.75)  # handle slaps back on its housing
    return x


def compressions(rng, count, start_rate, end_rate, level=1.0, fire=0.0):
    """The engine turned over: one chug per compression; 'fire' = chance a stroke actually catches (a bang)."""
    x = silence(count / min(start_rate, end_rate) + 0.6)
    at = 0.0
    for k in range(count):
        r = start_rate + (end_rate - start_rate) * k / max(1, count - 1)
        chug = thump(rng, rng.uniform(55, 75), 0.12, 0.6 * level)
        place(x, chug, at)
        if rng.random() < fire:
            bang = lp(noise(0.08, rng), 1800) * env_exp(int(0.08 * SR), 0.015) * 1.4 * level
            place(x, bang.astype(np.float32), at + 0.01)
            place(x, strike_object(rng, "steel", 1.2, 0.5, 0.4 * level, 0.3, damp=0.4), at + 0.01)
        at += 1.0 / r
    return x


def engine(rng, dur, rpm=1500, cyl=4, level=1.0):
    """A diesel generator running: firing pulses with jitter, block rattle, exhaust roar, cooling fan."""
    n = int(dur * SR)
    f = rpm / 60 * cyl / 2                                       # firing frequency (50 Hz)
    x = np.zeros(n, np.float32)
    period = SR / f
    pos = 0.0
    k = 0
    pulse = (lp(noise(0.02, rng), 900) * env_exp(int(0.02 * SR), 0.004)).astype(np.float32)
    while pos < n - len(pulse):
        i = int(pos)
        g = 1.0 + 0.25 * np.sin(2 * np.pi * k / cyl) + rng.normal(0, 0.08)  # cylinders differ a little
        x[i:i + len(pulse)] += pulse * g
        pos += period * (1 + rng.normal(0, 0.006))
        k += 1
    body = peak(x, f * 2, 3, 2.0) + peak(x, f, 4, 3.0)
    rattle = bp(noise(dur, rng), 1500, 6000) * 0.05 * (0.6 + 0.4 * np.abs(np.sin(np.pi * f * np.arange(n) / SR)))
    exhaust = lp(brown(dur, rng), 220) * 0.6
    fan = bp(noise(dur, rng), 300, 1200) * 0.06
    whine = 0.02 * np.sin(2 * np.pi * 1210 * np.arange(n) / SR)  # alternator
    y = sat((body * 0.7 + rattle + exhaust + fan + whine) * level, 1.6)
    return y.astype(np.float32)


def gen_pull(rng):
    """No fuel / no spark: the cord, the engine turning over a few times, nothing."""
    x = silence(1.6)
    place(x, cord(rng), 0)
    place(x, compressions(rng, rng.integers(3, 6), 9, 4, 0.8), 0.08)
    return fade(reverb(soften(x, 8000), 1.2, 0.6, 0.25, 0.3, rng), 0.0005, 0.2)


def gen_cough(rng):
    """Fuelled but cold: it catches on a couple of strokes, stumbles, and dies with a shudder."""
    x = silence(2.6)
    place(x, cord(rng), 0)
    place(x, compressions(rng, rng.integers(5, 9), 9, 3, 1.0, fire=0.45), 0.08)
    place(x, thump(rng, 40, 0.4, 0.6), 1.5 + rng.uniform(0, 0.3))
    return fade(reverb(soften(x, 8000), 1.3, 0.7, 0.25, 0.3, rng), 0.0005, 0.3)


def gen_start(rng):
    """It catches: stumbling bangs that run together into a steady roar."""
    x = silence(4.5)
    place(x, cord(rng), 0)
    place(x, compressions(rng, 8, 8, 22, 1.1, fire=0.9), 0.08)
    run = engine(rng, 3.6)
    ramp = np.clip(np.arange(len(run)) / (0.8 * SR), 0, 1).astype(np.float32)
    place(x, run * ramp * 0.9, 0.8)
    return fade(reverb(soften(x, 9000), 1.4, 0.8, 0.25, 0.3, rng), 0.0005, 0.4)


def gen_run(rng):
    return loopify(reverb(soften(engine(rng, 7.0), 9000), 1.2, 0.7, 0.2, 0.3, rng), 1.0)


def fire_alarm(rng):
    """An electromechanical fire bell: a clapper hammering a steel gong ~18 times a second, ringing in a corridor."""
    dur = 4.0
    x = silence(dur + 1.0)
    gong = strike_object(rng, "steel", 0.45, 0.85, 1.0, 0.5, damp=0.7)
    rate = 18.0
    t = 0.0
    while t < dur:
        place(x, gong * rng.uniform(0.8, 1.0), t)
        t += 1 / rate * (1 + rng.normal(0, 0.03))
    buzz = bp(noise(dur + 1.0, rng), 120, 400) * 0.03                   # the solenoid
    x = x + buzz.astype(np.float32)
    return loopify(reverb(soften(x, 9000)[: int(dur * SR)], 1.6, 0.8, 0.3, 0.5, rng), 0.5)


def thunder(rng, near):
    """Thunder: a crack (near strikes only), then a long rolling rumble that swells and fades, heard through a roof."""
    dur = rng.uniform(5.0, 8.0)
    n = int(dur * SR)
    t = np.arange(n) / SR
    x = np.zeros(n, np.float32)
    if near:
        crack = hp(noise(0.25, rng), 600) * env_exp(int(0.25 * SR), 0.03, 0.001) * 1.2
        place(x, crack.astype(np.float32), 0.0)
    rumble = lp(brown(dur, rng), 140 if near else 90)
    # rolls: a few swells as the sound arrives from different parts of the bolt
    env = np.zeros(n)
    for _ in range(rng.integers(3, 6)):
        c = rng.uniform(0.2, dur * 0.6); w = rng.uniform(0.4, 1.6)
        env += rng.uniform(0.4, 1.0) * np.exp(-((t - c) / w) ** 2)
    env *= np.clip(1 - t / dur, 0, 1) ** 0.7
    x += (rumble * env * (1.6 if near else 1.0)).astype(np.float32)
    x += (lp(pink(dur, rng), 400) * env * 0.15).astype(np.float32)    # the building rattling
    return fade(reverb(x, 2.5, 1.0, 0.35, 0.2, rng), 0.002, 0.8)


def render(out):
    rng = np.random.default_rng(9090)
    for i in range(4):
        out(f"sfx/generator_pull_{i}", gen_pull(rng))
        out(f"sfx/generator_cough_{i}", gen_cough(rng))
    for i in range(2):
        out(f"sfx/generator_start_{i}", gen_start(rng))
    out("loops/generator_run_0", gen_run(rng))
    out("loops/fire_alarm_0", fire_alarm(rng))
    trng = np.random.default_rng(4411)       # (own generator: adding thunder doesn't change the sounds above)
    for i in range(3):
        out(f"sfx/thunder_near_{i}", thunder(trng, True))
        out(f"sfx/thunder_far_{i}", thunder(trng, False))


if __name__ == "__main__":
    n = [0]
    def out(name, x):
        x = soften(x, 11000)
        path = os.path.join(OUT, name + ".wav")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        write(path, x); n[0] += 1
    render(out)
    print("wrote", n[0], "sounds")
