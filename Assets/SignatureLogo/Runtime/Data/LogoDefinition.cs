using System;

using UnityEngine;

namespace SignatureLogo
{
    /// 一个 Logo 的资产定义：Mask 贴图 + 采样参数 + 烘焙结果缓存。
    /// 目标点在编辑器中通过 Inspector 的"烘焙"按钮生成并序列化进本资产，运行时零纹理读取。
    [CreateAssetMenu(menuName = "SignatureLogo/Logo Definition", fileName = "LogoDefinition_")]
    public sealed class LogoDefinition : ScriptableObject
    {
        [Header("Mask")]
        [Tooltip("Logo 剪影图：alpha >= 阈值的像素区域即目标区域")]
        public Texture2D mask;

        [Tooltip("Mask 像素 → 世界单位的换算（决定 Logo 世界尺寸）")]
        [Min(1f)] public float pixelsPerUnit = 100f;

        [Header("Sampling")]
        [Tooltip("采样格边长（像素），越小点越密")]
        [Min(1f)] public float cellSize = 6f;

        [Range(0.01f, 1f)] public float alphaThreshold = 0.5f;

        [Tooltip("按格内 alpha 概率接受采样点：笔画中心更密、边缘更疏")]
        public bool densityByAlpha = true;

        [Tooltip("最多生成多少目标点；0 = 不限制")]
        [Min(0)] public int maxPoints = 0;

        [Tooltip("采样随机种子（相同种子结果可复现）")]
        public uint seed = 12345;

        [Min(0.01f)] public float pointScale = 1f;

        [Tooltip("无缝拼贴模式：目标点精确落在规则网格上（无抖动），运行时把签名宽度缩放到正好等于网格间距，实现不重叠的密铺。适合文字 Logo；此模式下 BaseScale 和各项抖动不生效")]
        public bool seamlessTiling = false;

        [Tooltip("桥接修复：补采细笔画断裂的格子。粗笔画文字 Logo 建议关闭——它会把复杂字内部的窄缝隙也封死，导致文字糊成一团")]
        public bool bridgeThinStrokes = true;

        [Header("Baked（由烘焙按钮生成，勿手改）")]
        public TargetPoint[] bakedPoints = Array.Empty<TargetPoint>();

        [Tooltip("Logo 世界宽度（mask.width / PPU），烘焙时写入")]
        public float bakedWidth;

        [Tooltip("Logo 世界高度，烘焙时写入")]
        public float bakedHeight;

        [Tooltip("烘焙时的来源签名（Mask 名称/尺寸 + 全部采样参数）。与当前不一致 = 烘焙数据过期。")]
        [HideInInspector] public string bakeSignature;

        /// 由当前 Mask 与采样参数计算签名；与 bakeSignature 不同即表示需要重新烘焙。
        /// 前缀为采样算法版本号：算法升级后旧烘焙数据自动失效（运行时自动重新生成）。
        public string ComputeBakeSignature()
        {
            if (mask == null) return null;
            return "g3|" + mask.name + "|" + mask.width + "x" + mask.height +
                   "|" + pixelsPerUnit + "|" + cellSize + "|" + alphaThreshold +
                   "|" + densityByAlpha + "|" + maxPoints + "|" + seed + "|" + pointScale +
                   "|" + seamlessTiling + "|" + (bridgeThinStrokes ? 1 : 0);
        }

        /// 拼贴模式下相邻目标点的世界间距（cellSize / PPU）。
        public float GetPointSpacing()
        {
            return cellSize / Mathf.Max(1f, pixelsPerUnit);
        }
    }
}
