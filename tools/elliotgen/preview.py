"""Tiny software renderer for checking the sculpt: z-buffer + lambert + rim, several views."""
import numpy as np, sys
from PIL import Image


def render(V, F, eye, target, up=(0, 1, 0), fov=30, size=(600, 900), N=None, colors=None):
    eye, target = np.array(eye, float), np.array(target, float)
    f = target - eye; f /= np.linalg.norm(f)
    r = np.cross(f, up); r /= np.linalg.norm(r)
    u = np.cross(r, f)
    rel = V - eye
    x, y, z = rel @ r, rel @ u, rel @ f
    W, Hh = size
    s = (Hh / 2) / np.tan(np.radians(fov) / 2)
    px = W / 2 + x / z * s
    py = Hh / 2 - y / z * s
    zb = np.full((Hh, W), np.inf)
    img = np.zeros((Hh, W, 3))
    tri = F
    fn = np.cross(V[tri[:, 1]] - V[tri[:, 0]], V[tri[:, 2]] - V[tri[:, 0]])
    fn /= np.maximum(np.linalg.norm(fn, axis=1, keepdims=True), 1e-12)
    L1 = np.array([0.4, 0.7, 0.6]); L1 /= np.linalg.norm(L1)
    for t in range(len(tri)):
        i0, i1, i2 = tri[t]
        xs = np.array([px[i0], px[i1], px[i2]]); ys = np.array([py[i0], py[i1], py[i2]]); zs = np.array([z[i0], z[i1], z[i2]])
        x0, x1 = int(max(0, np.floor(xs.min()))), int(min(W - 1, np.ceil(xs.max())))
        y0, y1 = int(max(0, np.floor(ys.min()))), int(min(Hh - 1, np.ceil(ys.max())))
        if x1 < x0 or y1 < y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        d = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
        if abs(d) < 1e-12:
            continue
        a = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / d
        b = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / d
        c = 1 - a - b
        m = (a >= 0) & (b >= 0) & (c >= 0)
        if not m.any():
            continue
        zz = a * zs[0] + b * zs[1] + c * zs[2]
        sub = zb[y0:y1 + 1, x0:x1 + 1]
        m &= zz < sub
        if not m.any():
            continue
        sub[m] = zz[m]
        if N is not None:
            nn = (a[..., None] * N[i0] + b[..., None] * N[i1] + c[..., None] * N[i2])
            nn /= np.linalg.norm(nn, axis=-1, keepdims=True)
        else:
            nn = np.broadcast_to(fn[t], a.shape + (3,))
        lam = np.clip(nn @ L1, 0, 1) * 0.8 + 0.12
        vd = -f
        rim = np.clip(1 - np.abs(nn @ vd), 0, 1) ** 3 * 0.25
        col = np.array([0.78, 0.74, 0.66]) if colors is None else (a[..., None] * colors[i0] + b[..., None] * colors[i1] + c[..., None] * colors[i2])
        shade = col * lam[..., None] + rim[..., None]
        img[y0:y1 + 1, x0:x1 + 1][m] = shade[m]
    bg = np.isinf(zb)
    img[bg] = [0.08, 0.08, 0.09]
    return Image.fromarray((np.clip(img, 0, 1) ** (1 / 1.4) * 255).astype(np.uint8))


if __name__ == "__main__":
    d = np.load(sys.argv[1]); V, F = d["V"], d["F"]
    views = [((0, 1.3, 4.6), (0, 1.3, 0), 36, (520, 900)), ((4.6, 1.3, 0.3), (0, 1.3, 0), 36, (520, 900)),
             ((0.25, 2.38, 0.75), (0, 2.36, 0), 22, (520, 520)), ((-0.95, 0.95, 0.5), (-0.74, 0.82, 0.0), 22, (520, 520))]
    ims = [render(V, F, e, t, fov=fv, size=s) for e, t, fv, s in views]
    W = sum(i.width for i in ims); Hh = max(i.height for i in ims)
    out = Image.new("RGB", (W, Hh))
    xo = 0
    for i in ims:
        out.paste(i, (xo, 0)); xo += i.width
    out.save(sys.argv[2])
