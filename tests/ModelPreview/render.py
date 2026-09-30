"""Простой программный рендер OBJ из ModelPreview (z-буфер, текстуры, ламберт) — чтобы сравнить модель с эскизом.

python render.py <папка с model.obj> <out.png>
"""
import math
import os
import sys

import numpy as np
from PIL import Image

W, H = 520, 560


def load_obj(folder):
    verts, uvs, norms = [], [], []
    groups = {}
    cur = None
    mtl_tex = {}
    with open(os.path.join(folder, "model.mtl")) as f:
        name = None
        for line in f:
            p = line.split()
            if not p:
                continue
            if p[0] == "newmtl":
                name = p[1]
            elif p[0] == "map_Kd":
                mtl_tex[name] = p[1]
    with open(os.path.join(folder, "model.obj")) as f:
        for line in f:
            if line.startswith("v "):
                verts.append([float(x) for x in line.split()[1:4]])
            elif line.startswith("vt "):
                uvs.append([float(x) for x in line.split()[1:3]])
            elif line.startswith("vn "):
                norms.append([float(x) for x in line.split()[1:4]])
            elif line.startswith("usemtl"):
                cur = line.split()[1]
                groups.setdefault(cur, [])
            elif line.startswith("f "):
                idx = [int(t.split("/")[0]) - 1 for t in line.split()[1:4]]
                groups[cur].append(idx)
    return np.array(verts), np.array(uvs), np.array(norms), groups, mtl_tex


def look_at(eye, target, up=(0, 1, 0)):
    eye, target, up = np.array(eye, float), np.array(target, float), np.array(up, float)
    f = target - eye
    f /= np.linalg.norm(f)
    r = np.cross(up, f)  # Unity: right = up x forward (левая система)
    r /= np.linalg.norm(r)
    u = np.cross(f, r)
    return eye, r, u, f


def render(folder, views, out):
    V, UV, N, groups, mtl_tex = load_obj(folder)
    textures = {}
    for m, t in mtl_tex.items():
        img = np.asarray(Image.open(os.path.join(folder, t)).convert("RGB"), dtype=np.float32) / 255.0
        textures[m] = img[::-1]  # строка 0 = низ (как в Unity)
    tiles = []
    light = np.array([-0.45, 0.75, 0.5])
    light /= np.linalg.norm(light)
    for title, eye, target in views:
        eye, r, u, f = look_at(eye, target)
        fov = math.radians(32)
        scale = (H / 2) / math.tan(fov / 2)
        rel = V - eye
        cx = rel @ r
        cy = rel @ u
        cz = rel @ f
        sx = W / 2 + cx / np.maximum(cz, 1e-3) * scale
        sy = H / 2 - cy / np.maximum(cz, 1e-3) * scale
        color = np.zeros((H, W, 3), np.float32)
        color[:] = np.array([0.72, 0.70, 0.68])
        zbuf = np.full((H, W), np.inf, np.float32)
        for m, faces in groups.items():
            tex = textures.get(m)
            th, tw = tex.shape[:2]
            for (a, b, c) in faces:
                if cz[a] < 0.05 or cz[b] < 0.05 or cz[c] < 0.05:
                    continue
                x0, y0, x1, y1, x2, y2 = sx[a], sy[a], sx[b], sy[b], sx[c], sy[c]
                # отсечение задних граней (в Unity лицевые — по часовой в экранных координатах с Y вниз → det < 0?)
                det = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
                if det <= 0:
                    continue
                minx, maxx = int(max(0, math.floor(min(x0, x1, x2)))), int(min(W - 1, math.ceil(max(x0, x1, x2))))
                miny, maxy = int(max(0, math.floor(min(y0, y1, y2)))), int(min(H - 1, math.ceil(max(y0, y1, y2))))
                if minx > maxx or miny > maxy:
                    continue
                xs, ys = np.meshgrid(np.arange(minx, maxx + 1) + 0.5, np.arange(miny, maxy + 1) + 0.5)
                w0 = ((x1 - xs) * (y2 - ys) - (x2 - xs) * (y1 - ys)) / det
                w1 = ((x2 - xs) * (y0 - ys) - (x0 - xs) * (y2 - ys)) / det
                w2 = 1 - w0 - w1
                inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
                if not inside.any():
                    continue
                # перспективно-корректная интерполяция
                iz0, iz1, iz2 = 1 / cz[a], 1 / cz[b], 1 / cz[c]
                iz = w0 * iz0 + w1 * iz1 + w2 * iz2
                z = 1 / iz
                sub = zbuf[miny:maxy + 1, minx:maxx + 1]
                mask = inside & (z < sub)
                if not mask.any():
                    continue
                pa, pb, pc = w0 * iz0 / iz, w1 * iz1 / iz, w2 * iz2 / iz
                uu = pa * UV[a, 0] + pb * UV[b, 0] + pc * UV[c, 0]
                vv = pa * UV[a, 1] + pb * UV[b, 1] + pc * UV[c, 1]
                tx = (np.mod(uu, 1.0) * (tw - 1)).astype(int)
                ty = (np.mod(vv, 1.0) * (th - 1)).astype(int)
                albedo = tex[ty, tx]
                n = N[a] * pa[..., None] + N[b] * pb[..., None] + N[c] * pc[..., None]
                n /= np.linalg.norm(n, axis=-1, keepdims=True) + 1e-6
                lam = np.clip(n @ light, 0, 1)
                shade = 0.38 + 0.62 * lam
                col = albedo * shade[..., None]
                sub[mask] = z[mask]
                region = color[miny:maxy + 1, minx:maxx + 1]
                region[mask] = col[mask]
        img = Image.fromarray((np.clip(color, 0, 1) * 255).astype(np.uint8))
        tiles.append((title, img))
    sheet = Image.new("RGB", (W * 2, H * 2), (180, 178, 175))
    for i, (title, img) in enumerate(tiles):
        sheet.paste(img, ((i % 2) * W, (i // 2) * H))
    sheet.save(out)


if __name__ == "__main__":
    folder, out = sys.argv[1], sys.argv[2]
    # Кадр подбирается по габаритам модели: простой шар ~6.6 м, средний с парусами ~8 м.
    V = load_obj(folder)[0]
    lo, hi = V.min(axis=0), V.max(axis=0)
    cy = float(lo[1] + hi[1]) / 2
    d = float((hi - lo).max()) * 1.95
    views = [
        ("side", (d, cy, 0.0), (0, cy, 0)),
        ("front", (0.0, cy, d), (0, cy, 0)),
        ("3d front", (-0.5 * d, cy + 0.3 * d, 0.8 * d), (0, cy - 0.3, 0)),
        ("3d back", (0.35 * d, cy + 0.15 * d, -0.9 * d), (0, cy - 0.3, 0)),
    ]
    render(folder, views, out)
    print("saved", out)
