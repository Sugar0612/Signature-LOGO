using System;

using UnityEngine;

namespace SignatureLogo
{
    /// Logo Mask 采样输入（纯数据，编辑器烘焙与运行时兜底共用）。
    public struct LogoBakeInput
    {
        public string SourceName;
        public bool SeamlessTiling;
        public bool BridgeThinStrokes;
        public Color32[] Pixels;
        public int Width;
        public int Height;
        public float PixelsPerUnit;
        public float CellSize;
        public float AlphaThreshold;
        public bool DensityByAlpha;
        public int MaxPoints;
        public int Seed;
        public float PointScale;
    }

    /// Logo Mask → TargetPoint[] 的纯函数生成器。
    /// 算法：网格步进采样 + alpha 概率加权 + 种子抖动 →（可选洗牌限量）→ 本地坐标 → 按 X 排序 → 归一化 normalizedX。
    public static class LogoTargetGenerator
    {
    public static TargetPoint[] Generate(
        Texture2D mask, float pixelsPerUnit, float cellSize, float alphaThreshold,
        bool densityByAlpha, int maxPoints, int seed, float pointScale, bool seamlessTiling = false,
        bool bridgeThinStrokes = true)
    {
        if (mask == null) return Array.Empty<TargetPoint>();
        var input = new LogoBakeInput
        {
            SourceName = mask.name,
            SeamlessTiling = seamlessTiling,
            BridgeThinStrokes = bridgeThinStrokes,
            Pixels = GetReadablePixels32(mask),
            Width = mask.width,
            Height = mask.height,
            PixelsPerUnit = pixelsPerUnit,
            CellSize = cellSize,
            AlphaThreshold = alphaThreshold,
            DensityByAlpha = densityByAlpha,
            MaxPoints = maxPoints,
            Seed = seed,
            PointScale = pointScale
        };
        return Generate(in input);
    }

        public static TargetPoint[] Generate(in LogoBakeInput input)
        {
            int w = input.Width, h = input.Height;
            if (input.Pixels == null || w <= 0 || h <= 0) return Array.Empty<TargetPoint>();

            var rng = new System.Random(input.Seed);
            int cell = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1f, input.CellSize)));
            int cellsX = (w + cell - 1) / cell;
            int cellsY = (h + cell - 1) / cell;
            int estimated = cellsX * cellsY / 4 + 16;
            var candidateX = new System.Collections.Generic.List<float>(estimated);
            var candidateY = new System.Collections.Generic.List<float>(estimated);

            // Pass 1：逐格计算平均 alpha 并做基础接受判定
            var alphaGrid = new float[cellsX * cellsY];
            var accepted = new bool[cellsX * cellsY];
            // 拼贴模式用不高于 0.35 的判定阈值：细笔画/软边缘格子不再漏采（完整性优先）
            float baseThreshold = input.SeamlessTiling ? Mathf.Min(input.AlphaThreshold, 0.35f) : input.AlphaThreshold;
            for (int cy = 0; cy < cellsY; cy++)
            {
                int y0 = cy * cell;
                int y1 = Mathf.Min(y0 + cell, h);
                for (int cx = 0; cx < cellsX; cx++)
                {
                    int x0 = cx * cell;
                    int x1 = Mathf.Min(x0 + cell, w);
                    int sum = 0, n = 0;
                    for (int y = y0; y < y1; y++)
                    {
                        int row = y * w;
                        for (int x = x0; x < x1; x++) { sum += input.Pixels[row + x].a; n++; }
                    }
                    int idx = cy * cellsX + cx;
                    alphaGrid[idx] = n > 0 ? sum / (255f * n) : 0f;
                    bool ok = alphaGrid[idx] >= baseThreshold;
                    // 散点模式：alpha 加权接受（笔画中心必收，边缘按概率）；拼贴模式强制全收保证密铺
                    if (ok && !input.SeamlessTiling && input.DensityByAlpha && alphaGrid[idx] < 0.999f && rng.NextDouble() > alphaGrid[idx]) ok = false;
                    accepted[idx] = ok;
                }
            }

            // Pass 2（拼贴模式）：桥接修复——覆盖不足但邻居已占的格子补上，迭代至稳定，消除细笔画断裂与空洞。
            // 注意：粗笔画 Logo 应关闭（BridgeThinStrokes=false），否则复杂字内部的窄缝隙会被封死导致文字糊掉。
            if (input.SeamlessTiling && input.BridgeThinStrokes)
            {
                const float BridgeFloor = 0.15f;
                for (int pass = 0; pass < 8; pass++)
                {
                    var additions = new System.Collections.Generic.List<int>();
                    for (int cy = 0; cy < cellsY; cy++)
                    {
                        for (int cx = 0; cx < cellsX; cx++)
                        {
                            int idx = cy * cellsX + cx;
                            if (accepted[idx] || alphaGrid[idx] < BridgeFloor) continue;
                            int neighbors = 0;
                            if (cx > 0 && accepted[idx - 1]) neighbors++;
                            if (cx < cellsX - 1 && accepted[idx + 1]) neighbors++;
                            if (cy > 0 && accepted[idx - cellsX]) neighbors++;
                            if (cy < cellsY - 1 && accepted[idx + cellsX]) neighbors++;
                            if (neighbors >= 2) additions.Add(idx);
                        }
                    }
                    for (int i = 0; i < additions.Count; i++) accepted[additions[i]] = true;
                    if (additions.Count == 0) break;
                }
            }

            // Pass 3：收集候选点（格中心 + 抖动；拼贴模式零抖动，精确落在网格中心）
            for (int cy = 0; cy < cellsY; cy++)
            {
                int y0 = cy * cell;
                int y1 = Mathf.Min(y0 + cell, h);
                for (int cx = 0; cx < cellsX; cx++)
                {
                    if (!accepted[cy * cellsX + cx]) continue;
                    int x0 = cx * cell;
                    int x1 = Mathf.Min(x0 + cell, w);
                    float centerX = (x0 + x1 - 1) * 0.5f;
                    float centerY = (y0 + y1 - 1) * 0.5f;
                    float half = input.SeamlessTiling ? 0f : cell * 0.5f;
                    candidateX.Add(Mathf.Clamp(centerX + (float)(rng.NextDouble() * 2.0 - 1.0) * half, 0f, w - 1f));
                    candidateY.Add(Mathf.Clamp(centerY + (float)(rng.NextDouble() * 2.0 - 1.0) * half, 0f, h - 1f));
                }
            }

            int count = candidateX.Count;
            if (count == 0) return Array.Empty<TargetPoint>();

            // 覆盖率自检：几乎铺满全部网格，说明 Mask 很可能不是透明底/镂空剪影
            int totalCells = cellsX * cellsY;
            if (count > totalCells * 0.85f)
            {
                Debug.LogWarning(
                    "[SignatureLogo] Mask '" + input.SourceName + "' 的采样点覆盖了 " +
                    Mathf.RoundToInt(100f * count / totalCells) + "% 的网格——它很可能不是透明底/镂空剪影图，" +
                    "拼出来会是一整块矩形。请检查图片背景的 alpha 是否为 0。");
            }

            // 2) 指定数量：Fisher–Yates 洗牌取前 N（种子可复现）
            if (input.MaxPoints > 0 && count > input.MaxPoints)
            {
                for (int i = count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (candidateX[i], candidateX[j]) = (candidateX[j], candidateX[i]);
                    (candidateY[i], candidateY[j]) = (candidateY[j], candidateY[i]);
                }
                count = input.MaxPoints;
            }

            // 3) 像素 → LogoRoot 本地坐标（原点在 Logo 中心）。
            //    GetPixels32 的数据是"自下而上"的：y=0 是图像底部 → 本地 Y 也应在下方。
            float ppu = Mathf.Max(1f, input.PixelsPerUnit);
            float minX = float.MaxValue, maxX = float.MinValue;
            var points = new System.Collections.Generic.List<TargetPoint>(count);
            for (int i = 0; i < count; i++)
            {
                float lx = (candidateX[i] - w * 0.5f) / ppu;
                float ly = (candidateY[i] - h * 0.5f) / ppu;
                if (lx < minX) minX = lx;
                if (lx > maxX) maxX = lx;
                points.Add(new TargetPoint
                {
                    position = new Vector3(lx, ly, 0f),
                    rotationZ = 0f,
                    scale = input.PointScale,
                    normalizedX = 0f
                });
            }

            // 4) 左→右排序；同列自上而下（Y 降序）
            points.Sort(CompareByXThenTopDown);

            // 5) 归一化 X（Formation 波次用）
            float span = maxX - minX;
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                p.normalizedX = span > 1e-5f ? (p.position.x - minX) / span : 0f;
                points[i] = p;
            }
            return points.ToArray();
        }

        static int CompareByXThenTopDown(TargetPoint a, TargetPoint b)
        {
            if (a.position.x != b.position.x) return a.position.x.CompareTo(b.position.x);
            return b.position.y.CompareTo(a.position.y);
        }

        /// 编辑器/运行时通用：贴图不可读时经 RenderTexture 回读（无需 Read/Write 常开）。
        public static Color32[] GetReadablePixels32(Texture2D texture)
        {
            try
            {
                return texture.GetPixels32();
            }
            catch (Exception)
            {
                // 不可读贴图实际抛 ArgumentException 而非 UnityException，这里统一走回退
            }

            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
            readable.Apply(false, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            var pixels = readable.GetPixels32();
            if (Application.isPlaying) UnityEngine.Object.Destroy(readable);
            else UnityEngine.Object.DestroyImmediate(readable);
            return pixels;
        }
    }
}
