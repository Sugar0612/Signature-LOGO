using System;

using System.Collections.Generic;

using DG.Tweening;

using UnityEngine;

namespace SignatureLogo
{
    /// 负责"当前 Logo 的组建"：租单位 → Round-Robin 分配签名 → 按 normalizedX 波次飞入。
    /// 冷启动 / Logo 切换 Morph / 球体返回，统一走 BuildLogo 入口。
    /// 完成判定按时间戳（不逐帧扫描单位），由门面 Tick 驱动。
    public sealed class LogoCompositionController
    {
        const float ShrinkOutDuration = 0.2f;
        const float FallbackCameraZ = -10f; // 场景无相机时的兜底（与默认主相机位置一致）
        const float RngSeed = 20260912;

        readonly SignatureSpriteRepository _repository;
        readonly SpriteUnitPool _pool;
        readonly LogoDefinition[] _logos;
        readonly FormationSettings _formation;
        readonly RenderingSettings _rendering;
        readonly Transform _logoRoot;
        readonly System.Random _rng;
        readonly Dictionary<LogoDefinition, TargetPoint[]> _runtimePointCache =
            new Dictionary<LogoDefinition, TargetPoint[]>();
        readonly List<SpriteUnit> _units = new List<SpriteUnit>(512);
        readonly List<SpawnTask> _spawnQueue = new List<SpawnTask>(1024);
        int _spawnHead;
        float _buildTime;
        bool _tiling;
        float _spacing;
        float _spawnSpread;

        /// 分帧补间任务：单位已瞬移到入场起点（不可见），等待 Launch 创建飞行补间。
        struct SpawnTask
        {
            public SpriteUnit Unit;
            public TargetPoint Point;
            public Sprite Sprite;
            public float OriginalDelay;
        }

        public event Action<int> FormationCompleted;

        public int CurrentLogoIndex { get; private set; } = -1;
        public int ActiveUnitCount { get { return _units.Count; } }
        public bool CompletionPending { get; private set; }
        public float FormationEndTime { get; private set; }

        public LogoCompositionController(
            SignatureSpriteRepository repository, SpriteUnitPool pool, LogoDefinition[] logos,
            FormationSettings formation, RenderingSettings rendering, Transform logoRoot)
        {
            _repository = repository;
            _pool = pool;
            _logos = logos;
            _formation = formation;
            _rendering = rendering;
            _logoRoot = logoRoot;
            _rng = new System.Random((int)RngSeed);
        }

        /// 门面 LateUpdate 驱动：先分帧推进补间创建队列，Formation 时间到 → 发完成事件。
        public void Tick(float now)
        {
            if (_spawnHead < _spawnQueue.Count) ProcessSpawnQueue(now);
            if (!CompletionPending || now < FormationEndTime) return;
            CompletionPending = false;
            var handler = FormationCompleted;
            if (handler != null) handler(CurrentLogoIndex);
        }

        /// 组建指定 Logo。Morph 语义：复用已有单位飞向新目标点，富余缩小回池，新补入的签名按配置的入场方式出现。
        /// 性能关键路径（万级单位）：复用单位不做 SetActive 切换；全部单位同帧瞬移到镜头外起点，
        /// 飞行补间的创建由 Tick 分帧推进（ProcessSpawnQueue），避免切换瞬间的单帧尖峰。
        public void BuildLogo(int logoIndex, float now)
        {
            var logo = _logos[logoIndex];
            var points = GetPoints(logo);
            if (points == null || points.Length == 0 || _repository.Count == 0) return;

            CurrentLogoIndex = logoIndex;
            _tiling = logo.seamlessTiling;
            _spacing = _tiling ? logo.GetPointSpacing() : 0f;
            float leftEdgeX = -GetLogoWidth(logo) * 0.5f - _formation.startEdgeMargin;
            float logoW = GetLogoWidth(logo);
            float logoH = GetLogoHeight(logo);
            _spawnSpread = Mathf.Sqrt(logoW * logoW + logoH * logoH) * _formation.startSpreadRadius;

            _spawnQueue.Clear();
            _spawnHead = 0;
            _buildTime = now;

            int common = Mathf.Min(_units.Count, points.Length);

            // 1) 复用：已有单位直接重飞（瞬移发生在镜头外，旧画面同帧消失）
            for (int k = 0; k < common; k++)
            {
                var unit = _units[k];
                unit.CachedTransform.DOKill();
                PrepareUnit(unit, points[k], leftEdgeX);
                _spawnQueue.Add(new SpawnTask
                {
                    Unit = unit,
                    Point = points[k],
                    Sprite = SpriteDistributor.Resolve(_repository.Sprites, k, logoIndex),
                    OriginalDelay = ComputeDelay(points[k].normalizedX)
                });
            }

            // 2) 富余：缩小消失并回池（ReleaseCallback 由池一次性提供，零闭包分配）
            for (int k = common; k < _units.Count; k++)
            {
                var tr = _units[k].CachedTransform;
                tr.DOKill();
                tr.DOScale(Vector3.zero, ShrinkOutDuration).SetEase(Ease.InQuad).OnComplete(_units[k].ReleaseCallback);
            }
            if (_units.Count > common) _units.RemoveRange(common, _units.Count - common);

            // 3) 缺口：静默租新单位（保持未激活，激活随分帧 Launch 进行）
            for (int k = common; k < points.Length; k++)
            {
                var unit = _pool.LeaseDormant();
                _units.Add(unit);
                PrepareUnit(unit, points[k], leftEdgeX);
                _spawnQueue.Add(new SpawnTask
                {
                    Unit = unit,
                    Point = points[k],
                    Sprite = SpriteDistributor.Resolve(_repository.Sprites, k, logoIndex),
                    OriginalDelay = ComputeDelay(points[k].normalizedX)
                });
            }

            CompletionPending = true;
            // +0.3s：分帧创建补间（约 4 帧排空）的排空余量
            FormationEndTime = now + _formation.staggerDuration + _formation.moveDuration + _formation.popDuration + 0.3f;
        }

        /// 每帧处理约 1/4 队列：激活单位 + 创建飞行补间。delay 按已流逝时间折算，波次节奏与同步创建一致。
        void ProcessSpawnQueue(float now)
        {
            float elapsed = now - _buildTime;
            int remaining = _spawnQueue.Count - _spawnHead;
            int end = _spawnHead + Mathf.Min(Mathf.Max(64, remaining / 4), remaining);
            for (int i = _spawnHead; i < end; i++)
            {
                var task = _spawnQueue[i];
                LaunchUnit(task.Unit, task.Point, task.Sprite, Mathf.Max(0f, task.OriginalDelay - elapsed));
            }
            _spawnHead = end;
            if (_spawnHead >= _spawnQueue.Count)
            {
                _spawnQueue.Clear();
                _spawnHead = 0;
            }
        }

        /// 停止播放：冻结当前画面——Kill 全部补间（签名停在当前位置），不清屏、不回池、保留单位跟踪。
        public void FreezeFormation()
        {
            for (int i = 0; i < _units.Count; i++) _units[i].CachedTransform.DOKill();
            _spawnQueue.Clear();
            _spawnHead = 0;
            CompletionPending = false;
        }

        /// 立即停止 Formation：Kill 全部补间并清空跟踪（单位仍由池出租，供下次 Formation 复用）。
        public void BreakFormation()
        {
            for (int i = 0; i < _units.Count; i++) _units[i].CachedTransform.DOKill();
            _units.Clear();
            _spawnQueue.Clear();
            _spawnHead = 0;
            CompletionPending = false;
        }

        /// 取目标点：烘焙数据未过期 → 直接返回（零开销）；
        /// 未烘焙或已过期（Mask/参数变了没重新烘焙）→ 运行时生成兜底，并警告提示。
        public TargetPoint[] GetPoints(LogoDefinition logo)
        {
            if (logo == null) return null;
            bool hasBaked = logo.bakedPoints != null && logo.bakedPoints.Length > 0;
            if (hasBaked && logo.bakeSignature == logo.ComputeBakeSignature()) return logo.bakedPoints;

            if (_runtimePointCache.TryGetValue(logo, out var cached)) return cached;
            if (logo.mask == null) return hasBaked ? logo.bakedPoints : null;
            try
            {
                var points = LogoTargetGenerator.Generate(
                    logo.mask, logo.pixelsPerUnit, logo.cellSize, logo.alphaThreshold,
                    logo.densityByAlpha, logo.maxPoints, (int)logo.seed, logo.pointScale, logo.seamlessTiling,
                    logo.bridgeThinStrokes, logo.subCellCentroid);
                _runtimePointCache.Add(logo, points);
                Debug.LogWarning(
                    hasBaked
                        ? "[SignatureLogo] Logo '" + logo.name + "' 的 Mask/采样参数已变更但未重新烘焙，本次运行临时重新生成了 " +
                          points.Length + " 个目标点（请在编辑器中打开该资产重新烘焙）。"
                        : "[SignatureLogo] Logo '" + logo.name + "' 未烘焙，已运行时生成 " + points.Length +
                          " 个目标点（建议在编辑器中烘焙以消除启动开销）。",
                    logo);
                return points;
            }
            catch (Exception e)
            {
                Debug.LogError("[SignatureLogo] Logo '" + logo.name + "' 目标点生成失败：" + e.Message, logo);
                return hasBaked ? logo.bakedPoints : null;
            }
        }

        public float GetLogoWidth(LogoDefinition logo)
        {
            if (logo.bakedWidth > 0f) return logo.bakedWidth;
            return logo.mask != null ? logo.mask.width / Mathf.Max(1f, logo.pixelsPerUnit) : 0f;
        }

        public float GetLogoHeight(LogoDefinition logo)
        {
            if (logo.bakedHeight > 0f) return logo.bakedHeight;
            return logo.mask != null ? logo.mask.height / Mathf.Max(1f, logo.pixelsPerUnit) : 0f;
        }

        /// 烘焙点数的最大值（供池预热）。
        public int GetBakedMaxPointCount()
        {
            int max = 0;
            for (int i = 0; i < _logos.Length; i++)
            {
                var pts = _logos[i] != null ? _logos[i].bakedPoints : null;
                if (pts != null && pts.Length > max) max = pts.Length;
            }
            return max;
        }

        /// 入场第一步（同步、BuildLogo 内调用）：把单位瞬移到入场起点。
        /// 相机身后 / 屏幕左外均在镜头外，观众看到的旧画面同帧消失；起始缩放先置极小值，
        /// 可见的"可读起始尺寸"由 LaunchUnit 在分帧激活时按 Sprite 尺寸设置。
        void PrepareUnit(SpriteUnit unit, in TargetPoint point, float leftEdgeX)
        {
            var tr = unit.CachedTransform;
            float posJitter = _tiling ? 0f : _formation.positionJitter;
            bool nearCamera = _formation.coldStartPose == FormationStartPose.NearCamera;

            if (nearCamera)
            {
                // 从相机身后出发：XY 在 Logo 四周大半径散布（飞行轨迹可见），以"可读尺寸"越过镜头
                float angle = (float)(_rng.NextDouble() * Mathf.PI * 2.0);
                float radius = _spawnSpread * Mathf.Sqrt((float)_rng.NextDouble());
                tr.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius + RandRange(posJitter),
                    Mathf.Sin(angle) * radius + RandRange(posJitter),
                    GetBehindCameraLocalZ());
            }
            else
            {
                // 左缘飞入：从 Logo 左侧屏幕外弹出
                tr.localPosition = new Vector3(
                    leftEdgeX + RandRange(posJitter),
                    point.position.y + RandRange(posJitter), 0f);
            }
            tr.localScale = new Vector3(0.001f, 0.001f, 1f);
            tr.localRotation = Quaternion.Euler(0f, 0f, ComputeRotation(point.rotationZ));
        }

        /// 入场第二步（分帧、ProcessSpawnQueue 内调用）：换 Sprite、激活并创建飞行补间。
        void LaunchUnit(SpriteUnit unit, in TargetPoint point, Sprite sprite, float delay)
        {
            unit.gameObject.SetActive(true);
            unit.SetSprite(sprite);

            var tr = unit.CachedTransform;
            float targetScale = ComputeTargetScale(sprite, point, _rendering.baseScale);
            bool nearCamera = _formation.coldStartPose == FormationStartPose.NearCamera;

            if (nearCamera)
            {
                // 起始尺寸 = 绝对可读宽度（与落位尺寸无关），保证飞行中认得出字符
                float boundsX = sprite != null ? sprite.bounds.size.x : 0f;
                float startScale = boundsX > 0.0001f
                    ? _formation.flyInWorldSize / boundsX
                    : targetScale * 2.5f;
                tr.localScale = new Vector3(startScale, startScale, 1f);
                // InQuad：前半程慢（镜头前保持可读大小），后程加速缩小入位
                tr.DOLocalMove(point.position, _formation.moveDuration).SetDelay(delay).SetEase(_formation.flyInEase);
                tr.DOScale(new Vector3(targetScale, targetScale, 1f), _formation.moveDuration)
                    .SetDelay(delay).SetEase(_formation.flyInEase);
            }
            else
            {
                tr.DOLocalMove(point.position, _formation.moveDuration).SetDelay(delay).SetEase(_formation.moveEase);
                tr.DOScale(new Vector3(targetScale, targetScale, 1f), _formation.popDuration)
                    .SetDelay(delay).SetEase(_formation.popEase);
            }
        }

        /// 相机身后起点的 LogoRoot 本地 Z（负值）：按实际相机位置换算。
        /// 注意 LogoRoot 的 Z 轴缩放恒为 1（自适应只缩放 XY），必须按 Z 轴缩放换算深度，
        /// 否则起点会落在相机前方半空，而不是相机身后。
        float GetBehindCameraLocalZ()
        {
            var cam = Camera.main;
            Vector3 camWorld = cam != null ? cam.transform.position : new Vector3(0f, 0f, FallbackCameraZ);
            float zScale = Mathf.Max(0.0001f, _logoRoot.lossyScale.z);
            float camLocalZ = _logoRoot.InverseTransformPoint(camWorld).z;
            return camLocalZ - _formation.startBehindCamera / zScale;
        }

        float ComputeDelay(float normalizedX)
        {
            // ±5% 抖动，打散同一列的单位
            float wobble = 1f + (float)(_rng.NextDouble() - 0.5) * 0.1f;
            return normalizedX * _formation.staggerDuration * wobble;
        }

        float ComputeTargetScale(Sprite sprite, in TargetPoint point, float baseScale)
        {
            // 拼贴模式：签名世界宽度 = 网格间距 × tileScale × point.scale
            // （tileScale > 1 时相邻签名相互重叠，扁宽的签名图才能填满行间空隙、笔画更实；
            //  point.scale 为边缘抗锯齿系数：边缘格按覆盖率缩小，Logo 边界呈渐变而非台阶）
            if (_tiling && sprite != null)
            {
                float width = sprite.bounds.size.x;
                if (width > 0.0001f) return _spacing * _rendering.tileScale * point.scale / width;
            }
            float jitter = 1f - _formation.scaleJitter * 0.5f + (float)_rng.NextDouble() * _formation.scaleJitter;
            return baseScale * point.scale * jitter;
        }

        float ComputeRotation(float baseRotationZ)
        {
            if (_tiling) return baseRotationZ; // 拼贴模式不抖动旋转，保证整齐密铺
            return baseRotationZ + RandRange(_formation.rotationJitter);
        }

        float RandRange(float magnitude)
        {
            return ((float)_rng.NextDouble() * 2f - 1f) * magnitude;
        }
    }
}
