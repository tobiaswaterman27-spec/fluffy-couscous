using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.World;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Cars left in the car park overnight: twenty or so scattered through the bays - staff cars, a van, one with
    /// its hazard lights still blinking on a dying battery. Solid (good to crouch behind), one mesh per material.
    /// </summary>
    public static partial class MallBuilder
    {
        static Material[] carPaints;
        static Material carChrome, carIndicator, carTail, carPlate, carPlateRear, carTyre;
        static int carsParked;
        const int MaxCars = 18;

        static void CarMaterials()
        {
            Color[] cols =
            {
                new Color(0.5f, 0.52f, 0.55f), new Color(0.15f, 0.2f, 0.3f), new Color(0.45f, 0.08f, 0.07f), new Color(0.75f, 0.75f, 0.72f),
                new Color(0.1f, 0.1f, 0.1f), new Color(0.2f, 0.3f, 0.22f), new Color(0.6f, 0.55f, 0.42f), new Color(0.82f, 0.82f, 0.8f),
            };
            carPaints = new Material[cols.Length];
            for (int i = 0; i < cols.Length; i++) carPaints[i] = ArtLibrary.Plain("Car_Paint_" + i, cols[i], 0.8f, 0.4f);
            carChrome = ArtLibrary.Plain("Car_Chrome", new Color(0.7f, 0.72f, 0.74f), 0.9f, 1f);
            carIndicator = ArtLibrary.Plain("Car_Indicator", new Color(0.9f, 0.55f, 0.1f), 0.7f, 0f, new Color(0f, 0f, 0f), false);
            carTail = ArtLibrary.Plain("Car_TailLight", new Color(0.5f, 0.03f, 0.03f), 0.85f);
            carPlate = ArtLibrary.Plain("Car_Plate", new Color(0.9f, 0.9f, 0.88f), 0.5f);
            carPlateRear = ArtLibrary.Plain("Car_Plate_Rear", new Color(0.92f, 0.8f, 0.15f), 0.5f);
            carTyre = ArtLibrary.Plain("Car_Tyre", new Color(0.05f, 0.05f, 0.05f), 0.2f);
        }

        /// <summary>A parked car on the floor at 'floor', nose along 'yaw'. kind: 0 hatchback, 1 saloon, 2 small van.</summary>
        static void ParkedCar(Transform parent, Vector3 floor, float yaw, int kind, Material paint, bool hazards)
        {
            var root = new GameObject(kind == 2 ? "Parked van" : "Parked car");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(floor, Quaternion.Euler(0f, yaw, 0f));
            var k = new MeshKit();
            k.Frame(floor, Quaternion.Euler(0f, yaw, 0f));

            float L = kind == 1 ? 4.5f : kind == 2 ? 4.6f : 4.0f, W = kind == 2 ? 1.9f : 1.78f;
            float hl = L * 0.5f, hw = W * 0.5f;
            // body: sills, waist, bonnet and boot
            k.Box(new Vector3(0f, 0.52f, 0f), new Vector3(W, 0.56f, L), paint);
            k.Box(new Vector3(0f, 0.27f, 0f), new Vector3(W - 0.12f, 0.08f, L - 0.9f), carTyre, 0f, false);          // underside shadow
            // cabin
            float cabLen = kind == 0 ? 2.1f : kind == 1 ? 2.2f : 3.2f;
            float cabZ = kind == 0 ? -0.35f : kind == 1 ? -0.1f : -0.6f;
            float cabH = kind == 2 ? 0.95f : 0.55f;
            float cabTop = 0.8f + cabH;
            k.Box(new Vector3(0f, 0.8f + cabH * 0.5f, cabZ), new Vector3(W - 0.14f, cabH, cabLen), darkGlass);
            k.Box(new Vector3(0f, cabTop + 0.03f, cabZ - 0.05f), new Vector3(W - 0.2f, 0.06f, cabLen - 0.25f), paint);   // roof
            // pillars between the windows
            for (int s = -1; s <= 1; s += 2)
            {
                float px = s * (hw - 0.065f);
                k.Box(new Vector3(px, 0.8f + cabH * 0.5f, cabZ + cabLen * 0.5f - 0.06f), new Vector3(0.05f, cabH, 0.1f), paint);
                k.Box(new Vector3(px, 0.8f + cabH * 0.5f, cabZ - cabLen * 0.5f + 0.06f), new Vector3(0.05f, cabH, 0.12f), paint);
                if (kind != 2) k.Box(new Vector3(px, 0.8f + cabH * 0.5f, cabZ + 0.1f), new Vector3(0.05f, cabH, 0.08f), paint);
                // door shut lines and handles
                k.Box(new Vector3(s * (hw + 0.002f), 0.55f, cabZ + 0.1f), new Vector3(0.006f, 0.48f, 0.012f), carTyre, 0f, false);
                k.Box(new Vector3(s * (hw + 0.012f), 0.7f, cabZ + 0.55f), new Vector3(0.02f, 0.03f, 0.16f), carChrome, 0f, false);
                k.Box(new Vector3(s * (hw + 0.012f), 0.7f, cabZ - 0.45f), new Vector3(0.02f, 0.03f, 0.16f), carChrome, 0f, false);
                // wing mirror
                k.Box(new Vector3(s * (hw + 0.1f), 0.95f, cabZ + cabLen * 0.5f - 0.15f), new Vector3(0.16f, 0.1f, 0.08f), paint);
                // wheels (tyre, hub)
                for (int e = -1; e <= 1; e += 2)
                {
                    float wz = e * (hl - (kind == 0 ? 0.72f : 0.85f));
                    k.Bar(new Vector3(s * (hw - 0.2f), 0.32f, wz), new Vector3(s * (hw + 0.01f), 0.32f, wz), 0.32f, carTyre, 16);
                    k.Disc(new Vector3(s * (hw + 0.01f), 0.32f, wz), new Vector3(s, 0f, 0f), 0.32f, carTyre, 16);
                    k.Disc(new Vector3(s * (hw + 0.015f), 0.32f, wz), new Vector3(s, 0f, 0f), 0.19f, carChrome, 16);
                    k.Disc(new Vector3(s * (hw + 0.02f), 0.32f, wz), new Vector3(s, 0f, 0f), 0.06f, carTyre, 8);
                }
                // lamps
                k.Box(new Vector3(s * (hw - 0.3f), 0.68f, hl + 0.005f), new Vector3(0.38f, 0.13f, 0.02f), whiteFixture, 0f, false);
                k.Box(new Vector3(s * (hw - 0.27f), 0.7f, -hl - 0.005f), new Vector3(0.32f, 0.14f, 0.02f), carTail, 0f, false);
            }
            // sloped screens: a hint of rake at the front and back of the cabin
            float rake = kind == 2 ? 16f : 48f, ph = kind == 2 ? cabH * 0.98f : cabH * 0.95f;
            float rakeOut = ph * 0.5f * Mathf.Sin(rake * Mathf.Deg2Rad) + 0.01f;   // its top edge meets the front of the roof
            k.Box(new Vector3(0f, kind == 2 ? 0.8f + cabH * 0.5f : 0.8f + cabH * 0.42f, cabZ + cabLen * 0.5f + rakeOut), new Vector3(W - 0.18f, ph, 0.04f), darkGlass, 0f, true, 0f, -rake);
            if (kind != 2) k.Box(new Vector3(0f, 0.8f + cabH * 0.42f, cabZ - cabLen * 0.5f - 0.12f), new Vector3(W - 0.2f, cabH * 0.9f, 0.04f), darkGlass, 0f, true, 0f, 40f);
            // bumpers and plates
            k.Box(new Vector3(0f, 0.36f, hl + 0.04f), new Vector3(W + 0.02f, 0.2f, 0.1f), blackMat);
            k.Box(new Vector3(0f, 0.36f, -hl - 0.04f), new Vector3(W + 0.02f, 0.2f, 0.1f), blackMat);
            k.Box(new Vector3(0f, 0.42f, hl + 0.095f), new Vector3(0.52f, 0.11f, 0.01f), carPlate, 0f, false);
            k.Box(new Vector3(0f, 0.6f, -hl - 0.01f), new Vector3(0.52f, 0.11f, 0.01f), carPlateRear, 0f, false);
            k.Box(new Vector3(0f, 0.55f, hl + 0.006f), new Vector3(0.7f, 0.1f, 0.01f), blackMat, 0f, false);   // grille
            k.Build(root.transform, root.name);
            foreach (Transform t in root.transform) t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // the kit wrote world positions

            // indicators (separate, so they can blink)
            var lensList = new System.Collections.Generic.List<Renderer>();
            for (int s = -1; s <= 1; s += 2)
            {
                for (int e = -1; e <= 1; e += 2)
                {
                    GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    lens.name = "Indicator";
                    Object.DestroyImmediate(lens.GetComponent<Collider>());
                    lens.transform.SetParent(root.transform, false);
                    lens.transform.localPosition = new Vector3(s * (hw - 0.06f), 0.68f, e * (hl + 0.012f));
                    lens.transform.localScale = new Vector3(0.1f, 0.08f, 0.02f);
                    Renderer r = lens.GetComponent<Renderer>();
                    r.sharedMaterial = carIndicator;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    lensList.Add(r);
                }
            }
            if (hazards)
            {
                var lg = new GameObject("Hazard light");
                lg.transform.SetParent(root.transform, false);
                lg.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                Light l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 6.5f;
                l.intensity = 1.6f;
                l.color = new Color(1f, 0.6f, 0.15f);
                l.shadows = LightShadows.None;
                l.lightmapBakeType = LightmapBakeType.Realtime;
                BlinkLight blink = root.AddComponent<BlinkLight>();
                blink.glow = l;
                blink.lenses = lensList.ToArray();
            }

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, (cabTop + 0.12f) * 0.5f, 0f);
            col.size = new Vector3(W + 0.05f, cabTop - 0.12f, L + 0.15f);
            root.AddComponent<SurfaceMaterial>().type = SurfaceType.Metal;
            carsParked++;
        }
    }
}
