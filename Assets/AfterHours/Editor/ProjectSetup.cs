using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.Core;

namespace AfterHours.EditorTools
{
    /// <summary>Menu: After Hours > 1 - Set Up Project. Safe to run any number of times.</summary>
    public static class ProjectSetup
    {
        [MenuItem("After Hours/1 - Set Up Project", priority = 1)]
        public static void SetUpFromMenu()
        {
            bool changed = EnsureLayers();
            EnsureRendering();
            if (PlayerSettings.productName != "After Hours")
            {
                PlayerSettings.productName = "After Hours";
                changed = true;
            }
            EditorUtility.DisplayDialog("After Hours",
                changed ? "Project set up: physics layers created.\n\nNext: After Hours > 2 - Build Movement Test Scene."
                        : "Project was already set up.\n\nNext: After Hours > 2 - Build Movement Test Scene.",
                "OK");
        }

        /// <summary>
        /// Lighting settings the mall needs, on whatever URP asset the project uses: Forward+ (so every light can
        /// reach every surface - plain Forward only lets 8 lights touch an object, and the merged floors are huge),
        /// and shadows from many lights at once (so light stops at walls and floors instead of shining through).
        /// </summary>
        public static void EnsureRendering()
        {
            var assets = new System.Collections.Generic.List<RenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline != null) assets.Add(GraphicsSettings.defaultRenderPipeline);
            int q = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                RenderPipelineAsset a = QualitySettings.GetRenderPipelineAssetAt(i);
                if (a != null && !assets.Contains(a)) assets.Add(a);
            }
            foreach (RenderPipelineAsset asset in assets)
            {
                var so = new SerializedObject(asset);
                void Int(string n, int v) { SerializedProperty p = so.FindProperty(n); if (p != null) p.intValue = v; }
                void Bool(string n, bool v) { SerializedProperty p = so.FindProperty(n); if (p != null) p.boolValue = v; }
                Int("m_AdditionalLightsRenderingMode", 1);          // per pixel
                Int("m_AdditionalLightsPerObjectLimit", 8);
                Bool("m_AdditionalLightShadowsSupported", true);
                Int("m_AdditionalLightsShadowmapResolution", 4096);
                Int("m_AdditionalLightsShadowResolutionTierLow", 256);
                Int("m_AdditionalLightsShadowResolutionTierMedium", 512);
                Int("m_AdditionalLightsShadowResolutionTierHigh", 1024);
                Bool("m_SoftShadowsSupported", true);
                so.ApplyModifiedPropertiesWithoutUndo();
                SerializedProperty list = so.FindProperty("m_RendererDataList");
                if (list != null)
                    for (int i = 0; i < list.arraySize; i++)
                    {
                        Object rd = list.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (rd == null) continue;
                        var rso = new SerializedObject(rd);
                        SerializedProperty mode = rso.FindProperty("m_RenderingMode");
                        if (mode != null && mode.intValue != 2) { mode.intValue = 2; rso.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(rd); }   // 2 = Forward+
                    }
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Adds the game's physics layers to Project Settings > Tags and Layers.</summary>
        public static bool EnsureLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[AfterHours] Could not open ProjectSettings/TagManager.asset");
                return false;
            }

            var so = new SerializedObject(assets[0]);
            SerializedProperty layers = so.FindProperty("layers");
            bool changed = false;

            foreach (string name in GameLayers.AllNames)
            {
                if (HasLayer(layers, name)) continue;
                int slot = -1;
                for (int i = 8; i < layers.arraySize; i++)
                {
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    {
                        slot = i;
                        break;
                    }
                }
                if (slot < 0)
                {
                    Debug.LogError("[AfterHours] No free layer slot for " + name);
                    continue;
                }
                layers.GetArrayElementAtIndex(slot).stringValue = name;
                changed = true;
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
            return changed;
        }

        static bool HasLayer(SerializedProperty layers, string name)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == name) return true;
            }
            return false;
        }
    }
}
