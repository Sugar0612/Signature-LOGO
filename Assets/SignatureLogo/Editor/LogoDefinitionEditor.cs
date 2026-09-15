using UnityEditor;
using UnityEngine;

namespace SignatureLogo.EditorTools
{
    /// LogoDefinition 自定义 Inspector：一键烘焙目标点 + 统计信息。
    /// 烘焙会把 TargetPoint[] 序列化进资产，运行时零纹理读取；
    /// Mask 贴图无需开启 Read/Write（生成器内部有 RenderTexture 回读兜底）。
    [CustomEditor(typeof(LogoDefinition))]
    public sealed class LogoDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var def = (LogoDefinition)target;

            // 烘焙过期自检：换了 Mask 或改了采样参数但没重新烘焙
            bool stale = def.mask != null && def.bakedPoints != null && def.bakedPoints.Length > 0 &&
                         def.bakeSignature != def.ComputeBakeSignature();
            if (stale)
            {
                EditorGUILayout.HelpBox(
                    "⚠ Mask 或采样参数已变更，当前烘焙数据已过期（运行时会临时用旧数据/重新生成，可能有启动开销）。\n请点击下方按钮重新烘焙！",
                    MessageType.Warning);
            }

            GUILayout.Space(6);
            EditorGUILayout.HelpBox(BuildStats(def), MessageType.Info);

            using (new EditorGUI.DisabledScope(def.mask == null))
            {
                if (GUILayout.Button("烘焙目标点 (Bake Target Points)", GUILayout.Height(30)))
                {
                    Undo.RecordObject(def, "Bake Target Points");
                    var points = LogoTargetGenerator.Generate(
                        def.mask, def.pixelsPerUnit, def.cellSize, def.alphaThreshold,
                        def.densityByAlpha, def.maxPoints, (int)def.seed, def.pointScale, def.seamlessTiling,
                        def.bridgeThinStrokes);
                    def.bakedPoints = points;
                    def.bakedWidth = def.mask.width / Mathf.Max(1f, def.pixelsPerUnit);
                    def.bakedHeight = def.mask.height / Mathf.Max(1f, def.pixelsPerUnit);
                    def.bakeSignature = def.ComputeBakeSignature();
                    EditorUtility.SetDirty(def);
                    AssetDatabase.SaveAssetIfDirty(def);
                    Debug.Log("[SignatureLogo] '" + def.name + "' 烘焙完成：" + points.Length + " 个目标点。", def);
                }
            }
        }

        static string BuildStats(LogoDefinition def)
        {
            int count = def.bakedPoints != null ? def.bakedPoints.Length : 0;
            string size = def.mask != null ? def.mask.width + "×" + def.mask.height : "-";
            string world = def.bakedWidth > 0f
                ? def.bakedWidth.ToString("0.##") + "×" + def.bakedHeight.ToString("0.##") + " 世界单位"
                : "未烘焙";
            return "Mask: " + size + "    烘焙点: " + count + "    Logo 尺寸: " + world +
                   "\n修改采样参数后需重新烘焙；把 Logo 拖入场景中的 Signature Logo Visualizer，" +
                   "选中后可在 Scene 视图查看点分布（Debug ▸ Show Target Point Gizmos）。" +
                   (def.seamlessTiling
                       ? "\n拼贴模式已开启：建议 Cell Size ≈ Mask 笔画宽度（笔画约 1 个签名厚，最容易认）"
                       : "");
        }
    }
}
