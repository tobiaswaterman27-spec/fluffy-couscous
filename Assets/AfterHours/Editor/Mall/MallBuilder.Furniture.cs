using System.Collections.Generic;
using UnityEngine;
using AfterHours.Core;
using AfterHours.World;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Real furniture in place of blocks: every "Bench" box becomes a slatted bench on cast frames (its collider kept),
    /// and the office chairs are swivel chairs - five-star base on castors, gas lift, padded seat and back, arms.
    /// </summary>
    public static partial class MallBuilder
    {
        static void RefineFurniture(Transform root)
        {
            Material slat = ArtLibrary.Plain("Bench_Slat", new Color(0.42f, 0.28f, 0.17f), 0.35f);
            Material frame = ArtLibrary.Plain("Bench_Frame", new Color(0.12f, 0.12f, 0.13f), 0.5f, 0.7f);
            var benches = new List<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Bench" && t.GetComponent<MeshRenderer>() != null && t.GetComponent<Collider>() != null) benches.Add(t.gameObject);

            foreach (GameObject b in benches)
            {
                Bounds bb = b.GetComponent<Renderer>().bounds;
                Transform parent = b.transform.parent;
                Object.DestroyImmediate(b.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(b.GetComponent<MeshFilter>());
                bool alongX = bb.size.x >= bb.size.z;
                float len = alongX ? bb.size.x : bb.size.z, wid = alongX ? bb.size.z : bb.size.x;
                Vector3 along = alongX ? Vector3.right : Vector3.forward, across = alongX ? Vector3.forward : Vector3.right;
                Quaternion rot = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                float top = bb.max.y, floor = bb.min.y;
                var g = new GameObject("Bench (slatted)").transform;
                g.SetParent(parent, false);
                g.position = Vector3.zero; g.rotation = Quaternion.identity;
                Vector3 L(Vector3 world) => parent != null ? parent.InverseTransformPoint(world) : world;

                // four slats with gaps
                int n = 4;
                float sw = wid / n;
                for (int k = 0; k < n; k++)
                {
                    Vector3 c = new Vector3(bb.center.x, top - 0.02f, bb.center.z) + across * (-wid * 0.5f + sw * (k + 0.5f));
                    Detail("Bench slat", g, L(c), new Vector3(len, 0.035f, sw - 0.025f), slat, rot);
                }
                // frames: ends, plus a middle one on long benches
                var frames = new List<float> { -len * 0.5f + 0.12f, len * 0.5f - 0.12f };
                if (len > 2.6f) frames.Add(0f);
                foreach (float f in frames)
                {
                    Vector3 c = new Vector3(bb.center.x, 0f, bb.center.z) + along * f;
                    Detail("Bench rail", g, L(c + Vector3.up * (top - 0.055f)), new Vector3(0.05f, 0.04f, wid - 0.02f), frame, rot);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 foot = c + across * s * (wid * 0.5f - 0.05f);
                        Detail("Bench leg", g, L(foot + Vector3.up * (floor + (top - floor - 0.07f) * 0.5f)), new Vector3(0.05f, top - floor - 0.07f, 0.05f), frame, rot);
                        Detail("Bench foot", g, L(foot + Vector3.up * (floor + 0.01f)), new Vector3(0.09f, 0.02f, 0.09f), frame, rot);
                    }
                }
                // under-rail tying the frames together
                Detail("Bench stretcher", g, L(new Vector3(bb.center.x, floor + 0.12f, bb.center.z)), new Vector3(len - 0.3f, 0.03f, 0.03f), frame, rot);
            }
        }

        /// <summary>A swivel office chair as one physics prop (knock it, push it about), facing 'yaw'.</summary>
        static void OfficeChairProp(Transform g, Vector3 floorCentre, float yaw, float mass, SurfaceType s)
        {
            Material fabric = ArtLibrary.Plain("OfficeChair_Fabric", new Color(0.12f, 0.13f, 0.16f), 0.15f);
            Material plastic = ArtLibrary.Plain("OfficeChair_Plastic", new Color(0.05f, 0.05f, 0.055f), 0.5f);
            Material chrome = ArtLibrary.Plain("OfficeChair_Chrome", new Color(0.7f, 0.72f, 0.74f), 0.8f, 1f);
            var root = new GameObject("Office chair");
            root.transform.SetParent(g, false);
            root.transform.localPosition = floorCentre + Vector3.up * 0.01f;
            // swivelled a bit, as it was left
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            void Part(string n, Vector3 c, Vector3 size, Material m, Quaternion? r = null)
            {
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = n; Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.SetParent(root.transform, false);
                p.transform.localPosition = c; p.transform.localRotation = r ?? Quaternion.identity; p.transform.localScale = size;
                p.GetComponent<Renderer>().sharedMaterial = m;
            }
            void Rod(string n, Vector3 a, Vector3 b, float d, Material m)
            {
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                p.name = n; Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.SetParent(root.transform, false);
                Vector3 dv = b - a;
                p.transform.localPosition = (a + b) * 0.5f;
                p.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dv.normalized);
                p.transform.localScale = new Vector3(d, dv.magnitude * 0.5f, d);
                p.GetComponent<Renderer>().sharedMaterial = m;
            }
            // five-star base and castors
            for (int k = 0; k < 5; k++)
            {
                Quaternion arm = Quaternion.Euler(0f, k * 72f + 18f, 0f);
                Vector3 tip = arm * Vector3.forward * 0.3f;
                Rod("Base arm", new Vector3(0f, 0.09f, 0f), tip + Vector3.up * 0.06f, 0.045f, plastic);
                Rod("Castor", tip + Vector3.up * 0.03f - arm * Vector3.right * 0.025f, tip + Vector3.up * 0.03f + arm * Vector3.right * 0.025f, 0.055f, plastic);
            }
            Rod("Gas lift", new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0.42f, 0f), 0.05f, chrome);
            Part("Seat mechanism", new Vector3(0f, 0.43f, 0f), new Vector3(0.2f, 0.04f, 0.22f), plastic);
            Part("Seat", new Vector3(0f, 0.48f, 0.02f), new Vector3(0.48f, 0.08f, 0.46f), fabric);
            Part("Back spine", new Vector3(0f, 0.58f, -0.24f), new Vector3(0.06f, 0.24f, 0.03f), plastic, Quaternion.Euler(-8f, 0f, 0f));
            Part("Backrest", new Vector3(0f, 0.8f, -0.26f), new Vector3(0.44f, 0.42f, 0.07f), fabric, Quaternion.Euler(-8f, 0f, 0f));
            foreach (float sx in new[] { -0.25f, 0.25f })
            {
                Part("Arm post", new Vector3(sx, 0.57f, 0f), new Vector3(0.03f, 0.18f, 0.04f), plastic);
                Part("Arm pad", new Vector3(sx, 0.665f, 0.02f), new Vector3(0.06f, 0.025f, 0.24f), plastic);
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, -0.02f);
            col.size = new Vector3(0.56f, 1.0f, 0.6f);
            root.AddComponent<SurfaceMaterial>().type = s;
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            root.AddComponent<PhysicsProp>().material = s;
            EditorBuild.SetLayerRecursive(root, GameLayers.PropLayer);
        }
    }
}
