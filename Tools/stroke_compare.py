# -*- coding: utf-8 -*-
"""渲染当前算法(before)与子格质心(after)的拼贴成品对比图：
1) 整图对比 2) 单字放大裁剪对比。"""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
import numpy as np
from PIL import Image, ImageDraw

BASE = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'SignatureLogo', 'Samples')
OUT = os.path.join(os.path.dirname(__file__), 'previews')
os.makedirs(OUT, exist_ok=True)

def load_rgba(p): return Image.open(p).convert('RGBA')
SIGS = [load_rgba(os.path.join(BASE, 'Signatures', n)) for n in ('李白.png', '兰子玉.png', '李一一.png')]
SIG_AR = SIGS[0].height / SIGS[0].width

def gen_points(a, cell, centroid):
    h, w = a.shape
    cx_n, cy_n = (w + cell - 1) // cell, (h + cell - 1) // cell
    pts = []
    for cy in range(cy_n):
        for cx in range(cx_n):
            blk = a[cy*cell:(cy+1)*cell, cx*cell:(cx+1)*cell]
            if blk.size == 0: continue
            avg = blk.mean()
            if avg < 0.35: continue
            if centroid:
                ys, xs = np.mgrid[cy*cell:min((cy+1)*cell, h), cx*cell:min((cx+1)*cell, w)]
                wsum = blk.sum()
                pts.append(((blk*xs).sum()/wsum, (blk*ys).sum()/wsum) if wsum > 0
                           else (cx*cell+(cell-1)/2, cy*cell+(cell-1)/2))
            else:
                pts.append((cx*cell+(cell-1)/2, cy*cell+(cell-1)/2))
    return np.array(pts)

def mosaic(a, pts, cell, ts):
    h, w = a.shape
    sw = max(1, int(round(cell * ts))); sh = max(1, int(round(sw * SIG_AR)))
    resized = [s.resize((sw, sh), Image.LANCZOS) for s in SIGS]
    canvas = Image.new('RGB', (w, h), (0, 0, 0))
    for k, (x, y) in enumerate(pts):
        canvas.paste(resized[k % 3], (int(x - sw / 2), int(y - sh / 2)), resized[k % 3])
    return canvas

def char_boxes(a):
    """按 x 投影聚类找字符框"""
    h, w = a.shape
    proj = (a > 0.5).sum(axis=0)
    boxes, s = [], None
    for x in range(w):
        if proj[x] > 0 and s is None: s = x
        if proj[x] == 0 and s is not None:
            if x - s > 15: boxes.append((s, x))
            s = None
    if s is not None and w - s > 15: boxes.append((s, w))
    return boxes

def main():
    name = sys.argv[1] if len(sys.argv) > 1 else 'Logo_text.png'
    img = load_rgba(os.path.join(BASE, 'DemoLogos', name))
    a = np.array(img)[:, :, 3].astype(np.float64) / 255.0
    h, w = a.shape
    pb, pa = gen_points(a, 3, False), gen_points(a, 3, True)
    mb, ma = mosaic(a, pb, 3, 1.3), mosaic(a, pa, 3, 1.3)
    # 整图对比（上before 下after）
    sheet = Image.new('RGB', (w, h * 2 + 26), (20, 20, 20))
    d = ImageDraw.Draw(sheet)
    sheet.paste(mb, (0, 13)); sheet.paste(ma, (0, h + 26))
    d.text((4, 0), 'BEFORE (cell-center)', fill=(255, 255, 100))
    d.text((4, h + 13), 'AFTER (alpha centroid)', fill=(100, 255, 100))
    sheet = sheet.resize((w * 2, (h * 2 + 26) * 2), Image.LANCZOS)
    sheet.save(os.path.join(OUT, 'cmp_full_%s' % name))
    print('points before=%d after=%d -> cmp_full_%s' % (len(pb), len(pa), name))
    # 单字放大对比
    boxes = char_boxes(a)
    print('char boxes:', boxes)
    ZOOM = 4
    for i, (x0, x1) in enumerate(boxes):
        if x1 - x0 < 30: continue
        cw = x1 - x0
        crop_b = mb.crop((x0, 0, x1, h)).resize((cw * ZOOM, h * ZOOM), Image.LANCZOS)
        crop_a = ma.crop((x0, 0, x1, h)).resize((cw * ZOOM, h * ZOOM), Image.LANCZOS)
        csheet = Image.new('RGB', (cw * ZOOM, h * ZOOM * 2 + 8), (20, 20, 20))
        csheet.paste(crop_b, (0, 0)); csheet.paste(crop_a, (0, h * ZOOM + 8))
        csheet.save(os.path.join(OUT, 'cmp_char%d_%s' % (i, name)))
    print('char crops saved')

if __name__ == '__main__':
    main()
