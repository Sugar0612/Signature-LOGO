# -*- coding: utf-8 -*-
"""用与 LogoTargetGenerator(g4) 完全一致的算法重新烘焙 Demo Logo 资产。
参数：cellSize=3, PPU=100, 拼贴模式, 无桥接, 子格质心开启, maxPoints=0。
直接改写 .asset 的 YAML（bakedPoints/bakedWidth/bakedHeight/bakeSignature/subCellCentroid）。"""
import sys, io, os, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
import numpy as np
from PIL import Image

BASE = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'SignatureLogo', 'Samples')

CELL, PPU, THRESH = 3, 100.0, 0.35  # 拼贴模式阈值 = min(0.5, 0.35)

def f32(v):
    return float(np.float32(v))

def fmt(v):
    s = '%.7g' % v
    return s

def bake_points(mask_path):
    img = Image.open(mask_path).convert('RGBA')
    w, h = img.size
    # PIL 行序自上而下，Unity GetPixels32 自下而上：垂直翻转对齐 Unity 坐标（y=0 为底部）
    a = np.array(img)[:, :, 3][::-1, :].astype(np.int64)
    cellsX, cellsY = (w + CELL - 1) // CELL, (h + CELL - 1) // CELL
    xs = np.arange(w)[None, :]
    ys = np.arange(h)[:, None]
    pts = []
    for cy in range(cellsY):
        y0, y1 = cy * CELL, min(cy * CELL + CELL, h)
        for cx in range(cellsX):
            x0, x1 = cx * CELL, min(cx * CELL + CELL, w)
            blk = a[y0:y1, x0:x1]
            if blk.size == 0:
                continue
            avg = blk.sum() / (255.0 * blk.size)
            if avg < THRESH:
                continue
            # 子格质心（与 C# long 累计等价，int64 范围内精确）
            s = int(blk.sum())
            cxp = (blk * xs[:, x0:x1]).sum() / s
            cyp = (blk * ys[y0:y1, :]).sum() / s
            px = min(max(cxp, x0), x1 - 1)
            py = min(max(cyp, y0), y1 - 1)
            lx = f32((px - w * 0.5) / PPU)
            ly = f32((py - h * 0.5) / PPU)
            pts.append((lx, ly))
    # 排序：X 升序，X 同按 Y 降序（C# float 精确比较 → 此处用 float32 化后的值）
    pts.sort(key=lambda p: (p[0], -p[1]))
    minX = f32(min(p[0] for p in pts))
    maxX = f32(max(p[0] for p in pts))
    span = f32(maxX - minX)
    out = []
    for lx, ly in pts:
        nx = f32((lx - minX) / span) if span > 1e-5 else 0.0
        out.append((lx, ly, nx))
    return out, w, h

def rewrite_asset(asset_path, mask_name, mask_path):
    pts, w, h = bake_points(mask_path)
    sig = 'g4|%s|%dx%d|%s|%s|%s|%s|%s|%s|%s|%s|%s|%s' % (
        mask_name, w, h, '100', '3', '0.5', 'True', '0', '12345', '1', 'True', '0', '1')
    with open(asset_path, 'r', encoding='utf-8') as f:
        text = f.read()
    lines = ['  bakedPoints:']
    for lx, ly, nx in pts:
        lines.append('  - position: {x: %s, y: %s, z: 0}' % (fmt(lx), fmt(ly)))
        lines.append('    rotationZ: 0')
        lines.append('    scale: 1')
        lines.append('    normalizedX: %s' % fmt(nx))
    block = '\n'.join(lines)
    # 替换 bakedPoints 到 bakeSignature 之间的整段
    text = re.sub(r'  bakedPoints:.*?(?=  bakedWidth:)', block + '\n', text, flags=re.S)
    text = re.sub(r'  bakedWidth: .*', '  bakedWidth: %s' % fmt(f32(w / PPU)), text)
    text = re.sub(r'  bakedHeight: .*', '  bakedHeight: %s' % fmt(f32(h / PPU)), text)
    text = re.sub(r'  bakeSignature: .*', '  bakeSignature: %s' % sig, text)
    if 'subCellCentroid' not in text:
        text = text.replace('  bridgeThinStrokes: 0\n', '  bridgeThinStrokes: 0\n  subCellCentroid: 1\n')
    with open(asset_path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    print('%s: %d 点, sig=%s' % (os.path.basename(asset_path), len(pts), sig))

if __name__ == '__main__':
    rewrite_asset(os.path.join(BASE, 'DemoLogos', 'Logo01.asset'), 'Logo_text',
                  os.path.join(BASE, 'DemoLogos', 'Logo_text.png'))
    rewrite_asset(os.path.join(BASE, 'DemoLogos', 'Logo02.asset'), 'newlogo-removebg-preview',
                  os.path.join(BASE, 'DemoLogos', 'newlogo-removebg-preview.png'))
