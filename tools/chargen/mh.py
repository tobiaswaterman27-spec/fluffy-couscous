"""MakeHuman (CC0) base body + shape targets -> a specific person, in Unity space (metres, +X right, +Y up, +Z forward).

Data comes from the MakeHuman 1.x repository (released CC0 in 2020): base.obj, the shape targets, and the default
skeleton with its skin weights. fetch.py downloads them into ./mhdata."""
import os, json, glob
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "mhdata")


def load_obj(path):
    V, VT, faces, groups = [], [], [], []
    g = None
    for line in open(path):
        if line.startswith("v "):
            V.append([float(x) for x in line.split()[1:4]])
        elif line.startswith("vt "):
            VT.append([float(x) for x in line.split()[1:3]])
        elif line.startswith("g "):
            g = line.split()[1]
        elif line.startswith("f "):
            idx = [p.split("/") for p in line.split()[1:]]
            faces.append(([int(a[0]) - 1 for a in idx], [int(a[1]) - 1 if len(a) > 1 and a[1] else -1 for a in idx]))
            groups.append(g)
    return np.array(V), np.array(VT), faces, groups


_targets = {}


def target(name):
    if name not in _targets:
        p = os.path.join(DATA, "targets", name + ".target")
        if not os.path.exists(p):
            _targets[name] = None
        else:
            rows = [l.split() for l in open(p) if l.strip() and not l.startswith("#")]
            a = np.array(rows, dtype=float).reshape(-1, 4) if rows else np.zeros((0, 4))
            _targets[name] = (a[:, 0].astype(int), a[:, 1:4]) if len(a) else None
    return _targets[name]


def tri(v):
    """MakeHuman 0..1 slider (0.5 = average) -> weights for the min / average / max targets."""
    if v < 0.5:
        return {"min": 1 - v * 2, "average": v * 2, "max": 0.0}
    return {"min": 0.0, "average": 2 - v * 2, "max": v * 2 - 1}


STANDARD_HEIGHT = 1.76   # metres, top of the head (None: keep MakeHuman's own height)


class Human:
    """gender 0 female .. 1 male; age in years (18-80); muscle/weight/height 0..1 (0.5 average);
    race = (african, asian, caucasian) proportions; details = {"nose/nose-scale-horiz-incr": 0.4, ...}"""

    def __init__(self, gender=1.0, age=30, muscle=0.5, weight=0.5, height=0.5, race=(0, 0, 1), details=None, breast=0.5):
        self.base_V, self.VT, self.faces, self.groups = load_obj(os.path.join(DATA, "base.obj"))
        V = self.base_V.copy()
        gw = {"female": 1 - gender, "male": gender}
        t = np.clip((age - 25) / 65.0, 0, 1) if age >= 25 else 0.0
        aw = {"young": 1 - t, "old": t}
        mw = {k + "muscle": w for k, w in tri(muscle).items()}
        ww = {k + "weight": w for k, w in tri(weight).items()}
        hw = {"minheight": max(0, 1 - height * 2), "maxheight": max(0, height * 2 - 1)}
        def add(name, w):
            nonlocal V
            if w < 1e-4:
                return
            tg = target(name)
            if tg is None:
                return
            V[tg[0]] += tg[1] * w
        for g, a in gw.items():
            for ag, b in aw.items():
                for m, c in mw.items():
                    for wt, d in ww.items():
                        w = a * b * c * d
                        add(f"macrodetails/universal-{g}-{ag}-{m}-{wt}", w)
                        for hh, e in hw.items():
                            add(f"macrodetails/height/{g}-{ag}-{m}-{wt}-{hh}", w * e)
                        if g == "female":
                            for cup, cw in {"mincup": max(0, 1 - breast * 2), "maxcup": max(0, breast * 2 - 1)}.items():
                                add(f"breast/female-{ag}-{m}-{wt}-{cup}-averagefirmness", w * cw)
                for rname, r in zip(("african", "asian", "caucasian"), race):
                    add(f"macrodetails/{rname}-{g}-{ag}", a * b * r)
        for name, w in (details or {}).items():
            if w >= 0:
                add(name, w)
        # MakeHuman: decimetres, +X = the body's left. Unity: metres, +X = the body's right.
        V = V * 0.1
        V[:, 0] *= -1
        body_idx = sorted({v for i, g in enumerate(self.groups) if g == "body" for v in self.faces[i][0]})
        V[:, 1] -= V[body_idx, 1].min()          # feet on the ground
        # every playable character stands the same height (the game's player capsule is one size); build,
        # proportions and shape still differ
        if STANDARD_HEIGHT:
            V *= STANDARD_HEIGHT / V[body_idx, 1].max()
        self.V = V
        self.body_faces = [i for i, g in enumerate(self.groups) if g == "body"]

    def group_centroid(self, name):
        idx = sorted({v for i, g in enumerate(self.groups) if g == name for v in self.faces[i][0]})
        return self.V[idx].mean(0) if idx else None

    def joint(self, mh_name):
        """Joint position from the base mesh's joint helper cubes (moves with every target)."""
        return self.group_centroid("joint-" + mh_name)

    def body_mesh(self):
        """Triangles of the body only (no helpers), with MakeHuman's UVs; winding flipped for the mirror."""
        tris, uvt = [], []
        for i in self.body_faces:
            v, t = self.faces[i]
            for k in range(1, len(v) - 1):
                tris.append([v[0], v[k + 1], v[k]])
                uvt.append([t[0], t[k + 1], t[k]])
        tris = np.array(tris)
        used = np.unique(tris)
        remap = -np.ones(len(self.V), int); remap[used] = np.arange(len(used))
        return self.V[used], remap[tris], used


# ---------------------------------------------------------------- skeleton: MakeHuman's 163 bones -> the game's skeleton
def my_bone(mh):
    """Which game bone a MakeHuman bone's skin weights belong to."""
    side = "L" if mh.endswith(".L") else "R" if mh.endswith(".R") else ""
    base = mh.split(".")[0]
    if base in ("root", "spine05", "pelvis"): return "Pelvis"
    if base in ("spine04", "spine03"): return "Spine"
    if base in ("spine02", "spine01", "breast", "clavicle"): return "Chest"
    if base.startswith("neck"): return "Neck"
    if base in ("jaw", "oris03", "oris05", "oris07") or base.startswith("tongue"): return "Jaw"
    if base in ("shoulder01", "upperarm01", "upperarm02"): return "UpperArm" + side
    if base.startswith("lowerarm"): return "ForeArm" + side
    if base in ("wrist",) or base.startswith("metacarpal"): return "Hand" + side
    if base.startswith("finger1-"):
        return "Thumb" + side + ("_1" if base in ("finger1-1", "finger1-2") else "_2")
    if base.startswith("finger"):
        n, k = base[6:].split("-")
        return f"Finger{side}{int(n) - 2}_{k}"
    if base.startswith("upperleg"): return "Thigh" + side
    if base.startswith("lowerleg"): return "Shin" + side
    if base == "foot": return "Foot" + side
    if base.startswith("toe"): return "Toes" + side
    return "Head"


def look_rot(fwd, up):
    z = fwd / np.linalg.norm(fwd)
    x = np.cross(up, z); x /= np.linalg.norm(x)
    y = np.cross(z, x)
    return np.stack([x, y, z], 1)


def perp(v, d):
    v = v - d * np.dot(v, d)
    return v / np.linalg.norm(v)


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


class Skeleton:
    """The game's joints placed on this person (rest pose), plus per-vertex weights merged from MakeHuman's."""

    def __init__(self, human):
        h = human
        sk = json.load(open(os.path.join(DATA, "default.mhskel")))
        jv = sk["joints"]
        def J(name):
            return h.V[jv[name]].mean(0)
        def head(b): return J(b + "____head")
        def tail(b): return J(b + "____tail")
        self.names, self.parents, self.pos, self.rot = [], [], [], []
        self.index = {}
        def add(name, parent, p, R=None):
            self.index[name] = len(self.names)
            self.names.append(name); self.parents.append(parent); self.pos.append(np.asarray(p, float)); self.rot.append(np.eye(3) if R is None else R)
        Z, Y = np.array([0, 0, 1.0]), np.array([0, 1.0, 0])
        hipL, hipR = head("upperleg01.L"), head("upperleg01.R")
        pelvis = (hipL + hipR) / 2 + Y * 0.03
        neck = head("neck01")
        headj = head("head")
        add("Pelvis", None, pelvis)
        add("Spine", None, pelvis, look_rot(perp(Z, (neck - pelvis) / np.linalg.norm(neck - pelvis)), neck - pelvis))
        add("Chest", "Spine", head("spine02"), self.rot[1])
        add("Neck", None, neck, look_rot(perp(Z, (headj - neck) / np.linalg.norm(headj - neck)), headj - neck))
        add("Head", None, headj)
        add("Jaw", "Head", head("jaw"))
        self.eyes = (J("special05.L____head") if False else None)
        for s in ("L", "R"):
            sh, el, wr = head(f"upperarm01.{s}"), head(f"lowerarm01.{s}"), head(f"wrist.{s}")
            mid = head(f"finger3-1.{s}")
            idx, pinky = head(f"finger2-1.{s}"), head(f"finger5-1.{s}")
            back = np.array([0, 0, -1.0])
            for nm, a, b in ((f"UpperArm{s}", sh, el), (f"ForeArm{s}", el, wr)):
                d = (b - a) / np.linalg.norm(b - a)
                add(nm, None, a, look_rot(perp(back, d), -d))
            dh = (mid - wr) / np.linalg.norm(mid - wr)
            palm = np.cross(idx - pinky, dh); palm /= np.linalg.norm(palm)
            if palm[1] > 0: palm = -palm                      # palms face down in the bind pose
            self.palm = palm
            add(f"Hand{s}", None, wr, look_rot(perp(palm, dh), -dh))
            for f in range(4):
                parent = f"Hand{s}"
                for k in range(1, 4):
                    a = head(f"finger{f + 2}-{k}.{s}"); b = tail(f"finger{f + 2}-{k}.{s}")
                    d = (b - a) / np.linalg.norm(b - a)
                    nm = f"Finger{s}{f}_{k}"
                    add(nm, parent, a, look_rot(perp(palm, d), -d)); parent = nm
            parent = f"Hand{s}"
            for k, mhb in ((1, "finger1-2"), (2, "finger1-3")):
                a = head(f"{mhb}.{s}"); b = tail(f"{mhb}.{s}")
                d = (b - a) / np.linalg.norm(b - a)
                nm = f"Thumb{s}_{k}"
                add(nm, parent, a, look_rot(perp(palm, d), -d)); parent = nm
            hip, knee, ankle = head(f"upperleg01.{s}"), head(f"lowerleg01.{s}"), head(f"foot.{s}")
            for nm, a, b in ((f"Thigh{s}", hip, knee), (f"Shin{s}", knee, ankle)):
                d = (b - a) / np.linalg.norm(b - a)
                add(nm, None, a, look_rot(perp(Z, d), -d))
            add(f"Foot{s}", None, ankle)
            add(f"Toes{s}", f"Foot{s}", np.mean([head(f"toe{t}-1.{s}") for t in range(1, 6)], 0))
        self.eye_l, self.eye_r = h.joint("l-eye"), h.joint("r-eye")
        add("EyeL", "Head", self.eye_l); add("EyeR", "Head", self.eye_r)     # not weighted: they tell the game where the eyes are
        # upper eyelids: pivot on the eyeball's centre, so rotating them sweeps the lid down over the eye (blinking)
        add("LidL", "Head", self.eye_l); add("LidR", "Head", self.eye_r)
        # weights: MakeHuman's, summed into the game's bones
        mw = json.load(open(os.path.join(DATA, "default_weights.mhw")))["weights"]
        n = len(h.V)
        W = np.zeros((n, len(self.names)))
        for mhb, pairs in mw.items():
            b = self.index[my_bone(mhb)]
            a = np.array(pairs)
            W[a[:, 0].astype(int), b] += a[:, 1]
        W /= np.maximum(W.sum(1, keepdims=True), 1e-9)
        # the upper lid skin (just above and in front of each eyeball) moves with its lid bone
        V = h.V
        hb = self.index["Head"]
        for nm, e in (("LidL", self.eye_l), ("LidR", self.eye_r)):
            rel = V - e
            d = np.linalg.norm(rel, axis=1)
            dy, dz = rel[:, 1], rel[:, 2]
            g = (smooth(0.0225, 0.012, d) * smooth(-0.003, 0.003, dy) * (1 - smooth(0.011, 0.017, dy)) * smooth(-0.004, 0.004, dz))
            mv = np.clip(g, 0, 1) * W[:, hb]
            W[:, hb] -= mv
            W[:, self.index[nm]] += mv
        self.W = W
