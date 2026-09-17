using System;

using System.IO;

using System.Linq;

using UnityEditor;

using UnityEditor.Build.Reporting;

using UnityEngine;

namespace SignatureLogo.EditorTools
{
    /// Windows x64 打包脚本。
    /// 菜单：Tools ▸ SignatureLogo ▸ 打包 Windows 版；
    /// 命令行：Unity.exe -batchmode -quit -projectPath <项目> -logFile <日志>
    ///         -executeMethod SignatureLogo.EditorTools.SignatureLogoBuildScript.BuildFromCommandLine
    ///         [-buildOutput "Build/Signature LOGO.exe"]
    public static class SignatureLogoBuildScript
    {
        const string DefaultOutput = "Build/Signature LOGO.exe";
        const string DefaultPackageName = "Build/Signature-LOGO_v1.3.unitypackage";

        [MenuItem("Tools/SignatureLogo/打包 Windows 版 (Build Win64)")]
        public static void BuildFromMenu()
        {
            Build(DefaultOutput);
        }

        [MenuItem("Tools/SignatureLogo/导出 UnityPackage (Export Package)")]
        public static void ExportPackageFromMenu()
        {
            ExportPackage(DefaultPackageName);
        }

        /// 命令行：Unity.exe -batchmode -quit -projectPath <项目> -logFile <日志>
        ///        -executeMethod SignatureLogo.EditorTools.SignatureLogoBuildScript.ExportPackageFromCommandLine
        ///        [-packageOutput "输出路径.unitypackage"]
        public static void ExportPackageFromCommandLine()
        {
            string output = GetArg("-packageOutput");
            ExportPackage(string.IsNullOrEmpty(output) ? DefaultPackageName : output);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// 导出可直接给其他开发者导入的 .unitypackage：
        /// SignatureLogo 全套 + 演示场景 + DOTween 插件 + 场景依赖的 URP 设置等资产。
        static void ExportPackage(string outputPath)
        {
            BakeAllStaleLogos();

            var roots = new[]
            {
                "Assets/SignatureLogo",
                "Assets/Plugins/Demigiant",
                "Assets/Scenes/SampleScene.unity"
            };
            // 从演示场景出发收集依赖（URP 设置、输入配置等），只收 Assets 内的（Packages 由使用方 Package Manager 解析）
            var deps = AssetDatabase.GetDependencies(new[] { "Assets/Scenes/SampleScene.unity" }, true)
                .Where(p => p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                            && !roots.Any(r => p.StartsWith(r, StringComparison.OrdinalIgnoreCase))
                            && !p.Contains("/TutorialInfo/")
                            && !p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(p => p)
                .ToList();
            var all = roots.Concat(deps).ToArray();

            string dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            AssetDatabase.ExportPackage(all, outputPath, ExportPackageOptions.Recurse);
            if (!File.Exists(outputPath))
            {
                Debug.LogError("[Export] UnityPackage 导出失败，详见日志。");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log("[Export] 已导出 " + all.Length + " 个资产条目 → " + outputPath
                      + "\n随包依赖：" + (deps.Count == 0 ? "无" : "\n  " + string.Join("\n  ", deps)));
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void BuildFromCommandLine()
        {
            string output = GetArg("-buildOutput");
            Build(string.IsNullOrEmpty(output) ? DefaultOutput : output);
        }

        /// 命令行只烘焙不打包：-executeMethod SignatureLogo.EditorTools.SignatureLogoBuildScript.BakeLogosFromCommandLine
        public static void BakeLogosFromCommandLine()
        {
            BakeAllStaleLogos();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void Build(string outputPath)
        {
            BakeAllStaleLogos();

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/SampleScene.unity" };

            string dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            Debug.Log("[Build] 开始打包：" + outputPath + "，场景：" + string.Join(", ", scenes));
            BuildReport report = BuildPipeline.BuildPlayer(
                scenes, outputPath, BuildTarget.StandaloneWindows64, BuildOptions.None);

            Debug.Log("[Build] 结果：" + report.summary.result
                      + "，大小：" + report.summary.totalSize + " 字节"
                      + "，错误：" + report.summary.totalErrors
                      + "，耗时：" + report.summary.totalTime);

            if (report.summary.result != BuildResult.Succeeded)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw new Exception("[Build] 打包失败，详见 Console / 日志。");
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// 打包前自动烘焙：把所有烘焙过期（Mask/采样参数变更后未重烤）的 Logo 重新固化，
        /// 保证包内运行时零纹理读取、无启动重算开销，也不再弹过期警告。
        static void BakeAllStaleLogos()
        {
            var guids = AssetDatabase.FindAssets("t:LogoDefinition");
            int baked = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<LogoDefinition>(path);
                if (def == null || def.mask == null) continue;
                if (def.bakedPoints != null && def.bakedPoints.Length > 0 &&
                    def.bakeSignature == def.ComputeBakeSignature()) continue;

                var points = LogoTargetGenerator.Generate(
                    def.mask, def.pixelsPerUnit, def.cellSize, def.alphaThreshold,
                    def.densityByAlpha, def.maxPoints, (int)def.seed, def.pointScale, def.seamlessTiling,
                    def.bridgeThinStrokes, def.subCellCentroid);
                def.bakedPoints = points;
                def.bakedWidth = def.mask.width / Mathf.Max(1f, def.pixelsPerUnit);
                def.bakedHeight = def.mask.height / Mathf.Max(1f, def.pixelsPerUnit);
                def.bakeSignature = def.ComputeBakeSignature();
                EditorUtility.SetDirty(def);
                AssetDatabase.SaveAssetIfDirty(def);
                baked++;
                Debug.Log("[Build] 已烘焙 '" + def.name + "'：" + points.Length + " 个目标点（" + path + "）");
            }
            Debug.Log("[Build] Logo 烘焙检查完成，本次烘焙 " + baked + " 个。");
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}
