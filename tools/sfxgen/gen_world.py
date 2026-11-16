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


# ------------------------------------------------------------------ music (v3)

def strings(dur, notes, rng, attack=0.4, release=0.6, bright=2200, vibrato=5.5, trem=0.0, level=1.0):
    """A string section: detuned saws per note with vibrato, a soft bow attack, filtered body, bow noise."""
    n = int(dur * SR)
    t = t_axis(dur)
    x = np.zeros(n, np.float32)
    for f in notes:
        for det in (-0.006, -0.002, 0.003, 0.007):
            vib = 1 + 0.004 * np.sin(2 * np.pi * (vibrato + rng.uniform(-0.6, 0.6)) * t + rng.uniform(0, 6))
            ph = (np.cumsum(f * (1 + det) * vib) / SR) % 1
            x += (2 * ph - 1).astype(np.float32)
    x = formants(x, [450, 1100, 2600], 1.5, [1.0, 0.7, 0.35]) * 0.6 + lp(x, bright) * 0.4
    x = lp(x, bright) / (len(notes) * 4)
    x += bp(pink(dur, rng), 1200, 5000) * 0.02
    if trem > 0:
        x *= (1 - trem + trem * (0.5 + 0.5 * np.sin(2 * np.pi * 11 * t))).astype(np.float32)
    return x * env_adsr(n, attack, 0.2, 0.9, release) * level


def drum(rng, f0=90, f1=48, decay=0.25, level=1.0):
    """A big low drum (taiko-like): pitch dropping skin, a slap of the stick."""
    dur = decay * 3
    t = t_axis(dur)
    f = f1 + (f0 - f1) * np.exp(-t / 0.04)
    body = np.sin(2 * np.pi * np.cumsum(f) / SR).astype(np.float32) * np.exp(-t / decay).astype(np.float32)
    slap = bp(pink(dur, rng), 300, 2500) * env_exp(len(t), 0.012, 0.0003) * 0.5
    return sat(body * 1.3 + slap, 1.4) * level


def metal_hit(rng, level=1.0):
    from gen_mech import click
    x = strike_object(rng, "steel", rng.uniform(1.2, 2.0), 0.7, 1.0, 1.2, damp=0.25)
    return (x * 0.7 + np.concatenate([click(rng, (400, 4000), 0.01, 1.0), np.zeros(len(x))])[: len(x)]) * level


def music_dread(rng):
    """The background dread: sub drone, slow swelling clusters a semitone apart, a far-off metal scrape, a pulse."""
    dur = 24.0
    t = t_axis(dur)
    x = lp(sum(np.sin(2 * np.pi * f * t) for f in (36.7, 38.9)).astype(np.float32), 120) * 0.35
    for at, notes in ((0.0, [73.4, 77.8]), (8.0, [110, 116.5, 155.6]), (16.0, [82.4, 87.3, 123.5])):
        place(x, strings(7.5, notes, rng, 3.0, 3.5, 900) * 0.8, at)
    from gen_mech import scrape
    sc = scrape("metal", rng)[: int(3 * SR)]
    place(x, lp(sc, 2500) * env_adsr(len(sc), 1.0, 0.5, 0.6, 1.2) * 0.12, 11.0)
    for k in np.arange(0.0, dur, 1.6):  # a slow, quiet pulse - like something heavy, far away
        place(x, drum(rng, 60, 38, 0.3, 0.12), k)
    return loopify(reverb(soften(x, 6000), 3.5, 1.0, 0.45, 0.25, rng)[: len(t)], 3.0)


def music_near(rng):
    """It's close: thin, high, trembling string harmonics rubbing a semitone apart, with a breath of noise."""
    dur = 12.0
    t = t_axis(dur)
    x = strings(dur, [1046.5, 1108.7, 1568.0], rng, 2.0, 2.0, 6000, 7.0, trem=0.55, level=0.5)
    x += strings(dur, [261.6, 277.2], rng, 2.5, 2.5, 1500, 5.0, trem=0.0, level=0.35)
    x *= (0.75 + 0.25 * np.sin(2 * np.pi * t / 6)).astype(np.float32)
    return loopify(reverb(soften(x, 8000), 2.5, 0.9, 0.35, 0.35, rng)[: len(t)], 2.0)


def music_chase(rng):
    """The chase: 4 bars at 120 bpm (loops). Pounding low drums, a sawing staccato string ostinato on a minor
    second, metal hits on the off-beats, a cluster that swells and screams over the top."""
    bpm = 120
    beat = 60 / bpm
    dur = 16 * beat  # 8 s
    t = t_axis(dur)
    x = np.zeros(len(t), np.float32)
    for b in range(16):  # drums: heavy on 1 and 3, lighter between, a fill at the end
        place(x, drum(rng, 95, 46, 0.3, 1.0 if b % 2 == 0 else 0.55), b * beat)
        if b in (13, 14, 15):
            place(x, drum(rng, 120, 60, 0.15, 0.5), b * beat + beat * 0.5)
    for k in range(64):  # 16th-note ostinato: D / Eb, low strings, staccato
        f = 73.4 if (k // 2) % 2 == 0 else 77.8
        note = strings(beat / 4 * 0.9, [f, f * 2], rng, 0.005, 0.05, 1800, 0.0, 0.0, 0.9)
        place(x, note * (1.0 if k % 4 == 0 else 0.65), k * beat / 4)
    for b in (1, 5, 9, 13):
        place(x, metal_hit(rng, 0.35), b * beat + beat * 0.5)
    swell = strings(dur, [587.3, 622.3, 880.0, 932.3], rng, dur * 0.8, 0.3, 4500, 6.5, trem=0.6, level=0.7)
    x += swell * np.clip(t / dur, 0, 1).astype(np.float32) ** 1.5
    x += lp(sum(np.sin(2 * np.pi * 36.7 * h * t) / h for h in (1, 2)).astype(np.float32), 150) * 0.25
    x = sat(x * 1.2, 1.6)
    return loopify(reverb(soften(x, 7000), 1.6, 0.8, 0.22, 0.3, rng)[: len(t)], 0.15)


def music_sting(rng):
    """Seen: a violent string stab, a brass-like roar swelling under it, a huge low hit and a metal crash."""
    dur = 4.0
    x = silence(dur)
    stab = strings(1.6, [146.8, 155.6, 220.0, 233.1, 311.1, 329.6], rng, 0.01, 1.2, 5000, 6.0, trem=0.7, level=1.4)
    place(x, stab, 0.0)
    t = t_axis(2.5)
    brass = voice(2.5, 73.4 * (1 + 0.02 * np.sin(2 * np.pi * 5 * t)), rng, [(0, [600, 1100, 2400]), (1, [700, 1300, 2600])], 0.02, 0.1, 0.6, 0.0, 0.0, 3)
    place(x, sat(brass * 2, 2.5) * env_adsr(len(t), 0.4, 0.5, 0.7, 1.0) * 0.35, 0.05)
    place(x, drum(rng, 80, 35, 0.6, 1.4), 0.0)
    place(x, metal_hit(rng, 0.6), 0.02)
    return fade(reverb(soften(sat(x, 1.5), 7500), 2.2, 0.9, 0.3, 0.3, rng), 0.002, 0.5)


def music_menu(rng):
    """The menu: a slow, detuned, half-broken music box over a low drone and the mall's hum."""
    dur = 30.0
    x = amb_hum(rng)[: int(12 * SR)]
    x = np.tile(x, 3)[: int(dur * SR)] * 0.1
    x += lp(np.sin(2 * np.pi * 55 * t_axis(dur)).astype(np.float32), 100) * 0.15
    notes = [440, 415.3, 392, 349.2, 392, 329.6, 349.2, 311.1]
    for k in range(16):
        f = notes[k % len(notes)] * (1 + rng.uniform(-0.015, 0.015))
        b = fade(modal_ir(3.0, [f, f * 2.01, f * 3.03], [1.0, 0.4, 0.2], [1, .25, .08]), 0.002, 0.2)
        place(x, b * 0.22 * (0.6 if rng.random() < 0.2 else 1.0), 1.0 + k * 1.75 + rng.uniform(-0.08, 0.12))
    return loopify(reverb(soften(x, 6000), 3.5, 1.0, 0.45, 0.3, rng)[: int(dur * SR)], 3.0)


def music_lost(rng):
    """It lost you: a long, low falling exhale of strings."""
    dur = 5.0
    t = t_axis(dur)
    x = strings(dur, [110, 116.5], rng, 0.4, 2.5, 900, 4.0, level=0.7)
    x *= np.linspace(1, 0.3, len(t)).astype(np.float32)
    return fade(reverb(soften(x, 5000), 3.0, 1.0, 0.5, 0.25, rng), 0.05, 0.5)


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
    out("music/music_near_0", music_near(rng))
    for i in range(3):
        out(f"music/music_sting_{i}", music_sting(rng))
    out("music/music_lost_0", music_lost(rng))
