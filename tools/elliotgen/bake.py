"""Texture baking in UV space: every texel knows its 3-D position, so detail is painted 'in the solid' (no seams)."""
import numpy as np
from scipy.ndimage import distance_transform_edt
from sdf import *
from elliot import H0


def rasterize(UV, F, size):
    """For every texel: triangle id and barycentric coords (texel centres; slightly conservative)."""
    tid = -np.ones((size, size), np.int64)
    bary = np.zeros((size, size, 3), np.float32)
    pu = UV[:, 0] * size - 0.5
    pv = UV[:, 1] * size - 0.5
    for t, (a, b, c) in enumerate(F):
        xs = np.array([pu[a], pu[b], pu[c]]); ys = np.array([pv[a], pv[b], pv[c]])
        x0, x1 = max(0, int(np.floor(xs.min() - 0.5))), min(size - 1, int(np.ceil(xs.max() + 0.5)))
        y0, y1 = max(0, int(np.floor(ys.min() - 0.5))), min(size - 1, int(np.ceil(ys.max() + 0.5)))
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1, dtype=float), np.arange(y0, y1 + 1, dtype=float))
        d = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
        if abs(d) < 1e-14:
            continue
        l0 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / d
        l1 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / d
        l2 = 1 - l0 - l1
        eps = -0.02
        m = (l0 >= eps) & (l1 >= eps) & (l2 >= eps)
        if not m.any():
            continue
        sub = tid[y0:y1 + 1, x0:x1 + 1]
        m &= sub < 0
        sub[m] = t
        bs = bary[y0:y1 + 1, x0:x1 + 1]
        bs[m] = np.stack([l0[m], l1[m], l2[m]], 1)
    return tid, bary


def dilate(img, filled):
    _, (iy, ix) = distance_transform_edt(~filled, return_indices=True)
    return img[iy, ix]


def interp(attr, F, tid, bary, mask):
    t = tid[mask]
    b = bary[mask]
    f = F[t]
    return attr[f[:, 0]] * b[:, 0:1] + attr[f[:, 1]] * b[:, 1:2] + attr[f[:, 2]] * b[:, 2:3]


# ---------------------------------------------------------------- detail height field (metres, outward)
def joints_folds(el, p):
    """Skin folds and creases over the knuckles, elbows, knees and the front of the neck."""
    h = np.zeros(len(p))
    for b in el.bones:
        nm = b.name
        if nm.startswith(("Finger", "Thumb")):
            ax = -b.rot[:, 1]
            r_w, lam, amp, dors = 0.011, 0.0021, 0.00028, -b.rot[:, 2]
        elif nm.startswith("ForeArm"):
            ax = -b.rot[:, 1]; r_w, lam, amp, dors = 0.04, 0.0045, 0.00035, -b.rot[:, 2]
        elif nm.startswith("Shin"):
            ax = np.array([0, -1.0, 0]); r_w, lam, amp, dors = 0.05, 0.005, 0.00035, np.array([0, 0, 1.0])
        else:
            continue
        rel = p - b.pos
        al = rel @ ax
        rad = np.linalg.norm(rel - np.outer(al, ax), axis=1)
        near = (np.abs(al) < 0.02) & (rad < r_w * 1.6)
        if not near.any():
            continue
        q = rel[near]
        side = np.clip((q - np.outer(al[near], ax)) @ dors / np.maximum(rad[near], 1e-6), 0, 1)
        wob = 0.0012 * perlin(p[near] * 300)
        h[near] += amp * np.cos(2 * np.pi * (al[near] + wob) / lam) * np.exp(-(al[near] / 0.0075) ** 2) * (0.3 + 0.7 * side)
    # neck rings, forehead lines, crow's feet and lines around the mouth
    hl = p - H0
    neck = smoothstep(1.98, 2.05, p[:, 1]) * (1 - smoothstep(2.2, 2.24, p[:, 1])) * smoothstep(0.0, 0.03, p[:, 2])
    neck *= smoothstep(-0.2, 0.4, fbm(p, 25, 2, seed=51))                        # creases come and go
    h += neck * 0.00016 * np.cos(2 * np.pi * (p[:, 1] + 0.006 * perlin(p * 45)) / (0.009 + 0.003 * perlin(p * 20 + 3)))
    fore = smoothstep(0.215, 0.23, hl[:, 1]) * (1 - smoothstep(0.26, 0.28, hl[:, 1])) * smoothstep(0.06, 0.085, hl[:, 2]) * (1 - smoothstep(0.035, 0.055, np.abs(hl[:, 0])))
    fore *= smoothstep(-0.3, 0.3, fbm(p, 30, 2, seed=52))                         # broken, uneven lines
    h += fore * 0.00013 * np.cos(2 * np.pi * (hl[:, 1] + 0.004 * perlin(p * 70)) / (0.0095 + 0.002 * perlin(p * 25 + 7)))
    r = np.hypot(np.abs(hl[:, 0]) - 0.059, hl[:, 1] - 0.075)
    corner = np.exp(-(r / 0.02) ** 2) * smoothstep(0.04, 0.07, hl[:, 2])
    ang = np.arctan2(hl[:, 1] - 0.075, np.abs(hl[:, 0]) - 0.059)
    h += corner * 0.0003 * np.cos(ang * 14 + 3 * perlin(p * 90))
    return h


def veins(p, hl):
    """Veins run mostly along the limbs and up the neck: stretch the noise along the body's long axis."""
    q = p * [1.0, 0.32, 1.0]
    v = ridged(q, 22, 3, seed=3, sharp=16)
    v2 = ridged(q, 55, 2, seed=11, sharp=20) * 0.55
    return np.maximum(v, v2) * smoothstep(-0.1, 0.35, fbm(p, 7, 2, seed=12))


def vein_region(p, masks):
    hand, tips, foot, shin, head, jaw, forearm, neck = masks.T
    hl = p - H0
    temple = head * np.exp(-((np.abs(hl[:, 0]) - 0.075) / 0.03) ** 2) * smoothstep(0.17, 0.2, hl[:, 1])
    chest = smoothstep(1.7, 1.85, p[:, 1]) * (1 - smoothstep(1.95, 2.0, p[:, 1])) * smoothstep(0.0, 0.06, p[:, 2])
    thigh = smoothstep(1.0, 1.2, p[:, 1]) * (1 - smoothstep(1.3, 1.4, p[:, 1])) * (np.abs(p[:, 0]) < 0.17)
    return np.clip(0.25 + 0.9 * hand + 0.75 * forearm + 0.9 * neck + 1.0 * temple + 0.6 * chest + 0.5 * thigh + 0.4 * foot, 0, 1.2)


def height(el, p, masks):
    hl = p - H0
    pores = 0.00011 * fbm(p, 700, 2, seed=2)
    crepe = 0.00018 * fbm(p * [1, 2.5, 1], 160, 3, seed=4)                   # crepey, thin skin
    vr = vein_region(p, masks)
    vh = 0.00055 * veins(p, hl) * vr
    return pores + crepe + vh + joints_folds(el, p)


def detail_normal(el, p, n, masks, eps=0.00035):
    """Perturbs the sculpt normal with the gradient of the fine detail height field."""
    g = np.zeros_like(p)
    for i in range(3):
        e = np.zeros(3); e[i] = eps
        g[:, i] = (height(el, p + e, masks) - height(el, p - e, masks)) / (2 * eps)
    g -= n * np.sum(g * n, 1, keepdims=True)
    out = n - g
    return out / np.linalg.norm(out, axis=1, keepdims=True)


def ambient_occlusion(model, p, n):
    ao = np.zeros(len(p))
    for i, h in enumerate((0.004, 0.009, 0.018, 0.035, 0.07)):
        d = model.evaluate(p + n * h, far=0.2)
        ao += (h - np.minimum(d, h)) / h * (0.5 ** i)
    return np.clip(1 - ao * 0.55, 0, 1)


# ---------------------------------------------------------------- colour
def mix(a, b, t):
    return a + (np.asarray(b, float) - a) * np.clip(t, 0, 1)[:, None]


def drips(p, starts, seed, front=True):
    """Dried blood that ran down from a set of starting points: thin wandering streaks, widest at the top."""
    m = np.zeros(len(p))
    rng = np.random.default_rng(seed)
    for (sx, sy, sz, length, width) in starts:
        L = length * rng.uniform(0.7, 1.3)
        dy = sy - p[:, 1]
        on = (dy > -0.004) & (dy < L) & (np.abs(p[:, 0] - sx) < 0.05)
        if front:
            on &= p[:, 2] > sz - 0.12
        if not on.any():
            continue
        q = p[on]
        t = np.clip((sy - q[:, 1]) / L, 0, 1)
        wander = 0.02 * perlin(np.stack([q[:, 1] * 14, np.full(len(q), sx * 31 + seed), np.zeros(len(q))], 1)) * (0.3 + t) \
            + 0.004 * perlin(np.stack([q[:, 1] * 60, np.full(len(q), sx * 7 + seed), np.ones(len(q))], 1))
        w = width * (1 - 0.75 * t) * (0.45 + 0.9 * np.abs(perlin(q * [40, 25, 40] + seed)))
        d = np.abs(q[:, 0] - sx - wander)
        drop = smoothstep(1.0, 0.92, t) + np.exp(-((t - 1) / 0.03) ** 2) * 0.6    # a bead where it stopped
        m[on] = np.maximum(m[on], smoothstep(w, w * 0.5, d) * drop)
    return m


def scratches(p, sets):
    """Sets of parallel claw marks (it does this to itself)."""
    m = np.zeros(len(p))
    for c, dirv, normal, length, gap in sets:
        c, dirv, normal = np.asarray(c, float), nrm(dirv), nrm(normal)
        side = nrm(np.cross(normal, dirv))
        rel = p - c
        a = rel @ dirv
        near = (np.abs(a) < length / 2) & (np.abs(rel @ normal) < 0.03)
        if not near.any():
            continue
        q = rel[near]
        for k in range(-1, 3):
            off = (q @ side) - k * gap - 0.002 * np.sin(a[near] * 40 + k)
            taper = 1 - np.abs(a[near]) / (length / 2)
            w = 0.0012 + 0.0013 * taper
            m[near] = np.maximum(m[near], smoothstep(w, w * 0.35, np.abs(off)) * smoothstep(0, 0.25, taper))
    return m


def albedo(el, p, n, tags, masks, ao, bloody=False):
    T = dict(zip(["skin", "nail", "mouth", "lip", "socket", "nostril", "ear"], tags.T))
    hand, tips, foot, shin, head, jaw, forearm, neck = masks.T
    hl = p - H0
    N = len(p)
    base = np.array([0.6, 0.58, 0.54])                                              # grey, bloodless
    col = np.tile(base, (N, 1))
    m1 = fbm(p, 5, 4, seed=1)
    col = mix(col, [0.5, 0.45, 0.5], smoothstep(0.05, 0.45, m1) * 0.65)           # bruised grey-violet patches
    col = mix(col, [0.64, 0.6, 0.44], smoothstep(0.0, -0.4, m1) * 0.45)           # sallow yellow
    m3 = fbm(p, 11, 3, seed=17)
    col = mix(col, [0.47, 0.5, 0.45], smoothstep(0.1, 0.5, m3) * 0.4)             # greenish-grey, like something dead
    thin = np.clip(masks[:, 1] * 0.8 + np.clip(tags[:, 6] * 2, 0, 1), 0, 1)
    col = mix(col, [0.62, 0.45, 0.42], thin * 0.5)                                 # blood under thin skin (fingers, ears)
    cav = 1 - ao
    col = mix(col, [0.36, 0.3, 0.3], smoothstep(0.08, 0.5, cav) * 0.6)            # darker, purpler in the creases
    m2 = fbm(p, 22, 3, seed=7)
    col = mix(col, [0.46, 0.39, 0.33], smoothstep(0.32, 0.45, m2) * 0.65)         # blotches / liver spots
    fine = fbm(p, 90, 2, seed=9)
    col *= (1 + 0.06 * fine)[:, None]
    # veins showing through thin skin
    vr = vein_region(p, masks)
    v = veins(p, hl) * vr
    col = mix(col, [0.4, 0.42, 0.5], smoothstep(0.3, 0.9, v) * 0.55)
    # flushed knuckles, elbows, knees, ears; red rims round the nose and eyes
    red = np.zeros(N)
    for b in el.bones:
        if b.name.startswith(("Finger", "Thumb", "ForeArm", "Shin")):
            r = np.linalg.norm(p - b.pos, axis=1)
            red = np.maximum(red, np.exp(-(r / (0.012 if b.name[0] in "FT" and not b.name.startswith("Fore") else 0.045)) ** 2))
    red = np.maximum(red, T["ear"] * 0.8)
    red = np.maximum(red, head * np.exp(-((np.hypot(hl[:, 0], (hl[:, 1] - 0.127) * 1.5)) / 0.02) ** 2) * smoothstep(0.09, 0.11, hl[:, 2]) * 0.7)
    col = mix(col, [0.6, 0.38, 0.37], red * 0.55)
    # the eye sockets: dark, bruised, sunken
    sock = np.zeros(N)
    for s in (-1, 1):
        ey = 0.184 if s < 0 else 0.177
        r = np.linalg.norm((hl - [s * 0.034, ey, 0.09]) * [1, 1.2, 0.7], axis=1)
        sock = np.maximum(sock, np.exp(-(r / 0.03) ** 2))
    col = mix(col, [0.3, 0.22, 0.25], np.maximum(sock * head * 0.8, T["socket"]))
    col = mix(col, [0.36, 0.28, 0.3], T["lip"])
    col = mix(col, [0.17, 0.035, 0.035], T["mouth"])
    col = mix(col, [0.12, 0.06, 0.06], T["nostril"])
    # nails: dark, ridged, yellow at the tips
    nailc = mix(np.tile([0.15, 0.12, 0.09], (N, 1)), [0.42, 0.37, 0.25], smoothstep(0.2, 0.8, fbm(p * [1, 3, 1], 300, 2, seed=5)) * 0.5)
    col = mix(col, nailc, smoothstep(0.3, 0.7, T["nail"]))
    # grime: hands, fingertips, feet and shins - it crawls and lives in the dark
    dirt = np.clip(0.55 * hand + 0.45 * tips + 0.85 * foot + 0.55 * shin * smoothstep(0.6, 0.15, p[:, 1]), 0, 1)
    dn = fbm(p, 30, 4, seed=13) * 0.5 + 0.5
    dmask = smoothstep(0.35, 0.65, dn + dirt * 0.6 - 0.25) * np.clip(dirt * 1.4, 0, 1)
    smudge = smoothstep(0.42, 0.5, fbm(p, 9, 4, seed=21)) * 0.6
    dmask = np.maximum(dmask, smudge * smoothstep(0.3, 0.6, fbm(p, 60, 2, seed=22) + 0.5))
    col = mix(col, [0.24, 0.2, 0.16], dmask * 0.85)
    # scratches
    sc = scratches(p, [([0.06, 1.78, 0.1], [0.4, -1, 0.15], [0.3, 0.1, 1], 0.16, 0.012),
                       ([-0.5, 1.45, 0.02], [0.45, 1, 0], [0, 0, 1], 0.12, 0.009),
                       ([0.08, 1.85, -0.1], [-0.5, -1, 0], [0, 0, -1], 0.2, 0.014),
                       ([-0.13, 1.15, 0.05], [0.1, -1, 0.2], [0, 0, 1], 0.14, 0.01)])
    col = mix(col, [0.55, 0.3, 0.29], sc * 0.85)
    # dried blood from the mouth, down the chin, the throat and the chest
    corners = [(-0.05, H0[1] + 0.07, 0.08, 0.3, 0.0035), (0.052, H0[1] + 0.068, 0.08, 0.45, 0.004),
               (0.004, H0[1] + 0.03, 0.1, 0.22, 0.0045), (0.028, H0[1] + 0.045, 0.1, 0.38, 0.003)]
    if bloody:
        corners += [(-0.032, H0[1] + 0.06, 0.08, 0.55, 0.008), (0.018, H0[1] + 0.055, 0.08, 0.75, 0.006), (0.045, H0[1] + 0.065, 0.08, 0.4, 0.005)]
        corners += [(-0.045, 1.97, 0.07, 0.32, 0.006), (0.06, 1.95, 0.07, 0.22, 0.005)]
    bl = drips(p, corners, 3)
    mouth_stain = head * np.exp(-(np.hypot(hl[:, 0] / 2.2, (hl[:, 1] - 0.072) * 1.6) / (0.014 if not bloody else 0.035)) ** 2) * smoothstep(0.06, 0.095, hl[:, 2])
    bl = np.maximum(bl, mouth_stain * (0.6 + 0.4 * (fbm(p, 120, 2, seed=31) > 0)))
    spat = smoothstep(0.38, 0.5, fbm(p, 45, 3, seed=33)) * np.clip(hand + forearm * 0.6, 0, 1) * (0.5 if not bloody else 1.0)
    bl = np.maximum(bl, spat)
    if bloody:
        # smeared where it wiped its mouth and chest with its hands; chin and throat soaked
        chin = head * smoothstep(0.075, 0.02, hl[:, 1]) * smoothstep(0.03, 0.08, hl[:, 2]) * smoothstep(0.1, 0.35, fbm(p, 35, 3, seed=61) + 0.3)
        smear = smoothstep(0.12, 0.45, fbm(p * [1, 0.45, 1], 14, 3, seed=63)) * smoothstep(1.7, 1.85, p[:, 1]) * (1 - smoothstep(2.0, 2.08, p[:, 1])) * smoothstep(0.0, 0.07, p[:, 2])
        throat = smoothstep(2.0, 2.06, p[:, 1]) * (1 - smoothstep(2.2, 2.24, p[:, 1])) * smoothstep(0.0, 0.03, p[:, 2]) * smoothstep(0.0, 0.4, fbm(p, 25, 3, seed=65) + 0.2)
        bl = np.maximum(bl, np.maximum(chin * 0.9, np.maximum(smear * 0.6, throat * 0.7)))
    if bloody:
        bl = np.maximum(bl, np.clip(hand * 1.2, 0, 1) * smoothstep(0.2, 0.5, fbm(p, 20, 3, seed=35) + 0.4))
    dried = np.array([0.2, 0.07, 0.05])
    fresh = np.array([0.26, 0.025, 0.025])
    age = smoothstep(-0.2, 0.4, fbm(p, 40, 2, seed=39))                         # some fresh and wet, some dried brown
    bcol = mix(np.tile(fresh, (N, 1)), dried, age * 0.8) if bloody else np.tile(dried, (N, 1))
    col = col + (bcol - col) * (bl * (0.7 + 0.25 * (fbm(p, 200, 2, seed=37) > 0)))[:, None]
    col *= (0.45 + 0.55 * ao)[:, None]
    # smoothness: oily skin, wet mouth, eyes and blood; dry grime
    sm = 0.33 + 0.1 * fbm(p, 12, 3, seed=40)
    sweat = head * smoothstep(0.2, 0.26, hl[:, 1]) * 0.15 + smoothstep(1.7, 1.9, p[:, 1]) * smoothstep(0.0, 0.08, p[:, 2]) * 0.1
    sm = sm + sweat
    sm = sm * (1 - dmask * 0.6)
    sm = np.maximum(sm, T["mouth"] * 0.85)
    sm = np.maximum(sm, T["lip"] * 0.6)
    sm = np.maximum(sm, T["socket"] * 0.55)
    sm = np.where(T["nail"] > 0.5, 0.5, sm)
    sm = sm * (1 - bl) + bl * ((0.42 if not bloody else 0.62) - (0.2 * age if bloody else 0))
    return np.clip(col, 0, 1), np.clip(sm, 0, 1)
