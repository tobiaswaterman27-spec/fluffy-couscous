"""Shop mannequins, made from the same MakeHuman bodies as the player characters: smoothed into fibreglass
(no nipples, no fingernails, a featureless egg of a head), some dressed in a plain top or shift dress.

Writes Assets/AfterHours/Models/Mannequin/mannequin_<n>.bytes ("MNQ1": verts, normals, submeshes:
0 = the fibreglass body, 1 = clothes). Run:  python3 mannequins.py"""
import os, sys, struct
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "elliotgen"))
from mh import Human, Skeleton
from garments import neighbours, vertex_normals, shell, smooth_edges
from mesh import decimate
from sdf import smoothstep

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets/AfterHours/Models/Mannequin")

VARIANTS = [
    dict(name="female_top", body=dict(gender=0.0, age=25, muscle=0.5, weight=0.3, height=0.62, race=(0, 0, 1), breast=0.35), clothes="top"),
    dict(name="male_bare", body=dict(gender=1.0, age=25, muscle=0.55, weight=0.35, height=0.7, race=(0, 0, 1)), clothes=None),
    dict(name="female_dressed", body=dict(gender=0.0, age=25, muscle=0.5, weight=0.3, height=0.62, race=(0, 0, 1), breast=0.35), clothes="outfit"),
    dict(name="male_top", body=dict(gender=1.0, age=25, muscle=0.55, weight=0.35, height=0.7, race=(0, 0, 1)), clothes="top"),
]


def laplace(V, F, sel, iters, k=0.5):
    A = neighbours(F, len(V)); d = np.asarray(A.sum(1)).ravel()
    V = V.copy()
    for _ in range(iters):
        avg = (A @ V) / np.maximum(d, 1)[:, None]
        V[sel] += k * (avg[sel] - V[sel])
    return V


def cap_holes(V, F, region):
    """Close every hole whose edge lies in 'region' with a fan around its centre."""
    e = np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]])
    key = np.sort(e, 1)
    u, inv, cnt = np.unique(key, axis=0, return_inverse=True, return_counts=True)
    bd = e[cnt[inv] == 1]                                   # boundary edges, in face winding order
    nxt = {a: b for a, b in bd}
    seen = set(); newV = [V]; newF = [F]; n = len(V)
    for start in list(nxt):
        if start in seen: continue
        loop = []; v = start
        while v not in seen and v in nxt:
            seen.add(v); loop.append(v); v = nxt[v]
        if len(loop) < 3 or region[loop].mean() < 0.7 or len(loop) > 200: continue
        cpos = V[loop].mean(0)
        newV.append(cpos[None]); ci = n; n += 1
        # the hole's edges run one way round; the cap faces must run the other
        newF.append(np.array([[loop[(i + 1) % len(loop)], loop[i], ci] for i in range(len(loop))]))
    return np.concatenate(newV), np.concatenate(newF)


def build(spec):
    h = Human(**spec["body"])
    sk = Skeleton(h)
    V, F, used = h.body_mesh()
    W = sk.W[used]; I = sk.index
    w = lambda *n: sum(W[:, I[x]] for x in n)
    # drop the eyeballs: the head becomes a blank egg
    for e in (sk.eye_l, sk.eye_r):
        F = F[~(np.linalg.norm(V[F].mean(1) - e, axis=1) < 0.016)]
    usedv = np.unique(F); rm = -np.ones(len(V), int); rm[usedv] = np.arange(len(usedv))
    V, F, W = V[usedv], rm[F], W[usedv]
    w = lambda *n: sum(W[:, I[x]] for x in n)
    hw = w("Head", "Jaw")
    head = hw > 0.3
    hands = sum(W[:, I[n]] for n in sk.names if n.startswith(("Finger", "Thumb", "Hand"))) > 0.3
    feet = w("FootL", "FootR", "ToesL", "ToesR") > 0.3
    # featureless egg head: drop everything inside the skull (teeth, tongue, eye sockets), then lay the rest onto
    # a smooth egg fitted to the head, blending into the neck
    hv = V[hw > 0.8]
    c = (hv.max(0) + hv.min(0)) / 2; c[1] -= 0.005
    R = (hv.max(0) - hv.min(0)) / 2 * np.array([0.86, 0.9, 0.9])
    def egg_r(d):
        # radius of the egg along unit directions d (narrower towards the chin)
        k = 1 - 0.18 * smoothstep(0.0, -0.9, d[:, 1])
        return 1 / np.sqrt((d[:, 0] / (R[0] * k)) ** 2 + (d[:, 1] / R[1]) ** 2 + (d[:, 2] / (R[2] * (0.92 + 0.08 * k))) ** 2)
    fc = V[F].mean(1)
    dd = fc - c; dl = np.linalg.norm(dd, axis=1) + 1e-9
    inside = (hw[F].mean(1) > 0.3) & (dl < egg_r(dd / dl[:, None]) * 0.82)
    F = F[~inside]
    dv = V - c; dlen = np.linalg.norm(dv, axis=1) + 1e-9; dn = dv / dlen[:, None]
    target = c + dn * egg_r(dn)[:, None]
    blend = smoothstep(0.35, 0.85, hw)
    V = V + (target - V) * blend[:, None]
    V = laplace(V, F, head, 10, 0.5)
    # faces left folded inwards (inner lips, eyelid creases) go, and the holes get capped
    for _ in range(2):
        fc = V[F].mean(1)
        fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
        fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-12
        out_dir = (fc - c) / (np.linalg.norm(fc - c, axis=1, keepdims=True) + 1e-9)
        facing = np.sum(fn * out_dir, 1)
        if np.median(facing[hw[F].mean(1) > 0.6]) < 0: facing = -facing     # whichever winding the mesh uses
        F = F[~((hw[F].mean(1) > 0.4) & (facing < 0.2))]
    nv0 = len(V)
    V, F = cap_holes(V, F, head)
    extra = len(V) - nv0
    if extra:
        hrow = W[np.argmax(hw)]
        W = np.vstack([W, np.tile(hrow, (extra, 1))])
        head = np.r_[head, np.ones(extra, bool)]; hands = np.r_[hands, np.zeros(extra, bool)]; feet = np.r_[feet, np.zeros(extra, bool)]
        w = lambda *n: sum(W[:, I[x]] for x in n)
    V = laplace(V, F, hands | feet, 8, 0.5)          # moulded hands, toes merged
    V = laplace(V, F, np.ones(len(V), bool), 6, 0.35) # fibreglass: everything softened
    # chest smooth (no nipples)
    chest = (w("Chest", "Spine") > 0.4) & (V[:, 2] > 0.0)
    V = laplace(V, F, chest, 80, 0.5)
    # flatten the points of the chest: inside 5 cm of each apex the surface is laid onto a smooth dome
    chY = sk.pos[I["Chest"]][1]; nkY = sk.pos[I["Neck"]][1]
    for side in (-1, 1):
        band = (V[:, 1] > chY + 0.05) & (V[:, 1] < nkY - 0.03) & (V[:, 0] * side > 0.03) & (V[:, 0] * side < 0.16) & (V[:, 2] > 0)
        if not band.any(): continue
        apex = V[band][np.argmax(V[band][:, 2])]
        rr = np.hypot(V[:, 0] - apex[0], V[:, 1] - apex[1])
        near = band & (rr < 0.06)
        ring = band & (rr > 0.055) & (rr < 0.075)
        if not ring.any(): continue
        zr = V[ring][:, 2].mean()
        dome = zr + (apex[2] - zr) * 0.6 * (1 - (rr[near] / 0.06) ** 2)
        V[near, 2] = np.minimum(V[near, 2], dome)
    V = laplace(V, F, chest, 10, 0.4)
    hipY0 = sk.pos[I["ThighR"]][1]
    crotch = (np.abs(V[:, 1] - (hipY0 - 0.05)) < 0.12) & (np.abs(V[:, 0]) < 0.12) & (w("Pelvis", "ThighL", "ThighR") > 0.4)
    V = laplace(V, F, crotch, 60, 0.5)               # moulded smooth, like real mannequins
    V[:, 1] -= V[:, 1].min()
    N = vertex_normals(V, F)
    subs = [F]
    allV = [V]
    if spec["clothes"]:
        y = V[:, 1]
        hipY, neckY = sk.pos[I["ThighR"]][1], sk.pos[I["Neck"]][1]
        torso = w("Pelvis", "Spine", "Chest", "Neck", "UpperArmL", "UpperArmR")
        body = dict(V=V, F=F, N=N)
        m = (torso > 0.5) & (y > hipY + 0.06) & (y < neckY + 0.02) & (w("ForeArmL", "ForeArmR") < 0.3)
        pieces = [shell(body, m, 0.006, "top", smooth=20, hem=hipY + 0.06)]
        if spec["clothes"] == "outfit":
            ankleY = sk.pos[I["FootR"]][1]
            legs = w("Pelvis", "ThighL", "ThighR", "ShinL", "ShinR")
            mb = (legs > 0.5) & (y < hipY + 0.12) & (y > ankleY + 0.04) & (w("FootL", "FootR") < 0.4)
            pieces.append(shell(body, mb, 0.009, "bottom", smooth=20, hem=ankleY + 0.04))
        o = sum(len(a) for a in allV)
        for part in pieces:
            allV.append(part.V)
            subs.append(part.F + o)
            o += len(part.V)
    V = np.concatenate(allV)
    if len(subs) > 2:
        subs = [subs[0], np.concatenate(subs[1:])]     # all clothes in one submesh
    # decimate each piece separately
    outV, outS, o = [], [], 0
    for s in subs:
        vi = np.unique(s); rm = -np.ones(len(V), int); rm[vi] = np.arange(len(vi))
        pv, pf = decimate(V[vi], rm[s], min(len(s), 9000))
        outV.append(pv); outS.append(pf + o); o += len(pv)
    V = np.concatenate(outV)
    Fall = np.concatenate(outS)
    N = vertex_normals(V, Fall)
    return V, N, outS


def write(name, V, N, subs):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, f"mannequin_{name}.bytes")
    with open(path, "wb") as f:
        f.write(b"MNQ1"); f.write(struct.pack("<i", len(V)))
        f.write(V.astype("<f4").tobytes()); f.write(N.astype("<f4").tobytes())
        f.write(struct.pack("<i", len(subs)))
        for s in subs:
            f.write(struct.pack("<i", s.size)); f.write(s.astype("<u4").tobytes())
    print("wrote", path, len(V), "verts", [len(s) for s in subs], "tris")
    return path


if __name__ == "__main__":
    for spec in VARIANTS:
        V, N, subs = build(spec)
        write(spec["name"], V, N, subs)
