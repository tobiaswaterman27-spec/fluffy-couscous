"""Doors, locks, shutters, drawers, switches, breakers, lifts, metal, glass - the building's mechanics."""
import numpy as np
from dsp import *
from gen_steps import creak


def click_pair(rng, dur=0.25, f=(2200, 3600), gap=0.04, level=1.0):
    x = silence(dur)
    for k in range(2):
        c = bp(noise(0.03, rng), *f) * env_exp(int(0.03 * SR), 0.003, 0.0002)
        c += modes(0.03, [rng.uniform(2500, 4000), rng.uniform(5000, 7000)], [0.008, 0.005], [0.5, 0.3], rng)
        place(x, c * (1.0 if k == 0 else 0.7) * level, k * gap * rng.uniform(0.8, 1.3))
    return x


def latch(rng):
    x = silence(0.5)
    place(x, click_pair(rng, 0.25, (1500, 5000), 0.05), 0)
    # door settling in its frame
    t = t_axis(0.3)
    thud = np.sin(2 * np.pi * 85 * t).astype(np.float32) * np.exp(-t / 0.04).astype(np.float32) * 0.5
    place(x, thud + lp(noise(0.3, rng), 600) * env_exp(len(t), 0.03) * 0.3, rng.uniform(0.06, 0.1))
    return fade(reverb(x, 0.4, 0.3, 0.1, 0.5, rng), 0.0005, 0.05)


def handle(rng):
    """Turning a handle: spring squeak + latch tongue withdrawing."""
    x = silence(0.4)
    t = t_axis(0.15)
    f = rng.uniform(900, 1400) * (1 + 0.3 * t / 0.15)
    sq = np.sin(2 * np.pi * np.cumsum(f) / SR).astype(np.float32) * env_adsr(len(t), 0.02, 0.05, 0.4, 0.05) * 0.1
    place(x, sq, 0)
    place(x, click_pair(rng, 0.2, (1800, 6000), 0.06, 0.8), 0.08)
    return fade(x, 0.0005, 0.04)


def rattle(rng):
    """Locked door: the handle rattles, the latch knocks against the keep, the door thumps in its frame."""
    x = silence(0.9)
    for k in range(rng.integers(4, 7)):
        at = k * rng.uniform(0.08, 0.13)
        place(x, click_pair(rng, 0.12, (1500, 5000), 0.02, rng.uniform(0.5, 1.0)), at)
        t = t_axis(0.12)
        place(x, (np.sin(2 * np.pi * 95 * t) * np.exp(-t / 0.03)).astype(np.float32) * 0.5, at + 0.01)
    return fade(reverb(x, 0.4, 0.3, 0.12, 0.5, rng), 0.0005, 0.06)


def door_creak(rng):
    dur = rng.uniform(0.8, 1.4)
    x = creak(dur, rng, 1.0)
    x += creak(dur, rng, 0.4) * 0.5
    return fade(reverb(x, 0.6, 0.4, 0.15, 0.4, rng), 0.01, 0.1)


def slam(rng):
    dur = 1.6
    n = int(dur * SR)
    t = t_axis(dur)
    x = np.zeros(n, np.float32)
    f = 70 * (1 + 0.5 * np.exp(-t / 0.02))
    x += np.sin(2 * np.pi * np.cumsum(f) / SR).astype(np.float32) * np.exp(-t / 0.12).astype(np.float32) * 1.3
    x += bp(noise(dur, rng), 200, 3000) * env_exp(n, 0.03, 0.0003) * 1.4
    x += modes(dur, [180, 410, 820, 1640], [0.2, 0.12, 0.08, 0.05], [1, .6, .4, .2], rng, .05) * 0.4
    for k in range(3):  # the frame and hinges rattling after
        place(x, click_pair(rng, 0.1, (1200, 4000), 0.015, 0.25), 0.05 + k * 0.06 + rng.uniform(0, 0.03))
    return fade(reverb(x, 1.4, 0.7, 0.3, 0.4, rng), 0.0003, 0.2)


def unlock(rng):
    """A key: in, a ratchet of tumblers, the bolt clacking back."""
    x = silence(0.8)
    place(x, bp(noise(0.12, rng), 2000, 8000) * env_adsr(int(0.12 * SR), 0.03, 0.03, 0.4, 0.04) * 0.2, 0)
    for k in range(5):
        place(x, click_pair(rng, 0.05, (3000, 8000), 0.01, 0.4), 0.15 + k * 0.035)
    place(x, click_pair(rng, 0.2, (900, 4000), 0.03, 1.2), 0.38)
    t = t_axis(0.2)
    place(x, (np.sin(2 * np.pi * 140 * t) * np.exp(-t / 0.03)).astype(np.float32) * 0.6, 0.39)
    return fade(reverb(x, 0.4, 0.3, 0.1, 0.5, rng), 0.0005, 0.05)


def shutter_roll(rng):
    """Corrugated slats rolling: a clatter of slat joints, the drum rumbling, chain links."""
    dur = 2.0
    n = int(dur * SR)
    t = t_axis(dur)
    x = lp(noise(dur, rng), 300) * 0.5
    rate = 16 * (1 + 0.05 * np.sin(2 * np.pi * 0.5 * t))
    hits = np.diff(np.floor(np.cumsum(rate / SR)), prepend=0) > 0
    for i in np.nonzero(hits)[0]:
        c = modes(0.12, [rng.uniform(300, 500), rng.uniform(900, 1300), rng.uniform(2000, 3000)], [0.04, 0.03, 0.02], [1, .6, .3], rng)
        c += bp(noise(0.12, rng), 1000, 6000) * env_exp(int(0.12 * SR), 0.008) * 0.8
        place(x, c * rng.uniform(0.5, 1.0) * 0.6, i / SR)
    chain = hp(noise(dur, rng), 3000) * (0.5 + 0.5 * np.sin(2 * np.pi * 37 * t)).astype(np.float32) * 0.08
    x += chain
    return loopify(reverb(x, 0.9, 0.6, 0.25, 0.4, rng)[:n], 0.25)


def shutter_stop(rng):
    dur = 2.0
    x = modes(dur, [210, 480, 860, 1400, 2300], [0.6, 0.45, 0.3, 0.2, 0.12], [1, .7, .5, .3, .2], rng, .03)
    x += bp(noise(dur, rng), 150, 3000) * env_exp(int(dur * SR), 0.04, 0.0003) * 2
    return fade(reverb(x, 1.5, 0.8, 0.3, 0.4, rng), 0.0003, 0.3)


def drawer(rng):
    dur = 0.6
    n = int(dur * SR)
    t = t_axis(dur)
    roll = bp(noise(dur, rng), 150, 1200) * env_adsr(n, 0.05, 0.1, 0.7, 0.1) * (0.6 + 0.4 * np.sin(2 * np.pi * 23 * t)).astype(np.float32)
    x = roll * 0.6
    place(x, click_pair(rng, 0.15, (600, 3000), 0.02, 1.0), 0.48)
    return fade(x, 0.003, 0.05)


def metal_clank(rng):
    dur = 1.4
    base = rng.uniform(250, 420)
    ratios = [1, 2.76, 5.4, 8.93, 13.3]
    x = modes(dur, [base * r for r in ratios], [0.5, 0.3, 0.18, 0.12, 0.08], [1, .7, .5, .3, .2], rng, .02)
    x += bp(noise(dur, rng), 800, 8000) * env_exp(int(dur * SR), 0.005, 0.0002) * 2
    return fade(reverb(x, 0.9, 0.5, 0.2, 0.5, rng), 0.0003, 0.2)


def switch_click(rng):
    x = click_pair(rng, 0.2, (2000, 9000), 0.012, 1.0)
    return fade(x, 0.0003, 0.03)


def bump(rng):
    dur = 0.35
    t = t_axis(dur)
    x = (np.sin(2 * np.pi * 110 * t) * np.exp(-t / 0.03)).astype(np.float32)
    x += bp(noise(dur, rng), 200, 2000) * env_exp(len(t), 0.01) * 0.6
    return fade(x, 0.0003, 0.05)


def thud(rng):
    dur = 0.9
    t = t_axis(dur)
    f = 48 * (1 + 0.6 * np.exp(-t / 0.02))
    x = np.sin(2 * np.pi * np.cumsum(f) / SR).astype(np.float32) * np.exp(-t / 0.15).astype(np.float32) * 1.4
    x += lp(noise(dur, rng), 700) * env_exp(len(t), 0.05, 0.001) * 1.2
    return fade(reverb(x, 0.8, 0.5, 0.2, 0.3, rng), 0.0005, 0.15)


def chime(rng):
    """Lift arrival: a soft two-tone electronic bell."""
    x = silence(1.8)
    for k, f in enumerate([1318.5, 1046.5]):  # E6 then C6
        tone = modes(1.4, [f, f * 2, f * 3.01], [0.6, 0.25, 0.12], [1, .25, .08], rng)
        place(x, tone * 0.8, k * 0.38)
    return fade(reverb(x, 0.8, 0.4, 0.25, 0.6, rng), 0.001, 0.2)


def breaker_on(rng):
    """A big industrial breaker: lever travel, a slamming clunk, a brief arc, the transformer hum swelling."""
    dur = 2.0
    n = int(dur * SR)
    x = silence(dur)
    place(x, bp(noise(0.15, rng), 400, 3000) * env_adsr(int(0.15 * SR), 0.05, 0.05, 0.5, 0.05) * 0.3, 0)
    place(x, metal_clank(rng)[: int(0.8 * SR)] * 0.8, 0.15)
    place(x, slam(rng)[: int(0.6 * SR)] * 0.7, 0.15)
    arc = hp(noise(0.08, rng), 2000) * env_exp(int(0.08 * SR), 0.02) * (rng.random(int(0.08 * SR)) < 0.3) * 1.5
    place(x, arc.astype(np.float32), 0.16)
    t = t_axis(1.6)
    hum = sum(np.sin(2 * np.pi * 50 * h * t) / h for h in (1, 2, 3, 4, 6)).astype(np.float32)
    place(x, hum * np.clip(t / 0.8, 0, 1).astype(np.float32) * np.clip((1.6 - t) / 0.6, 0, 1).astype(np.float32) * 0.15, 0.25)
    return fade(reverb(x, 1.0, 0.5, 0.2, 0.4, rng), 0.0005, 0.2)


def breaker_off(rng):
    x = silence(1.6)
    place(x, metal_clank(rng)[: int(0.9 * SR)] * 0.9, 0)
    t = t_axis(1.4)
    hum = sum(np.sin(2 * np.pi * 50 * h * t) / h for h in (1, 2, 3)).astype(np.float32)
    f = np.exp(-t / 0.3).astype(np.float32)
    place(x, hum * f * 0.25, 0.02)
    return fade(reverb(x, 1.0, 0.5, 0.2, 0.4, rng), 0.0005, 0.2)


def tube_start(rng):
    """A fluorescent tube striking: starter ticks, a ping, flutter and buzz."""
    dur = 1.6
    x = silence(dur)
    for k in range(rng.integers(2, 5)):
        place(x, click_pair(rng, 0.05, (3000, 9000), 0.005, 0.5), k * rng.uniform(0.1, 0.25))
    t = t_axis(0.9)
    ping = np.sin(2 * np.pi * rng.uniform(4000, 6000) * t).astype(np.float32) * np.exp(-t / 0.05).astype(np.float32) * 0.3
    buzz = sum(np.sign(np.sin(2 * np.pi * 100 * h * t)) / h ** 1.5 for h in (1, 2, 3)).astype(np.float32)
    flutter = (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 9 * t + rng.uniform(0, 6)))).astype(np.float32)
    place(x, ping + lp(buzz, 3000) * flutter * np.exp(-t / 0.5).astype(np.float32) * 0.2, 0.6)
    return fade(x, 0.0005, 0.1)


def glass_break(rng):
    dur = 2.0
    n = int(dur * SR)
    x = hp(noise(dur, rng), 2000) * env_exp(n, 0.04, 0.0002) * 1.5
    for k in range(60):  # shards
        at = rng.uniform(0, 1.2) ** 2
        f = rng.uniform(2500, 9000)
        s = modes(0.2, [f, f * 1.7], [0.04, 0.02], [1, .4], rng)
        place(x, s * rng.uniform(0.1, 0.5) * (1 - at / 1.5), at)
    return fade(reverb(x, 0.9, 0.5, 0.25, 0.7, rng), 0.0002, 0.2)


def bell(rng):
    """Old electric alarm bell: a hammer at ~20 Hz on a bright bell."""
    dur = 3.0
    x = silence(dur)
    for k in range(int(dur * 20)):
        b = modes(0.4, [2050, 2050 * 2.4, 2050 * 4.2], [0.25, 0.12, 0.06], [1, .5, .25], rng, .002)
        place(x, b * rng.uniform(0.8, 1.0) * 0.35, k / 20)
    return fade(reverb(x, 1.2, 0.6, 0.3, 0.5, rng), 0.002, 0.3)


def beep(rng):
    t = t_axis(0.6)
    on = ((t % 0.15) < 0.07).astype(np.float32)
    x = np.sign(np.sin(2 * np.pi * 2800 * t)).astype(np.float32) * on * 0.3
    return fade(lp(x, 6000), 0.001, 0.02)


def phone_ring(rng):
    """An old phone: two bells hammered at 20 Hz, ring-ring."""
    dur = 2.4
    x = silence(dur)
    for burst in (0.0, 0.6):
        for k in range(8):
            b = modes(0.3, [1700, 2150, 3400], [0.12, 0.1, 0.05], [1, .8, .3], rng, .003)
            place(x, b * 0.3, burst + k / 20)
    return fade(reverb(x, 0.6, 0.3, 0.2, 0.5, rng), 0.002, 0.3)


def pickup(rng):
    x = bp(noise(0.25, rng), 800, 6000) * env_adsr(int(0.25 * SR), 0.03, 0.08, 0.3, 0.08) * 0.4
    place(x, click_pair(rng, 0.1, (1000, 5000), 0.02, 0.3), 0.05)
    return fade(x, 0.002, 0.03)


def lift_doors(rng):
    """Lift doors: a motor whine sliding up, the leaves rumbling in their tracks, a soft stop."""
    dur = 1.4
    n = int(dur * SR)
    t = t_axis(dur)
    f = 180 + 120 * np.clip(t / 0.5, 0, 1)
    whine = np.sin(2 * np.pi * np.cumsum(f) / SR).astype(np.float32) * 0.1
    rumble = lp(noise(dur, rng), 500) * 0.5
    x = (whine + rumble) * env_adsr(n, 0.15, 0.2, 0.8, 0.25)
    place(x, bump(rng) * 0.4, 1.15)
    return fade(x, 0.005, 0.05)


def scrape(kind, rng):
    dur = 3.0
    n = int(dur * SR)
    t = t_axis(dur)
    stick = (0.6 + 0.3 * np.sin(2 * np.pi * 3 * t) + 0.1 * np.sin(2 * np.pi * 11 * t)).astype(np.float32)
    if kind == "metal":
        x = bp(noise(dur, rng), 600, 7000) * stick
        x += modes(dur, [410, 980, 2200], [5, 5, 5], [.3, .2, .1], rng) * stick * 0.4
    elif kind == "wood":
        x = bp(noise(dur, rng), 200, 3000) * stick + creak(dur, rng, 0.3) * 0.5
    else:
        x = bp(noise(dur, rng), 150, 5000) * stick
        x += hp(noise(dur, rng), 3000) * (rng.random(n) < 0.03) * 2
    return loopify(x.astype(np.float32), 0.3)


def render(out, rng):
    for i in range(4):
        out(f"sfx/sfx_latch_{i}", latch(rng))
        out(f"sfx/sfx_handle_{i}", handle(rng))
        out(f"sfx/sfx_rattle_{i}", rattle(rng))
        out(f"sfx/sfx_creak_{i}", door_creak(rng))
        out(f"sfx/sfx_slam_{i}", slam(rng))
        out(f"sfx/sfx_unlock_{i}", unlock(rng))
        out(f"sfx/sfx_drawerslide_{i}", drawer(rng))
        out(f"sfx/sfx_metalclank_{i}", metal_clank(rng))
        out(f"sfx/sfx_click_{i}", switch_click(rng))
        out(f"sfx/sfx_bump_{i}", bump(rng))
        out(f"sfx/sfx_thud_{i}", thud(rng))
        out(f"sfx/sfx_pickup_{i}", pickup(rng))
    for i in range(3):
        out(f"sfx/sfx_shutterstop_{i}", shutter_stop(rng))
        out(f"sfx/sfx_breakeron_{i}", breaker_on(rng))
        out(f"sfx/sfx_breakeroff_{i}", breaker_off(rng))
        out(f"sfx/sfx_tubestart_{i}", tube_start(rng))
        out(f"sfx/sfx_glassbreak_{i}", glass_break(rng))
        out(f"sfx/sfx_liftdoors_{i}", lift_doors(rng))
    out("sfx/sfx_shutterroll_0", shutter_roll(rng))
    out("sfx/sfx_chime_0", chime(rng))
    out("sfx/sfx_bell_0", bell(rng))
    out("sfx/sfx_beep_0", beep(rng))
    out("sfx/sfx_phonering_0", phone_ring(rng))
    for k in ("metal", "concrete", "wood"):
        out(f"loops/scrape_{k}_0", scrape(k, rng))
