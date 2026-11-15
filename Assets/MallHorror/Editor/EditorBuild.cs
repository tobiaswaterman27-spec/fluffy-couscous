using System.IO;
using UnityEditor;
using UnityEngine;
using MallHorror.World;

namespace MallHorror.EditorTools
{
    /// <summary>Shared helpers for the scene-building tools.</summary>
    public static class EditorBuild
    {
        public const string Root = "Assets/MallHorror";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Creates (or updates) a lit material. Works with URP, HDRP or the built-in pipeline.</summary>
        public static Material Material(string folder, string name, Color color, float smoothness = 0.3f, float metallic = 0f, Color? emission = null)
        {
            EnsureFolder(folder);
            string path = folder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("HDRP/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Solid static box with an exactly matching BoxCollider.</summary>
        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, SurfaceType surface, Quaternion? rotation = null, bool isStatic = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.AddComponent<SurfaceMaterial>().type = surface;
            if (isStatic)
            {
                GameObjectUtility.SetStaticEditorFlags(go,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            return go;
        }

        /// <summary>A sloped slab whose top surface runs from topStart to topEnd.</summary>
        public static GameObject Ramp(string name, Transform parent, Vector3 topStart, Vector3 topEnd, float width, float thickness, Material mat, SurfaceType surface)
        {
            Vector3 dir = topEnd - topStart;
            Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 normal = rot * Vector3.up;
            Vector3 center = (topStart + topEnd) * 0.5f - normal * (thickness * 0.5f);
            return Box(name, parent, center, new Vector3(width, thickness, dir.magnitude), mat, surface, rot);
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            if (layer < 0) return;
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }

        public static void Label(Transform parent, string text, Vector3 position, float yaw, float size = 0.06f)
        {
            var go = new GameObject("Label - " + text.Split('\n')[0]);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = size;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.85f, 0.3f);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }
    }
}
