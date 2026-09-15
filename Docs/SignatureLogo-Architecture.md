# Signature Logo 视觉系统 — 架构设计 v1.2

> 环境：Unity 6000.3.20f1 / URP 17.3 + DOTween 1.2.825。
> **v1.2 变更**：新增**视频背景**（VideoBackground，1.mp4 循环铺底、Logo 深度天然在前）、
> **签名描边**（SpriteOutline 着色器，亮背景保持可读）、**拼贴密度**（Tile Scale 重叠系数 + cellSize 调密）、
> 飞入签名缩小（flyInWorldSize 0.9→0.5）、Resources 化自写着色器修复打包剥离、新增打包脚本。
> **v1.1 变更**：应用户要求**移除球体模式**（Stop / Fibonacci 球 / 随机轴自转 / Scale Impact），
> 系统只保留 **Logo 从左到右拼接 + 按间隔无限轮换**。接口相应移除 `Stop()` 与 `SphereAssembleCompleted`。
> v1.1 同时包含：无缝拼贴模式、烘焙过期自检、透明度覆盖率自检、宽 Logo 防溢出适配。
> 职责边界：签名板 / 手写识别 / Sprite 产出由外部团队负责；本系统只消费 `Sprite`。

---

## 1. 关键假设

| # | 假设 | 说明 |
|---|------|------|
| A1 | 2D 正交呈现 | Logo 在 XY 平面（Z=0） |
| A2 | 单 Logo 显示 | 同一时刻只组装一个 Logo；切换时新旧短暂重叠（Morph），池峰值 = max(旧点数, 新点数) |
| A3 | 规模 | Sprite 1 ~ 数千；单 Logo 目标点 0 ~ ~2000 |
| A4 | 线程模型 | 公开 API 仅主线程调用 |

## 2. 总体架构（三层）

```
┌─────────────────────────────────────────────────────────────┐
│ 门面层  ISignatureLogoVisualizer ◄── SignatureLogoVisualizer │
│         （唯一 MonoBehaviour，显式接口实现 Start）             │
└───────────────┬─────────────────────────────────────────────┘
                │ 持有并驱动（唯一 LateUpdate → Tick）
┌───────────────▼─────────────────────────────────────────────┐
│ 编排层                                                       │
│  VisualizerState(enum: Idle/Forming/Holding)                 │
│  LogoSequenceController ──► LogoCompositionController        │
│  （计时/轮换）                （组建/Formation/Morph）          │
└──────┬──────────────────┬───────────────────┬────────────────┘
       │                  │                   │
┌──────▼───────┐  ┌───────▼────────┐  ┌───────▼────────┐
│ 数据层        │  │ 生成/分配层      │  │ 表现层          │
│ Signature-   │  │ LogoTarget-    │  │ SpriteUnitPool │
│ Sprite-      │  │ Generator      │  │ SpriteUnit     │
│ Repository   │  │ SpriteDistributor│ │ (DOTween 驱动) │
│ LogoDef (SO) │  │ TargetPoint    │  │                │
└──────────────┘  └────────────────┘  └────────────────┘
```

设计原则：**单一 MonoBehaviour 门面**（其余为普通 C# 类）；**单点驱动**（一个 LateUpdate，单位动画交 DOTween 全局更新）；**预计算优先**（Mask→TargetPoint 编辑器烘焙进 SO）；**池化 + 零热路径 GC**。

## 3. 类关系与职责

| 类 | 关键成员 |
|----|----------|
| `ISignatureLogoVisualizer` | Start / LogoSwitchInterval / Sprites / AddSprite(s) / ClearSprites / State / 2 事件 |
| `SignatureLogoVisualizer`(MB) | 组装依赖图、状态机 Tick、显式接口实现 Start、LogoRoot 自适应、Gizmos |
| `SignatureSpriteRepository` | 内部 List 封装，缓存 `ReadOnlyCollection` 只读视图，`Version` 脏标记 |
| `LogoDefinition`(SO) | `mask, pixelsPerUnit, cellSize, alphaThreshold, densityByAlpha, maxPoints, seed, seamlessTiling, bakedPoints, bakeSignature` |
| `LogoTargetGenerator` | 纯函数采样（散点/拼贴双模式）+ 过期签名 + 覆盖率自检 + RenderTexture 回读兜底 |
| `SpriteDistributor` | Round-Robin：`sprites[(k+offset) % count]` |
| `SpriteUnitPool` / `SpriteUnit` | 预热池零 Instantiate；Unit 仅 SpriteRenderer + 缓存 Transform |
| `LogoCompositionController` | `BuildLogo`（冷启动/Morph 统一入口）、按时刻判定完成、拼贴缩放 |
| `LogoSequenceController` | `LogoSwitchInterval`（≤0 钳 1），Holding 计时 → 请求下一个 |
| `TextSpriteFactory` / 两个 Feeder | 演示用：字体渲染字符为 Sprite / 手动拖 Sprite（正式接入时移除） |

## 4. 状态机

`enum VisualizerState { Idle, Forming, Holding, Stopped }`

- Idle --Start()--> Forming --完成--> Holding --到 LogoSwitchInterval--> Forming(下一个，取模回绕，无限循环)
- Forming/Holding --Stop()--> Stopped（画面冻结：签名停在当前位置，不清空不隐藏，计时停止）--Start()--> Forming（重飞当前 Logo，恢复播放）
- 边界：无 Sprite 时 Start() 记警告保持 Idle（`autoStartWhenSpritesReady` 可选自动开始）；重复 Start() 忽略；`LogoSwitchInterval≤0` 钳制为 1（运行时改 API 立即生效）；切换计时从 Formation 完成才启动；运行中 AddSprite 下次 Formation 快照生效；ClearSprites 立即停止显示回 Idle（Stopped 状态下调用会连同冻结画面一起清掉）。

## 5. Logo Mask → TargetPoint（烘焙主路径 + 运行时回退）

```
1. pixels = GetReadablePixels32(mask)             // 不可读时经 RenderTexture 回读
2. 网格步进 cellSize：格平均 alpha < threshold 跳过
   散点模式：densityByAlpha 概率加权 + ±cell/2 抖动
   拼贴模式：零抖动、关闭概率镂空，精确落在网格中心
3. maxPoints>0 且超量：Fisher–Yates(种子) 洗牌取前 N
4. 像素→本地坐标: localPos = ((x−W/2)/PPU, (y−H/2)/PPU, 0)   // GetPixels32 自下而上，同向换算
5. 排序：X 升序，X 同按 Y 降序（同列自上而下）
6. normalizedX = (x−minX)/(maxX−minX)
```

**无缝拼贴模式（seamlessTiling）**：运行时把每个签名的世界宽度缩放到正好等于网格间距（cellSize/PPU）→ 相邻签名相接不重叠，适合文字 Logo；此模式绕过 BaseScale 与各项抖动；建议 cellSize ≈ Mask 笔画宽度。**完整性保障**：判定阈值放宽至 ≤0.4 + 桥接修复 pass（覆盖不足但邻居已占的格子自动补上，迭代两轮）——细笔画无断裂、无空洞。

**过期与覆盖率自检**：SO 记录烘焙签名（Mask 名称/尺寸 + 全部参数）。编辑器检测过期 → 高亮警告；运行时检测过期 → 临时重新生成并警告。采样点铺满 >85% 网格 → 警告"Mask 很可能不是透明底"。

## 6. Sprite → TargetPoint 分配（Round-Robin / Modulo）

`sprites[((pointIndex + offset) % count + count) % count]`，相邻点拿相邻签名；`offset = logoIndex` 错开起始；每次 Formation 快照分配，只换 `renderer.sprite` 引用。

## 7. Left → Right Formation

波浪感来自**延迟按 normalizedX 分布**；单位永远从当前位置出发，绝不瞬移。

```
delay_k = normalizedX × staggerDuration ± 5%
move:  DOLocalMove(target, moveDuration).SetDelay(delay).SetEase(OutCubic)
```

入场方式（FormationSettings.coldStartPose）：
- **NearCamera（默认）从相机身后飞入**：起点按实际相机位置换算（相机平面后方 startBehindCamera 世界单位），XY 在 Logo 四周以 startSpreadRadius×对角线 的半径随机散布；飞行时以绝对可读宽度 flyInWorldSize（世界单位）越过镜头，缓动默认 InQuad（镜头前停留更久、观众能认出字符），临近落位才缩小到拼贴尺寸。
- **LeftEdge 左缘飞入**：从 Logo 左侧屏幕外弹出（原平面效果，OutBack 弹出）。

Logo 切换（**一律重飞**）：全部签名回到相机身后（瞬移发生在镜头外不可见），按入场方式（默认从相机身后四周散布）重新按左→右波次飞入拼接。烘焙签名含算法版本号（g2|…）：生成算法升级后旧烘焙数据自动失效，运行时自动重新生成。

细节度：拼贴文字的"分辨率" = LogoDefinition.cellSize（采样格步长，像素）；调小 → 更精细、点数按平方增长，DOTween 补间容量随池规模自适应（prewarm×2+512）。

## 8. 公共 API

```csharp
public interface ISignatureLogoVisualizer {
    IReadOnlyList<Sprite> Sprites { get; }        // 缓存只读视图
    float  LogoSwitchInterval { get; set; }       // 默认15，≤0 钳为 1，运行时可改
    VisualizerState State { get; }
    void   AddSprite(Sprite sprite);  void AddSprites(IEnumerable<Sprite> sprites);  void ClearSprites();
    void   Start();                               // 无限 Logo 循环；Stopped 状态下调用=恢复播放
    void   Stop();                                // 纯停止：冻结当前画面（不清空），计时停止
    event Action<int> LogoFormationStarted;
    event Action<int> LogoFormationCompleted;
}
```

**关键陷阱**：`Start()` 与 MonoBehaviour 生命周期消息重名 → 门面用**显式接口实现**；外部一律持 `ISignatureLogoVisualizer` 引用调用。

## 9. Inspector 配置

门面：Logos(SO数组) / Switch(LogoSwitchInterval=15) / Formation(stagger 2.0、move 0.6、margin、抖动、缓动) /
Rendering(baseScale、tint、order、FitToCamera 宽高取小、Outline 描边三参、tileScale 拼贴重叠系数) / Pool(预热覆盖) / Debug(Gizmos、autoStart)。
`LogoDefinition`：mask、PPU、cellSize、alphaThreshold、densityByAlpha、maxPoints、seed、pointScale、seamlessTiling、烘焙产物。
自定义 Inspector：过期警告 + **烘焙按钮** + 统计信息。
`VideoBackground`（场景物体）：videoClip、loop、brightness、behindLogoDistance。

## 10. 性能方案

对象池预热（max 各 Logo 点数，不足自动扩）；单 LateUpdate；DOTween `SetTweensCapacity(3072,128)` + recyclable + 单位级零闭包；烘焙进 SO 运行时零纹理读取；热路径禁 foreach-IReadOnlyList / LINQ / Camera.main；SpriteRenderer 共享材质 SRP Batcher。2000 Sprite 预算：逻辑 <0.1ms/帧、0 GC Alloc。

## 11. 目录结构

```
Assets/SignatureLogo/
  Runtime/  Core(VisualizerState/TargetPoint) Data(LogoDefinition/VisualizerSettings)
            Generation Distribution Pooling Composition Sequence Repository
            Background(VideoBackground 视频背景) Shaders/Resources(SpriteOutline/VideoUnlit，Resources 保证进包)
            ISignatureLogoVisualizer.cs SignatureLogoVisualizer.cs Samples(TextSpriteFactory/两个Feeder)
  Editor/   LogoDefinitionEditor(烘焙) SignatureLogoDemoBuilder(演示场景) SignatureLogoSelfTests(自测)
            SignatureLogoBuildScript(菜单/命令行打包 Win64)
  Samples/  DemoLogos(Logo 资产) Signatures(签名 PNG) Vedio(背景视频 1.mp4)
Docs/      本文档
```

## 12. 阶段记录

P1-P5 已全部完成并经无头批处理验证（编译零错误 / 逻辑自测 / Play 全链路冒烟）。v1.1 移除球体模式后回归验证通过。
v1.2：视频背景 / 描边 / 密集拼贴 / 打包修复（着色器 Resources 化）已实机打包验证（Windows x64，构建日志 Succeeded）。
