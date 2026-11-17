#!/usr/bin/env python3
"""
AFTER HOURS - procedural texture generator.

Creates tileable PBR textures (albedo, normal, mask) and alpha decals for the mall.
Output: Assets/AfterHours/Textures/Generated/

  <name>_A.jpg   albedo (sRGB)
  <name>_N.png   normal map (OpenGL / Y+)
  <name>_M.png   mask: R = metallic, G = ambient occlusion, A = smoothness (linear)
  decal_<name>.png  RGBA decal (sRGB colour + alpha)

Everything is periodic (tiles seamlessly) because all noise is made with FFTs.
Run:  python3 tools/texgen/generate.py
"""
import os
import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "AfterHours", "Textures", "Generated")
os.makedirs(OUT, exist_ok=True)


# ----------------------------------------------------------------------------- noise

def fnoise(n, beta=2.0, seed=0, lo=1.0):
    """Periodic fractal noise in [0,1]. beta: spectral slope (higher = smoother)."""
    rng = np.random.default_rng(seed)
    white = rng.standard_normal((n, n))
    f = np.fft.fft2(white)
    fy = np.fft.fftfreq(n)[:, None] * n
    fx = np.fft.fftfreq(n)[None, :] * n
    r = np.sqrt(fx * fx + fy * fy)
    r[0, 0] = 1.0
    amp = 1.0 / np.power(np.maximum(r, lo), beta / 2.0)
    amp[0, 0] = 0.0
    out = np.real(np.fft.ifft2(f * amp))
    out -= out.min()
    out /= max(out.max(), 1e-9)
    return out


def band(n, f0, f1, seed=0):
    """Periodic noise containing only frequencies f0..f1 (cycles per texture)."""
    rng = np.random.default_rng(seed)
    f = np.fft.fft2(rng.standard_normal((n, n)))
    fy = np.fft.fftfreq(n)[:, None] * n
    fx = np.fft.fftfreq(n)[None, :] * n
    r = np.sqrt(fx * fx + fy * fy)
    out = np.real(np.fft.ifft2(f * ((r >= f0) & (r <= f1))))
    out -= out.min()
    out /= max(out.max(), 1e-9)
    return out


def aniso(n, stretch, beta=2.0, seed=0):
    """Periodic noise stretched along X (streaks), e.g. wood fibres, brushed marks."""
    rng = np.random.default_rng(seed)
    f = np.fft.fft2(rng.standard_normal((n, n)))
    fy = np.fft.fftfreq(n)[:, None] * n
    fx = np.fft.fftfreq(n)[None, :] * n
    r = np.sqrt((fx * stretch) ** 2 + fy * fy)
    r[0, 0] = 1.0
    amp = 1.0 / np.power(np.maximum(r, 1.0), beta / 2.0)
    amp[0, 0] = 0.0
    out = np.real(np.fft.ifft2(f * amp))
    out -= out.min()
    out /= max(out.max(), 1e-9)
    return out


def cracks(n, count, seed, length=(0.25, 0.6), width=(0.8, 2.2), branch=0.35):
    """Thin, mostly straight wandering cracks with occasional branches (periodic wrap)."""
    rng = np.random.default_rng(seed)
    img = Image.new("L", (n, n), 0)
    from PIL import ImageDraw
    d = ImageDraw.Draw(img)

    def walk(x, y, ang, steps, w):
        pts = [(x, y)]
        for _ in range(steps):
            ang += rng.normal(0, 0.25)
            x += np.cos(ang) * 6
            y += np.sin(ang) * 6
            pts.append((x, y))
            if rng.random() < branch * 0.05 and w > 0.9:
                walk(x, y, ang + rng.choice([-1, 1]) * rng.uniform(0.5, 1.1), int(steps * 0.3), w * 0.6)
        for ox in (-n, 0, n):
            for oy in (-n, 0, n):
                d.line([(px + ox, py + oy) for px, py in pts], fill=int(255 * min(1, w / 2)), width=max(1, int(round(w))))

    for _ in range(count):
        walk(rng.random() * n, rng.random() * n, rng.uniform(0, 2 * np.pi), int(rng.uniform(*length) * n / 6), rng.uniform(*width))
    return np.asarray(img, dtype=np.float64) / 255.0


def blur(img, radius):
    """Periodic box-ish blur via FFT gaussian."""
    n = img.shape[0]
    fy = np.fft.fftfreq(n)[:, None]
    fx = np.fft.fftfreq(n)[None, :]
    g = np.exp(-(fx * fx + fy * fy) * (np.pi * radius) ** 2 * 2.0)
    if img.ndim == 2:
        return np.real(np.fft.ifft2(np.fft.fft2(img) * g))
    return np.stack([np.real(np.fft.ifft2(np.fft.fft2(img[..., c]) * g)) for c in range(img.shape[2])], -1)


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def normal_from_height(h, strength):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    nx = -dx * strength
    ny = dy * strength  # image rows go down; OpenGL normal maps want +Y up
    nz = np.ones_like(h)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / l, ny / l, nz / l], -1)


def save_set(name, albedo, height, strength, smooth, metal=None, ao=None):
    n = albedo.shape[0]
    a = (np.clip(albedo, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(a, "RGB").save(os.path.join(OUT, name + "_A.jpg"), quality=92)
    nm = normal_from_height(height, strength)
    Image.fromarray(((nm * 0.5 + 0.5) * 255).astype(np.uint8), "RGB").save(os.path.join(OUT, name + "_N.png"))
    full = lambda v, d: np.broadcast_to(np.asarray(d if v is None else v, dtype=np.float64), (n, n))
    metal, ao, smooth = full(metal, 0.0), full(ao, 1.0), full(smooth, 0.5)
    m = np.stack([metal, ao, np.zeros((n, n)), smooth], -1)
    Image.fromarray((np.clip(m, 0, 1) * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, name + "_M.png"))
    print("  ", name)


def save_decal(name, rgb, alpha):
    img = np.concatenate([np.clip(rgb, 0, 1), np.clip(alpha, 0, 1)[..., None]], -1)
    Image.fromarray((img * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, "decal_" + name + ".png"))
    print("   decal_" + name)


def col(rgb):
    return np.array(rgb, dtype=np.float64) / 255.0


def tint(gray, c):
    return gray[..., None] * c[None, None, :]


def coords(n):
    y, x = np.mgrid[0:n, 0:n].astype(np.float64)
    return x, y


# ----------------------------------------------------------------------------- tiled helpers

def tile_grid(n, tiles, grout_px, seed, bevel_px=3):
    """Returns (tile_id_value per pixel in [0,1], grout mask 1=grout, edge_bevel 0..1)."""
    x, y = coords(n)
    size = n / tiles
    tx = np.floor(x / size).astype(int)
    ty = np.floor(y / size).astype(int)
    rng = np.random.default_rng(seed)
    vals = rng.random((tiles, tiles))
    tid = vals[ty % tiles, tx % tiles]
    fx = x - tx * size
    fy = y - ty * size
    d = np.minimum(np.minimum(fx, size - fx), np.minimum(fy, size - fy))
    grout = (d < grout_px * 0.5).astype(np.float64)
    bevel = smoothstep(grout_px * 0.5, grout_px * 0.5 + bevel_px, d)
    return tid, grout, bevel, (tx, ty)


# ----------------------------------------------------------------------------- materials

def floor_tile():
    """Mall concourse: 600 mm porcelain tiles (2 x 2 per texture = 1.2 m), worn and scuffed."""
    n = 1024
    tid, grout, bevel, _ = tile_grid(n, 2, 8, 11, 6)
    base = col([196, 188, 170])
    speck = band(n, 120, 400, 3)
    mott = fnoise(n, 2.6, 4)
    dirt = fnoise(n, 2.2, 5)
    scuff = band(n, 6, 30, 6)
    g = 0.88 + 0.08 * (tid - 0.5) + 0.06 * (mott - 0.5) + 0.05 * (speck - 0.5)
    alb = tint(g, base)
    alb *= (1 - 0.12 * smoothstep(0.55, 0.9, dirt))[..., None]
    alb *= (1 - 0.1 * smoothstep(0.7, 0.95, scuff))[..., None]
    alb = alb * (1 - grout[..., None]) + grout[..., None] * col([92, 88, 82]) * (0.8 + 0.3 * dirt[..., None])
    # rare chipped corners
    chips = smoothstep(0.86, 0.95, fnoise(n, 1.4, 8)) * (1 - bevel * 0.7)
    alb = alb * (1 - 0.35 * chips[..., None])
    h = bevel * 0.8 + 0.03 * speck - 0.25 * chips
    smooth = (0.62 - 0.25 * smoothstep(0.5, 0.9, dirt) - 0.2 * smoothstep(0.7, 0.95, scuff)) * (1 - grout) + 0.12 * grout
    ao = 1 - 0.5 * grout
    save_set("floor_tile", alb, h, 6.0, smooth, ao=ao)


def terrazzo():
    """Atrium terrazzo: speckled chips in a warm cement matrix (2 m per texture)."""
    n = 1024
    matrix = col([180, 172, 158])
    m = fnoise(n, 2.4, 21)
    alb = tint(0.9 + 0.1 * (m - 0.5), matrix)
    rng = np.random.default_rng(22)
    x, y = coords(n)
    chips_h = np.zeros((n, n))
    palette = [col([60, 58, 55]), col([150, 120, 95]), col([230, 225, 215]), col([110, 115, 120]), col([170, 90, 70])]
    for i in range(2600):
        cx, cy = rng.random() * n, rng.random() * n
        r = rng.uniform(1.5, 6.5)
        dx = (x - cx + n / 2) % n - n / 2
        dy = (y - cy + n / 2) % n - n / 2
        d = np.sqrt(dx * dx * rng.uniform(0.6, 1.4) + dy * dy * rng.uniform(0.6, 1.4))
        mask = d < r
        if not mask.any():
            continue
        c = palette[rng.integers(len(palette))] * rng.uniform(0.85, 1.1)
        alb[mask] = c
        chips_h[mask] = 0.05
    dirt = fnoise(n, 2.0, 23)
    alb *= (1 - 0.1 * smoothstep(0.55, 0.95, dirt))[..., None]
    smooth = 0.7 - 0.2 * smoothstep(0.5, 0.95, dirt)
    save_set("terrazzo", alb, chips_h + 0.02 * m, 2.0, smooth)


def ceiling_tile():
    """600 mm mineral-fibre ceiling tiles in a white T-grid (2 x 2 per texture = 1.2 m)."""
    n = 1024
    tid, grout, bevel, _ = tile_grid(n, 2, 14, 31, 10)
    fiss = band(n, 40, 180, 32)
    worms = smoothstep(0.62, 0.7, fiss) * 0.6
    pores = smoothstep(0.8, 0.9, band(n, 150, 500, 33))
    base = 0.86 + 0.05 * (tid - 0.5) - 0.12 * worms - 0.1 * pores
    yel = fnoise(n, 2.4, 34)
    alb = tint(base, col([232, 230, 222]))
    alb *= (1 - 0.06 * yel)[..., None] * np.array([1.0, 0.99, 0.95])[None, None, :]
    alb = alb * (1 - grout[..., None]) + grout[..., None] * col([214, 214, 210])
    h = bevel * 0.6 - 0.2 * worms - 0.15 * pores + 0.6 * grout * 0  # grid sits proud: handled by bevel
    smooth = 0.08 + 0.3 * grout
    metal = 0.4 * grout
    save_set("ceiling_tile", alb, h, 4.0, smooth, metal=metal)


def wall_paint():
    """Painted plaster, slightly yellowed, roller texture and old touch-ups (2 m)."""
    n = 1024
    roll = band(n, 60, 260, 41)
    patch = fnoise(n, 3.0, 42)
    grime = fnoise(n, 2.2, 43)
    touch = smoothstep(0.6, 0.72, patch)
    g = 0.93 + 0.025 * (roll - 0.5) + 0.012 * touch - 0.05 * smoothstep(0.55, 0.95, grime)
    alb = tint(g, col([214, 206, 186]))
    h = 0.25 * roll + 0.08 * touch
    smooth = 0.25 - 0.08 * grime + 0.04 * touch
    save_set("wall_paint", alb, h, 1.5, smooth)


def wall_block():
    """Painted concrete blockwork for service areas (440 x 215 mm stretcher bond, 2.64 m per texture)."""
    n = 1024
    x, y = coords(n)
    bw, bh = n / 6.0, n / 12.27
    row = np.floor(y / bh)
    off = (row % 2) * bw * 0.5
    bx = np.floor((x + off) / bw)
    fx = (x + off) - bx * bw
    fy = y - row * bh
    d = np.minimum(np.minimum(fx, bw - fx), np.minimum(fy, bh - fy))
    joint = smoothstep(4.5, 2.5, d)
    rng = np.random.default_rng(51)
    bid = rng.random((40, 40))[row.astype(int) % 40, bx.astype(int) % 40]
    pores = smoothstep(0.75, 0.9, band(n, 200, 500, 52))
    grime = fnoise(n, 2.0, 53)
    g = 0.86 + 0.05 * (bid - 0.5) - 0.12 * pores - 0.1 * smoothstep(0.5, 0.95, grime)
    alb = tint(g, col([196, 198, 192]))
    alb = alb * (1 - 0.25 * joint[..., None])
    h = (1 - joint) * 0.6 - 0.2 * pores
    smooth = 0.2 - 0.1 * grime
    save_set("wall_block", alb, h, 4.0, smooth, ao=1 - 0.3 * joint)


def concrete():
    """Structural concrete (pillars, car park walls, stairwells): mottled, pitted (2 m)."""
    n = 1024
    m = fnoise(n, 2.2, 61)
    m2 = fnoise(n, 3.2, 62)
    pits = smoothstep(0.82, 0.92, band(n, 120, 480, 63))
    stain = smoothstep(0.55, 0.9, fnoise(n, 2.0, 64))
    g = 0.62 + 0.12 * (m - 0.5) + 0.08 * (m2 - 0.5) - 0.2 * pits - 0.12 * stain
    alb = tint(g, col([178, 176, 170]))
    h = 0.4 * m - 0.5 * pits
    smooth = 0.18 - 0.08 * pits + 0.05 * m2
    save_set("concrete", alb, h, 3.0, smooth)


def carpark_floor():
    """Power-floated car park slab: tyre polish lanes, oil, cracks (4 m)."""
    n = 1024
    x, y = coords(n)
    m = fnoise(n, 2.3, 71)
    grit = band(n, 150, 500, 72)
    oil = smoothstep(0.7, 0.85, fnoise(n, 2.6, 73))
    lanes = 0.5 + 0.5 * np.sin(x / n * 2 * np.pi * 2) ** 2
    crack = blur(cracks(n, 3, 74, (0.3, 0.7), (0.8, 1.8)), 0.0006)
    g = 0.5 + 0.1 * (m - 0.5) + 0.06 * (grit - 0.5) - 0.06 * lanes * 0.5
    alb = tint(g, col([150, 150, 146]))
    alb *= (1 - 0.45 * oil)[..., None]
    alb *= (1 - 0.5 * crack)[..., None]
    h = 0.15 * grit + 0.2 * m - 0.6 * crack
    smooth = 0.22 + 0.25 * oil + 0.08 * lanes - 0.1 * crack
    save_set("carpark_floor", alb, h, 3.0, smooth)


def floor_vinyl():
    """Speckled sheet vinyl for service corridors and back-of-house (2 m)."""
    n = 512
    speck = band(n, 80, 250, 81)
    m = fnoise(n, 2.4, 82)
    scuff = band(n, 4, 20, 83)
    g = 0.7 + 0.06 * (m - 0.5) + 0.12 * (smoothstep(0.65, 0.75, speck) - smoothstep(0.25, 0.35, speck) * 0.6)
    alb = tint(g, col([170, 168, 150]))
    alb *= (1 - 0.15 * smoothstep(0.65, 0.95, scuff))[..., None]
    smooth = 0.45 - 0.2 * smoothstep(0.65, 0.95, scuff)
    save_set("floor_vinyl", alb, 0.05 * speck, 1.0, smooth)


def carpet():
    """Commercial loop-pile carpet: dark ground with 1990s 'confetti' flecks (1 m)."""
    n = 512
    loops = band(n, 180, 256, 91)
    wear = fnoise(n, 2.2, 92)
    alb = np.ones((n, n, 3)) * col([46, 52, 66])
    for i, c in enumerate([col([150, 60, 70]), col([60, 120, 125]), col([170, 150, 90]), col([100, 80, 140])]):
        fleck = smoothstep(0.7, 0.74, band(n, 60, 160, 93 + i))
        alb = alb * (1 - fleck[..., None]) + c[None, None, :] * fleck[..., None]
    alb *= (0.78 + 0.35 * loops)[..., None]
    alb = alb * (1 - 0.2 * smoothstep(0.5, 0.9, wear))[..., None] + 0.06 * smoothstep(0.5, 0.9, wear)[..., None]
    save_set("carpet", alb, loops, 2.5, 0.04 + 0.03 * loops)


def metal_brushed():
    """Brushed stainless steel (railings, kick plates, counters) (1 m)."""
    n = 512
    rng = np.random.default_rng(101)
    lines = rng.standard_normal((n, 1)) * np.ones((1, n))
    lines = blur(lines + 0.3 * rng.standard_normal((n, n)), 0.002)
    lines = (lines - lines.min()) / (lines.max() - lines.min())
    smudge = fnoise(n, 2.2, 102)
    g = 0.7 + 0.12 * (lines - 0.5) - 0.06 * smudge
    alb = tint(g, col([200, 202, 205]))
    smooth = 0.62 - 0.2 * smoothstep(0.5, 0.9, smudge) + 0.05 * lines
    save_set("metal_brushed", alb, 0.2 * lines, 1.0, smooth, metal=np.full((n, n), 0.95))


def shutter():
    """Galvanised roller shutter slats (horizontal ridges, 1 m), streaked and dented."""
    n = 512
    x, y = coords(n)
    slat = (y / n * 13.0) % 1.0
    profile = np.sin(slat * np.pi) ** 0.6
    joint = smoothstep(0.06, 0.0, slat) + smoothstep(0.94, 1.0, slat)
    streak = blur(np.random.default_rng(111).standard_normal((n, n)) * np.ones((n, 1)), 0.01)
    streak = (streak - streak.min()) / (streak.max() - streak.min())
    dents = smoothstep(0.85, 0.95, fnoise(n, 2.0, 112))
    rust = smoothstep(0.78, 0.9, fnoise(n, 2.4, 113)) * joint.clip(0, 1)
    g = 0.62 + 0.1 * profile - 0.15 * joint + 0.08 * (streak - 0.5)
    alb = tint(g, col([176, 180, 182]))
    alb = alb * (1 - rust[..., None]) + rust[..., None] * col([120, 70, 40])
    h = profile - 0.6 * joint - 0.2 * dents
    smooth = 0.4 + 0.1 * profile - 0.25 * rust
    save_set("shutter", alb, h, 5.0, smooth, metal=0.8 * (1 - rust))


def wood():
    """Wood veneer (benches, counters, doors): straight grain along X with fibres and figure (1 m)."""
    n = 512
    x, y = coords(n)
    warp = aniso(n, 14.0, 2.6, 121)
    fibres = aniso(n, 40.0, 1.4, 122)
    grain = 0.5 + 0.5 * np.sin((y / n * 14 + warp * 2.5) * np.pi * 2)
    late = smoothstep(0.55, 0.95, grain)
    g = 0.62 - 0.18 * late + 0.08 * (fibres - 0.5) + 0.05 * (fnoise(n, 2.8, 123) - 0.5)
    alb = tint(g, col([158, 108, 66]))
    h = 0.15 * late + 0.1 * fibres
    save_set("wood", alb, h, 1.2, 0.32 + 0.06 * (1 - late))


def tile_white():
    """White 150 mm wall tiles for toilets (4 x 4 per texture = 0.6 m)."""
    n = 512
    tid, grout, bevel, _ = tile_grid(n, 4, 5, 131, 4)
    grime = fnoise(n, 2.0, 132)
    g = 0.92 + 0.03 * (tid - 0.5)
    alb = tint(g, col([236, 238, 234]))
    alb = alb * (1 - grout[..., None]) + grout[..., None] * col([150, 148, 140]) * (0.8 + 0.3 * grime[..., None])
    smooth = 0.8 * (1 - grout) + 0.15 * grout
    save_set("tile_white", alb, bevel, 4.0, smooth, ao=1 - 0.4 * grout)


def metal_painted():
    """Painted sheet metal / machinery (green-grey), with chips revealing bare metal (1 m)."""
    n = 512
    chips = smoothstep(0.8, 0.86, fnoise(n, 1.8, 141))
    m = fnoise(n, 2.4, 142)
    paint = tint(0.9 + 0.08 * (m - 0.5), col([92, 108, 98]))
    bare = tint(0.7 + 0.1 * m, col([150, 150, 150]))
    alb = paint * (1 - chips[..., None]) + bare * chips[..., None]
    save_set("metal_painted", alb, -0.4 * chips + 0.05 * m, 2.0, 0.35 + 0.2 * chips, metal=0.9 * chips)


def laminate():
    """Shop counter / desk laminate (1 m)."""
    n = 512
    m = fnoise(n, 2.6, 151)
    fine = band(n, 120, 256, 152)
    alb = tint(0.85 + 0.05 * (m - 0.5) + 0.03 * fine, col([200, 192, 178]))
    save_set("laminate", alb, 0.03 * fine, 1.0, 0.5 - 0.1 * m)


def plaster_dark():
    """Dark painted surface for shopfront pilasters / bulkheads (2 m)."""
    n = 512
    m = fnoise(n, 2.4, 161)
    roll = band(n, 50, 200, 162)
    alb = tint(0.85 + 0.06 * (m - 0.5) + 0.03 * roll, col([70, 72, 78]))
    save_set("plaster_dark", alb, 0.2 * roll, 1.0, 0.3)


# ----------------------------------------------------------------------------- decals

def blob(n, seed, r_frac=0.35, rough=0.25, beta=2.2):
    x, y = coords(n)
    cx = cy = n / 2
    d = np.sqrt((x - cx) ** 2 + (y - cy) ** 2) / (n * r_frac)
    edge = fnoise(n, beta, seed)
    return smoothstep(1.0, 0.6, d + rough * (edge - 0.5) * 2)


def decals():
    n = 512
    x, y = coords(n)
    # coffee / water stain with a darker rim
    for i, (c, s) in enumerate([(col([90, 60, 35]), 201), (col([70, 70, 60]), 202)]):
        b = blob(n, s, 0.38, 0.35)
        rim = np.clip(b - blob(n, s, 0.33, 0.35), 0, 1)
        a = 0.35 * b + 0.45 * rim
        save_decal("stain_" + "ab"[i], np.ones((n, n, 3)) * c, a * (0.7 + 0.3 * fnoise(n, 2.0, s + 5)))
    # shoe scuffs: short dark strokes
    rng = np.random.default_rng(210)
    a = np.zeros((n, n))
    for _ in range(40):
        cx, cy = rng.random() * n, rng.random() * n
        ang = rng.uniform(-0.4, 0.4)
        L, W = rng.uniform(30, 120), rng.uniform(2, 6)
        dx, dy = x - cx, y - cy
        u = dx * np.cos(ang) + dy * np.sin(ang)
        v = -dx * np.sin(ang) + dy * np.cos(ang)
        a = np.maximum(a, smoothstep(1, 0, np.abs(v) / W) * smoothstep(L, L * 0.6, np.abs(u)) * rng.uniform(0.3, 0.8))
    edge = smoothstep(0.5, 0.35, np.sqrt(((x - n / 2) / n) ** 2 + ((y - n / 2) / n) ** 2))
    save_decal("scuffs", np.ones((n, n, 3)) * col([30, 28, 26]), a * edge)
    # chewing gum: flattened grey-black blobs
    a = np.zeros((n, n))
    for _ in range(18):
        cx, cy = rng.random() * n * 0.8 + n * 0.1, rng.random() * n * 0.8 + n * 0.1
        r = rng.uniform(5, 13)
        a = np.maximum(a, smoothstep(r, r * 0.6, np.sqrt((x - cx) ** 2 + (y - cy) ** 2)))
    save_decal("gum", np.ones((n, n, 3)) * col([45, 45, 48]), a * 0.9)
    # crack: branching thin dark lines
    a = blur(cracks(n, 2, 220, (0.5, 0.8), (1.2, 2.6), 0.8), 0.0008) * edge
    save_decal("crack", np.ones((n, n, 3)) * col([25, 24, 22]), a)
    # oil stain
    b = blob(n, 230, 0.4, 0.45)
    save_decal("oil", np.ones((n, n, 3)) * col([18, 16, 14]), b * (0.55 + 0.35 * fnoise(n, 2.0, 231)))
    # tyre marks: two curved bands
    a = np.zeros((n, n))
    for off in (-60, 60):
        curve = n / 2 + off + 40 * np.sin(y / n * np.pi)
        a = np.maximum(a, smoothstep(14, 6, np.abs(x - curve)))
    a *= smoothstep(0, 60, y) * smoothstep(n, n - 60, y) * (0.5 + 0.5 * band(n, 30, 120, 232))
    save_decal("tyre", np.ones((n, n, 3)) * col([22, 22, 22]), a * 0.6)
    # puddle: dark, glossy (use with a high smoothness material)
    b = blob(n, 240, 0.42, 0.5, 2.6)
    save_decal("puddle", np.ones((n, n, 3)) * col([40, 44, 46]), b * 0.55)
    # ceiling water damage: brown rings
    b = blob(n, 250, 0.4, 0.4)
    rings = (np.sin(np.sqrt((x - n / 2) ** 2 + (y - n / 2) ** 2) / n * 60 + 10 * fnoise(n, 2.5, 251)) * 0.5 + 0.5)
    save_decal("water_damage", np.ones((n, n, 3)) * col([130, 95, 55]), b * (0.25 + 0.35 * rings))
    # wall grime: dirt gradient rising from the floor (use stretched along walls)
    g = smoothstep(n, n * 0.3, y) * (0.6 + 0.4 * fnoise(n, 2.0, 260))
    save_decal("grime", np.ones((n, n, 3)) * col([60, 55, 45]), g * 0.45)
    # mould / damp patch for walls
    b = blob(n, 270, 0.4, 0.6, 1.8)
    save_decal("damp", np.ones((n, n, 3)) * col([55, 62, 45]), b * (0.3 + 0.5 * band(n, 20, 120, 271)))
    # tape residue rectangle (old poster)
    a = np.zeros((n, n))
    for (cx, cy) in [(80, 60), (432, 60), (80, 452), (432, 452)]:
        a = np.maximum(a, ((np.abs(x - cx) < 40) & (np.abs(y - cy) < 14)).astype(float) * 0.7)
    inner = ((np.abs(x - n / 2) < 180) & (np.abs(y - n / 2) < 200)).astype(float)
    a = np.maximum(a, inner * 0.12 * fnoise(n, 2.0, 280))
    save_decal("tape_marks", np.ones((n, n, 3)) * col([150, 140, 110]), a)


def blood_decals():
    """Gore (the catch sequence): pools, spatter, drag smears and blood running down a lens."""
    n = 512
    x, y = coords(n)
    rng = np.random.default_rng(900)
    dark = col([70, 4, 6])
    fresh = col([120, 8, 10])

    def colour(a, wet):
        c = dark * (1 - wet[..., None]) + fresh * wet[..., None]
        return c

    # pool: thick, glossy, darker in the middle, with satellite drops
    b = blob(n, 901, 0.36, 0.45, 2.4)
    a = b.copy()
    for _ in range(26):
        cx, cy = rng.random() * n, rng.random() * n
        r = rng.uniform(3, 12)
        a = np.maximum(a, smoothstep(r, r * 0.5, np.sqrt((x - cx) ** 2 + (y - cy) ** 2)) * 0.95)
    wet = np.clip(1 - b * 0.8 + 0.2 * fnoise(n, 2.0, 902), 0, 1)
    save_decal("blood_pool", colour(a, wet), np.clip(a, 0, 1) * 0.97)

    # spatter: impact centre with radiating streaks and droplets
    a = blob(n, 910, 0.12, 0.6, 2.0) * 0.95
    for _ in range(70):
        ang = rng.uniform(0, 2 * np.pi)
        dist = rng.uniform(0.08, 0.46) * n
        cx, cy = n / 2 + np.cos(ang) * dist, n / 2 + np.sin(ang) * dist
        r = rng.uniform(1.5, 9) * (1.2 - dist / n)
        L = r * rng.uniform(1.5, 4.0)
        dx, dy = x - cx, y - cy
        u = dx * np.cos(ang) + dy * np.sin(ang)
        v = -dx * np.sin(ang) + dy * np.cos(ang)
        drop = smoothstep(1, 0.6, np.sqrt((u / L) ** 2 + (v / r) ** 2))
        a = np.maximum(a, drop * rng.uniform(0.7, 1.0))
    save_decal("blood_spatter", colour(a, np.full((n, n), 0.6)), a)

    # drag smear: long streaky band with finger lines
    a = np.zeros((n, n))
    streak = aniso(n, 14, 2.0, 920).T
    band_ = smoothstep(150, 90, np.abs(x - n / 2 - 30 * np.sin(y / n * 3)))
    a = band_ * (0.35 + 0.65 * streak) * smoothstep(0, 80, y) * smoothstep(n, n - 140, y)
    for k in range(4):
        cx = n / 2 - 75 + k * 50
        a = np.maximum(a, smoothstep(7, 2, np.abs(x - cx - 6 * np.sin(y / 40 + k))) * smoothstep(n * 0.2, n * 0.9, y) * 0.9)
    save_decal("blood_smear", colour(a, np.full((n, n), 0.3)), np.clip(a, 0, 1))

    # lens: spatter around the edges of the view, with drips running down (drawn over the screen)
    a = np.zeros((n, n))
    for _ in range(90):
        side = rng.integers(4)
        t = rng.random()
        cx, cy = [(t * n, rng.uniform(0, 0.25) * n), (t * n, n - rng.uniform(0, 0.3) * n),
                  (rng.uniform(0, 0.25) * n, t * n), (n - rng.uniform(0, 0.25) * n, t * n)][side]
        r = rng.uniform(4, 26)
        a = np.maximum(a, smoothstep(r, r * 0.55, np.sqrt((x - cx) ** 2 + (y - cy) ** 2)) * rng.uniform(0.6, 0.95))
    for _ in range(14):
        cx = rng.uniform(0.05, 0.95) * n
        top = rng.uniform(0, 0.35) * n
        L = rng.uniform(0.2, 0.55) * n
        w = rng.uniform(2, 6)
        wob = cx + 5 * np.sin(y / 23 + cx)
        a = np.maximum(a, smoothstep(w, w * 0.4, np.abs(x - wob)) * (y > top) * smoothstep(top + L, top + L * 0.8, y) * 0.9)
    vign = smoothstep(0.15, 0.75, np.sqrt(((x - n / 2) / n) ** 2 + ((y - n / 2) / n) ** 2) * 1.6)
    a = np.maximum(a, vign * 0.55)
    save_decal("blood_lens", colour(a, np.full((n, n), 0.5)), np.clip(a, 0, 1))


def wear_decal():
    """Worn walking path: a broad, soft darker band where decades of shoes have dulled the floor, with
    long scuff streaks running along it. Ends fade so pieces overlap seamlessly down a concourse."""
    n = 512
    x, y = coords(n)
    u, v = x / n, y / n
    across = np.abs(v - 0.5) * 2.0
    wob = 0.08 * (fnoise(n, 2.4, 611) - 0.5)
    core = smoothstep(1.0, 0.25, across + wob)
    ends = smoothstep(0.0, 0.25, u) * smoothstep(1.0, 0.75, u)
    rng = np.random.default_rng(612)
    streak = np.zeros((n, n))
    for _ in range(140):
        cy, cx = rng.uniform(0.15, 0.85) * n, rng.random() * n
        L, W = rng.uniform(40, 220), rng.uniform(0.8, 3.0)
        ang = rng.uniform(-0.06, 0.06)
        dx, dy = x - cx, y - cy
        uu = dx * np.cos(ang) + dy * np.sin(ang)
        vv = -dx * np.sin(ang) + dy * np.cos(ang)
        streak = np.maximum(streak, smoothstep(1, 0, np.abs(vv) / W) * smoothstep(L, L * 0.5, np.abs(uu)) * rng.uniform(0.2, 0.6))
    grain = 0.75 + 0.25 * fnoise(n, 1.6, 613)
    a = (0.28 * core * grain + 0.35 * streak * core) * ends
    rgb = np.ones((n, n, 3)) * col([38, 34, 30])
    save_decal("wear", rgb, a)


if __name__ == "__main__":
    print("Generating textures into", os.path.abspath(OUT))
    for fn in (floor_tile, terrazzo, ceiling_tile, wall_paint, wall_block, concrete, carpark_floor, floor_vinyl,
               carpet, metal_brushed, shutter, wood, tile_white, metal_painted, laminate, plaster_dark):
        fn()
    decals()
    wear_decal()
    blood_decals()
    print("Done.")
