# -*- coding: utf-8 -*-
"""g6 候选验证：边缘缩放仅作用于真边界格（内部全覆盖格不缩），logo2 低阈值补英文细笔。"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mosaic_final import load_rgba, SIGS, SIG_AR, BASE, OUT
from PIL import Image, ImageDraw, ImageFont

DITHER_FLOOR = 0.15
EDGE_MIN = 0.55
SMOOTH_MIN = 0.35

def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)

def gen_points(a, w, h, cell, thr, boundary_only, seed=12345):
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
                    sax += x * v; say += y * v
            c = s / (255.0 * n)
            cov[cy][cx] = c
            if c >= thr: acc[cy][cx] = True
            elif c >= DITHER_FLOOR and rng.random() < c / thr: acc[cy][cx] = True
            if s > 0: cen[(cx, cy)] = (sax / s, say / s)
    def is_edge(cx, cy):
        for dx, dy in ((1,0),(-1,0),(0,1),(0,-1)):
            nx, ny = cx+dx, cy+dy
            if nx < 0 or ny < 0 or nx >= cxn or ny >= cyn: return True
            if not acc[ny][nx]: return True
        return False
    pts = []
    for cy in range(cyn):
        for cx in range(cxn):
            if not acc[cy][cx]: continue
            X, Y = cen.get((cx, cy), (cx*cell+cell//2, cy*cell+cell//2))
            X = max(cx*cell, min(X, cx*cell+cell-1)); Y = max(cy*cell, min(Y, cy*cell+cell-1))
            c = cov[cy][cx]
            if c >= thr:
                sc = EDGE_MIN + (1-EDGE_MIN)*smoothstep((c-thr)/(1-thr))
            else:
                sc = SMOOTH_MIN + (EDGE_MIN-SMOOTH_MIN)*(c/thr)
            if boundary_only and not is_edge(cx, cy): sc = 1.0
            pts.append((X, Y, sc))
    return pts

def sim(mask, cell, ts, thr, boundary_only):
    w, h = mask.size
    pts = gen_points(mask.split()[3], w, h, cell, thr, boundary_only)
    sw = max(1, int(cell * ts))
    cache = {}
    cv = Image.new('RGBA', (w, h), (0, 0, 0, 255))
    for k, (x, y, sc) in enumerate(pts):
        key = (sw, round(sc, 2))
        if key not in cache:
            s = sw * sc
            cache[key] = [g.resize((max(1, int(s)), max(1, int(s * SIG_AR))), Image.LANCZOS) for g in SIGS]
        img = cache[key][k % 3]
        cv.alpha_composite(img, (int(x - img.width / 2), int(y - img.height / 2)))
    return cv, len(pts)

def zoom(img, box, z):
    w, h = img.size
    c = img.crop((int(w*box[0]), int(h*box[1]), int(w*box[2]), int(h*box[3])))
    return c.resize((c.width*z, c.height*z), Image.LANCZOS)

def sheet(mask, combos, box, z, out):
    tiles = [('MASK', zoom(mask.convert('RGB'), box, z))]
    for c, t, thr, bo, tag in combos:
        img, n = sim(mask, c, t, thr, bo)
        tiles.append(('%s pts=%d' % (tag, n), zoom(img.convert('RGB'), box, z)))
    cw = max(x.width for _, x in tiles); ch = max(x.height for _, x in tiles)
    sh = Image.new('RGB', (cw+16, (ch+30)*len(tiles)+8), (10,10,10))
    d = ImageDraw.Draw(sh)
    try: font = ImageFont.load_default(size=20)
    except TypeError: font = ImageFont.load_default()
    y = 4
    for label, crop in tiles:
        d.text((8, y), label, fill=(120,220,255), font=font)
        sh.paste(crop, (8, y+24)); y += ch+30
    sh.save(out); print('saved', out, sh.size)

def main():
    logo1 = load_rgba(os.path.join(BASE, 'DemoLogos', 'Logo_text.png'))
    logo2 = load_rgba(os.path.join(BASE, 'DemoLogos', 'newlogo-removebg-preview.png'))
    # 1) logo1 内部+边缘：g5全格缩放 vs g6仅边界缩放 vs +tileScale1.4
    combos1 = [
        (2, 1.3, 0.35, False, 'g5 ALL-cell scale (current)'),
        (2, 1.3, 0.35, True,  'g6 boundary-only'),
        (2, 1.4, 0.35, True,  'g6 boundary + ts1.4'),
    ]
    sheet(logo1, combos1, (0.30, 0.52, 0.70, 1.00), 4, os.path.join(OUT, 'g6_interior.png'))   # 人类健康服 中段
    sheet(logo1, combos1, (0.02, 0.50, 0.26, 1.00), 4, os.path.join(OUT, 'g6_edge.png'))       # 左缘 专
    # 2) logo2 英文细笔：阈值 0.35 vs 0.20
    combos2 = [
        (2, 1.3, 0.35, True, 'thr0.35 (current)'),
        (2, 1.3, 0.25, True, 'thr0.25'),
        (2, 1.3, 0.20, True, 'thr0.20'),
    ]
    sheet(logo2, combos2, (0.30, 0.62, 0.98, 1.00), 4, os.path.join(OUT, 'g6_english.png'))    # 英文条带

if __name__ == '__main__':
    main()
