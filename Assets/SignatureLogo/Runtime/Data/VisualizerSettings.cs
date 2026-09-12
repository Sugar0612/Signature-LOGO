using System;

using DG.Tweening;

using UnityEngine;

namespace SignatureLogo
{
    /// 冷启动拼接时签名的入场方式。
    public enum FormationStartPose
    {
        /// 从 Logo 左缘外弹出（平面效果）。
        LeftEdge,
        /// 从近景（相机方向、放大状态）飞入目标点（纵深感）。
        NearCamera
    }

    /// Logo Formation（左→右成形）相关配置。
    [Serializable]
    public sealed class FormationSettings
    {
        [Tooltip("签名入场方式：从相机身后飞入 / 左缘弹出（每次切换 Logo 都按此方式整体重飞）")]
        public FormationStartPose coldStartPose = FormationStartPose.NearCamera;

        [Tooltip("近景飞入：起点在相机平面后方多少世界单位（按实际相机位置自动换算）")]
        [Min(0f)] public float startBehindCamera = 1.5f;

        [Tooltip("近景飞入：起点在 Logo 四周的散布半径（Logo 对角线的倍数），让飞行轨迹清晰可见")]
        [Min(0f)] public float startSpreadRadius = 0.75f;

        [Tooltip("近景飞入：签名飞过镜头时的显示宽度（世界单位，绝对尺寸）——决定观众能否认出字符。注意：越大同时同屏的大签名越多，性能开销越大")]
        [Min(0.05f)] public float flyInWorldSize = 0.9f;

        [Tooltip("近景飞入：飞行缓动。InQuad=在镜头前停留更久更容易看清（推荐）；OutCubic=快速掠过")]
        public Ease flyInEase = Ease.InQuad;

        [Tooltip("左→右波浪总时长（秒）")]
        [Min(0.05f)] public float staggerDuration = 2.0f;

        [Tooltip("单个单位的位移时长（秒）")]
        [Min(0.05f)] public float moveDuration = 0.6f;

        [Tooltip("冷启动时单位从 Logo 左缘外多远生成（世界单位）")]
        [Min(0f)] public float startEdgeMargin = 2f;

        [Tooltip("目标点位置随机抖动（世界单位）")]
        [Min(0f)] public float positionJitter = 0.05f;

        [Tooltip("单位 Z 旋转随机抖动（±度）")]
        [Min(0f)] public float rotationJitter = 8f;

        [Tooltip("单位缩放随机抖动比例（0~1，对称）")]
        [Range(0f, 1f)] public float scaleJitter = 0.15f;

        [Tooltip("冷启动弹出时长（秒）")]
        [Min(0.05f)] public float popDuration = 0.35f;

        [Tooltip("单位位移缓动")]
        public Ease moveEase = Ease.OutCubic;

        [Tooltip("冷启动弹出缓动")]
        public Ease popEase = Ease.OutBack;
    }

    /// 签名单位渲染外观与 Logo 适配配置。
    [Serializable]
    public sealed class RenderingSettings
    {
        [Tooltip("签名基础世界缩放（配合 Sprite 的 PixelsPerUnit 折算实际尺寸）；拼贴模式下不生效")]
        [Min(0.01f)] public float baseScale = 1f;

        [Tooltip("签名着色")]
        public Color tint = Color.white;

        [Tooltip("Sorting Order")]
        [Min(0)] public int sortingOrder = 0;

        [Tooltip("每次 Formation 时缩放 LogoRoot 使 Logo 适配相机视口（宽/高取小）")]
        public bool fitToCameraHeight = true;

        [Tooltip("Logo 占视口尺寸的比例")]
        [Range(0.1f, 1f)] public float targetHeightFraction = 0.8f;
    }
}
