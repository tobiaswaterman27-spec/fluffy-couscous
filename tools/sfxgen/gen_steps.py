"""Footsteps: player shoes on every floor (walk / run / sneak / crawl) and Elliot's bare, heavy, wet feet."""
import numpy as np
from dsp import *

# Each floor: the transient's colour, the floor's own resonances, its grit, and how it booms.
SURF = {
    "tile":     dict(band=(900, 9000), click=0.9, thump=(120, 0.5, 0.025), modes=[(1850, .02), (2900, .015), (4200, .01)], mode_amp=.25, grit=.15, squeak=.25),
    "concrete": dict(band=(400, 6000), click=0.7, thump=(95, 0.7, 0.03), modes=[(310, .02)], mode_amp=.05, grit=.6, squeak=.0),
    "carpet":   dict(band=(150, 1800), click=0.15, thump=(70, 0.9, 0.04), modes=[], mode_amp=0, grit=.0, squeak=0, fabric=.5),
    "metal":    dict(band=(700, 8000), click=0.8, thump=(110, 0.5, 0.03), modes=[(430, .25), (1170, .18), (2380, .12), (3150, .08), (5100, .05)], mode_amp=.45, grit=.2, squeak=0),
    "ventmetal":dict(band=(500, 5000), click=0.6, thump=(85, 0.8, 0.06), modes=[(140, .3), (260, .25), (720, .18), (1530, .1)], mode_amp=.7, grit=.1, squeak=0, boom=1),
    "wood":     dict(band=(300, 5000), click=0.6, thump=(130, 0.8, 0.045), modes=[(380, .06), (620, .05), (900, .04)], mode_amp=.25, grit=.1, squeak=0, creak=.3),
    "glass":    dict(band=(1500, 12000), click=0.9, thump=(150, 0.3, 0.02), modes=[(2100, .09), (3400, .07), (5200, .05)], mode_amp=.35, grit=.1, squeak=0),
}

MODES = {
    # gap heel->toe, level, transient sharpness
    "walk":  dict(gap=(0.06, 0.1), level=0.75, sharp=1.0, toe=0.55),
    "run":   dict(gap=(0.025, 0.05), level=1.0, sharp=1.3, toe=0.8),
    "sneak": dict(gap=(0.12, 0.2), level=0.35, sharp=0.5, toe=0.7),
}


def strike(s, rng, level, sharp, dur=0.4):
    """One contact with the floor: click + shoe thump + floor modes + grit."""
    n = int(dur * SR)
    x = np.zeros(n, np.float32)
    lo, hi = s["band"]
    click = bp(noise(dur, rng), lo, hi) * env_exp(n, 0.004 / sharp, 0.0003)
    x += click * s["click"] * 3.0
    f, a, d = s["thump"]
    t = t_axis(dur)
    fdrop = f * (1 + 0.6 * np.exp(-t / 0.01))
    x += (np.sin(2 * np.pi * np.cumsum(fdrop) / SR) * np.exp(-t / d) * a).astype(np.float32)
    if s["modes"]:
        fr = [m[0] for m in s["modes"]]
        de = [m[1] for m in s["modes"]]
        x += modes(dur, fr, de, [1.0 / (i + 1) for i in range(len(fr))], rng, 0.03) * s["mode_amp"]
    if s.get("boom"):
        # Thin sheet metal: a hollow, slightly wobbling boom.
        wob = 1 + 0.03 * np.sin(2 * np.pi * 7 * t)
        x += (np.sin(2 * np.pi * np.cumsum(95 * wob) / SR) * np.exp(-t / 0.12) * 0.6).astype(np.float32)
    if s["grit"] > 0:
        g = hp(noise(dur, rng), 2500) * env_exp(n, 0.03, 0.005)
        g *= (rng.random(n) < 0.08).astype(np.float32) * 3 + 0.2  # sandy crackle
        x += g.astype(np.float32) * s["grit"] * 0.5
    if s.get("fabric"):
        x += lp(noise(dur, rng), 900) * env_exp(n, 0.05, 0.01) * s["fabric"]
    return x * level


def footstep(name, mode, rng):
    s = SURF[name]
    m = MODES[mode]
    dur = 0.6 if name in ("metal", "ventmetal") else 0.42
    x = silence(dur)
    heel = strike(s, rng, m["level"], m["sharp"], dur)
    place(x, heel, 0.0)
    gap = rng.uniform(*m["gap"])
    toe = strike(s, rng, m["level"] * m["toe"] * rng.uniform(0.7, 1.0), m["sharp"] * 1.3, dur - gap)
    place(x, toe, gap)
    # Shoe scuff as the foot rolls.
    sc_n = int(0.07 * SR)
    scuff = bp(noise(0.07, rng), 1200, 7000) * env_adsr(sc_n, 0.02, 0.02, 0.5, 0.03) * 0.12 * m["level"]
    place(x, scuff, gap * 0.4)
    if s.get("squeak", 0) > 0 and mode == "run" and rng.random() < 0.6:
        t = t_axis(0.07)
        f = rng.uniform(1600, 2800) * (1 + 0.15 * np.sin(2 * np.pi * 40 * t))
        sq = np.sin(2 * np.pi * np.cumsum(f) / SR).astype(np.float32) * env_adsr(len(t), 0.01, 0.02, 0.6, 0.02) * 0.18
        place(x, sq, gap + 0.01)
    if s.get("creak") and rng.random() < 0.5:
        place(x, creak(0.25, rng, 0.35) * s["creak"], gap + 0.02)
    x = reverb(x, 0.25, 0.2, 0.08, 0.6, rng)
    return fade(x[: int(dur * SR)], 0.0005, 0.04)


def creak(dur, rng, level):
    """Stick-slip friction: a train of tiny impulses at a wandering rate through wooden resonances."""
    n = int(dur * SR)
    t = t_axis(dur)
    rate = rng.uniform(60, 160) * (1 + 0.4 * np.sin(2 * np.pi * rng.uniform(1, 4) * t))
    ph = np.cumsum(rate / SR)
    imp = (np.diff(np.floor(ph), prepend=0) > 0).astype(np.float32)
    imp *= rng.uniform(0.5, 1.0, n).astype(np.float32)
    y = formants(imp, [rng.uniform(300, 500), rng.uniform(800, 1300), rng.uniform(1800, 2600)], 12)
    return y * env_adsr(n, dur * 0.2, dur * 0.3, 0.7, dur * 0.3) * level


def crawl(name, rng):
    """Crawling: a hand pat, the knee and clothing dragging, the other hand."""
    s = SURF[name]
    dur = 0.6
    x = silence(dur)
    place(x, strike(s, rng, 0.25, 0.4, 0.4) * 0.8, 0.0)
    drag_n = int(0.35 * SR)
    drag = bp(noise(0.35, rng), 300, 4000 if name != "carpet" else 1500) * env_adsr(drag_n, 0.08, 0.1, 0.6, 0.12) * 0.22
    place(x, drag, 0.05)
    cloth = bp(noise(0.3, rng), 1500, 6000) * env_adsr(int(0.3 * SR), 0.05, 0.1, 0.4, 0.1) * 0.08
    place(x, cloth, 0.1)
    place(x, strike(s, rng, 0.2, 0.4, 0.3) * 0.7, rng.uniform(0.22, 0.32))
    if name in ("metal", "ventmetal"):
        place(x, modes(0.5, [140, 260, 610], [0.3, 0.2, 0.12], [1, .7, .4], rng, .05) * 0.25, 0.0)
    x = reverb(x, 0.3, 0.2, 0.1, 0.5, rng)
    return fade(x[: int(dur * SR)], 0.002, 0.05)


def landing(name, rng):
    s = SURF[name]
    dur = 0.7
    x = silence(dur)
    place(x, strike(s, rng, 1.0, 0.9, 0.6) * 1.2, 0.0)
    place(x, strike(s, rng, 0.8, 0.8, 0.5), rng.uniform(0.02, 0.05))
    body = lp(noise(0.25, rng), 400) * env_exp(int(0.25 * SR), 0.06) * 0.6
    place(x, body, 0.005)
    return fade(reverb(x, 0.4, 0.3, 0.12, 0.5, rng)[: int(dur * SR)], 0.0005, 0.06)


def elliot_step(kind, rng):
    """Bare, very heavy feet: a deep thump, a wet slap of skin, a sticky peel-off. Sometimes nails click."""
    dur = 0.6
    n = int(dur * SR)
    x = silence(dur)
    t = t_axis(dur)
    f = 62 * (1 + 0.8 * np.exp(-t / 0.015))
    thump = np.sin(2 * np.pi * np.cumsum(f) / SR).astype(np.float32) * np.exp(-t / 0.07).astype(np.float32) * 1.2
    x += thump
    slap = bp(noise(dur, rng), 700, 3500) * env_exp(n, 0.012, 0.0005) * (1.4 if kind != "carpet" else 0.4)
    x += slap
    peel = bp(noise(0.05, rng), 1500, 6000) * env_adsr(int(0.05 * SR), 0.003, 0.01, 0.3, 0.03) * 0.25
    place(x, peel, rng.uniform(0.11, 0.18))
    if kind == "hard" and rng.random() < 0.5:
        for k in range(rng.integers(1, 3)):
            nail = hp(noise(0.02, rng), 3000) * env_exp(int(0.02 * SR), 0.002) * 0.7
            place(x, nail, 0.03 + k * 0.025)
    if kind == "metal":
        x += modes(dur, [140, 260, 430, 1170], [0.35, 0.25, 0.2, 0.12], [1, .8, .5, .3], rng, .05) * 0.6
    x = reverb(x, 0.5, 0.4, 0.12, 0.4, rng)
    return fade(x[:n], 0.0005, 0.08)


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
