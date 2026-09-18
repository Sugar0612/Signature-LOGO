# -*- coding: utf-8 -*-
"""边缘平滑模拟：对比 当前(cell3) vs 细格(cell2) vs 细格+边缘抗锯齿(coverage 概率接受+缩放)。"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mosaic_final import load_rgba, SIGS, SIG_AR, BASE, OUT
from PIL import Image, ImageDraw, ImageFont

THR = 0.35
DITHER_FLOOR = 0.15
EDGE_MIN = 0.55
SMOOTH_MIN = 0.35

def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)

def gen_points(a, w, h, cell, centroid=True, edge_aa=False, seed=12345):
    import random
    rng = random.Random(seed)
    cxn, cyn = (w + cell - 1) // cell, (h + cell - 1) // cell
    px = a.load()
    acc = [[False] * cxn for _ in range(cyn)]
    cov = [[0.0] * cxn for _ in range(cyn)]
    cen = {}
    for cy in range(cyn):
        for cx in range(cxn):
            x0, x1 = cx * cell, min(cx * cell + cell, w)
            y0, y1 = cy * cell, min(cy * cell + cell, h)
            s = n = 0; sax = say = 0
            for y in range(y0, y1):
                for x in range(x0, x1):
                    v = px[x, y]; s += v; n += 1
                    if centroid: sax += x * v; say += y * v
            c = s / (255.0 * n)
            cov[cy][cx] = c
            if c >= THR:
                acc[cy][cx] = True
            elif edge_aa and c >= DITHER_FLOOR and rng.random() < c / THR:
                acc[cy][cx] = True
            if centroid and s > 0:
                cen[(cx, cy)] = (sax / s, say / s)
    pts = []
    for cy in range(cyn):
        for cx in range(cxn):
            if not acc[cy][cx]: continue
            if centroid and (cx, cy) in cen and cov[cy][cx] > 0:
                X, Y = cen[(cx, cy)]
                X = max(cx * cell, min(X, cx * cell + cell - 1))
                Y = max(cy * cell, min(Y, cy * cell + cell - 1))
            else:
                X, Y = cx * cell + cell // 2, cy * cell + cell // 2
            if edge_aa:
                c = cov[cy][cx]
                if c >= THR:
                    sc = EDGE_MIN + (1 - EDGE_MIN) * smoothstep((c - THR) / (1 - THR))
                else:
                    sc = SMOOTH_MIN + (EDGE_MIN - SMOOTH_MIN) * (c / THR)
            else:
                sc = 1.0
            pts.append((X, Y, sc))
    return pts

def sim(mask, cell, ts, centroid, edge_aa):
    w, h = mask.size
    pts = gen_points(mask.split()[3], w, h, cell, centroid, edge_aa)
    sw = max(1, int(cell * ts))
    cache = {}
    cv = Image.new('RGBA', (w, h), (0, 0, 0, 255))
    for k, (x, y, sc) in enumerate(pts):
        key = (sw, round(sc, 2))
        if key not in cache:
            s = sw * sc
            cache[key] = [g.resize((max(1, int(s)), max(1, int(s * SIG_AR))), Image.LANCZOS) for g in SIGS]
        rs = cache[key]
        img = rs[k % 3]
        cv.alpha_composite(img, (int(x - img.width / 2), int(y - img.height / 2)))
    return cv, len(pts)

def zoom(img, box, z):
    w, h = img.size
    c = img.crop((int(w * box[0]), int(h * box[1]), int(w * box[2]), int(h * box[3])))
    return c.resize((c.width * z, c.height * z), Image.LANCZOS)

def main():
    mask = load_rgba(os.path.join(BASE, 'DemoLogos', 'Logo_text.png'))
    combos = [
        (3, 1.3, True, False, 'CURRENT cell3'),
        (2, 1.3, True, False, 'cell2 only'),
        (2, 1.3, True, True, 'cell2 + edgeAA'),
        (2, 1.5, True, True, 'cell2 + edgeAA + ts1.5'),
    ]
    # 左右边缘特写（底行文字的左 22% 与右 22%）
    boxes = {'LEFT': (0.02, 0.50, 0.24, 1.00, 4), 'RIGHT': (0.76, 0.50, 0.98, 1.00, 4)}
    for side, (bx0, by0, bx1, by1, z) in boxes.items():
        tiles = [('MASK', zoom(mask.convert('RGB'), (bx0, by0, bx1, by1), z))]
        for c, t, cen, eaa, tag in combos:
            img, n = sim(mask, c, t, cen, eaa)
            tiles.append(('%s pts=%d' % (tag, n), zoom(img.convert('RGB'), (bx0, by0, bx1, by1), z)))
        cw = max(t[1].width for t in tiles); ch = max(t[1].height for t in tiles)
        sheet = Image.new('RGB', (cw + 16, (ch + 30) * len(tiles) + 8), (10, 10, 10))
        d = ImageDraw.Draw(sheet)
        try: font = ImageFont.load_default(size=20)
        except TypeError: font = ImageFont.load_default()
        y = 4
        for label, crop in tiles:
            d.text((8, y), label, fill=(120, 220, 255), font=font)
            sheet.paste(crop, (8, y + 24)); y += ch + 30
        out = os.path.join(OUT, 'edge_%s.png' % side)
        sheet.save(out); print('saved', out, sheet.size)

if __name__ == '__main__':
    main()
