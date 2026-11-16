"""Signed-distance modelling: primitives, smooth blends, noise. Everything is vectorised over (N,3) point arrays."""
import numpy as np


def nrm(v):
    v = np.asarray(v, float)
    return v / np.linalg.norm(v)


def frame(y_axis, z_hint):
    """Orthonormal basis (columns X, Y, Z) with Y along y_axis and Z as close as possible to z_hint."""
    y = nrm(y_axis)
    z = np.asarray(z_hint, float) - y * np.dot(z_hint, y)
    z = nrm(z)
    x = np.cross(y, z)
    return np.stack([x, y, z], 1)


# ---------------------------------------------------------------- primitives (return distance, bbox)
class Prim:
    def __init__(self, kind, bmin, bmax, fn, bone=None, tag="skin", op="add", k=0.0, group=None):
        self.kind, self.fn, self.bone, self.tag, self.op, self.k = kind, fn, bone, tag, op, k
        self.bmin, self.bmax = np.asarray(bmin, float), np.asarray(bmax, float)
        self.group = group


def sphere(c, r, **kw):
    c = np.asarray(c, float)
    return Prim("sphere", c - r, c + r, lambda p: np.linalg.norm(p - c, axis=1) - r, **kw)


def ellipsoid(c, r, R=None, **kw):
    c, r = np.asarray(c, float), np.asarray(r, float)
    Rm = np.eye(3) if R is None else R

    def f(p):
        q = (p - c) @ Rm
        k0 = np.linalg.norm(q / r, axis=1)
        k1 = np.linalg.norm(q / (r * r), axis=1)
        return k0 * (k0 - 1.0) / np.maximum(k1, 1e-9)
    ext = np.abs(Rm) @ r
    return Prim("ellipsoid", c - ext, c + ext, f, **kw)


def cone(a, b, ra, rb, **kw):
    """Round cone: a capsule whose radius goes from ra (at a) to rb (at b)."""
    a, b = np.asarray(a, float), np.asarray(b, float)
    ba = b - a
    l2 = float(ba @ ba)
    rr = ra - rb
    a2 = l2 - rr * rr
    il2 = 1.0 / l2

    def f(p):
        pa = p - a
        y = pa @ ba
        z = y - l2
        xv = pa * l2 - np.outer(y, ba)
        x2 = np.einsum("ij,ij->i", xv, xv)
        y2 = y * y * l2
        z2 = z * z * l2
        k = np.sign(rr) * rr * rr * x2
        d = np.empty(len(p))
        m1 = np.sign(z) * a2 * z2 > k
        m2 = np.sign(y) * a2 * y2 < k
        d[m1] = np.sqrt(x2[m1] + z2[m1]) * il2 - rb
        d[m2 & ~m1] = np.sqrt(x2[m2 & ~m1] + y2[m2 & ~m1]) * il2 - ra
        m3 = ~m1 & ~m2
        d[m3] = (np.sqrt(x2[m3] * a2 * il2) + y[m3] * rr) * il2 - ra
        return d
    r = max(ra, rb)
    return Prim("cone", np.minimum(a, b) - r, np.maximum(a, b) + r, f, **kw)


def rbox(c, half, R, rad, **kw):
    c, half = np.asarray(c, float), np.asarray(half, float)

    def f(p):
        q = np.abs((p - c) @ R) - (half - rad)
        return np.linalg.norm(np.maximum(q, 0), axis=1) + np.minimum(q.max(1), 0) - rad
    ext = np.abs(R) @ half
    return Prim("box", c - ext, c + ext, f, **kw)


# ---------------------------------------------------------------- blending
def smin(a, b, k):
    if k <= 0:
        return np.minimum(a, b)
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b + (a - b) * h - k * h * (1 - h)


def smax(a, b, k):
    return -smin(-a, -b, k)


class Model:
    def __init__(self):
        self.prims = []
        self.disp = []   # (prim index, fn(p) -> displacement metres, outward)

    def add(self, prim):
        self.prims.append(prim)
        return len(self.prims) - 1

    def eval_prim(self, i, p, idx_sorted=None):
        return self.prims[i].fn(p) - (self.disp_fn(i)(p) if self.disp_fn(i) else 0)

    def disp_fn(self, i):
        for j, f in self.disp:
            if j == i:
                return f
        return None

    def evaluate(self, p, far=0.05, per_prim=False):
        """SDF of the whole model. Points are culled per primitive by bounding box (+ blend margin)."""
        n = len(p)
        order = np.argsort(p[:, 0], kind="stable")
        xs = p[order, 0]
        d = np.full(n, far, float)
        dp_all = [] if per_prim else None
        for i, pr in enumerate(self.prims):
            m = max(pr.k * 2, 0.004) + 0.01
            lo, hi = np.searchsorted(xs, pr.bmin[0] - m), np.searchsorted(xs, pr.bmax[0] + m, "right")
            cand = order[lo:hi]
            q = p[cand]
            sel = (q[:, 1] > pr.bmin[1] - m) & (q[:, 1] < pr.bmax[1] + m) & (q[:, 2] > pr.bmin[2] - m) & (q[:, 2] < pr.bmax[2] + m)
            cand = cand[sel]
            if len(cand) == 0:
                if per_prim:
                    dp_all.append((cand, np.zeros(0)))
                continue
            dp = pr.fn(p[cand])
            f = self.disp_fn(i)
            if f is not None:
                dp = dp - f(p[cand])
            if pr.op == "add":
                d[cand] = smin(d[cand], dp, pr.k)
            else:
                d[cand] = smax(d[cand], -dp, pr.k)
            if per_prim:
                dp_all.append((cand, dp))
        return (d, dp_all) if per_prim else d

    def gradient(self, p, eps=0.0006):
        e = np.eye(3) * eps
        g = np.stack([self.evaluate(p + e[i]) - self.evaluate(p - e[i]) for i in range(3)], 1)
        return g / np.maximum(np.linalg.norm(g, axis=1, keepdims=True), 1e-9)


# ---------------------------------------------------------------- noise
_rng = np.random.default_rng(1977)
_perm = np.concatenate([_rng.permutation(256)] * 2).astype(np.int64)
_grad = np.array([[1, 1, 0], [-1, 1, 0], [1, -1, 0], [-1, -1, 0], [1, 0, 1], [-1, 0, 1], [1, 0, -1], [-1, 0, -1],
                  [0, 1, 1], [0, -1, 1], [0, 1, -1], [0, -1, -1], [1, 1, 0], [-1, 1, 0], [0, -1, 1], [0, -1, -1]], float)


def perlin(p):
    pi = np.floor(p).astype(np.int64)
    pf = p - pi
    pi &= 255
    u = pf * pf * pf * (pf * (pf * 6 - 15) + 10)
    out = 0.0
    res = np.zeros(len(p))
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                h = _perm[_perm[_perm[pi[:, 0] + dx] + pi[:, 1] + dy] + pi[:, 2] + dz] & 15
                g = _grad[h]
                dot = g[:, 0] * (pf[:, 0] - dx) + g[:, 1] * (pf[:, 1] - dy) + g[:, 2] * (pf[:, 2] - dz)
                w = (u[:, 0] if dx else 1 - u[:, 0]) * (u[:, 1] if dy else 1 - u[:, 1]) * (u[:, 2] if dz else 1 - u[:, 2])
                res += w * dot
    return res


def fbm(p, freq, octaves=4, gain=0.5, lac=2.03, seed=0):
    off = np.array([seed * 17.13, seed * 31.7, seed * 7.31])
    s, a, f = np.zeros(len(p)), 1.0, freq
    norm = 0
    for o in range(octaves):
        s += a * perlin(p * f + off + o * 13.7)
        norm += a
        a *= gain
        f *= lac
    return s / norm


def ridged(p, freq, octaves=3, seed=0, sharp=8.0):
    """Thin branching lines (veins, cracks): 1 at a line, 0 away from it."""
    warp = np.stack([fbm(p, freq * 0.7, 2, seed=seed + 5 + i) for i in range(3)], 1) * (0.35 / freq)
    q = p + warp
    v = np.zeros(len(p))
    a, f = 1.0, freq
    for o in range(octaves):
        n = 1 - np.abs(perlin(q * f + seed * 3.1 + o * 9.1))
        v = np.maximum(v, a * n ** sharp)
        a *= 0.6
        f *= 2.1
    return v


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)
