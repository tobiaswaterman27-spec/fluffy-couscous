"""Doors, locks, shutters, drawers, switches, breakers, lifts, metal, glass (v2: physical modal models)."""
import numpy as np
from dsp import *
from gen_steps import creak, thump


def click(rng, band=(1200, 6000), decay=0.0025, level=1.0):
    """A small mechanical click: a very short coloured noise burst (no ringing)."""
    d = 0.03
    c = bp(pink(d, rng), band[0] * rng.uniform(0.85, 1.15), band[1] * rng.uniform(0.85, 1.15)) * env_exp(int(d * SR), decay, 0.0001)
    body = bp(pink(d, rng), 300, 1200) * env_exp(int(d * SR), decay * 2, 0.0002) * 0.4
    return (c + body) * level * 2


def click_pair(rng, dur=0.25, f=(1200, 6000), gap=0.04, level=1.0):
    """Two small clicks (a latch tongue going in and catching)."""
    x = silence(dur)
    for k in range(2):
        place(x, click(rng, f, rng.uniform(0.002, 0.004), (1.0 if k == 0 else 0.6) * level), k * gap * rng.uniform(0.8, 1.3))
    return soften(x, 8000)


def door_body(rng, level=1.0, size=1.0):
    """A wooden door leaf knocking in its frame: a dull, hollow, short boom."""
    x = thump(rng, rng.uniform(75, 100) / size, 0.18, 1.0)
    knock = bp(pink(0.18, rng), 140 / size, 900 / size) * env_exp(int(0.18 * SR), 0.03 * size, 0.0005) * 1.2
    return (x + knock[: len(x)]) * level


def latch(rng):
    x = silence(0.6)
    place(x, click_pair(rng, 0.2, gap=0.05, level=0.7), 0)                # handle springs back
    place(x, door_body(rng, 0.6), rng.uniform(0.05, 0.09))                 # door meets the frame
    place(x, click_pair(rng, 0.15, gap=0.02, level=0.9), rng.uniform(0.06, 0.1))  # tongue snaps into the keep
    return fade(reverb(soften(x, 8000), 0.6, 0.4, 0.12, 0.35, rng), 0.0005, 0.06)


def handle(rng):
    x = silence(0.45)
    t = t_axis(0.18)
    spring = creak(0.18, rng, 0.15)
    place(x, spring, 0)
    place(x, click_pair(rng, 0.2, gap=0.05, level=0.8), 0.1)
    return fade(soften(x, 8000), 0.001, 0.05)


def rattle(rng):
    """A locked door: the handle jerked, the latch clacking against the keep, the door thudding in its frame."""
    x = silence(1.0)
    t = 0.0
    for k in range(rng.integers(3, 6)):
        place(x, click_pair(rng, 0.1, gap=0.015, level=rng.uniform(0.5, 1.0)), t)
        place(x, door_body(rng, rng.uniform(0.4, 0.8)), t + 0.008)
        t += rng.uniform(0.11, 0.18)
    return fade(reverb(soften(x, 7500), 0.6, 0.4, 0.14, 0.3, rng), 0.0005, 0.08)


def door_creak(rng):
    dur = rng.uniform(0.9, 1.6)
    c = creak(dur, rng, 1.0) + creak(dur, rng, 0.4) * 0.5
    # the door panel resonates with the hinge
    body = strike_object(rng, "wood", 1.5, 0.2, 1.0, 0.25, damp=1.0)
    c = signal.fftconvolve(c, body * 0.3)[: len(c)] + c * 0.6
    return fade(reverb(soften(c, 6000), 0.8, 0.5, 0.18, 0.3, rng), 0.01, 0.15)


def slam(rng):
    """A heavy door slammed: the whole leaf booms, the latch cracks, the frame and hinges rattle, the room answers."""
    dur = 2.0
    x = silence(dur)
    place(x, thump(rng, 58, 0.35, 1.6), 0)
    place(x, door_body(rng, 1.3, 1.2), 0)
    place(x, click_pair(rng, 0.15, gap=0.01, level=1.0), 0.002)
    for k in range(3):
        place(x, click_pair(rng, 0.1, gap=0.02, level=0.25), 0.05 + k * rng.uniform(0.04, 0.08))
    return fade(reverb(soften(x, 7500), 1.6, 0.8, 0.3, 0.3, rng), 0.0003, 0.3)


def bang(rng):
    """Something big hammering on a door: the whole door booms, the frame jumps, the wall shudders. LOUD."""
    dur = 1.8
    x = silence(dur)
    place(x, thump(rng, rng.uniform(40, 50), 0.6, 2.2), 0)
    place(x, door_body(rng, 2.0, 1.4), 0)
    place(x, lp(pink(0.4, rng), 500) * env_exp(int(0.4 * SR), 0.08, 0.0005) * 1.5, 0)  # the wall shuddering
    for k in range(rng.integers(3, 6)):  # hinges, latch and frame rattling
        place(x, click(rng, (800, 4000), 0.004, rng.uniform(0.3, 0.7)), 0.01 + k * rng.uniform(0.02, 0.05))
    x = sat(x * 1.3, 1.8)
    return fade(reverb(soften(x, 6000), 1.5, 0.8, 0.28, 0.25, rng), 0.0003, 0.3)


def unlock(rng):
    x = silence(0.9)
    place(x, bp(pink(0.12, rng), 1500, 6000) * env_adsr(int(0.12 * SR), 0.03, 0.03, 0.4, 0.04) * 0.15, 0)  # key sliding in
    for k in range(5):
        place(x, click_pair(rng, 0.05, gap=0.008, level=0.35), 0.15 + k * rng.uniform(0.03, 0.045))     # pins
    place(x, click(rng, (500, 4000), 0.006, 1.2), 0.4)                                                   # bolt
    place(x, door_body(rng, 0.35), 0.41)
    return fade(reverb(soften(x, 8000), 0.5, 0.3, 0.12, 0.3, rng), 0.0005, 0.06)


def shutter_body_ir(rng, size=2.5, damp=0.6):
    f, d, a = material_modes("sheet", size, rng, 50)
    ir = fade(modal_ir(1.2, f, d * damp, a), 0.0, 0.3)
    return ir / (np.abs(ir).max() + 1e-9)


def slat_clack(rng, level=1.0):
    """One slat joint knocking over the drum: a short, dry, tinny clack (damped - it's corrugated steel)."""
    d = 0.05
    c = bp(pink(d, rng), rng.uniform(500, 900), rng.uniform(3000, 5000)) * env_exp(int(d * SR), rng.uniform(0.006, 0.012), 0.0002)
    lowk = bp(pink(d, rng), 150, 600) * env_exp(int(d * SR), 0.015, 0.0005) * 0.6
    return (c * 1.6 + lowk) * level


def shutter_roll(rng):
    """A roller shutter going up: a loud metal rattle - each slat clacking over the drum in quick succession,
    the slats jangling against each other, the drum and motor rumbling underneath."""
    dur = 3.0
    n = int(dur * SR)
    t = t_axis(dur)
    x = np.zeros(n, np.float32)
    pos = 0.0
    while pos < dur:
        rate = 24 * (1 + 0.15 * np.sin(2 * np.pi * 0.7 * pos))
        burst = slat_clack(rng, rng.uniform(0.6, 1.0))
        for j in range(rng.integers(1, 4)):  # each joint knocks two or three times as it settles
            place(x, burst * (0.6 ** j), pos + j * rng.uniform(0.004, 0.012))
        pos += 1 / rate * rng.uniform(0.75, 1.25)
    jangle = bp(pink(dur, rng), 1500, 5000) * (0.4 + 0.6 * np.abs(np.sin(2 * np.pi * 11 * t))).astype(np.float32) * 0.15
    rumble = lp(brown(dur, rng), 160) * 0.7
    motor = lp(sum(np.sin(2 * np.pi * 95 * h * t) / h for h in (1, 2, 3)).astype(np.float32), 400) * 0.12
    x = x + jangle + rumble + motor
    return loopify(reverb(soften(x, 6500), 1.2, 0.7, 0.18, 0.25, rng)[:n], 0.3)


def shutter_stop(rng):
    """It reaches the end: a heavy clattering crash of slats and the bottom bar slamming home."""
    dur = 2.0
    x = silence(dur)
    for k in range(18):
        place(x, slat_clack(rng, rng.uniform(0.5, 1.0)) * 1.2, rng.uniform(0, 0.12) ** 1.4)
    place(x, thump(rng, 48, 0.5, 2.0), 0.02)
    place(x, lp(pink(0.5, rng), 700) * env_exp(int(0.5 * SR), 0.1, 0.0005) * 1.2, 0.02)
    x = sat(x, 1.5)
    return fade(reverb(soften(x, 6500), 1.8, 0.9, 0.3, 0.25, rng), 0.0003, 0.3)


def drawer(rng):
    """A desk drawer: rollers grumbling along metal runners, contents sliding, a wooden knock at the end."""
    dur = 0.7
    n = int(dur * SR)
    t = t_axis(dur)
    grain = np.zeros(n, np.float32)
    pos = 0.02
    while pos < 0.5:  # roller bearings ticking over the runner
        place(grain, click(rng, (400, 2500), 0.003, rng.uniform(0.15, 0.4)), pos)
        pos += rng.uniform(0.012, 0.025)
    slide = bp(pink(dur, rng), 150, 1200) * env_adsr(n, 0.05, 0.1, 0.6, 0.15) * 0.35
    x = grain + slide
    for k in range(rng.integers(2, 4)):  # pens / bits sliding inside
        place(x, click(rng, (1500, 5000), 0.003, 0.25), rng.uniform(0.1, 0.45))
    place(x, door_body(rng, 0.7, 0.5), 0.52)
    return fade(reverb(soften(x, 6000), 0.5, 0.3, 0.1, 0.25, rng), 0.003, 0.06)


def metal_clank(rng):
    dur = 1.6
    x = strike_object(rng, "steel", rng.uniform(0.9, 1.4), 0.6, 0.6, dur, damp=0.15)
    place(x, thump(rng, 80, 0.15, 0.6), 0)
    place(x, click(rng, (500, 4000), 0.008, 1.0), 0)
    place(x, strike_object(rng, "steel", rng.uniform(0.9, 1.4), 0.5, 0.25, 0.8, damp=0.12), rng.uniform(0.08, 0.16))
    return fade(reverb(soften(x, 8000), 1.0, 0.6, 0.2, 0.35, rng), 0.0003, 0.2)


def switch_click(rng):
    x = silence(0.2)
    c = strike_object(rng, "body", 0.3, 0.95, 1.0, 0.05, damp=0.3)
    place(x, c, 0)
    place(x, click_pair(rng, 0.1, gap=0.01, level=0.6), 0.004)
    return fade(soften(x, 9000), 0.0003, 0.03)


def bump(rng):
    x = door_body(rng, 0.8, 0.8)
    return fade(soften(x, 5000), 0.0003, 0.05)


def thud(rng):
    dur = 1.0
    x = silence(dur)
    place(x, thump(rng, 45, 0.5, 1.6), 0)
    place(x, lp(pink(0.3, rng), 600) * env_exp(int(0.3 * SR), 0.05, 0.001) * 0.8, 0)
    return fade(reverb(soften(x, 4000), 1.0, 0.6, 0.2, 0.25, rng), 0.0005, 0.15)


def chime(rng):
    x = silence(1.9)
    for k, f in enumerate([659.3, 523.3]):  # a soft, low two-note ding-dong
        tone = modal_ir(1.5, [f, f * 2.0], [0.9, 0.25], [1, .08])
        att = np.clip(t_axis(1.5) / 0.012, 0, 1).astype(np.float32)
        place(x, fade(tone * att, 0.0, 0.08) * 0.7, k * 0.42)
    return fade(reverb(x, 0.9, 0.4, 0.22, 0.4, rng), 0.001, 0.2)


def hum(dur, rng, base=50, harmonics=(1, 2, 3, 4, 6), level=1.0):
    t = t_axis(dur)
    return lp(sum(np.sin(2 * np.pi * base * h * t) / h for h in harmonics).astype(np.float32), 900) * level


def breaker_on(rng):
    dur = 2.2
    x = silence(dur)
    place(x, creak(0.15, rng, 0.2), 0)                                               # lever travel
    place(x, click(rng, (300, 3000), 0.015, 1.4), 0.15)                              # contacts slam
    place(x, thump(rng, 60, 0.3, 1.2), 0.15)
    arc_n = int(0.06 * SR)
    arc = bp(pink(0.06, rng), 1500, 5000) * (rng.random(arc_n) < 0.25) * 0.8
    place(x, arc.astype(np.float32), 0.152)
    h = hum(1.8, rng, level=0.35)
    t = t_axis(1.8)
    place(x, h * np.clip(t / 0.6, 0, 1).astype(np.float32) * np.clip((1.8 - t) / 0.6, 0, 1).astype(np.float32), 0.2)
    return fade(reverb(soften(x, 7000), 1.2, 0.6, 0.2, 0.3, rng), 0.0005, 0.25)


def breaker_off(rng):
    dur = 1.8
    x = silence(dur)
    place(x, click(rng, (300, 3000), 0.015, 1.4), 0)
    place(x, thump(rng, 60, 0.3, 1.0), 0)
    t = t_axis(1.5)
    place(x, hum(1.5, rng, level=0.45) * np.exp(-t / 0.25).astype(np.float32), 0.0)  # everything winding down
    return fade(reverb(soften(x, 7000), 1.2, 0.6, 0.2, 0.3, rng), 0.0005, 0.25)


def tube_start(rng):
    dur = 1.8
    x = silence(dur)
    for k in range(rng.integers(2, 5)):
        place(x, click_pair(rng, 0.05, gap=0.004, level=0.3), k * rng.uniform(0.1, 0.25))
    t = t_axis(1.0)
    ping = np.zeros(int(1.0 * SR), np.float32)
    buzz = hum(1.0, rng, 100, (1, 2, 3, 5), 0.4)
    flutter = (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 9 * t + rng.uniform(0, 6)))).astype(np.float32)
    place(x, ping + buzz * flutter * np.exp(-t / 0.5).astype(np.float32), 0.65)
    return fade(soften(x, 6000), 0.0005, 0.1)


def glass_break(rng):
    dur = 2.2
    x = silence(dur)
    place(x, bp(pink(0.05, rng), 600, 6000) * env_exp(int(0.05 * SR), 0.01) * 1.2, 0)          # the crack
    place(x, strike_object(rng, "glass", 2.0, 0.9, 1.0, 0.4), 0)
    for k in range(50):  # shards falling and bouncing
        at = 0.05 + rng.uniform(0, 1.0) ** 1.6
        s = strike_object(rng, "glass", rng.uniform(0.3, 1.0), 0.8, 1.0, 0.25, count=5, damp=0.6)
        place(x, s * rng.uniform(0.1, 0.5) * (1.2 - at / 1.3), at)
    return fade(reverb(soften(x, 9000), 1.0, 0.6, 0.22, 0.4, rng), 0.0002, 0.25)


def bell(rng):
    dur = 3.0
    x = silence(dur)
    for k in range(int(dur * 18)):
        b = modal_ir(0.4, [1150, 1150 * 2.4, 1150 * 4.2], [0.2, 0.08, 0.04], [1, .3, .1])
        place(x, fade(b, 0.0005, 0.02) * rng.uniform(0.8, 1.0) * 0.35, k / 18)
    return fade(reverb(soften(x, 8000), 1.2, 0.6, 0.25, 0.4, rng), 0.002, 0.3)


def beep(rng):
    t = t_axis(0.6)
    on = ((t % 0.15) < 0.07).astype(np.float32)
    x = np.sin(2 * np.pi * 2800 * t).astype(np.float32) * on * 0.3
    return fade(lp(x, 5000), 0.001, 0.02)


def phone_ring(rng):
    dur = 2.4
    x = silence(dur)
    for burst in (0.0, 0.6):
        for k in range(8):
            b = modal_ir(0.3, [1700, 2150, 3400], [0.12, 0.1, 0.05], [1, .8, .3])
            place(x, fade(b, 0.0005, 0.02) * 0.3, burst + k / 20)
    return fade(reverb(soften(x, 7000), 0.6, 0.3, 0.2, 0.4, rng), 0.002, 0.3)


def pickup(rng):
    x = lp(pink(0.25, rng), 2500) * env_adsr(int(0.25 * SR), 0.03, 0.08, 0.3, 0.08) * 0.4
    place(x, switch_click(rng) * 0.25, 0.06)
    return fade(x, 0.002, 0.03)


def lift_doors(rng):
    dur = 1.5
    n = int(dur * SR)
    t = t_axis(dur)
    f = 140 + 90 * np.clip(t / 0.5, 0, 1)
    whine = lp(sum(np.sin(2 * np.pi * np.cumsum(f * h) / SR) / h for h in (1, 2, 3)).astype(np.float32), 1200) * 0.12
    rumble = bp(pink(dur, rng), 100, 900) * 0.5
    x = (whine + rumble) * env_adsr(n, 0.15, 0.2, 0.8, 0.25)
    place(x, door_body(rng, 0.4, 0.7), 1.2)
    return fade(soften(x, 5000), 0.005, 0.05)


def scrape(kind, rng):
    dur = 3.0
    n = int(dur * SR)
    t = t_axis(dur)
    stick = (0.6 + 0.3 * np.sin(2 * np.pi * 3 * t) + 0.1 * np.sin(2 * np.pi * 11 * t)).astype(np.float32)
    if kind == "metal":
        grains = np.zeros(n, np.float32)
        pos = 0.0
        while pos < dur:
            i = int(pos * SR)
            e = excitation(rng, 0.6, 0.002)
            grains[i:i + len(e)] += e[: max(0, min(len(e), n - i))] * rng.uniform(0.2, 1.0)
            pos += rng.exponential(1 / 250)
        x = signal.fftconvolve(grains, shutter_body_ir(rng, 1.2, 0.06))[:n].astype(np.float32)
        x = x / (np.abs(x).max() + 1e-9) * stick
    elif kind == "wood":
        x = bp(pink(dur, rng), 150, 2500) * stick + creak(dur, rng, 0.4) * 0.5
    else:
        x = bp(pink(dur, rng), 150, 4000) * stick
    return loopify(soften(x, 6000).astype(np.float32), 0.3)


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
        out(f"sfx/sfx_bang_{i}", bang(rng))
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
