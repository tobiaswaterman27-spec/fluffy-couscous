"""Builds every playable character: MakeHuman body -> clothes, shoes, hair, accessories -> UVs -> baked textures ->
model files for the game (Resources/Player/Characters/<id>/) and the phone viewer.

    python3 build_chars.py [ids...] [--quick] [--viewer=DIR]
"""
import os, sys, json, time, struct, base64, gzip
import numpy as np
import xatlas, trimesh
from PIL import Image
from scipy.spatial import cKDTree
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "elliotgen"))
from mh import Human, Skeleton
from garments import *
from characters import CHARACTERS
from sdf import *
from bake import rasterize, dilate, interp, mix
from build import tangents, save_rgb, _img
from elliot import mat_to_quat

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--out=")),
           os.path.join(ROOT, "Assets/AfterHours/Resources/Player/Characters"))
QUICK = "--quick" in sys.argv
VIEWER = next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--viewer=")), None)
T0 = time.time()


def log(s):
    print(f"[{time.time() - T0:6.1f}s] {s}", flush=True)


# ================================================================ assembling one person
def build(spec):
    rng = np.random.default_rng(abs(hash(spec["id"])) % 2**32)
    h = Human(**spec["body"])
    sk = Skeleton(h)
    V, F, used = h.body_mesh()
    W = sk.W[used]
    N = vertex_normals(V, F)
    body = dict(V=V, F=F, N=N)
    I = sk.index
    P = lambda n: sk.pos[I[n]]
    def w(*names): return sum(W[:, I[n]] for n in names)
    y = V[:, 1]
    hipY, kneeY, ankleY, neckY = P("ThighR")[1], P("ShinR")[1], P("FootR")[1], P("Neck")[1]
    eyeY = (sk.eye_l[1] + sk.eye_r[1]) / 2
    headw = w("Head", "Jaw")
    handw = w("HandL", "HandR", *[n for n in sk.names if n.startswith(("Finger", "Thumb"))])
    footw = w("FootL", "FootR", "ToesL", "ToesR")
    # how far along the forearm each vertex is (sleeves end at the wrist)
    fore_t = np.zeros(len(V))
    for s in "LR":
        e, wr = P("ForeArm" + s), P("Hand" + s)
        d = wr - e; L = np.linalg.norm(d)
        t = (V - e) @ (d / L) / L
        sel = w("ForeArm" + s, "Hand" + s) > 0.3
        fore_t[sel] = np.maximum(fore_t[sel], t[sel])
    parts = []
    covered = np.zeros(len(V), bool)       # body skin that clothes hide (removed so it can't poke through)
    tree = cKDTree(V)

    # ---------------- bottoms
    bot = spec["bottom"]
    waistY = hipY + (0.12 if bot != "leggings" else 0.1)
    hem = ankleY + {"jeans": 0.01, "trousers": 0.005, "cargo": 0.03, "joggers": 0.045, "leggings": 0.05}[bot]
    m_bot = (w("Pelvis", "Spine", "ThighL", "ThighR", "ShinL", "ShinR") > 0.5) & (y < waistY) & (y > hem) & (footw < 0.4)
    thick = {"jeans": 0.011, "trousers": 0.015, "cargo": 0.016, "joggers": 0.016, "leggings": 0.0025}[bot]
    # trousers lie smooth over the groin (the anatomy underneath doesn't show through the cloth)
    groin = (np.abs(V[:, 0]) < 0.1) & (y > hipY - 0.12) & (y < hipY + 0.08) & (V[:, 2] > P("Pelvis")[2] - 0.01)
    Vb = V.copy()
    Ab = neighbours(F, len(V)); db = np.asarray(Ab.sum(1)).ravel()
    for _ in range(40):
        avg = (Ab @ Vb) / np.maximum(db, 1)[:, None]
        Vb[groin] += 0.5 * (avg[groin] - Vb[groin])
    body_bot = dict(V=Vb, F=F, N=vertex_normals(Vb, F))
    pb = shell(body_bot, m_bot, lambda p: np.full(len(p), thick) + 0.006 * smoothstep(hipY + 0.05, hipY - 0.15, p[:, 1]) * (bot in ("cargo", "joggers", "trousers")),
               "bottom", hem=hem, smooth=22 if bot != "leggings" else 3, folds=trouser_folds(kneeY, 0.0025 if bot != "leggings" else 0.0008))
    if bot != "leggings":
        # straight legs: each leg falls from the thigh to the hem
        for s, side in (("L", -1), ("R", 1)):
            leg = (pb.V[:, 0] * side > 0.01) & (pb.V[:, 1] < hipY - 0.06)
            hang(pb, P("Thigh" + s) + [0, -0.05, 0], P("Foot" + s), leg, window=0.32 if bot != "jeans" else 0.26, loose=0.65 if bot != "jeans" else 0.6)
    unfold(pb)
    parts.append(pb)
    covered |= m_bot & (y < waistY - (0.008 if bot == "leggings" else 0.03)) & (y > hem + 0.03)

    # ---------------- top
    top = spec["top"]
    # tops lie across the chest rather than clinging into every detail of it: they're made over a copy of the
    # body whose chest is smoothed (no nipples, softened cleavage), the way a bra and a layer of cloth would
    chest_sel = (w("Chest", "Spine") > 0.4) & (V[:, 2] > P("Chest")[2]) & (np.abs(V[:, 0]) < 0.2) & (y > hipY + 0.15) & (y < neckY - 0.04)
    Vs = V.copy()
    An = neighbours(F, len(V)); dn = np.asarray(An.sum(1)).ravel()
    for _ in range(45):
        avg = (An @ Vs) / np.maximum(dn, 1)[:, None]
        Vs[chest_sel] += 0.5 * (avg[chest_sel] - Vs[chest_sel])
    body_top = dict(V=Vs, F=F, N=vertex_normals(Vs, F))
    hemY = {"hoodie": hipY - 0.03, "cardigan": hipY - 0.06, "security": hipY + 0.02, "workjacket": hipY - 0.02,
            "jacket": hipY - 0.0, "tabard": hipY + 0.02, "coat": kneeY + 0.12}[top]
    sleeve_end = {"tabard": 0.75}.get(top, 0.93)
    torso = w("Pelvis", "Spine", "Chest", "Neck", "UpperArmL", "UpperArmR", "ForeArmL", "ForeArmR")
    if top == "coat":
        torso = torso + w("ThighL", "ThighR") * (y > hemY)
    thighw = w("ThighL", "ThighR")
    # the crotch: the lowest point of the body between the legs
    between = (np.abs(V[:, 0]) < 0.025) & (y < hipY + 0.05) & (y > kneeY) & (np.abs(V[:, 2] - P("Pelvis")[2]) < 0.12)
    crotchY = float(V[between][:, 1].max()) if between.any() else hipY - 0.06
    crotchY = float(np.percentile(V[between][:, 1], 95)) if between.sum() > 5 else crotchY
    if top != "coat":
        hemY = max(hemY, crotchY + 0.02)           # a hem goes straight across, it never cuts up between the legs
    torso = torso + (thighw > 0.2) * (y > hemY) * (top != "coat")                    # the hips too, so the hem runs straight round
    m_top = (torso > 0.5) & (y > hemY) & (y < neckY + (0.035 if top != "tabard" else 0.02)) & (headw < 0.3) & (handw < 0.3) & (fore_t < sleeve_end)
    tthick = {"hoodie": 0.03, "cardigan": 0.016, "security": 0.02, "workjacket": 0.028, "jacket": 0.026, "tabard": 0.012, "coat": 0.032}[top]
    def top_thick(p):
        armness = np.zeros(len(p))
        return np.full(len(p), tthick) + 0.008 * smoothstep(hipY + 0.25, hipY, p[:, 1])
    if top == "tabard":
        # a long-sleeved top underneath, the tabard over it (that's the coloured part)
        under = shell(body_top, m_top | ((torso > 0.5) & (y > hipY + 0.02) & (y < neckY + 0.02) & (headw < 0.3) & (handw < 0.3) & (fore_t < 0.93)),
                      0.006, "undershirt", smooth=6, folds=sleeve_folds(None, 0.002))
        for s, side in (("L", -1), ("R", 1)):
            cuff(under, P("ForeArm" + s), P("Hand" + s), side, wrist_radius(V, P("ForeArm" + s), P("Hand" + s), fore_t), ease=0.004)
        parts.append(under)
        m_tab = (w("Pelvis", "Spine", "Chest", "Neck") > 0.55) & (y > hipY - 0.03) & (y < neckY + 0.0) & (headw < 0.3)
        ptab = shell(body_top, m_tab, 0.02, "top", smooth=20, folds=torso_folds(0.003), hem=hipY - 0.03)
        shY = P("UpperArmR")[1]
        hang(ptab, np.array([0, shY - 0.1, P("Neck")[2]]), np.array([0, hipY - 0.15, P("Pelvis")[2]]), ptab.V[:, 1] < shY - 0.1, window=0.35, loose=0.85)
        parts.append(ptab)
        covered |= (torso > 0.5) & (y > hipY + 0.05) & (y < neckY - 0.02) & (fore_t < 0.88)
    else:
        pt = shell(body_top, m_top, top_thick, "top", smooth=30, hem=hemY, folds=lambda p: torso_folds(0.005)(p) + sleeve_folds(None, 0.003)(p))
        # loose: the body of it hangs from below the arms, the sleeves from the shoulders (the skeleton says which is which)
        Wp = W[pt.src]
        for s, side in (("L", -1), ("R", 1)):
            sh, wr = P("UpperArm" + s), P("Hand" + s)
            d = (wr - sh) / np.linalg.norm(wr - sh)
            sel = (Wp[:, I["UpperArm" + s]] + Wp[:, I["ForeArm" + s]] > 0.5)
            # the sleeve falls from the widest part of the upper arm straight past the elbow (no dip into it)
            hang(pt, sh + d * 0.06, wr, sel, window=0.32, loose={"hoodie": 0.9, "jacket": 0.85, "workjacket": 0.88}.get(top, 0.75))
            # and the cloth over the elbow is relaxed so the crease there softens
            Ap = neighbours(pt.F, len(pt.V)); dp = np.asarray(Ap.sum(1)).ravel()
            elb = P("ForeArm" + s)
            near = sel & (np.linalg.norm(pt.V - elb, axis=1) < 0.09)
            base = pt.V.copy()
            for _ in range(12):
                avg = (Ap @ pt.V) / np.maximum(dp, 1)[:, None]
                pt.V[near] += 0.5 * (avg[near] - pt.V[near])
            # never shrink inwards while relaxing (only the dent fills)
            ax = (wr - sh) / np.linalg.norm(wr - sh)
            def rad(Q): r = Q - sh; return np.linalg.norm(r - np.outer(r @ ax, ax), axis=1)
            shrink = near & (rad(pt.V) < rad(base))
            pt.V[shrink] = base[shrink]
        for s, side in (("L", -1), ("R", 1)):
            cuff(pt, P("ForeArm" + s), P("Hand" + s), side, wrist_radius(V, P("ForeArm" + s), P("Hand" + s), fore_t),
                 ease={"hoodie": 0.006, "cardigan": 0.006, "security": 0.01, "workjacket": 0.016, "jacket": 0.014}.get(top, 0.01))
        armw = sum(Wp[:, I[n]] for n in ("UpperArmL", "UpperArmR", "ForeArmL", "ForeArmR"))
        shY = P("UpperArmR")[1]
        body_sel = (armw < 0.15) & (pt.V[:, 1] < shY - 0.1)
        hang(pt, np.array([0, shY - 0.1, P("Neck")[2] - 0.01]), np.array([0, hemY - 0.05, P("Pelvis")[2]]), body_sel,
             window=0.35, loose={"hoodie": 0.85, "jacket": 0.8, "workjacket": 0.85, "security": 0.75, "cardigan": 0.78}.get(top, 0.6))
        # relax the cloth over the chest and belly: no bumps poking up through it
        sel = (chest_sel | ((w("Pelvis", "Spine") > 0.4) & (V[:, 2] > P("Spine")[2]) & (np.abs(V[:, 0]) < 0.18) & (y > hipY)))[pt.src]
        Ap = neighbours(pt.F, len(pt.V)); dp = np.asarray(Ap.sum(1)).ravel()
        Nb = body_top["N"][pt.src]; Bb = Vs[pt.src]
        floor_d = np.maximum(np.sum((pt.V - Bb) * Nb, 1), tthick * 0.6)   # never sink below where it hangs now
        for _ in range(40):
            avg = (Ap @ pt.V) / np.maximum(dp, 1)[:, None]
            pt.V[sel] += 0.5 * (avg[sel] - pt.V[sel])
            dd = np.sum((pt.V - Bb) * Nb, 1)
            push = np.maximum(0, floor_d - dd) * sel
            pt.V += Nb * push[:, None]
        flatten_dense_patches(pt, (V[:, 2] > P("Spine")[2])[pt.src] & (pt.V[:, 1] > hipY - 0.05) & (pt.V[:, 1] < neckY - 0.02) & (np.abs(pt.V[:, 0]) < 0.2))
        unfold(pt)
        smooth_edges(pt, iters=14)
        # last: the sleeves fall straight over the elbows (earlier smoothing can pull a dent back in)
        Wp2 = W[pt.src]
        for s, side in (("L", -1), ("R", 1)):
            sh, wr = P("UpperArm" + s), P("Hand" + s)
            d = (wr - sh) / np.linalg.norm(wr - sh); L = np.linalg.norm(wr - sh)
            sel = (Wp2[:, I["UpperArm" + s]] + Wp2[:, I["ForeArm" + s]] > 0.5)
            tt = (pt.V - sh) @ d / L
            hang(pt, sh + d * 0.06, wr, sel, window=0.35, loose=0.85, apply=smoothstep(0.22, 0.42, tt) * (1 - smoothstep(0.66, 0.84, tt)))
        # then relax the sleeves along their length so nothing is left stepped
        Ap = neighbours(pt.F, len(pt.V)); dp = np.asarray(Ap.sum(1)).ravel()
        arms = (Wp2[:, I["UpperArmL"]] + Wp2[:, I["ForeArmL"]] + Wp2[:, I["UpperArmR"]] + Wp2[:, I["ForeArmR"]] > 0.5) & ~boundary_vertices(pt.F, len(pt.V))
        for _ in range(8):
            avg = (Ap @ pt.V) / np.maximum(dp, 1)[:, None]
            pt.V[arms] += 0.45 * (avg[arms] - pt.V[arms])
        parts.append(pt)
        covered |= (torso > 0.5) & (y > hemY + 0.02) & (y < neckY - 0.02) & (fore_t < sleeve_end - 0.05) & (headw < 0.2) & (handw < 0.1)
    covered |= (footw > 0.5) & (y < ankleY + (0.12 if spec["shoes"] == "boots" else 0.05))

    # ---------------- sculpted bits: hood, collar, pockets, shoes, hair, accessories
    hj = P("Head")
    headV = V[(headw > 0.5) & (y > neckY + 0.02)]
    headN = N[(headw > 0.5) & (y > neckY + 0.02)]
    htree = cKDTree(headV)

    def scalp(p):
        """Signed distance to the scalp (approximate): + outside the head."""
        d, i = htree.query(p)
        return d * np.sign(np.sum((p - headV[i]) * headN[i], 1) + 1e-9)

    face_z = headV[:, 2].max()
    back_z = headV[:, 2].min()
    hcx = 0.0
    hm = Model()
    def hadd(prim, tag, k=0.0, op="add"):
        prim.bone, prim.tag, prim.op, prim.k = "Head", tag, op, k
        return hm.add(prim)

    style = spec["hair"]["style"]
    def hairline(p, front_up=0.05, side_down=0.0, back_down=0.035):
        q = p - hj
        zf = (p[:, 2] - back_z) / max(face_z - back_z, 1e-3)          # 0 at the back, 1 at the face
        line = eyeY + front_up * smoothstep(0.55, 0.85, zf) + side_down * (1 - smoothstep(0.55, 0.85, zf)) * 0 \
            - back_down * (1 - smoothstep(0.1, 0.5, zf)) + 0.01
        sideburn = smoothstep(0.035, 0.07, np.abs(p[:, 0])) * smoothstep(0.4, 0.7, zf) * 0.035
        return line - sideburn
    def hair_prim(thick, line_kw=None, extra=None, noise=0.004, freq=45):
        lk = line_kw or {}
        lo, hi = hj - 0.2, hj + 0.25
        def f(p):
            above = p[:, 1] - hairline(p, **lk)
            taper = 0.35 + 0.65 * smoothstep(0.0, 0.025, above)
            d = scalp(p) - thick * taper - noise * taper * fbm(p, freq, 3, seed=5)
            return np.maximum(d, -above)
        return Prim("hair", lo, hi, f)
    if style == "messy":
        hadd(hair_prim(0.014, noise=0.007, freq=30), "hair")
    elif style == "short":
        hadd(hair_prim(0.01, noise=0.004), "hair")
    elif style == "buzz":
        hadd(hair_prim(0.0035, noise=0.0008), "hair")
    elif style == "receding":
        hadd(hair_prim(0.006, line_kw=dict(front_up=0.085), noise=0.0012, freq=90), "hair")
    elif style == "bob":
        def f(p):
            zf = (p[:, 2] - back_z) / max(face_z - back_z, 1e-3)
            line = np.where(zf > 0.72, eyeY + 0.05, eyeY - 0.075)
            # uneven strand ends, and the hair thins to its tips (no flat-cut underside to catch the dark)
            line = line + 0.005 * np.sin(p[:, 0] * 140 + 3 * fbm(p, 20, 1, seed=61)) + 0.004 * fbm(p, 60, 2, seed=62)
            tip = 0.2 + 0.8 * smoothstep(0.0, 0.03, p[:, 1] - line)
            d = scalp(p) - 0.02 * tip - 0.004 * tip * fbm(p * [1, 3, 1], 30, 2, seed=6)
            return np.maximum(d, line - p[:, 1])
        hadd(Prim("hair", hj - 0.2, hj + 0.25, f), "hair")
    elif style == "ponytail":
        hadd(hair_prim(0.007, noise=0.002), "hair")
        a = np.array([0, eyeY + 0.04, back_z - 0.005]); b = np.array([0, eyeY - 0.2, back_z - 0.07])
        hadd(cone(a, b, 0.028, 0.012), "hair", k=0.02)
        hadd(sphere(a + [0, 0, 0.005], 0.016), "band", k=0.004)
    elif style == "curlybun":
        hadd(hair_prim(0.012, noise=0.006, freq=70), "hair")
        c = np.array([0, eyeY + 0.13, back_z + 0.03])
        bun = sphere(c, 0.05); f0 = bun.fn
        bun.fn = lambda p, f0=f0: f0(p) - 0.006 * fbm(p, 80, 2, seed=9)
        hadd(bun, "hair", k=0.02)
    elif style == "afro":
        def f(p):
            d = scalp(p) - 0.045 - 0.008 * fbm(p, 60, 3, seed=7)
            return np.maximum(d, hairline(p, front_up=0.06) - 0.01 - p[:, 1])
        hadd(Prim("hair", hj - 0.25, hj + 0.3, f), "hair")
    elif style == "wrap":
        pass   # covered by the headwrap
    beard = spec.get("beard", "none")
    if beard in ("short", "grey"):
        def f(p):
            zf = (p[:, 2] - back_z) / max(face_z - back_z, 1e-3)
            q = p[:, 1]
            region = np.maximum(np.maximum((q - (eyeY - 0.045)), 0.55 - zf), (neckY - 0.01) - q)
            mouth = 0.012 - np.hypot(np.abs(p[:, 0]) / 2.2, q - (eyeY - 0.075))   # keep the lips clear
            # thick in the middle, thinning to nothing at its edges, so it grows out of the skin, not sits on it
            inside = -np.maximum(region, mouth)
            thick = 0.0012 + 0.0055 * smoothstep(0.0, 0.018, inside)
            d = scalp(p) - thick - 0.0018 * fbm(p, 120, 2, seed=8) * smoothstep(0.0, 0.01, inside)
            return np.maximum(np.maximum(d, region), mouth)
        hadd(Prim("beard", hj - 0.2, hj + 0.2, f), "beard")
    if "headwrap" in spec["extras"]:
        def f(p):
            return np.maximum(scalp(p) - 0.03 - 0.006 * fbm(p * [1, 2, 1], 20, 2, seed=10), hairline(p, front_up=0.055, back_down=0.06) - p[:, 1])
        hadd(Prim("wrap", hj - 0.25, hj + 0.3, f), "wrap")
        hadd(ellipsoid(np.array([0, eyeY + 0.11, face_z - 0.045]), [0.04, 0.025, 0.03]), "wrap", k=0.02)
    if "beanie" in spec["extras"]:
        def f(p):
            edge = hairline(p, front_up=0.05, back_down=-0.01)
            cuff = smoothstep(0.045, 0.03, p[:, 1] - edge)                 # the folded-up brim, a bit thicker
            return np.maximum(scalp(p) - 0.03 - 0.006 * cuff - 0.002 * np.sin(np.arctan2(p[:, 0], p[:, 2]) * 60), edge - p[:, 1])
        hadd(Prim("beanie", hj - 0.25, hj + 0.3, f), "beanie")
    if "glasses" in spec["extras"]:
        for e in (sk.eye_l, sk.eye_r):
            c = e + [0, 0, 0.022]
            ring = Prim("ring", c - 0.04, c + 0.04, lambda p, c=c: np.maximum(np.abs(np.hypot((p[:, 0] - c[0]) / 1.25, p[:, 1] - c[1]) - 0.019) - 0.0018, np.abs(p[:, 2] - c[2]) - 0.0015))
            hadd(ring, "glasses")
            side = np.sign(e[0])
            hadd(cone(c + [side * 0.024, 0.003, 0], c + [side * 0.075, 0.006, -0.1], 0.0018, 0.0018), "glasses")
        hadd(cone(sk.eye_l + [0.018, 0.004, 0.024], sk.eye_r + [-0.018, 0.004, 0.024], 0.0016, 0.0016), "glasses")
    hair_part = None
    if hm.prims:
        hair_part = sdf_part(hm, hj - [0.2, 0.3, 0.2], hj + [0.2, 0.3, 0.25], "hair", fine=0.0025 if not QUICK else 0.004, target=9000, src_tree=tree)
        if hair_part is not None:
            # per-vertex tag from the nearest sculpt part (hair / beard / glasses / wrap / beanie / band)
            _, lst = hm.evaluate(hair_part.V, per_prim=True)
            best = np.full(len(hair_part.V), np.inf); tagv = np.array(["hair"] * len(hair_part.V), dtype=object)
            for pr, (cand, dp) in zip(hm.prims, lst):
                if len(cand) == 0: continue
                better = np.abs(dp) < best[cand]
                best[cand[better]] = np.abs(dp[better]); tagv[cand[better]] = pr.tag
            hair_part.vtag = tagv
            hair_part.rigid = "Head" if not (style == "ponytail") else None
            parts.append(hair_part)

    # shoes
    sm = Model()
    for s in "LR":
        a, toe = P("Foot" + s), P("Toes" + s)
        fwd = toe - a; fwd[1] = 0; L = np.linalg.norm(fwd); fwd /= L
        side = np.cross([0, 1, 0], fwd)
        heel = a - fwd * 0.07
        tip = toe + fwd * 0.08
        R = np.stack([side, [0, 1, 0], fwd], 1)
        length = np.linalg.norm(tip - heel)
        c = (heel + tip) / 2
        kind = spec["shoes"]
        hgt = {"trainers": 0.085, "boots": 0.15, "work": 0.08, "flats": 0.06}[kind]
        wid = {"trainers": 0.05, "boots": 0.052, "work": 0.048, "flats": 0.044}[kind]
        def sadd(prim, tag, k=0.0):
            prim.bone, prim.tag, prim.op, prim.k = "Foot" + s, tag, "add", k
            sm.add(prim)
        # a real shoe shape: the outline narrows at the heel, widens across the ball, rounds at the toe;
        # the upper rises to a collar round the ankle and slopes down to a low toe box; the sole follows the outline
        def footprint(p, heel=heel, fwd=fwd, side=side, length=length, wid=wid, grow=0.0):
            # one tapered outline from heel to toe (narrow heel, wide ball, rounded toe)
            q = p - heel
            u = q @ side; t = (q @ fwd) / length
            tc = np.clip(t, 0.12, 0.9)
            r = np.interp(tc, [0.12, 0.45, 0.68, 0.9], [wid * 0.8, wid * 0.92, wid * 1.08, wid * 0.7])
            d = np.hypot(u, (t - tc) * length) - (r + grow)
            return d, t
        def upper_fn(p, footprint=footprint, hgt=hgt):
            d2, t = footprint(p)
            top = hgt + 0.02 - (hgt - 0.045) * smoothstep(0.22, 0.62, t) - 0.012 * smoothstep(0.8, 1.0, t)
            dy = p[:, 1] - top
            r = 0.018
            qx, qy = np.maximum(d2 + r, 0), np.maximum(dy + r, 0)
            return np.hypot(qx, qy) + np.minimum(np.maximum(d2 + r, dy + r), 0) - r
        def sole_fn(p, footprint=footprint):
            d2, _ = footprint(p, grow=0.004)
            return np.maximum(np.maximum(d2, p[:, 1] - 0.024), -p[:, 1]) - 0.002
        lo = np.minimum(heel, tip) - 0.12; hi = np.maximum(heel, tip) + 0.12
        lo[1] = -0.01; hi[1] = hgt + 0.06
        sadd(Prim("upper", lo, hi, upper_fn), "shoe", 0.006)
        sadd(Prim("sole", lo, hi, sole_fn), "sole" if kind != "boots" else "boot_sole", 0.003)
    shoe_part = sdf_part(sm, np.array([-0.4, -0.01, -0.3]), np.array([0.4, 0.25, 0.4]), "shoe", fine=0.003 if not QUICK else 0.004, target=6000, src_tree=tree)
    if shoe_part is not None:
        _, lst = sm.evaluate(shoe_part.V, per_prim=True)
        best = np.full(len(shoe_part.V), np.inf); tagv = np.array(["shoe"] * len(shoe_part.V), dtype=object)
        for pr, (cand, dp) in zip(sm.prims, lst):
            if len(cand) == 0: continue
            better = np.abs(dp) < best[cand]
            best[cand[better]] = np.abs(dp[better]); tagv[cand[better]] = pr.tag
        shoe_part.vtag = tagv
        parts.append(shoe_part)

    # body extras: lanyard, backpack, apron
    em = Model()
    ch = P("Chest"); nk = P("Neck")
    def eadd(prim, tag, bone="Chest", k=0.0):
        prim.bone, prim.tag, prim.op, prim.k = bone, tag, "add", k
        em.add(prim)
    btree_top = cKDTree(V[(torso > 0.5) & (y > hipY) & (headw < 0.2) & (handw < 0.2)])
    card_c = None
    if "lanyard" in spec["extras"]:
        card = np.array([0.03, nk[1] - 0.2, 0]);
        zc = V[(torso > 0.5) & (np.abs(V[:, 0] - 0.03) < 0.03) & (np.abs(y - card[1]) < 0.03)][:, 2].max() + tthick + 0.012
        card[2] = zc
        for sgn in (-1, 1):
            a = np.array([sgn * 0.05, nk[1] + 0.01, nk[2] + 0.04]); eadd(cone(a, card + [0, 0.035, -0.003], 0.003, 0.003), "strap")
        eadd(rbox(card, [0.028, 0.04, 0.002], np.eye(3), 0.002), "card")
        card_c = card.copy()
    if "backpack" in spec["extras"]:
        bz = V[(torso > 0.5) & (y > hipY + 0.2) & (y < nk[1] - 0.05)][:, 2].min() - tthick - 0.06
        c = np.array([0, nk[1] - 0.26, bz])
        eadd(rbox(c, [0.14, 0.2, 0.065], np.eye(3), 0.04), "pack")
        eadd(rbox(c + [0, -0.06, -0.06], [0.11, 0.1, 0.02], np.eye(3), 0.02), "pack", k=0.02)
        for sgn in (-1, 1):
            sh = P("UpperArm" + ("R" if sgn > 0 else "L"))
            eadd(cone(c + [sgn * 0.09, 0.17, 0.04], np.array([sgn * 0.11, sh[1] + 0.05, sh[2] + 0.02]), 0.012, 0.012), "strap")
            front = np.array([sgn * 0.1, sh[1] - 0.2, V[(torso > 0.5) & (np.abs(y - (sh[1] - 0.2)) < 0.03)][:, 2].max() + tthick + 0.01])
            eadd(cone(np.array([sgn * 0.11, sh[1] + 0.05, sh[2] + 0.02]), front, 0.012, 0.012), "strap")
            eadd(cone(front, c + [sgn * 0.12, -0.15, 0.06], 0.012, 0.012), "strap")
    if "apron" in spec["extras"]:
        zt = V[(np.abs(y - (hipY + 0.14)) < 0.02) & (np.abs(V[:, 0]) < 0.05)][:, 2].max() + tthick + 0.03
        zb = V[(np.abs(y - (hipY - 0.15)) < 0.03) & (np.abs(V[:, 0]) < 0.15)][:, 2].max() + 0.025
        top_c = np.array([0, hipY + 0.14, zt]); bot_c = np.array([0, hipY - 0.33, max(zb, zt - 0.01)])
        ap = cone(top_c, bot_c, 0.02, 0.02)
        def apron_fn(p, a=top_c, b=bot_c):
            # a sheet: thin in depth, widening a little to the hem, hanging in soft vertical folds
            t = np.clip((p[:, 1] - a[1]) / (b[1] - a[1]), 0, 1)
            zc = a[2] + (b[2] - a[2]) * t + 0.006 * np.sin(p[:, 0] * 70 + 2 * fbm(p, 6, 2, seed=71)) * t
            half_w = 0.15 + 0.03 * t
            dx = np.abs(p[:, 0]) - half_w
            dy = np.maximum(p[:, 1] - a[1], b[1] - p[:, 1])
            dz = np.abs(p[:, 2] - zc) - 0.003
            q = np.stack([np.maximum(dx, 0), np.maximum(dy, 0), np.maximum(dz, 0)], 1)
            return np.linalg.norm(q, axis=1) + np.minimum(np.maximum(np.maximum(dx, dy), dz), 0) - 0.002
        eadd(Prim("apron", np.array([-0.25, bot_c[1] - 0.02, min(zt, zb) - 0.06]), np.array([0.25, top_c[1] + 0.02, max(zt, zb) + 0.06]), apron_fn), "apron", bone="Pelvis")
        for sgn in (-1, 1):
            eadd(cone(np.array([sgn * 0.17, hipY + 0.14, zt - 0.03]), np.array([sgn * 0.12, hipY + 0.13, P("Pelvis")[2] - 0.15]), 0.005, 0.005), "apron_tie", bone="Pelvis")
    if top == "hoodie":
        # hood bunched behind the neck, drawstrings, kangaroo pocket
        backZ = V[(torso > 0.5) & (np.abs(y - (nk[1] - 0.06)) < 0.02) & (np.abs(V[:, 0]) < 0.05)][:, 2].min() - tthick
        # the hood, down: a soft folded pad of cloth lying over the upper back, its rim rolled at the neck
        eadd(ellipsoid(np.array([0, nk[1] - 0.06, backZ + 0.015]), [0.115, 0.085, 0.022]), "top", bone="Chest", k=0.03)
        eadd(ellipsoid(np.array([0, nk[1] - 0.005, backZ + 0.03]), [0.11, 0.025, 0.025]), "top", bone="Neck", k=0.025)
        fz = V[(torso > 0.5) & (np.abs(y - (hipY + 0.1)) < 0.04) & (np.abs(V[:, 0]) < 0.08)][:, 2].max() + tthick
        # (the kangaroo pocket is sewn flat onto the front: painted, not a slab stuck on)
        for sgn in (-1, 1):
            a = np.array([sgn * 0.035, nk[1] - 0.005, nk[2] + 0.07])
            cz = V[(torso > 0.5) & (np.abs(y - (nk[1] - 0.17)) < 0.03) & (np.abs(V[:, 0] - sgn * 0.04) < 0.02)][:, 2].max() + tthick + 0.006
            eadd(cone(a, np.array([sgn * 0.04, nk[1] - 0.17, cz]), 0.0035, 0.0035), "cord")
    if top in ("security", "workjacket", "jacket", "coat"):
        # a proper collar: a band of cloth standing round the neck, higher at the back, open at the front zip
        ring = V[(np.abs(y - (neckY + 0.02)) < 0.012) & (np.abs(V[:, 0]) < 0.1) & (headw < 0.5) & (np.abs(V[:, 2] - nk[2]) < 0.12)]
        ccx, ccz = float(np.median(ring[:, 0])), float((ring[:, 2].max() + ring[:, 2].min()) / 2)
        crx = float(np.percentile(np.abs(ring[:, 0] - ccx), 90)) + 0.012
        crz = float((ring[:, 2].max() - ring[:, 2].min()) / 2) + 0.012
        open_front = top in ("workjacket", "jacket", "coat")
        cy0 = neckY - 0.005
        def collar_fn(p, ccx=ccx, ccz=ccz, crx=crx, crz=crz, cy0=cy0, open_front=open_front):
            dx, dz = p[:, 0] - ccx, p[:, 2] - ccz
            r = np.sqrt((dx / crx) ** 2 + (dz / crz) ** 2)
            wall = np.abs((r - 1) * min(crx, crz)) - 0.0025
            back = smoothstep(0.3, -0.9, dz / crz)                       # 0 front .. 1 back
            h = (0.012 + 0.016 * back) * (1.0 if open_front else 0.8)    # a low band: 1-1.5 cm at the front, ~3 at the back
            # the band runs down into the garment's neckline, so there's no gap under it
            lo_y, hi_y = cy0 - 0.03, cy0 + h + 0.008 * back
            band = np.abs(p[:, 1] - (lo_y + hi_y) / 2) - (hi_y - lo_y) / 2
            d = np.maximum(wall, band)
            if open_front:
                d = np.maximum(d, np.where(dz > 0, 0.006 - np.abs(dx), -1))
            return d - 0.0015
        lo = np.array([ccx - crx - 0.02, cy0 - 0.02, ccz - crz - 0.02]); hi = np.array([ccx + crx + 0.02, cy0 + 0.08, ccz + crz + 0.02])
        eadd(Prim("collar", lo, hi, collar_fn), "top", bone="Neck")
    extra_part = None
    if em.prims:
        extra_part = sdf_part(em, np.array([-0.45, 0.3, -0.45]), np.array([0.45, 1.9, 0.45]), "extra", fine=0.003 if not QUICK else 0.004, target=8000, src_tree=tree)
        if extra_part is not None:
            _, lst = em.evaluate(extra_part.V, per_prim=True)
            best = np.full(len(extra_part.V), np.inf); tagv = np.array(["top"] * len(extra_part.V), dtype=object)
            for pr, (cand, dp) in zip(em.prims, lst):
                if len(cand) == 0: continue
                better = np.abs(dp) < best[cand]
                best[cand[better]] = np.abs(dp[better]); tagv[cand[better]] = pr.tag
            extra_part.vtag = tagv
            parts.append(extra_part)

    # ---------------- the body itself, minus the skin under clothes
    keepF = ~covered[F].all(1)
    bodyF = F[keepF]
    used_b = np.unique(bodyF)
    rb = -np.ones(len(V), int); rb[used_b] = np.arange(len(used_b))
    bodyPart = Part(V[used_b], rb[bodyF], "skin", src=used_b)
    parts.insert(0, bodyPart)
    lk = dict(front_up=0.085) if style == "receding" else dict(front_up=0.06) if style == "afro" else {}
    return dict(spec=spec, human=h, sk=sk, V=V, W=W, N=N, parts=parts, card=card_c, eyeY=eyeY, headw=headw, hipY=hipY, neckY=neckY, tree=tree,
                hairline_fn=(lambda q, lk=lk: hairline(q, **lk)) if style not in ("wrap", "bob") else None)


# ================================================================ textures
TAGS = ["skin", "top", "bottom", "undershirt", "shoe", "sole", "boot_sole", "hair", "beard", "band", "wrap", "beanie", "glasses",
        "strap", "card", "pack", "apron", "apron_tie", "cord"]


def colours(c, p, n, tagw, ao, head_frame):
    spec = c["spec"]; N = len(p)
    T = {t: tagw[:, i] for i, t in enumerate(TAGS)}
    x, y, z = p[:, 0], p[:, 1], p[:, 2]
    col = np.zeros((N, 3)); sm = np.zeros(N)
    def put(tag, rgb, smooth):
        nonlocal col, sm
        col += rgb * T[tag][:, None]; sm += smooth * T[tag]
    # skin: tone, flush, pores, brows painted on, stubble, darker lips
    eyeY, hj = c["eyeY"], c["sk"].pos[c["sk"].index["Head"]]
    sk = np.tile(spec["skin"], (N, 1)) * (0.94 + 0.08 * fbm(p, 30, 3, seed=51))[:, None]
    el, er = c["sk"].eye_l, c["sk"].eye_r
    face = smoothstep(hj[2] - 0.0, hj[2] + 0.06, z) * (y > c["neckY"])
    redness = np.array(spec["skin"]) * [1.05, 0.8, 0.8]
    nose = np.exp(-((np.hypot(x, y - (eyeY - 0.035))) / 0.018) ** 2) * face
    cheek = np.exp(-((np.abs(x) - 0.04) ** 2 + (y - (eyeY - 0.03)) ** 2) / 0.0005) * face
    sk = mix(sk, redness, np.clip(nose * 0.4 + cheek * 0.3, 0, 1))
    for e in (el, er):
        bx = x - e[0]
        out = bx * np.sign(e[0])                                   # + towards the temple
        arch = e[1] + 0.02 + 0.006 * np.exp(-((out - 0.004) / 0.012) ** 2) - 0.004 * smoothstep(0.008, 0.024, out)
        thick = 0.0055 * (1 - 0.6 * smoothstep(-0.012, 0.024, out))       # heavier at the inner end, thin at the tail
        shape = np.exp(-((y - arch) / np.maximum(thick, 1e-4)) ** 2) * smoothstep(0.026, 0.018, np.abs(out - 0.004)) * face
        hairs = 0.5 + 0.5 * np.sin((out * 0.6 + (y - arch) * 1.0) * 5200 + 6 * fbm(p, 300, 1, seed=55))
        hairs = smoothstep(0.35, 0.9, hairs) * (0.7 + 0.3 * fbm(p, 900, 1, seed=56))
        brow = shape * (0.35 + 0.65 * hairs)
        hairc = np.array(spec["hair"]["colour"]) * 0.85 if spec["hair"]["style"] != "receding" else np.array([0.45, 0.43, 0.41])
        sk = mix(sk, hairc, np.clip(brow, 0, 0.92))
        lid = np.exp(-((bx) ** 2 + (y - e[1]) ** 2) / 0.00035) * face
        sk = mix(sk, np.array(spec["skin"]) * 0.75, lid * 0.4)
    lips = np.exp(-((x / 2.6) ** 2 + (y - (eyeY - 0.075)) ** 2) / 0.00012) * face
    sk = mix(sk, np.array(spec["skin"]) * [0.95, 0.68, 0.68], lips * 0.6)
    if spec.get("beard") in ("short", "grey"):
        # the skin under a beard is shadowed with it, so its thinning edge blends instead of looking cut out
        under = smoothstep(eyeY - 0.035, eyeY - 0.055, y) * (y > c["neckY"] - 0.02) * face
        mouthc = smoothstep(0.012, 0.018, np.hypot(np.abs(x) / 2.2, y - (eyeY - 0.075)))
        bc = np.array(spec["hair"]["colour"]) if spec.get("beard") != "grey" else np.array([0.6, 0.59, 0.56])
        sk = mix(sk, bc * 0.8 + np.array(spec["skin"]) * 0.2, under * mouthc * (0.55 + 0.25 * fbm(p, 400, 1, seed=53)))
    if spec.get("beard") == "stubble":
        stub = smoothstep(eyeY - 0.05, eyeY - 0.07, y) * (y > c["neckY"] - 0.02) * face
        sk = mix(sk, np.array(spec["skin"]) * 0.62, stub * smoothstep(0.1, 0.5, fbm(p, 900, 1, seed=52) + 0.3) * 0.5)
    if spec["hair"]["style"] not in ("wrap",):
        d_line = c["hairline_fn"](p) - p[:, 1] if c.get("hairline_fn") else np.full(N, 1.0)
        # short fine hairs at the hairline: a soft graded shadow, not dots
        band = smoothstep(0.016, -0.002, d_line) * (y > c["neckY"]) * (0.75 + 0.25 * fbm(p, 120, 2, seed=57))
        sk = mix(sk, np.array(spec["hair"]["colour"]) * 0.7 + np.array(spec["skin"]) * 0.3, band * 0.32)
    put("skin", sk, 0.36)
    # the coloured garment: neutral, the material tints it
    top = spec["top"]
    knit = 0.92 + 0.08 * fbm(p, 60, 2, seed=21)
    grime = smoothstep(0.35, 0.65, fbm(p, 9, 3, seed=22)) * 0.4
    base_c = np.array(spec.get("top_colour", (0.25, 0.25, 0.25)))
    tcol = np.tile(base_c, (N, 1)) * knit[:, None]
    if top == "security":
        # epaulettes and a sewn-on badge
        tcol = mix(tcol, [0.55, 0.55, 0.58], np.exp(-((y - (c["neckY"] - 0.17)) / 0.02) ** 2) * smoothstep(0.06, 0.09, np.abs(x - 0.07)) * (z > 0) * 0)
        badge = (np.abs(x - 0.09) < 0.035) * (np.abs(y - (c["neckY"] - 0.2)) < 0.025) * (z > 0)
        tcol = mix(tcol, [0.75, 0.68, 0.35], badge * 0.8)
    if top == "tabard":
        tcol = np.tile(base_c, (N, 1)) * (0.9 + 0.1 * fbm(p, 40, 2, seed=23))[:, None]
    tsm = np.full(N, 0.14 if top not in ("jacket", "workjacket", "security") else 0.28)
    if top != "tabard":
        tcol, tsm = garment_details(c, top, p, n, tcol, tsm, base_c)
    tcol = mix(tcol, base_c * 0.7 + 0.08, grime)
    put("top", tcol, tsm)
    put("cord", np.tile([0.85, 0.85, 0.82], (N, 1)), 0.1)
    # bottoms
    bot = spec["bottom"]
    if bot == "jeans":
        tw = 0.5 + 0.5 * np.sin((y + (x + z) * 0.7) * 2600)
        b = mix(np.tile([0.13, 0.19, 0.32], (N, 1)), [0.32, 0.41, 0.55], tw * 0.3 + 0.1 * fbm(p, 80, 2, seed=31))
        fade = smoothstep(0.0, 0.08, z) * np.exp(-((y - (c["hipY"] - 0.25)) / 0.15) ** 2) * 0.5
        b = mix(b, [0.42, 0.5, 0.62], fade)
        bs = 0.17
    elif bot == "cargo":
        b = np.tile([0.26, 0.25, 0.2], (N, 1)) * (0.9 + 0.12 * fbm(p, 50, 2, seed=33))[:, None]; bs = 0.15
    elif bot == "joggers":
        b = np.tile([0.13, 0.13, 0.14], (N, 1)) * (0.9 + 0.1 * fbm(p, 70, 2, seed=34))[:, None]; bs = 0.12
    elif bot == "leggings":
        b = np.tile([0.07, 0.07, 0.08], (N, 1)) * (0.9 + 0.1 * fbm(p, 90, 2, seed=35))[:, None]; bs = 0.3
    else:
        b = np.tile([0.12, 0.12, 0.13], (N, 1)) * (0.92 + 0.08 * fbm(p, 60, 2, seed=36))[:, None]; bs = 0.2
    dirt = smoothstep(0.2, 0.55, fbm(p, 20, 3, seed=37) + 0.4) * np.exp(-((y - (c["hipY"] - 0.4)) / 0.07) ** 2) * smoothstep(0.0, 0.05, z)
    b = mix(b, [0.3, 0.27, 0.23], dirt * 0.22)
    put("bottom", b, bs)
    put("undershirt", np.tile([0.22, 0.24, 0.26], (N, 1)), 0.15)
    shoe = {"trainers": [0.12, 0.12, 0.13], "boots": [0.13, 0.09, 0.06], "work": [0.05, 0.05, 0.05], "flats": [0.06, 0.06, 0.07]}[spec["shoes"]]
    scuff = smoothstep(0.35, 0.55, fbm(p, 50, 2, seed=41))
    put("shoe", mix(np.tile(shoe, (N, 1)), [0.3, 0.27, 0.24], scuff * 0.5), 0.35 if spec["shoes"] != "trainers" else 0.25)
    put("sole", mix(np.tile([0.72, 0.7, 0.64], (N, 1)), [0.35, 0.32, 0.28], smoothstep(0.012, 0.0, y) * 0.8), 0.15)
    put("boot_sole", np.tile([0.06, 0.055, 0.05], (N, 1)), 0.2)
    hc = np.array(spec["hair"]["colour"])
    strands = 0.8 + 0.35 * (0.5 + 0.5 * np.sin(x * 2600 + fbm(p, 60, 2, seed=12) * 9)) * (0.6 + 0.4 * fbm(p, 400, 1, seed=13))
    put("hair", hc * strands[:, None], 0.35)
    beardc = hc if spec.get("beard") != "grey" else np.array([0.6, 0.59, 0.56])
    put("beard", beardc * strands[:, None], 0.3)
    put("band", np.tile([0.1, 0.1, 0.1], (N, 1)), 0.4)
    put("wrap", mix(np.tile([0.45, 0.2, 0.1], (N, 1)), [0.75, 0.55, 0.15], (np.sin(x * 120 + y * 90) > 0.3) * 0.7), 0.15)
    put("beanie", np.tile([0.3, 0.25, 0.13], (N, 1)) * (0.85 + 0.2 * np.abs(np.sin(np.arctan2(x, z) * 60)))[:, None], 0.1)
    put("glasses", np.tile([0.05, 0.04, 0.035], (N, 1)), 0.75)
    put("strap", np.tile([0.1, 0.12, 0.2], (N, 1)), 0.25)
    put("card", id_card(c, p), 0.5)
    put("pack", np.tile([0.14, 0.15, 0.13], (N, 1)) * (0.9 + 0.1 * fbm(p, 50, 2, seed=61))[:, None], 0.2)
    put("apron", np.tile([0.16, 0.12, 0.09], (N, 1)) * (0.9 + 0.1 * fbm(p, 40, 2, seed=62))[:, None], 0.25)
    put("apron_tie", np.tile([0.16, 0.12, 0.09], (N, 1)), 0.25)
    hairish = T["hair"] + T["beanie"] + T["wrap"]
    ao = np.maximum(ao, hairish * smoothstep(-0.2, -0.7, n[:, 1]) * 0.75)   # undersides of hair/hats: no black rim
    col *= (0.5 + 0.5 * ao)[:, None]
    return np.clip(col, 0, 1), np.clip(sm, 0, 1)


def id_card(c, p):
    """The staff pass on the lanyard: company stripe, photo of its owner, name and number lines, a barcode."""
    N = len(p)
    out = np.tile([0.86, 0.86, 0.83], (N, 1))
    cc = c.get("card")
    if cc is None: return out
    spec = c["spec"]
    u = (p[:, 0] - cc[0]) / 0.028; v = (p[:, 1] - cc[1]) / 0.04
    front = p[:, 2] > cc[2]
    stripe_c = {"marcus": [0.1, 0.16, 0.36], "priya": [0.62, 0.1, 0.12]}.get(spec["id"], [0.15, 0.3, 0.22])
    out = mix(out, stripe_c, (v > 0.55) * front)
    ph = (u > -0.8) & (u < -0.1) & (v > -0.35) & (v < 0.42) & front
    face = np.exp(-(((u + 0.45) / 0.22) ** 2 + ((v - 0.02) / 0.3) ** 2))
    hair = (v > 0.14 + 0.25 * ((u + 0.45) / 0.35) ** 2) & ph
    photo = mix(np.tile([0.55, 0.66, 0.75], (N, 1)), np.array(spec["skin"]), np.clip(face * 1.6, 0, 1))
    photo = mix(photo, np.array(spec["hair"]["colour"]), hair * 0.9)
    photo = mix(photo, np.array(spec.get("top_colour", (0.2, 0.2, 0.2))), (v < -0.22) * 1.0)
    out = np.where(ph[:, None], photo, out)
    for row, length in ((0.3, 0.75), (0.12, 0.55), (-0.06, 0.65), (-0.24, 0.4)):
        line = (np.abs(v - row) < 0.045) & (u > 0.0) & (u < 0.0 + length * 0.85) & front
        out = mix(out, [0.18, 0.18, 0.2], line * 0.9)
    bars = (v > -0.85) & (v < -0.55) & (np.abs(u) < 0.8) & front & (np.sin(u * 90 + np.sin(u * 37) * 3) > 0.1)
    out = mix(out, [0.08, 0.08, 0.08], bars * 0.95)
    return out


def wrist_radius(V, elbow, wrist, fore_t):
    """How thick the wrist is: the body's distance from the forearm axis just above the hand."""
    d = wrist - elbow; d = d / np.linalg.norm(d)
    side = np.sign(wrist[0])
    sel = (fore_t > 0.88) & (fore_t < 0.96) & (V[:, 0] * side > 0.1)
    if sel.sum() < 5: return 0.03
    rel = V[sel] - elbow
    radial = rel - np.outer(rel @ d, d)
    return float(np.percentile(np.linalg.norm(radial, axis=1), 80))


def garment_details(c, top, p, n, tcol, tsm, base_c):
    """Construction details painted onto the top: side seams, a stitched or ribbed hem, a zip (teeth and
    puller), welt pockets, chest pocket flaps, buttons. Front is +z; the wearer's centre line is x = 0."""
    x, y, z = p[:, 0], p[:, 1], p[:, 2]
    hipY, nkY = c["hipY"], c["neckY"]
    front = smoothstep(0.25, 0.6, n[:, 2])
    torso = (np.abs(x) < 0.2) * (y > hipY - 0.12) * (y < nkY)
    hem = hipY + {"hoodie": -0.03, "cardigan": -0.06, "security": 0.02, "workjacket": -0.02, "jacket": 0.0, "coat": -0.4}.get(top, 0.0)
    dark = base_c * 0.55
    # side seams (where the surface turns from front to back) and a shoulder seam line
    seam = np.exp(-(n[:, 2] / 0.05) ** 2) * (np.abs(n[:, 0]) > 0.7) * torso
    tcol = mix(tcol, dark, seam * 0.6)
    # hem: ribbed band on knits, a line of stitching on woven jackets
    above = y - hem
    if top in ("hoodie", "cardigan"):
        rib = smoothstep(0.06, 0.05, above) * (above > -0.02)
        ribs = 0.5 + 0.5 * np.sin(np.arctan2(x, z) * 160)
        tcol = mix(tcol, base_c * (0.8 + 0.25 * ribs[:, None]), rib * 0.8)
    else:
        stitch = np.exp(-((above - 0.022) / 0.0015) ** 2) * (0.5 + 0.5 * np.sign(np.sin(np.arctan2(x, z) * 500)))
        tcol = mix(tcol, dark * 0.8, stitch * 0.7)
    centre = np.abs(x)
    if top in ("jacket", "workjacket"):
        # zip: placket, teeth, puller near the top
        plk = smoothstep(0.016, 0.012, centre) * front * (above > 0) * (y < nkY)
        tcol = mix(tcol, dark, plk * 0.35)
        teeth = smoothstep(0.0045, 0.003, centre) * front * (above > 0) * (y < nkY + 0.01)
        tooth = 0.5 + 0.5 * np.sign(np.sin(y * 2 * np.pi / 0.0045))
        metal = np.array([0.42, 0.42, 0.44]) if top == "jacket" else np.array([0.1, 0.1, 0.1])
        tcol = mix(tcol, metal * (0.7 + 0.3 * tooth[:, None]), teeth)
        tsm = tsm + teeth * 0.4
        pull = (np.abs(x - 0.002) < 0.006) * (np.abs(y - (nkY - 0.07)) < 0.014) * front
        tcol = mix(tcol, metal * 1.1, pull)
        tsm = tsm + pull * 0.45
        # welt pockets: slanted slits with a stitched lip
        ax = np.abs(x)
        slit_y = hipY + 0.085 + (ax - 0.07) * 0.55
        inpk = (ax > 0.07) * (ax < 0.15) * front
        slit = np.exp(-((y - slit_y) / 0.0022) ** 2) * inpk
        lip = np.exp(-((y - slit_y - 0.008) / 0.004) ** 2) * inpk
        tcol = mix(tcol, base_c * 0.25, slit)
        tcol = mix(tcol, dark, lip * 0.35)
    if top in ("workjacket", "security"):
        # chest pockets with flaps and a button each
        for sgn in (-1, 1):
            cx, cy = sgn * 0.09, nkY - 0.16
            inside = (np.abs(x - cx) < 0.045) * (np.abs(y - cy) < 0.05) * front
            edge = inside * (1 - (np.abs(x - cx) < 0.04) * (np.abs(y - cy) < 0.045))
            flap = (np.abs(x - cx) < 0.047) * (np.abs(y - (cy + 0.035)) < 0.017) * front
            flapedge = flap * (1 - (np.abs(x - cx) < 0.043) * (np.abs(y - (cy + 0.035)) < 0.013))
            tcol = mix(tcol, dark, edge * 0.6 + flapedge * 0.7)
            tcol = mix(tcol, base_c * 1.08, flap * 0.15)
            btn = (np.hypot(x - cx, y - (cy + 0.028)) < 0.006) * front
            tcol = mix(tcol, [0.08, 0.08, 0.08], btn)
            tsm = tsm + btn * 0.3
    if top in ("cardigan", "security"):
        # button front: a placket and buttons every 7-8 cm
        plk = smoothstep(0.014, 0.01, centre) * front * (above > 0) * (y < nkY - 0.05)
        tcol = mix(tcol, dark, plk * 0.25 + np.exp(-((centre - 0.013) / 0.0015) ** 2) * front * (above > 0) * (y < nkY - 0.05) * 0.4)
        bc = np.array([0.12, 0.09, 0.07]) if top == "cardigan" else np.array([0.08, 0.08, 0.1])
        for k in range(7):
            by = nkY - 0.1 - k * 0.075
            if by < hem + 0.04: break
            b = (np.hypot(x, y - by) < 0.0065) * front
            ring = (np.abs(np.hypot(x, y - by) - 0.0045) < 0.0009) * front
            tcol = mix(tcol, bc, b)
            tcol = mix(tcol, bc * 0.5, ring)
            tsm = tsm + b * 0.35
    if top == "hoodie":
        # kangaroo pocket outline and the hood's seam line at the neck
        ax = np.abs(x)
        pocket = (ax < 0.13) * (np.abs(y - (hipY + 0.1)) < 0.075) * front
        edge = pocket * (1 - (ax < 0.125) * (np.abs(y - (hipY + 0.1)) < 0.07))
        opening = np.exp(-((ax - (0.11 - (y - (hipY + 0.1)) * 0.3)) / 0.003) ** 2) * pocket
        tcol = mix(tcol, dark, edge * 0.5 + opening * 0.6)
    return np.clip(tcol, 0, 1), np.clip(tsm, 0, 1)


def heights(p, tagw):
    T = {t: tagw[:, i] for i, t in enumerate(TAGS)}
    knit = 0.00012 * np.abs(np.sin(p[:, 0] * 2200 + p[:, 2] * 2200) * np.sin(p[:, 1] * 2600)) + 0.00008 * fbm(p, 500, 2, seed=5)
    twill = 0.0001 * np.sin((p[:, 1] + (p[:, 0] + p[:, 2]) * 0.7) * 2600) + 0.00006 * fbm(p, 400, 2, seed=8)
    pores = 0.00005 * fbm(p, 900, 2, seed=11)
    strands = 0.0003 * np.sin(p[:, 0] * 900 + fbm(p, 40, 2, seed=12) * 6)
    leather = 0.00008 * fbm(p, 600, 2, seed=13)
    return (knit * (T["top"] + T["undershirt"] + T["beanie"] + T["cord"]) + twill * (T["bottom"] + T["apron"] + T["pack"] + T["strap"])
            + pores * T["skin"] + strands * (T["hair"] + T["beard"] + T["wrap"]) + leather * (T["shoe"] + T["sole"] + T["boot_sole"]))


def detail_normal(p, n, tagw, eps=0.0003):
    g = np.zeros_like(p)
    for i in range(3):
        e = np.zeros(3); e[i] = eps
        g[:, i] = (heights(p + e, tagw) - heights(p - e, tagw)) / (2 * eps)
    g -= n * np.sum(g * n, 1, keepdims=True)
    o = n - g
    return o / np.linalg.norm(o, axis=1, keepdims=True)


def eye_texture(iris, size=256):
    yy, xx = (np.mgrid[0:size, 0:size] + 0.5) / size
    r = np.hypot(xx - 0.5, yy - 0.5) * 2
    a = np.arctan2(yy - 0.5, xx - 0.5)
    Pq = np.stack([np.cos(a) * 3, np.sin(a) * 3, r * 6], -1).reshape(-1, 3)
    fib = (0.5 + 0.5 * fbm(Pq * [1, 1, 0.2], 3, 2, seed=61)).reshape(size, size)
    col = np.zeros((size, size, 3)); col[:] = [0.8, 0.76, 0.72]
    # sclera: greyer and a little bloodshot towards the corners (it's 3 am and they're terrified)
    veins = smoothstep(0.55, 0.8, fbm(Pq * [2, 2, 0.5], 6, 2, seed=63).reshape(size, size)) * smoothstep(0.6, 0.95, r)
    col = col * (1 - 0.18 * smoothstep(0.6, 1.0, r))[..., None]
    col = col * (1 - veins[..., None] * 0.25) + veins[..., None] * np.array([0.55, 0.18, 0.15]) * 0.25
    ir = np.array(iris) * (0.6 + 0.7 * fib)[..., None]
    IR, PU = 0.56, 0.21                                      # iris ~ half the visible eye, as real ones are
    col = np.where((r < IR)[..., None], ir, col)
    limbal = smoothstep(IR - 0.07, IR, r) * (r < IR)
    col = col * (1 - 0.5 * limbal)[..., None]
    col = np.where((r < PU)[..., None], np.array([0.02, 0.02, 0.02]), col)
    return col


# ================================================================ export
def per_vertex_ao(V, F, cell=0.012):
    """Soft occlusion from a 1.2 cm occupancy grid of the surfaces: how much 'stuff' is near, over the hemisphere."""
    lo = V.min(0) - 0.1
    dims = np.ceil((V.max(0) + 0.1 - lo) / cell).astype(int) + 1
    occ = np.zeros(dims, np.float32)
    rng = np.random.default_rng(4)
    a, b, c = V[F[:, 0]], V[F[:, 1]], V[F[:, 2]]
    for _ in range(6):                                    # scatter points over every triangle
        r1, r2 = rng.random((len(F), 1)), rng.random((len(F), 1))
        s = np.sqrt(r1)
        pts = a * (1 - s) + b * (s * (1 - r2)) + c * (s * r2)
        ij = np.floor((pts - lo) / cell).astype(int)
        occ[ij[:, 0], ij[:, 1], ij[:, 2]] = 1
    from scipy.ndimage import uniform_filter
    occ = uniform_filter(occ, 3)
    n = vertex_normals(V, F)
    dirs = rng.normal(size=(16, 3)); dirs /= np.linalg.norm(dirs, axis=1, keepdims=True)
    tot = np.zeros(len(V)); cnt = np.zeros(len(V))
    for d in dirs:
        dd = np.tile(d, (len(V), 1)); fl = np.sum(dd * n, 1) < 0; dd[fl] *= -1
        for dist, wgt in ((0.03, 1.0), (0.06, 0.7), (0.12, 0.45), (0.22, 0.25)):
            q = V + n * 0.012 + dd * dist
            ij = np.clip(np.floor((q - lo) / cell).astype(int), 0, dims - 1)
            tot += occ[ij[:, 0], ij[:, 1], ij[:, 2]] * wgt
            cnt += wgt
    return np.clip(1 - 1.6 * tot / cnt, 0.15, 1)


def export(c, size):
    spec = c["spec"]; sk = c["sk"]; W = c["W"]
    Vs, Fs, Ws, Tg, Vt = [], [], [], [], []
    o = 0
    for part in c["parts"]:
        Vs.append(part.V); Fs.append(part.F + o)
        if part.rigid:
            w = np.zeros((len(part.V), len(sk.names))); w[:, sk.index[part.rigid]] = 1
        else:
            w = W[part.src]
        Ws.append(w)
        if hasattr(part, "vtag"):
            Vt.append(part.vtag)
        else:
            Vt.append(np.array([part.tag] * len(part.V), dtype=object))
        o += len(part.V)
    V = np.concatenate(Vs); F = np.concatenate(Fs); Wv = np.concatenate(Ws); vt = np.concatenate(Vt)
    tagw = np.zeros((len(V), len(TAGS)))
    for i, t in enumerate(TAGS):
        tagw[vt == t, i] = 1
    N = vertex_normals(V, F)
    log(f"  {spec['id']}: {len(V)} verts, {len(F)} tris; ambient occlusion")
    ao_v = per_vertex_ao(V, F)
    atlas = xatlas.Atlas()
    atlas.add_mesh(V.astype(np.float32), F.astype(np.uint32))
    co = xatlas.ChartOptions(); co.max_iterations = 1
    po = xatlas.PackOptions(); po.resolution = size; po.padding = 5; po.bilinear = True
    atlas.generate(co, po)
    vmap, F2, UV = atlas[0]
    F2 = F2.astype(np.int64)
    V2, N2, W2, tg2, ao2 = V[vmap], N[vmap], Wv[vmap], tagw[vmap], ao_v[vmap]
    T2 = tangents(V2, N2, UV, F2)
    tid, bary = rasterize(UV, F2, size)
    mask = tid >= 0
    p = interp(V2, F2, tid, bary, mask)
    nl = interp(N2, F2, tid, bary, mask); nl /= np.linalg.norm(nl, axis=1, keepdims=True)
    tg = interp(T2[:, :3], F2, tid, bary, mask); tg -= nl * np.sum(tg * nl, 1, keepdims=True); tg /= np.maximum(np.linalg.norm(tg, axis=1, keepdims=True), 1e-9)
    bt = np.cross(nl, tg) * T2[F2[tid[mask], 0], 3][:, None]
    tw = interp(tg2, F2, tid, bary, mask)
    ao = interp(ao2[:, None], F2, tid, bary, mask)[:, 0]
    nh = detail_normal(p, nl, tw)
    ts = np.stack([np.sum(nh * tg, 1), np.sum(nh * bt, 1), np.maximum(np.sum(nh * nl, 1), 0.05)], 1)
    ts /= np.linalg.norm(ts, axis=1, keepdims=True)
    col, smv = colours(c, p, nh, tw, ao, None)
    img = lambda vals, ch: _img(vals, ch, size, mask)
    flip = lambda a: a[::-1]
    cimg = dilate(img(col, 3), mask); nimg = dilate(img(ts * 0.5 + 0.5, 3), mask)
    mimg = np.zeros((size, size, 4)); mimg[..., 1] = dilate(img(ao, 1), mask)[..., 0]; mimg[..., 3] = dilate(img(smv, 1), mask)[..., 0]
    d = os.path.join(OUT, spec["id"]); os.makedirs(d, exist_ok=True)
    save_rgb(flip(cimg), os.path.join(d, f"player_{spec['id']}_A.jpg"), 92)
    save_rgb(flip(nimg), os.path.join(d, f"player_{spec['id']}_N.jpg"), 92)
    Image.fromarray((flip(mimg) * 255 + 0.5).astype(np.uint8), "RGBA").resize((min(size, 1024),) * 2, Image.LANCZOS).save(os.path.join(d, f"player_{spec['id']}_M.png"), optimize=True)
    eye = eye_texture(spec["eyes"])
    save_rgb(eye, os.path.join(d, f"player_{spec['id']}_eye_A.png"))
    # submeshes: 0 coloured garment, 1 the rest of the body, 2 head (hidden in first person), 3 eyes
    ft = tg2[F2].mean(1)
    is_top = ft[:, TAGS.index("top")] > 0.5
    headish = (W2[F2][:, :, [sk.index["Head"], sk.index["Jaw"]]].sum(2).mean(1) > 0.5) & ~is_top
    for t in ("hair", "beard", "wrap", "beanie", "glasses", "band"):
        headish |= ft[:, TAGS.index(t)] > 0.5
    subs_tris = [F2[is_top], F2[~is_top & ~headish], F2[headish & ~is_top]]
    # eyes
    EV, EUV, EF = [], [], []
    for e in (sk.eye_l, sk.eye_r):
        dd, _ = c["tree"].query(e)
        r = max(0.009, min(0.0125, dd * 0.92))
        nu, nv = 20, 14
        vs, uvs = [], []
        for j in range(nv + 1):
            th = np.pi * j / nv
            for i in range(nu + 1):
                ph = 2 * np.pi * i / nu
                dv = np.array([np.sin(th) * np.cos(ph), np.cos(th), np.sin(th) * np.sin(ph)])
                vs.append(e + dv * r); uvs.append([0.5 + dv[0] * 0.5, 0.5 + dv[1] * 0.5])
        tris = []
        for j in range(nv):
            for i in range(nu):
                a = j * (nu + 1) + i
                tris += [[a, a + 1, a + nu + 1], [a + 1, a + nu + 2, a + nu + 1]]
        vs = np.array(vs); tris = np.array(tris)
        fn = np.cross(vs[tris[:, 1]] - vs[tris[:, 0]], vs[tris[:, 2]] - vs[tris[:, 0]])
        fl = np.sum(fn * (vs[tris].mean(1) - e), 1) < 0
        tris[fl] = tris[fl][:, [0, 2, 1]]
        EF.append(tris + sum(len(x) for x in EV)); EV.append(vs); EUV.append(np.array(uvs))
    EV = np.concatenate(EV); EUV = np.concatenate(EUV); EF = np.concatenate(EF)
    EN = vertex_normals(EV, EF); ET = tangents(EV, EN, EUV, EF)
    o2 = len(V2)
    AV = np.concatenate([V2, EV]); AN = np.concatenate([N2, EN]); AT = np.concatenate([T2, ET]); AUV = np.concatenate([UV, EUV])
    EW = np.zeros((len(EV), len(sk.names))); EW[:, sk.index["Head"]] = 1
    AW = np.concatenate([W2, EW])
    order = np.argsort(-AW, axis=1)[:, :4]
    bw = np.take_along_axis(AW, order, 1); bw /= np.maximum(bw.sum(1, keepdims=True), 1e-9)
    subs = [("Player_Clothes", subs_tris[0].ravel()), ("Player_Body", subs_tris[1].ravel()), ("Player_Head", subs_tris[2].ravel()), ("Player_Eyes", (EF + o2).ravel())]
    path = os.path.join(d, f"player_{spec['id']}_model.bytes")
    with open(path, "wb") as f:
        f.write(b"ELM1"); f.write(struct.pack("<i", len(sk.names)))
        for i, nm in enumerate(sk.names):
            b = nm.encode(); f.write(struct.pack("<i", len(b))); f.write(b)
            f.write(struct.pack("<i", sk.index[sk.parents[i]] if sk.parents[i] else -1))
            f.write(struct.pack("<3f", *sk.pos[i])); f.write(struct.pack("<4f", *mat_to_quat(sk.rot[i])))
        f.write(struct.pack("<i", len(AV)))
        for a in (AV, AN, AT, AUV):
            f.write(a.astype("<f4").tobytes())
        f.write(order.astype("<u2").tobytes()); f.write(bw.astype("<f4").tobytes())
        f.write(struct.pack("<i", len(subs)))
        for name, idx in subs:
            b = name.encode(); f.write(struct.pack("<i", len(b))); f.write(b)
            f.write(struct.pack("<i", idx.size)); f.write(idx.astype("<u4").tobytes())
    log(f"  {spec['id']}: wrote {len(AV)} verts")
    if VIEWER:
        vd = os.path.join(VIEWER, spec["id"]); os.makedirs(vd, exist_ok=True)
        open(os.path.join(vd, "model.txt"), "w").write(base64.b64encode(gzip.compress(open(path, "rb").read(), 9)).decode())
        save_rgb(flip(cimg), os.path.join(vd, "A.jpg"), 86)
        save_rgb(flip(nimg), os.path.join(vd, "N.jpg"), 90)
        r = np.zeros((size, size, 3)); r[..., 1] = 1 - mimg[..., 3]
        save_rgb(flip(r), os.path.join(vd, "R.jpg"), 88)
        save_rgb(eye, os.path.join(vd, "eye.jpg"), 90)


def main():
    ids = [a for a in sys.argv[1:] if not a.startswith("--")]
    specs = [s for s in CHARACTERS if not ids or s["id"] in ids]
    size = 1024 if QUICK else 2048
    os.makedirs(OUT, exist_ok=True)
    for spec in specs:
        log(f"building {spec['name']}")
        c = build(spec)
        export(c, size)
    roster = [{k: s[k] for k in ("id", "name")} for s in CHARACTERS]
    json.dump(roster, open(os.path.join(OUT, "roster.json"), "w"), indent=1)
    if VIEWER:
        json.dump(roster, open(os.path.join(VIEWER, "roster.json"), "w"), indent=1)
    log("done")


if __name__ == "__main__":
    main()
