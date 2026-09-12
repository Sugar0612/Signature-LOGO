using System;

using UnityEngine;

namespace SignatureLogo
{
    /// 一个 Signature Sprite 在 Logo 中的最终落点（LogoRoot 本地空间）。
    [Serializable]
    public struct TargetPoint
    {
        /// LogoRoot 本地坐标，Z 恒为 0。
        public Vector3 position;

        /// 单位 Z 轴旋转（度）。
        public float rotationZ;

        /// 相对 RenderingSettings.baseScale 的倍率。
        public float scale;

        /// 0 = 最左，1 = 最右。Formation 按它排波次延迟。
        public float normalizedX;
    }
}
