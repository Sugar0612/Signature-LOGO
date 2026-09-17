# -*- coding: utf-8 -*-
"""量化对比：采样点重建的笔画边缘 vs mask 真实边缘的平直度。
思路：对 mask 的每一条水平笔画（长横），取其上/下边缘的真实轨迹（亚像素），
再看"before/after"两组采样点在同一行上形成的边界轨迹，计算两者随 X 的偏差标准差。"""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
import numpy as np
from PIL import Image

BASE = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'SignatureLogo', 'Samples')

def gen_points(a, cell, centroid):
    h, w = a.shape
    cx_n, cy_n = (w + cell - 1) // cell, (h + cell - 1) // cell
    acc = np.zeros((cy_n, cx_n), bool)
    cents = np.zeros((cy_n, cx_n, 2))
    for cy in range(cy_n):
        for cx in range(cx_n):
            blk = a[cy*cell:(cy+1)*cell, cx*cell:(cx+1)*cell].astype(np.float64)
            if blk.size == 0:
                continue
            avg = blk.mean()
            acc[cy, cx] = avg >= 0.35
            if centroid:
                ys, xs = np.mgrid[cy*cell:min((cy+1)*cell,h), cx*cell:min((cx+1)*cell,w)]
                wsum = blk.sum()
                if wsum > 0:
                    cents[cy, cx] = ((blk*xs).sum()/wsum, (blk*ys).sum()/wsum)
                else:
                    cents[cy, cx] = (cx*cell+(cell-1)/2, cy*cell+(cell-1)/2)
    pts = []
    for cy in range(cy_n):
        for cx in range(cx_n):
            if acc[cy, cx]:
                if centroid:
                    pts.append(cents[cy, cx])
                else:
                    pts.append((cx*cell+(cell-1)/2, cy*cell+(cell-1)/2))
    return np.array(pts)

def analyze(name):
    img = Image.open(os.path.join(BASE, 'DemoLogos', name)).convert('RGBA')
    a = np.array(img)[:, :, 3].astype(np.float64) / 255.0
    h, w = a.shape
    # 用高分辨率 alpha 的重心行轨迹找长横：某像素行的覆盖率 > 0.5 的连续段超过 25px
    rows = []
    for y in range(h):
        cov = a[y] > 0.5
        # 最长连续段
        best = cur = 0; s = e = 0; cs = 0
        for x in range(w):
            if cov[x]:
                if cur == 0: cs = x
                cur += 1
                if cur > best: best, s, e = cur, cs, x
            else: cur = 0
        if best >= 25:
            rows.append((y, s, e, best))
    # 合并相邻行（同一条横取中间行）
    merged = []
    for r in rows:
        if merged and r[0] - merged[-1][0] <= 2:
            merged[-1] = r  # 取更靠下的一行近似
        else:
            merged.append(list(r))
    print('%s: 找到 %d 条候选长横' % (name, len(merged)))
    results = {'before': [], 'after': []}
    pts_before = gen_points(a, 3, False)
    pts_after = gen_points(a, 3, True)
    for (y, s, e, ln) in merged[:14]:
        # mask 真实上边缘亚像素轨迹（沿 x 每列首个 alpha>0.3 的 y）
        xs = np.arange(s, e + 1)
        edge = []
        for x in xs:
            col = np.where(a[:, x] > 0.3)[0]
            edge.append(col.min() if len(col) else np.nan)
        edge = np.array(edge, float)
        good = ~np.isnan(edge)
        if good.sum() < 10: continue
        # 真实边缘的直线拟合残差（mask 本身直不直）
        p = np.polyfit(xs[good], edge[good], 1)
        mask_dev = np.std(edge[good] - np.polyval(p, xs[good]))
        def band_dev(pts):
            # 该横附近（y±6）的采样点，按 X 分桶后取最上方点的轨迹，量直线残差
            sel = pts[(np.abs(pts[:,1] - y) < 7) & (pts[:,0] >= s) & (pts[:,0] <= e)]
            if len(sel) < 8: return None
            bx = ((sel[:,0] - s) / 3).astype(int)
            tops = []
            for b in range(bx.max() + 1):
                m = bx == b
                if m.any(): tops.append((sel[m,0].mean(), sel[m,1].min()))
            tops = np.array(tops)
            pp = np.polyfit(tops[:,0], tops[:,1], 1)
            return np.std(tops[:,1] - np.polyval(pp, tops[:,0]))
        db, da = band_dev(pts_before), band_dev(pts_after)
        if db is None or da is None: continue
        results['before'].append(db); results['after'].append(da)
        print('  横@y=%3d len=%3d  mask自身残差=%.2fpx  采样点边缘残差 before=%.2f after=%.2f'
              % (y, ln, mask_dev, db, da))
    for k in results:
        v = results[k]
        if v: print('%s 平均边缘直线残差: %.3f px' % (k, sum(v)/len(v)))

if __name__ == '__main__':
    analyze('Logo_text.png')
    analyze('newlogo-removebg-preview.png')
