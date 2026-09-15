# -*- coding: utf-8 -*-
"""最终候选组合的屏幕尺度对比图（含与 Unity 一致的采样逻辑，独立版避免导入副作用）。"""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
from PIL import Image, ImageDraw, ImageFont

BASE = r'E:\ParasiticWasps\Log\Assets\SignatureLogo\Samples'
OUT = r'E:\ParasiticWasps\Log\Tools\previews'

def load_rgba(p): return Image.open(p).convert('RGBA')
SIGS = [load_rgba(os.path.join(BASE, 'Signatures', n)) for n in ('李白.png', '兰子玉.png', '李一一.png')]
SIG_AR = SIGS[0].height / SIGS[0].width

def generate_points(mask_a, w, h, cell):
    cx_n, cy_n = (w + cell - 1) // cell, (h + cell - 1) // cell
    px = mask_a.load()
    acc = [[False] * cx_n for _ in range(cy_n)]
    alpha = [[0.0] * cx_n for _ in range(cy_n)]
    for cy in range(cy_n):
        for cx in range(cx_n):
            x0, x1 = cx * cell, min(cx * cell + cell, w)
            y0, y1 = cy * cell, min(cy * cell + cell, h)
            s = n = 0
            for y in range(y0, y1):
                for x in range(x0, x1):
                    s += px[x, y]; n += 1
            alpha[cy][cx] = s / (255.0 * n)
            acc[cy][cx] = alpha[cy][cx] >= 0.35
    for _ in range(8):
        add = []
        for cy in range(cy_n):
            for cx in range(cx_n):
                if acc[cy][cx] or alpha[cy][cx] < 0.15: continue
                nb = 0
                if cx > 0 and acc[cy][cx - 1]: nb += 1
                if cx < cx_n - 1 and acc[cy][cx + 1]: nb += 1
                if cy > 0 and acc[cy - 1][cx]: nb += 1
                if cy < cy_n - 1 and acc[cy + 1][cx]: nb += 1
                if nb >= 2: add.append((cx, cy))
        for cx, cy in add: acc[cy][cx] = True
        if not add: break
    return [(cx * cell + cell // 2, cy * cell + cell // 2)
            for cy in range(cy_n) for cx in range(cx_n) if acc[cy][cx]]

def simulate(mask, cell, ts):
    w, h = mask.size
    pts = generate_points(mask.split()[3], w, h, cell)
    sw = max(1, int(cell * ts)); sh = max(1, int(sw * SIG_AR))
    resized = [s.resize((sw, sh), Image.LANCZOS) for s in SIGS]
    canvas = Image.new('RGBA', (w, h), (0, 0, 0, 255))
    for k, (x, y) in enumerate(pts):
        canvas.alpha_composite(resized[k % 3], (x - sw // 2, y - sh // 2))
    return canvas, len(pts)

def main():
    combos = [(5, 1.0), (5, 1.4), (5, 1.7), (4, 1.6), (6, 1.2), (7, 2.5)]
    scale = 2.58  # 1920 宽屏、Logo 占 90% 宽时相对剪影图的放大倍数
    masks = {'logo1': load_rgba(os.path.join(BASE, 'DemoLogos', 'Logo_text.png')),
             'logo2': load_rgba(os.path.join(BASE, 'DemoLogos', 'newlogo-removebg-preview.png'))}
    for mname, mask in masks.items():
        w, h = mask.size
        sw, sh = int(w * scale), int(h * scale)
        sheet = Image.new('RGB', (sw * 2 + 30, (sh + 34) * 3 + 10), (15, 15, 15))
        d = ImageDraw.Draw(sheet)
        try: font = ImageFont.load_default(size=22)
        except TypeError: font = ImageFont.load_default()
        for i, (c, t) in enumerate(combos):
            img, n = simulate(mask, c, t)
            img = img.resize((sw, sh), Image.LANCZOS).convert('RGB')
            col, row = i % 2, i // 2
            ox, oy = col * (sw + 30), row * (sh + 34)
            sheet.paste(img, (ox, oy + 30))
            tag = '  <-- current' if (c, t) == (7, 2.5) else ''
            d.text((ox + 4, oy + 5), 'cellSize=%d tileScale=%.1f points=%d%s' % (c, t, n, tag),
                   fill=(255, 255, 100), font=font)
        out = os.path.join(OUT, 'final_%s.png' % mname)
        sheet.save(out)
        print('saved', out)

if __name__ == '__main__':
    main()
