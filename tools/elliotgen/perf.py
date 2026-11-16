"""Elliot's 'performances': for every take of every Elliot sound, a timed track of how his body moves while it plays.

Each WAV is analysed (loudness, pitch brightness, low-end weight, onsets) and turned into movement by a style for
that kind of sound - a scream throws the head back then lunges with the arms flung out; a sniff flares the nostrils
and lifts the head; clicks tick the jaw; feeding chews and shakes the head... Every variation gets its own track
(timed to its own audio), and the random parts are seeded by the take name so each take moves differently.

Output: one binary file both the game and the viewer read.
"""
import os, sys, glob, struct, zlib
import numpy as np
from scipy.io import wavfile
from scipy.signal import stft, find_peaks

FPS = 30
CH = ["jaw", "lip", "nose", "brow", "throat", "chest", "tongue", "hp", "hy", "hr", "push", "arch", "sh", "arms", "curl", "splay", "shake"]
# scales the game / viewer apply: unit channels 0..1 (or -1..1); angles in units of 40 deg; push of 0.15 m; arch 30 deg
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
AUDIO = os.path.join(ROOT, "Assets/AfterHours/Resources/Audio/creature")
OUT = os.path.join(ROOT, "Assets/AfterHours/Resources/Creature/elliot_perf.bytes")


def follow(x, up, down):
    y = np.zeros_like(x)
    v = 0.0
    for i, s in enumerate(x):
        v += (s - v) * (up if s > v else down)
        y[i] = v
    return y


def analyse(path):
    sr, x = wavfile.read(path)
    x = x.astype(np.float64)
    if x.ndim > 1:
        x = x.mean(1)
    x /= 32768.0
    hop = sr // FPS
    n = int(np.ceil(len(x) / hop)) + 2
    f, t, Z = stft(x, sr, nperseg=2048, noverlap=2048 - hop, boundary="even", padded=True)
    P = np.abs(Z) ** 2
    P = P[:, :n] if P.shape[1] >= n else np.pad(P, ((0, 0), (0, n - P.shape[1])))
    E = P.sum(0) + 1e-12
    rms = np.sqrt(E / 1024)
    peak = np.percentile(rms, 99.5) + 1e-9
    env = np.clip(rms / peak, 0, 1)
    env = np.where(env < 0.04, 0, env)
    cen = (f[:, None] * P).sum(0) / E
    bright = np.clip((np.log2(np.maximum(cen, 50)) - np.log2(300)) / (np.log2(3500) - np.log2(300)), 0, 1)
    low = P[f < 260].sum(0) / E
    flux = np.maximum(np.diff(np.log1p(P * 1e4), axis=1, prepend=0), 0).sum(0)
    flux = flux / (np.percentile(flux, 99) + 1e-9)
    on, _ = find_peaks(flux, height=0.35, distance=3)
    on = on[env[np.minimum(on + 1, n - 1)] > 0.08]
    if len(on) == 0:
        on, _ = find_peaks(env, height=0.25, distance=2)
    if len(on) == 0:
        on = np.array([int(np.argmax(env))])
    return dict(env=env, bright=bright, low=low, onsets=on, n=n, loud=float(np.sqrt(np.mean(x ** 2))) , dur=len(x) / sr)


def pulse(n, at, width, shape="decay"):
    """1 at each frame in 'at', decaying over 'width' frames."""
    y = np.zeros(n)
    k = np.arange(n)
    for a in at:
        d = k - a
        if shape == "decay":
            y = np.maximum(y, np.where(d >= 0, np.exp(-d / max(width, 1e-3)), np.where(d > -2, 0.6, 0)))
        else:
            y = np.maximum(y, np.exp(-(d / width) ** 2))
    return y


def perform(kind, a, rng):
    n = a["n"]
    env, br, low, on = a["env"], a["bright"], a["low"], a["onsets"]
    C = {c: np.zeros(n) for c in CH}
    t = np.arange(n) / FPS
    sm = lambda v, up=0.5, dn=0.15: follow(v, up, dn)
    voiced = sm(env, 0.6, 0.12)
    side = rng.choice([-1, 1])

    if kind in ("scream", "spot", "howl"):
        start = int(np.argmax(env > 0.25)) if (env > 0.25).any() else 0
        pre = np.clip((np.arange(n) - (start - 12)) / 12, 0, 1) * (np.arange(n) < start + 2)
        main = voiced * (np.arange(n) >= start - 1)
        C["chest"] = sm(pre * 0.9 - main * 0.6, 0.3, 0.08)
        C["jaw"] = np.clip(main ** 0.6 * 1.05, 0, 1)
        C["lip"] = np.clip(main * 1.3, 0, 1) * 0.9
        C["brow"] = sm(main, 0.4, 0.05)
        C["hp"] = sm(-pre * 0.35 + main * (0.15 - 0.95 * br), 0.25, 0.06)
        C["push"] = sm(main * (0.8 if kind != "howl" else 0.4), 0.35, 0.05)
        C["sh"] = sm(main * 0.8 + pre * 0.4, 0.3, 0.05)
        C["arms"] = sm(main * (1.0 if kind != "spot" else 0.5), 0.2, 0.04)
        C["splay"] = sm(main, 0.3, 0.05)
        C["shake"] = main * 0.45
        C["hr"] = sm(main * side * 0.25, 0.1, 0.05)
        C["throat"] = main * 0.8
        C["arch"] = sm(-pre * 0.3 + main * 0.25, 0.2, 0.05)
        if kind == "howl":
            C["hp"] = sm(main * (0.1 - 0.85 * np.clip(t / max(a["dur"] * 0.7, 0.1), 0, 1)), 0.2, 0.05)
            C["arms"] *= 0.6
    elif kind == "shriek":
        C["jaw"] = np.clip(voiced ** 0.6, 0, 1)
        jerk = pulse(n, on[:1] if len(on) else [0], 10)
        C["hp"] = sm(-jerk * 0.55 - voiced * br * 0.3, 0.6, 0.12)
        C["hy"] = sm(jerk * side * 0.3, 0.6, 0.1)
        C["lip"] = voiced * 0.8; C["brow"] = voiced
        C["sh"] = sm(voiced * 0.9, 0.5, 0.1); C["arms"] = sm(voiced * 0.45, 0.3, 0.06)
        C["curl"] = sm(voiced * 0.8, 0.5, 0.08); C["shake"] = voiced * 0.55; C["throat"] = voiced
        C["chest"] = -voiced * 0.4
    elif kind == "wail":
        slow = sm(env, 0.08, 0.03)
        C["jaw"] = np.clip(slow * 0.85, 0, 1)
        C["hp"] = -slow * 0.75
        C["hr"] = side * slow * 0.45 + 0.15 * np.sin(t * 0.9) * slow
        C["hy"] = 0.2 * np.sin(t * 0.6 + rng.uniform(0, 6)) * slow
        C["arms"] = slow * 0.25; C["splay"] = slow * 0.5; C["chest"] = -slow * 0.3
        C["throat"] = slow * 0.6; C["arch"] = -slow * 0.35
    elif kind == "moan":
        slow = sm(env, 0.06, 0.03)
        C["jaw"] = slow * 0.45
        C["hp"] = slow * 0.4
        C["hr"] = (side * 0.35 + 0.25 * np.sin(t * 0.7)) * slow
        C["hy"] = 0.15 * np.sin(t * 0.5 + 1) * slow
        C["sh"] = -slow * 0.3; C["arch"] = slow * 0.3; C["chest"] = -slow * 0.25; C["throat"] = slow * 0.5
    elif kind == "roar":
        C["jaw"] = np.clip(voiced ** 0.5 * 1.05, 0, 1)
        C["lip"] = np.clip(voiced * 1.5, 0, 1); C["brow"] = np.clip(voiced * 1.4, 0, 1)
        C["push"] = sm(voiced, 0.4, 0.06); C["arch"] = sm(voiced * 0.45, 0.3, 0.05); C["hp"] = sm(voiced * 0.3, 0.3, 0.05)
        C["sh"] = sm(voiced, 0.4, 0.06); C["arms"] = sm(voiced * 0.55, 0.25, 0.05); C["curl"] = sm(voiced, 0.4, 0.06)
        C["shake"] = voiced * 0.35 * (0.5 + low); C["throat"] = voiced; C["chest"] = -voiced * 0.6
    elif kind == "snarl":
        jerks = pulse(n, on, 5)
        C["jaw"] = voiced * 0.6; C["lip"] = np.clip(voiced * 2, 0, 1); C["brow"] = np.clip(voiced * 2, 0, 1)
        yaw = np.zeros(n); pit = np.zeros(n)
        for o in on:
            yaw += pulse(n, [o], 6) * rng.uniform(-0.4, 0.4)
            pit += pulse(n, [o], 6) * rng.uniform(0.05, 0.3)
        C["hy"] = sm(yaw, 0.7, 0.2); C["hp"] = sm(pit, 0.7, 0.2); C["push"] = sm(jerks * 0.6, 0.7, 0.15)
        C["sh"] = sm(voiced * 0.7, 0.5, 0.1); C["curl"] = np.clip(voiced * 1.5, 0, 1); C["shake"] = voiced * 0.3
        C["throat"] = voiced; C["chest"] = -jerks * 0.3
    elif kind == "growl":
        C["jaw"] = voiced * 0.3; C["lip"] = np.clip(voiced * 1.2, 0, 1) * 0.8; C["brow"] = voiced * 0.9
        C["hp"] = sm(voiced * 0.35, 0.15, 0.05); C["hy"] = side * 0.12 * voiced
        C["throat"] = voiced * (0.6 + 0.4 * np.sin(t * 2 * np.pi * 9)); C["sh"] = voiced * 0.35; C["curl"] = voiced * 0.5
        C["chest"] = -voiced * 0.35; C["arch"] = voiced * 0.2
    elif kind == "croak":
        rate = rng.uniform(5, 9)
        C["jaw"] = voiced * 0.22
        C["throat"] = voiced * (0.5 + 0.5 * np.sin(t * 2 * np.pi * rate) ** 2)
        C["hr"] = side * sm(voiced, 0.1, 0.04) * 0.55
        C["hp"] = -sm(voiced, 0.1, 0.04) * 0.15
        C["chest"] = -voiced * 0.2
    elif kind == "clicks":
        ticks = pulse(n, on, 1.5)
        C["jaw"] = ticks * 0.18; C["throat"] = ticks * 0.6
        yaw = np.zeros(n); rol = np.zeros(n)
        for o in on[::2]:
            yaw += pulse(n, [o], 3) * rng.uniform(-0.12, 0.12)
            rol += pulse(n, [o], 3) * rng.uniform(-0.12, 0.12)
        C["hy"] = yaw; C["hr"] = rol + side * 0.15 * sm(env, 0.1, 0.03)
    elif kind == "mutter":
        syl = pulse(n, on, 3)
        C["jaw"] = np.clip(voiced * 0.35 + syl * 0.15, 0, 0.6); C["lip"] = syl * 0.25
        yaw = np.zeros(n); rol = np.zeros(n); pit = np.zeros(n)
        for o in on:
            g = pulse(n, [o], 8)
            yaw += g * rng.uniform(-0.1, 0.1); rol += g * rng.uniform(-0.18, 0.18); pit += g * rng.uniform(-0.05, 0.12)
        C["hy"] = sm(yaw, 0.4, 0.15); C["hr"] = sm(rol, 0.4, 0.15) + side * 0.2 * sm(env, 0.1, 0.03); C["hp"] = sm(pit, 0.4, 0.15) + 0.15 * sm(env, 0.1, 0.03)
        C["sh"] = sm(env, 0.1, 0.03) * 0.25; C["arch"] = sm(env, 0.1, 0.03) * 0.25; C["throat"] = voiced * 0.5
        C["curl"] = sm(env, 0.1, 0.03) * 0.3 + 0.2 * np.sin(t * 7) * sm(env, 0.1, 0.03)
    elif kind == "whine":
        slow = sm(env, 0.1, 0.04)
        C["jaw"] = slow * 0.3; C["hr"] = side * slow * 0.6; C["hp"] = slow * (0.2 - 0.3 * br)
        C["brow"] = -slow * 0.6; C["sh"] = slow * 0.55; C["curl"] = slow * 0.45; C["shake"] = slow * 0.25
        C["throat"] = slow * 0.7; C["arch"] = slow * 0.25
    elif kind == "laugh":
        bursts = pulse(n, on, 3)
        C["jaw"] = np.clip(voiced * 0.7, 0, 1); C["sh"] = sm(bursts, 0.8, 0.25) * 0.8
        C["hp"] = -sm(bursts, 0.8, 0.25) * 0.25 + 0.1 * sm(env, 0.1, 0.03); C["chest"] = -sm(bursts, 0.8, 0.3) * 0.5
        C["hr"] = side * 0.3 * sm(env, 0.1, 0.03); C["lip"] = voiced * 0.6; C["throat"] = bursts * 0.8
        C["shake"] = voiced * 0.2; C["splay"] = sm(env, 0.1, 0.03) * 0.3
    elif kind == "call":
        slow = sm(env, 0.25, 0.06)
        C["jaw"] = np.clip(slow * 0.75, 0, 1); C["hp"] = -sm(env, 0.08, 0.02) * 0.45; C["push"] = slow * 0.35
        C["throat"] = slow * 0.7; C["chest"] = -slow * 0.4; C["hy"] = side * sm(env, 0.05, 0.02) * 0.2
    elif kind == "huff":
        bursts = pulse(n, on, 4)
        pre = np.zeros(n)
        for o in on:
            pre = np.maximum(pre, pulse(n, [max(0, o - 3)], 2, "gauss"))
        C["nose"] = np.clip(pre + bursts * 0.7, 0, 1); C["chest"] = sm(pre * 0.6 - bursts * 0.7, 0.5, 0.15)
        C["push"] = bursts * 0.3; C["hp"] = bursts * 0.1; C["sh"] = sm(env, 0.2, 0.05) * 0.35
        C["lip"] = bursts * 0.4; C["curl"] = sm(env, 0.2, 0.05) * 0.6
    elif kind == "feed":
        slow = sm(env, 0.2, 0.05)
        chew = rng.uniform(3, 4.5)
        C["jaw"] = slow * (0.35 + 0.35 * np.maximum(0, np.sin(t * 2 * np.pi * chew)))
        C["hp"] = slow * 0.85; C["push"] = slow * 0.55
        C["hy"] = slow * 0.35 * np.sin(t * 2 * np.pi * rng.uniform(2, 3)) * (0.5 + 0.5 * voiced)
        C["lip"] = slow * 0.9; C["brow"] = slow * 0.8; C["curl"] = slow; C["arch"] = slow * 0.6
        C["tongue"] = slow * 0.3 * np.maximum(0, np.sin(t * 2 * np.pi * chew + 1)); C["throat"] = slow * 0.7
        C["shake"] = slow * 0.2
    elif kind == "sniff":
        for i, o in enumerate(on):
            C["nose"] = np.maximum(C["nose"], pulse(n, [o], 3))
        lift = sm(env, 0.3, 0.04)
        C["hp"] = -lift * 0.3
        scan = np.zeros(n)
        for i, o in enumerate(on):
            scan += pulse(n, [o], 10, "gauss") * side * (0.08 * i - 0.12)
        C["hy"] = sm(scan, 0.3, 0.1); C["chest"] = sm(pulse(n, on, 4) * 0.4, 0.6, 0.15); C["push"] = lift * 0.2
    elif kind == "twitch":
        for o in on:
            g = pulse(n, [o], 4)
            C["hy"] += g * rng.uniform(-0.65, 0.65); C["hr"] += g * rng.uniform(-0.5, 0.5); C["hp"] += g * rng.uniform(-0.3, 0.3)
            C["arms"] += g * rng.uniform(0, 0.25); C["curl"] = np.maximum(C["curl"], g * rng.uniform(0.3, 1)); C["sh"] += g * rng.uniform(-0.3, 0.6)
    elif kind in ("breath_calm", "breath_hunt"):
        inh = np.where(br > np.median(br[env > 0.05]) if (env > 0.05).any() else br > 0.5, env, 0)
        exh = env - inh
        C["chest"] = np.clip(follow(inh * 1.6 - exh * 0.9, 0.12, 0.05), -1, 1)
        C["nose"] = np.clip(sm(inh, 0.3, 0.1) * 1.2, 0, 1)
        C["jaw"] = sm(exh, 0.2, 0.1) * (0.12 if kind == "breath_calm" else 0.25)
        C["sh"] = C["chest"] * 0.25; C["throat"] = sm(exh, 0.3, 0.1) * 0.4
        if kind == "breath_hunt":
            C["lip"] = sm(exh, 0.2, 0.1) * 0.4
    # ease in/out so a take never snaps
    fade = np.minimum(1, np.minimum(np.arange(n) / 3.0, (n - 1 - np.arange(n)) / 6.0 + 0.0))
    fade = np.clip(fade, 0, 1)
    for c in CH:
        if kind.startswith("breath"):
            continue
        C[c] = C[c] * fade
    return np.stack([np.clip(C[c], -1, 1) for c in CH], 0)


def kind_of(name):
    k = name[len("elliot_"):].rsplit("_", 1)[0]
    return k


def main():
    files = sorted(glob.glob(os.path.join(AUDIO, "elliot_*.wav")))
    files = [f for f in files if "_step_" not in f]
    entries = []
    for f in files:
        name = os.path.splitext(os.path.basename(f))[0]
        kind = kind_of(name)
        a = analyse(f)
        rng = np.random.default_rng(zlib.crc32(name.encode()))
        tr = perform(kind, a, rng)
        entries.append((name, tr))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "wb") as fo:
        fo.write(b"EPF1")
        fo.write(struct.pack("<iii", FPS, len(CH), len(entries)))
        for c in CH:
            b = c.encode(); fo.write(struct.pack("<i", len(b))); fo.write(b)
        for name, tr in entries:
            b = name.encode(); fo.write(struct.pack("<i", len(b))); fo.write(b)
            fo.write(struct.pack("<i", tr.shape[1]))
            fo.write(np.round(tr.T * 32000).astype("<i2").tobytes())   # frame-major: all channels of frame 0, then 1...
    print(f"{len(entries)} performances -> {OUT} ({os.path.getsize(OUT) // 1024} KB)")
    kinds = sorted(set(kind_of(n) for n, _ in entries))
    print("kinds:", kinds)


if __name__ == "__main__":
    main()
