"""Иконка и баннер мода: три шара из ModelPreview в небе — программный рендер со сглаживанием
(кадр в несколько раз больше и уменьшение).

dotnet run --project tests/ModelPreview -- out/simple simple
dotnet run --project tests/ModelPreview -- out/medium medium 2 0
dotnet run --project tests/ModelPreview -- out/large large
python tests/ModelPreview/icon.py out package/icon.png             иконка 256×256
python tests/ModelPreview/icon.py out media/banner.png --banner    баннер 1300×372 (шапка страницы мода)
Ключ --big — ещё и кадр до уменьшения рядом.
"""
import math
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from render import load_obj, look_at  # noqa: E402


def lin(c):
    return np.power(np.asarray(c, np.float32), 2.2)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def grid(x0, x1, y0, y1):
    return np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)


class Model:
    """Модель из папки ModelPreview, поставленная в сцену: поворот вокруг вертикали и сдвиг."""

    def __init__(self, folder, pos, yaw, haze, fire=None, glow=1.0):
        V, UV, N, groups, mtl_tex = load_obj(folder)
        a = math.radians(yaw)
        # Поворот Unity вокруг Y: (0, 0, 1) -> (sin, 0, cos).
        rot = np.array([[math.cos(a), 0, math.sin(a)], [0, 1, 0], [-math.sin(a), 0, math.cos(a)]], np.float32)
        self.V = V @ rot.T + np.array(pos, np.float32)
        self.N = N @ rot.T
        self.UV = UV
        self.groups = groups
        self.textures = {}
        for m, t in mtl_tex.items():
            img = np.asarray(Image.open(os.path.join(folder, t)).convert("RGB"), dtype=np.float32) / 255.0
            self.textures[m] = lin(img[::-1])  # строка 0 = низ (как в Unity)
        self.haze = haze
        self.fire = None if fire is None else rot @ np.array(fire, np.float32) + np.array(pos, np.float32)
        self.glow = glow


class Camera:
    """Перспектива с вертикальным углом обзора fov_deg на кадр W×H."""

    def __init__(self, eye, target, fov_deg, W, H):
        self.W, self.H = W, H
        self.eye, self.r, self.u, self.f = look_at(eye, target)
        self.scale = (H / 2) / math.tan(math.radians(fov_deg) / 2)

    def project(self, P):
        rel = P - self.eye
        cx, cy, cz = rel @ self.r, rel @ self.u, rel @ self.f
        sx = self.W / 2 + cx / np.maximum(cz, 1e-3) * self.scale
        sy = self.H / 2 - cy / np.maximum(cz, 1e-3) * self.scale
        return sx, sy, cz

    def rays(self):
        xs, ys = grid(0, self.W, 0, self.H)
        d = (self.r[None, None, :] * ((xs - self.W / 2) / self.scale)[..., None]
             + self.u[None, None, :] * (-(ys - self.H / 2) / self.scale)[..., None]
             + self.f[None, None, :])
        return d / np.linalg.norm(d, axis=-1, keepdims=True)


def rasterize(model, cam, gb):
    """Треугольники модели в G-буфер (альбедо, нормаль, глубина, дымка). Без отсечения задних граней: паруса тонкие."""
    alb, nrm, dep, haze = gb
    H, W = dep.shape
    sx, sy, cz = cam.project(model.V)
    for m, faces in model.groups.items():
        tex = model.textures[m]
        th, tw = tex.shape[:2]
        for (a, b, c) in faces:
            if cz[a] < 0.05 or cz[b] < 0.05 or cz[c] < 0.05:
                continue
            x0, y0, x1, y1, x2, y2 = sx[a], sy[a], sx[b], sy[b], sx[c], sy[c]
            det = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
            if abs(det) < 1e-9:
                continue
            minx, maxx = int(max(0, math.floor(min(x0, x1, x2)))), int(min(W - 1, math.ceil(max(x0, x1, x2))))
            miny, maxy = int(max(0, math.floor(min(y0, y1, y2)))), int(min(H - 1, math.ceil(max(y0, y1, y2))))
            if minx > maxx or miny > maxy:
                continue
            xs, ys = grid(minx, maxx + 1, miny, maxy + 1)
            w0 = ((x1 - xs) * (y2 - ys) - (x2 - xs) * (y1 - ys)) / det
            w1 = ((x2 - xs) * (y0 - ys) - (x0 - xs) * (y2 - ys)) / det
            w2 = 1 - w0 - w1
            inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
            if not inside.any():
                continue
            iz0, iz1, iz2 = 1 / cz[a], 1 / cz[b], 1 / cz[c]
            iz = w0 * iz0 + w1 * iz1 + w2 * iz2
            z = 1 / iz
            sub = dep[miny:maxy + 1, minx:maxx + 1]
            mask = inside & (z < sub)
            if not mask.any():
                continue
            pa, pb, pc = w0 * iz0 / iz, w1 * iz1 / iz, w2 * iz2 / iz
            uu = pa * model.UV[a, 0] + pb * model.UV[b, 0] + pc * model.UV[c, 0]
            vv = pa * model.UV[a, 1] + pb * model.UV[b, 1] + pc * model.UV[c, 1]
            tx = (np.mod(uu, 1.0) * (tw - 1)).astype(int)
            ty = (np.mod(vv, 1.0) * (th - 1)).astype(int)
            n = model.N[a] * pa[..., None] + model.N[b] * pb[..., None] + model.N[c] * pc[..., None]
            sub[mask] = z[mask]
            alb[miny:maxy + 1, minx:maxx + 1][mask] = tex[ty, tx][mask]
            nrm[miny:maxy + 1, minx:maxx + 1][mask] = n[mask]
            haze[miny:maxy + 1, minx:maxx + 1][mask] = model.haze


def sky(rays_y):
    """Небо по высоте луча: густо-синее вверху, светлое к горизонту."""
    t = np.clip((rays_y + 0.12) / 0.75, 0, 1)[..., None]
    top, mid, low = lin([0.13, 0.33, 0.62]), lin([0.36, 0.58, 0.82]), lin([0.80, 0.86, 0.90])
    upper = mid + (top - mid) * np.clip((t - 0.35) / 0.65, 0, 1)
    return np.where(t < 0.35, low + (mid - low) * (t / 0.35), upper)


def clouds(img, rng, cx, cy, w, h, puffs, light_dir=(-0.6, -0.8), dark=0.62):
    """Кучевое облако из мягких «клубов» (размеры в пикселях кадра): плоское снизу, освещено сверху-слева."""
    H, W = img.shape[:2]
    # Считаем только там, где облако может быть видно: дальше клубы прозрачны.
    x0, x1 = max(0, int(cx - 0.5 * w - 0.6 * h)), min(W, int(cx + 0.5 * w + 0.6 * h) + 1)
    y0, y1 = max(0, int(cy - 1.1 * h)), min(H, int(cy + 0.2 * h) + 1)
    empty = x1 <= x0 or y1 <= y0
    xs, ys = grid(x0, max(x0 + 1, x1), y0, max(y0 + 1, y1))
    field = np.zeros(xs.shape, np.float32)
    lit_sum = np.zeros(xs.shape, np.float32)
    weight = np.full(xs.shape, 1e-6, np.float32)
    for _ in range(puffs):
        px = cx + rng.uniform(-0.5, 0.5) * w
        k = 1 - abs(px - cx) / (0.5 * w)
        pr = (0.35 + 0.65 * k) * h * rng.uniform(0.35, 0.6)
        py = cy - rng.uniform(0.0, 0.5) * h * k
        d2 = ((xs - px) ** 2 + (ys - py) ** 2) / (pr * pr)
        g = np.exp(-d2 * 1.6)
        field = np.maximum(field, g)
        # Освещённость клуба: со стороны света ярче; клубы смешиваются плавно, без швов.
        lit = 0.5 + 0.5 * np.clip(((xs - px) * light_dir[0] + (ys - py) * light_dir[1]) / pr, -1, 1)
        pw = g ** 4
        lit_sum += lit * pw
        weight += pw
    if empty:
        return img
    shade = lit_sum / weight
    bottom = smoothstep(cy + 0.15 * h, cy - 0.05 * h, ys)  # ровный низ
    alpha = smoothstep(0.32, 0.5, field) * bottom
    c = lin([0.98, 0.98, 0.99])[None, None, :] * (dark + (1 - dark) * shade[..., None])
    c = c * (0.92 + 0.08 * smoothstep(cy + 0.1 * h, cy - 0.6 * h, ys))[..., None] + lin([0.05, 0.06, 0.1]) * (1 - shade[..., None]) * 0.3
    out = img.copy()
    out[y0:y1, x0:x1] = img[y0:y1, x0:x1] * (1 - alpha[..., None]) + c * alpha[..., None]
    return out


def hills(img, rng, base, amp, color, haze_col, haze, ridged=False):
    """Гряда холмов внизу кадра: base и amp — в долях высоты кадра; ridged — острые горные хребты."""
    H, W = img.shape[:2]
    xs = np.arange(W) / H
    if ridged:
        y = base + sum(amp / (k + 1) ** 1.4 * (2 * np.abs(np.sin(xs * (0.8 + 2.2 * k) * rng.uniform(0.8, 1.25) * math.pi
                                                                   + rng.uniform(0, 6.28))) - 1) for k in range(5))
    else:
        y = base + sum(amp / (k + 1) * np.sin(xs * (3 + 5 * k) * math.pi + rng.uniform(0, 6.28)) for k in range(5))
    ys = np.arange(H)[:, None] / H
    mask = smoothstep(-1.5 / H, 1.5 / H, ys - y[None, :])
    col = lin(color) * (1 - haze) + lin(haze_col) * haze
    return img * (1 - mask[..., None]) + col[None, None, :] * mask[..., None]


def shade(gb, rays, sun_dir, fog_col):
    alb, nrm, dep, haze = gb
    hit = np.isfinite(dep)
    n = nrm[hit]
    n /= np.linalg.norm(n, axis=-1, keepdims=True) + 1e-6
    v = -rays[hit]
    flip = (n * v).sum(-1) < 0
    n[flip] *= -1  # тонкие поверхности видны с обеих сторон
    sun = np.array(sun_dir, np.float32)
    sun /= np.linalg.norm(sun)
    ndl = np.clip((n @ sun + 0.15) / 1.15, 0, 1)
    sky_amb, gnd_amb = lin([0.62, 0.70, 0.84]), lin([0.36, 0.33, 0.30])
    hemi = gnd_amb + (sky_amb - gnd_amb) * (0.5 + 0.5 * n[:, 1:2])
    light = hemi * 0.62 + lin([1.0, 0.93, 0.80]) * 1.05 * ndl[:, None]
    col = alb[hit] * light
    rim = np.power(1 - np.clip((n * v).sum(-1), 0, 1), 3)[:, None]
    col = col + lin([0.75, 0.82, 0.95]) * rim * 0.12
    k = haze[hit][:, None]
    col = col * (1 - k) + fog_col * k
    out = np.zeros_like(alb)
    out[hit] = col
    return out, hit


def glow(img, cam, dep, pos, radius_m, strength):
    """Огонь под куполом: тёплое свечение там, где его не загораживает ближняя геометрия."""
    sx, sy, cz = cam.project(np.array([pos], np.float32))
    sx, sy, cz = float(sx[0]), float(sy[0]), float(cz[0])
    r = radius_m / cz * cam.scale
    H, W = dep.shape
    # За 3 радиуса свечение уже незаметно.
    x0, x1 = max(0, int(sx - 3 * r)), min(W, int(sx + 3 * r) + 1)
    y0, y1 = max(0, int(sy - 3 * r)), min(H, int(sy + 3 * r) + 1)
    if x1 <= x0 or y1 <= y0:
        return img
    xs, ys = grid(x0, x1, y0, y1)
    d = np.sqrt((xs - sx) ** 2 + (ys - sy) ** 2) / r
    visible = (dep[y0:y1, x0:x1] > cz - 0.35).astype(np.float32)
    core = np.exp(-d * d * 6) * visible
    halo = np.exp(-d * d * 1.2) * (0.35 + 0.65 * visible)
    add = lin([1.0, 0.85, 0.45])[None, None, :] * core[..., None] * 1.6 + lin([1.0, 0.5, 0.15])[None, None, :] * halo[..., None] * 0.5
    out = img.copy()
    out[y0:y1, x0:x1] += add * strength
    return out


def icon_scene(base, rng, W, H):
    """Иконка: драккар крупно, за ним карви и тролль."""
    cam = Camera(eye=(0.0, 0.0, 0.0), target=(0.0, 3.2, 20.0), fov_deg=44, W=W, H=H)
    img = sky(cam.rays()[..., 1])
    img = clouds(img, rng, 0.80 * W, 0.40 * H, 0.62 * W, 0.30 * H, 26)
    img = clouds(img, rng, 0.18 * W, 0.70 * H, 0.55 * W, 0.22 * H, 18)
    img = clouds(img, rng, 0.62 * W, 0.82 * H, 0.9 * W, 0.18 * H, 26)
    img = hills(img, rng, 0.90, 0.025, [0.30, 0.42, 0.40], [0.72, 0.80, 0.86], 0.55)
    img = hills(img, rng, 0.95, 0.02, [0.16, 0.26, 0.20], [0.72, 0.80, 0.86], 0.25)
    models = [
        Model(os.path.join(base, "simple"), pos=(7.2, 12.6, 43.0), yaw=30, haze=0.16, fire=(0.0, 2.22, 0.0), glow=0.5),
        Model(os.path.join(base, "medium"), pos=(-6.6, 6.2, 29.0), yaw=-30, haze=0.14, fire=(0.0, 2.35, 0.0), glow=0.8),
        Model(os.path.join(base, "large"), pos=(2.2, -2.6, 17.5), yaw=-125, haze=0.0, fire=(0.0, 2.6, 0.0)),
    ]
    return cam, img, models


def banner_scene(base, rng, W, H):
    """Баннер: панорама — «Дутый тролль» крупно на фоне облака, драккар справа летит к нему, карви слева вдали."""
    cam = Camera(eye=(0.0, 0.0, 0.0), target=(0.0, 1.8, 26.0), fov_deg=20, W=W, H=H)
    img = sky(cam.rays()[..., 1])
    img = clouds(img, rng, 0.38 * W, 0.72 * H, 0.32 * W, 0.56 * H, 34)
    img = clouds(img, rng, 0.86 * W, 0.93 * H, 0.30 * W, 0.30 * H, 26)
    img = clouds(img, rng, 0.10 * W, 0.95 * H, 0.25 * W, 0.28 * H, 20)
    img = clouds(img, rng, 0.58 * W, 0.26 * H, 0.10 * W, 0.18 * H, 18)
    img = hills(img, rng, 0.84, 0.035, [0.42, 0.52, 0.56], [0.74, 0.82, 0.88], 0.62, ridged=True)
    img = hills(img, rng, 0.89, 0.03, [0.30, 0.42, 0.40], [0.72, 0.80, 0.86], 0.5, ridged=True)
    img = hills(img, rng, 0.95, 0.022, [0.16, 0.26, 0.20], [0.72, 0.80, 0.86], 0.22)
    models = [
        Model(os.path.join(base, "medium"), pos=(-27.0, 0.9, 55.0), yaw=40, haze=0.2, fire=(0.0, 2.35, 0.0), glow=0.5),
        Model(os.path.join(base, "large"), pos=(13.6, 1.5, 48.0), yaw=-115, haze=0.15, fire=(0.0, 2.6, 0.0), glow=0.7),
        Model(os.path.join(base, "simple"), pos=(-3.5, -1.7, 24.0), yaw=30, haze=0.0, fire=(0.0, 2.22, 0.0)),
    ]
    return cam, img, models


def main():
    base, out = sys.argv[1], sys.argv[2]
    banner = "--banner" in sys.argv[3:]
    size, ss, scene = ((1300, 372), 3, banner_scene) if banner else ((256, 256), 4, icon_scene)
    W, H = size[0] * ss, size[1] * ss
    rng = np.random.default_rng(7)
    cam, img, models = scene(base, rng, W, H)

    gb = (np.zeros((H, W, 3), np.float32), np.zeros((H, W, 3), np.float32), np.full((H, W), np.inf, np.float32),
          np.zeros((H, W), np.float32))
    for m in models:
        rasterize(m, cam, gb)
    fog_col = lin([0.70, 0.80, 0.90])
    col, hit = shade(gb, cam.rays(), sun_dir=(-0.55, 0.75, -0.35), fog_col=fog_col)
    img = np.where(hit[..., None], col, img)
    for m in models:
        img = glow(img, cam, gb[2], m.fire, 0.55, m.glow)

    # Лёгкая виньетка — взгляд к центру.
    xs, ys = grid(0, W, 0, H)
    r2 = ((xs / W - 0.5) ** 2 + (ys / H - 0.5) ** 2) / 0.5
    img = img * (1 - 0.16 * r2)[..., None]

    srgb = np.power(np.clip(img, 0, 1), 1 / 2.2)
    big = Image.fromarray((srgb * 255 + 0.5).astype(np.uint8))
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    big.resize(size, Image.LANCZOS).save(out)
    if "--big" in sys.argv[3:]:
        big.save(os.path.splitext(out)[0] + "_big.png")
    print("saved", out)


if __name__ == "__main__":
    main()
