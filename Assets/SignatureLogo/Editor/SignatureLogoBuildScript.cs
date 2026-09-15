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

        [MenuItem("Tools/SignatureLogo/打包 Windows 版 (Build Win64)")]
        public static void BuildFromMenu()
        {
            Build(DefaultOutput);
        }

        public static void BuildFromCommandLine()
        {
            string output = GetArg("-buildOutput");
            Build(string.IsNullOrEmpty(output) ? DefaultOutput : output);
        }

        static void Build(string outputPath)
        {
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
