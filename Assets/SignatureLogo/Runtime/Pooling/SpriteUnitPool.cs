using System.Collections.Generic;

using DG.Tweening;

using UnityEngine;

namespace SignatureLogo
{
    /// 预热式对象池：Awake 预热后，运行时 Lease 命中缓存即零 Instantiate；
    /// Release 仅 SetActive(false)，永不 Destroy。超预热自动扩容兜底。
    public sealed class SpriteUnitPool
    {
        readonly Transform _parent;
        readonly RenderingSettings _rendering;
        readonly Material _unitMaterial;
        readonly Stack<SpriteUnit> _free = new Stack<SpriteUnit>(64);
        readonly List<SpriteUnit> _leased = new List<SpriteUnit>(512);
        int _created;

        /// 当前全部出租中的单位（顺序不保证）。热路径遍历请用 for + 索引。
        public IReadOnlyList<SpriteUnit> Leased { get { return _leased; } }
        public int LeasedCount { get { return _leased.Count; } }
        public int TotalCreated { get { return _created; } }

        public SpriteUnitPool(Transform parent, RenderingSettings rendering)
        {
            _parent = parent;
            _rendering = rendering;
            _unitMaterial = BuildUnitMaterial(rendering);
        }

        /// 描边开启时返回全部单位共用的描边材质（SpriteRenderer 会按渲染器覆写 _MainTex）；
        /// 关闭或着色器缺失时返回 null（保持 SpriteRenderer 默认材质）。
        /// 着色器经 Resources.Load 获取：Resources 内资源始终打包进游戏，不会被剥离。
        static Material BuildUnitMaterial(RenderingSettings rendering)
        {
            if (!rendering.outlineEnabled) return null;
            var shader = Resources.Load<Shader>("SignatureSpriteOutline");
            if (shader == null) shader = Shader.Find("SignatureLogo/SpriteOutline");
            if (shader == null)
            {
                Debug.LogWarning("[SpriteUnitPool] 找不到 SignatureSpriteOutline 着色器，签名退回默认材质。");
                return null;
            }
            var material = new Material(shader);
            material.SetColor("_OutlineColor", rendering.outlineColor);
            material.SetFloat("_OutlineWidth", rendering.outlineWidth);
            return material;
        }

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++) Create();
        }

        public SpriteUnit Lease()
        {
            var unit = _free.Count > 0 ? _free.Pop() : Create();
            // 防御：清理上次被 Stop/Clear 中断、可能仍挂在单位上的补间
            unit.CachedTransform.DOKill();
            unit.gameObject.SetActive(true);
            _leased.Add(unit);
            return unit;
        }

        /// 静默租借：不激活 GameObject，激活时机交由调用方（配合分帧启动，避免万级 SetActive 尖峰）。
        public SpriteUnit LeaseDormant()
        {
            var unit = _free.Count > 0 ? _free.Pop() : Create();
            unit.CachedTransform.DOKill();
            _leased.Add(unit);
            return unit;
        }

        public void Release(SpriteUnit unit)
        {
            if (unit == null || !_leased.Remove(unit)) return;
            unit.CachedTransform.DOKill();
            unit.gameObject.SetActive(false);
            _free.Push(unit);
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < _leased.Count; i++)
            {
                var unit = _leased[i];
                unit.CachedTransform.DOKill();
                unit.gameObject.SetActive(false);
                _free.Push(unit);
            }
            _leased.Clear();
        }

        SpriteUnit Create()
        {
            var go = new GameObject("SpriteUnit_" + _created++, typeof(SpriteRenderer), typeof(SpriteUnit));
            var tr = go.transform;
            tr.SetParent(_parent, false);
            tr.localPosition = Vector3.zero;
            tr.localRotation = Quaternion.identity;
            tr.localScale = Vector3.one;

            var unit = go.GetComponent<SpriteUnit>();
            unit.Bind(tr, go.GetComponent<SpriteRenderer>());
            unit.ReleaseCallback = () => Release(unit); // 每单位生命周期内仅此一次闭包分配

            unit.Renderer.color = _rendering.tint;
            unit.Renderer.sortingOrder = _rendering.sortingOrder;
            if (_unitMaterial != null) unit.Renderer.sharedMaterial = _unitMaterial;

            go.SetActive(false);
            _free.Push(unit);
            return unit;
        }
    }
}
