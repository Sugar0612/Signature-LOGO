# Signature Logo 视觉系统

签名 Sprite → 从相机身后飞入 → 从左到右无缝拼接成 Logo → 按间隔无限轮换。
适用于现场互动大屏：签名板/手写识别由外部程序负责，识别结果以 Unity `Sprite` 喂给本系统即可。

> 环境：Unity 6000.3.20f1 / URP / DOTween 1.2.825（已内置）
> 架构详情见 `Docs/SignatureLogo-Architecture.md`

---

## 快速开始（3 行代码）

```csharp
using SignatureLogo;
using UnityEngine;

// 场景中挂有 SignatureLogoVisualizer 组件的物体（演示场景中名为 SignatureLogoVisualizer）
ISignatureLogoVisualizer viz =
    GameObject.Find("SignatureLogoVisualizer").GetComponent<ISignatureLogoVisualizer>();

viz.AddSprites(mySignatureSprites);   // 喂入签名
viz.Start();                          // 开始播放（无限轮换）
```

建议把 `ISignatureLogoVisualizer` 引用缓存在自己的类里，不要每帧 GetComponent。

---

## 公开接口一览 `ISignatureLogoVisualizer`

| 接口 | 说明 |
|------|------|
| `void Start()` | 开始播放。Stopped 状态下调用 = 恢复播放 |
| `void Stop()` | **纯停止**：冻结当前画面（签名停在原地），不清空、不隐藏，切换计时停止 |
| `float LogoSwitchInterval { get; set; }` | 每个 Logo 停留秒数，默认 15。≤0 自动钳制为 1，**运行中修改立即生效** |
| `void AddSprite(Sprite s)` | 加入 1 个签名（null 安全跳过） |
| `void AddSprites(IEnumerable<Sprite> list)` | 批量加入签名 |
| `void ClearSprites()` | 清空全部签名：立即停止显示，回到待机 |
| `IReadOnlyList<Sprite> Sprites { get; }` | 当前签名列表（只读视图，外部不可修改） |
| `VisualizerState State { get; }` | 状态：`Idle / Forming / Holding / Stopped` |

事件（用于灯光、音效等联动）：

```csharp
viz.LogoFormationStarted  += index => { };   // 某 Logo 开始拼接
viz.LogoFormationCompleted += index => { };  // 某 Logo 拼接完成
```

---

## 完整接入示例（签名板团队参考）

```csharp
using System.Collections.Generic;
using SignatureLogo;
using UnityEngine;

public class SignatureTeamIntegration : MonoBehaviour
{
    ISignatureLogoVisualizer _viz;

    void Awake()
    {
        _viz = GameObject.Find("SignatureLogoVisualizer")
               .GetComponent<ISignatureLogoVisualizer>();
    }

    /// <summary>签名识别完成：喂签名 → 设定间隔 → 开始播放</summary>
    public void OnSignaturesReady(List<Sprite> signatures)
    {
        _viz.AddSprites(signatures);      // 顺序 = 沿笔画从左到右的轮换顺序
        _viz.LogoSwitchInterval = 10f;    // 每 10 秒切换下一个 Logo
        _viz.Start();
    }

    /// <summary>停止（冻结当前画面，签名保留）</summary>
    public void PauseShow()  => _viz.Stop();

    /// <summary>恢复播放</summary>
    public void ResumeShow() => _viz.Start();

    /// <summary>彻底结束（停止并清屏，回到待机）</summary>
    public void EndShow()    => _viz.ClearSprites();
}
```

---

## 行为细节与注意事项

1. **全部接口仅支持主线程调用**（Unity 限制）。
2. **播放中 `AddSprite/AddSprites` 不打断当前画面**，在下次 Logo 切换（Formation）时生效；`ClearSprites` 则立即清屏。
3. `Start()` 的前置条件：已喂入至少 1 个 Sprite，且至少 1 个 Logo 已烘焙。条件不满足时调用会被忽略并输出警告，**条件满足后会自动开始**，无需轮询重试。
4. `Stop()` 只冻结画面与计时，不动 Sprites 数据；`Start()` 恢复时签名会重新从相机身后飞入、拼接停止时的那个 Logo。
5. 切换计时从 Logo 拼接完成后才启动（"完成后再停留 N 秒"），拼接耗时不会挤占停留时间。
6. 场景中同一物体上的演示组件（`DemoSignatureFeeder` / `TextSignatureFeeder`）是测试用的签名来源，正式接入时**移除它们**，由签名板代码调用接口。

---

## 美术 / 策划：如何添加一个 Logo

1. 准备 Logo 剪影图：PNG、透明背景（文字 Logo 建议笔画粗一些，太细的笔画采样后容易断）。
2. 拖入项目任意位置（保持默认导入设置即可，无需开 Read/Write）。
3. Project 窗口右键 → **Create ▸ SignatureLogo ▸ Logo Definition**，把图片拖到 `Mask` 槽。
4. 关键参数：
   - `Cell Size`：采样步长（像素）= 拼贴"分辨率"，越小越精细、点数越多（点数≈按平方增长）
   - `Seamless Tiling`：文字 Logo 勾选（签名无缝密铺、完整无空洞）
   - `Max Points`：0 = 不限；需要控量时设置上限
5. 点 **"烘焙目标点 (Bake Target Points)"** 按钮，Console 会输出点数。
6. 把 LogoDefinition 资产拖进场景中 `SignatureLogoVisualizer` 的 `Logos` 数组（顺序 = 轮换顺序）。

修改 Mask 或采样参数后**必须重新烘焙**——Inspector 检测到过期会显示黄色警告。

---

## 测试

- 挂载 **`Keyboard Tester (1=Start 2=Stop)`** 组件（演示场景已自带）：按 **1** = Start / 恢复，按 **2** = Stop / 冻结；每次按键 Console 打印调用记录。
- 菜单 **Tools ▸ SignatureLogo ▸ 运行逻辑自测**：21 项核心逻辑断言。
- 菜单 **Tools ▸ SignatureLogo ▸ 创建演示场景**：一键生成演示 Logo、占位签名与测试组件。

---

## 常见问题

| 现象 | 原因与处理 |
|------|-----------|
| 拼出的文字有空洞 | 烘焙数据过期（旧算法）→ 重新点"烘焙"；或 Cell Size 相对笔画太大 → 调小。拼贴模式自带桥接修复，正常不会再出洞 |
| 切换时看不到"从相机身后飞入" | 把 Main Camera 的 Projection 改为 **Perspective**（透视下穿越感完整）；确认 Formation 的 `Cold Start Pose = NearCamera` |
| 飞入的签名看不清 | 调大 Formation 的 `Fly In World Size`（默认 0.9 世界单位）；`Fly In Ease` 保持 InQuad（镜头前停留更久） |
| 帧率下降 | 减小目标点数量（调大 `Cell Size` 后重新烘焙）、调大 `Stagger Duration`（拉长波浪、减少同屏飞行数）、缩小 `Fly In World Size` |
| 调了 LogoSwitchInterval 没反应 | 它立即生效，但只影响**下一个停留周期**；确认修改的是接口属性而非仅 Inspector 数值 |
| Start 没反应 | 查 Console 警告：通常是无签名、或 Logo 未烘焙且贴图不可读 |

---

## 性能与规模参考

- 对象池预热后运行时零 Instantiate/Destroy；全系统单 LateUpdate 驱动；补间容量随规模自适应。
- 实测规模：单 Logo 约 1.5 万目标点仍可运行；**建议控制在 5000 点以内**（Cell Size 4~6 通常即可保证清晰）。
- 渲染批次 ≈ 不同签名贴图数量（SRP Batcher），与签名个数无关。

## 目录

```
Assets/SignatureLogo/Runtime   运行时（接口入口 ISignatureLogoVisualizer.cs / SignatureLogoVisualizer.cs）
Assets/SignatureLogo/Editor    烘焙、演示场景生成、逻辑自测（Tools 菜单）
Assets/Plugins/Demigiant       DOTween 1.2.825
Docs/                          架构设计文档
```
