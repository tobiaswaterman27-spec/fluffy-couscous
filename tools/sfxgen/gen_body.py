"""Living things: Elliot's breathing, clicks, growls and screams; the gore; the player's breath, heart and gasps."""
import numpy as np
from dsp import *
from gen_mech import click_pair

# Vowel formants (Hz): roughly a / e / i / o / u and a narrow, strained throat.
VOWELS = {
    "a": [800, 1200, 2600], "e": [500, 1900, 2600], "i": [300, 2300, 3000],
    "o": [500, 850, 2500], "u": [320, 800, 2400], "throat": [600, 1500, 3400],
}


def breath_cycle(dur_in, dur_out, rng, wet=0.0, voiced=0.0, f0=110, level=1.0, inhale_band=(900, 3500), exhale_band=(350, 2000)):
    """One breath: a rising airy inhale, a pause, a softer exhale. 'wet' adds gurgle/rattle, 'voiced' a moan."""
    pause = rng.uniform(0.05, 0.2)
    dur = dur_in + pause + dur_out + 0.1
    x = silence(dur)
    n_in, n_out = int(dur_in * SR), int(dur_out * SR)
    inh = bp(noise(dur_in, rng), *inhale_band) * env_adsr(n_in, dur_in * 0.6, dur_in * 0.2, 0.7, dur_in * 0.2)
    inh = formants(inh, [rng.uniform(1000, 1400), rng.uniform(2200, 2800)], 3) * 0.6 + inh * 0.4
    exh = bp(noise(dur_out, rng), *exhale_band) * env_adsr(n_out, dur_out * 0.15, dur_out * 0.3, 0.6, dur_out * 0.5) * 0.8
    if voiced > 0:
        g = glottal(dur_out, f0 * np.linspace(1.05, 0.9, n_out), rng, 0.04, 0.2)
        exh += formants(g, VOWELS["u"], 6) * voiced * 0.05
    if wet > 0:
        for part, at, d in ((inh, 0.0, dur_in), (exh, dur_in + pause, dur_out)):
            n = int(d * SR)
            rat = (rng.random(n) < 0.02 * wet).astype(np.float32)
            rat = bp(rat, 300, 1800) * 6 * wet
            t = t_axis(d)
            flutter = (0.6 + 0.4 * np.sin(2 * np.pi * rng.uniform(25, 40) * t)).astype(np.float32)
            part *= 1 - 0.5 * wet + 0.5 * wet * flutter
            part += rat * env_adsr(n, d * 0.3, d * 0.2, 0.8, d * 0.3)
    place(x, inh * level, 0.0)
    place(x, exh * level, dur_in + pause)
    return x


def breath_loop(rng, seconds, rate, depth, wet=0.0, voiced=0.0, f0=110, jitter=0.15, inhale_band=(900, 3500), exhale_band=(350, 2000)):
    x = silence(seconds + 1.5)
    tpos = 0.0
    while tpos < seconds:
        cyc = 60.0 / rate * rng.uniform(1 - jitter, 1 + jitter)
        di, do = cyc * 0.38, cyc * 0.45
        b = breath_cycle(di, do, rng, wet, voiced, f0, depth * rng.uniform(0.8, 1.1), inhale_band, exhale_band)
        place(x, b, tpos)
        tpos += cyc
    x = lp(x[: int(seconds * SR)], 5000)
    return loopify(x, 0.4)


# ------------------------------------------------------------------ Elliot

def elliot_breath(rng, hunting):
    # Huge chest, slow and wet; hunting: faster, harsher, with a growl underneath.
    x = breath_loop(rng, 12.0, 14 if hunting else 7, 1.0, wet=0.9, voiced=0.6 if hunting else 0.25, f0=55,
                    inhale_band=(250, 2400), exhale_band=(120, 1600))
    if hunting:
        t = t_axis(len(x) / SR)
        growl = glottal(len(x) / SR, 48 + 6 * np.sin(2 * np.pi * 0.3 * t), rng, 0.08, 0.4, 0.3)
        x += formants(growl, [400, 900, 2100], 4) * 0.02
    return reverb(x, 0.4, 0.3, 0.12, 0.3, rng)[: len(x)]


def elliot_clicks(rng):
    """Throat clicks: a dry, fast, irregular rattle - like something counting."""
    dur = rng.uniform(0.8, 1.6)
    x = silence(dur + 0.3)
    tpos = 0.0
    rate = rng.uniform(12, 26)
    while tpos < dur:
        c = bp(noise(0.02, rng), 900, 4500) * env_exp(int(0.02 * SR), 0.003, 0.0002)
        c = formants(c, [rng.uniform(700, 1100), rng.uniform(1900, 2600)], 8) * 2 + c
        place(x, c * rng.uniform(0.5, 1.0), tpos)
        tpos += 1 / rate * rng.uniform(0.6, 1.5)
        if rng.random() < 0.1:
            tpos += rng.uniform(0.08, 0.2)
    return fade(reverb(x, 0.5, 0.3, 0.15, 0.4, rng), 0.001, 0.1)


def elliot_growl(rng):
    """A low, creaking vocal-fry growl that rises into a strained hiss."""
    dur = rng.uniform(1.4, 2.4)
    n = int(dur * SR)
    t = t_axis(dur)
    f0 = 38 + 18 * (t / dur) + 4 * np.sin(2 * np.pi * 5 * t)
    g = glottal(dur, f0, rng, 0.12, 0.5, 0.25)
    v = morph_formants(g, [(0, VOWELS["o"]), (0.6, VOWELS["a"]), (1, VOWELS["throat"])], 4)
    hiss = bp(noise(dur, rng), 2500, 7000) * np.clip((t / dur - 0.5) * 2, 0, 1).astype(np.float32) * 0.3
    x = sat(v * 0.3 + hiss, 2.5) * env_adsr(n, 0.2, 0.3, 0.8, 0.4)
    return fade(reverb(x, 0.7, 0.4, 0.15, 0.3, rng), 0.01, 0.2)


def elliot_scream(rng):
    """The scream. Several voices at once: a shrieking rising voice, a ripping noise band, a sub roar, a crack."""
    dur = 2.4
    n = int(dur * SR)
    t = t_axis(dur)
    x = np.zeros(n, np.float32)
    rise = np.clip(t / 0.35, 0, 1) ** 0.6
    for k, det in enumerate((1.0, 1.013, 0.987, 1.5)):
        f0 = (190 + 560 * rise) * det * (1 + 0.04 * np.sin(2 * np.pi * (6 + k) * t)) * (1 - 0.15 * np.clip((t - 1.7) / 0.7, 0, 1))
        g = glottal(dur, f0, rng, 0.06, 0.4, 0.45)
        v = morph_formants(g, [(0, VOWELS["o"]), (0.15, VOWELS["a"]), (0.6, VOWELS["e"]), (1, VOWELS["i"])], 3.5)
        x += v * (0.35 if k < 3 else 0.15)
    shriek = bp(noise(dur, rng), 2500, 6500) * (0.6 + 0.4 * np.sin(2 * np.pi * 31 * t)).astype(np.float32)
    x += shriek * 0.25
    roar = glottal(dur, 52 + 10 * rise, rng, 0.1, 0.5, 0.3)
    x += formants(roar, [300, 700, 1800], 3) * 0.08
    x = sat(x * 1.5, 3.0)
    x *= env_adsr(n, 0.04, 0.2, 0.85, 0.5)
    crack = bp(noise(0.05, rng), 500, 6000) * env_exp(int(0.05 * SR), 0.01) * 1.5
    place(x, crack, 0.0)
    return fade(reverb(x, 1.2, 0.6, 0.22, 0.5, rng), 0.002, 0.3)


def elliot_twitch(rng):
    """Joints cracking: a quick volley of dry knuckle-pops."""
    x = silence(0.6)
    for k in range(rng.integers(2, 6)):
        c = bp(noise(0.015, rng), 1500, 7000) * env_exp(int(0.015 * SR), 0.0015, 0.0001)
        c += modes(0.015, [rng.uniform(1800, 3200)], [0.004], [0.6], rng)
        place(x, c * rng.uniform(0.6, 1.0), k * rng.uniform(0.03, 0.09))
    return fade(x, 0.0005, 0.05)


def elliot_sniff(rng):
    """Sniffing the air: short sharp intakes."""
    x = silence(1.4)
    for k in range(rng.integers(3, 6)):
        s = bp(noise(0.12, rng), 1500, 5000) * env_adsr(int(0.12 * SR), 0.02, 0.04, 0.5, 0.05)
        place(x, s * rng.uniform(0.5, 1.0), k * rng.uniform(0.15, 0.22))
    return fade(reverb(x, 0.4, 0.3, 0.1, 0.4, rng), 0.002, 0.1)


# ------------------------------------------------------------------ gore

def crunch(rng):
    dur = 0.9
    n = int(dur * SR)
    x = silence(dur)
    for k in range(rng.integers(15, 30)):  # bone fracturing
        at = rng.uniform(0, 0.35) ** 1.5
        c = bp(noise(0.02, rng), 1200, 8000) * env_exp(int(0.02 * SR), 0.002, 0.0001)
        place(x, c * rng.uniform(0.3, 1.0), at)
    sq = lp(noise(dur, rng), 900) * env_exp(n, 0.15, 0.005)
    sq *= (0.5 + 0.5 * np.sin(2 * np.pi * rng.uniform(8, 14) * t_axis(dur))).astype(np.float32)
    x += sq * 0.8
    t = t_axis(dur)
    x += (np.sin(2 * np.pi * 70 * t) * np.exp(-t / 0.06)).astype(np.float32) * 0.6
    return fade(reverb(x, 0.4, 0.2, 0.1, 0.4, rng), 0.0003, 0.1)


def tear(rng):
    dur = rng.uniform(0.7, 1.1)
    n = int(dur * SR)
    t = t_axis(dur)
    rip_rate = 18 + 40 * (t / dur)
    am = (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * np.cumsum(rip_rate) / SR))).astype(np.float32)
    x = bp(noise(dur, rng), 300, 5000) * (0.3 + 0.7 * am) * env_adsr(n, 0.05, 0.2, 0.8, 0.2)
    wet = lp(noise(dur, rng), 600) * (rng.random(n) < 0.004).astype(np.float32) * 8
    x += bp(wet, 200, 1500) * 0.5
    return fade(reverb(x, 0.35, 0.2, 0.1, 0.4, rng), 0.002, 0.08)


def splatter(rng):
    dur = 0.9
    x = lp(noise(dur, rng), 1200) * env_exp(int(dur * SR), 0.05, 0.001) * 0.8
    for k in range(rng.integers(20, 40)):
        at = rng.uniform(0.02, 0.7)
        d = bp(noise(0.03, rng), 600, 3000) * env_exp(int(0.03 * SR), 0.006)
        place(x, d * rng.uniform(0.1, 0.5) * (1 - at), at)
    return fade(reverb(x, 0.4, 0.3, 0.1, 0.4, rng), 0.0005, 0.1)


def chewing(rng):
    """Feeding: a slow wet rhythm of bites, crunches and tearing."""
    dur = 6.0
    x = silence(dur)
    tpos = 0.0
    while tpos < dur - 0.8:
        r = rng.random()
        s = crunch(rng) if r < 0.4 else tear(rng) if r < 0.7 else splatter(rng) * 0.6
        place(x, s * rng.uniform(0.4, 0.9), tpos)
        tpos += rng.uniform(0.35, 0.7)
    return loopify(x, 0.5)


# ------------------------------------------------------------------ the player

def player_gasp(rng):
    dur = 0.8
    n = int(dur * SR)
    t = t_axis(dur)
    air = bp(noise(dur, rng), 800, 4000) * env_adsr(n, 0.03, 0.15, 0.4, 0.4)
    g = glottal(dur, 210 * np.linspace(1.1, 0.9, n), rng, 0.05, 0.3)
    v = morph_formants(g, [(0, VOWELS["a"]), (1, VOWELS["o"])], 5) * env_adsr(n, 0.04, 0.1, 0.3, 0.3) * 0.04
    return fade(air * 0.7 + v, 0.002, 0.1)


def heartbeat(rng):
    """Lub-dub: two muffled thumps felt more than heard."""
    x = silence(0.6)
    for k, (f, lvl) in enumerate(((52, 1.0), (60, 0.65))):
        t = t_axis(0.18)
        thump = np.sin(2 * np.pi * f * (1 + 0.4 * np.exp(-t / 0.02)) * t).astype(np.float32) * np.exp(-t / 0.05).astype(np.float32)
        place(x, lp(thump, 150) * lvl, k * 0.16)
    return fade(x, 0.001, 0.05)


def exhale(rng):
    """Letting out a held breath."""
    dur = 1.2
    n = int(dur * SR)
    x = bp(noise(dur, rng), 300, 2500) * env_adsr(n, 0.05, 0.3, 0.5, 0.6)
    return fade(x, 0.003, 0.1)


def whimper(rng):
    dur = 0.9
    n = int(dur * SR)
    t = t_axis(dur)
    g = glottal(dur, 280 + 60 * np.sin(2 * np.pi * 2.5 * t), rng, 0.06, 0.3)
    v = formants(g, VOWELS["u"], 7) * env_adsr(n, 0.08, 0.2, 0.6, 0.3) * 0.05
    v += bp(noise(dur, rng), 700, 3000) * env_adsr(n, 0.05, 0.2, 0.3, 0.3) * 0.2
    return fade(v, 0.003, 0.1)


def render(out, rng):
    out("creature/elliot_breath_calm_0", elliot_breath(rng, False))
    out("creature/elliot_breath_hunt_0", elliot_breath(rng, True))
    for i in range(5):
        out(f"creature/elliot_clicks_{i}", elliot_clicks(rng))
        out(f"creature/elliot_twitch_{i}", elliot_twitch(rng))
    for i in range(4):
        out(f"creature/elliot_growl_{i}", elliot_growl(rng))
        out(f"creature/elliot_scream_{i}", elliot_scream(rng))
        out(f"creature/elliot_sniff_{i}", elliot_sniff(rng))
        out(f"gore/sfx_crunch_{i}", crunch(rng))
        out(f"gore/sfx_tear_{i}", tear(rng))
        out(f"gore/sfx_splatter_{i}", splatter(rng))
        out(f"player/sfx_gasp_{i}", player_gasp(rng))
        out(f"player/heartbeat_{i}", heartbeat(rng))
        out(f"player/exhale_{i}", exhale(rng))
        out(f"player/whimper_{i}", whimper(rng))
    out("gore/feeding_0", chewing(rng))
    out("player/breath_calm_0", breath_loop(rng, 10, 13, 0.25))
    out("player/breath_heavy_0", breath_loop(rng, 10, 32, 0.8, voiced=0.1, f0=180))
    out("player/breath_panic_0", breath_loop(rng, 10, 40, 1.0, voiced=0.35, f0=220, jitter=0.35))
