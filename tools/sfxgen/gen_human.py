"""The players' own voices without words: breathing (calm, heavy, panting, scared), gasps, effort grunts, impacts,
whimpers, the release after holding your breath, and screams / struggling / choking when caught.

Built like a speech synthesiser (Klatt-style CASCADE vocal tract: the source passes through each formant
resonator in turn, which gives the natural -12 dB/octave fall between formants instead of the hissy, buzzy sound
of formant filters added side by side). Breath is turbulent air through the tract - its brightness rises with the
airflow, as it does in a real mouth. Voiced sounds use a Rosenberg glottal pulse with jitter, shimmer and
(when straining or screaming) roughness. Two voices: male (F0 ~110 Hz, longer tract) and female (F0 ~205 Hz,
formants ~17% higher); in game each character also gets their own small pitch offset.

Own seed: rendering these never changes any other sound. Run: python3 gen_human.py
"""
import os
import numpy as np
from scipy import signal
from dsp import SR, pink, silence, place, fade, reverb, lp, hp, bp, env_exp, write, t_axis

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "AfterHours", "Resources", "Audio")

# formants (Hz) of an adult male tract; bandwidths widen in breathy / open-mouth sounds
V = {
    "schwa": [500, 1500, 2500, 3500, 4500],
    "a": [730, 1090, 2440, 3400, 4300],
    "ae": [660, 1720, 2410, 3400, 4300],
    "o": [570, 840, 2410, 3300, 4200],
    "u": [300, 870, 2240, 3300, 4200],
    "i": [270, 2290, 3010, 3600, 4500],
    "h_in": [620, 1650, 2700, 3600, 4600],    # inhaling through the mouth: tongue a little higher
    "nose": [260, 1100, 2300, 3300, 4300],
}
BW = [90, 110, 170, 250, 300]


def resonator(F, B):
    C = -np.exp(-2 * np.pi * B / SR)
    Bc = 2 * np.exp(-np.pi * B / SR) * np.cos(2 * np.pi * F / SR)
    A = 1 - Bc - C
    return [A], [1, -Bc, -C]


def antiresonator(F, B):
    b, a = resonator(F, B)
    A, Bc, C = b[0], -a[1], -a[2]
    return [1 / A, -Bc / A, -C / A], [1]


def tract(x, path, scale=1.0, bwk=1.0, nasal_zero=None, block=256):
    """Cascade vocal tract with formants moving along 'path' = [(t 0..1, vowel or [f1..f5]), ...]."""
    n = len(x)
    out = np.zeros(n, np.float32)
    zi = [None] * 6
    pts = [(t, V[v] if isinstance(v, str) else v) for t, v in path]
    for a in range(0, n, block):
        b = min(n, a + block)
        u = (a + b) / 2 / max(1, n)
        for j in range(len(pts) - 1):
            if pts[j][0] <= u <= pts[j + 1][0]:
                k = (u - pts[j][0]) / max(1e-6, pts[j + 1][0] - pts[j][0])
                fs = [p + (q - p) * k for p, q in zip(pts[j][1], pts[j + 1][1])]
                break
        else:
            fs = pts[-1][1]
        y = x[a:b].astype(np.float64)
        for i, (f, bw) in enumerate(zip(fs, BW)):
            bb, aa = resonator(min(f * scale, SR * 0.45), bw * bwk * (1 + 0.3 * i * (bwk - 1)))
            if zi[i] is None: zi[i] = signal.lfilter_zi(bb, aa) * 0
            y, zi[i] = signal.lfilter(bb, aa, y, zi=zi[i])
        if nasal_zero:
            bb, aa = antiresonator(nasal_zero * scale, 180)
            if zi[5] is None: zi[5] = np.zeros(2)
            y, zi[5] = signal.lfilter(bb, aa, y, zi=zi[5])
        out[a:b] = y
    return out


def rosenberg(f0, rng, jitter=0.01, shimmer=0.06, oq=0.6, rough=0.0):
    """Glottal flow derivative: Rosenberg pulses along an f0 track (per-sample array)."""
    n = len(f0)
    j = 1 + jitter * signal.lfilter([1], [1, -0.995], rng.standard_normal(n)) * 0.1
    if rough > 0:  # irregular, harsh voice: fast random pitch wobble + period doubling
        j *= 1 + rough * 0.05 * rng.standard_normal(n)
    ph = np.cumsum(f0 * j / SR)
    frac = ph % 1.0
    tp, tn = oq * 0.65, oq * 0.35
    g = np.where(frac < tp, 0.5 * (1 - np.cos(np.pi * frac / tp)),
                 np.where(frac < tp + tn, np.cos(0.5 * np.pi * (frac - tp) / tn), 0.0))
    d = np.diff(g, prepend=0.0)
    cyc = np.floor(ph).astype(int)
    amp = 1 + shimmer * rng.standard_normal(cyc.max() + 2)[cyc]
    if rough > 0:
        amp *= 1 + rough * 0.6 * ((cyc % 2) - 0.5)
    return (d * amp * SR / (f0 * 3.0)).astype(np.float32)   # ~unit amplitude whatever the pitch


def flow_noise(env, rng, tilt=1.0):
    """Turbulence: louder airflow is brighter (more high-frequency rush), quiet flow is soft and dark."""
    n = len(env)
    w = rng.standard_normal(n).astype(np.float32)
    dark = lp(w, 1800, 2)
    bright = hp(lp(w, 6500, 2), 1500)
    e = np.clip(env, 0, None)
    return (dark * e ** 1.5 + bright * e ** 2.5 * 0.35 * tilt).astype(np.float32)


def shape(n, a, peak, release, curve=1.6):
    """Airflow envelope: rises over 'a', holds/peaks, then decays (fractions of n)."""
    t = np.linspace(0, 1, n)
    up = np.clip(t / max(a, 1e-3), 0, 1) ** 0.8
    down = np.clip((1 - t) / max(release, 1e-3), 0, 1) ** curve
    return (up * down * peak).astype(np.float32)


class Voice:
    def __init__(self, sex):
        self.male = sex == "m"
        self.f0 = 112.0 if self.male else 205.0
        self.k = 1.0 if self.male else 1.17      # formant scale (shorter tract)
        self.tag = sex


def breath_phase(vo, rng, dur, inhale, mouth, level, shaky=0.0, voiced=0.0):
    n = int(dur * SR)
    if inhale:
        env = shape(n, 0.55, 1.0, 0.25)
    else:
        env = shape(n, 0.12, 1.0, 0.75, 1.2)
    if shaky > 0:
        t = t_axis(dur)[:n]
        trem = 1 - shaky * (0.5 + 0.5 * np.sin(2 * np.pi * rng.uniform(6, 10) * t + rng.uniform(0, 6)))
        env = env * trem.astype(np.float32)
    src = flow_noise(env * level, rng, 1.3 if inhale else 1.0)
    if mouth:
        vow = "h_in" if inhale else rng.choice(["schwa", "a", "ae"])
        path = [(0, vow), (1, "schwa" if inhale else "o")]
        y = tract(src, path, vo.k * rng.uniform(0.95, 1.05), bwk=2.2)
    else:
        y = tract(src, [(0, "nose"), (1, "nose")], vo.k, bwk=2.8, nasal_zero=750) * 0.6
    if voiced > 0 and not inhale:
        f = vo.f0 * np.linspace(1.02, 0.86, n) * rng.uniform(0.95, 1.05)
        g = rosenberg(f, rng, 0.015, 0.1, 0.7)
        venv = shape(n, 0.08, 1.0, 0.7, 1.4) * voiced * level
        y = y + tract(g * venv, [(0, "schwa"), (1, "o")], vo.k, bwk=1.6) * 0.5
    return y


def cycle(vo, rng, kind):
    """One full breath (in + out), as a one-shot. The game strings them together at the right rate."""
    if kind == "calm":       # resting, through the nose, barely there
        din, dout, mouth, lvl, sh, vc = rng.uniform(1.1, 1.5), rng.uniform(1.4, 1.9), False, 0.25, 0.0, 0.0
    elif kind == "heavy":    # getting your breath back
        din, dout, mouth, lvl, sh, vc = rng.uniform(0.45, 0.6), rng.uniform(0.55, 0.75), True, 0.7, 0.0, 0.15
    elif kind == "pant":     # sprinting: fast, hard, mouth open
        din, dout, mouth, lvl, sh, vc = rng.uniform(0.2, 0.28), rng.uniform(0.24, 0.32), True, 1.0, 0.0, 0.3
    else:                    # scared: shaky, uneven, held a moment
        din, dout, mouth, lvl, sh, vc = rng.uniform(0.5, 0.8), rng.uniform(0.7, 1.0), True, 0.45, 0.45, 0.1
    gap = rng.uniform(0.02, 0.12) if kind != "scared" else rng.uniform(0.15, 0.4)
    x = silence(din + gap + dout + 0.15)
    place(x, breath_phase(vo, rng, din, True, mouth, lvl, sh), 0)
    place(x, breath_phase(vo, rng, dout, False, mouth, lvl, sh * 0.6, vc), din + gap)
    return x


def vocal(vo, rng, dur, f0_path, vowel_path, level=1.0, oq=0.55, rough=0.0, breathy=0.15, onset=0.01, release=0.4,
          jitter=0.02, glottal_stop=True):
    """A voiced utterance: f0 from a (t, multiple-of-base-f0) path, formants along a vowel path."""
    n = int(dur * SR)
    t = np.linspace(0, 1, n)
    f0 = vo.f0 * np.interp(t, [p[0] for p in f0_path], [p[1] for p in f0_path])
    g = rosenberg(f0, rng, jitter, 0.08 + rough * 0.1, oq, rough)
    env = shape(n, onset / dur, 1.0, release, 1.3)
    if glottal_stop:  # a hard start: the cords snap closed then open (a little click of energy)
        env[: int(0.004 * SR)] *= np.linspace(2.0, 1.0, int(0.004 * SR))
    src = g * env + flow_noise(env, rng) * breathy
    y = tract(src, vowel_path, vo.k, bwk=1.2 + breathy)
    return y * level


def gasp(vo, rng):
    """Startled: a sharp, partly voiced inhale (ingressive 'hah' with a squeak), then a held breath."""
    d = rng.uniform(0.28, 0.42)
    n = int(d * SR)
    env = shape(n, 0.2, 1.0, 0.35, 1.0)
    air = tract(flow_noise(env, rng, 1.6), [(0, "h_in"), (1, "ae")], vo.k, bwk=1.8)
    f0 = vo.f0 * np.linspace(2.2, 2.9, n) * rng.uniform(0.95, 1.08)
    sq = tract(rosenberg(f0, rng, 0.04, 0.3, 0.45) * env * 0.35, [(0, "ae"), (1, "i")], vo.k, bwk=2.0)
    return air + sq


def effort(vo, rng, big=False):
    """Pushing, pulling, climbing ('hnh!'), or a big shove at a door ('uhh!')."""
    if big:
        d = rng.uniform(0.32, 0.5)
        return vocal(vo, rng, d, [(0, 1.15), (0.25, 1.25), (1, 0.85)], [(0, "schwa"), (0.4, "a"), (1, "o")],
                     1.0, oq=0.4, rough=0.35, breathy=0.25, onset=0.006, release=0.6)
    d = rng.uniform(0.18, 0.32)
    return vocal(vo, rng, d, [(0, 1.05), (1, 0.92)], [(0, "schwa"), (1, "u")], 0.8, oq=0.35, rough=0.2, breathy=0.3,
                 onset=0.005, release=0.5)


def strain(vo, rng):
    """A long effort through closed lips - heaving yourself up into a vent: 'nnnnggh... hah'."""
    d = rng.uniform(0.7, 1.1)
    v = vocal(vo, rng, d, [(0, 1.0), (0.6, 1.2), (1, 1.1)], [(0, "u"), (1, "u")], 0.5, oq=0.3, rough=0.3,
              breathy=0.1, onset=0.05, release=0.15)
    v = lp(v, 1400) * 1.4            # through the nose, mouth shut
    x = silence(d + 0.6)
    place(x, v, 0)
    place(x, breath_phase(vo, rng, 0.4, False, True, 0.8, voiced=0.3), d + 0.02)
    return x


def oof(vo, rng):
    """Winded by an impact: air knocked out, a grunt, a pained inhale."""
    x = silence(1.1)
    d = rng.uniform(0.16, 0.24)
    place(x, vocal(vo, rng, d, [(0, 0.95), (1, 0.75)], [(0, "a"), (1, "schwa")], 1.0, oq=0.4, rough=0.4, breathy=0.6,
                   onset=0.003, release=0.6), 0)
    place(x, breath_phase(vo, rng, rng.uniform(0.3, 0.45), True, True, 0.55, shaky=0.3), d + rng.uniform(0.15, 0.3))
    return x


def whimper(vo, rng):
    """Hiding, it right outside: two or three tiny broken 'hn' through the nose, trembling."""
    x = silence(1.5)
    t0 = 0.0
    for _ in range(rng.integers(2, 4)):
        d = rng.uniform(0.16, 0.3)
        v = vocal(vo, rng, d, [(0, 2.0), (1, 1.6)], [(0, "u"), (1, "u")], 0.35, oq=0.5, breathy=0.6, onset=0.03,
                  release=0.5, jitter=0.05, glottal_stop=False)
        v = lp(v, 1600)
        place(x, v, t0)
        t0 += d + rng.uniform(0.08, 0.22)
    return x


def relief(vo, rng):
    """Letting a held breath go: a long, shaky, open-mouthed exhale, then a deep gulp of air."""
    x = silence(2.4)
    place(x, breath_phase(vo, rng, rng.uniform(0.7, 0.9), False, True, 0.9, shaky=0.35, voiced=0.25), 0)
    place(x, breath_phase(vo, rng, rng.uniform(0.5, 0.65), True, True, 0.85), 1.0)
    return x


def scream(vo, rng, short=False):
    """Terror: high, harsh, open /a/ with the jaw dropped (F1 raised), pitch breaking, roughness."""
    d = rng.uniform(0.6, 0.9) if short else rng.uniform(1.4, 2.0)
    hi = (3.4 if vo.male else 2.9) * rng.uniform(0.9, 1.1)
    f0p = [(0, hi * 0.8), (0.12, hi), (0.6, hi * rng.uniform(0.95, 1.08)), (1, hi * 0.75)]
    open_a = [x * f for x, f in zip(V["a"], [1.35, 1.15, 1.05, 1.0, 1.0])]
    v = vocal(vo, rng, d, f0p, [(0, "ae"), (0.2, open_a), (1, "a")], 1.0, oq=0.45, rough=0.7, breathy=0.2,
              onset=0.03, release=0.3, jitter=0.04)
    return np.tanh(v * 2.5 / (np.abs(v).max() + 1e-9)).astype(np.float32)   # the throat overdrives


def struggle(vo, rng):
    """Being held: short cries - a strangled 'nh!', 'ah-', a sob - and ragged breath."""
    k = rng.integers(3)
    if k == 0:
        return vocal(vo, rng, rng.uniform(0.25, 0.4), [(0, 1.9), (1, 1.5)], [(0, "a"), (1, "schwa")], 1.0, oq=0.4,
                     rough=0.6, breathy=0.3, release=0.5)
    if k == 1:
        return vocal(vo, rng, rng.uniform(0.2, 0.3), [(0, 1.4), (1, 1.2)], [(0, "u"), (1, "u")], 0.8, oq=0.35,
                     rough=0.5, breathy=0.2, release=0.4)
    x = silence(0.9)
    place(x, vocal(vo, rng, 0.35, [(0, 2.2), (0.5, 2.5), (1, 1.6)], [(0, "ae"), (1, "a")], 0.9, oq=0.5, rough=0.4,
                   breathy=0.4, release=0.6, jitter=0.06), 0)
    place(x, breath_phase(vo, rng, 0.3, True, True, 0.8, shaky=0.5), 0.42)
    return x


def choke(vo, rng):
    """The end: a cut-off cry, a wet catch in the throat, a last pushed-out breath."""
    x = silence(1.6)
    place(x, vocal(vo, rng, 0.3, [(0, 1.8), (1, 1.2)], [(0, "a"), (1, "u")], 0.9, oq=0.35, rough=0.9, breathy=0.3,
                   release=0.2), 0)
    gl = vocal(vo, rng, 0.45, [(0, 0.6), (1, 0.45)], [(0, "u"), (1, "schwa")], 0.6, oq=0.25, rough=1.0, breathy=0.4,
               onset=0.02, release=0.6, jitter=0.1)
    place(x, gl * (0.6 + 0.4 * np.tanh(3 * np.sin(2 * np.pi * 22 * t_axis(0.45)[: len(gl)]))).astype(np.float32), 0.35)
    place(x, breath_phase(vo, rng, 0.5, False, True, 0.5, shaky=0.6), 0.85)
    return x


def master(x, rng, room=0.12):
    """Close mic in a big hard space: a touch of proximity warmth, gentle top roll-off, a little room."""
    x = hp(x.astype(np.float32), 70)
    x = lp(x, 7500, 2)
    x = reverb(x, 0.9, 0.5, room, 0.35, rng)
    return fade(x, 0.002, 0.08)


def render(out):
    rng = np.random.default_rng(31337)
    for sex in ("m", "f"):
        vo = Voice(sex)
        for kind, nvar in (("calm", 6), ("heavy", 8), ("pant", 10), ("scared", 6)):
            for i in range(nvar):
                out(f"player/hv_{sex}_{kind}_{i}", master(cycle(vo, rng, kind), rng, 0.08))
        for i in range(5): out(f"player/hv_{sex}_gasp_{i}", master(gasp(vo, rng), rng))
        for i in range(6): out(f"player/hv_{sex}_effort_{i}", master(effort(vo, rng), rng))
        for i in range(5): out(f"player/hv_{sex}_effortbig_{i}", master(effort(vo, rng, True), rng))
        for i in range(4): out(f"player/hv_{sex}_strain_{i}", master(strain(vo, rng), rng))
        for i in range(4): out(f"player/hv_{sex}_oof_{i}", master(oof(vo, rng), rng))
        for i in range(4): out(f"player/hv_{sex}_whimper_{i}", master(whimper(vo, rng), rng, 0.06))
        for i in range(3): out(f"player/hv_{sex}_relief_{i}", master(relief(vo, rng), rng))
        for i in range(4): out(f"player/hv_{sex}_scream_{i}", master(scream(vo, rng), rng, 0.25))
        for i in range(6): out(f"player/hv_{sex}_struggle_{i}", master(struggle(vo, rng), rng, 0.2))
        for i in range(3): out(f"player/hv_{sex}_choke_{i}", master(choke(vo, rng), rng, 0.15))


if __name__ == "__main__":
    import sys
    n = [0]
    only = sys.argv[1] if len(sys.argv) > 1 else None
    def out(name, x):
        if only and only not in name: return
        path = os.path.join(OUT, name + ".wav")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        write(path, x); n[0] += 1
    render(out)
    print("wrote", n[0], "sounds")
