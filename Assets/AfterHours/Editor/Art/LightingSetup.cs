using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AfterHours.Rendering;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Atmosphere and lighting: post-processing look, light/reflection probes, bake settings,
    /// and the "Bake Lighting" menu (pre-calculates soft light, bounce light and shadows).
    /// </summary>
    public static class LightingSetup
    {
        const string SettingsFolder = EditorBuild.Root + "/Settings";
        const string ProfilePath = SettingsFolder + "/MallPostProcess.asset";
        const string LightingPath = SettingsFolder + "/MallLighting.lighting";

        // ------------------------------------------------------------------ post-processing look

        public static VolumeProfile CreatePostProfile()
        {
            EditorBuild.EnsureFolder(SettingsFolder);
            AssetDatabase.DeleteAsset(ProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            T Add<T>() where T : VolumeComponent
            {
                T c = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }

            Tonemapping tone = Add<Tonemapping>();
            tone.mode.value = TonemappingMode.ACES;

            // Early-2000s night-time fluorescent look: slightly green, a little washed out, low saturation.
            ColorAdjustments color = Add<ColorAdjustments>();
            color.postExposure.value = 0.7f;
            color.contrast.value = 14f;
            color.saturation.value = -22f;
            color.colorFilter.value = new Color(0.97f, 1f, 0.95f);

            WhiteBalance wb = Add<WhiteBalance>();
            wb.temperature.value = -6f;
            wb.tint.value = 7f;

            ShadowsMidtonesHighlights smh = Add<ShadowsMidtonesHighlights>();
            smh.shadows.value = new Vector4(0.95f, 1.0f, 1.04f, -0.04f);
            smh.highlights.value = new Vector4(1.0f, 1.0f, 0.97f, 0f);

            Bloom bloom = Add<Bloom>();
            bloom.threshold.value = 0.95f;
            bloom.intensity.value = 0.55f;
            bloom.scatter.value = 0.65f;

            Vignette vig = Add<Vignette>();
            vig.intensity.value = 0.3f;
            vig.smoothness.value = 0.45f;

            FilmGrain grain = Add<FilmGrain>();
            grain.type.value = FilmGrainLookup.Medium3;
            grain.intensity.value = 0.32f;
            grain.response.value = 0.75f;

            ChromaticAberration ca = Add<ChromaticAberration>();
            ca.intensity.value = 0.07f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        public static void AddGlobalVolume(Transform parent)
        {
            var go = new GameObject("Post Processing");
            go.transform.SetParent(parent, false);
            Volume v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 0f;
            v.sharedProfile = CreatePostProfile();
        }

        // ------------------------------------------------------------------ bake settings

        /// <param name="forBake">true: final dark ambient for baking. false: slightly brighter preview before baking.</param>
        public static void ApplyLightingSettings(bool forBake)
        {
            EditorBuild.EnsureFolder(SettingsFolder);
            LightingSettings ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (ls == null)
            {
                ls = new LightingSettings { name = "MallLighting" };
                AssetDatabase.CreateAsset(ls, LightingPath);
            }
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU; // falls back to CPU automatically if needed
            ls.mixedBakeMode = MixedLightingMode.IndirectOnly;
            ls.lightmapResolution = 4f;       // texels per metre: a balance of quality vs bake time for a huge building
            ls.lightmapPadding = 2;
            ls.lightmapMaxSize = 2048;
            ls.directionalityMode = LightmapsMode.CombinedDirectional; // surface bumps (tiles, blockwork) still show under baked light
            ls.ao = true;
            ls.aoMaxDistance = 1.2f;
            ls.directSampleCount = 32;
            ls.indirectSampleCount = 384;
            ls.environmentSampleCount = 64;
            ls.maxBounces = 3;
            EditorUtility.SetDirty(ls);
            Lightmapping.lightingSettings = ls;

            // Night inside a sealed building: almost no ambient light; everything comes from the fittings.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = forBake ? new Color(0.05f, 0.054f, 0.06f) : new Color(0.11f, 0.115f, 0.125f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
        }

        // ------------------------------------------------------------------ probes

        /// <summary>Light probes on a grid through every walkable space (moving things and small details use these).</summary>
        public static void AddLightProbes(Transform parent)
        {
            var positions = new List<Vector3>();
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                for (float x = -124f; x <= 124f; x += 4f)
                {
                    for (float z = -124f; z <= 124f; z += 4f)
                    {
                        ZoneInfo zone = MallZones.Classify(new Vector3(x, y + 1f, z));
                        if (zone.kind == ZoneKind.Outside || zone.kind == ZoneKind.LiftShaft) continue;
                        positions.Add(new Vector3(x, y + 1.0f, z));
                        positions.Add(new Vector3(x, y + 3.2f, z));
                    }
                }
            }
            for (float x = -96f; x <= 96f; x += 6f)
            {
                for (float z = -96f; z <= 96f; z += 6f)
                {
                    float floorY = MallLayout.CarParkRaised.Contains(x, z) ? -2.5f : MallLayout.CarParkY;
                    positions.Add(new Vector3(x, floorY + 1f, z));
                    positions.Add(new Vector3(x, floorY + 2.6f, z));
                }
            }
            // Atrium void: a few levels high.
            for (float yy = 7.5f; yy < 18f; yy += 4f)
            {
                for (float x = -8f; x <= 8f; x += 4f)
                {
                    for (float z = -8f; z <= 8f; z += 4f) positions.Add(new Vector3(x, yy, z));
                }
            }

            var go = new GameObject("Light Probes");
            go.transform.SetParent(parent, false);
            go.AddComponent<LightProbeGroup>().probePositions = positions.ToArray();
        }

        /// <summary>Box-projected reflection probes so shiny floors reflect the right surroundings.</summary>
        public static void AddReflectionProbes(Transform parent)
        {
            Transform g = new GameObject("Reflection Probes").transform;
            g.SetParent(parent, false);

            void Probe(Vector3 centre, Vector3 size)
            {
                var go = new GameObject("Reflection Probe");
                go.transform.SetParent(g, false);
                go.transform.position = centre;
                ReflectionProbe p = go.AddComponent<ReflectionProbe>();
                p.mode = ReflectionProbeMode.Baked;
                p.boxProjection = true;
                p.size = size;
                p.resolution = 128;
                p.importance = 1;
                p.blendDistance = 2f;
            }

            Probe(new Vector3(0f, 9f, 0f), new Vector3(32f, 18f, 32f));
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                foreach (WingId w in MallLayout.Wings)
                {
                    for (float u = 26f; u < 96f; u += 20f)
                    {
                        Vector3 c = MallLayout.WingPoint(w, u, 0f, y + 2.25f);
                        AreaRect r = MallLayout.WingRect(w, u - 10f, u + 10f, -6f, 6f);
                        Probe(c, new Vector3(r.Width, 4.5f, r.Depth));
                    }
                    AreaRect a = MallLayout.WingRect(w, 96f, MallLayout.AnchorEndFor(f, w), -19f, 19f);
                    Probe(new Vector3(a.Center.x, y + 2.25f, a.Center.y), new Vector3(a.Width, 4.5f, a.Depth));
                }
            }
            for (float x = -66f; x <= 66f; x += 66f)
            {
                for (float z = -66f; z <= 66f; z += 66f) Probe(new Vector3(x, -2.2f, z), new Vector3(66f, 3.6f, 66f));
            }
        }

        // ------------------------------------------------------------------ bake

        [MenuItem("After Hours/5 - Bake Lighting (slow)", priority = 5)]
        public static void Bake()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("After Hours", "Stop Play mode first, then try again.", "OK");
                return;
            }
            if (EditorSceneManager.GetActiveScene().path != MallBuilder.ScenePath)
            {
                EditorUtility.DisplayDialog("After Hours", "Open the mall first (After Hours > 3 - Build Mall).", "OK");
                return;
            }
            int choice = EditorUtility.DisplayDialogComplex("After Hours - Bake Lighting",
                    "This pre-calculates all the mall's lighting, shadows and reflections.\n\n" +
                    "It can take a LONG time (30 minutes to a few hours on a laptop). You can keep using the computer, " +
                    "but leave Unity open. Progress shows in the bottom-right corner of Unity.\n\n" +
                    "Moving shadows: Elliot, players and props still cast real shadows from the nearby lights (looks best).\n" +
                    "Fastest: everything baked, moving things cast no shadows from the ceiling lights (for weak PCs).",
                    "Bake with moving shadows", "Cancel", "Bake fastest");
            if (choice == 1) return;
            bool mixed = choice == 0;

            ApplyLightingSettings(true);
            LightingSettings lsMode = Lightmapping.lightingSettings;
            if (lsMode != null) { lsMode.mixedBakeMode = mixed ? MixedLightingMode.Shadowmask : MixedLightingMode.IndirectOnly; EditorUtility.SetDirty(lsMode); }
            int converted = 0;
            foreach (BakeableLight b in Object.FindObjectsByType<BakeableLight>(FindObjectsInactive.Include))
            {
                Light l = b.GetComponent<Light>();
                if (l == null) continue;
                l.lightmapBakeType = mixed ? LightmapBakeType.Mixed : LightmapBakeType.Baked;
                l.shadows = LightShadows.Soft;
                l.shadowRadius = 0.35f;
                converted++;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[AfterHours] Baking lighting with {converted} baked lights...");

            Lightmapping.bakeCompleted -= OnBakeCompleted;
            Lightmapping.bakeCompleted += OnBakeCompleted;
            Lightmapping.BakeAsync();
        }

        static void OnBakeCompleted()
        {
            Lightmapping.bakeCompleted -= OnBakeCompleted;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[AfterHours] Lighting bake finished and saved. Now computing occlusion culling...");
            StaticOcclusionCulling.smallestOccluder = 2f;
            StaticOcclusionCulling.smallestHole = 0.25f;
            StaticOcclusionCulling.GenerateInBackground();
        }

        [MenuItem("After Hours/6 - Undo Baked Lighting (back to preview)", priority = 6)]
        public static void ClearBake()
        {
            Lightmapping.Clear();
            Lightmapping.ClearLightingDataAsset();
            foreach (BakeableLight b in Object.FindObjectsByType<BakeableLight>(FindObjectsInactive.Include))
            {
                Light l = b.GetComponent<Light>();
                if (l == null) continue;
                l.lightmapBakeType = LightmapBakeType.Realtime;
                l.shadows = LightShadows.Soft;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
