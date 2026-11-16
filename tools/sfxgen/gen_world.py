"""The building's air: ambience loops, random distant sounds, and the music (dread, stingers, chase)."""
import numpy as np
from dsp import *
from gen_steps import creak
from gen_mech import metal_clank, click_pair, slam


def amb_hum(rng):
    """Fluorescent fittings: 50 Hz mains harmonics, a faint high whine, slow beating between fittings."""
    dur = 12.0
    t = t_axis(dur)
    x = np.zeros_like(t, dtype=np.float32)
    for h, a in ((2, 1.0), (4, 0.5), (6, 0.3), (8, 0.15), (10, 0.08)):
        x += (a * np.sin(2 * np.pi * 50 * h * t * (1 + rng.uniform(-0.001, 0.001)))).astype(np.float32)
        x += (a * 0.4 * np.sin(2 * np.pi * 50.15 * h * t)).astype(np.float32)
    x += lp(pink(dur, rng), 3000) * 0.04
    return loopify(x * 0.6, 1.0)


def amb_buzz(rng):
    """A failing ballast: harsh 100 Hz buzz that crackles."""
    dur = 8.0
    t = t_axis(dur)
    x = np.sign(np.sin(2 * np.pi * 100 * t)).astype(np.float32)
    x = lp(x, 1800) * 0.4
    crackle = (rng.random(len(t)) < 0.002).astype(np.float32)
    x += bp(crackle, 1500, 5000) * 4
    x *= (0.7 + 0.3 * lp(rng.standard_normal(len(t)).astype(np.float32), 3) * 30).clip(0.2, 1.2)
    return loopify(x, 1.0)


def amb_vent(rng):
    """Big building air handling: broadband air with a slow surge and a fan-blade tone."""
    dur = 14.0
    t = t_axis(dur)
    air = lp(pink(dur, rng), 1800) * 0.8 + lp(brown(dur, rng), 300) * 0.6
    surge = (0.75 + 0.25 * np.sin(2 * np.pi * 0.05 * t) + 0.08 * np.sin(2 * np.pi * 0.23 * t)).astype(np.float32)
    blade = np.sin(2 * np.pi * 118 * t).astype(np.float32) * 0.05 + np.sin(2 * np.pi * 236 * t).astype(np.float32) * 0.02
    return loopify(air * surge + blade, 2.0)


def amb_duct(rng):
    """Inside a duct: a hollow, tubular whistle of air; distant thumps travel along the metal."""
    dur = 16.0
    t = t_axis(dur)
    air = pink(dur, rng)
    x = formants(air, [190, 520, 880], 9) * 0.6 + lp(air, 400) * 0.4
    x *= (0.7 + 0.3 * np.sin(2 * np.pi * 0.11 * t)).astype(np.float32)
    for k in range(4):
        place(x, metal_clank(rng)[: int(0.8 * SR)] * 0.08, rng.uniform(1, dur - 2))
    return loopify(x, 1.5)


def amb_machinery(rng):
    """Plant room: motors, pumps, a rhythmic knock."""
    dur = 12.0
    t = t_axis(dur)
    x = sum(np.sin(2 * np.pi * f * t) * a for f, a in ((25, 1), (50, .7), (75, .3), (147, .2), (294, .1))).astype(np.float32)
    x += lp(pink(dur, rng), 1200) * 0.5
    knock = np.zeros_like(x)
    for k in range(int(dur * 1.5)):
        place(knock, click_pair(rng, 0.1, (300, 1500), 0.0, 0.8), k / 1.5)
    return loopify(x * 0.5 + knock * 0.3, 1.0)


def amb_carpark(rng):
    """Underground car park: huge fans, a distant low rumble, the odd drip, a faint road far above."""
    dur = 16.0
    t = t_axis(dur)
    fans = lp(pink(dur, rng), 900) * (0.8 + 0.2 * np.sin(2 * np.pi * 0.07 * t)).astype(np.float32)
    fans += np.sin(2 * np.pi * 47 * t).astype(np.float32) * 0.1
    rumble = lp(brown(dur, rng), 120) * 0.6
    x = fans + rumble
    for k in range(6):
        place(x, drip(rng) * 0.2, rng.uniform(0.5, dur - 1))
    return loopify(reverb(x, 2.5, 1.0, 0.3, 0.3, rng)[: len(t)], 2.0)


def amb_water(rng):
    dur = 12.0
    x = bp(pink(dur, rng), 300, 6000) * 0.7
    for k in range(400):
        place(x, bp(noise(0.02, rng), 1000, 5000) * env_exp(int(0.02 * SR), 0.004) * rng.uniform(0.1, 0.4), rng.uniform(0, dur - 0.1))
    return loopify(x, 1.0)


def amb_rain(rng):
    """Rain on the roof, heard from inside: a soft roar and patter, gutters gurgling."""
    dur = 14.0
    x = lp(pink(dur, rng), 2500) * 0.6
    for k in range(3000):
        place(x, bp(noise(0.01, rng), 2000, 8000) * env_exp(int(0.01 * SR), 0.002) * rng.uniform(0.05, 0.2), rng.uniform(0, dur - 0.05))
    return loopify(lp(x, 4000), 2.0)


def amb_wind(rng):
    """Wind on the roof: gusts, a moaning tone across vents."""
    dur = 14.0
    t = t_axis(dur)
    gust = (0.5 + 0.5 * lp(rng.standard_normal(len(t)).astype(np.float32), 0.3, 1) * 300).clip(0.1, 1.5)
    x = lp(pink(dur, rng), 1500) * gust
    x += formants(pink(dur, rng), [340, 680], 25) * gust * 0.3
    return loopify(x, 2.0)


def amb_dawn(rng):
    """Outside at dawn: a distant road, wind in trees, many birds."""
    dur = 14.0
    x = lp(brown(dur, rng), 250) * 0.3 + bp(pink(dur, rng), 1500, 6000) * 0.08
    for k in range(120):
        place(x, bird(rng), rng.uniform(0, dur - 1))
    return loopify(x, 2.0)


def bird(rng):
    notes = rng.integers(1, 7)
    f0 = rng.uniform(2200, 4800)
    d = rng.uniform(0.05, 0.16)
    x = silence(notes * d * 1.5 + 0.1)
    for k in range(notes):
        t = t_axis(d)
        f = f0 * (1 + rng.uniform(-0.2, 0.4) * np.sin(np.pi * t / d)) * (1 + 0.08 * k)
        s = np.sin(2 * np.pi * np.cumsum(f) / SR).astype(np.float32) * np.sin(np.pi * t / d).astype(np.float32)
        place(x, s * rng.uniform(0.05, 0.3), k * d * 1.4)
    return x


# ------------------------------------------------------------------ distant one-shots

def drip(rng):
    """A drop landing in a puddle: a tiny splash and the bubble 'plink' (pitch rising as it dies)."""
    x = silence(0.4)
    place(x, bp(pink(0.01, rng), 1500, 6000) * env_exp(int(0.01 * SR), 0.002) * 0.4, 0)
    place(x, bubble(rng, rng.uniform(900, 1700), rng.uniform(0.04, 0.08)), 0.002)
    return fade(reverb(soften(x, 8000), 1.6, 0.8, 0.45, 0.4, rng), 0.001, 0.2)


def structure_groan(rng):
    """The building settling: a long low creak of steel and concrete."""
    dur = rng.uniform(1.5, 3.0)
    x = creak(dur, rng, 1.0)
    x = lp(x, 900) * 2 + lp(brown(dur, rng), 120) * env_adsr(int(dur * SR), dur * 0.3, dur * 0.3, 0.6, dur * 0.3) * 0.4
    return fade(reverb(x, 2.5, 1.0, 0.45, 0.2, rng), 0.05, 0.4)


def distant_thud(rng):
    x = slam(rng)[: int(1.2 * SR)]
    x = lp(x, 600)
    return fade(reverb(x, 3.0, 1.0, 0.6, 0.2, rng), 0.001, 0.5)


def pipe_tick(rng):
    x = silence(1.5)
    for k in range(rng.integers(2, 6)):
        place(x, click_pair(rng, 0.08, (500, 3000), 0.0, rng.uniform(0.4, 1.0)), k * rng.uniform(0.15, 0.4))
    return fade(reverb(x, 1.5, 0.7, 0.35, 0.4, rng), 0.001, 0.2)


def distant_clatter(rng):
    x = silence(2.0)
    for k in range(rng.integers(3, 7)):
        place(x, metal_clank(rng)[: int(0.6 * SR)] * rng.uniform(0.3, 0.8), k * rng.uniform(0.08, 0.25))
    return fade(reverb(lp(x, 3000), 2.5, 1.0, 0.5, 0.3, rng), 0.001, 0.3)


def electrical_crackle(rng):
    dur = rng.uniform(0.4, 1.0)
    n = int(dur * SR)
    x = hp(noise(dur, rng), 1500) * (rng.random(n) < 0.02) * 2 * env_adsr(n, 0.02, 0.1, 0.7, 0.2)
    return fade(x.astype(np.float32), 0.001, 0.05)


# ------------------------------------------------------------------ music

def bowed(dur, freqs, rng, bright=1500, tremolo=0.0):
    """String-like cluster: detuned saws, slow bow pressure, filtered."""
    t = t_axis(dur)
    x = np.zeros_like(t, dtype=np.float32)
    for f in freqs:
        for det in (-0.004, 0.0, 0.005):
            ph = (np.cumsum(np.full(len(t), f * (1 + det)) * (1 + 0.003 * np.sin(2 * np.pi * rng.uniform(4, 6) * t))) / SR) % 1
            x += (2 * ph - 1).astype(np.float32)
    x = lp(x, bright) / (len(freqs) * 3)
    if tremolo > 0:
        x *= (1 - tremolo + tremolo * (0.5 + 0.5 * np.sin(2 * np.pi * 6 * t))).astype(np.float32)
    x += bp(noise(dur, rng), 1500, 6000) * 0.03  # bow hair
    return x


def music_dread(rng):
    """A quiet, barely-there bed: low drones a semitone apart, slow swells, distant metallic shimmer."""
    dur = 24.0
    t = t_axis(dur)
    x = bowed(dur, [36.7, 38.9, 55.0], rng, 600) * (0.6 + 0.4 * np.sin(2 * np.pi * t / 16)).astype(np.float32)
    shimmer = modes(dur, [1760, 1864, 2637], [8, 8, 8], [.2, .2, .1], rng) * (0.5 + 0.5 * np.sin(2 * np.pi * t / 11)).astype(np.float32)
    x += shimmer * 0.08
    return loopify(reverb(x, 4.0, 1.0, 0.5, 0.4, rng)[: len(t)], 4.0)


def music_chase(rng):
    """Being hunted: a pulsing low cluster, hammering heartbeat percussion, rising scraping strings."""
    dur = 16.0
    t = t_axis(dur)
    x = bowed(dur, [41.2, 43.7, 61.7, 87.3], rng, 900, 0.0)
    pulse = (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 2.2 * t))).astype(np.float32)
    x *= 0.6 + 0.4 * pulse
    hi = bowed(dur, [659, 698, 740], rng, 4000, 0.6) * 0.3
    x += hi
    for k in range(int(dur * 2.2)):
        tt = t_axis(0.3)
        hit = np.sin(2 * np.pi * 50 * (1 + np.exp(-tt / 0.02)) * tt).astype(np.float32) * np.exp(-tt / 0.08).astype(np.float32)
        place(x, hit * (1.0 if k % 2 == 0 else 0.6) * 0.8, k / 2.2)
    x = sat(x, 1.5)
    return loopify(reverb(x, 1.5, 0.8, 0.25, 0.4, rng)[: len(t)], 1.0)


def music_sting(rng):
    """Seen: a sudden dissonant swell and a hard hit, the kind that makes you jump."""
    dur = 3.5
    n = int(dur * SR)
    t = t_axis(dur)
    x = silence(dur)
    swell = bowed(1.2, [233, 247, 349, 370, 523, 554], rng, 5000) * np.clip(t_axis(1.2) / 1.2, 0, 1).astype(np.float32) ** 2
    place(x, swell * 1.5, 0.0)
    tt = t_axis(2.3)
    hit = (np.sin(2 * np.pi * 41 * (1 + 2 * np.exp(-tt / 0.03)) * tt) * np.exp(-tt / 0.4)).astype(np.float32) * 1.5
    hit += bp(noise(2.3, rng), 200, 8000) * env_exp(len(tt), 0.08) * 0.8
    hit += bowed(2.3, [233, 247, 370, 523], rng, 6000, 0.8) * env_exp(len(tt), 0.8) * 1.2
    place(x, hit, 1.2)
    return fade(reverb(sat(x, 1.8), 2.5, 1.0, 0.35, 0.5, rng), 0.01, 0.5)


def music_menu(rng):
    """The main menu: a hollow, slow, slightly out-of-tune music-box drift over the mall's hum."""
    dur = 30.0
    x = amb_hum(rng)[: int(12 * SR)]
    x = np.tile(x, 3)[: int(dur * SR)] * 0.15
    notes = [440, 523, 494, 392, 440, 330, 349, 330]
    for k in range(17):
        f = notes[k % len(notes)] * (1 + rng.uniform(-0.01, 0.01)) * (0.5 if k % 5 == 0 else 1)
        b = modes(3.0, [f, f * 2.01, f * 3.02], [1.2, 0.5, 0.25], [1, .3, .1], rng)
        place(x, b * 0.25, 1.0 + k * 1.6 + rng.uniform(-0.05, 0.05))
    return loopify(reverb(x, 3.5, 1.0, 0.45, 0.4, rng)[: int(dur * SR)], 3.0)


def music_lost(rng):
    """It lost you: a slow, falling, exhausted release."""
    dur = 5.0
    t = t_axis(dur)
    f = [110 * (1 - 0.08 * t / dur), 116.5 * (1 - 0.1 * t / dur)]
    x = np.zeros_like(t, dtype=np.float32)
    for ff in f:
        ph = (np.cumsum(ff) / SR) % 1
        x += (2 * ph - 1).astype(np.float32)
    x = lp(x, 700) * env_adsr(len(t), 0.5, 1.0, 0.6, 2.5) * 0.4
    return fade(reverb(x, 3.0, 1.0, 0.5, 0.3, rng), 0.05, 0.5)


def render(out, rng):
    out("loops/amb_fluorescenthum_0", amb_hum(rng))
    out("loops/amb_buzz_0", amb_buzz(rng))
    out("loops/amb_ventilation_0", amb_vent(rng))
    out("loops/amb_duct_0", amb_duct(rng))
    out("loops/amb_machinery_0", amb_machinery(rng))
    out("loops/amb_carparkfans_0", amb_carpark(rng))
    out("loops/amb_water_0", amb_water(rng))
    out("loops/amb_rain_0", amb_rain(rng))
    out("loops/amb_wind_0", amb_wind(rng))
    out("loops/amb_dawn_0", amb_dawn(rng))
    for i in range(4):
        out(f"world/drip_{i}", drip(rng))
        out(f"world/groan_{i}", structure_groan(rng))
        out(f"world/distant_thud_{i}", distant_thud(rng))
        out(f"world/pipe_tick_{i}", pipe_tick(rng))
        out(f"world/distant_clatter_{i}", distant_clatter(rng))
        out(f"world/crackle_{i}", electrical_crackle(rng))
    out("music/music_dread_0", music_dread(rng))
    out("music/music_chase_0", music_chase(rng))
    out("music/music_menu_0", music_menu(rng))
    for i in range(3):
        out(f"music/music_sting_{i}", music_sting(rng))
    out("music/music_lost_0", music_lost(rng))
