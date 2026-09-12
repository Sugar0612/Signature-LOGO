using System.Collections.Generic;
using System.Collections.ObjectModel;

using UnityEngine;

namespace SignatureLogo
{
    /// Signature Sprite 的唯一数据源。内部 List 不会暴露给外部；
    /// Sprites 属性返回缓存的只读视图（cast 回 List 也无法修改内部数据）。
    public sealed class SignatureSpriteRepository
    {
        readonly List<Sprite> _sprites = new List<Sprite>(256);
        ReadOnlyCollection<Sprite> _view;

        /// 每次修改自增，外部可据此检测数据变化。
        public int Version { get; private set; }

        public int Count => _sprites.Count;

        /// 外部读取入口。仅在 Add/Clear 时重建视图（一次性小分配，热路径零分配）。
        public IReadOnlyList<Sprite> Sprites
        {
            get { return _view ?? (_view = new ReadOnlyCollection<Sprite>(_sprites)); }
        }

        public void Add(Sprite sprite)
        {
            if (sprite == null) return;
            _sprites.Add(sprite);
            Invalidate();
        }

        public void AddRange(IEnumerable<Sprite> sprites)
        {
            if (sprites == null) return;
            bool added = false;
            foreach (var sprite in sprites)
            {
                if (sprite == null) continue;
                _sprites.Add(sprite);
                added = true;
            }
            if (added) Invalidate();
        }

        public void Clear()
        {
            if (_sprites.Count == 0) return;
            _sprites.Clear();
            Invalidate();
        }

        void Invalidate()
        {
            _view = null;
            Version++;
        }
    }
}
