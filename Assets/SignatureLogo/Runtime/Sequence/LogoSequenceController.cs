using System;

using UnityEngine;

namespace SignatureLogo
{
    /// Logo 轮换计时器：只在 Holding 阶段计时；到 LogoSwitchInterval 后请求下一个 Logo。
    /// 计时从 Formation 完成才启动，与"完成后等待设定时间"的需求一致。
    public sealed class LogoSequenceController
    {
        float _interval = 15f;
        float _holdTimer;
        bool _counting;

        public event Action IntervalElapsed;

        /// ≤0 会被钳制为 1 秒，避免除零/疯狂切换。
        public float LogoSwitchInterval
        {
            get { return _interval; }
            set { _interval = Mathf.Max(1f, value); }
        }

        public float HoldRemainder
        {
            get { return _counting ? Mathf.Max(0f, _interval - _holdTimer) : 0f; }
        }

        public void BeginHold()
        {
            _holdTimer = 0f;
            _counting = true;
        }

        public void StopHold()
        {
            _counting = false;
        }

        public void Tick(float deltaTime)
        {
            if (!_counting) return;
            _holdTimer += deltaTime;
            if (_holdTimer < _interval) return;
            _counting = false; // 先停再通知，避免回调里重入
            var handler = IntervalElapsed;
            if (handler != null) handler();
        }
    }
}
