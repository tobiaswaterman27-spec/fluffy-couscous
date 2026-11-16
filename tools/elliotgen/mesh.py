"""Turns the SDF into a mesh: coarse grid -> narrow band at fine resolution -> marching cubes in slabs -> weld."""
import numpy as np
from scipy.ndimage import zoom
from skimage.measure import marching_cubes
import fast_simplification


def polygonise(model, lo, hi, fine=0.0025, coarse=0.01, band=0.02, slab=96, log=print):
    lo, hi = np.asarray(lo, float), np.asarray(hi, float)
    ratio = int(round(coarse / fine))
    nc = np.ceil((hi - lo) / coarse).astype(int) + 1
    gx, gy, gz = [lo[i] + np.arange(nc[i]) * coarse for i in range(3)]
    P = np.stack(np.meshgrid(gx, gy, gz, indexing="ij"), -1).reshape(-1, 3)
    C = model.evaluate(P, far=0.2).reshape(nc)
    log(f"coarse grid {tuple(nc)}")
    nf = (nc - 1) * ratio + 1
    verts, faces, off = [], [], 0
    for y0 in range(0, nf[1] - 1, slab):
        y1 = min(nf[1], y0 + slab + 1)
        # coarse rows covering this slab
        cy0, cy1 = y0 // ratio, min(nc[1] - 1, (y1 - 1) // ratio + 1)
        sub = C[:, cy0:cy1 + 1, :]
        up = zoom(sub, ratio, order=1)  # (nx*r - ..)
        shape = ((sub.shape[0] - 1) * ratio + 1, (sub.shape[1] - 1) * ratio + 1, (sub.shape[2] - 1) * ratio + 1)
        up = zoom(sub, [(shape[i]) / sub.shape[i] for i in range(3)], order=1)[: shape[0], : shape[1], : shape[2]]
        a = y0 - cy0 * ratio
        F = up[:, a:a + (y1 - y0), :].copy()
        near = np.abs(F) < band
        idx = np.nonzero(near)
        if len(idx[0]) == 0:
            continue
        pts = np.stack([lo[0] + idx[0] * fine, lo[1] + (y0 + idx[1]) * fine, lo[2] + idx[2] * fine], 1)
        F[idx] = model.evaluate(pts, far=0.2)
        if F.min() > 0 or F.max() < 0:
            continue
        v, f, _, _ = marching_cubes(F, 0.0, spacing=(fine, fine, fine))
        v += [lo[0], lo[1] + y0 * fine, lo[2]]
        verts.append(v)
        faces.append(f + off)
        off += len(v)
        log(f"  slab y {y0}-{y1}: {len(f)} tris")
    V = np.concatenate(verts)
    Fc = np.concatenate(faces)
    # weld duplicated slab-boundary vertices
    key = np.round(V / (fine * 0.01)).astype(np.int64)
    _, uniq, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
    V = V[uniq]
    Fc = inv.reshape(-1)[Fc]
    good = (Fc[:, 0] != Fc[:, 1]) & (Fc[:, 1] != Fc[:, 2]) & (Fc[:, 0] != Fc[:, 2])
    return V, Fc[good]


def keep_largest(V, F):
    """Drop floating specks (marching-cubes islands)."""
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    n = len(V)
    e = np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]])
    g = coo_matrix((np.ones(len(e)), (e[:, 0], e[:, 1])), shape=(n, n))
    nc, lab = connected_components(g, directed=False)
    counts = np.bincount(lab[F[:, 0]], minlength=nc)
    keep = counts >= 0.002 * len(F)
    fm = keep[lab[F[:, 0]]]
    F = F[fm]
    used = np.unique(F)
    remap = -np.ones(n, int)
    remap[used] = np.arange(len(used))
    return V[used], remap[F]


def decimate(V, F, target):
    r = 1.0 - target / len(F)
    v, f = fast_simplification.simplify(V.astype(np.float32), F.astype(np.int32), target_reduction=r, agg=5)
    return v.astype(float), f.astype(np.int64)
