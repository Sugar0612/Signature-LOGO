# -*- coding: utf-8 -*-
"""定点诊断：logo1“专”横折钩 + logo2 右下英文 的笔画覆盖率测量与阈值扫描。"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mosaic_g6 import sim, zoom, smoothstep, DITHER_FLOOR, EDGE_MIN, SMOOTH_MIN
from mosaic_final import load_rgba, BASE, OUT
from PIL import Image, ImageDraw, ImageFont

def cell_stats(a, cell):
    w, h = a.size
    px = a.load()
    cyn = (h + cell - 1) // cell
    cxn = (w + cell - 1) // cell
    cov = [[0.0] * cxn for _ in range(cyn)]
    for cy in range(cyn):
        for cx in range(cxn):
            x0, x1 = cx * cell, min(cx * cell + cell, w)
            y0, y1 = cy * cell, min(cy * cell + cell, h)
            s = n = 0
            for y in range(y0, y1):
                for x in range(x0, x1):
                    s += px[x, y]; n += 1
            cov[cy][cx] = s / (255.0 * n)
    return cov

def region_hist(mask, box, cell, label):
    w, h = mask.size
    x0, y0, x1, y1 = int(w*box[0]), int(h*box[1]), int(w*box[2]), int(h*box[3])
    cov = cell_stats(mask.split()[3], cell)
    cy0, cy1 = y0//cell, (y1+cell-1)//cell
    cx0, cx1 = x0//cell, (x1+cell-1)//cell
    r = []
    for row in cov[cy0:cy1]:
        for v in row[cx0:cx1]:
            if v > 0.02: r.append(v)
    bands = [(0.02,0.1),(0.1,0.15),(0.15,0.2),(0.2,0.25),(0.25,0.3),(0.3,0.35),(0.35,0.5),(0.5,0.8),(0.8,1.01)]
    out = ['%s coverage dist (non-empty cells):' % label]
    for lo, hi in bands:
        n = sum(1 for v in r if lo <= v < hi)
        if n: out.append('  %.2f-%.2f: %d' % (lo, hi, n))
    print('\n'.join(out))

def sheet(mask, thr_list, cell, ts, box, z, out):
    tiles = [('MASK', zoom(mask.convert('RGB'), box, z))]
    for thr in thr_list:
        img, n = sim(mask, cell, ts, thr, True)
        tiles.append(('thr=%.2f pts=%d' % (thr, n), zoom(img.convert('RGB'), box, z)))
    cw = max(t.width for _, t in tiles); ch = max(t.height for _, t in tiles)
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

    # 覆盖率测量
    zhuan = (0.02, 0.02, 0.20, 0.55)     # “专”字区域（顶行左一）
    jkf   = (0.55, 0.55, 0.75, 0.98)     # “健康服”区域（对照：缝隙不能被封）
    region_hist(logo1, zhuan, 2, 'logo1 专')
    region_hist(logo1, jkf, 2, 'logo1 健康服(对照)')
    region_hist(logo2, (0.55, 0.72, 1.00, 1.00), 2, 'logo2 右下英文')

    # 阈值扫描
    sheet(logo1, [0.35, 0.22, 0.15], 2, 1.3, zhuan, 5, os.path.join(OUT, 'gap_zhuan.png'))
    sheet(logo1, [0.35, 0.22, 0.15], 2, 1.3, jkf, 5, os.path.join(OUT, 'gap_jkf_check.png'))
    sheet(logo2, [0.2, 0.14, 0.10], 2, 1.3, (0.55, 0.72, 1.00, 1.00), 5, os.path.join(OUT, 'gap_en2.png'))

if __name__ == '__main__':
    main()
