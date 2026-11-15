using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Realistic materials built from the generated textures (colour + normal + metallic/AO/smoothness mask).
    /// Every surface material remembers its real-world tile size, so the mesh combiner can lay textures out
    /// at the right physical scale on every wall, floor and ceiling (tiles are always 60 cm, blocks 44 cm...).
    /// </summary>
    public static class ArtLibrary
    {
        const string TexFolder = EditorBuild.Root + "/Textures/Generated";
        const string MatFolder = EditorBuild.Root + "/Materials/Art";

        static readonly Dictionary<Material, float> tileSizes = new Dictionary<Material, float>();
        static readonly HashSet<Material> detailMats = new HashSet<Material>();

        public static bool TryGetTileSize(Material m, out float size) => tileSizes.TryGetValue(m, out size);

        /// <summary>Small detail materials: lit by light probes instead of lightmaps (faster bakes).</summary>
        public static bool IsDetail(Material m) => detailMats.Contains(m);
        public static void MarkDetail(Material m) { if (m != null) detailMats.Add(m); }

        public static void Reset()
        {
            tileSizes.Clear();
            detailMats.Clear();
            TextureImportRules.EnsureAll();
        }

        static Shader Lit()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            return s != null ? s : Shader.Find("Standard");
        }

        static Material LoadOrCreate(string name)
        {
            EditorBuild.EnsureFolder(MatFolder);
            string path = MatFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Lit());
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != Lit())
            {
                m.shader = Lit();
            }
            return m;
        }

        static Texture2D Tex(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + "/" + file);

        /// <summary>A textured surface. tileMeters = how many metres one copy of the texture covers.</summary>
        public static Material Surface(string name, string tex, float tileMeters, Color tint, float smoothness = 1f, float normalStrength = 1f, float metallic = 0f)
        {
            Material m = LoadOrCreate(name);
            Texture2D a = Tex(tex + "_A.jpg"), n = Tex(tex + "_N.png"), mask = Tex(tex + "_M.png");

            m.SetColor("_BaseColor", tint);
            if (a != null) m.SetTexture("_BaseMap", a);
            if (m.HasProperty("_MainTex") && a != null) m.SetTexture("_MainTex", a);

            if (n != null)
            {
                m.SetTexture("_BumpMap", n);
                m.SetFloat("_BumpScale", normalStrength);
                m.EnableKeyword("_NORMALMAP");
            }
            if (mask != null)
            {
                m.SetTexture("_MetallicGlossMap", mask);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_OcclusionMap", mask);
                m.EnableKeyword("_OCCLUSIONMAP");
                m.SetFloat("_OcclusionStrength", 1f);
                m.SetFloat("_Smoothness", smoothness);   // multiplier on the mask's smoothness
                m.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            else
            {
                m.SetFloat("_Smoothness", smoothness * 0.4f);
            }
            m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            tileSizes[m] = tileMeters;
            return m;
        }

        /// <summary>Plain colour material (no texture), optionally emissive.</summary>
        public static Material Plain(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null, bool bakedEmission = true)
        {
            Material m = LoadOrCreate(name);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetTexture("_BaseMap", null);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = bakedEmission ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>See-through decal material (stains, scuffs, cracks...). Drawn on thin quads just above surfaces.</summary>
        public static Material Decal(string name, string decalFile, Color tint, float smoothness = 0.2f)
        {
            Material m = LoadOrCreate("Decal_" + name);
            Texture2D t = Tex("decal_" + decalFile + ".png");
            m.SetTexture("_BaseMap", t);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", smoothness);
            MakeTransparent(m);
            m.renderQueue = (int)RenderQueue.Transparent - 50; // decals before glass
            detailMats.Add(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static void MakeTransparent(Material m)
        {
            if (!m.HasProperty("_Surface")) return;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
