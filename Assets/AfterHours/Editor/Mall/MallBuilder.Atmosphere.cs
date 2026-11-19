using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.World;
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

        /// <summary>Leaks: water dripping from the car park ceiling and a couple of service corridors into puddles.</summary>
        static void BuildDrips(Transform parent)
        {
            Transform g = Group("Leaks", parent);
            Material drop = AdditiveMaterial("Water_Drop", "decal_dust_mote.png", new Color(0.7f, 0.78f, 0.85f, 0.5f));
            var spots = new List<(Vector3 floor, float height)>
            {
                (new Vector3(12f, MallLayout.CarParkY, -18f), 3.5f), (new Vector3(-44f, MallLayout.CarParkY, -60f), 3.5f),
                (new Vector3(66f, MallLayout.CarParkY, 34f), 3.5f), (new Vector3(-20f, MallLayout.CarParkY, 30f), 3.5f),
                (new Vector3(30f, MallLayout.CarParkY, 62f), 3.5f), (new Vector3(-80f, MallLayout.CarParkY, -10f), 3.5f),
                (new Vector3(-74f, MallLayout.CarParkY, 82f), 1.9f), (new Vector3(84f, MallLayout.CarParkY, -76f), 3.5f),
                (MallLayout.WingPoint(WingId.North, 64f, 17.6f, 0f), 4.4f), (MallLayout.WingPoint(WingId.West, 82f, -17.4f, 6f), 4.4f),
            };
            foreach (var (floor, height) in spots)
            {
                var go = new GameObject("Drip");
                go.transform.SetParent(g, false);
                go.transform.position = floor + Vector3.up * height;
                AfterHours.World.Drip d = go.AddComponent<AfterHours.World.Drip>();
                d.dropMaterial = drop;
                d.height = height;
                DecalQuad(g, floor + Vector3.up * 0.015f, Vector3.up, Rand(0.9f, 1.6f), Rand(0.9f, 1.6f), Rand(0f, 360f), decalPuddle);
                DecalQuad(g, floor + Vector3.up * (height - 0.02f), Vector3.down, 0.9f, 0.9f, Rand(0f, 360f), decalWater);
            }
            // The atrium skylight leaks in the storm: drops falling the full height of the atrium onto the ground floor,
            // one of them into a bucket somebody put out (knock it over and it's loud).
            float roofDrop = MallLayout.RoofY - 0.45f;
            Vector3[] skyLeaks = { new Vector3(7.4f, 0f, 6.3f), new Vector3(-8.2f, 0f, -4.4f), new Vector3(2.8f, 0f, -8.7f) };
            for (int i = 0; i < skyLeaks.Length; i++)
            {
                Vector3 floor = skyLeaks[i];
                var go = new GameObject("Skylight leak");
                go.transform.SetParent(g, false);
                go.transform.position = floor + Vector3.up * roofDrop;
                AfterHours.World.Drip d = go.AddComponent<AfterHours.World.Drip>();
                d.dropMaterial = drop;
                d.height = roofDrop;
                d.rainFed = true;
                d.minInterval = 0.8f; d.maxInterval = 2.6f;
                DecalQuad(g, floor + Vector3.up * 0.015f, Vector3.up, Rand(1.2f, 2f), Rand(1.0f, 1.8f), Rand(0f, 360f), decalPuddle);
                if (i == 0) LeakBucket(g, floor);
            }
        }

        /// <summary>A grey mop bucket left under a leak.</summary>
        static void LeakBucket(Transform g, Vector3 at)
        {
            Material grey = Mat("Bucket plastic", new Color(0.45f, 0.47f, 0.5f), 0.35f);
            GameObject r = KnockRoot(g, "Bucket", at, 20f, 2.5f, SurfaceType.Metal, 0.32f, 0.4f, 1.6f);
            KPart(r, PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f), new Vector3(0.3f, 0.15f, 0.3f), grey);
            KPart(r, PrimitiveType.Cylinder, new Vector3(0f, 0.24f, 0f), new Vector3(0.27f, 0.005f, 0.27f), Mat("Bucket water", new Color(0.18f, 0.2f, 0.22f), 0.95f));
            KDone(r, new Vector3(0f, 0.16f, 0f), new Vector3(0.3f, 0.32f, 0.3f));
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
