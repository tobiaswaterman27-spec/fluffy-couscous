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


def shell(body, mask, thickness, tag, smooth=12, folds=None, rng=None, hem=None, bridge=0):
    """A garment: the body faces inside 'mask', pushed out along the normals, smoothed like hanging cloth.
    'bridge': how many passes the cloth gets to span small bumps and hollows (cloth lies across a chest,
    it doesn't cling into every detail of it)."""
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
    base = V[used].copy()
    for _ in range(bridge):
        avg = (A @ base) / np.maximum(deg, 1)[:, None]
        base = np.where(bnd[:, None], base, base + 0.6 * (avg - base))
    if bridge:
        P = base + N[used] * th[:, None]
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
    part = Part(P, Fs, tag, src=used)
    smooth_edges(part)
    return part


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
    # soften the profile so fabric falls from a rounded bust / shoulder, not from a point
    from scipy.ndimage import gaussian_filter1d
    empty = hung <= 0
    hung = gaussian_filter1d(hung, 1.6, axis=1, mode="wrap")
    hung = gaussian_filter1d(hung, 1.2, axis=0, mode="nearest")
    hung[empty] = 0
    # bilinear lookup in angle, so neighbouring bins don't leave steps
    fh = (th + np.pi) / (2 * np.pi) * bins - 0.5
    h0 = np.floor(fh).astype(int); fr = fh - h0
    target = hung[ti, h0 % bins] * (1 - fr) + hung[ti, (h0 + 1) % bins] * fr
    newr = np.where(s & (target > r), r + (target - r) * loose, r)
    scale = np.where(r > 1e-6, newr / np.maximum(r, 1e-6), 1)
    part.V = a + np.outer(t, ax) + radial * scale[:, None]
    return part


def cuff(part, elbow, wrist, side, wrist_r, ease=0.008, end_t=None):
    """Finish a sleeve: the ragged open edge is laid onto one plane across the forearm (a straight cuff), and
    the last few centimetres are drawn in towards the wrist (sleeves taper, they don't flare like a bell)."""
    d = wrist - elbow; L = np.linalg.norm(d); d = d / L
    P = part.V
    rel = P - elbow
    t = rel @ d
    rad0 = np.linalg.norm(rel - np.outer(t, d), axis=1)
    on = (P[:, 0] * side > 0.12) & (t > 0.3 * L) & (t < 1.3 * L) & (rad0 < wrist_r + 0.07)
    if not on.any(): return
    bnd = boundary_vertices(part.F, len(P)) & on
    if not bnd.any(): return
    te = np.percentile(t[bnd], 35) if end_t is None else end_t * L
    # the edge: onto the plane
    P[bnd] += d * (te - t[bnd])[:, None]
    # anything poking past it: pulled back behind the edge
    past = on & ~bnd & (t > te - 0.004)
    P[past] += d * ((te - 0.006) - t[past])[:, None]
    # taper: radius eased in towards the wrist over the last 7 cm
    rel = P - elbow; t = rel @ d
    radial = rel - np.outer(t, d)
    r = np.linalg.norm(radial, axis=1) + 1e-9
    k = smoothstep(te - 0.08, te - 0.005, t) * on
    target = np.minimum(r, wrist_r + ease)
    newr = r + (target - r) * k
    P[:] = elbow + np.outer(t, d) + radial * (newr / r)[:, None]


def smooth_edges(part, iters=10, select=None):
    """Even out every open edge (neckline, cuffs, hems): each edge vertex is eased towards its two neighbours
    along the edge (Taubin smoothing, so the opening doesn't shrink), then the ring next to it follows."""
    F, P = part.F, part.V
    e = np.sort(np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]]), 1)
    u, c = np.unique(e, axis=0, return_counts=True)
    be = u[c == 1]
    if len(be) == 0: return
    if select is not None:
        be = be[select[be[:, 0]] & select[be[:, 1]]]
        if len(be) == 0: return
    n = len(P)
    import scipy.sparse as sp
    A = sp.coo_matrix((np.ones(2 * len(be)), (np.r_[be[:, 0], be[:, 1]], np.r_[be[:, 1], be[:, 0]])), shape=(n, n)).tocsr()
    deg = np.asarray(A.sum(1)).ravel()
    b = deg > 0
    for _ in range(iters):
        for lam in (0.5, -0.53):
            avg = (A @ P) / np.maximum(deg, 1)[:, None]
            P[b] += lam * (avg[b] - P[b])
    # the ring of faces touching the edge relaxes a little towards its neighbours
    Afull = neighbours(F, n)
    dfull = np.asarray(Afull.sum(1)).ravel()
    ring = (np.asarray((Afull @ b.astype(float))).ravel() > 0) & ~b
    for _ in range(3):
        avg = (Afull @ P) / np.maximum(dfull, 1)[:, None]
        P[ring] += 0.4 * (avg[ring] - P[ring])


def unfold(part, iters=12):
    """Find triangles folded back over their neighbours (tiny base-mesh details such as nipples or a navel can
    flip when the cloth is offset and smoothed) and relax their corners until the surface lies flat."""
    P, F = part.V, part.F
    n = len(P)
    A = neighbours(F, n); deg = np.asarray(A.sum(1)).ravel()
    for _ in range(iters):
        fn = np.cross(P[F[:, 1]] - P[F[:, 0]], P[F[:, 2]] - P[F[:, 0]])
        fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-12
        vn = np.zeros_like(P); np.add.at(vn, F.ravel(), np.repeat(fn, 3, axis=0))
        vn /= np.linalg.norm(vn, axis=1, keepdims=True) + 1e-12
        agree = np.sum(fn * vn[F].mean(1), 1)
        bad = agree < 0.5
        if not bad.any(): break
        sel = np.zeros(n, bool); sel[F[bad].ravel()] = True
        # grow by one ring so the neighbourhood relaxes with it
        sel = sel | (np.asarray(A @ sel.astype(float)).ravel() > 0)
        avg = (A @ P) / np.maximum(deg, 1)[:, None]
        P[sel] += 0.8 * (avg[sel] - P[sel])


def flatten_dense_patches(part, region, max_area=4e-6, r_in=0.024, r_out=0.034):
    """Base-mesh details modelled with tiny triangles (nipples, the navel) crumple when cloth is laid over them.
    Find each cluster of tiny faces inside 'region' (a vertex mask), and re-lay the cloth there as a smooth
    disc: heights from a plane fitted to the ring around it, positions relaxed in that plane with the ring
    fixed (which can't fold), then eased into the surroundings."""
    P, F = part.V, part.F
    fn = np.cross(P[F[:, 1]] - P[F[:, 0]], P[F[:, 2]] - P[F[:, 0]])
    area = np.linalg.norm(fn, axis=1) / 2
    tiny = (area < max_area) & region[F].all(1)
    if not tiny.any(): return
    cents = P[F[tiny]].mean(1)
    done = np.zeros(len(cents), bool)
    A = neighbours(F, len(P)); deg = np.asarray(A.sum(1)).ravel()
    for k in range(len(cents)):
        if done[k]: continue
        c = cents[(np.linalg.norm(cents - cents[k], axis=1) < 0.03)].mean(0)
        done |= np.linalg.norm(cents - c, axis=1) < 0.035
        d = np.linalg.norm(P - c, axis=1)
        inner = d < r_in
        ring = (d >= r_in) & (d < r_out)
        if ring.sum() < 6 or inner.sum() < 3: continue
        # plane through the ring
        R = P[ring]; m = R.mean(0)
        _, _, vt = np.linalg.svd(R - m)
        nrm = vt[2]
        # in-plane relaxation with the ring fixed
        for _ in range(60):
            avg = (A @ P) / np.maximum(deg, 1)[:, None]
            P[inner] = avg[inner]
        # onto the plane (slightly domed so it isn't dead flat)
        rel = P[inner] - m
        h = rel @ nrm
        dome = 0.002 * (1 - (d[inner] / r_in) ** 2)
        P[inner] -= np.outer(h, nrm)
        P[inner] += np.outer(dome, nrm * np.sign(np.dot(nrm, [0, 0, 1]) + 1e-9))
        # blend the seam between the re-laid disc and the cloth around it
        band = (d >= r_in - 0.008) & (d < r_out + 0.012)
        for _ in range(10):
            avg = (A @ P) / np.maximum(deg, 1)[:, None]
            P[band] += 0.5 * (avg[band] - P[band])
