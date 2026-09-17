# -*- coding: utf-8 -*-
"""复现 Unity 当前采样（cellSize=3, tileScale=1.3, 阈值0.35, 无桥接），
放大观察"横"笔画是否笔直，定位不直的根因。"""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
from PIL import Image

BASE = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'SignatureLogo', 'Samples')
OUT = os.path.join(os.path.dirname(__file__), 'previews')
os.makedirs(OUT, exist_ok=True)

def load_rgba(p): return Image.open(p).convert('RGBA')
SIGS = [load_rgba(os.path.join(BASE, 'Signatures', n)) for n in ('李白.png', '兰子玉.png', '李一一.png')]
SIG_AR = SIGS[0].height / SIGS[0].width

def generate_points(mask, w, h, cell, centroid=False):
    """与 LogoTargetGenerator 一致的网格采样；centroid=True 时改用格内 alpha 质心。"""
    cx_n, cy_n = (w + cell - 1) // cell, (h + cell - 1) // cell
    px = mask.load()
    acc = [[False] * cx_n for _ in range(cy_n)]
    cent = [[(0.0, 0.0)] * cx_n for _ in range(cy_n)]
    for cy in range(cy_n):
        for cx in range(cx_n):
            x0, x1 = cx * cell, min(cx * cell + cell, w)
            y0, y1 = cy * cell, min(cy * cell + cell, h)
            s = n = 0; sx = sy = 0.0
            for y in range(y0, y1):
                for x in range(x0, x1):
                    a = px[x, y]; s += a; n += 1
                    if centroid and a > 0:
                        sx += x * a; sy += y * a
            avg = s / (255.0 * n)
            acc[cy][cx] = avg >= 0.35
            if centroid and s > 0:
                cent[cy][cx] = (sx / s, sy / s)
    pts = []
    for cy in range(cy_n):
        for cx in range(cx_n):
            if not acc[cy][cx]: continue
            if centroid:
                pts.append(cent[cy][cx])
            else:
                pts.append((cx * cell + (cell - 1) * 0.5, cy * cell + (cell - 1) * 0.5))
    return pts

def simulate(mask_img, cell, ts, centroid):
    w, h = mask_img.size
    pts = generate_points(mask_img.split()[3], w, h, cell, centroid)
    sw = max(1, int(round(cell * ts))); sh = max(1, int(round(sw * SIG_AR)))
    resized = [s.resize((sw, sh), Image.LANCZOS) for s in SIGS]
    canvas = Image.new('RGBA', (w, h), (0, 0, 0, 255))
    for k, (x, y) in enumerate(pts):
        canvas.alpha_composite(resized[k % 3], (int(x - sw / 2), int(y - sh / 2)))
    return canvas, len(pts)

def main():
    mask = load_rgba(os.path.join(BASE, 'DemoLogos', 'Logo_text.png'))
    for label, centroid in (('before', False), ('after_centroid', True)):
        img, n = simulate(mask, 3, 1.3, centroid)
        img.convert('RGB').save(os.path.join(OUT, 'diag_%s_full.png' % label))
        print(label, 'points=', n)

if __name__ == '__main__':
    main()
