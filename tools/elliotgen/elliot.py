"""Elliot, sculpted as a signed-distance model in its bind pose (Unity space: +X right, +Y up, +Z forward, metres).

The skeleton matches ElliotAnimator: pelvis/spine/neck/head grow along +Y, limbs hang along each bone's local -Y,
with each limb bone's local +Z pointing at its IK pole (knees forward, elbows back). Extra 'detail' bones (chest,
throat, lips, nostrils, brows, tongue, finger joints, toes) are children of those joints."""
import numpy as np
from sdf import *

H0 = np.array([0.0, 2.22, 0.0])          # head joint (top of the neck)
ARM_ANGLE = np.radians(24)               # bind pose: arms out from the body (A-pose)
UPPER, FORE = 0.56, 0.56
HIP_W, SH_W = 0.12, 0.21


def look_rot(fwd, up):
    """Unity's Quaternion.LookRotation as a matrix (columns = local X, Y, Z in world)."""
    z = nrm(fwd)
    x = nrm(np.cross(up, z))
    y = np.cross(z, x)
    return np.stack([x, y, z], 1)


def mat_to_quat(m):
    t = np.trace(m)
    if t > 0:
        s = np.sqrt(t + 1) * 2
        w, x, y, z = 0.25 * s, (m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = np.sqrt(1 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        w, x, y, z = (m[2, 1] - m[1, 2]) / s, 0.25 * s, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s
    elif m[1, 1] > m[2, 2]:
        s = np.sqrt(1 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        w, x, y, z = (m[0, 2] - m[2, 0]) / s, (m[0, 1] + m[1, 0]) / s, 0.25 * s, (m[1, 2] + m[2, 1]) / s
    else:
        s = np.sqrt(1 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
        w, x, y, z = (m[1, 0] - m[0, 1]) / s, (m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, 0.25 * s
    return np.array([x, y, z, w])


class Bone:
    def __init__(self, name, parent, pos, rot=None):
        self.name, self.parent = name, parent
        self.pos = np.asarray(pos, float)
        self.rot = np.eye(3) if rot is None else rot


class Elliot:
    def __init__(self):
        self.m = Model()
        self.bones = []
        self.index = {}
        self.arm = {}
        self.fingers = {}
        self.build()

    def bone(self, name, parent, pos, rot=None):
        self.index[name] = len(self.bones)
        self.bones.append(Bone(name, parent, pos, rot))
        return name

    def add(self, prim, bone, tag="skin", op="add", k=0.0):
        prim.bone, prim.tag, prim.op, prim.k = bone, tag, op, k
        return self.m.add(prim)

    # ------------------------------------------------------------------ helpers
    def armpt(self, s, t, u=0.0, v=0.0):
        """A point in the arm's frame: t along the arm from the shoulder, u towards the back, v outwards."""
        a = self.arm[s]
        return a["S"] + a["d"] * t + a["b"] * u + a["n"] * v

    def H(self, x, y, z):
        return H0 + np.array([x, y, z])

    def build(self):
        m = self
        # ---------------------------------------------------------------- skeleton
        m.bone("Pelvis", None, [0, 1.40, 0])
        m.bone("Spine", None, [0, 1.40, 0])
        m.bone("Chest", "Spine", [0, 1.80, 0.0])
        m.bone("Neck", None, [0, 2.02, 0])
        m.bone("Throat", "Neck", [0, 2.12, 0.035])
        m.bone("Head", None, H0)
        m.bone("Jaw", "Head", m.H(0, 0.125, 0.005))
        m.bone("Tongue", "Jaw", m.H(0, 0.06, 0.008))
        m.bone("LipUpper", "Head", m.H(0, 0.084, 0.1))
        m.bone("NostrilL", "Head", m.H(-0.0085, 0.127, 0.114))
        m.bone("NostrilR", "Head", m.H(0.0085, 0.127, 0.114))
        m.bone("BrowL", "Head", m.H(-0.032, 0.203, 0.088))
        m.bone("BrowR", "Head", m.H(0.032, 0.203, 0.088))
        for s, side in ((-1, "L"), (1, "R")):
            S = np.array([s * SH_W, 1.98, 0.0])
            d = np.array([s * np.sin(ARM_ANGLE), -np.cos(ARM_ANGLE), 0.0])
            b = np.array([0.0, 0.0, -1.0])
            n = np.array([s * np.cos(ARM_ANGLE), np.sin(ARM_ANGLE), 0.0])
            m.arm[s] = dict(S=S, d=d, b=b, n=n)
            R = look_rot(b, -d)  # limb bones: local -Y along the bone, local +Z at the elbow pole (behind)
            m.bone("UpperArm" + side, None, S, R)
            m.bone("ForeArm" + side, None, S + d * UPPER, R)
            m.bone("Hand" + side, None, S + d * (UPPER + FORE), R)
            hip = np.array([s * HIP_W, 1.40, 0.0])
            m.bone("Thigh" + side, None, hip)
            m.bone("Shin" + side, None, hip + [0, -0.68, 0])
            m.bone("Foot" + side, None, hip + [0, -1.34, 0])
            m.bone("Toes" + side, "Foot" + side, [s * HIP_W, 0.02, 0.165])

        # ---------------------------------------------------------------- torso
        A = m.add
        A(ellipsoid([0, 1.385, -0.01], [0.15, 0.1, 0.1]), "Pelvis", k=0)
        for s in (-1, 1):
            A(ellipsoid([s * 0.06, 1.335, -0.055], [0.062, 0.075, 0.05]), "Pelvis", k=0.03)          # flat, wasted buttocks
            A(ellipsoid([s * 0.104, 1.47, 0.032], [0.022, 0.02, 0.036]), "Pelvis", k=0.03)           # hip bones jut out
        A(cone([0, 1.44, 0.0], [0, 1.69, -0.005], 0.104, 0.112), "Spine", k=0.05)
        A(ellipsoid([0, 1.57, 0.112], [0.075, 0.09, 0.035]), "Spine", op="sub", k=0.03)           # sunken belly
        A(sphere([0, 1.505, 0.094], 0.006), "Spine", op="sub", k=0.004)                          # navel
        rib = A(ellipsoid([0, 1.80, 0.0], [0.142, 0.19, 0.106]), "Chest", k=0.05)

        def ribs(p):
            y, x, z = p[:, 1], p[:, 0], p[:, 2]
            phi = np.arctan2(np.abs(x), z)                        # 0 at the front, pi at the back
            s = (y - 1.62 + 0.075 * np.cos(phi) - 0.03 * (phi / np.pi)) / 0.041
            ridge = np.maximum(0, np.cos(2 * np.pi * s)) ** 3
            mask = smoothstep(1.6, 1.66, y) * (1 - smoothstep(1.9, 1.97, y)) * smoothstep(0.012, 0.035, np.abs(x)) * (1 - smoothstep(2.3, 2.8, phi) * 0.7)
            return 0.0042 * ridge * mask
        m.m.disp.append((rib, ribs))
        A(ellipsoid([0, 1.935, -0.012], [0.165, 0.09, 0.094]), "Chest", k=0.04)                  # upper chest / shoulders
        for s in (-1, 1):
            A(cone([s * 0.015, 1.988, 0.072], [s * 0.168, 2.0, 0.018], 0.011, 0.013), "Chest", k=0.012)   # collarbones
            A(ellipsoid([s * 0.098, 1.86, -0.088], [0.064, 0.075, 0.022]), "Spine", k=0.025)            # shoulder blades
            A(cone([s * 0.02, 2.06, -0.035], [s * 0.18, 1.99, -0.02], 0.028, 0.022), "Chest", k=0.04)     # trapezius
        for i in range(19):
            y = 1.42 + i * 0.034
            zb = -0.093 - 0.012 * np.sin(np.clip((y - 1.6) / 0.4, 0, 1) * np.pi)
            A(sphere([0, y, zb], 0.0135), "Spine" if y > 1.47 else "Pelvis", k=0.007)                   # spine knobs

        # ---------------------------------------------------------------- neck
        A(cone([0, 1.97, -0.015], m.H(0, 0.075, -0.022), 0.05, 0.04), "Neck", k=0.035)
        for s in (-1, 1):
            A(cone(m.H(s * 0.048, 0.115, -0.028), [s * 0.022, 2.0, 0.045], 0.011, 0.012), "Neck", k=0.016)   # tendons
        A(ellipsoid([0, 2.115, 0.031], [0.012, 0.018, 0.012]), "Throat", k=0.012)                            # larynx

        # ---------------------------------------------------------------- head (skull, long face)
        A(ellipsoid(m.H(0, 0.215, -0.02), [0.085, 0.12, 0.11]), "Head", k=0.03)
        A(ellipsoid(m.H(0, 0.145, 0.035), [0.072, 0.098, 0.08]), "Head", k=0.025)
        A(ellipsoid(m.H(0, 0.105, 0.07), [0.054, 0.034, 0.044]), "Head", k=0.02)        # upper jaw / muzzle
        A(ellipsoid(m.H(0, 0.06, 0.05), [0.049, 0.042, 0.052]), "Jaw", k=0.03)           # lower jaw
        A(ellipsoid(m.H(0, 0.033, 0.07), [0.025, 0.02, 0.03]), "Jaw", k=0.02)            # chin
        A(ellipsoid(m.H(0, 0.201, 0.03), [0.071, 0.017, 0.077]), "Head", k=0.02)  # heavy brow, following the skull
        for s in (-1, 1):
            A(cone(m.H(s * 0.056, 0.115, -0.022), m.H(s * 0.035, 0.034, 0.058), 0.01, 0.009), "Jaw", k=0.012)  # jaw line
            A(ellipsoid(m.H(s * 0.056, 0.156, 0.064), [0.02, 0.011, 0.024]), "Head", k=0.02)                # cheekbones
            A(ellipsoid(m.H(s * 0.062, 0.108, 0.068), [0.022, 0.03, 0.03]), "Head", op="sub", k=0.016)      # hollow cheeks
            A(ellipsoid(m.H(s * 0.081, 0.215, 0.03), [0.015, 0.03, 0.03]), "Head", op="sub", k=0.015)       # sunken temples
            ey = 0.184 if s < 0 else 0.177                                                                   # eyes not level
            A(ellipsoid(m.H(s * 0.034, ey, 0.099), [0.024, 0.019, 0.028]), "Head", tag="socket", op="sub", k=0.008)
            A(ellipsoid(m.H(s * 0.081, 0.17, -0.015), [0.008, 0.026, 0.016]), "Head", tag="ear", k=0.006)   # small, flat ears
            A(sphere(m.H(s * 0.088, 0.166, -0.012), 0.0055), "Head", tag="ear", op="sub", k=0.003)
        A(cone(m.H(0, 0.188, 0.093), m.H(0, 0.143, 0.111), 0.0055, 0.009), "Head", k=0.012)   # flat nasal ridge
        A(ellipsoid(m.H(0, 0.132, 0.113), [0.016, 0.014, 0.011]), "Head", k=0.01)
        for s in (-1, 1):
            A(ellipsoid(m.H(s * 0.0085, 0.126, 0.121), [0.0048, 0.0032, 0.011]), "Nostril" + ("L" if s < 0 else "R"), tag="nostril", op="sub", k=0.002)
        # the mouth: far too wide, lipless, a slit that runs back into the cheeks, a dark wet cavity behind it
        A(ellipsoid(m.H(0, 0.075, 0.079), [0.059, 0.0036, 0.052]), "Head", tag="lip", op="sub", k=0.003)
        A(ellipsoid(m.H(0, 0.073, 0.046), [0.045, 0.021, 0.046]), "Head", tag="mouth", op="sub", k=0.006)
        A(cone(m.H(0, 0.068, 0.005), m.H(0, 0.03, -0.02), 0.016, 0.012), "Head", tag="mouth", op="sub", k=0.006)

        # ---------------------------------------------------------------- arms and hands
        for s, side in ((-1, "L"), (1, "R")):
            P = lambda t, u=0.0, v=0.0, s=s: m.armpt(s, t, u, v)
            ua, fa, hd = "UpperArm" + side, "ForeArm" + side, "Hand" + side
            A(ellipsoid(P(0.03, 0.0, 0.006), [0.05, 0.058, 0.048], R=frame(m.arm[s]["d"], [0, 0, 1])), ua, k=0.035)  # shoulder
            A(cone(P(0.03), P(0.54), 0.04, 0.031), ua, k=0.02)
            A(cone(P(0.1, 0.012), P(0.38, 0.008), 0.027, 0.019), ua, k=0.02)        # triceps (what's left of it)
            A(cone(P(0.12, -0.01), P(0.4, -0.008), 0.024, 0.018), ua, k=0.02)       # biceps
            A(sphere(P(0.565, 0.016), 0.025), fa, k=0.014)                          # pointed elbow
            for v in (-0.021, 0.021):
                A(sphere(P(0.55, 0.0, v), 0.013), ua, k=0.012)
            A(cone(P(0.58), P(1.09), 0.033, 0.021), fa, k=0.02)
            A(cone(P(0.6, -0.004, 0.012), P(0.86, 0.0, 0.007), 0.025, 0.017), fa, k=0.02)
            A(sphere(P(1.1, 0.008, -0.017), 0.011), fa, k=0.008)                    # wrist knobs
            A(sphere(P(1.1, 0.006, 0.017), 0.01), fa, k=0.008)
            # hand: palm faces back (+b), fingers curl that way. Thumb on the inner side (-n).
            Rh = frame(m.arm[s]["d"], m.arm[s]["b"])
            Rp = np.stack([m.arm[s]["n"], m.arm[s]["d"], m.arm[s]["b"]], 1)
            A(ellipsoid(P(1.168, 0.002, 0.0), [0.036, 0.062, 0.0125], R=Rp), hd, k=0.012)                # narrow palm
            for v in (-0.0285, -0.0095, 0.0095, 0.0285):                                                # bones of the hand
                A(cone(P(1.125, -0.004, v * 0.55), P(1.222, -0.006, v), 0.0062, 0.0075), hd, k=0.008)
            A(ellipsoid(P(1.17, 0.012, -0.025), [0.012, 0.03, 0.01], R=frame(m.arm[s]["d"], m.arm[s]["b"])), hd, k=0.01)  # thumb pad
            spreads = np.radians([-7, -2.5, 2.5, 7.5])
            vs = [-0.0285, -0.0095, 0.0095, 0.0285]
            lens = [(0.07, 0.05, 0.04), (0.078, 0.056, 0.044), (0.074, 0.053, 0.042), (0.06, 0.043, 0.035)]
            curls = np.radians([7, 11, 9])
            fingers = []
            for f in range(4):
                dirv = m.arm[s]["d"] * np.cos(spreads[f]) + m.arm[s]["n"] * np.sin(spreads[f])
                pos = P(1.225, 0.0, vs[f])
                parent = hd
                r0 = 0.0086 - 0.0007 * (f == 3)
                joints = []
                for j in range(3):
                    ang = curls[j]
                    dirv = nrm(dirv * np.cos(ang) + m.arm[s]["b"] * np.sin(ang))
                    end = pos + dirv * lens[f][j]
                    name = f"Finger{side}{f}_{j + 1}"
                    m.bone(name, parent, pos, look_rot(nrm(m.arm[s]["b"] - dirv * np.dot(m.arm[s]["b"], dirv)), -dirv))
                    ra, rb = r0 * (1 - 0.13 * j), r0 * (1 - 0.13 * (j + 1))
                    A(cone(pos, end, ra, rb), name, k=0.004)
                    A(sphere(pos - m.arm[s]["b"] * 0.0025, ra * 1.18), name if j else hd, k=0.004)   # knuckles
                    if j == 2:
                        nb = pos + dirv * lens[f][j] * 0.45 - m.arm[s]["b"] * (rb * 0.55)
                        tip = end + dirv * 0.024 + m.arm[s]["b"] * 0.008
                        A(cone(nb, tip, rb * 0.75, 0.0012), name, tag="nail", k=0.003)                 # long dark claws
                    joints.append(name)
                    parent = name
                    pos = end
                fingers.append(joints)
            # thumb
            tdir = nrm(m.arm[s]["d"] * 0.75 - m.arm[s]["n"] * 0.55 + m.arm[s]["b"] * 0.35)
            pos = P(1.135, 0.008, -0.034)
            parent = hd
            for j, L in enumerate((0.048, 0.037)):
                tdir = nrm(tdir + m.arm[s]["b"] * 0.15)
                end = pos + tdir * L
                name = f"Thumb{side}_{j + 1}"
                m.bone(name, parent, pos, look_rot(nrm(m.arm[s]["b"] - tdir * np.dot(m.arm[s]["b"], tdir)), -tdir))
                A(cone(pos, end, 0.0098 - 0.0015 * j, 0.0083 - 0.0015 * j), name, k=0.006)
                if j == 1:
                    A(cone(pos + tdir * L * 0.4 - m.arm[s]["b"] * 0.004, end + tdir * 0.018 + m.arm[s]["b"] * 0.006, 0.0055, 0.0012), name, tag="nail", k=0.003)
                fingers.append([name]) if j == 1 else None
                parent = name
                pos = end
            m.fingers[s] = fingers

        # ---------------------------------------------------------------- legs and feet
        for s, side in ((-1, "L"), (1, "R")):
            x = s * HIP_W
            th, sh, ft, to = "Thigh" + side, "Shin" + side, "Foot" + side, "Toes" + side
            A(cone([x, 1.37, -0.005], [x, 0.76, 0.008], 0.066, 0.043), th, k=0.04)
            A(cone([x + s * 0.006, 1.22, 0.028], [x, 0.8, 0.026], 0.038, 0.028), th, k=0.03)
            A(cone([x + s * 0.024, 1.26, 0.0], [x + s * 0.012, 0.84, 0.004], 0.03, 0.019), th, k=0.03)   # outer thigh
            for o in (-0.02, 0.02):
                A(cone([x + o, 0.9, -0.028], [x + o * 1.1, 0.7, -0.03], 0.0095, 0.008), th, k=0.012)       # hamstring tendons
            A(sphere([x, 0.72, 0.008], 0.04), sh, k=0.025)
            A(ellipsoid([x, 0.725, 0.048], [0.024, 0.028, 0.014]), sh, k=0.012)       # kneecap
            A(cone([x, 0.70, 0.0], [x, 0.1, -0.004], 0.039, 0.026), sh, k=0.02)
            A(cone([x, 0.66, 0.028], [x, 0.15, 0.022], 0.012, 0.01), sh, k=0.015)      # shin bone ridge
            A(ellipsoid([x, 0.52, -0.024], [0.031, 0.09, 0.03]), sh, k=0.03)          # wasted calf
            for o in (-0.022, 0.022):
                A(sphere([x + o, 0.074, 0.004], 0.0135), sh, k=0.01)                   # ankle knobs
            A(ellipsoid([x, 0.036, -0.035], [0.029, 0.035, 0.035]), ft, k=0.02)       # heel
            A(rbox([x, 0.03, 0.07], [0.033, 0.021, 0.095], np.eye(3), 0.016), ft, k=0.025)
            A(cone([x, 0.06, -0.01], [x, 0.04, 0.13], 0.024, 0.014), ft, k=0.025)     # top of the foot
            for t, o in enumerate((-0.024, -0.008, 0.008, 0.024)):
                o *= s
                a, b = np.array([x + o, 0.02, 0.155]), np.array([x + o * 1.25, 0.012, 0.245 - 0.012 * abs(t - 1.5)])
                A(cone(a, b, 0.0105, 0.0078), to, k=0.006)
                A(cone(b - [0, -0.004, 0.012], b + [0, -0.004, 0.014], 0.0058, 0.0012), to, tag="nail", k=0.003)

    # ------------------------------------------------------------------ mouth parts (separate meshes)
    def teeth(self, rng):
        """Far too many thin, uneven, pointed, stained teeth - some missing, a few crowding in a second row behind."""
        out = []
        for row, bone in (("upper", "Head"), ("lower", "Jaw")):
            n = 27
            for i in range(n):
                if rng.random() < 0.12:
                    continue                                          # gaps where teeth are missing
                a = (i / (n - 1) - 0.5) * 2.5 + rng.uniform(-0.03, 0.03)
                for inner in (0, 1):
                    if inner and rng.random() > 0.35:
                        continue
                    rr = 0.05 - inner * 0.006
                    x = np.sin(a) * rr
                    z = 0.046 + np.cos(a) * rr - inner * 0.002
                    L = rng.uniform(0.006, 0.019) * (1.0 - 0.3 * abs(a) / 1.25) * (0.7 if inner else 1.0)
                    if rng.random() < 0.1:
                        L *= 0.4                                      # broken stumps
                    w = rng.uniform(0.0016, 0.0029)
                    base = self.H(x, (0.0798 if row == "upper" else 0.0702) + rng.uniform(-0.001, 0.001), z)
                    tip_dir = np.array([rng.uniform(-0.25, 0.25), -1 if row == "upper" else 1, rng.uniform(-0.35, 0.2)])
                    tip = base + nrm(tip_dir) * L
                    out.append((base, tip, w, bone))
        return out
