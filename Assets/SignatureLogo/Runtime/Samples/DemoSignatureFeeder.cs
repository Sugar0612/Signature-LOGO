using System;

using UnityEngine;

namespace SignatureLogo
{
    /// 演示组件：模拟"签名团队"——生成一批占位手写风签名 Sprite，走公开 API 喂给系统并 Start。
    /// 正式接入时删除本组件，由签名板开发者在识别完成后调用 ISignatureLogoVisualizer。
    [AddComponentMenu("SignatureLogo/Demo Signature Feeder")]
    public sealed class DemoSignatureFeeder : MonoBehaviour
    {
        [SerializeField, Min(1)] int signatureCount = 24;
        [SerializeField, Min(1f)] float demoSwitchInterval = 10f;

        ISignatureLogoVisualizer _viz;

        void Awake()
        {
            _viz = GetComponent<ISignatureLogoVisualizer>();
        }

        void Start()
        {
            if (_viz == null) return;
            _viz.AddSprites(GeneratePlaceholderSignatures(signatureCount));
            _viz.LogoSwitchInterval = demoSwitchInterval;
            _viz.Start();
        }

        Sprite[] GeneratePlaceholderSignatures(int count)
        {
            var sprites = new Sprite[count];
            for (int i = 0; i < count; i++) sprites[i] = GenerateOne(i, count);
            return sprites;
        }

        Sprite GenerateOne(int index, int count)
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            var color = Color.HSVToRGB(index / (float)count, 0.7f, 1f);
            var rng = new System.Random(index * 7919 + 17);

            // 随机 3~5 段折线笔画，模拟手写感
            float x = size * (float)(0.25 + 0.5 * rng.NextDouble());
            float y = size * (float)(0.25 + 0.5 * rng.NextDouble());
            int strokes = 3 + rng.Next(3);
            for (int s = 0; s < strokes; s++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                float length = size * (float)(0.15 + 0.25 * rng.NextDouble());
                float tx = x + Mathf.Cos(angle) * length;
                float ty = y + Mathf.Sin(angle) * length;
                int steps = Mathf.Max(1, (int)length);
                for (int t = 0; t <= steps; t++)
                {
                    float k = t / (float)steps;
                    Stamp(pixels, size, Mathf.Lerp(x, tx, k), Mathf.Lerp(y, ty, k), 2.5f, color);
                }
                x = tx;
                y = ty;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "PlaceholderSignature_" + index.ToString("00");
            return sprite;
        }

        static void Stamp(Color32[] pixels, int size, float cx, float cy, float radius, Color color)
        {
            int r = Mathf.CeilToInt(radius);
            int x0 = Mathf.RoundToInt(cx);
            int y0 = Mathf.RoundToInt(cy);
            float r2 = radius * radius;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int px = x0 + dx;
                    int py = y0 + dy;
                    if (px < 0 || py < 0 || px >= size || py >= size) continue;
                    pixels[py * size + px] = color;
                }
            }
        }
    }
}
