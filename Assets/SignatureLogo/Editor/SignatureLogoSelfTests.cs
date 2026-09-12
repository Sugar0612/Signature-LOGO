using System.Collections.Generic;
using System.Collections.ObjectModel;

using UnityEditor;
using UnityEngine;

namespace SignatureLogo.EditorTools
{
    /// 纯逻辑自测（不依赖 NUnit，菜单即可运行）：
    /// Tools ▸ SignatureLogo ▸ 运行逻辑自测 (Run Self-Tests)
    /// 覆盖：仓库只读/空值安全、Round-Robin 取模、Mask 采样排序与确定性、Fibonacci 球面、切换间隔钳制。
    public sealed class SignatureLogoSelfTests
    {
        int _passed;
        int _failed;

        [MenuItem("Tools/SignatureLogo/运行逻辑自测 (Run Self-Tests)")]
        public static void RunFromMenu()
        {
            new SignatureLogoSelfTests().RunAll();
        }

        void RunAll()
        {
            _passed = 0;
            _failed = 0;
            TestRepository();
            TestDistributor();
            TestGeneratorSortAndCount();
            TestGeneratorDeterministic();
            TestGeneratorOrientation();
            TestTilingGrid();
            TestTileBridge();
            TestSequenceClamp();

            string suffix = _failed > 0 ? "（见上方 LogError）" : "  ✅ 全部通过";
            Debug.Log("[SignatureLogo] 自测完成：" + _passed + " 通过，" + _failed + " 失败。" + suffix);
        }

        void Check(bool condition, string name)
        {
            if (condition)
            {
                _passed++;
            }
            else
            {
                _failed++;
                Debug.LogError("[SignatureLogo][SelfTest] " + name + " 失败");
            }
        }

        void TestRepository()
        {
            var repo = new SignatureSpriteRepository();
            var s1 = MakeSprite();
            var s2 = MakeSprite();
            repo.Add(s1);
            repo.Add(null); // null 安全
            repo.Add(s2);
            Check(repo.Count == 2, "Repository: null 被跳过");
            Check(repo.Sprites.Count == 2 && repo.Sprites[0] == s1, "Repository: 顺序与只读视图");
            Check(repo.Sprites is ReadOnlyCollection<Sprite>, "Repository: 视图为只读集合");
            repo.Clear();
            Check(repo.Count == 0 && repo.Sprites.Count == 0, "Repository: Clear");
            Check(repo.Version == 3, "Repository: Version 自增");
        }

        void TestDistributor()
        {
            var a = MakeSprite();
            var b = MakeSprite();
            var c = MakeSprite();
            var sprites = new List<Sprite> { a, b, c };
            Check(SpriteDistributor.Resolve(sprites, 0) == a &&
                  SpriteDistributor.Resolve(sprites, 1) == b &&
                  SpriteDistributor.Resolve(sprites, 2) == c &&
                  SpriteDistributor.Resolve(sprites, 3) == a &&
                  SpriteDistributor.Resolve(sprites, 7) == b,
                  "Distributor: 取模循环 张三李四王五…");
            Check(SpriteDistributor.Resolve(sprites, 0, 1) == b, "Distributor: offset 错开");
            var single = new List<Sprite> { a };
            Check(SpriteDistributor.Resolve(single, 499) == a, "Distributor: 单 Sprite 全重复");
        }

        void TestGeneratorSortAndCount()
        {
            // 64×64：左半完全不透明，右半全透明
            const int w = 64, h = 64;
            var pixels = new Color32[w * h];
            var on = new Color32(255, 255, 255, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (x < w / 2) pixels[y * w + x] = on;

            var points = LogoTargetGenerator.Generate(new LogoBakeInput
            {
                Pixels = pixels, Width = w, Height = h,
                PixelsPerUnit = 100f, CellSize = 4f, AlphaThreshold = 0.5f,
                DensityByAlpha = false, MaxPoints = 0, Seed = 7, PointScale = 1f
            });

            Check(points.Length > 0, "Generator: 采到点");
            bool allLeft = true;
            for (int i = 0; i < points.Length; i++)
                if (points[i].position.x > 0.001f) { allLeft = false; break; }
            Check(allLeft, "Generator: 只采到不透明区域");

            bool sorted = true;
            for (int i = 1; i < points.Length; i++)
                if (points[i].position.x < points[i - 1].position.x - 1e-5f) { sorted = false; break; }
            Check(sorted, "Generator: 按 X 升序");

            bool mono = true;
            for (int i = 1; i < points.Length; i++)
                if (points[i].normalizedX < points[i - 1].normalizedX - 1e-5f) { mono = false; break; }
            Check(mono, "Generator: normalizedX 单调不减");

            var limited = LogoTargetGenerator.Generate(new LogoBakeInput
            {
                Pixels = pixels, Width = w, Height = h,
                PixelsPerUnit = 100f, CellSize = 4f, AlphaThreshold = 0.5f,
                DensityByAlpha = false, MaxPoints = 10, Seed = 7, PointScale = 1f
            });
            Check(limited.Length == 10, "Generator: maxPoints 限量");
        }

        void TestGeneratorDeterministic()
        {
            const int w = 48, h = 48;
            var pixels = new Color32[w * h];
            var on = new Color32(255, 255, 255, 200);
            for (int y = 10; y < 38; y++)
                for (int x = 8; x < 40; x++)
                    pixels[y * w + x] = on;

            var input = new LogoBakeInput
            {
                Pixels = pixels, Width = w, Height = h,
                PixelsPerUnit = 50f, CellSize = 3f, AlphaThreshold = 0.5f,
                DensityByAlpha = true, MaxPoints = 40, Seed = 99, PointScale = 1f
            };
            var a = LogoTargetGenerator.Generate(in input);
            var b = LogoTargetGenerator.Generate(in input);
            bool same = a.Length == b.Length;
            if (same)
                for (int i = 0; i < a.Length; i++)
                    if (a[i].position != b[i].position) { same = false; break; }
            Check(same && a.Length > 0, "Generator: 相同种子结果可复现");
        }

        void TestGeneratorOrientation()
        {
            // GetPixels32 数据自下而上：只填充图像底部 1/4（y=0 起的行），拼出的点必须在世界下方
            const int w = 64, h = 64;
            var pixels = new Color32[w * h];
            var on = new Color32(255, 255, 255, 255);
            for (int y = 0; y < h / 4; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = on;

            var points = LogoTargetGenerator.Generate(new LogoBakeInput
            {
                Pixels = pixels, Width = w, Height = h,
                PixelsPerUnit = 100f, CellSize = 4f, AlphaThreshold = 0.5f,
                DensityByAlpha = false, MaxPoints = 0, Seed = 7, PointScale = 1f
            });

            Check(points.Length > 0, "Orientation: 采到点");
            bool allBelowCenter = true;
            for (int i = 0; i < points.Length; i++)
                if (points[i].position.y >= 0f) { allBelowCenter = false; break; }
            Check(allBelowCenter, "Orientation: 图像底部拼在世界下方（不上下颠倒）");
        }

        void TestTilingGrid()
        {
            // 拼贴模式：全不透明 Mask → 每格一点，且全部精确落在网格中心（零抖动）
            const int w = 64, h = 64, cell = 8;
            var pixels = new Color32[w * h];
            var on = new Color32(255, 255, 255, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = on;

            var points = LogoTargetGenerator.Generate(new LogoBakeInput
            {
                Pixels = pixels, Width = w, Height = h,
                PixelsPerUnit = 100f, CellSize = cell, AlphaThreshold = 0.5f,
                DensityByAlpha = false, MaxPoints = 0, Seed = 7, PointScale = 1f,
                SeamlessTiling = true
            });

            Check(points.Length == (w / cell) * (h / cell), "Tiling: 全图网格点数");

            bool onGrid = true;
            for (int i = 0; i < points.Length; i++)
            {
                float px = points[i].position.x * 100f + w * 0.5f; // 还原像素坐标
                float py = points[i].position.y * 100f + h * 0.5f;
                float mx = Mathf.Abs((px + 0.5f) % cell - cell * 0.5f);
                float my = Mathf.Abs((py + 0.5f) % cell - cell * 0.5f);
                if (mx > 0.01f || my > 0.01f) { onGrid = false; break; }
            }
            Check(onGrid, "Tiling: 采样点精确落在网格中心（零抖动）");
        }

        void TestTileBridge()
        {
            // 拼贴模式：个别覆盖不足的格子（软边缘/细笔画）应被桥接补上，不留空洞
            const int w = 96, h = 96, cell = 8; // 12×12 格
            var pixels = new Color32[w * h];
            var on = new Color32(255, 255, 255, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = on;
            for (int y = 40; y < 48; y++)
                for (int x = 40; x < 48; x++)
                    pixels[y * w + x] = new Color32(255, 255, 255, 77); // 单格平均 alpha≈0.30 < 0.40

            var points = LogoTargetGenerator.Generate(new LogoBakeInput
            {
                Pixels = pixels, Width = w, Height = h,
                PixelsPerUnit = 100f, CellSize = cell, AlphaThreshold = 0.5f,
                DensityByAlpha = false, MaxPoints = 0, Seed = 7, PointScale = 1f,
                SeamlessTiling = true
            });
            Check(points.Length == 144, "Tiling: 桥接修复补齐空洞（Logo 完整无空点）");
        }

        void TestSequenceClamp()
        {
            var seq = new LogoSequenceController { LogoSwitchInterval = -5f };
            Check(Mathf.Approximately(seq.LogoSwitchInterval, 1f), "Sequence: 非法间隔钳制为 1");
            seq = new LogoSequenceController { LogoSwitchInterval = 10f };
            Check(Mathf.Approximately(seq.LogoSwitchInterval, 10f), "Sequence: 正常间隔保留");
        }

        Sprite MakeSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            DestroyQueue.Enqueue(tex);
            DestroyQueue.Enqueue(sprite);
            return sprite;
        }

        static readonly Queue<Object> DestroyQueue = new Queue<Object>();

        [MenuItem("Tools/SignatureLogo/清理自测临时对象")]
        static void Cleanup()
        {
            while (DestroyQueue.Count > 0)
            {
                var obj = DestroyQueue.Dequeue();
                if (obj != null) Object.DestroyImmediate(obj);
            }
        }
    }
}
