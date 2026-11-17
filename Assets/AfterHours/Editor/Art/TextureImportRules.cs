using System.IO;
using UnityEditor;
using UnityEngine;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Import settings for the generated textures, applied automatically by file name:
    ///   *_N  -> normal map        *_M -> linear mask (metallic / AO / smoothness)
    ///   decal_* -> transparency, clamped edges        everything else -> tiling colour texture
    /// </summary>
    public class TextureImportRules : AssetPostprocessor
    {
        public const string Folder = EditorBuild.Root + "/Textures/";

        void OnPreprocessTexture()
        {
            string ap = assetPath.Replace('\\', '/');
            if (!ap.StartsWith(Folder) && !ap.StartsWith(EditorBuild.Root + "/Resources/Player/")) return;
            Apply((TextureImporter)assetImporter, assetPath);
        }

        /// <summary>Returns true if anything had to change.</summary>
        public static bool Apply(TextureImporter ti, string path)
        {
            string file = Path.GetFileNameWithoutExtension(path);
            bool changed = false;

            TextureImporterType type = file.EndsWith("_N") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool srgb = !file.EndsWith("_N") && !file.EndsWith("_M");
            bool decal = file.StartsWith("decal_");
            bool creature = file.StartsWith("elliot_") || file.StartsWith("player_");   // baked character textures: UV atlases, full resolution
            TextureWrapMode wrap = decal || creature ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;

            if (ti.textureType != type) { ti.textureType = type; changed = true; }
            if (ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; changed = true; }
            if (ti.wrapMode != wrap) { ti.wrapMode = wrap; changed = true; }
            if (ti.alphaIsTransparency != decal) { ti.alphaIsTransparency = decal; changed = true; }
            if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; changed = true; }
            if (ti.anisoLevel != 8) { ti.anisoLevel = 8; changed = true; }
            int max = creature ? 2048 : 1024;
            if (ti.maxTextureSize != max) { ti.maxTextureSize = max; changed = true; }
            return changed;
        }

        /// <summary>Makes sure every texture uses the right settings (in case they imported before this script existed).</summary>
        public static void EnsureAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') });
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti && Apply(ti, path)) ti.SaveAndReimport();
            }
        }
    }
}
