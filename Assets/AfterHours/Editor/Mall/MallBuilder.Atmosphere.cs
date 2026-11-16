using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Atmosphere you can see: soft shafts of moonlight slanting down through the atrium skylight (dust hanging in
    /// them), and a faint glow haze under the brightest lamps. Additive, unlit and never baked.
    /// </summary>
    public static partial class MallBuilder
    {
        static Material AdditiveMaterial(string name, string texFile, Color tint)
        {
            string path = EditorBuild.Root + "/Materials/Art/" + name + ".mat";
            EditorBuild.EnsureFolder(EditorBuild.Root + "/Materials/Art");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (m == null)
            {
                m = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(m, path);
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(EditorBuild.Root + "/Textures/Generated/" + texFile);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            m.color = tint;
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.One);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent + 10;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void BuildAtmosphere(Transform parent)
        {
            Transform g = Group("Atmosphere", parent);
            Material beam = AdditiveMaterial("Moon_Beam", "decal_light_beam.png", new Color(0.55f, 0.65f, 0.85f, 0.07f));
            Vector3 dir = Quaternion.Euler(38f, 145f, 0f) * Vector3.forward; // the way the moonlight travels
            float top = MallLayout.RoofY - 0.4f;
            int k = 0;
            for (float x = -7.5f; x <= 7.6f; x += 5f)
            {
                for (float z = -7.5f; z <= 7.6f; z += 5f)
                {
                    if ((k++ * 7) % 3 == 0) continue; // not every pane: uneven, natural
                    Vector3 start = new Vector3(x, top, z);
                    float length = (top - 0.2f) / Mathf.Max(0.2f, -dir.y);
                    Shaft(g, start, dir, length, 2.2f, beam);
                }
            }
        }

        /// <summary>A light shaft: two crossed soft quads running from 'start' along 'dir'.</summary>
        static void Shaft(Transform g, Vector3 start, Vector3 dir, float length, float width, Material m)
        {
            Vector3 centre = start + dir * (length * 0.5f);
            for (int i = 0; i < 2; i++)
            {
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Light shaft";
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(g, false);
                q.transform.position = centre;
                // Quad's up (+Y, where the texture's top is) points back to the source; spun 90 degrees for the second.
                q.transform.rotation = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up).normalized, -dir) * Quaternion.Euler(0f, 0f, 0f);
                q.transform.Rotate(-dir, i * 90f, Space.World);
                q.transform.localScale = new Vector3(width, length, 1f);
                Renderer r = q.GetComponent<Renderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                GameObjectUtility.SetStaticEditorFlags(q, 0);
            }
        }
    }
}
