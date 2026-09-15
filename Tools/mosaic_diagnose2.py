# -*- coding: utf-8 -*-
"""诊断 v2：只裁“健康服”，高倍放大 + 提亮，分两张图。"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mosaic_final import load_rgba, simulate, BASE, OUT  # 导入时已配置 utf-8 stdout
from PIL import Image, ImageDraw, ImageFont, ImageEnhance

BOX = (0.40, 0.50, 0.81, 1.00)  # 健康服 三字区域
ZOOM = 5

def crop_boost(img):
    w, h = img.size
    c = img.crop((int(w * BOX[0]), int(h * BOX[1]), int(w * BOX[2]), int(h * BOX[3])))
    c = c.resize((c.width * ZOOM, c.height * ZOOM), Image.LANCZOS)
    if img.mode != 'L':
        c = ImageEnhance.Brightness(c).enhance(2.2)
    return c.convert('RGB')

def sheet_for(mask, combos, path):
    tiles = [('MASK yuan tu', crop_boost(mask))]
    for c, t in combos:
        img, n = simulate(mask, c, t)
        tiles.append(('%dx%.1f  pts=%d' % (c, t, n), crop_boost(img)))
    cw = max(c.width for _, c in tiles)
    ch = max(c.height for _, c in tiles)
    sheet = Image.new('RGB', (cw + 16, (ch + 30) * len(tiles) + 8), (10, 10, 10))
    d = ImageDraw.Draw(sheet)
    try: font = ImageFont.load_default(size=20)
    except TypeError: font = ImageFont.load_default()
    y = 4
    for label, crop in tiles:
        d.text((8, y), label, fill=(120, 220, 255), font=font)
        sheet.paste(crop, (8, y + 24))
        y += ch + 30
    sheet.save(path)
    print('saved', path, sheet.size)

def main():
    mask = load_rgba(os.path.join(BASE, 'DemoLogos', 'Logo_text.png'))
    sheet_for(mask, [(5, 1.5), (5, 1.15), (4, 1.2)], os.path.join(OUT, 'diag2_a.png'))
    sheet_for(mask, [(4, 1.0), (6, 1.0), (7, 1.0)], os.path.join(OUT, 'diag2_b.png'))

if __name__ == '__main__':
    main()
