"""Clothes, shoes, hair and accessories fitted to a MakeHuman body.

Garments are shells grown out of the body itself (so they fit every shape), smoothed so the cloth bridges hollows
the way fabric does, with folds pressed in. Shoes, hair and accessories are sculpted (signed distance) around the
body's own joints and scalp. Each part has a 'tag' that decides its material and texture."""
import sys, os
import numpy as np
from scipy.spatial import cKDTree
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "elliotgen"))
from sdf import *
from mesh import polygonise, keep_largest, decimate


def vertex_normals(V, F):
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    n = np.zeros_like(V)
    for k in range(3):
        np.add.at(n, F[:, k], fn)
    return n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)


def neighbours(F, n):
    e = np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]])
    e = np.concatenate([e, e[:, ::-1]])
    from scipy.sparse import coo_matrix
    A = coo_matrix((np.ones(len(e)), (e[:, 0], e[:, 1])), shape=(n, n)).tocsr()
    A.data[:] = 1
    return A


def boundary_vertices(F, n):
    e = np.sort(np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]]), 1)
    u, c = np.unique(e, axis=0, return_counts=True)
    b = np.zeros(n, bool); b[u[c == 1].ravel()] = True
    return b


class Part:
    def __init__(self, V, F, tag, src=None, bones=None, rigid=None):
        self.V, self.F, self.tag = np.asarray(V, float), np.asarray(F, np.int64), tag
        self.src = src            # body vertex each vertex follows (skin weights copied from it)
        self.rigid = rigid        # or: one bone for everything (hair, shoes...)


def shell(body, mask, thickness, tag, smooth=12, folds=None, rng=None, hem=None):
    """A garment: the body faces inside 'mask', pushed out along the normals, smoothed like hanging cloth."""
    V, F, N = body["V"], body["F"], body["N"]
    keep = mask[F].all(1)
    Fs = F[keep]
    used = np.unique(Fs)
    remap = -np.ones(len(V), int); remap[used] = np.arange(len(used))
    Fs = remap[Fs]
    th = thickness(V[used]) if callable(thickness) else np.full(len(used), thickness)
    P = V[used] + N[used] * th[:, None]
    A = neighbours(Fs, len(used))
    deg = np.asarray(A.sum(1)).ravel()
    bnd = boundary_vertices(Fs, len(used))
    base = V[used]
    for _ in range(smooth):
        avg = (A @ P) / np.maximum(deg, 1)[:, None]
        P = np.where(bnd[:, None], P, P + 0.6 * (avg - P))
        # never sink back into the body
        d = np.sum((P - base) * N[used], 1)
        P += N[used] * np.maximum(0, th * 0.7 - d)[:, None]
    if folds is not None:
        P += N[used] * folds(P)[:, None]
    if hem is not None:
        # a clean, straight hem: the open edge near the hem line is laid onto it (and the ring above eased)
        low = bnd & (P[:, 1] < hem + 0.05)
        P[low, 1] = hem
        near = ~bnd & (P[:, 1] < hem + 0.025)
        P[near, 1] = np.maximum(P[near, 1], hem + 0.008)
    return Part(P, Fs, tag, src=used)


def sdf_part(model, lo, hi, tag, fine=0.003, target=None, rigid=None, src_tree=None):
    V, F = polygonise(model, lo, hi, fine=fine, coarse=fine * 3, log=lambda s: None)
    if len(F) == 0:
        return None
    V, F = keep_largest(V, F)
    if target and len(F) > target:
        V, F = decimate(V, F, target)
    src = src_tree.query(V)[1] if src_tree is not None else None
    return Part(V, F, tag, src=src, rigid=rigid)


# ---------------------------------------------------------------- folds
def sleeve_folds(axis_pts, amp=0.004):
    def f(P):
        return amp * fbm(P * [1, 2.4, 1], 14, 3, seed=11) + amp * 0.5 * np.sin(P[:, 1] * 90 + fbm(P, 5, 2, seed=12) * 5)
    return f


def torso_folds(amp=0.004):
    def f(P):
        vert = np.sin(np.arctan2(P[:, 0], P[:, 2]) * 7 + fbm(P, 3, 2, seed=21) * 4) * smoothstep(-0.2, 0.3, fbm(P, 4, 2, seed=22))
        return amp * (0.7 * vert + 0.8 * fbm(P, 12, 3, seed=23))
    return f


def trouser_folds(knee_y, amp=0.003):
    def f(P):
        y = P[:, 1]
        knee = np.exp(-((y - knee_y) / 0.06) ** 2) * np.sin(y * 170 + fbm(P, 8, 2, seed=31) * 6)
        return amp * (0.8 * knee + fbm(P * [1, 2.5, 1], 16, 3, seed=32))
    return f


def hang(part, a, b, select, window=0.3, loose=0.85, bins=48, tbins=60):
    """Make cloth hang like cloth: around the axis a->b (a = top), each point's distance from the axis is pushed
    out towards the widest the fabric has been in the 'window' above it, so it falls straight from the chest /
    the thigh / the upper arm instead of hugging every curve."""
    V = part.V
    ax = (b - a) / np.linalg.norm(b - a)
    L = np.linalg.norm(b - a)
    rel = V - a
    t = rel @ ax
    radial = rel - np.outer(t, ax)
    r = np.linalg.norm(radial, axis=1)
    ref = np.array([0, 0, 1.0]) if abs(ax[2]) < 0.9 else np.array([1.0, 0, 0])
    u = ref - ax * (ref @ ax); u /= np.linalg.norm(u); v = np.cross(ax, u)
    th = np.arctan2(radial @ v, radial @ u)
    ti = np.clip(((t / L) * tbins).astype(int), 0, tbins - 1)
    hi = ((th + np.pi) / (2 * np.pi) * bins).astype(int) % bins
    grid = np.zeros((tbins, bins))
    s = select
    np.maximum.at(grid, (ti[s], hi[s]), r[s])
    # widest over the window above (and a little angular blur so folds read as drapes)
    w = max(1, int(window / L * tbins))
    hung = np.zeros_like(grid)
    for k in range(tbins):
        hung[k] = grid[max(0, k - w):k + 1].max(0)
    hung = np.maximum(hung, np.roll(hung, 1, 1) * 0.97)
    hung = np.maximum(hung, np.roll(hung, -1, 1) * 0.97)
    target = hung[ti, hi]
    newr = np.where(s & (target > r), r + (target - r) * loose, r)
    scale = np.where(r > 1e-6, newr / np.maximum(r, 1e-6), 1)
    part.V = a + np.outer(t, ax) + radial * scale[:, None]
    return part
