using UnityEngine;
using AfterHours.Core;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Things standing about that go over when someone runs into them (see Knockable): wet-floor signs left by
    /// the cleaners, pedal bins, shops' chalk A-boards, stacks of boxes in stock rooms. All loud when they fall.
    /// </summary>
    public static partial class MallBuilder
    {
        static GameObject KnockRoot(Transform g, string name, Vector3 at, float yaw, float mass, SurfaceType s, float height, float topHeavy, float noise = 1.4f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(g, false);
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            PhysicsProp pp = root.AddComponent<PhysicsProp>();
            pp.material = s;
            pp.noiseScale = noise;
            Knockable k = root.AddComponent<Knockable>();
            k.height = height;
            k.topHeavy = topHeavy;
            return root;
        }

        static void KPart(GameObject root, PrimitiveType type, Vector3 c, Vector3 size, Material m, Vector3? euler = null)
        {
            GameObject b = GameObject.CreatePrimitive(type);
            b.name = "Part";
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(root.transform, false);
            b.transform.localPosition = c;
            b.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            b.transform.localScale = size;
            b.GetComponent<Renderer>().sharedMaterial = m;
        }

        static void KDone(GameObject root, Vector3 colCentre, Vector3 colSize)
        {
            var col = root.AddComponent<BoxCollider>();
            col.center = colCentre;
            col.size = colSize;
            EditorBuild.SetLayerRecursive(root, GameLayers.PropLayer);
        }

        /// <summary>A yellow folding "CAUTION WET FLOOR" sign.</summary>
        static void WetFloorSign(Transform g, Vector3 at, float yaw)
        {
            Material yellow = Mat("Wet floor sign", new Color(0.95f, 0.78f, 0.08f), 0.45f);
            GameObject r = KnockRoot(g, "Wet floor sign", at, yaw, 1.3f, SurfaceType.Wood, 0.62f, 0.62f, 1.2f);
            KPart(r, PrimitiveType.Cube, new Vector3(0f, 0.31f, 0.075f), new Vector3(0.3f, 0.62f, 0.012f), yellow, new Vector3(-13f, 0f, 0f));
            KPart(r, PrimitiveType.Cube, new Vector3(0f, 0.31f, -0.075f), new Vector3(0.3f, 0.62f, 0.012f), yellow, new Vector3(13f, 0f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                float yawFace = yaw + (s > 0 ? 0f : 180f);
                Vector3 n = Quaternion.Euler(0f, yawFace, 0f) * Vector3.forward;
                TextMesh t = EditorBuild.Label(r.transform, "CAUTION\nWET FLOOR", at + Vector3.up * 0.38f + n * 0.09f, yawFace + 180f, 0.012f, false);
                t.color = new Color(0.08f, 0.08f, 0.08f);
                t.transform.SetParent(r.transform, true);
                t.transform.localRotation = t.transform.localRotation * Quaternion.Euler(13f * s, 0f, 0f);
            }
            KDone(r, new Vector3(0f, 0.31f, 0f), new Vector3(0.3f, 0.62f, 0.3f));
        }

        /// <summary>A steel pedal bin (half full: heavy enough to rock, light enough to go over).</summary>
        static void KnockBin(Transform g, Vector3 at)
        {
            GameObject r = KnockRoot(g, "Bin", at, 0f, 7f, SurfaceType.Metal, 0.9f, 0.55f, 1.8f);
            KPart(r, PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f), new Vector3(0.42f, 0.42f, 0.42f), metal);
            KPart(r, PrimitiveType.Cylinder, new Vector3(0f, 0.87f, 0f), new Vector3(0.44f, 0.03f, 0.44f), blackMat);
            KPart(r, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0.23f), new Vector3(0.12f, 0.02f, 0.08f), blackMat);
            KDone(r, new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.9f, 0.42f));
        }

        static readonly string[] ABoardText = { "SALE\nNOW ON", "UP TO\n50% OFF", "NEW IN", "OPEN\nLATE THURS", "COFFEE\n2.20", "EVERYTHING\nMUST GO", "BUY 1\nGET 1 FREE" };

        /// <summary>A shop's chalk A-board out on the concourse.</summary>
        static void ABoard(Transform g, Vector3 at, float yaw, int pick)
        {
            Material frame = Mat("A-board frame", new Color(0.32f, 0.22f, 0.14f), 0.3f);
            Material chalk = Mat("A-board slate", new Color(0.06f, 0.07f, 0.07f), 0.15f);
            GameObject r = KnockRoot(g, "A-board", at, yaw, 6f, SurfaceType.Wood, 1.0f, 0.6f, 1.5f);
            for (int s = -1; s <= 1; s += 2)
            {
                KPart(r, PrimitiveType.Cube, new Vector3(0f, 0.5f, s * 0.12f), new Vector3(0.6f, 1.0f, 0.03f), frame, new Vector3(-13f * s, 0f, 0f));
                KPart(r, PrimitiveType.Cube, new Vector3(0f, 0.52f, s * 0.135f), new Vector3(0.5f, 0.8f, 0.01f), chalk, new Vector3(-13f * s, 0f, 0f));
                float yawFace = yaw + (s > 0 ? 0f : 180f);
                Vector3 n = Quaternion.Euler(0f, yawFace, 0f) * Vector3.forward;
                TextMesh t = EditorBuild.Label(r.transform, ABoardText[(pick + (s > 0 ? 0 : 1)) % ABoardText.Length], at + Vector3.up * 0.55f + n * 0.15f, yawFace + 180f, 0.014f, false);
                t.color = new Color(0.92f, 0.92f, 0.88f);
                t.transform.SetParent(r.transform, true);
                t.transform.localRotation = t.transform.localRotation * Quaternion.Euler(13f * s, 0f, 0f);
            }
            KDone(r, new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 1.0f, 0.42f));
        }

        /// <summary>Two or three cardboard boxes stacked up, each one its own object (they topple separately).</summary>
        static void BoxStack(Transform g, Vector3 at, float yaw, int h)
        {
            Material card = Mat("Box cardboard", new Color(0.62f, 0.47f, 0.3f), 0.2f);
            int n = 2 + Mathf.Abs(h) % 2;
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 size = new Vector3(0.5f - i * 0.05f, 0.35f, 0.4f - i * 0.03f);
                GameObject r = KnockRoot(g, "Box", at + Vector3.up * y, yaw + (h >> (i + 1) & 7) * 3f - 9f, 3.5f, SurfaceType.Wood, size.y, 0.5f, 1.2f);
                KPart(r, PrimitiveType.Cube, new Vector3(0f, size.y * 0.5f, 0f), size, card);
                KPart(r, PrimitiveType.Cube, new Vector3(0f, size.y + 0.001f, 0f), new Vector3(size.x * 1.001f, 0.002f, 0.06f), Mat("Box tape", new Color(0.75f, 0.6f, 0.35f), 0.6f));
                KDone(r, new Vector3(0f, size.y * 0.5f, 0f), size);
                y += size.y + 0.002f;
            }
        }
    }
}
