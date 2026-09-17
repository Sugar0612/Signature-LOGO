# -*- coding: utf-8 -*-
"""沿具体"横"笔画打印边缘轨迹：mask 亚像素边缘 vs before/after 采样点形成的边界。"""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
import numpy as np
from PIL import Image

BASE = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'SignatureLogo', 'Samples')

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

def trace(name, y0, x0, x1):
    img = Image.open(os.path.join(BASE, 'DemoLogos', name)).convert('RGBA')
    a = np.array(img)[:, :, 3].astype(np.float64) / 255.0
    pts_b = gen_points(a, 3, False)
    pts_a = gen_points(a, 3, True)
    def tops(pts, label):
        sel = pts[(np.abs(pts[:,1] - y0) < 8) & (pts[:,0] >= x0) & (pts[:,0] <= x1)]
        bx = ((sel[:,0] - x0) / 3).astype(int)
        out = []
        for b in range(int(bx.max()) + 1):
            m = bx == b
            if m.any():
                out.append(round(float(sel[m,1].min()), 1))
        print('%-7s top边:' % label, out)
    # mask 亚像素上边缘（列 x 的 alpha 加权首接触 y）
    edge = []
    for x in range(x0, x1 + 1):
        col = a[:, x]
        idx = np.where(col > 0.3)[0]
        if len(idx):
            k = idx.min()
            # 亚像素：用 k-1,k 两点线性插值到 0.3
            y_sub = k - 0.0 if k == 0 else k - max(0.0, (0.3 - col[k-1]) / max(1e-6, col[k] - col[k-1])) * 0
            edge.append(round(float(k), 1))
        else:
            edge.append(None)
    print('%s 横@y=%d x∈[%d,%d]' % (name, y0, x0, x1))
    print('mask 边缘:', [e if e is not None else '-' for e in edge])
    tops(pts_b, 'before')
    tops(pts_a, 'after')

if __name__ == '__main__':
    trace('Logo_text.png', 152, 200, 260)
    trace('newlogo-removebg-preview.png', 264, 200, 280)
