"""Small DSP toolkit for AFTER HOURS sound design (numpy + scipy). Everything is mono float32 at 44.1 kHz."""
import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100


def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


def noise(dur, rng):
    return rng.standard_normal(int(dur * SR)).astype(np.float32)


def pink(dur, rng):
    n = int(dur * SR)
    w = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    f[0] = 1
    w /= np.sqrt(f)
    x = np.fft.irfft(w, n)
    return (x / (np.abs(x).max() + 1e-9)).astype(np.float32)


def brown(dur, rng):
    x = np.cumsum(rng.standard_normal(int(dur * SR)))
    x = signal.sosfilt(signal.butter(1, 20, 'hp', fs=SR, output='sos'), x)
    return (x / (np.abs(x).max() + 1e-9)).astype(np.float32)


def bp(x, lo, hi, order=2):
    hi = min(hi, SR / 2 - 100)
    return signal.sosfilt(signal.butter(order, [lo, hi], 'bandpass', fs=SR, output='sos'), x).astype(np.float32)


def lp(x, f, order=2):
    return signal.sosfilt(signal.butter(order, min(f, SR / 2 - 100), 'lowpass', fs=SR, output='sos'), x).astype(np.float32)


def hp(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, 'highpass', fs=SR, output='sos'), x).astype(np.float32)


def peak(x, f, q, gain=1.0):
    """Resonant band (a single formant / panel mode)."""
    b, a = signal.iirpeak(f, q, fs=SR)
    return (signal.lfilter(b, a, x) * gain).astype(np.float32)


def env_exp(n, tau, attack=0.0005):
    t = np.arange(n) / SR
    a = np.clip(t / max(attack, 1e-5), 0, 1)
    return (a * np.exp(-t / max(tau, 1e-5))).astype(np.float32)


def env_adsr(n, a, d, s, r):
    t = np.arange(n) / SR
    dur = n / SR
    e = np.where(t < a, t / max(a, 1e-5), np.where(t < a + d, 1 - (1 - s) * (t - a) / max(d, 1e-5), s))
    rel = np.clip((dur - t) / max(r, 1e-5), 0, 1)
    return (e * rel).astype(np.float32)


def modes(dur, freqs, decays, amps, rng, detune=0.0):
    """Modal synthesis: a struck object = a sum of decaying sines (its resonances)."""
    t = t_axis(dur)
    out = np.zeros_like(t, dtype=np.float32)
    for f, d, a in zip(freqs, decays, amps):
        f2 = f * (1 + rng.uniform(-detune, detune))
        out += (a * np.sin(2 * np.pi * f2 * t) * np.exp(-t / d)).astype(np.float32)
    # no clicks: a tiny attack, and a fade at the end in case the ring hasn't died away
    k = min(len(out), int(0.0008 * SR))
    if k > 0:
        out[:k] *= np.linspace(0, 1, k)
    k = min(len(out), int(0.02 * SR))
    if k > 0:
        out[-k:] *= np.linspace(1, 0, k)
    return out


def place(dst, src, at_sec, gain=1.0):
    i = max(0, int(at_sec * SR))
    if i >= len(dst):
        return dst
    n = min(len(src), len(dst) - i)
    dst[i:i + n] += src[:n] * gain
    return dst


def silence(dur):
    return np.zeros(int(dur * SR), dtype=np.float32)


def sat(x, drive=2.0):
    return (np.tanh(x * drive) / np.tanh(drive)).astype(np.float32)


def impulse_response(rt60, size, rng, bright=0.5, predelay=0.008):
    """A synthetic room: early reflections + exponentially decaying, darkening diffuse tail."""
    dur = rt60 * 1.2 + predelay
    n = int(dur * SR)
    t = np.arange(n) / SR
    ir = np.zeros(n, dtype=np.float32)
    for _ in range(int(6 + size * 10)):
        d = predelay + rng.uniform(0.002, 0.03 + size * 0.05)
        i = int(d * SR)
        if i < n:
            ir[i] += rng.uniform(-0.6, 0.6) * np.exp(-d * 6)
    tail = rng.standard_normal(n).astype(np.float32) * np.exp(-6.9 * t / rt60)
    tail[: int(predelay * SR)] = 0
    tail = lp(tail, 1500 + 9000 * bright, 1)
    ir += tail * 0.35
    ir[0] = 1.0
    return ir


def reverb(x, rt60=1.2, size=0.5, wet=0.3, bright=0.5, rng=None):
    rng = rng or np.random.default_rng(7)
    ir = impulse_response(rt60, size, rng, bright)
    w = signal.fftconvolve(x, ir)[: len(x) + len(ir) - 1].astype(np.float32)
    w /= (np.abs(w).max() + 1e-9)
    dry = np.concatenate([x, np.zeros(len(w) - len(x), dtype=np.float32)])
    y = (dry * (1 - wet) + w * wet * (np.abs(x).max() + 1e-9)).astype(np.float32)
    k = min(len(y), int(0.08 * SR))
    y[-k:] *= np.linspace(1, 0, k)
    return y


def glottal(dur, f0, rng, jitter=0.02, shimmer=0.1, open_q=0.6):
    """Voice source: a pulse train following an f0 curve (array or float), with jitter/shimmer."""
    n = int(dur * SR)
    if np.isscalar(f0):
        f0 = np.full(n, f0, dtype=np.float32)
    f0 = np.asarray(f0, dtype=np.float32)[:n]
    f0 = f0 * (1 + jitter * lp(rng.standard_normal(n).astype(np.float32), 30, 1) * 10)
    phase = np.cumsum(f0 / SR) % 1.0
    pulse = np.where(phase < open_q, np.sin(np.pi * phase / open_q) ** 2, 0.0).astype(np.float32)
    pulse = np.diff(pulse, prepend=0) * 20
    amp = 1 + shimmer * lp(rng.standard_normal(n).astype(np.float32), 20, 1) * 10
    return (pulse * amp).astype(np.float32)


def formants(src, f_list, q=6.0, gains=None):
    out = np.zeros_like(src)
    gains = gains or [1.0] * len(f_list)
    for f, g in zip(f_list, gains):
        out += peak(src, f, q, g)
    return out


def morph_formants(src, path, q=5.0, blocks=48):
    """Formants that change over time (vowel morph): path = list of (time_frac, [f1, f2, f3])."""
    n = len(src)
    out = np.zeros_like(src)
    edges = np.linspace(0, n, blocks + 1).astype(int)
    for i in range(blocks):
        a, b = edges[i], edges[i + 1]
        frac = (a + b) / 2 / n
        # interpolate formants
        for j in range(len(path) - 1):
            if path[j][0] <= frac <= path[j + 1][0]:
                k = (frac - path[j][0]) / max(1e-6, path[j + 1][0] - path[j][0])
                fs = [x + (y - x) * k for x, y in zip(path[j][1], path[j + 1][1])]
                break
        else:
            fs = path[-1][1]
        pad = 2048
        lo = max(0, a - pad)
        seg = formants(src[lo:b], fs, q)
        out[a:b] = seg[a - lo:]
    return out


def fade(x, fin=0.002, fout=0.01):
    n = len(x)
    a, b = int(fin * SR), int(fout * SR)
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)
    if b > 0 and b < n:
        x[-b:] *= np.linspace(1, 0, b)
    return x


def loopify(x, xfade=1.0):
    """Make a seamless loop by crossfading the end into the start."""
    n = int(xfade * SR)
    head, body, tail = x[:n], x[n:-n] if n else x, x[-n:]
    k = np.linspace(0, 1, n, dtype=np.float32)
    mixed = tail * (1 - k) + head * k
    return np.concatenate([mixed, body]).astype(np.float32)


def normalize(x, peak_db=-1.0):
    m = np.abs(x).max() + 1e-9
    return (x / m * (10 ** (peak_db / 20))).astype(np.float32)


def write(path, x, peak_db=-1.0):
    x = normalize(np.nan_to_num(x), peak_db)
    wavfile.write(path, SR, (x * 32767).astype(np.int16))


# ---------------------------------------------------------------------------------- physical models (v2)

def modal_ir(dur, freqs, decays, amps, rng=None):
    """Impulse response of a resonating object: decaying sines (no random phase, so no click)."""
    t = t_axis(dur)
    ir = np.zeros_like(t, dtype=np.float32)
    for f, d, a in zip(freqs, decays, amps):
        if f >= SR / 2 - 200:
            continue
        ir += (a * np.sin(2 * np.pi * f * t) * np.exp(-t / d)).astype(np.float32)
    return ir


def material_modes(kind, size, rng, count=None):
    """Mode frequencies / decays / amplitudes for a material. size scales pitch (bigger = lower)."""
    s = max(0.2, size)
    if kind == "sheet":       # thin steel sheet / shutter slat / duct: dense, inharmonic, long ringing
        n = count or 40
        f = np.sort(rng.uniform(60, 3500, n) / s)
        d = 0.9 * (f / 200.0) ** -0.55 * rng.uniform(0.6, 1.3, n)
        a = (f / 200.0) ** -0.4 * rng.uniform(0.3, 1.0, n)
    elif kind == "steel":     # thick steel (doors, frames): fewer, brighter, long
        n = count or 18
        base = rng.uniform(180, 320) / s
        f = base * np.array([1, 2.76, 5.4, 8.9, 13.3, 18.6, 24.8, 31.9, 40.0, 49.0, 58.8, 69.4, 80.9, 93.3, 106.6, 120.8, 135.9, 152.0])[:n]
        f *= rng.uniform(0.97, 1.03, n)
        d = 1.2 * (f / base) ** -0.5 * rng.uniform(0.7, 1.2, n)
        a = (f / base) ** -0.6
    elif kind == "wood":
        n = count or 10
        f = np.sort(rng.uniform(120, 1400, n) / s)
        d = 0.06 * (f / 200.0) ** -0.5 * rng.uniform(0.6, 1.4, n)
        a = rng.uniform(0.4, 1.0, n)
    elif kind == "ceramic":   # tile, porcelain
        n = count or 12
        f = np.sort(rng.uniform(900, 7000, n) / s)
        d = 0.025 * (f / 2000.0) ** -0.5 * rng.uniform(0.6, 1.3, n)
        a = rng.uniform(0.3, 1.0, n)
    elif kind == "glass":
        n = count or 12
        f = np.sort(rng.uniform(1200, 9000, n) / s)
        d = 0.12 * (f / 2000.0) ** -0.5 * rng.uniform(0.6, 1.3, n)
        a = rng.uniform(0.3, 1.0, n)
    elif kind == "concrete":
        n = count or 6
        f = np.sort(rng.uniform(80, 600, n) / s)
        d = 0.015 * rng.uniform(0.6, 1.4, n)
        a = rng.uniform(0.4, 1.0, n)
    else:                     # "body": a hollow / soft body (people, furniture)
        n = count or 6
        f = np.sort(rng.uniform(70, 400, n) / s)
        d = 0.03 * rng.uniform(0.6, 1.4, n)
        a = rng.uniform(0.5, 1.0, n)
    return f, d, a


def excitation(rng, hardness=0.5, dur=0.004):
    """The 'hit': a short burst whose brightness depends on how hard the two things are."""
    n = max(8, int(dur * SR))
    x = rng.standard_normal(n).astype(np.float32) * np.hanning(n * 2)[n:].astype(np.float32)
    cutoff = 800 + 12000 * hardness ** 2
    return lp(x, cutoff, 2)


def strike_object(rng, kind, size=1.0, hardness=0.5, level=1.0, dur=None, count=None, damp=1.0):
    f, d, a = material_modes(kind, size, rng, count)
    d = d * damp
    dur = dur or float(min(3.0, max(0.15, d.max() * 4)))
    ir = modal_ir(dur, f, d, a)
    ir /= (np.abs(ir).max() + 1e-9)
    exc = excitation(rng, hardness, rng.uniform(0.002, 0.006))
    y = signal.fftconvolve(exc, ir)[: len(ir)].astype(np.float32)
    # a little of the raw contact noise on top
    y[: len(exc)] += exc * 0.3 * hardness
    y = fade(y, 0.0, min(0.05, dur * 0.3))
    return (y / (np.abs(y).max() + 1e-9) * level).astype(np.float32)


def soften(x, top=9000.0):
    """Tame fizz: real recordings rarely have much above ~10 kHz at a distance."""
    return lp(x, top, 2)


def bubble(rng, f0=None, dur=None):
    """A single bubble popping in liquid: a sine whose pitch rises as it dies (Minnaert resonance)."""
    f0 = f0 or rng.uniform(250, 900)
    dur = dur or rng.uniform(0.02, 0.07)
    t = t_axis(dur)
    tau = dur / 3
    f = f0 * (1 + 0.6 * t / dur)
    return (np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / tau)).astype(np.float32)


def gurgle(dur, rng, rate=40, low=180, high=700, level=1.0):
    """Liquid in a throat: a dense, irregular stream of bubbles, some bigger and lower."""
    x = silence(dur + 0.1)
    tpos = 0.0
    while tpos < dur:
        b = bubble(rng, rng.uniform(low, high))
        place(x, b * rng.uniform(0.2, 1.0), tpos)
        tpos += rng.exponential(1.0 / rate)
    return lp(x[: int(dur * SR)], 3000) * level


def voice(dur, f0, rng, formant_path, jitter=0.03, shimmer=0.15, open_q=0.5, subharm=0.0, breath=0.1, q=5.0):
    """A voiced sound (groan, moan, scream). subharm adds period-doubling roughness (creaky, inhuman)."""
    g = glottal(dur, f0, rng, jitter, shimmer, open_q)
    if subharm > 0:
        n = len(g)
        f0a = np.asarray(f0, dtype=np.float32) if not np.isscalar(f0) else np.full(n, f0, np.float32)
        g2 = glottal(dur, f0a[:n] * 0.5, rng, jitter * 1.5, shimmer, open_q * 0.8)
        g = g + g2[:n] * subharm
    v = morph_formants(g, formant_path, q)
    air = lp(pink(dur, rng), 3500)
    air = morph_formants(air, formant_path, q * 0.6) * breath
    return (v + air[: len(v)]).astype(np.float32)
