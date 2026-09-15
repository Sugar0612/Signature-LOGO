# -*- coding: utf-8 -*-
"""诊断“健康服”三字：放大对比不同参数下该区域的清晰度。"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mosaic_final import load_rgba, simulate, BASE, OUT  # 导入时已配置 utf-8 stdout
from PIL import Image, ImageDraw, ImageFont

def crop_bottom(img, scale=3):
    """裁出底行“为人类健康服务”的右半段（健康服），放大 scale 倍。"""
    w, h = img.size
    box = (int(w * 0.44), int(h * 0.50), int(w * 0.95), h)  # 覆盖 健康服 + 两侧参照
    c = img.crop(box)
    return c.resize((c.width * scale, c.height * scale), Image.LANCZOS)

def main():
    mask = load_rgba(os.path.join(BASE, 'DemoLogos', 'Logo_text.png'))
    combos = [
        (5, 1.5, 'current'),
        (5, 1.2, ''),
        (4, 1.3, ''),
        (4, 1.5, ''),
        (6, 1.0, ''),
    ]
    crops = [('MASK (yuan tu)', crop_bottom(mask))]
    for c, t, tag in combos:
        img, n = simulate(mask, c, t)
        crop = crop_bottom(img.convert('RGB'))
        crops.append(('cell=%d ts=%.1f pts=%d %s' % (c, t, n, tag), crop))

    cw = max(c.width for _, c in crops)
    ch = max(c.height for _, c in crops)
    sheet = Image.new('RGB', (cw + 20, (ch + 36) * len(crops) + 10), (18, 18, 18))
    d = ImageDraw.Draw(sheet)
    try: font = ImageFont.load_default(size=22)
    except TypeError: font = ImageFont.load_default()
    y = 5
    for label, crop in crops:
        d.text((8, y), label, fill=(255, 255, 100), font=font)
        sheet.paste(crop, (10, y + 28))
        y += ch + 36
    out = os.path.join(OUT, 'diag_jiankangfu.png')
    sheet.save(out)
    print('saved', out, '(%dx%d)' % sheet.size)

if __name__ == '__main__':
    main()
