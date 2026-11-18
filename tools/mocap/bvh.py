"""Minimal BVH reader + forward kinematics (numpy). Positions come out in Unity's convention:
left-handed, Y up, the character's left at -X (the BVH's right-handed X is mirrored)."""
import numpy as np


class Joint:
    def __init__(self, name, parent):
        self.name, self.parent = name, parent
        self.offset = np.zeros(3)
        self.channels = []
        self.children = []
        self.end = None          # End Site offset


def euler_mat(axis, deg):
    a = np.radians(deg)
    c, s = np.cos(a), np.sin(a)
    n = len(a)
    m = np.zeros((n, 3, 3))
    if axis == 'X':
        m[:, 0, 0] = 1; m[:, 1, 1] = c; m[:, 1, 2] = -s; m[:, 2, 1] = s; m[:, 2, 2] = c
    elif axis == 'Y':
        m[:, 1, 1] = 1; m[:, 0, 0] = c; m[:, 0, 2] = s; m[:, 2, 0] = -s; m[:, 2, 2] = c
    else:
        m[:, 2, 2] = 1; m[:, 0, 0] = c; m[:, 0, 1] = -s; m[:, 1, 0] = s; m[:, 1, 1] = c
    return m


def load(path):
    tok = open(path).read().split()
    i = 0
    joints, stack = [], []
    cur = None
    while tok[i] != 'MOTION':
        t = tok[i]
        if t in ('ROOT', 'JOINT'):
            j = Joint(tok[i + 1], stack[-1] if stack else None)
            if j.parent is not None: j.parent.children.append(j)
            joints.append(j); cur = j; i += 2
        elif t == 'End':
            i += 2
            # { OFFSET x y z }
            assert tok[i] == '{'
            cur.end = np.array([float(tok[i + 2]), float(tok[i + 3]), float(tok[i + 4])])
            i += 6
        elif t == '{':
            stack.append(cur); i += 1
        elif t == '}':
            stack.pop(); cur = stack[-1] if stack else None; i += 1
        elif t == 'OFFSET':
            cur.offset = np.array([float(tok[i + 1]), float(tok[i + 2]), float(tok[i + 3])]); i += 4
        elif t == 'CHANNELS':
            n = int(tok[i + 1]); cur.channels = tok[i + 2:i + 2 + n]; i += 2 + n
        else:
            i += 1
    i += 1
    nf = int(tok[i + 1]); i += 2
    ft = float(tok[i + 2]); i += 3
    data = np.array(tok[i:], dtype=float).reshape(nf, -1)
    return joints, data, ft


def fk(joints, data):
    """World positions (F,J,3) and world rotation matrices (F,J,3,3), BVH (right-handed) space."""
    F = data.shape[0]
    J = len(joints)
    pos = np.zeros((F, J, 3)); rot = np.zeros((F, J, 3, 3))
    col = 0
    idx = {j: k for k, j in enumerate(joints)}
    names = {j.name: k for k, j in enumerate(joints)}
    local_r = []
    local_t = []
    for j in joints:
        R = np.tile(np.eye(3), (F, 1, 1))
        T = np.tile(j.offset, (F, 1))
        for ch in j.channels:
            v = data[:, col]; col += 1
            if ch.endswith('position'):
                T = T.copy(); T[:, 'XYZ'.index(ch[0])] = v + (0 if j.parent is not None else 0)
            else:
                R = R @ euler_mat(ch[0], v)
        local_r.append(R); local_t.append(T)
    for k, j in enumerate(joints):
        if j.parent is None:
            rot[:, k] = local_r[k]; pos[:, k] = local_t[k]
        else:
            p = idx[j.parent]
            rot[:, k] = rot[:, p] @ local_r[k]
            pos[:, k] = pos[:, p] + np.einsum('fij,fj->fi', rot[:, p], local_t[k])
    return pos, rot, names


def to_unity(pos, rot):
    """Mirror X: right-handed BVH -> left-handed Unity. Rotations: M' = S M S with S = diag(-1,1,1)."""
    S = np.diag([-1.0, 1.0, 1.0])
    p = pos.copy(); p[..., 0] *= -1
    r = S @ rot @ S
    return p, r
