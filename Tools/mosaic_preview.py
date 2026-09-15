# -*- coding: utf-8 -*-
"""Signature Logo 拼贴效果离线模拟器：
按 LogoTargetGenerator 的真实采样规则（网格平均 alpha≥0.35 + 桥接修复）生成目标点，
再用真实签名图按 pitch×tileScale 渲染拼贴，输出多组参数对比图。"""
import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
from PIL import Image, ImageDraw, ImageFont

BASE = r'E:\ParasiticWasps\Log\Assets\SignatureLogo\Samples'
OUT = r'E:\ParasiticWasps\Log\Tools\previews'
os.makedirs(OUT, exist_ok=True)

def load_rgba(p): return Image.open(p).convert('RGBA')

SIGS = [load_rgba(os.path.join(BASE, 'Signatures', n)) for n in ('李白.png', '兰子玉.png', '李一一.png')]
SIG_AR = SIGS[0].height / SIGS[0].width

def generate_points(mask_a, w, h, cell):
    """镜像 LogoTargetGenerator：网格平均 alpha≥0.35 接受 + 桥接修复（≥0.15 且邻居≥2）。"""
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
    pts = []
    for cy in range(cy_n):
        for cx in range(cx_n):
            if acc[cy][cx]:
                pts.append((cx * cell + cell // 2, cy * cell + cell // 2))
    return pts

def simulate(mask, cell, ts):
    w, h = mask.size
    a = mask.split()[3]
    pts = generate_points(a, w, h, cell)
    sw, sh = max(1, int(cell * ts)), 1
    sh = max(1, int(sw * SIG_AR))
    resized = [s.resize((sw, sh), Image.LANCZOS) for s in SIGS]
    canvas = Image.new('RGBA', (w, h), (0, 0, 0, 255))
    for k, (x, y) in enumerate(pts):
        canvas.alpha_composite(resized[k % 3], (x - sw // 2, y - sh // 2))
    return canvas, len(pts)

def main():
    masks = {
        'logo1': load_rgba(os.path.join(BASE, 'DemoLogos', 'Logo_text.png')),
        'logo2': load_rgba(os.path.join(BASE, 'DemoLogos', 'newlogo-removebg-preview.png')),
    }
    combos = [(5, 1.0), (5, 1.4), (4, 1.8), (6, 1.2), (7, 2.5), (9, 2.0)]
    for mname, mask in masks.items():
        sheet = Image.new('RGB', (mask.width * 2 + 30, (mask.height + 30) * 3 + 10), (20, 20, 20))
        draw = ImageDraw.Draw(sheet)
        try: font = ImageFont.load_default(size=20)
        except TypeError: font = ImageFont.load_default()
        for i, (cell, ts) in enumerate(combos):
            img, n = simulate(mask, cell, ts)
            col, row = i % 2, i // 2
            ox, oy = col * (mask.width + 30), row * (mask.height + 30)
            sheet.paste(img.convert('RGB'), (ox, oy + 28))
            draw.text((ox + 4, oy + 4), 'cellSize=%d  tileScale=%.1f  points=%d' % (cell, ts, n), fill=(255, 255, 100), font=font)
            img.convert('RGB').save(os.path.join(OUT, '%s_c%d_t%.0e.png' % (mname, cell, ts)).replace('.0e', ''), 'PNG')
            print('%s cell=%d ts=%.1f -> %d points' % (mname, cell, ts, n))
        sheet.save(os.path.join(OUT, 'sheet_%s.png' % mname), 'PNG')
        print('sheet saved:', os.path.join(OUT, 'sheet_%s.png' % mname))

if __name__ == '__main__':
    main()
