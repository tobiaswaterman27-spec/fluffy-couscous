"""Builds Elliot: sculpt -> mesh -> skin weights -> UV atlas -> baked textures -> files for Unity and the viewer.

    python3 build.py [--quick]       (quick = coarser sculpt and 1024 textures, for checking changes)
"""
import os, sys, time, struct
import numpy as np
import xatlas
from PIL import Image
from elliot import Elliot, H0
from mesh import polygonise, keep_largest, decimate
from rig import skin, region_masks
from bake import *
from sdf import fbm, perlin, smoothstep, nrm

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
UNITY_MODEL = os.path.join(ROOT, "Assets/AfterHours/Models/Elliot")
UNITY_TEX = os.path.join(ROOT, "Assets/AfterHours/Textures/Creature")
QUICK = "--quick" in sys.argv
VIEWER = next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--viewer=")), None)
t0 = time.time()


def log(s):
    print(f"[{time.time() - t0:6.1f}s] {s}", flush=True)


def tangents(V, N, UV, F):
    t1 = np.zeros_like(V); t2 = np.zeros_like(V)
    v0, v1, v2 = V[F[:, 0]], V[F[:, 1]], V[F[:, 2]]
    w0, w1, w2 = UV[F[:, 0]], UV[F[:, 1]], UV[F[:, 2]]
    e1, e2 = v1 - v0, v2 - v0
    d1, d2 = w1 - w0, w2 - w0
    r = d1[:, 0] * d2[:, 1] - d2[:, 0] * d1[:, 1]
    r = np.where(np.abs(r) < 1e-12, 1e-12, r)
    sdir = (e1 * d2[:, 1:2] - e2 * d1[:, 1:2]) / r[:, None]
    tdir = (e2 * d1[:, 0:1] - e1 * d2[:, 0:1]) / r[:, None]
    for k in range(3):
        np.add.at(t1, F[:, k], sdir)
        np.add.at(t2, F[:, k], tdir)
    t = t1 - N * np.sum(N * t1, 1, keepdims=True)
    bad = np.linalg.norm(t, axis=1) < 1e-9
    t[bad] = np.cross(N[bad], [0.31, 0.9, 0.3])
    t /= np.linalg.norm(t, axis=1, keepdims=True)
    w = np.where(np.sum(np.cross(N, t) * t2, 1) < 0, -1.0, 1.0)
    return np.concatenate([t, w[:, None]], 1)


def vertex_normals(V, F):
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    n = np.zeros_like(V)
    for k in range(3):
        np.add.at(n, F[:, k], fn)
    l = np.linalg.norm(n, axis=1, keepdims=True)
    n = np.where(l > 1e-12, n / np.maximum(l, 1e-12), [0, 1, 0])
    return n


# ---------------------------------------------------------------- mouth parts (eyes, teeth, tongue)
def mouth_parts(el):
    rng = np.random.default_rng(66)
    V, UV, F, B = [], [], [], []

    def add(verts, uvs, tris, bone, centre=None):
        o = sum(len(v) for v in V)
        verts = np.asarray(verts, float); tris = np.asarray(tris, np.int64)
        fn = np.cross(verts[tris[:, 1]] - verts[tris[:, 0]], verts[tris[:, 2]] - verts[tris[:, 0]])
        mid = verts[tris].mean(1)
        c = centre(mid) if centre is not None else verts.mean(0)
        flip = np.sum(fn * (mid - c), 1) < 0
        tris[flip] = tris[flip][:, [0, 2, 1]]
        V.append(verts); UV.append(np.asarray(uvs, float)); F.append(tris + o)
        B.append(np.full(len(verts), el.index[bone]))

    # eyes: wet, black, no pupils
    for s in (-1, 1):
        ey = 0.184 if s < 0 else 0.177
        c = H0 + [s * 0.034, ey - 0.003, 0.071]
        nu, nv = 18, 12
        vs, uvs = [], []
        for j in range(nv + 1):
            th = np.pi * j / nv
            for i in range(nu + 1):
                ph = 2 * np.pi * i / nu
                d = np.array([np.sin(th) * np.cos(ph), np.cos(th), np.sin(th) * np.sin(ph)])
                vs.append(c + d * [0.0182, 0.016, 0.0182])
                uvs.append([0.55 + 0.4 * i / nu, 0.55 + 0.4 * j / nv])
        tris = []
        for j in range(nv):
            for i in range(nu):
                a = j * (nu + 1) + i
                tris += [[a, a + nu + 1, a + 1], [a + 1, a + nu + 1, a + nu + 2]]
        add(vs, uvs, tris, "Head")
    # teeth
    teeth = el.teeth(rng)
    for k, (base, tip, w, bone) in enumerate(teeth):
        ax = nrm(tip - base)
        tang = nrm(np.cross(ax, [0, 0, 1]) if abs(ax[2]) < 0.9 else np.cross(ax, [1, 0, 0]))
        bit = np.cross(ax, tang)
        root = base - ax * 0.003
        seg = 6
        vs, uvs = [], []
        u0 = 0.02 + 0.46 * (k % 23) / 23
        centre_t = base + (tip - base) * 0.3
        for ring, (cen, rad, vv) in enumerate(((root, w, 0.0), (base, w, 0.25), (base + (tip - base) * 0.55, w * 0.55, 0.65))):
            for i in range(seg + 1):
                a = 2 * np.pi * i / seg
                vs.append(cen + (tang * np.cos(a) + bit * 0.7 * np.sin(a)) * rad)
                uvs.append([u0 + 0.018 * i / seg, 0.02 + 0.96 * vv])
        vs.append(tip); uvs.append([u0 + 0.009, 0.98])
        tris = []
        for r in range(2):
            for i in range(seg):
                a = r * (seg + 1) + i
                tris += [[a, a + 1, a + seg + 1], [a + 1, a + seg + 2, a + seg + 1]]
        tipi = len(vs) - 1
        for i in range(seg):
            a = 2 * (seg + 1) + i
            tris.append([a, a + 1, tipi])
        add(vs, uvs, tris, bone, lambda mid, a=root, ax=ax: a + np.outer((mid - a) @ ax, ax))
    # tongue
    rings, seg = 12, 12
    vs, uvs = [], []
    for j in range(rings + 1):
        t = j / rings
        c = H0 + [0, 0.06 + 0.002 * np.sin(t * np.pi), -0.006 + t * 0.082]
        hw = 0.018 * (1 - 0.55 * t ** 1.5) * (1 if j < rings else 0.15)
        hh = 0.0062 * (1 - 0.4 * t) * (1 if j < rings else 0.2)
        for i in range(seg + 1):
            a = 2 * np.pi * i / seg
            vs.append(c + [np.cos(a) * hw, np.sin(a) * hh, 0])
            uvs.append([0.52 + 0.46 * i / seg, 0.02 + 0.46 * t])
    tris = []
    for j in range(rings):
        for i in range(seg):
            a = j * (seg + 1) + i
            tris += [[a, a + seg + 1, a + 1], [a + 1, a + seg + 1, a + seg + 2]]
    add(vs, uvs, tris, "Tongue", lambda mid: np.stack([np.full(len(mid), H0[0]), np.full(len(mid), H0[1] + 0.06), np.clip(mid[:, 2], H0[2] - 0.004, H0[2] + 0.07)], 1))
    V = np.concatenate(V); UV = np.concatenate(UV); F = np.concatenate(F); B = np.concatenate(B)
    # make every triangle face outwards (consistent with the body), using the part's own centre
    return V, UV, F, B


def fix_winding(V, F, centre_of):
    c = centre_of
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    mid = V[F].mean(1)
    flip = np.sum(fn * (mid - c), 1) < 0
    F[flip] = F[flip][:, [0, 2, 1]]
    return F


def mouth_texture(size):
    y, x = np.mgrid[0:size, 0:size] / size
    col = np.zeros((size, size, 3)); sm = np.zeros((size, size))
    P = np.stack([x.ravel() * 40, y.ravel() * 40, np.zeros(x.size)], 1)
    n1 = fbm(P, 1, 3).reshape(size, size)
    n2 = fbm(P * [6, 0.4, 1], 1, 2, seed=3).reshape(size, size)
    teeth = x < 0.5
    v = y
    ivory = np.array([0.62, 0.55, 0.38])
    tc = ivory * (0.75 + 0.2 * n1[..., None]) * (0.85 + 0.3 * fbm(np.stack([np.floor(x.ravel() * 23 * 2) , np.zeros(x.size), np.zeros(x.size)], 1), 0.37, 1, seed=4).reshape(size, size)[..., None])
    tc = tc * (1 - 0.35 * np.clip(n2, 0, 1)[..., None]) + np.array([0.35, 0.25, 0.12]) * 0.35 * np.clip(n2, 0, 1)[..., None]
    gum = smoothstep(0.32, 0.2, v)[..., None]
    tc = tc * (1 - gum) + np.array([0.42, 0.12, 0.12]) * gum
    tip = smoothstep(0.75, 0.98, v)[..., None]
    tc = tc * (1 - tip * 0.3) + np.array([0.45, 0.43, 0.38]) * tip * 0.3
    col[teeth] = tc[teeth]
    sm[teeth] = 0.6
    tongue = (x >= 0.5) & (y < 0.5)
    tg = np.array([0.33, 0.09, 0.11]) * (0.8 + 0.3 * n1[..., None]) + 0.04 * (fbm(P * 8, 1, 1, seed=8).reshape(size, size)[..., None] > 0.3)
    col[tongue] = tg[tongue]
    sm[tongue] = 0.88
    eye = (x >= 0.5) & (y >= 0.5)
    col[eye] = np.array([0.018, 0.012, 0.013]) + 0.01 * n1[eye][:, None]
    sm[eye] = 0.96
    return col, sm


def write_model(path, el, V, N, T, UV, BI, BW, subs):
    with open(path, "wb") as f:
        f.write(b"ELM1")
        f.write(struct.pack("<i", len(el.bones)))
        from elliot import mat_to_quat
        for b in el.bones:
            nm = b.name.encode()
            f.write(struct.pack("<i", len(nm))); f.write(nm)
            f.write(struct.pack("<i", el.index[b.parent] if b.parent else -1))
            f.write(struct.pack("<3f", *b.pos))
            f.write(struct.pack("<4f", *mat_to_quat(b.rot)))
        f.write(struct.pack("<i", len(V)))
        f.write(V.astype("<f4").tobytes()); f.write(N.astype("<f4").tobytes()); f.write(T.astype("<f4").tobytes())
        f.write(UV.astype("<f4").tobytes()); f.write(BI.astype("<u2").tobytes()); f.write(BW.astype("<f4").tobytes())
        f.write(struct.pack("<i", len(subs)))
        for name, idx in subs:
            nm = name.encode()
            f.write(struct.pack("<i", len(nm))); f.write(nm)
            f.write(struct.pack("<i", idx.size)); f.write(idx.astype("<u4").tobytes())


def save_rgb(a, path, q=None):
    im = Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8))
    im.save(path, quality=q) if q else im.save(path)


def main():
    el = Elliot()
    fine = 0.004 if QUICK else 0.0025
    size = 1024 if QUICK else 2048
    V, F = polygonise(el.m, [-0.9, -0.01, -0.2], [0.9, 2.6, 0.34], fine=fine, coarse=0.012 if QUICK else 0.01, log=lambda s: None)
    V, F = keep_largest(V, F)
    log(f"sculpt: {len(F)} tris")
    V, F = decimate(V, F, 36000 if QUICK else 76000)
    log(f"decimated: {len(F)} tris, {len(V)} verts")
    N = el.m.gradient(V)
    BI, BW, TG, Wfull = skin(el, V)
    MK, _ = region_masks(el, Wfull, TG)
    log("skinned")
    atlas = xatlas.Atlas()
    atlas.add_mesh(V.astype(np.float32), F.astype(np.uint32))
    co = xatlas.ChartOptions(); co.max_iterations = 2
    po = xatlas.PackOptions(); po.resolution = size; po.padding = 6 if not QUICK else 4; po.bilinear = True
    atlas.generate(co, po)
    vmap, F2, UV = atlas[0]
    F2 = F2.astype(np.int64)
    V2, N2, BI2, BW2, TG2, MK2 = V[vmap], N[vmap], BI[vmap], BW[vmap], TG[vmap], MK[vmap]
    log(f"uv atlas: {len(V2)} verts")
    T2 = tangents(V2, N2, UV, F2)
    # ---------------------------------------------------- bake
    tid, bary = rasterize(UV, F2, size)
    mask = tid >= 0
    log(f"rasterised {mask.sum()} texels")
    p = interp(V2, F2, tid, bary, mask)
    nl = interp(N2, F2, tid, bary, mask); nl /= np.linalg.norm(nl, axis=1, keepdims=True)
    tg = interp(T2[:, :3], F2, tid, bary, mask)
    tg -= nl * np.sum(tg * nl, 1, keepdims=True); tg /= np.maximum(np.linalg.norm(tg, axis=1, keepdims=True), 1e-9)
    tw = T2[F2[tid[mask], 0], 3]
    bt = np.cross(nl, tg) * tw[:, None]
    tags = interp(TG2, F2, tid, bary, mask)
    masks = interp(MK2, F2, tid, bary, mask)
    # snap the texel onto the true surface, then take the sculpt's own normal there
    d = el.m.evaluate(p, far=0.05)
    g = el.m.gradient(p)
    p = p - g * np.clip(d, -0.004, 0.004)[:, None]
    nh = el.m.gradient(p)
    log("sculpt normals")
    nh = detail_normal(el, p, nh, masks)
    log("detail normals")
    ao = ambient_occlusion(el.m, p, nh)
    log("ambient occlusion")
    ts = np.stack([np.sum(nh * tg, 1), np.sum(nh * bt, 1), np.sum(nh * nl, 1)], 1)
    ts[:, 2] = np.maximum(ts[:, 2], 0.05)
    ts /= np.linalg.norm(ts, axis=1, keepdims=True)
    img = lambda vals, ch: _img(vals, ch, size, mask)
    nimg = dilate(img(ts * 0.5 + 0.5, 3), mask)
    os.makedirs(UNITY_TEX, exist_ok=True); os.makedirs(UNITY_MODEL, exist_ok=True)
    # UV v=0 is the bottom row in Unity/three textures: flip vertically when saving
    flip = lambda a: a[::-1]
    save_rgb(flip(nimg), os.path.join(UNITY_TEX, "elliot_body_N.png"))
    outs = {}
    for variant, bloody in (("body", True),):   # he always looks like this: he has fed
        col, sm = albedo(el, p, nh, tags, masks, ao, bloody)
        cimg = dilate(img(col, 3), mask)
        mimg = np.zeros((size, size, 4))
        mimg[..., 1] = dilate(img(ao, 1), mask)[..., 0]
        mimg[..., 3] = dilate(img(sm, 1), mask)[..., 0]
        save_rgb(flip(cimg), os.path.join(UNITY_TEX, f"elliot_{variant}_A.jpg"), 93)
        Image.fromarray((flip(mimg) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(UNITY_TEX, f"elliot_{variant}_M.png"))
        outs[variant] = (cimg, mimg)
        log(f"{variant} textures")
    # ---------------------------------------------------- mouth parts
    MV, MUV, MF, MB = mouth_parts(el)
    # winding: eyes outward from their centres; teeth / tongue outward from their own centres
    MN = vertex_normals(MV, MF)
    MT = tangents(MV, MN, MUV, MF)
    mcol, msm = mouth_texture(512)
    save_rgb(flip(mcol), os.path.join(UNITY_TEX, "elliot_mouth_A.png"))
    mm = np.zeros((512, 512, 4)); mm[..., 1] = 1; mm[..., 3] = msm
    Image.fromarray((flip(mm) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(UNITY_TEX, "elliot_mouth_M.png"))
    MBI = np.zeros((len(MV), 4), np.uint16); MBI[:, 0] = MB
    MBW = np.zeros((len(MV), 4), np.float32); MBW[:, 0] = 1
    # ---------------------------------------------------- write
    o = len(V2)
    AV = np.concatenate([V2, MV]); AN = np.concatenate([N2, MN]); AT = np.concatenate([T2, MT]); AUV = np.concatenate([UV, MUV])
    ABI = np.concatenate([BI2, MBI]); ABW = np.concatenate([BW2, MBW])
    subs = [("Elliot_Skin", F2.ravel()), ("Elliot_MouthParts", (MF + o).ravel())]
    write_model(os.path.join(UNITY_MODEL, "elliot_model.bytes"), el, AV, AN, AT, AUV, ABI, ABW, subs)
    log(f"wrote model: {len(AV)} verts, {len(F2) + len(MF)} tris")
    if VIEWER:
        os.makedirs(VIEWER, exist_ok=True)
        import shutil
        shutil.copy(os.path.join(UNITY_MODEL, "elliot_model.bytes"), os.path.join(VIEWER, "elliot_model.bin"))
        for variant in ("body",):
            cimg, mimg = outs[variant]
            save_rgb(flip(cimg), os.path.join(VIEWER, f"{variant}_A.jpg"), 88)
            r = np.zeros((size, size, 3)); r[..., 0] = mimg[..., 1]; r[..., 1] = 1 - mimg[..., 3]
            save_rgb(flip(r), os.path.join(VIEWER, f"{variant}_R.jpg"), 90)
        save_rgb(flip(nimg), os.path.join(VIEWER, "body_N.jpg"), 92)
        save_rgb(flip(mcol), os.path.join(VIEWER, "mouth_A.jpg"), 90)
        r = np.zeros((512, 512, 3)); r[..., 1] = 1 - msm
        save_rgb(flip(r), os.path.join(VIEWER, "mouth_R.jpg"), 90)
        log("viewer files")


def _img(vals, ch, size, mask):
    out = np.zeros((size, size, ch))
    out[mask] = vals.reshape(len(vals), -1)
    return out


if __name__ == "__main__":
    main()
