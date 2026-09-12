using System.IO;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;

namespace SignatureLogo.EditorTools
{
    /// 一键搭建演示场景：生成两个演示 Logo（Mask PNG + 烘焙好的 LogoDefinition 资产）、
    /// 挂好 SignatureLogoVisualizer + DemoSignatureFeeder，相机调为正交。
    /// 进入 Play 后自动生成占位签名并开始 Logo 循环；空格键切换 Logo/球体。
    public static class SignatureLogoDemoBuilder
    {
        const string SamplesFolder = "Assets/SignatureLogo/Samples";
        const string LogosFolder = SamplesFolder + "/DemoLogos";
        const int MaskW = 512;
        const int MaskH = 256;
        const float Ppu = 100f;

        [MenuItem("Tools/SignatureLogo/创建演示场景 (Create Demo Setup)")]
        public static void Create()
        {
            EnsureFolder(SamplesFolder);
            EnsureFolder(LogosFolder);

            var logo01 = CreateLogoAsset("Logo01", true);
            var logo02 = CreateLogoAsset("Logo02", false);
            if (logo01 == null || logo02 == null) return;

            var go = GameObject.Find("SignatureLogoVisualizer");
            if (go == null) go = new GameObject("SignatureLogoVisualizer");

            var viz = go.GetComponent<SignatureLogoVisualizer>();
            if (viz == null) viz = go.AddComponent<SignatureLogoVisualizer>();

            var feeder = go.GetComponent<DemoSignatureFeeder>();
            if (feeder == null) feeder = go.AddComponent<DemoSignatureFeeder>();

            var so = new SerializedObject(viz);
            var logosProp = so.FindProperty("logos");
            logosProp.arraySize = 2;
            logosProp.GetArrayElementAtIndex(0).objectReferenceValue = logo01;
            logosProp.GetArrayElementAtIndex(1).objectReferenceValue = logo02;
            so.ApplyModifiedPropertiesWithoutUndo();

            var cam = Camera.main;
            if (cam != null)
            {
                // 透视相机：从相机身后飞入的穿越感才完整
                cam.orthographic = false;
                cam.fieldOfView = 45f;
                cam.transform.position = new Vector3(0f, 0f, -12f);
            }

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[SignatureLogo] 演示场景已创建。进入 Play：自动生成占位签名并开始 Logo 循环；空格键在 Logo / 球体间切换。", go);
        }

        static LogoDefinition CreateLogoAsset(string name, bool staircaseShape)
        {
            string pngPath = LogosFolder + "/" + name + "_mask.png";
            WriteMaskPng(pngPath, staircaseShape);
            AssetDatabase.ImportAsset(pngPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.mipmapEnabled = false;
            importer.isReadable = true; // 仅供烘焙期直读；烘焙后可手动关掉，运行时走 RenderTexture 回读兜底
            importer.SaveAndReimport();
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            if (mask == null)
            {
                Debug.LogError("[SignatureLogo] 演示 Mask 导入失败：" + pngPath);
                return null;
            }

            string assetPath = LogosFolder + "/" + name + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<LogoDefinition>(assetPath);
            if (def != null)
            {
                // 防覆盖：用户可能已把 Logo01 换成自己的 Mask，不再重写
                Debug.Log("[SignatureLogo] '" + name + "' 已存在，跳过重建（如需重置请删除该资产后重新运行）。");
                return def;
            }
            def = ScriptableObject.CreateInstance<LogoDefinition>();
            AssetDatabase.CreateAsset(def, assetPath);
            def.mask = mask;
            def.pixelsPerUnit = Ppu;
            def.cellSize = 10f;
            def.alphaThreshold = 0.5f;
            def.densityByAlpha = true;
            def.maxPoints = 0;
            def.seed = 12345u;
            def.pointScale = 1f;
            def.bakedPoints = LogoTargetGenerator.Generate(mask, Ppu, 10f, 0.5f, true, 0, 12345, 1f);
            def.bakedWidth = MaskW / Ppu;
            def.bakedHeight = MaskH / Ppu;
            def.bakeSignature = def.ComputeBakeSignature();
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssetIfDirty(def);
            return def;
        }

        static void WriteMaskPng(string path, bool staircaseShape)
        {
            var texture = new Texture2D(MaskW, MaskH, TextureFormat.RGBA32, false);
            var pixels = new Color32[MaskW * MaskH];
            var on = new Color32(255, 255, 255, 255);
            for (int y = 0; y < MaskH; y++)
            {
                for (int x = 0; x < MaskW; x++)
                {
                    bool inside = staircaseShape ? IsStairBar(x, y) : IsRing(x, y);
                    if (inside) pixels[y * MaskW + x] = on;
                }
            }
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        /// 阶梯条形（自下而上变宽），贴近需求文档中的 ASCII 示意。
        static bool IsStairBar(int x, int y)
        {
            int barH = MaskH / 4;
            int bar = Mathf.Min(y / barH, 3);
            int barW = (int)(MaskW * (bar + 1) / 4f);
            int margin = (MaskW - barW) / 2;
            return x >= margin && x < margin + barW;
        }

        /// 圆环。
        static bool IsRing(int x, int y)
        {
            float dx = (x - MaskW * 0.5f) / (MaskW * 0.42f);
            float dy = (y - MaskH * 0.5f) / (MaskH * 0.42f);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return d <= 1f && d >= 0.55f;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
