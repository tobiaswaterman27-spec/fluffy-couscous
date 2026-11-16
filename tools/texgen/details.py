"""More textures for the detail pass: shelf stock, cardboard, Elliot's skin, litter / prints / cobwebs, dust."""
import os
import numpy as np
from PIL import Image
import generate as g
from generate import fnoise, cracks, blur, smoothstep, save_set, save_decal, col, coords, blob, aniso, band, OUT


def products(name, seed, palette, box_like=False):
    """Rows of product fronts on a shelf: packs of different widths, colours, a label band, a glint."""
    n = 1024
    rng = np.random.default_rng(seed)
    alb = np.zeros((n, n, 3))
    height = np.zeros((n, n))
    smooth = np.full((n, n), 0.35)
    x, y = coords(n)
    rows = 3
    rh = n // rows
    for r in range(rows):
        y0 = r * rh
        cx = 0
        while cx < n:
            w = int(rng.integers(40, 120) if not box_like else rng.integers(90, 220))
            h = int(rh * rng.uniform(0.55, 0.92))
            c = np.array(palette[rng.integers(len(palette))]) / 255.0 * rng.uniform(0.8, 1.1)
            x1 = min(n, cx + w - 4)
            top = y0 + rh - h
            alb[top:y0 + rh, cx:x1] = c
            height[top:y0 + rh, cx:x1] = 1.0
            # label band and fake text lines
            lb = top + int(h * rng.uniform(0.25, 0.5))
            lh = int(h * rng.uniform(0.15, 0.3))
            lc = np.array([0.92, 0.9, 0.85]) if rng.random() < 0.6 else c * 0.4
            alb[lb:lb + lh, cx + 4:x1 - 4] = lc
            for k in range(rng.integers(1, 4)):
                ly = lb + 4 + k * 7
                if ly + 3 < lb + lh:
                    alb[ly:ly + 3, cx + 8:cx + 8 + int((x1 - cx - 16) * rng.uniform(0.3, 0.9))] = c * 0.3
            smooth[top:y0 + rh, cx:x1] = 0.6 if not box_like else 0.25
            cx += w
        # shadow under the shelf above
        alb[y0:y0 + 18] *= np.linspace(0.15, 1, 18)[:, None, None]
    alb *= (0.85 + 0.15 * fnoise(n, 2.0, seed + 1))[..., None]
    alb = np.clip(alb + 0.04 * (height[..., None] == 0), 0, 1)
    save_set(name, alb, blur(height, 0.0015), 3.0, smooth)


def cardboard():
    n = 512
    x, y = coords(n)
    base = col([150, 112, 72])
    v = 0.85 + 0.15 * fnoise(n, 2.0, 501)
    alb = base * v[..., None]
    corr = 0.5 + 0.5 * np.sin(y / n * 2 * np.pi * 40)
    tape = (np.abs(y - n * 0.5) < 26).astype(float)
    alb = alb * (1 - tape[..., None]) + col([170, 140, 95]) * tape[..., None]
    stain = blob(n, 503, 0.2, 0.5) * 0.25
    alb *= (1 - stain[..., None])
    save_set("cardboard", alb, corr * 0.3 + tape * 0.2, 1.5, 0.2 + 0.4 * tape)


def skin():
    """Elliot: bloodless, grey-yellow, blotched; dark veins under thin skin; wet, uneven sheen."""
    n = 1024
    x, y = coords(n)
    base = col([196, 192, 176])
    mott = fnoise(n, 2.4, 601)
    blotch = blob(n, 602, 0.45, 0.8, 1.6) * 0.0 + smoothstep(0.55, 0.8, fnoise(n, 2.0, 603))
    alb = base * (0.8 + 0.25 * mott)[..., None]
    alb = alb * (1 - blotch[..., None] * 0.18) + col([150, 140, 150]) * blotch[..., None] * 0.18
    veins = blur(cracks(n, 14, 604, (0.2, 0.6), (0.6, 1.6), 0.6), 0.0012)
    veins = np.clip(veins * 1.6, 0, 1)
    alb = alb * (1 - veins[..., None] * 0.75) + col([60, 70, 105]) * veins[..., None] * 0.75
    pores = (np.random.default_rng(605).random((n, n)) < 0.02).astype(float)
    pores = blur(pores, 0.0008)
    height = mott * 0.3 - pores * 0.8 - veins * 0.3
    wet = 0.45 + 0.35 * fnoise(n, 2.0, 606)
    save_set("skin_pale", alb, height, 2.0, wet)


def decal_tex():
    n = 512
    x, y = coords(n)
    rng = np.random.default_rng(700)
    # litter: scraps of paper, a receipt, a sweet wrapper, a bottle cap
    a = np.zeros((n, n)); rgb = np.ones((n, n, 3)) * col([225, 222, 210])
    for _ in range(9):
        cx, cy = rng.random(2) * n * 0.8 + n * 0.1
        w, h = rng.uniform(15, 60), rng.uniform(10, 45)
        ang = rng.uniform(0, np.pi)
        dx, dy = x - cx, y - cy
        u = dx * np.cos(ang) + dy * np.sin(ang); v = -dx * np.sin(ang) + dy * np.cos(ang)
        edge = fnoise(n, 2.0, int(rng.integers(1e6))) * 6
        m = ((np.abs(u) < w + edge) & (np.abs(v) < h + edge)).astype(float)
        c = [col([230, 228, 215]), col([200, 40, 40]), col([40, 90, 170]), col([240, 200, 60])][rng.integers(4)]
        rgb = rgb * (1 - m[..., None]) + c * m[..., None]
        a = np.maximum(a, m * 0.95)
    rgb *= (0.8 + 0.2 * fnoise(n, 2.0, 701))[..., None]
    save_decal("litter", rgb, a)
    # leaflet: a glossy printed flyer, slightly crumpled
    a = ((np.abs(x - n / 2) < 150) & (np.abs(y - n / 2) < 210)).astype(float)
    rgb = np.ones((n, n, 3)) * col([240, 238, 230])
    rgb[(np.abs(x - n / 2) < 140) & (y > n / 2 - 200) & (y < n / 2 - 40)] = col([190, 40, 50])
    for k in range(8):
        yy = n / 2 + k * 18
        rgb[(np.abs(x - n / 2) < 120) & (np.abs(y - yy) < 4)] = col([60, 60, 60])
    rgb *= (0.85 + 0.15 * fnoise(n, 1.8, 702))[..., None]
    save_decal("leaflet", rgb, a * 0.97)
    # dusty shoe prints walking up the texture
    a = np.zeros((n, n))
    for k in range(6):
        cx = n / 2 + (40 if k % 2 else -40); cy = n - 60 - k * 80
        sole = (((x - cx) / 28) ** 2 + ((y - cy) / 55) ** 2 < 1) | (((x - cx) / 24) ** 2 + ((y - cy - 70) / 22) ** 2 < 1)
        tread = 0.6 + 0.4 * (np.sin(y / 4) > 0)
        a = np.maximum(a, sole * tread * 0.55)
    save_decal("prints_shoe", np.ones((n, n, 3)) * col([70, 60, 50]), blur(a, 0.002) * (0.7 + 0.3 * fnoise(n, 2.0, 703)))
    # long narrow bare prints with long toes (something walked here barefoot, through something dark)
    a = np.zeros((n, n))
    for k in range(4):
        cx = n / 2 + (55 if k % 2 else -55); cy = n - 90 - k * 120
        heel = ((x - cx) / 20) ** 2 + ((y - cy) / 30) ** 2 < 1
        arch = (np.abs(x - cx - 6) < 10) & (y < cy) & (y > cy - 80)
        ball = ((x - cx) / 26) ** 2 + ((y - cy + 95) / 22) ** 2 < 1
        foot = heel | arch | ball
        for t in range(5):
            tx = cx - 24 + t * 12; ty = cy - 125 - (t % 3) * 6
            foot |= ((x - tx) / 4.5) ** 2 + ((y - ty) / 16) ** 2 < 1
        a = np.maximum(a, foot.astype(float) * 0.8)
    save_decal("prints_bare", np.ones((n, n, 3)) * col([45, 25, 20]), blur(a, 0.0015) * (0.6 + 0.4 * fnoise(n, 2.0, 704)))
    # cobweb in a corner: radial threads and sagging spirals
    cx, cy = 0, 0
    r = np.sqrt((x - cx) ** 2 + (y - cy) ** 2); th = np.arctan2(y - cy, x - cx)
    radial = np.zeros((n, n))
    for k in range(9):
        ang = k / 8 * np.pi / 2
        radial = np.maximum(radial, smoothstep(2.0, 0.0, np.abs((th - ang) * r)))
    spiral = smoothstep(1.6, 0.0, np.abs(((r + 6 * np.sin(th * 9)) % 34) - 17) - 15)
    web = np.maximum(radial, spiral * smoothstep(n * 0.95, n * 0.2, r)) * smoothstep(n, n * 0.6, r)
    save_decal("cobweb", np.ones((n, n, 3)) * col([210, 210, 205]), web * 0.55)
    # a smeared handprint (for glass)
    a = np.zeros((n, n))
    palm = ((x - n / 2) / 70) ** 2 + ((y - n / 2 - 40) / 80) ** 2 < 1
    a = palm.astype(float)
    for k, (dx, ln) in enumerate([(-60, 110), (-22, 150), (18, 155), (55, 130), (95, 80)]):
        fx = n / 2 + dx; top = n / 2 - 40 - ln
        finger = (np.abs(x - fx - (y - n / 2) * (0.25 if k == 4 else 0.02)) < 14) & (y > top) & (y < n / 2)
        a = np.maximum(a, finger.astype(float))
    smear = np.zeros((n, n))
    for s in range(1, 30):
        smear = np.maximum(smear, np.roll(a, s * 4, axis=0) * (1 - s / 30) * 0.5)
    a = np.maximum(a * 0.7, smear)
    save_decal("handprint", np.ones((n, n, 3)) * col([60, 55, 50]), blur(a, 0.007) * (0.6 + 0.4 * fnoise(n, 2.0, 705)))
    # dust mote: a soft dot for particles
    m = 64
    xx, yy = np.mgrid[0:m, 0:m]
    d = np.sqrt((xx - m / 2 + 0.5) ** 2 + (yy - m / 2 + 0.5) ** 2) / (m / 2)
    dot = np.clip(1 - d, 0, 1) ** 2.2
    img = np.stack([np.ones((m, m))] * 3 + [dot], -1)
    Image.fromarray((img * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, "decal_dust_mote.png"))


def clothes(name, seed):
    """A rail of hanging clothes: garments of different widths, colours and lengths, soft folds."""
    n = 1024
    rng = np.random.default_rng(seed)
    alb = np.zeros((n, n, 3)); h = np.zeros((n, n)); x, y = coords(n)
    pal = [(30, 30, 35), (200, 195, 185), (90, 20, 30), (40, 60, 100), (150, 120, 90), (60, 80, 60), (180, 170, 200), (220, 210, 190), (110, 110, 115)]
    cx = 0
    while cx < n:
        w = int(rng.integers(14, 34)); c = np.array(pal[rng.integers(len(pal))]) / 255 * rng.uniform(.85, 1.1)
        length = int(n * rng.uniform(0.55, 0.95))
        x1 = min(n, cx + w)
        fold = 0.75 + 0.25 * np.sin((x[:length, cx:x1] - cx) / max(1, w) * np.pi)
        alb[:length, cx:x1] = c * fold[..., None]
        h[:length, cx:x1] = fold
        alb[:12, cx:x1] = 0.15
        cx += w - 2
    alb *= (0.9 + 0.1 * fnoise(n, 2.0, seed + 1))[..., None]
    save_set(name, np.clip(alb, 0, 1), blur(h, 0.001), 2.0, 0.15)


def light_beam():
    """A soft shaft of light: bright at the top, fading out downwards, soft at the sides, faint dusty streaks."""
    w, h = 256, 512
    u = (np.arange(w) / (w - 1) - 0.5)[None, :]
    v = (np.arange(h) / (h - 1))[:, None]
    across = np.exp(-(u / 0.22) ** 2)
    down = (1 - v) ** 1.6 * np.clip(v / 0.04, 0, 1)
    streak = 0.75 + 0.25 * np.sin(u * 60 + np.sin(v * 7) * 2)
    a = np.clip(across * down * streak, 0, 1)
    img = np.stack([np.ones((h, w))] * 3 + [a], -1)
    Image.fromarray((img * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, "decal_light_beam.png"))


def wear_decals():
    n = 512
    x, y = coords(n)
    rng = np.random.default_rng(950)
    # water streaks: drips running down from a leak line at the top, brown and uneven
    a = np.zeros((n, n))
    for _ in range(26):
        cx = rng.uniform(0.1, 0.9) * n
        length = rng.uniform(0.3, 0.95) * n
        wdt = rng.uniform(3, 14)
        wob = cx + 6 * np.sin(y / rng.uniform(25, 60) + rng.uniform(0, 6))
        a = np.maximum(a, smoothstep(wdt, wdt * 0.2, np.abs(x - wob)) * smoothstep(length, length * 0.5, y) * rng.uniform(0.3, 0.8))
    a = np.maximum(a, smoothstep(60, 0, y) * 0.6 * (0.6 + 0.4 * fnoise(n, 2.0, 951)))
    a *= 0.7 + 0.3 * fnoise(n, 2.0, 952)
    save_decal("water_streaks", np.ones((n, n, 3)) * col([105, 80, 50]), a * 0.75)
    # peeling paint: flakes lifting off, showing grey plaster under, with a dark rim
    b = blob(n, 953, 0.38, 0.9, 1.7)
    flakes = smoothstep(0.45, 0.6, fnoise(n, 1.6, 954)) * b
    rim = np.clip(blur(flakes, 0.004) - flakes, 0, 1) * 3
    rgb = np.ones((n, n, 3)) * col([150, 148, 140])
    rgb = rgb * (1 - rim[..., None]) + col([60, 58, 55]) * rim[..., None]
    save_decal("peeling", rgb, np.clip(flakes + rim, 0, 1) * 0.9)
    # glass grime: a dusty film, wiped arcs, fingerprints
    film = 0.25 + 0.2 * fnoise(n, 2.2, 955)
    arcs = 0.5 + 0.5 * np.sin(np.sqrt((x - n * 0.3) ** 2 + (y - n * 1.2) ** 2) / 9)
    wipe = smoothstep(0.3, 0.8, blob(n, 956, 0.35, 0.6, 2.0)) * arcs * 0.5
    a = np.clip(film - wipe * 0.3, 0, 1) * smoothstep(0, 0.15 * n, y) * 0.8
    save_decal("glass_grime", np.ones((n, n, 3)) * col([150, 145, 130]), a * 0.6)


if __name__ == "__main__":
    wear_decals()
    light_beam()
    clothes("clothes_rack", 901)
    products("products_grocery", 801, [(200, 40, 40), (240, 200, 60), (40, 120, 60), (230, 120, 30), (40, 80, 160), (240, 240, 230), (120, 60, 30), (160, 30, 90)])
    products("products_beauty", 802, [(240, 220, 225), (180, 140, 200), (60, 60, 70), (220, 230, 240), (230, 180, 160), (120, 190, 200), (250, 250, 250)])
    products("products_boxes", 803, [(40, 40, 45), (220, 220, 225), (30, 70, 140), (180, 30, 30), (90, 90, 95), (240, 240, 240)], box_like=True)
    cardboard()
    skin()
    decal_tex()
    print("done")
