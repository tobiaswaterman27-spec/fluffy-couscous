using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Renderer settings for a more realistic image: HDR, soft shadows at a sensible distance, and screen-space
    /// ambient occlusion (the soft darkening where things meet - corners, under shelves, around feet).
    /// Done through serialized properties and reflection so it keeps working across URP versions.
    /// </summary>
    [InitializeOnLoad]
    public static class RenderQuality
    {
        // once per editor session: keep the renderer settings current without needing a mall rebuild
        static RenderQuality()
        {
            if (SessionState.GetBool("AfterHours.RenderQuality", false)) return;
            SessionState.SetBool("AfterHours.RenderQuality", true);
            EditorApplication.delayCall += () => { try { Apply(); } catch (Exception e) { Debug.LogWarning("[AfterHours] Render settings: " + e.Message); } };
        }

        public static void Apply()
        {
            RenderPipelineAsset rp = GraphicsSettings.defaultRenderPipeline;
            if (rp == null) { Debug.LogWarning("[AfterHours] No render pipeline asset found; skipped render quality settings."); return; }
            var so = new SerializedObject(rp);
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_SoftShadowsSupported", true);
            SetF(so, "m_ShadowDistance", 60f);
            Set(so, "m_SupportsCameraDepthTexture", true);
            // real-time shadows from the mall's own lights (ShadowBudget picks which)
            Set(so, "m_MainLightShadowsSupported", true);
            Set(so, "m_AdditionalLightShadowsSupported", true);
            SetI(so, "m_AdditionalLightsRenderingMode", 1);
            SetI(so, "m_AdditionalLightsShadowmapResolution", 4096);
            SetI(so, "m_AdditionalLightsShadowResolutionTierLow", 256);
            SetI(so, "m_AdditionalLightsShadowResolutionTierMedium", 512);
            SetI(so, "m_AdditionalLightsShadowResolutionTierHigh", 1024);
            SetI(so, "m_AdditionalLightsPerObjectLimit", 8);
            SetI(so, "m_SoftShadowQuality", 2);
            Set(so, "m_SupportsLightCookies", true);
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedProperty list = so.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0) return;
            UnityEngine.Object data = list.GetArrayElementAtIndex(0).objectReferenceValue;
            if (data != null) AddAmbientOcclusion(data);
        }

        static void Set(SerializedObject so, string name, bool v) { SerializedProperty p = so.FindProperty(name); if (p != null) p.boolValue = v; }
        static void SetI(SerializedObject so, string name, int v) { SerializedProperty p = so.FindProperty(name); if (p != null) p.intValue = v; }
        static void SetF(SerializedObject so, string name, float v) { SerializedProperty p = so.FindProperty(name); if (p != null) p.floatValue = v; }

        static void AddAmbientOcclusion(UnityEngine.Object rendererData)
        {
            Type t = Type.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");
            if (t == null) return;
            var so = new SerializedObject(rendererData);
            SerializedProperty features = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            if (features == null) return;
            for (int i = 0; i < features.arraySize; i++)
            {
                UnityEngine.Object f = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (f != null && f.GetType() == t) return; // already there
            }
            var feature = ScriptableObject.CreateInstance(t);
            feature.name = "ScreenSpaceAmbientOcclusion";
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            if (map != null)
            {
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            Debug.Log("[AfterHours] Added ambient occlusion to the renderer.");
        }
    }
}
