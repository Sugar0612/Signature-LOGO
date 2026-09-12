using System.Collections.Generic;

using UnityEngine;

namespace SignatureLogo
{
    /// Round-Robin / Modulo 分配：目标点序号 → 签名 Sprite。
    /// 目标点已按 X 排序，因此相邻目标点拿到相邻签名：张三 李四 王五 张三 李四 王五…
    public static class SpriteDistributor
    {
        /// offset 可用于不同 Logo 错开起始签名（如传 logoIndex）。
        public static Sprite Resolve(IReadOnlyList<Sprite> sprites, int pointIndex, int offset = 0)
        {
            int count = sprites.Count;
            int index = (pointIndex + offset) % count;
            if (index < 0) index += count;
            return sprites[index];
        }
    }
}
