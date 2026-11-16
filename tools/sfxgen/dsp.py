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
    i = int(at_sec * SR)
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
    return (dry * (1 - wet) + w * wet * (np.abs(x).max() + 1e-9)).astype(np.float32)


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
