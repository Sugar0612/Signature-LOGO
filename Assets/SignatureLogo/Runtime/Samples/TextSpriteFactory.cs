using System.Collections.Generic;

using UnityEngine;

namespace SignatureLogo
{
    /// 把文字渲染成 Sprite 的工具：用系统/指定字体在运行时生成透明底字符图，无需图片资源。
    /// 原理：TextGenerator 排版 → 从字体图集中拷出字形像素 → 合成自己的透明底 Texture2D → Sprite.Create。
    public static class TextSpriteFactory
    {
        /// 用指定字体把一段文本渲染为一个透明底 Sprite（PPU = fontSize，字高约 1 世界单位）。
        public static Sprite CreateTextSprite(string text, Font font, int fontSize, Color color)
        {
            if (string.IsNullOrEmpty(text) || font == null) return null;

            var settings = new TextGenerationSettings
            {
                font = font,
                color = Color.white, // 字形先按白色渲染，颜色统一在输出像素上调整
                fontSize = fontSize,
                richText = false,
                scaleFactor = 1f,
                lineSpacing = 1f,
                textAnchor = TextAnchor.MiddleCenter,
                generationExtents = new Vector2(fontSize * (text.Length + 1), fontSize * 3f),
                horizontalOverflow = HorizontalWrapMode.Overflow,
                verticalOverflow = VerticalWrapMode.Overflow,
                fontStyle = FontStyle.Normal
            };

            var generator = new TextGenerator();
            if (!generator.Populate(text, settings) || generator.vertexCount == 0) return null;

            var atlas = font.material.mainTexture as Texture2D;
            if (atlas == null) return null;

            // 由生成顶点的 UV0 求字形在字体图集中的像素区域
            var minUv = new Vector2(float.MaxValue, float.MaxValue);
            var maxUv = new Vector2(float.MinValue, float.MinValue);
            var verts = generator.verts;
            for (int i = 0; i < verts.Count; i++)
            {
                minUv = Vector2.Min(minUv, verts[i].uv0);
                maxUv = Vector2.Max(maxUv, verts[i].uv0);
            }

            int x = Mathf.FloorToInt(minUv.x * atlas.width);
            int y = Mathf.FloorToInt(minUv.y * atlas.height);
            int w = Mathf.CeilToInt((maxUv.x - minUv.x) * atlas.width);
            int h = Mathf.CeilToInt((maxUv.y - minUv.y) * atlas.height);
            w = Mathf.Clamp(w, 1, atlas.width - 1);
            h = Mathf.Clamp(h, 1, atlas.height - 1);
            x = Mathf.Clamp(x, 0, atlas.width - w);
            y = Mathf.Clamp(y, 0, atlas.height - h);

            try
            {
                var glyphs = atlas.GetPixels(x, y, w, h);
                var output = new Color[glyphs.Length];
                for (int i = 0; i < glyphs.Length; i++)
                {
                    output[i] = new Color(color.r, color.g, color.b, glyphs[i].a);
                }

                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                texture.SetPixels(output);
                texture.Apply(false);

                var sprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), fontSize);
                sprite.name = "Text_" + text;
                return sprite;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SignatureLogo] 字体图集不可读，无法生成文字 Sprite：" + e.Message);
                return null;
            }
        }

        /// 批量渲染：perCharacter=true 时每个字符一个签名（自动去重、跳过空白）。
        public static Sprite[] CreateSprites(string characters, Font font, int fontSize, bool colorful, bool perCharacter = true)
        {
            if (font == null) font = CreateDefaultChineseFont(fontSize);
            var results = new List<Sprite>();

            if (perCharacter)
            {
                var seen = new HashSet<char>();
                int index = 0;
                foreach (char c in characters)
                {
                    if (char.IsWhiteSpace(c) || !seen.Add(c)) continue;
                    Color color = colorful ? Color.HSVToRGB(index % 12 / 12f, 0.6f, 1f) : Color.white;
                    var sprite = CreateTextSprite(c.ToString(), font, fontSize, color);
                    if (sprite != null) results.Add(sprite);
                    index++;
                }
            }
            else
            {
                var sprite = CreateTextSprite(characters.Trim(), font, fontSize, Color.white);
                if (sprite != null) results.Add(sprite);
            }
            return results.ToArray();
        }

        /// Windows 现场优先微软雅黑等中文字体；都找不到时退回 Unity 内置字体。
        public static Font CreateDefaultChineseFont(int fontSize)
        {
            var osFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimSun", "SimHei" }, fontSize);
            if (osFont != null) return osFont;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
