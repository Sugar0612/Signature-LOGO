# Signature LOGO — 签名拼贴 LOGO 视觉系统

用真实手写签名图片把任意 Mask 剪影（文字 / 图形）拼成 LOGO：签名从相机身后飞入、
按左→右波次拼接成形，按间隔无限轮换多个 Logo，背景循环播放视频。

## 环境要求

- Unity **6000.3.20f1**（其他 6000.x 大概率可用）
- 渲染管线：**URP**（包内含演示场景的 URP 设置资产）
- 包已附带 **DOTween 1.2.825**（Assets/Plugins/Demigiant），无需另行安装
- 输入系统：Input System（通过 Package Manager 安装，包清单见 Packages/manifest.json）

## 快速开始

1. 把本包导入工程（Assets ▸ Import Package ▸ Custom Package）。
2. 打开演示场景 `Assets/Scenes/SampleScene.unity`，直接进 Play：
   演示用的签名 Sprite 与两个演示 Logo 会自动开始循环。
3. 空格键可手动切换 Logo（演示脚本 `SignatureKeyboardTester`）。

## 接入你自己的签名与 Logo

- **签名 Sprite**：任何透明底 PNG。运行时调用可视化器接口注入：

  ```csharp
  ISignatureLogoVisualizer visualizer;
  visualizer.AddSprite(sprite);       // 逐张添加（内部自动去重/判空）
  visualizer.AddSprites(sprites);     // 批量添加
  visualizer.Start();                 // 开始无限 Logo 循环
  ```

  参考演示实现：`Runtime/Samples/TextSignatureFeeder.cs`、`DemoSignatureFeeder.cs`。

- **Logo 定义**：右键 Create ▸ SignatureLogo ▸ Logo Definition，指定透明底剪影 Mask 图，
  在 Inspector 点击 **烘焙目标点**。常用参数：

  | 参数 | 说明 |
  |---|---|
  | Cell Size | 采样格边长（像素），越小越精细、点数按平方增长；建议 ≈ 笔画宽度 |
  | Seamless Tiling | 无缝拼贴模式（文字 Logo 推荐）：签名密铺、零抖动 |
  | Sub Cell Centroid | 子格质心（默认开）：采样点贴合笔画真实边缘，横/竖等笔画笔直不台阶化 |
  | Bridge Thin Strokes | 桥接细笔画断裂；复杂字/粗笔画建议关，避免窄缝隙被封死 |
  | Max Points / Seed | 限量与随机种子（结果可复现） |

- **可视化器组件**：场景里挂 `SignatureLogoVisualizer`，把 LogoDefinition 数组拖进 Logos，
  把签名 Sprite 喂给 AddSprite 即可。Formation ▸ **Fly In World Size** 控制签名飞过镜头时的
  大小（默认 0.3 世界单位宽）；Rendering ▸ **Tile Scale** 控制拼贴重叠率（1=恰好相接）。

## 编辑器工具（Tools ▸ SignatureLogo 菜单）

- **导出 UnityPackage**：把本系统（含演示场景与依赖）打成 .unitypackage 分发。
- **打包 Windows 版**：自动烘焙过期 Logo 后构建 Win64 可执行文件。
- **运行逻辑自测**：纯逻辑单元自测（无需进 Play）。

命令行：

```
Unity.exe -batchmode -quit -projectPath <项目> -logFile <日志> ^
  -executeMethod SignatureLogo.EditorTools.SignatureLogoBuildScript.ExportPackageFromCommandLine ^
  [-packageOutput "Build/xxx.unitypackage"]

Unity.exe -batchmode -quit -projectPath <项目> -logFile <日志> ^
  -executeMethod SignatureLogo.EditorTools.SignatureLogoBuildScript.BuildFromCommandLine ^
  [-buildOutput "Build/Signature LOGO.exe"]
```

## 目录结构

```
Assets/SignatureLogo/
  Runtime/    门面 + 状态机 + 采样/分配/池化/编排（正式接入只依赖这层）
  Editor/     Inspector 烘焙、演示搭建、自测、打包/导出脚本
  Samples/    演示 Logo 资产、签名 PNG、背景视频（正式接入可删）
  Shaders/    签名描边、视频无光照着色器（Resources 自带，打包不剥离）
Docs/         架构设计文档（仓库根目录）
```

更多细节（架构、状态机、性能预算）见仓库 `Docs/SignatureLogo-Architecture.md`。
