"""
Real human motion for the player characters, from the CMU Graphics Lab Motion Capture Database
(mocap.cs.cmu.edu - free for any use; BVH conversion by B. Hahne / cgspeed, via github.com/una-dinosauria/cmu-mocap).

Each clip is reduced to what the game's rig needs, independent of anyone's proportions:
  - the pelvis: offset from the ground point under the body (in leg lengths) and its orientation;
  - every body segment (spine, neck, upper/fore arm, hand, thigh, shin, foot): its direction plus a second
    vector that carries its twist (both in the character's own space, facing +Z);
  - the head's orientation, the shoulders' shrug, and whether each foot is planted.
At runtime these are rebuilt with the character's own bone lengths (so nobody's limbs stretch), the feet are
pinned to the real floor and the stride is warped to the actual speed.

Locomotion clips are cut to one clean gait cycle (left heel strike to left heel strike), straightened, made to
loop exactly, and resampled to 64 phases. Idle clips are cut to a long stretch and cross-faded into a loop.

Run:  python3 build_mocap.py [--bvh DIR]      (downloads the BVH files it needs if missing)
Out:  Assets/AfterHours/Resources/Player/mocap.bytes
"""
import os, sys, struct, urllib.request
import numpy as np
from bvh import load, fk, to_unity

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Assets", "AfterHours", "Resources", "Player", "mocap.bytes")
SRC = "https://raw.githubusercontent.com/una-dinosauria/cmu-mocap/master/data/{s:03d}/{c}.bvh"

# name, CMU clip, kind, (start, end seconds for idles), cycle pick: which stride (0..1 through the clip)
CLIPS = [
    ("walk",        "08_01",  "cycle", None, 0.5),    # brisk walk
    ("walk_slow",   "16_15",  "cycle", None, 0.5),    # unhurried walk
    ("jog",         "02_03",  "cycle", None, 0.5),
    ("run",         "16_45",  "cycle", None, 0.5),
    ("crouch",      "77_32",  "cycle", None, 0.3),    # creeping walk, low
    ("crouch_back", "136_11", "cycle", None, 0.5),    # walking backwards crouched
    ("idle",        "137_28", "idle",  (3.0, 21.0), 0.1),    # standing, waiting (kept almost still)
    ("idle_scared", "142_16", "idle",  (4.0, 22.0), 0.18),   # scared, standing
    ("crawl",       "111_03", "cycle", None, 0.43),    # on hands and knees
]

SEGS = [  # name, from joint, to joint, twist source joint, twist axis (T-pose, char space)
    ("spine",  "Hips",          "Neck",            "Spine1",       (0, 0, 1)),
    ("neck",   "Neck",          "Head",            "Neck1",        (0, 0, 1)),
    ("upperL", "LeftArm",       "LeftForeArm",     "LeftArm",      (0, 0, 1)),
    ("foreL",  "LeftForeArm",   "LeftHand",        "LeftForeArm",  (0, 0, 1)),
    ("handL",  "LeftHand",      "LeftHandIndex1",  "LeftHand",     (0, -1, 0)),
    ("upperR", "RightArm",      "RightForeArm",    "RightArm",     (0, 0, 1)),
    ("foreR",  "RightForeArm",  "RightHand",       "RightForeArm", (0, 0, 1)),
    ("handR",  "RightHand",     "RightHandIndex1", "RightHand",    (0, -1, 0)),
    ("thighL", "LeftUpLeg",     "LeftLeg",         "LeftUpLeg",    (0, 0, 1)),
    ("shinL",  "LeftLeg",       "LeftFoot",        "LeftLeg",      (0, 0, 1)),
    ("footL",  "LeftFoot",      "LeftToeBase",     "LeftFoot",     (0, 1, 0)),
    ("thighR", "RightUpLeg",    "RightLeg",        "RightUpLeg",   (0, 0, 1)),
    ("shinR",  "RightLeg",      "RightFoot",       "RightLeg",     (0, 0, 1)),
    ("footR",  "RightFoot",     "RightToeBase",    "RightFoot",    (0, 1, 0)),
]
# per sample: pelvis off (3) + pelvis quat (4) + head quat (4) + chest quat (4) + foot quats L/R (4+4) + 14 segs * 6
#             + shoulder shrug L/R (3+3) + ankle L/R relative to the ground point (3+3) + contact L/R (2) + wrist L/R (3+3)
CH = 3 + 4 + 4 + 4 + 8 + len(SEGS) * 6 + 6 + 6 + 2 + 6
CON = CH - 8      # contact L/R
WR = CH - 6       # wrists L/R relative to the ground point (hands on the floor when crawling)


def fetch(c, d):
    p = os.path.join(d, c + ".bvh")
    if os.path.exists(p) and os.path.getsize(p) > 1000: return p
    s = int(c.split("_")[0])
    print("  downloading", c)
    urllib.request.urlretrieve(SRC.format(s=s, c=c), p)
    return p


def quat(m):
    """Rotation matrices (...,3,3) -> quaternions (...,4) as x,y,z,w (Unity order)."""
    m = np.asarray(m)
    q = np.zeros(m.shape[:-2] + (4,))
    t = m[..., 0, 0] + m[..., 1, 1] + m[..., 2, 2]
    for idx in np.ndindex(m.shape[:-2]):
        a = m[idx]; tr = t[idx]
        if tr > 0:
            s = np.sqrt(tr + 1.0) * 2
            w = 0.25 * s; x = (a[2, 1] - a[1, 2]) / s; y = (a[0, 2] - a[2, 0]) / s; z = (a[1, 0] - a[0, 1]) / s
        elif a[0, 0] > a[1, 1] and a[0, 0] > a[2, 2]:
            s = np.sqrt(1.0 + a[0, 0] - a[1, 1] - a[2, 2]) * 2
            w = (a[2, 1] - a[1, 2]) / s; x = 0.25 * s; y = (a[0, 1] + a[1, 0]) / s; z = (a[0, 2] + a[2, 0]) / s
        elif a[1, 1] > a[2, 2]:
            s = np.sqrt(1.0 + a[1, 1] - a[0, 0] - a[2, 2]) * 2
            w = (a[0, 2] - a[2, 0]) / s; x = (a[0, 1] + a[1, 0]) / s; y = 0.25 * s; z = (a[1, 2] + a[2, 1]) / s
        else:
            s = np.sqrt(1.0 + a[2, 2] - a[0, 0] - a[1, 1]) * 2
            w = (a[1, 0] - a[0, 1]) / s; x = (a[0, 2] + a[2, 0]) / s; y = (a[1, 2] + a[2, 1]) / s; z = 0.25 * s
        q[idx] = (x, y, z, w)
    # Unity's matrices act on column vectors in a left-handed frame; the formula above is the same algebra.
    # keep a consistent hemisphere through time (no flips for interpolation)
    if q.ndim == 2:
        for i in range(1, len(q)):
            if np.dot(q[i], q[i - 1]) < 0: q[i] = -q[i]
    return q


def roty(a):
    c, s = np.cos(a), np.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def smooth(x, k):
    if k <= 1: return x
    ker = np.ones(k) / k
    pad = k // 2
    xp = np.concatenate([np.repeat(x[:1], pad, 0), x, np.repeat(x[-1:], pad, 0)], 0)
    out = np.empty_like(x)
    for c in range(x.shape[1]):
        out[:, c] = np.convolve(xp[:, c], ker, mode="valid")[: len(x)]
    return out


def strikes(foot, ft):
    """Heel strikes of one foot: its horizontal speed falls from swing to near zero."""
    v = np.linalg.norm(np.diff(foot[:, [0, 2]], axis=0), axis=1) / ft
    v = np.convolve(v, np.ones(7) / 7, mode="same")
    hi, lo = np.percentile(v, 85), np.percentile(v, 15)
    th = lo + 0.25 * (hi - lo)
    out, swing = [], False
    for i in range(1, len(v)):
        if v[i] > lo + 0.6 * (hi - lo): swing = True
        if swing and v[i] < th:
            out.append(i); swing = False
    return out


def features(pos, rot, idx, L, fwd_yaw, ground_xz, floor):
    """One frame set -> channel array (F, CH). rot/pos already Unity space. Heading removed by fwd_yaw (rad)."""
    F = pos.shape[0]
    Ry = roty(-fwd_yaw)              # rotate the world so the travel / facing direction is +Z
    P = np.einsum("ij,fkj->fki", Ry, pos)
    R = np.einsum("ij,fkjl->fkil", Ry, rot)
    g = np.einsum("ij,fj->fi", Ry, np.stack([ground_xz[:, 0], np.zeros(F), ground_xz[:, 1]], 1))
    T = rot0  # T-pose rotations (J,3,3), facing +Z already
    out = np.zeros((F, CH))
    c = 0
    h = idx["Hips"]
    hm = (P[:, idx["LeftUpLeg"]] + P[:, idx["RightUpLeg"]]) * 0.5     # the hip JOINTS (CMU's Hips root sits ~10 cm above them)
    off = hm - g; off[:, 1] = hm[:, 1] - floor
    out[:, c:c + 3] = off / L; c += 3
    def delta(j):  # char-space rotation of joint j relative to its T-pose
        return np.einsum("fij,kj->fik", R[:, j], T[j])     # R * T^-1 (T orthonormal: T^-1 = T^T)
    out[:, c:c + 4] = quat(delta(h)); c += 4
    out[:, c:c + 4] = quat(delta(idx["Head"])); c += 4
    out[:, c:c + 4] = quat(delta(idx["Spine1"])); c += 4
    out[:, c:c + 4] = quat(delta(idx["LeftFoot"])); c += 4
    out[:, c:c + 4] = quat(delta(idx["RightFoot"])); c += 4
    for name, a, b, tw, ax in SEGS:
        d = P[:, idx[b]] - P[:, idx[a]]
        d /= np.linalg.norm(d, axis=1, keepdims=True)
        t = np.einsum("fij,j->fi", delta(idx[tw]), np.array(ax, float))
        out[:, c:c + 3] = d; out[:, c + 3:c + 6] = t; c += 6
    # shoulders: where the arm joint sits relative to the chest frame, minus where it sits in the T-pose
    ch_j = idx["Spine1"]
    for sh in ("LeftArm", "RightArm"):
        rel = np.einsum("fji,fj->fi", R[:, ch_j], P[:, idx[sh]] - P[:, idx["Neck"]])
        relT = T[ch_j].T @ (pos0[idx[sh]] - pos0[idx["Neck"]])
        out[:, c:c + 3] = np.einsum("fij,fj->fi", R[:, ch_j], rel - relT) / L; c += 3   # back in character space
    for ft in ("LeftFoot", "RightFoot"):
        a = P[:, idx[ft]] - g; a[:, 1] = P[:, idx[ft], 1] - floor
        out[:, c:c + 3] = a / L; c += 3
    for hd in ("LeftHand", "RightHand"):
        a = P[:, idx[hd]] - g; a[:, 1] = P[:, idx[hd], 1] - floor
        out[:, WR + (0 if hd[0] == "L" else 3):WR + (3 if hd[0] == "L" else 6)] = a / L
    return out


def build_cycle(name, path, pick):
    global rot0, pos0
    J, data, ft = load(path)
    pos, rot, idx = fk(J, data)
    pos, rot = to_unity(pos, rot)
    pos0, rot0 = pos[0], rot[0]                # frame 0 is the added T-pose (faces +Z)
    pos, rot = pos[1:], rot[1:]
    L = np.linalg.norm(pos0[idx["LeftUpLeg"]] - pos0[idx["LeftLeg"]]) + np.linalg.norm(pos0[idx["LeftLeg"]] - pos0[idx["LeftFoot"]])
    lf, rf = idx["LeftFoot"], idx["RightFoot"]
    floor = np.percentile(np.minimum(pos[:, lf, 1], pos[:, rf, 1]), 3)
    if name == "crawl":   # on all fours the hands step cleanly; the feet drag
        sl, sr = strikes(pos[:, idx["LeftHand"]], ft), strikes(pos[:, idx["RightHand"]], ft)
    else:
        sl, sr = strikes(pos[:, lf], ft), strikes(pos[:, rf], ft)
    if len(sl) < 2: raise SystemExit(f"{name}: not enough strides ({len(sl)})")
    k = int(np.clip(round(pick * (len(sl) - 2)), 0, len(sl) - 2))
    a, b = sl[k], sl[k + 1]
    mid = [s for s in sr if a < s < b]
    rphase = (mid[0] - a) / (b - a) if mid else 0.5
    seg = np.arange(a, b + 1)
    hp = pos[seg, idx["Hips"]]
    travel = hp[-1] - hp[0]
    yaw = np.arctan2(travel[0], travel[2])
    if name == "crawl":   # barely moving per cycle: face where the spine points (hips to neck)
        sd = (pos[seg, idx["Neck"]] - pos[seg, idx["Hips"]]).mean(axis=0)
        yaw = np.arctan2(sd[0], sd[2])
    backwards = name.endswith("_back")
    if backwards: yaw += np.pi                       # face the other way from the travel
    n = len(seg)
    tt = np.linspace(0, 1, n)
    ground = hp[0, [0, 2]] + np.outer(tt, travel[[0, 2]])
    f = features(pos[seg], rot[seg], idx, L, yaw, ground, floor)
    # contacts from foot speed (relative to the moving ground point = stance)
    for s, fj in enumerate((lf, rf)):
        v = np.linalg.norm(np.gradient(pos[seg, fj][:, [0, 2]], axis=0), axis=1) / ft / L
        f[:, CON + s] = np.clip(1.0 - (v - 0.15) / 0.5, 0, 1)
    # make it loop: spread the end-to-start mismatch over the cycle
    err = f[-1] - f[0]
    f = f - np.outer(tt, err)
    f = f[:-1]
    # resample to 64 phases
    N = 64
    src = np.linspace(0, 1, len(f), endpoint=False)
    dst = np.linspace(0, 1, N, endpoint=False)
    # time-warp so the right heel strikes exactly half way (the game's footsteps alternate evenly)
    r = 0.5 if name == "crawl" else float(np.clip(rphase, 0.25, 0.75))   # crawling keeps its own limb order
    tw = np.where(dst < 0.5, dst / 0.5 * r, r + (dst - 0.5) / 0.5 * (1 - r))
    res = np.stack([np.interp(tw, src, f[:, c], period=1.0) for c in range(CH)], 1)
    dur = (b - a) * ft
    speed = np.linalg.norm(travel[[0, 2]]) / dur / L
    step = np.linalg.norm(travel[[0, 2]]) / 2 / L
    print(f"  {name:12s} {path[-10:]}: cycle {dur:.2f}s  speed {speed:.2f} leg/s  step {step:.2f} leg  "
          f"cadence {2 / dur:.2f} steps/s  right strike at {rphase:.2f}")
    return dict(name=name, kind=0, length=1.0 / dur, speed=speed * (-1 if backwards else 1), step=step, rphase=rphase, data=res)


def build_idle(name, path, span, still=0.12):
    global rot0, pos0
    J, data, ft = load(path)
    pos, rot, idx = fk(J, data)
    pos, rot = to_unity(pos, rot)
    pos0, rot0 = pos[0], rot[0]
    pos, rot = pos[1:], rot[1:]
    L = np.linalg.norm(pos0[idx["LeftUpLeg"]] - pos0[idx["LeftLeg"]]) + np.linalg.norm(pos0[idx["LeftLeg"]] - pos0[idx["LeftFoot"]])
    a, b = int(span[0] / ft), min(len(pos) - 1, int(span[1] / ft))
    step = max(1, int(round((1 / 30) / ft)))         # 30 fps
    seg = np.arange(a, b, step)
    lf, rf = idx["LeftFoot"], idx["RightFoot"]
    floor = np.percentile(np.minimum(pos[:, lf, 1], pos[:, rf, 1]), 3)
    feet = (pos[seg, lf] + pos[seg, rf]) * 0.5
    ground = smooth(feet[:, [0, 2]], 61)
    fz = np.einsum("fij,j->fi", rot[seg, idx["Hips"]] @ np.transpose(rot0[idx["Hips"]]), np.array([0, 0, 1.0]))
    yaw = np.arctan2(fz[:, 0].mean(), fz[:, 2].mean())
    f = features(pos[seg], rot[seg], idx, L, yaw, ground, floor)
    f[:, CON:CON + 2] = 1.0
    # keep them still: the average stance plus only a little of the captured sway (breathing, small shifts)
    mean = f.mean(axis=0)
    f = mean + still * (f - mean)
    f[:, 113:119] = mean[113:119]           # feet stay exactly planted
    f = smooth(f, 9)
    # loop: cross-fade the last second into the first
    xf = 30
    w = np.linspace(0, 1, xf)[:, None]
    f[-xf:] = f[-xf:] * (1 - w) + f[:xf] * w
    f = f[:-1]
    dur = len(f) / 30.0
    print(f"  {name:12s} {path[-10:]}: {dur:.1f}s loop")
    return dict(name=name, kind=1, length=1.0 / dur, speed=0.0, step=0.0, rphase=0.5, data=f)


def write(clips):
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "wb") as fo:
        fo.write(b"MOC1")
        fo.write(struct.pack("<ii", len(clips), CH))
        names = [s[0] for s in SEGS]
        fo.write(struct.pack("<i", len(names)))
        for n in names:
            e = n.encode(); fo.write(struct.pack("<i", len(e))); fo.write(e)
        for c in clips:
            e = c["name"].encode(); fo.write(struct.pack("<i", len(e))); fo.write(e)
            d = c["data"].astype(np.float32)
            fo.write(struct.pack("<iffffi", c["kind"], c["length"], c["speed"], c["step"], c["rphase"], d.shape[0]))
            fo.write(d.tobytes())
    print("wrote", OUT, os.path.getsize(OUT) // 1024, "KB")


if __name__ == "__main__":
    d = sys.argv[sys.argv.index("--bvh") + 1] if "--bvh" in sys.argv else os.path.join(HERE, "bvh")
    os.makedirs(d, exist_ok=True)
    out = []
    for name, c, kind, span, pick in CLIPS:
        p = fetch(c, d)
        out.append(build_cycle(name, p, pick) if kind == "cycle" else build_idle(name, p, span, pick))
    write(out)
