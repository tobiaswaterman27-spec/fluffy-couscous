"""Skin weights for the body mesh: nearest sculpt parts per bone, then anatomical rules along each limb chain,
then the small 'detail' bones (lips, nostrils, brows, throat, chest)."""
import numpy as np
from sdf import smoothstep
from elliot import H0, UPPER, FORE

TAGS = ["skin", "nail", "mouth", "lip", "socket", "nostril", "ear"]


def gauss(d, s):
    return np.exp(-(d / s) ** 2)


def per_prim(model, P):
    """Distance of every vertex to every primitive's own surface (inf where culled)."""
    _, lst = model.evaluate(P, per_prim=True)
    return lst


def skin(el, P):
    model = el.m
    nb = len(el.bones)
    lst = per_prim(model, P)
    n = len(P)
    D = np.full((n, nb), np.inf)
    T = np.zeros((n, len(TAGS)))
    for pr, (cand, dp) in zip(model.prims, lst):
        if len(cand) == 0:
            continue
        b = el.index[pr.bone]
        ad = np.abs(dp)
        D[cand, b] = np.minimum(D[cand, b], ad)
        ti = TAGS.index(pr.tag)
        T[cand, ti] = np.maximum(T[cand, ti], np.exp(-ad / 0.0015))
    T[:, 0] = np.maximum(T[:, 0], 1e-3)
    T /= T.sum(1, keepdims=True)

    tau = np.full(nb, 0.008)
    for i, bn in enumerate(el.bones):
        if bn.name.startswith(("Finger", "Thumb")):
            tau[i] = 0.0025
        if bn.name.startswith("Toes"):
            tau[i] = 0.004
    dmin = D.min(1, keepdims=True)
    W = np.exp(-(D - dmin) / tau)
    W[~np.isfinite(D)] = 0
    W /= W.sum(1, keepdims=True)
    I = el.index
    x, y, z = P[:, 0], P[:, 1], P[:, 2]

    def chain(names, share):
        """Re-split the combined weight of a bone chain by a rule: share is a list of weight fractions per bone."""
        ids = [I[nm] for nm in names]
        tot = W[:, ids].sum(1)
        for i, s in zip(ids, share):
            W[:, i] = tot * s

    # arms: upper arm -> forearm at the elbow, forearm -> hand at the wrist
    for s, side in ((-1, "L"), (1, "R")):
        a = el.arm[s]
        t = (P - a["S"]) @ a["d"]
        s1 = smoothstep(UPPER - 0.07, UPPER + 0.05, t)
        s2 = smoothstep(UPPER + FORE - 0.035, UPPER + FORE + 0.012, t)
        chain(["UpperArm" + side, "ForeArm" + side, "Hand" + side], [(1 - s1), s1 * (1 - s2), s2])
        # legs: thigh -> shin at the knee, shin -> foot at the ankle, foot -> toes
        s1 = smoothstep(0.78, 0.67, y)
        s2 = smoothstep(0.125, 0.07, y)
        chain(["Thigh" + side, "Shin" + side, "Foot" + side], [(1 - s1), s1 * (1 - s2), s2])
        ft = smoothstep(0.14, 0.175, z)
        chain(["Foot" + side, "Toes" + side], [1 - ft, ft])
    # torso: pelvis -> spine, and the chest (breathing) over the ribcage
    s = smoothstep(1.39, 1.52, y)
    c = 0.75 * smoothstep(1.62, 1.72, y) * (1 - smoothstep(1.95, 2.03, y))
    chain(["Pelvis", "Spine", "Chest"], [1 - s, s * (1 - c), s * c])
    # neck base and head: blend over a few centimetres so the neck bends smoothly
    near = (np.abs(x) < 0.09)
    sn = smoothstep(1.975, 2.07, y) * near
    ids = [I["Spine"], I["Chest"], I["Neck"]]
    tot = W[:, ids].sum(1)
    body = W[:, I["Spine"]] + W[:, I["Chest"]]
    keep = np.where(body + W[:, I["Neck"]] > 0, body / np.maximum(body + W[:, I["Neck"]], 1e-9), 0)
    nw = tot * np.where(near, sn, W[:, I["Neck"]] / np.maximum(tot, 1e-9))
    rest = tot - nw
    cs = np.where(body > 0, W[:, I["Chest"]] / np.maximum(body, 1e-9), 0.5)
    W[:, I["Neck"]] = nw
    W[:, I["Spine"]] = rest * (1 - cs)
    W[:, I["Chest"]] = rest * cs
    # head / neck
    yl, zl, xl = y - H0[1], z - H0[2], x - H0[0]
    sh = smoothstep(0.0, 0.07, yl) * (np.abs(xl) < 0.11) * (y > 2.1)
    ids = [I["Neck"], I["Head"], I["Jaw"]]
    tot = W[:, ids].sum(1)
    hj = W[:, I["Head"]] + W[:, I["Jaw"]]
    jshare = np.where(hj > 0, W[:, I["Jaw"]] / np.maximum(hj, 1e-9), 0)
    headpart = np.where(y > 2.1, sh, hj / np.maximum(tot, 1e-9))
    # the jaw: everything below the mouth line at the front of the face; prim-based behind it
    front = smoothstep(-0.012, 0.012, zl)
    jrule = smoothstep(0.0785, 0.0715, yl)
    jshare = front * jrule + (1 - front) * jshare * smoothstep(0.135, 0.1, yl)
    # loose skin under the chin follows the jaw a little
    under = 0.55 * smoothstep(-0.03, 0.03, yl) * smoothstep(-0.005, 0.03, zl) * (1 - smoothstep(0.03, 0.05, yl))
    W[:, I["Neck"]] = tot * (1 - headpart)
    W[:, I["Head"]] = tot * headpart * (1 - jshare)
    W[:, I["Jaw"]] = tot * headpart * jshare
    mv = np.minimum(under, 1) * W[:, I["Neck"]]
    W[:, I["Neck"]] -= mv
    W[:, I["Jaw"]] += mv

    def detail(bone, frm, g):
        mvv = np.clip(g, 0, 1) * W[:, I[frm]]
        W[:, I[frm]] -= mvv
        W[:, I[bone]] += mvv

    lip = smoothstep(0.0765, 0.0795, yl) * (1 - smoothstep(0.09, 0.106, yl)) * smoothstep(0.055, 0.085, zl) * (1 - smoothstep(0.042, 0.06, np.abs(xl)))
    detail("LipUpper", "Head", 0.9 * lip)
    for nm in ("NostrilL", "NostrilR", "BrowL", "BrowR"):
        bp = el.bones[I[nm]].pos
        r = np.linalg.norm(P - bp, axis=1)
        detail(nm, "Head", (0.95 * gauss(r, 0.0065)) if nm.startswith("Nos") else 0.85 * gauss(r, 0.017))
    tp = el.bones[I["Throat"]].pos
    detail("Throat", "Neck", 0.85 * gauss(np.linalg.norm((P - tp) * [1, 0.6, 1], axis=1), 0.024) * smoothstep(-0.03, 0.0, z))

    # keep the 4 strongest influences
    order = np.argsort(-W, axis=1)[:, :4]
    bw = np.take_along_axis(W, order, 1)
    bw /= np.maximum(bw.sum(1, keepdims=True), 1e-9)
    return order.astype(np.uint16), bw.astype(np.float32), T, W


def region_masks(el, W, T):
    """Per-vertex scalars the texture baker uses (hands, feet, lower legs, head...)."""
    I = el.index
    def s(*names):
        return sum(W[:, I[n]] for n in names)
    fingers = [b.name for b in el.bones if b.name.startswith(("Finger", "Thumb"))]
    hand = s("HandL", "HandR", *fingers)
    tips = s(*[f for f in fingers if f.endswith("_3") or f.endswith("Thumb_2") or f.endswith("_2")])
    foot = s("FootL", "FootR", "ToesL", "ToesR")
    shin = s("ShinL", "ShinR")
    head = s("Head", "Jaw", "LipUpper", "NostrilL", "NostrilR", "BrowL", "BrowR")
    jaw = s("Jaw")
    forearm = s("ForeArmL", "ForeArmR")
    neck = s("Neck", "Throat")
    return np.stack([hand, tips, foot, shin, head, jaw, forearm, neck], 1).astype(np.float32), \
        ["hand", "tips", "foot", "shin", "head", "jaw", "forearm", "neck"]
