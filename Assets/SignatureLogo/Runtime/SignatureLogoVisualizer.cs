using System;

using System.Collections.Generic;

using DG.Tweening;

using UnityEngine;

namespace SignatureLogo
{
    /// 系统唯一 MonoBehaviour 门面：
    /// - 组装依赖图（仓库/池/两个控制器）
    /// - 显式实现 ISignatureLogoVisualizer.Start（避免与 Unity 生命周期消息 Start() 冲突）
    /// - 唯一 LateUpdate 驱动状态机（Formation 完成判定 / 切换计时）
    [DisallowMultipleComponent]
    [AddComponentMenu("SignatureLogo/Signature Logo Visualizer")]
    public sealed class SignatureLogoVisualizer : MonoBehaviour, ISignatureLogoVisualizer
    {
        [Header("Logos（LogoDefinition 资产，顺序即轮换顺序）")]
        [SerializeField] LogoDefinition[] logos = Array.Empty<LogoDefinition>();

        [Header("Switch")]
        [SerializeField, Min(1f), Tooltip("每个 Logo Formation 完成后的停留秒数")]
        float logoSwitchInterval = 15f;

        [Header("Settings")]
        [SerializeField] FormationSettings formation = new FormationSettings();
        [SerializeField] RenderingSettings rendering = new RenderingSettings();

        [Header("Pool")]
        [SerializeField, Min(0), Tooltip("预热单位数；0 = 自动取各 Logo 烘焙点数的最大值")]
        int prewarmOverride = 0;

        [Header("Debug")]
        [SerializeField, Tooltip("选中本组件时在 Scene 视图绘制全部 Logo 目标点")]
        bool showTargetPointGizmos = true;
        [SerializeField, Tooltip("Idle 时一旦加入 Sprite 立即自动 Start")]
        bool autoStartWhenSpritesReady = false;

        readonly SignatureSpriteRepository _repository = new SignatureSpriteRepository();
        SpriteUnitPool _pool;
        LogoCompositionController _composition;
        LogoSequenceController _sequenceCtrl;
        Transform _logoRoot;
        bool _startPending;

        public VisualizerState State { get; private set; } = VisualizerState.Idle;
        public IReadOnlyList<Sprite> Sprites { get { return _repository.Sprites; } }

        public float LogoSwitchInterval
        {
            get { return logoSwitchInterval; }
            set
            {
                logoSwitchInterval = Mathf.Max(1f, value);
                if (_sequenceCtrl != null) _sequenceCtrl.LogoSwitchInterval = logoSwitchInterval;
            }
        }

        public int SpriteCount { get { return _repository.Count; } }
        public int ActiveUnitCount { get { return _pool != null ? _pool.LeasedCount : 0; } }
        public int CurrentLogoIndex { get { return _composition != null ? _composition.CurrentLogoIndex : -1; } }

        public event Action<int> LogoFormationStarted;
        public event Action<int> LogoFormationCompleted;

        // ---------------- 公开 API（显式接口实现，Unity 不会把它当生命周期消息调用） ----------------

        void ISignatureLogoVisualizer.Start() { EnterLogoMode(); }

        public void AddSprite(Sprite sprite)
        {
            _repository.Add(sprite);
            MaybeAutoStart();
        }

        public void AddSprites(IEnumerable<Sprite> sprites)
        {
            _repository.AddRange(sprites);
            MaybeAutoStart();
        }

        public void ClearSprites()
        {
            _repository.Clear();
            if (State == VisualizerState.Forming || State == VisualizerState.Holding)
            {
                _composition.BreakFormation();
                _pool.ReleaseAll();
                State = VisualizerState.Idle;
            }
        }

        // ---------------- 生命周期 ----------------

        void Awake()
        {
            if (logoSwitchInterval < 1f) logoSwitchInterval = 15f;
            BuildHierarchy();

            _pool = new SpriteUnitPool(_logoRoot, rendering);
            _composition = new LogoCompositionController(_repository, _pool, logos, formation, rendering, _logoRoot);
            _composition.FormationCompleted += OnFormationCompleted;

            int prewarm = prewarmOverride;
            if (prewarm <= 0)
            {
                // 用真实目标点数（烘焙过期时触发运行时重生成后的数量）预热，避免规模低估
                for (int i = 0; i < logos.Length; i++)
                {
                    var points = logos[i] != null ? _composition.GetPoints(logos[i]) : null;
                    if (points != null && points.Length > prewarm) prewarm = points.Length;
                }
            }
            if (prewarm > 0) _pool.Prewarm(prewarm);

            // 补间容量随规模自适应（每个单位同时最多 2 条补间：位移 + 缩放）
            DOTween.SetTweensCapacity(Mathf.Max(4096, prewarm * 2 + 512), 128);

            _sequenceCtrl = new LogoSequenceController { LogoSwitchInterval = logoSwitchInterval };
            _sequenceCtrl.IntervalElapsed += OnSwitchIntervalElapsed;
        }

        void LateUpdate()
        {
            switch (State)
            {
                case VisualizerState.Forming:
                    _composition.Tick(Time.time);
                    break;

                case VisualizerState.Holding:
                    _sequenceCtrl.Tick(Time.deltaTime);
                    break;

                case VisualizerState.Idle:
                    break;
            }
        }

        void OnValidate()
        {
            if (logoSwitchInterval < 1f) logoSwitchInterval = 1f;
        }

        // ---------------- 状态转移 ----------------

        void EnterLogoMode()
        {
            if (_repository.Count == 0 || CountUsableLogos() == 0)
            {
                Debug.LogWarning("[SignatureLogo] Start() 被忽略：缺少 Signature Sprite 或可用 Logo（Mask 未烘焙且不可读）。", this);
                _startPending = true;
                return;
            }
            _startPending = false;

            if (State == VisualizerState.Idle)
            {
                BeginLogoSequence(0);
            }
            else
            {
                Debug.LogWarning("[SignatureLogo] Start() 被忽略：已处于 Logo 模式。", this);
            }
        }

        void BeginLogoSequence(int logoIndex)
        {
            State = VisualizerState.Forming;
            _sequenceCtrl.StopHold();
            var handler = LogoFormationStarted;
            if (handler != null) handler(logoIndex);

            ApplyLogoFit(logoIndex);                      // 先定 LogoRoot 最终缩放
            _composition.BuildLogo(logoIndex, Time.time); // 相机身后深度换算必须用最终缩放
        }

        void OnFormationCompleted(int logoIndex)
        {
            State = VisualizerState.Holding;
            _sequenceCtrl.BeginHold();
            var handler = LogoFormationCompleted;
            if (handler != null) handler(logoIndex);
        }

        void OnSwitchIntervalElapsed()
        {
            if (State != VisualizerState.Holding) return;
            int usable = CountUsableLogos();
            if (usable == 0) return;
            int next = (Mathf.Max(0, CurrentLogoIndex) + 1) % usable;
            BeginLogoSequence(next); // Morph：已有单位从当前位置飞向新 Logo，新补位签名按配置入场
        }

        void MaybeAutoStart()
        {
            if (_repository.Count == 0) return;
            bool shouldStart = _startPending || (autoStartWhenSpritesReady && State == VisualizerState.Idle);
            if (shouldStart && CountUsableLogos() > 0) EnterLogoMode();
        }

        // ---------------- 辅助 ----------------

        void BuildHierarchy()
        {
            var existingRoot = transform.Find("LogoRoot");
            _logoRoot = existingRoot != null ? existingRoot : new GameObject("LogoRoot").transform;
            _logoRoot.SetParent(transform, false);
        }

        int CountUsableLogos()
        {
            if (_composition == null) return 0;
            int count = 0;
            for (int i = 0; i < logos.Length; i++)
            {
                var points = logos[i] != null ? _composition.GetPoints(logos[i]) : null;
                if (points != null && points.Length > 0) count++;
            }
            return count;
        }

        void ApplyLogoFit(int logoIndex)
        {
            if (!rendering.fitToCameraHeight) return;
            var logo = logoIndex >= 0 && logoIndex < logos.Length ? logos[logoIndex] : null;
            if (logo == null) return;
            float logoHeight = _composition.GetLogoHeight(logo);
            float logoWidth = _composition.GetLogoWidth(logo);
            if (logoHeight <= 0f && logoWidth <= 0f) return;

            var cam = Camera.main;
            if (cam == null) return;
            float viewHeight;
            if (cam.orthographic)
            {
                viewHeight = cam.orthographicSize * 2f;
            }
            else
            {
                float distance = Mathf.Abs(_logoRoot.position.z - cam.transform.position.z);
                if (distance < 0.01f) return;
                viewHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            }

            // 同时约束高度与宽度取小者：横幅文字 Logo 才不会左右溢出屏幕
            float viewWidth = viewHeight * cam.aspect;
            float fraction = rendering.targetHeightFraction;
            float byHeight = logoHeight > 0f ? viewHeight * fraction / logoHeight : float.MaxValue;
            float byWidth = logoWidth > 0f ? viewWidth * fraction / logoWidth : float.MaxValue;
            float scale = Mathf.Min(byHeight, byWidth);
            if (scale <= 0f || scale == float.MaxValue) return;
            _logoRoot.localScale = new Vector3(scale, scale, 1f);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!showTargetPointGizmos || logos == null) return;
            var root = Application.isPlaying && _logoRoot != null ? _logoRoot : transform.Find("LogoRoot");
            if (root == null) root = transform;

            var previousColor = Gizmos.color;
            Gizmos.color = new Color(0f, 1f, 0.6f, 0.85f);
            const float size = 0.06f;
            for (int i = 0; i < logos.Length; i++)
            {
                var logo = logos[i];
                if (logo == null) continue;
                var points = logo.bakedPoints;
                if (points == null || points.Length == 0) continue;
                for (int k = 0; k < points.Length; k++)
                {
                    Gizmos.DrawWireCube(root.TransformPoint(points[k].position), new Vector3(size, size, size));
                }
            }
            Gizmos.color = previousColor;
        }
#endif
    }
}
