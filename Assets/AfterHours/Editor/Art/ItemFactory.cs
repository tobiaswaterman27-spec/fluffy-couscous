using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AfterHours.Core;
using AfterHours.Items;
using AfterHours.World;

namespace AfterHours.EditorTools
{
    /// <summary>Builds item objects (simple but recognisable shapes) with physics, noise and pickup behaviour.</summary>
    public static class ItemFactory
    {
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static int counter;

        public static void Reset()
        {
            mats.Clear();
            counter = 0;
        }

        static Material M(Color c, float smooth = 0.45f, float metal = 0f)
        {
            string key = ColorUtility.ToHtmlStringRGB(c) + smooth.ToString("0.00") + metal.ToString("0.0");
            if (mats.TryGetValue(key, out Material m) && m != null) return m;
            m = ArtLibrary.Plain("Item_" + key, c, smooth, metal);
            ArtLibrary.MarkDetail(m);
            mats[key] = m;
            return m;
        }

        static void P(Transform root, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        /// <summary>Creates item 'id' lying at 'position' (the bottom of the item).</summary>
        public static ItemPickup Create(string id, Transform parent, Vector3 position, float yaw = 0f)
        {
            ItemDefinition def = ItemCatalog.Get(id);
            if (def == null)
            {
                Debug.LogError("[AfterHours] Unknown item id " + id);
                return null;
            }
            var root = new GameObject("Item: " + def.displayName);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            Material main = M(def.tint, 0.5f, def.material == SurfaceType.Metal ? 0.7f : 0f);
            Material dark = M(new Color(0.08f, 0.08f, 0.09f), 0.4f);
            Material steel = M(new Color(0.7f, 0.71f, 0.74f), 0.6f, 0.9f);
            Transform t = root.transform;
            Vector3 cyl = new Vector3(0f, 0f, 90f); // cylinder lying along X

            switch (def.shape)
            {
                case ItemShape.Key:
                    P(t, PrimitiveType.Cylinder, new Vector3(-0.022f, 0.003f, 0f), new Vector3(0.026f, 0.002f, 0.026f), main);
                    P(t, PrimitiveType.Cube, new Vector3(0.012f, 0.003f, 0f), new Vector3(0.045f, 0.004f, 0.008f), main);
                    P(t, PrimitiveType.Cube, new Vector3(0.026f, 0.003f, 0.006f), new Vector3(0.012f, 0.004f, 0.006f), main);
                    break;
                case ItemShape.Card:
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.001f, 0f), new Vector3(0.086f, 0.0015f, 0.054f), M(new Color(0.92f, 0.92f, 0.9f)));
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.0018f, 0.014f), new Vector3(0.086f, 0.0005f, 0.012f), main);
                    break;
                case ItemShape.Screwdriver:
                    P(t, PrimitiveType.Cylinder, new Vector3(-0.05f, 0.015f, 0f), new Vector3(0.03f, 0.05f, 0.03f), main, cyl);
                    P(t, PrimitiveType.Cylinder, new Vector3(0.05f, 0.015f, 0f), new Vector3(0.006f, 0.05f, 0.006f), steel, cyl);
                    break;
                case ItemShape.Pliers:
                case ItemShape.Cutters:
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.008f, 0.01f), new Vector3(0.18f, 0.012f, 0.012f), main, new Vector3(0f, 10f, 0f));
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.008f, -0.01f), new Vector3(0.18f, 0.012f, 0.012f), main, new Vector3(0f, -10f, 0f));
                    P(t, PrimitiveType.Cube, new Vector3(0.08f, 0.008f, 0f), new Vector3(0.05f, 0.014f, 0.02f), steel);
                    break;
                case ItemShape.Spanner:
                case ItemShape.Wrench:
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.005f, 0f), new Vector3(def.shape == ItemShape.Wrench ? 0.25f : 0.2f, 0.008f, 0.025f), steel);
                    P(t, PrimitiveType.Cube, new Vector3(0.11f, 0.005f, 0f), new Vector3(0.04f, 0.01f, 0.05f), steel);
                    break;
                case ItemShape.PryBar:
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.012f, 0f), new Vector3(0.36f, 0.02f, 0.02f), main);
                    P(t, PrimitiveType.Cube, new Vector3(0.19f, 0.03f, 0f), new Vector3(0.05f, 0.02f, 0.02f), main, new Vector3(0f, 0f, 50f));
                    break;
                case ItemShape.Knife:
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.008f, 0f), new Vector3(0.12f, 0.015f, 0.025f), main);
                    P(t, PrimitiveType.Cube, new Vector3(0.07f, 0.008f, 0f), new Vector3(0.025f, 0.004f, 0.018f), steel);
                    break;
                case ItemShape.Fuse:
                    P(t, PrimitiveType.Cylinder, new Vector3(0f, 0.007f, 0f), new Vector3(0.013f, 0.025f, 0.013f), main, cyl);
                    P(t, PrimitiveType.Cylinder, new Vector3(-0.024f, 0.007f, 0f), new Vector3(0.014f, 0.004f, 0.014f), steel, cyl);
                    P(t, PrimitiveType.Cylinder, new Vector3(0.024f, 0.007f, 0f), new Vector3(0.014f, 0.004f, 0.014f), steel, cyl);
                    break;
                case ItemShape.Module:
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.004f, 0f), new Vector3(0.1f, 0.006f, 0.07f), main);
                    P(t, PrimitiveType.Cube, new Vector3(0.01f, 0.009f, 0f), new Vector3(0.025f, 0.006f, 0.025f), dark);
                    P(t, PrimitiveType.Cube, new Vector3(-0.025f, 0.009f, 0.015f), new Vector3(0.02f, 0.005f, 0.012f), dark);
                    break;
                case ItemShape.Connector:
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(0.06f, 0.04f, 0.04f), main);
                    P(t, PrimitiveType.Cylinder, new Vector3(0.08f, 0.012f, 0f), new Vector3(0.012f, 0.05f, 0.012f), dark, cyl);
                    break;
                case ItemShape.Battery:
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.15f, 0.1f, 0.08f), main);
                    P(t, PrimitiveType.Cylinder, new Vector3(0.04f, 0.105f, 0f), new Vector3(0.02f, 0.006f, 0.02f), M(new Color(0.7f, 0.1f, 0.1f)));
                    break;
                case ItemShape.Handle:
                    P(t, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.035f, 0.2f, 0.035f), steel, cyl);
                    P(t, PrimitiveType.Cylinder, new Vector3(-0.17f, 0.02f, 0f), new Vector3(0.05f, 0.05f, 0.05f), main, cyl);
                    break;
                case ItemShape.Gear:
                    P(t, PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0f), new Vector3(0.14f, 0.012f, 0.14f), steel);
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i * 36f;
                        Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0.075f, 0.012f, 0f);
                        P(t, PrimitiveType.Cube, p, new Vector3(0.025f, 0.024f, 0.02f), steel, new Vector3(0f, a, 0f));
                    }
                    break;
                case ItemShape.Switch:
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(0.06f, 0.04f, 0.08f), main);
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.012f, 0.03f, 0.012f), steel, new Vector3(20f, 0f, 0f));
                    break;
                case ItemShape.Token:
                case ItemShape.Coin:
                    float dia = def.shape == ItemShape.Coin ? 0.023f : 0.04f;
                    P(t, PrimitiveType.Cylinder, new Vector3(0f, 0.002f, 0f), new Vector3(dia, 0.0015f, dia), main);
                    break;
                case ItemShape.Dial:
                    P(t, PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0f), new Vector3(0.07f, 0.01f, 0.07f), main);
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.021f, 0.02f), new Vector3(0.006f, 0.004f, 0.02f), steel);
                    break;
                case ItemShape.Can:
                    P(t, PrimitiveType.Cylinder, new Vector3(0f, 0.058f, 0f), new Vector3(0.066f, 0.058f, 0.066f), main);
                    break;
                case ItemShape.Bottle:
                    P(t, PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(0.07f, 0.08f, 0.07f), main);
                    P(t, PrimitiveType.Cylinder, new Vector3(0f, 0.19f, 0f), new Vector3(0.025f, 0.035f, 0.025f), main);
                    break;
                case ItemShape.AlarmClock:
                    P(t, PrimitiveType.Cylinder, new Vector3(0f, 0.055f, 0f), new Vector3(0.1f, 0.025f, 0.1f), main, new Vector3(90f, 0f, 0f));
                    P(t, PrimitiveType.Sphere, new Vector3(-0.03f, 0.11f, 0f), new Vector3(0.035f, 0.035f, 0.035f), steel);
                    P(t, PrimitiveType.Sphere, new Vector3(0.03f, 0.11f, 0f), new Vector3(0.035f, 0.035f, 0.035f), steel);
                    break;
                case ItemShape.Phone:
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.01f, 0f), new Vector3(0.05f, 0.02f, 0.11f), main);
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.021f, 0.02f), new Vector3(0.035f, 0.002f, 0.04f), M(new Color(0.3f, 0.4f, 0.35f), 0.8f));
                    break;
                case ItemShape.Valve:
                    P(t, PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.32f, 0.012f, 0.32f), main);
                    P(t, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(0.06f, 0.03f, 0.06f), steel);
                    break;
                case ItemShape.Remote:
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.01f, 0f), new Vector3(0.05f, 0.02f, 0.12f), main);
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.021f, 0.03f), new Vector3(0.015f, 0.003f, 0.015f), M(new Color(0.7f, 0.1f, 0.1f)));
                    break;
                case ItemShape.Jerrycan:
                    // A 20 l steel jerry can: flat-sided body with pressed X ribs, three-bar handle, spout cap.
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.175f, 0f), new Vector3(0.34f, 0.35f, 0.16f), main);
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.17f, 0.081f), new Vector3(0.36f, 0.012f, 0.004f), main, new Vector3(0f, 0f, 42f));
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.17f, 0.081f), new Vector3(0.36f, 0.012f, 0.004f), main, new Vector3(0f, 0f, -42f));
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.17f, -0.081f), new Vector3(0.36f, 0.012f, 0.004f), main, new Vector3(0f, 0f, 42f));
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.17f, -0.081f), new Vector3(0.36f, 0.012f, 0.004f), main, new Vector3(0f, 0f, -42f));
                    for (int i = -1; i <= 1; i++)
                        P(t, PrimitiveType.Cube, new Vector3(-0.06f + i * 0.05f, 0.375f, 0f), new Vector3(0.018f, 0.05f, 0.03f), main);
                    P(t, PrimitiveType.Cube, new Vector3(-0.06f, 0.4f, 0f), new Vector3(0.16f, 0.014f, 0.03f), main);
                    P(t, PrimitiveType.Cylinder, new Vector3(0.12f, 0.37f, 0f), new Vector3(0.05f, 0.025f, 0.05f), dark);
                    break;
                case ItemShape.Axe:
                    // Fire axe: long ash handle, red head with a blade and a pick.
                    P(t, PrimitiveType.Cylinder, new Vector3(0f, 0.022f, 0f), new Vector3(0.036f, 0.43f, 0.03f), M(new Color(0.62f, 0.47f, 0.3f), 0.35f), cyl);
                    P(t, PrimitiveType.Cube, new Vector3(0.38f, 0.024f, 0f), new Vector3(0.06f, 0.035f, 0.1f), main);
                    P(t, PrimitiveType.Cube, new Vector3(0.38f, 0.024f, 0.1f), new Vector3(0.11f, 0.03f, 0.1f), main);
                    P(t, PrimitiveType.Cube, new Vector3(0.38f, 0.024f, 0.152f), new Vector3(0.14f, 0.026f, 0.012f), steel);
                    P(t, PrimitiveType.Cube, new Vector3(0.38f, 0.024f, -0.1f), new Vector3(0.03f, 0.025f, 0.12f), main);
                    break;
                default:
                    P(t, PrimitiveType.Cube, new Vector3(0f, 0.09f, 0f), new Vector3(0.25f, 0.18f, 0.2f), main);
                    break;
            }

            // One box collider around the whole item.
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                Bounds rb = r.bounds;
                Vector3 lmin = root.transform.InverseTransformPoint(rb.min), lmax = root.transform.InverseTransformPoint(rb.max);
                Bounds lb = new Bounds((lmin + lmax) * 0.5f, new Vector3(Mathf.Abs(lmax.x - lmin.x), Mathf.Abs(lmax.y - lmin.y), Mathf.Abs(lmax.z - lmin.z)));
                if (first) { b = lb; first = false; } else b.Encapsulate(lb);
            }
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = Vector3.Max(b.size, new Vector3(0.01f, 0.006f, 0.01f));

            // Small items get a larger invisible aiming zone so they are easy to look at and pick up.
            if (Mathf.Max(b.size.x, b.size.z) < 0.15f)
            {
                SphereCollider aim = root.AddComponent<SphereCollider>();
                aim.isTrigger = true;
                aim.radius = 0.1f;
                aim.center = b.center;
            }

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = def.mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            PhysicsProp prop = root.AddComponent<PhysicsProp>();
            prop.material = def.material;
            prop.speedBasedNoise = true;
            prop.noiseScale = def.category == ItemCategory.Distraction ? 6f : 4f;
            root.AddComponent<SurfaceMaterial>().type = def.material;

            if (def.shape == ItemShape.AlarmClock || def.shape == ItemShape.Phone)
            {
                TimedDistraction td = root.AddComponent<TimedDistraction>();
                td.isPhone = def.shape == ItemShape.Phone;
                td.delay = td.isPhone ? 12f : 8f;
            }
            if (def.shape == ItemShape.Bottle) root.AddComponent<Shatterable>();

            ItemPickup pick = root.AddComponent<ItemPickup>();
            pick.itemId = id;
            pick.instanceId = id + "#" + (++counter);
            EditorBuild.SetLayerRecursive(root, GameLayers.PropLayer);
            return pick;
        }
    }
}
