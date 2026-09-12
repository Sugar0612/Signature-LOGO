using DG.Tweening;

using UnityEngine;

namespace SignatureLogo
{
    /// 池内签名单位：仅一个 SpriteRenderer + 缓存的 Transform 引用。
    /// 不挂任何 Update；所有动画由各控制器用 DOTween 驱动。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteUnit : MonoBehaviour
    {
        public SpriteRenderer Renderer { get; private set; }
        public Transform CachedTransform { get; private set; }

        /// 池在创建时一次性赋值的"缩出回池"回调（TweenCallback 类型，供 OnComplete 零分配使用）。
        public TweenCallback ReleaseCallback { get; set; }

        public void Bind(Transform tr, SpriteRenderer renderer)
        {
            CachedTransform = tr;
            Renderer = renderer;
        }

        public void SetSprite(Sprite sprite)
        {
            Renderer.sprite = sprite;
        }

        /// 直接摆出最终姿态（不做补间）。
        public void SetPose(Vector3 localPosition, float rotationZ, float uniformScale)
        {
            CachedTransform.localPosition = localPosition;
            CachedTransform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
            CachedTransform.localScale = new Vector3(uniformScale, uniformScale, 1f);
        }
    }
}
