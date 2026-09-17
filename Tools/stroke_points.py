# -*- coding: utf-8 -*-
"""把采样点中心画在 mask 轮廓上（放大），直观检查"横"笔画是否笔直。"""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
from PIL import Image, ImageDraw

BASE = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'SignatureLogo', 'Samples')
OUT = os.path.join(os.path.dirname(__file__), 'previews')
os.makedirs(OUT, exist_ok=True)
ZOOM = 3  # 放大倍数

def generate(mask, w, h, cell, centroid=False):
    cx_n, cy_n = (w + cell - 1) // cell, (h + cell - 1) // cell
    px = mask.load()
    acc, cent = [], []
    for cy in range(cy_n):
        row_a, row_c = [], []
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
            row_a.append(avg >= 0.35)
            row_c.append((sx / s, sy / s) if s > 0 else (x0 + (cell - 1) * .5, y0 + (cell - 1) * .5))
        acc.append(row_a); cent.append(row_c)
    pts = []
    for cy in range(cy_n):
        for cx in range(cx_n):
            if acc[cy][cx]:
                pts.append(cent[cy][cx] if centroid else (cx * cell + (cell - 1) * .5, cy * cell + (cell - 1) * .5))
    return pts

def main():
    name = sys.argv[1] if len(sys.argv) > 1 else 'Logo_text.png'
    mask_img = Image.open(os.path.join(BASE, 'DemoLogos', name)).convert('RGBA')
    w, h = mask_img.size
    mask = mask_img.split()[3]
    for label, centroid in (('before', False), ('after', True)):
        pts = generate(mask, w, h, 3, centroid)
        # 底图：mask 半透明灰色 + 黑背景
        base = Image.new('RGBA', (w, h), (0, 0, 0, 255))
        gray = Image.new('RGBA', (w, h), (70, 70, 70, 255))
        base.paste(gray, (0, 0), mask)
        base = base.convert('RGB')
        d = ImageDraw.Draw(base)
        for (x, y) in pts:
            d.ellipse((x - .6, y - .6, x + .6, y + .6), fill=(255, 255, 90))
        base = base.resize((w * ZOOM, h * ZOOM), Image.NEAREST)
        base.save(os.path.join(OUT, 'points_%s_%s' % (label, name)))
        print(label, len(pts), '-> saved')

if __name__ == '__main__':
    main()
