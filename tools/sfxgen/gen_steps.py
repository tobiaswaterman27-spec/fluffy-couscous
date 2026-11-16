"""Footsteps (v2): heel and toe strikes that excite the floor's own resonances, a soft sole, a quiet scuff."""
import numpy as np
from dsp import *

# How each floor answers a foot: the colour and length of the contact noise, how heavy it sounds,
# and (only for metal) a short, low, damped boom - never a pitched ring.
SURF = {
    "tile":      dict(band=(700, 7000), decay=0.006, thump=0.6, sole=1.0, grit=0.15),
    "concrete":  dict(band=(350, 4500), decay=0.009, thump=0.8, sole=0.9, grit=0.45),
    "carpet":    dict(band=(120, 1400), decay=0.022, thump=0.9, sole=0.6, grit=0.0),
    "metal":     dict(band=(300, 3500), decay=0.012, thump=0.7, sole=0.9, grit=0.1, boom=(110, 420, 0.07, 0.5)),
    "ventmetal": dict(band=(250, 2600), decay=0.014, thump=0.6, sole=0.8, grit=0.05, boom=(80, 300, 0.14, 0.8)),
    "wood":      dict(band=(200, 3500), decay=0.011, thump=0.9, sole=0.9, grit=0.08, knock=1),
    "glass":     dict(band=(1200, 8000), decay=0.005, thump=0.4, sole=0.9, grit=0.05),
}
MODES = {
    "walk":  dict(gap=(0.07, 0.11), level=0.8, toe=0.55),
    "run":   dict(gap=(0.03, 0.055), level=1.0, toe=0.75),
    "sneak": dict(gap=(0.13, 0.2), level=0.4, toe=0.8),
}


def thump(rng, f=90, dur=0.12, level=1.0):
    """The weight of a body landing: a short, low, damped push."""
    t = t_axis(dur)
    fr = f * (1 + 0.5 * np.exp(-t / 0.008))
    x = np.sin(2 * np.pi * np.cumsum(fr) / SR).astype(np.float32) * np.exp(-t / (dur / 4)).astype(np.float32)
    return lp(x, 400) * level


def scuff(rng, dur, band=(400, 3000), level=0.1):
    n = int(dur * SR)
    return bp(pink(dur, rng), *band) * env_adsr(n, dur * 0.2, dur * 0.3, 0.5, dur * 0.4) * level


def contact(s, rng, level, sharp=1.0):
    dur = 0.35
    x = silence(dur)
    place(x, thump(rng, rng.uniform(70, 105), 0.11, s["thump"]), 0)
    d = s["decay"] / sharp * rng.uniform(0.8, 1.2)
    n = int(0.08 * SR)
    lo, hi = s["band"]
    slap = bp(pink(0.08, rng), lo * rng.uniform(0.9, 1.1), hi * rng.uniform(0.9, 1.1)) * env_exp(n, d, 0.0004)
    place(x, slap * s["sole"] * 1.6, 0)
    if s.get("knock"):  # boards: a short hollow knock
        kn = bp(pink(0.06, rng), 180, 700) * env_exp(int(0.06 * SR), 0.018, 0.0005)
        place(x, kn * 1.0, 0)
    if s.get("boom"):
        lo2, hi2, dk, lvl = s["boom"]
        f = np.sort(rng.uniform(lo2, hi2, 6))
        boom = fade(modal_ir(0.35, f, np.full(6, dk) * rng.uniform(0.7, 1.2, 6), np.linspace(1, 0.4, 6)), 0.0, 0.05)
        exc = excitation(rng, 0.4, 0.004)
        boom = signal.fftconvolve(exc, boom)[: len(boom)].astype(np.float32)
        boom /= np.abs(boom).max() + 1e-9
        place(x, boom * lvl, 0)
    if s["grit"] > 0:
        for _ in range(rng.integers(4, 14)):
            gd = rng.uniform(0.003, 0.008)
            g = bp(pink(gd, rng), 1500, 5000) * env_exp(int(gd * SR), gd / 3, 0.0002)
            place(x, g * rng.uniform(0.3, 1.0) * s["grit"], rng.uniform(0.0, 0.07))
    return x * level


def footstep(name, mode, rng):
    s, m = SURF[name], MODES[mode]
    dur = 0.55
    x = silence(dur)
    place(x, contact(s, rng, m["level"], 1.3 if mode == "run" else 1.0), 0)
    gap = rng.uniform(*m["gap"])
    place(x, contact(s, rng, m["level"] * m["toe"] * rng.uniform(0.7, 1.0), 1.1), gap)
    place(x, scuff(rng, rng.uniform(0.04, 0.09), (400, 2500), 0.06 * m["level"]), gap * 0.3)
    x = soften(x, 6500)
    x = reverb(x, 0.55, 0.45, 0.12, 0.25, rng)
    return fade(x[: int(dur * SR)], 0.0005, 0.08)


def creak(dur, rng, level):
    """Stick-slip friction (wood / hinges): impulses at a wandering rate through resonances."""
    n = int(dur * SR)
    t = t_axis(dur)
    rate = rng.uniform(60, 160) * (1 + 0.4 * np.sin(2 * np.pi * rng.uniform(1, 4) * t))
    ph = np.cumsum(rate / SR)
    imp = (np.diff(np.floor(ph), prepend=0) > 0).astype(np.float32)
    imp *= rng.uniform(0.5, 1.0, n).astype(np.float32)
    y = formants(imp, [rng.uniform(300, 500), rng.uniform(800, 1300), rng.uniform(1800, 2600)], 12)
    return y * env_adsr(n, dur * 0.2, dur * 0.3, 0.7, dur * 0.3) * level


def crawl(name, rng):
    s = SURF[name]
    dur = 0.65
    x = silence(dur)
    place(x, contact(s, rng, 0.3, 0.7), 0.0)                        # hand
    place(x, contact(s, rng, 0.4, 0.6), rng.uniform(0.18, 0.28))   # knee
    # clothes and the knee sliding
    place(x, lp(pink(0.4, rng), 2200) * env_adsr(int(0.4 * SR), 0.1, 0.1, 0.6, 0.15) * 0.18, 0.04)
    place(x, scuff(rng, 0.25, (250, 2500), 0.12), 0.2)
    x = reverb(soften(x, 7000), 0.35, 0.3, 0.08, 0.3, rng)
    return fade(x[: int(dur * SR)], 0.002, 0.06)


def landing(name, rng):
    s = SURF[name]
    dur = 0.8
    x = silence(dur)
    place(x, thump(rng, 60, 0.25, 1.4), 0)
    place(x, contact(s, rng, 1.0, 1.2), 0)
    place(x, contact(s, rng, 0.7, 1.0), rng.uniform(0.02, 0.05))
    place(x, lp(pink(0.2, rng), 1500) * env_exp(int(0.2 * SR), 0.05) * 0.3, 0.01)  # clothes
    return fade(reverb(soften(x, 8000), 0.5, 0.4, 0.1, 0.3, rng)[: int(dur * SR)], 0.0005, 0.08)


def elliot_step(kind, rng):
    """Huge bare feet: a deep impact, wet skin smacking the floor, a sticky peel as it lifts, nails."""
    dur = 0.7
    x = silence(dur)
    place(x, thump(rng, rng.uniform(45, 60), 0.3, 1.8), 0)
    smack = bp(pink(0.05, rng), 250, 2200) * env_exp(int(0.05 * SR), 0.01, 0.0004) * 1.2
    place(x, smack, 0.003)
    if kind == "metal":
        place(x, contact(SURF["ventmetal"], rng, 1.0, 0.7), 0)
    elif kind == "hard":
        place(x, contact(SURF["concrete"], rng, 0.6, 0.7), 0)
        if rng.random() < 0.6:
            for k in range(rng.integers(1, 4)):  # nails
                nd = 0.006
                place(x, bp(pink(nd, rng), 2000, 6000) * env_exp(int(nd * SR), 0.0015, 0.0001) * 0.35, 0.02 + k * rng.uniform(0.012, 0.03))
    # sticky skin peeling off the floor
    pd = 0.05
    place(x, bp(pink(pd, rng), 900, 4000) * env_adsr(int(pd * SR), 0.004, 0.01, 0.4, 0.03) * 0.18, rng.uniform(0.18, 0.28))
    x = reverb(soften(x, 7500), 0.7, 0.5, 0.12, 0.3, rng)
    return fade(x[: int(dur * SR)], 0.0005, 0.08)


def render(out, rng):
    for name in SURF:
        for mode in MODES:
            for i in range(6):
                out(f"steps/step_{name}_{mode}_{i}", footstep(name, mode, rng))
        for i in range(6):
            out(f"steps/crawl_{name}_{i}", crawl(name, rng))
        for i in range(3):
            out(f"steps/land_{name}_{i}", landing(name, rng))
    for kind in ("hard", "carpet", "metal"):
        for i in range(8):
            out(f"creature/elliot_step_{kind}_{i}", elliot_step(kind, rng))
