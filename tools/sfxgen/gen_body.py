"""Living things (v2): breath shaped by mouth / nose resonances (no hiss); Elliot built from wet gurgles,
vocal fry, sub-harmonic groans and a rough, layered scream; gore; the player's gasps and heart."""
import numpy as np
from dsp import *

MOUTH_OUT = [520, 1350, 2500]   # an open-mouth exhale
MOUTH_IN = [680, 1700, 2800]    # inhale through the mouth (tighter, a little higher)
NOSE = [260, 1050, 2200]        # nasal breathing
VOWELS = {"e": [480, 1850, 2550], "i": [320, 2250, 3000], "a": [780, 1200, 2600], "ae": [700, 1700, 2500], "o": [520, 880, 2500], "u": [330, 800, 2300],
          "uh": [600, 1150, 2450], "throat": [450, 1250, 3100]}


def airflow(dur, rng, shape, q=2.5, top=3000):
    """Breath air: soft pink noise resonating in the mouth or nose, with the fizz rolled off."""
    a = hp(lp(pink(dur, rng), top, 2), 120)
    g = [1.0, 0.6, 0.25]
    return formants(a, shape, q, g) * 0.7 + lp(a, 900) * 0.3


def breath(rng, din, dout, level=1.0, mode="calm", voiced=0.0, f0=140, shaky=0.0):
    pause_in = rng.uniform(0.04, 0.15)
    x = silence(din + pause_in + dout + 0.3)
    nin, nout = int(din * SR), int(dout * SR)
    shape_in = NOSE if mode == "calm" else MOUTH_IN
    shape_out = NOSE if mode == "calm" else MOUTH_OUT
    jit = lambda sh: [f * rng.uniform(0.92, 1.08) for f in sh]
    inh = airflow(din, rng, jit(shape_in)) * env_adsr(nin, din * 0.75, din * 0.1, 0.9, din * 0.12) * 0.4
    if shaky > 0:
        t = t_axis(din)
        inh *= (1 - shaky + shaky * (0.5 + 0.5 * np.sin(2 * np.pi * rng.uniform(7, 11) * t))).astype(np.float32)
    exh = airflow(dout, rng, jit(shape_out)) * env_adsr(nout, dout * 0.08, dout * 0.35, 0.55, dout * 0.55)
    if voiced > 0:
        f = f0 * np.linspace(1.04, 0.9, nout)
        v = voice(dout, f, rng, [(0, VOWELS["uh"]), (1, VOWELS["u"])], 0.04, 0.2, 0.55, 0.0, 0.0, 6)
        exh += v * env_adsr(nout, dout * 0.1, dout * 0.3, 0.5, dout * 0.5) * voiced * 0.12
    place(x, inh * level, 0)
    place(x, exh * level, din + pause_in)
    return x


def breath_loop(rng, seconds, rate, level, mode, voiced=0.0, f0=140, jitter=0.15, shaky=0.0, extras=0.0):
    x = silence(seconds + 2)
    t = 0.0
    while t < seconds:
        cyc = 60.0 / rate * rng.uniform(1 - jitter, 1 + jitter)
        b = breath(rng, cyc * rng.uniform(0.32, 0.42), cyc * rng.uniform(0.4, 0.5), level * rng.uniform(0.8, 1.1), mode,
                   voiced * (1 if rng.random() < 0.5 else 0.3), f0 * rng.uniform(0.95, 1.05), shaky)
        place(x, b, t)
        if extras > 0 and rng.random() < extras:  # a swallow, a lip smack
            sm = bp(pink(0.03, rng), 600, 3000) * env_exp(int(0.03 * SR), 0.006) * 0.15
            place(x, sm, t + cyc * 0.95)
        t += cyc
    x = lp(x[: int(seconds * SR)], 2600, 4)
    return loopify(reverb(x, 0.3, 0.2, 0.05, 0.2, rng)[: int(seconds * SR)], 0.4)


# ------------------------------------------------------------------ Elliot

def rasp(dur, rng, rate=(28, 42), level=1.0, shape=(380, 950, 2100)):
    """A rattling throat: air dragged past something loose - pulsing, dry, raw. No bubbles."""
    t = t_axis(dur)
    r = rng.uniform(*rate) * (1 + 0.2 * np.sin(2 * np.pi * rng.uniform(1, 3) * t))
    pulse = (0.35 + 0.65 * np.abs(np.sin(np.pi * np.cumsum(r) / SR)) ** 3).astype(np.float32)
    a = airflow(dur, rng, list(shape), 3.5, 2600)
    return a * pulse * level


def elliot_breath(rng, hunting):
    """Huge, slow, wet. Liquid rattles in its throat on every inhale; a fry-croak under the exhale."""
    secs = 12.0
    x = silence(secs + 3)
    t = 0.0
    rate = 13 if hunting else 6.5
    while t < secs:
        cyc = 60 / rate * rng.uniform(0.85, 1.2)
        din, dout = cyc * 0.4, cyc * 0.5
        ni, no = int(din * SR), int(dout * SR)
        inh = rasp(din, rng, (30, 45) if hunting else (22, 32), 0.8) * env_adsr(ni, din * 0.7, din * 0.1, 0.9, din * 0.15)
        fry_f0 = 34 + 8 * rng.random()
        exh = voice(dout, fry_f0, rng, [(0, VOWELS["o"]), (1, VOWELS["uh"])], 0.15, 0.6, 0.2, 0.0, 0.9, 3)
        exh *= env_adsr(no, dout * 0.15, dout * 0.3, 0.6, dout * 0.5) * (1.0 if hunting else 0.7)
        if hunting:
            growl = voice(dout, 62 * np.linspace(1.05, 0.9, no), rng, [(0, VOWELS["uh"]), (1, VOWELS["o"])], 0.06, 0.4, 0.4, 0.7, 0.2, 4)
            exh += sat(growl * 0.5, 2.0) * env_adsr(no, dout * 0.2, dout * 0.3, 0.5, dout * 0.5) * 0.6
        place(x, inh, t)
        place(x, exh, t + din + rng.uniform(0.05, 0.2))
        t += cyc
    x = soften(x[: int(secs * SR)], 4500)
    return loopify(reverb(x, 0.6, 0.4, 0.12, 0.25, rng)[: int(secs * SR)], 0.6)


def elliot_groan(rng):
    """A long, low, wet groan that bends down and catches - not a sound a person could make."""
    dur = rng.uniform(2.2, 3.6)
    n = int(dur * SR)
    t = t_axis(dur)
    f0 = (78 + 10 * np.sin(2 * np.pi * 0.4 * t)) * np.linspace(1.0, 0.72, n) * (1 + 0.04 * np.sin(2 * np.pi * 5.5 * t))
    v = voice(dur, f0, rng, [(0, VOWELS["o"]), (0.5, VOWELS["uh"]), (1, VOWELS["throat"])], 0.07, 0.4, 0.45, 0.6, 0.25, 4)
    v = sat(v * 1.2, 2.2)
    v += rasp(dur, rng, (20, 30), 0.25)
    v *= env_adsr(n, 0.3, 0.4, 0.8, 0.6)
    return fade(reverb(soften(v, 5000), 1.0, 0.6, 0.18, 0.25, rng), 0.02, 0.3)


def elliot_gurgle(rng):
    """Liquid bubbling in a throat, with a croak breaking through."""
    dur = rng.uniform(1.0, 2.0)
    n = int(dur * SR)
    x = gurgle(dur, rng, rng.uniform(40, 90), 140, 900, 1.0) * env_adsr(n, 0.1, 0.2, 0.8, 0.3)
    croak = voice(dur * 0.5, 40 + 10 * rng.random(), rng, [(0, VOWELS["throat"]), (1, VOWELS["uh"])], 0.2, 0.6, 0.2, 0.0, 0.6, 3)
    place(x, croak * env_adsr(len(croak), 0.05, 0.1, 0.6, 0.2) * 0.6, dur * rng.uniform(0.2, 0.5))
    return fade(reverb(soften(x, 5000), 0.7, 0.4, 0.15, 0.3, rng), 0.01, 0.2)


def elliot_clicks(rng):
    """Dry clicks deep in the throat, irregular - like something counting."""
    dur = rng.uniform(0.8, 1.6)
    x = silence(dur + 0.3)
    t = 0.0
    rate = rng.uniform(10, 22)
    while t < dur:
        c = strike_object(rng, "body", 0.5, 0.6, 1.0, 0.04)
        c = formants(c, [rng.uniform(500, 800), rng.uniform(1400, 2000)], 6) + c * 0.3
        place(x, c * rng.uniform(0.5, 1.0), t)
        t += 1 / rate * rng.uniform(0.6, 1.6)
        if rng.random() < 0.12:
            t += rng.uniform(0.1, 0.3)
    return fade(reverb(soften(x, 5000), 0.6, 0.4, 0.15, 0.3, rng), 0.001, 0.1)


def elliot_scream(rng):
    """The scream: a rattling inhale, then a rough, tearing voice with an inhuman roar an octave under it."""
    pre = 0.45
    dur = 2.3
    x = silence(pre + dur + 0.2)
    # the inhale: a wet, sucking rattle
    ni = int(pre * SR)
    inh = airflow(pre, rng, [400, 1100, 2400], 3, 3500) * env_adsr(ni, pre * 0.8, 0.02, 1.0, 0.03)
    inh += rasp(pre, rng, (35, 50), 0.8)
    place(x, inh * 0.8, 0)
    n = int(dur * SR)
    t = t_axis(dur)
    rise = np.clip(t / 0.25, 0, 1) ** 0.5
    f0 = (260 + 420 * rise) * (1 + 0.06 * np.sin(2 * np.pi * 7 * t)) * (1 - 0.25 * np.clip((t - 1.5) / 0.8, 0, 1))
    main = voice(dur, f0, rng, [(0, VOWELS["o"]), (0.12, VOWELS["a"]), (0.6, VOWELS["ae"]), (1, VOWELS["a"])], 0.08, 0.3, 0.5, 0.5, 0.25, 3.5)
    under = voice(dur, f0 * 0.5, rng, [(0, VOWELS["o"]), (1, VOWELS["uh"])], 0.1, 0.4, 0.4, 0.8, 0.2, 3)
    s = sat(main * 1.4, 3.0) + sat(under * 1.2, 3.5) * 0.6
    s += bp(pink(dur, rng), 1200, 4500) * 0.12  # rasp
    s *= env_adsr(n, 0.03, 0.25, 0.85, 0.45)
    place(x, s, pre)
    return fade(reverb(soften(x, 7000), 1.3, 0.7, 0.22, 0.35, rng), 0.005, 0.3)


def elliot_spot(rng):
    """When it sees you: a short, sharp, rising screech."""
    dur = 1.1
    n = int(dur * SR)
    t = t_axis(dur)
    f0 = 330 + 520 * np.clip(t / 0.18, 0, 1) - 120 * np.clip((t - 0.6) / 0.5, 0, 1)
    v = voice(dur, f0, rng, [(0, VOWELS["a"]), (1, VOWELS["ae"])], 0.09, 0.3, 0.45, 0.6, 0.3, 3.2)
    v = sat(v * 1.5, 3.2) * env_adsr(n, 0.015, 0.15, 0.8, 0.35)
    return fade(reverb(soften(v, 7000), 1.1, 0.6, 0.2, 0.35, rng), 0.003, 0.2)


def elliot_sniff(rng):
    x = silence(1.4)
    for k in range(rng.integers(3, 6)):
        d = rng.uniform(0.08, 0.14)
        s = airflow(d, rng, NOSE, 3, 3000) * env_adsr(int(d * SR), d * 0.3, d * 0.3, 0.6, d * 0.3) * 1.2
        place(x, s * rng.uniform(0.6, 1.0), k * rng.uniform(0.14, 0.22))
    return fade(reverb(soften(x, 4500), 0.5, 0.3, 0.12, 0.3, rng), 0.002, 0.1)


def elliot_twitch(rng):
    x = silence(0.6)
    for k in range(rng.integers(2, 6)):
        c = strike_object(rng, "wood", 0.4, 0.95, 1.0, 0.05, damp=0.4)
        place(x, c * rng.uniform(0.5, 1.0), k * rng.uniform(0.03, 0.09))
    return fade(soften(x, 7000), 0.0005, 0.05)


# ------------------------------------------------------------------ gore

def squelch(dur, rng, level=1.0):
    """Wet flesh: low, thick noise that pulses irregularly - no bubbles."""
    n = int(dur * SR)
    t = t_axis(dur)
    am = (0.4 + 0.6 * np.abs(np.sin(2 * np.pi * rng.uniform(5, 11) * t + rng.uniform(0, 6))) ** 2).astype(np.float32)
    return lp(bp(pink(dur, rng), 120, 1600), 1400) * am * env_adsr(n, 0.01, dur * 0.3, 0.5, dur * 0.5) * level


def crunch(rng):
    dur = 0.9
    x = silence(dur)
    for k in range(rng.integers(12, 25)):  # bone and cartilage giving way: dry, short cracks
        cd = rng.uniform(0.004, 0.012)
        c = bp(pink(cd, rng), rng.uniform(600, 1500), rng.uniform(3000, 6000)) * env_exp(int(cd * SR), cd / 4, 0.0001)
        place(x, c * rng.uniform(0.4, 1.0) * 2, rng.uniform(0, 0.4) ** 1.4)
    place(x, squelch(0.6, rng, 0.8), 0.02)                        # wet
    place(x, lp(pink(0.4, rng), 700) * env_exp(int(0.4 * SR), 0.1) * 0.6, 0)  # squelch
    t = t_axis(0.3)
    place(x, (np.sin(2 * np.pi * 55 * t) * np.exp(-t / 0.07)).astype(np.float32) * 0.8, 0)
    return fade(reverb(soften(x, 6500), 0.4, 0.3, 0.1, 0.3, rng), 0.0003, 0.1)


def tear(rng):
    dur = rng.uniform(0.7, 1.1)
    n = int(dur * SR)
    t = t_axis(dur)
    rate = 16 + 35 * (t / dur)
    am = (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * np.cumsum(rate) / SR))).astype(np.float32)
    x = bp(pink(dur, rng), 250, 3500) * (0.3 + 0.7 * am) * env_adsr(n, 0.05, 0.2, 0.8, 0.2)
    # fibres snapping as it goes
    for k in range(rng.integers(15, 35)):
        cd = rng.uniform(0.002, 0.006)
        place(x, bp(pink(cd, rng), 800, 5000) * env_exp(int(cd * SR), cd / 3, 0.0001) * rng.uniform(0.3, 0.9), rng.uniform(0, dur * 0.9))
    x += squelch(dur, rng, 0.5)
    return fade(reverb(soften(x, 6000), 0.35, 0.2, 0.1, 0.3, rng), 0.002, 0.08)


def splatter(rng):
    dur = 0.9
    x = lp(pink(dur, rng), 1500) * env_exp(int(dur * SR), 0.05, 0.001) * 0.7
    for k in range(rng.integers(20, 40)):  # drops landing
        at = rng.uniform(0.02, 0.7)
        dd = rng.uniform(0.006, 0.015)
        place(x, lp(bp(pink(dd, rng), 300, 2500), 2000) * env_exp(int(dd * SR), dd / 3, 0.0002) * rng.uniform(0.2, 0.6) * (1 - at), at)
    return fade(reverb(soften(x, 6000), 0.4, 0.3, 0.1, 0.3, rng), 0.0005, 0.1)


def chewing(rng):
    dur = 6.0
    x = silence(dur)
    t = 0.0
    while t < dur - 0.8:
        r = rng.random()
        s = crunch(rng) if r < 0.4 else tear(rng) if r < 0.7 else splatter(rng) * 0.6
        place(x, s * rng.uniform(0.4, 0.9), t)
        t += rng.uniform(0.35, 0.7)
    return loopify(x, 0.5)


def sub_hit(rng):
    """A deep, physical impact for scares: a sub-bass drop with a hard transient."""
    dur = 2.0
    t = t_axis(dur)
    f = 32 + 60 * np.exp(-t / 0.05)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR).astype(np.float32) * np.exp(-t / 0.5).astype(np.float32) * 1.6
    x += lp(pink(dur, rng), 2500) * env_exp(len(t), 0.04, 0.0005) * 0.8
    return fade(reverb(sat(x, 1.6), 2.0, 0.9, 0.25, 0.3, rng), 0.0005, 0.3)


# ------------------------------------------------------------------ the player

def player_gasp(rng):
    dur = 0.7
    n = int(dur * SR)
    air = airflow(dur, rng, MOUTH_IN, 2.5, 4500) * env_adsr(n, 0.03, 0.15, 0.4, 0.4)
    v = voice(dur, 230 * np.linspace(1.1, 0.85, n), rng, [(0, VOWELS["a"]), (1, VOWELS["uh"])], 0.05, 0.3, 0.5, 0.0, 0.0, 5)
    v *= env_adsr(n, 0.03, 0.1, 0.25, 0.3) * 0.1
    return fade(soften(air + v, 5500), 0.002, 0.1)


def heartbeat(rng):
    x = silence(0.6)
    for k, (f, lvl) in enumerate(((50, 1.0), (58, 0.6))):
        t = t_axis(0.2)
        th = np.sin(2 * np.pi * f * (1 + 0.5 * np.exp(-t / 0.015)) * t).astype(np.float32) * np.exp(-t / 0.05).astype(np.float32)
        place(x, lp(th, 140) * lvl, k * 0.17)
    return fade(x, 0.001, 0.05)


def exhale(rng):
    dur = 1.1
    n = int(dur * SR)
    x = airflow(dur, rng, MOUTH_OUT, 2.5, 3000) * env_adsr(n, 0.04, 0.3, 0.5, 0.6)
    return fade(lp(x, 2800, 4), 0.003, 0.1)


def whimper(rng):
    """A suppressed whimper: two or three broken little 'hn' sounds through the nose, shaking, mostly breath."""
    x = silence(1.4)
    t0 = 0.0
    for k in range(rng.integers(2, 4)):
        d = rng.uniform(0.18, 0.32)
        n = int(d * SR)
        t = t_axis(d)
        f0 = rng.uniform(330, 420) * (1.08 - 0.18 * t / d) * (1 + 0.03 * np.sin(2 * np.pi * 9 * t))
        v = voice(d, f0, rng, [(0, NOSE), (1, VOWELS["u"])], 0.05, 0.4, 0.4, 0.0, 0.0, 7) * 0.06
        air = airflow(d, rng, NOSE, 3, 2500) * 0.35
        place(x, (v + air) * env_adsr(n, 0.02, d * 0.3, 0.6, d * 0.5), t0)
        t0 += d + rng.uniform(0.08, 0.2)
    return fade(lp(x, 3000, 4), 0.003, 0.1)


# ------------------------------------------------------------------ Elliot's voice, many variations

def contour(kind, dur, rng, lo, hi):
    """A pitch path for a vocalisation."""
    n = int(dur * SR)
    t = np.linspace(0, 1, n)
    if kind == "rise":
        c = lo + (hi - lo) * np.clip(t / rng.uniform(0.15, 0.4), 0, 1) ** 0.6
    elif kind == "fall":
        c = hi - (hi - lo) * t ** rng.uniform(0.6, 1.4)
    elif kind == "arch":
        c = lo + (hi - lo) * np.sin(np.pi * np.clip(t * rng.uniform(0.9, 1.2), 0, 1))
    elif kind == "warble":
        c = (lo + hi) / 2 + (hi - lo) / 2 * np.sin(2 * np.pi * rng.uniform(2, 5) * t)
    else:  # "stutter": breaks and jumps
        steps = rng.uniform(lo, hi, 6)
        c = steps[np.minimum((t * 6).astype(int), 5)]
        c = lp(c.astype(np.float32), 30, 1)
    vib = 1 + rng.uniform(0.01, 0.07) * np.sin(2 * np.pi * rng.uniform(4, 9) * t * dur)
    return (c * vib).astype(np.float32)


def vocalise(rng, dur, lo, hi, kind, vowels, rough, under=0.0, drive=3.0, rasp_amt=0.15, attack=0.03, room=(1.2, 0.6, 0.22)):
    n = int(dur * SR)
    f0 = contour(kind, dur, rng, lo, hi)
    path = [(k / (len(vowels) - 1), VOWELS[v]) for k, v in enumerate(vowels)]
    main = voice(dur, f0, rng, path, rng.uniform(0.05, 0.1), 0.3, rng.uniform(0.4, 0.55), rough, 0.25, rng.uniform(3, 4.5))
    x = sat(main * 1.4, drive)
    if under > 0:
        low = voice(dur, f0 * 0.5, rng, [(0, VOWELS["o"]), (1, VOWELS["uh"])], 0.1, 0.4, 0.4, 0.8, 0.2, 3)
        x += sat(low * 1.2, drive + 0.5) * under
    x += bp(pink(dur, rng), 1000, 4500) * rasp_amt
    x *= env_adsr(n, attack, dur * 0.15, 0.85, dur * 0.3)
    if kind == "stutter":  # broken off and restarting
        t = t_axis(dur)
        gate = (np.sin(2 * np.pi * rng.uniform(5, 9) * t) > -0.6).astype(np.float32)
        x *= lp(gate, 60, 1)
    return fade(reverb(soften(x, 7500), *room, 0.3, rng), 0.003, 0.25)


def scream_variant(rng):
    pre = rasp(rng.uniform(0.25, 0.5), rng, (35, 50), 0.7) if rng.random() < 0.6 else silence(0.01)
    body = vocalise(rng, rng.uniform(1.6, 2.8), rng.uniform(200, 320), rng.uniform(560, 820),
                    rng.choice(["rise", "arch", "warble", "stutter"]), list(rng.choice([["o", "a", "ae"], ["a", "ae", "a"], ["uh", "a", "i"], ["o", "a", "e"]])),
                    rng.uniform(0.4, 0.8), rng.uniform(0.3, 0.7), rng.uniform(2.5, 3.5))
    x = silence(len(pre) / SR + len(body) / SR)
    place(x, pre, 0)
    place(x, body, len(pre) / SR)
    return x


def shriek_variant(rng):
    return vocalise(rng, rng.uniform(0.6, 1.2), rng.uniform(380, 520), rng.uniform(800, 1100),
                    rng.choice(["rise", "arch", "fall"]), list(rng.choice([["a", "ae"], ["e", "i"], ["ae", "i"]])),
                    rng.uniform(0.3, 0.7), 0.2, 3.2, 0.2, 0.01)


def wail_variant(rng):
    """Long, eerie, carrying - meant to be heard from far away through the building."""
    return vocalise(rng, rng.uniform(2.5, 4.5), rng.uniform(140, 220), rng.uniform(300, 480),
                    rng.choice(["fall", "arch", "warble"]), list(rng.choice([["o", "u", "uh"], ["a", "o", "u"], ["uh", "o", "u"]])),
                    rng.uniform(0.15, 0.4), rng.uniform(0.2, 0.4), 2.0, 0.08, 0.4, room=(2.4, 0.9, 0.35))


def roar_variant(rng):
    return vocalise(rng, rng.uniform(1.2, 2.2), rng.uniform(70, 100), rng.uniform(130, 190),
                    rng.choice(["arch", "fall", "rise"]), list(rng.choice([["o", "a", "uh"], ["uh", "a", "o"]])),
                    rng.uniform(0.7, 1.0), 0.8, 4.0, 0.25, 0.06)


def snarl_variant(rng):
    """Short, wet-mouthed, aggressive bursts - the sound it makes while it's on you."""
    x = silence(1.6)
    t0 = 0.0
    for k in range(rng.integers(2, 4)):
        d = rng.uniform(0.25, 0.5)
        b = vocalise(rng, d, rng.uniform(90, 140), rng.uniform(160, 260), "arch", ["uh", "a"], 0.9, 0.7, 4.0, 0.3, 0.01, room=(0.5, 0.3, 0.1))
        place(x, b, t0)
        t0 += d * rng.uniform(0.7, 1.0)
    return x


# ------------------------------------------------------------------ more of Elliot's repertoire

def pick(rng, options):
    return list(options[int(rng.integers(len(options)))])

def elliot_moan(rng):
    """Low, slow and almost human - a sad, hollow moan drifting through the dark."""
    return vocalise(rng, rng.uniform(2.5, 4.0), rng.uniform(85, 115), rng.uniform(130, 180),
                    rng.choice(["fall", "arch"]), pick(rng, [["u", "o", "uh"], ["o", "u"], ["uh", "o", "u"]]),
                    rng.uniform(0.05, 0.2), rng.uniform(0.1, 0.3), 1.6, 0.05, 0.5, room=(2.2, 0.85, 0.3))


def elliot_croak(rng):
    """Vocal fry slowed to a crawl: a creaking, clicking rattle like a door hinge in its throat."""
    dur = rng.uniform(1.4, 2.6)
    n = int(dur * SR)
    t = t_axis(dur)
    f0 = rng.uniform(14, 24) * (1 + 0.35 * np.sin(2 * np.pi * rng.uniform(0.3, 0.8) * t)) * np.linspace(1.15, 0.8, n)
    v = voice(dur, f0, rng, [(0, VOWELS["a"]), (0.5, VOWELS[rng.choice(["uh", "o"])]), (1, VOWELS["throat"])], 0.25, 0.7, 0.15, 0.0, 0.05, 4)
    v = sat(v * 1.3, 2.5) * env_adsr(n, 0.15, 0.3, 0.85, 0.4)
    return fade(reverb(soften(v, 5500), 0.9, 0.5, 0.16, 0.25, rng), 0.01, 0.25)


def elliot_chatter(rng):
    """Teeth clattering together, fast and uncontrolled, with a thin whine leaking through."""
    dur = rng.uniform(0.9, 1.8)
    x = silence(dur + 0.4)
    t, rate = 0.0, rng.uniform(16, 28)
    while t < dur:
        c = strike_object(rng, "glass", 0.25, 0.95, 1.0, 0.03, damp=0.3)
        c = formants(c, [rng.uniform(900, 1300), rng.uniform(2200, 2900)], 5) + c * 0.2
        place(x, c * rng.uniform(0.4, 1.0), t)
        t += 1 / rate * rng.uniform(0.7, 1.4)
    if rng.random() < 0.7:
        w = vocalise(rng, dur, 500, 700, "warble", ["i", "e"], 0.2, 0.0, 2.0, 0.05, 0.1, room=(0.4, 0.3, 0.1))
        place(x, w * 0.25, 0.05)
    return fade(reverb(soften(x, 7000), 0.6, 0.4, 0.14, 0.3, rng), 0.001, 0.1)


def elliot_whine(rng):
    """A thin, high keening - like something in pain, or something pretending to be."""
    return vocalise(rng, rng.uniform(1.5, 3.0), rng.uniform(420, 560), rng.uniform(640, 900),
                    rng.choice(["warble", "fall", "arch"]), pick(rng, [["i", "e"], ["e", "i", "e"], ["ae", "i"]]),
                    rng.uniform(0.05, 0.2), 0.0, 1.8, 0.04, 0.3, room=(1.8, 0.8, 0.28))


def elliot_laugh(rng):
    """Breathy, stuttering bursts falling in pitch - it sounds like laughing. It isn't."""
    x = silence(2.6)
    t0, hi = 0.0, rng.uniform(260, 380)
    for k in range(rng.integers(4, 8)):
        d = rng.uniform(0.09, 0.16)
        f = hi * (0.93 ** k)
        b = vocalise(rng, d, f * 0.9, f, "arch", pick(rng, [["a", "uh"], ["ae", "a"], ["e", "uh"]]), 0.5, 0.4, 3.0, 0.35, 0.005, room=(0.3, 0.2, 0.05))
        place(x, b, t0)
        t0 += d + rng.uniform(0.05, 0.12)
    tail = rasp(0.5, rng, (30, 40), 0.5) * env_adsr(int(0.5 * SR), 0.05, 0.1, 0.7, 0.3)
    place(x, tail, t0 + 0.05)
    return fade(reverb(soften(x[: int((t0 + 0.6) * SR)], 7000), 1.0, 0.55, 0.18, 0.3, rng), 0.002, 0.2)


def elliot_call(rng):
    """Two notes, like someone calling out across the mall. From the wrong throat."""
    x = silence(3.0)
    a = rng.uniform(170, 230)
    b = a * rng.choice([0.75, 0.8, 1.2])
    n1 = vocalise(rng, rng.uniform(0.4, 0.6), a * 0.97, a, "arch", ["e", "uh"], 0.3, 0.3, 2.2, 0.06, 0.04, room=(0.2, 0.1, 0.02))
    n2 = vocalise(rng, rng.uniform(0.7, 1.1), b * 0.85, b, "fall", ["o", "u"], 0.35, 0.35, 2.2, 0.06, 0.04, room=(0.2, 0.1, 0.02))
    place(x, n1, 0.0)
    place(x, n2, len(n1) / SR + rng.uniform(0.02, 0.1))
    x = x[: int((len(n1) + len(n2)) / SR * SR) + int(0.3 * SR)]
    return fade(reverb(soften(x, 6000), 2.6, 0.9, 0.38, 0.4, rng), 0.003, 0.3)


def elliot_howl(rng):
    """A roar that climbs into a scream and keeps going - the loudest thing in the building."""
    dur = rng.uniform(2.8, 4.0)
    x = vocalise(rng, dur, rng.uniform(90, 130), rng.uniform(520, 760), "rise", pick(rng, [["uh", "a", "ae", "a"], ["o", "a", "e"]]),
                 rng.uniform(0.6, 0.9), 0.7, 3.6, 0.2, 0.08, room=(2.4, 0.9, 0.32))
    return x


def elliot_huff(rng):
    """Short, heavy, voiced huffs through the nose and teeth - it is close, and it is searching."""
    x = silence(2.4)
    t0 = 0.0
    for k in range(rng.integers(3, 6)):
        d = rng.uniform(0.15, 0.28)
        nn = int(d * SR)
        a = airflow(d, rng, list(rng.choice([NOSE, MOUTH_OUT])), 3, 2200) * 0.9
        g = voice(d, rng.uniform(55, 75), rng, [(0, VOWELS["uh"]), (1, VOWELS["o"])], 0.1, 0.5, 0.4, 0.6, 0.1, 4) * 0.6
        place(x, (a + sat(g, 2.5)) * env_adsr(nn, 0.01, d * 0.3, 0.5, d * 0.6), t0)
        t0 += d + rng.uniform(0.12, 0.35)
    return fade(reverb(soften(x[: int((t0 + 0.3) * SR)], 4500), 0.6, 0.4, 0.14, 0.25, rng), 0.002, 0.15)


def elliot_feed(rng):
    """Wet growling with a full mouth - the sound it makes over a body."""
    dur = rng.uniform(1.8, 3.0)
    n = int(dur * SR)
    g = vocalise(rng, dur, rng.uniform(70, 90), rng.uniform(110, 150), "warble", ["uh", "o", "uh"], 0.9, 0.8, 3.8, 0.3, 0.1, room=(0.5, 0.3, 0.1))
    g = lp(g, 1600, 2)  # muffled by what's in its mouth
    x = silence(dur + 0.5)
    place(x, g, 0)
    place(x, squelch(dur, rng, 0.35) * env_adsr(n, 0.1, 0.2, 0.8, 0.3), 0.05)
    return fade(x, 0.003, 0.2)


def render(out, rng):
    out("creature/elliot_breath_calm_0", elliot_breath(rng, False))
    out("creature/elliot_breath_hunt_0", elliot_breath(rng, True))
    for i in range(5):
        out(f"creature/elliot_clicks_{i}", elliot_clicks(rng))
        out(f"creature/elliot_twitch_{i}", elliot_twitch(rng))
    for i in range(4):
        out(f"creature/elliot_growl_{i}", elliot_groan(rng))
        out(f"creature/elliot_scream_{i}", elliot_scream(rng))
        out(f"creature/elliot_spot_{i}", elliot_spot(rng))
        out(f"creature/elliot_sniff_{i}", elliot_sniff(rng))
        out(f"gore/sfx_crunch_{i}", crunch(rng))
        out(f"gore/sfx_tear_{i}", tear(rng))
        out(f"gore/sfx_splatter_{i}", splatter(rng))
        out(f"player/heartbeat_{i}", heartbeat(rng))
    for i in range(3):
        out(f"music/sub_hit_{i}", sub_hit(rng))
    out("gore/feeding_0", chewing(rng))
    # Elliot's voice: many takes of each, so it never sounds like a loop
    for i in range(10):
        out(f"creature/elliot_scream_{i + 4}", scream_variant(rng))
        out(f"creature/elliot_shriek_{i}", shriek_variant(rng))
        out(f"creature/elliot_snarl_{i}", snarl_variant(rng))
    for i in range(8):
        out(f"creature/elliot_wail_{i}", wail_variant(rng))
        out(f"creature/elliot_roar_{i}", roar_variant(rng))
    for i in range(6):
        out(f"creature/elliot_growl_{i + 4}", elliot_groan(rng))
        out(f"creature/elliot_spot_{i + 4}", shriek_variant(rng))
    # more of its repertoire
    for i in range(8):
        out(f"creature/elliot_moan_{i}", elliot_moan(rng))
        out(f"creature/elliot_croak_{i}", elliot_croak(rng))
        out(f"creature/elliot_chatter_{i}", elliot_chatter(rng))
        out(f"creature/elliot_whine_{i}", elliot_whine(rng))
        out(f"creature/elliot_laugh_{i}", elliot_laugh(rng))
        out(f"creature/elliot_call_{i}", elliot_call(rng))
        out(f"creature/elliot_howl_{i}", elliot_howl(rng))
        out(f"creature/elliot_huff_{i}", elliot_huff(rng))
    for i in range(6):
        out(f"creature/elliot_feed_{i}", elliot_feed(rng))
        out(f"creature/elliot_roar_{i + 8}", roar_variant(rng))
        out(f"creature/elliot_wail_{i + 8}", wail_variant(rng))
        out(f"creature/elliot_clicks_{i + 5}", elliot_clicks(rng))
        out(f"creature/elliot_sniff_{i + 4}", elliot_sniff(rng))
