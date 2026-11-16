"""Footsteps (v2): heel and toe strikes that excite the floor's own resonances, a soft sole, a quiet scuff."""
import numpy as np
from dsp import *

#           floor material, size, how much it rings, extra
SURF = {
    "tile":      dict(mat="ceramic", size=1.0, ring=0.5, sole=0.5, grit=0.05),
    "concrete":  dict(mat="concrete", size=1.0, ring=0.6, sole=0.6, grit=0.35),
    "carpet":    dict(mat="body", size=1.5, ring=0.0, sole=0.25, grit=0.0, fibre=1.0),
    "metal":     dict(mat="steel", size=1.6, ring=0.4, sole=0.55, grit=0.08, damp=0.2),
    "ventmetal": dict(mat="sheet", size=0.8, ring=0.8, sole=0.5, grit=0.03, damp=0.25),
    "wood":      dict(mat="wood", size=1.0, ring=0.7, sole=0.55, grit=0.05),
    "glass":     dict(mat="glass", size=1.2, ring=0.35, sole=0.5, grit=0.03),
}
MODES = {
    "walk":  dict(gap=(0.07, 0.11), level=0.8, hard=0.45, toe=0.55),
    "run":   dict(gap=(0.03, 0.055), level=1.0, hard=0.6, toe=0.75),
    "sneak": dict(gap=(0.13, 0.2), level=0.4, hard=0.25, toe=0.8),
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


def contact(s, rng, hard, level):
    dur = 0.5 if s["mat"] in ("sheet", "steel") else 0.3
    x = silence(dur)
    place(x, thump(rng, rng.uniform(70, 110), 0.12, 0.9), 0)
    if s["ring"] > 0:
        place(x, strike_object(rng, s["mat"], s["size"] * rng.uniform(0.85, 1.15), hard, s["ring"], dur, damp=s.get("damp", 1.0)), 0)
    # the sole itself: a short, dull slap
    place(x, bp(pink(0.03, rng), 300, 2500) * env_exp(int(0.03 * SR), 0.006, 0.0005) * s["sole"], 0)
    if s.get("fibre"):
        place(x, lp(pink(0.12, rng), 1800) * env_exp(int(0.12 * SR), 0.03, 0.004) * 0.35, 0)
    if s["grit"] > 0:
        # a few grains of dust and sand crushed under the shoe
        for _ in range(rng.integers(4, 14)):
            gd = rng.uniform(0.003, 0.008)
            g = bp(pink(gd, rng), 1500, 5500) * env_exp(int(gd * SR), gd / 3, 0.0002)
            place(x, g * rng.uniform(0.3, 1.0) * s["grit"] * 1.5, rng.uniform(0.0, 0.07))
    return x * level


def footstep(name, mode, rng):
    s, m = SURF[name], MODES[mode]
    dur = 0.6 if s["mat"] in ("sheet", "steel") else 0.4
    x = silence(dur)
    place(x, contact(s, rng, m["hard"] * rng.uniform(0.85, 1.1), m["level"]), 0)
    gap = rng.uniform(*m["gap"])
    place(x, contact(s, rng, m["hard"] * 0.8, m["level"] * m["toe"] * rng.uniform(0.7, 1.0)), gap)
    place(x, scuff(rng, rng.uniform(0.04, 0.09), (500, 3500), 0.08 * m["level"]), gap * 0.3)
    if name in ("tile", "wood") and mode == "run" and rng.random() < 0.35:
        # rubber sole squeak
        t = t_axis(0.06)
        f = rng.uniform(1100, 1900) * (1 + 0.2 * np.sin(2 * np.pi * 30 * t))
        sq = formants(np.sin(2 * np.pi * np.cumsum(f) / SR).astype(np.float32), [1500, 3000], 4) * env_adsr(len(t), 0.01, 0.02, 0.5, 0.02) * 0.12
        place(x, sq, gap + 0.005)
    x = soften(x, 8000)
    x = reverb(x, 0.35, 0.3, 0.07, 0.3, rng)
    return fade(x[: int(dur * SR)], 0.0005, 0.05)


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
    hand = contact(s, rng, 0.2, 0.35)
    place(x, hand, 0.0)
    place(x, contact(s, rng, 0.3, 0.45), rng.uniform(0.18, 0.28))  # knee
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
    place(x, contact(s, rng, 0.7, 1.0), 0)
    place(x, contact(s, rng, 0.5, 0.7), rng.uniform(0.02, 0.05))
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
        place(x, strike_object(rng, "sheet", 1.4, 0.35, 0.8, 0.7, damp=0.35), 0)
    elif kind == "hard":
        place(x, strike_object(rng, "concrete", 1.3, 0.3, 0.4, 0.3), 0)
        if rng.random() < 0.6:
            for k in range(rng.integers(1, 4)):  # nails
                nd = 0.006
                place(x, bp(pink(nd, rng), 2000, 6000) * env_exp(int(nd * SR), 0.0015, 0.0001) * 0.35, 0.02 + k * rng.uniform(0.012, 0.03))
    # wet, sticky lift-off
    peel_at = rng.uniform(0.18, 0.28)
    place(x, gurgle(0.08, rng, 120, 500, 1600, 0.25), peel_at)
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
