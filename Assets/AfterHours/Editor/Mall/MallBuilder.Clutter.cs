using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// The detail pass that makes it look lived-in and abandoned in a hurry: stocked shelves (with gaps where things
    /// sold out), litter and leaflets, coffee cups, cardboard, wet-floor signs, trolleys left where they stopped,
    /// traffic cones in the car park, cobwebs in service corridors, dirty handprints - and a trail of long bare
    /// footprints in the car park leading from where Elliot waits towards the lifts.
    /// </summary>
    public static partial class MallBuilder
    {
        static Material stockClothes, stockGrocery, stockBeauty, stockBoxes, cardboardMat, decalLitter, decalLeaflet, decalPrintsShoe, decalPrintsBare,
            decalCobweb, decalHandprint, cupMat, coneMat;

        static void PrepareClutterMaterials()
        {
            Color w = Color.white;
            stockGrocery = ArtLibrary.Surface("Stock_Grocery", "products_grocery", 1f, w, 1f, 0.6f);
            stockBeauty = ArtLibrary.Surface("Stock_Beauty", "products_beauty", 1f, w, 1f, 0.6f);
            stockBoxes = ArtLibrary.Surface("Stock_Boxes", "products_boxes", 1.5f, w, 1f, 0.6f);
            stockClothes = ArtLibrary.Surface("Stock_Clothes", "clothes_rack", 1.6f, w, 1f, 0.8f);
            cardboardMat = ArtLibrary.Surface("Cardboard", "cardboard", 0.8f, w, 1f, 0.8f);
            decalLitter = ArtLibrary.Decal("Litter", "litter", Color.white, 0.2f);
            decalLeaflet = ArtLibrary.Decal("Leaflet", "leaflet", Color.white, 0.55f);
            decalPrintsShoe = ArtLibrary.Decal("PrintsShoe", "prints_shoe", Color.white, 0.1f);
            decalPrintsBare = ArtLibrary.Decal("PrintsBare", "prints_bare", Color.white, 0.35f);
            decalCobweb = ArtLibrary.Decal("Cobweb", "cobweb", Color.white, 0.1f);
            decalHandprint = ArtLibrary.Decal("Handprint", "handprint", Color.white, 0.25f);
            cupMat = ArtLibrary.Plain("Paper_Cup", new Color(0.88f, 0.86f, 0.8f), 0.3f);
            ArtLibrary.MarkDetail(cupMat);
            coneMat = ArtLibrary.Plain("Traffic_Cone", new Color(0.9f, 0.35f, 0.08f), 0.4f);
            ArtLibrary.MarkDetail(coneMat);
        }

        // ------------------------------------------------------------------ shelves

        /// <summary>
        /// Puts stock on a shelving block: rows of products on each long face (the packaging texture lines up with
        /// the shelves at 1/3 m), thin shelf lips, and gaps where things have sold out or been knocked off.
        /// </summary>
        static void Stock(Transform g, AreaRect r, float y, float height, Material stock, int seed)
        {
            if (stock == null) return;
            var rng = new System.Random(seed);
            bool alongX = r.Width >= r.Depth;
            float len = alongX ? r.Width : r.Depth;
            for (int face = -1; face <= 1; face += 2)
            {
                // Product faces in a few runs, leaving the odd empty gap.
                float s = 0.05f;
                while (s < len - 0.1f)
                {
                    float run = Mathf.Min(len - 0.05f - s, 0.6f + (float)rng.NextDouble() * 1.6f);
                    bool empty = rng.NextDouble() < 0.15;
                    if (!empty)
                    {
                        float top = y + Mathf.Min(height - 0.05f, 0.35f + Mathf.Floor((height - 0.35f) * 3f) / 3f + (float)rng.NextDouble() * 0.08f);
                        Vector3 a, b;
                        if (alongX)
                        {
                            float zf = face < 0 ? r.minZ : r.maxZ;
                            a = new Vector3(r.minX + s, y + 0.12f, zf - 0.02f * face);
                            b = new Vector3(r.minX + s + run, top, zf + 0.12f * face);
                        }
                        else
                        {
                            float xf = face < 0 ? r.minX : r.maxX;
                            a = new Vector3(xf - 0.02f * face, y + 0.12f, r.minZ + s);
                            b = new Vector3(xf + 0.12f * face, top, r.minZ + s + run);
                        }
                        Decor("Stock", g, Vector3.Min(a, b), Vector3.Max(a, b), stock);
                    }
                    s += run + (empty ? 0f : 0.02f);
                }
                // Shelf lips.
                for (float level = y + 0.1f; level < y + height - 0.1f; level += 1f / 3f)
                {
                    Vector3 a, b;
                    if (alongX)
                    {
                        float zf = face < 0 ? r.minZ : r.maxZ;
                        a = new Vector3(r.minX, level, zf); b = new Vector3(r.maxX, level + 0.025f, zf + 0.16f * face);
                    }
                    else
                    {
                        float xf = face < 0 ? r.minX : r.maxX;
                        a = new Vector3(xf, level, r.minZ); b = new Vector3(xf + 0.16f * face, level + 0.025f, r.maxZ);
                    }
                    Decor("Shelf lip", g, Vector3.Min(a, b), Vector3.Max(a, b), metal);
                }
            }
            // Something knocked off onto the floor.
            if (rng.NextDouble() < 0.35)
            {
                Vector3 c = new Vector3(r.Center.x, y, r.Center.y) + (alongX ? Vector3.forward : Vector3.right) * ((alongX ? r.Depth : r.Width) * 0.5f + 0.35f);
                Prop(g, "Fallen stock", c, new Vector3(0.22f, 0.16f, 0.3f), 1.2f, cardboardMat, SurfaceType.Wood);
            }
        }

        // ------------------------------------------------------------------ clutter

        static void DressClutter(Transform parent)
        {
            PrepareClutterMaterials();
            Transform g = Group("Clutter", parent);
            Physics.SyncTransforms();
            var rng = new System.Random(4401);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            bool Free(Vector3 p, float r) => !Physics.CheckSphere(p + Vector3.up * (r + 0.02f), r, ~0, QueryTriggerInteraction.Ignore);

            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                List<AreaRect> holes = MallLayout.FloorHoles(f);
                for (int i = 0; i < 1600; i++)
                {
                    float x = R(-125f, 125f), z = R(-125f, 125f);
                    ZoneInfo zone = MallZones.Classify(new Vector3(x, y + 1f, z));
                    if (zone.kind == ZoneKind.Outside || zone.kind == ZoneKind.LiftShaft || zone.kind == ZoneKind.Stairwell) continue;
                    if ((zone.kind == ZoneKind.Shop || zone.kind == ZoneKind.Anchor) && (zone.unit == null || !zone.unit.HasInterior)) continue;
                    if (holes.Exists(h => h.Expanded(0.8f).Contains(x, z))) continue;
                    Vector3 p = new Vector3(x, y, z);
                    bool service = zone.kind == ZoneKind.ServiceCorridor || zone.kind == ZoneKind.BackOfHouse || zone.kind == ZoneKind.Passage;
                    bool publicArea = zone.kind == ZoneKind.Concourse || zone.kind == ZoneKind.Atrium || zone.kind == ZoneKind.FoodCourt || zone.kind == ZoneKind.EntranceHall;
                    double roll = rng.NextDouble();

                    if (publicArea || zone.kind == ZoneKind.Shop || zone.kind == ZoneKind.Anchor)
                    {
                        if (roll < 0.35) DecalQuad(g, p + Vector3.up * 0.014f, Vector3.up, R(0.5f, 1.0f), R(0.5f, 1.0f), R(0f, 360f), decalLitter);
                        else if (roll < 0.5) DecalQuad(g, p + Vector3.up * 0.015f, Vector3.up, 0.22f, 0.3f, R(0f, 360f), decalLeaflet);
                        else if (roll < 0.6 && Free(p, 0.05f)) Cup(g, p, rng);
                        else if (roll < 0.66 && Free(p, 0.06f)) Crumple(g, p, rng);
                    }
                    else if (service)
                    {
                        if (roll < 0.25) DecalQuad(g, p + Vector3.up * 0.014f, Vector3.up, R(0.5f, 1.0f), R(0.5f, 1.0f), R(0f, 360f), decalLitter);
                        else if (roll < 0.45) DecalQuad(g, p + Vector3.up * 0.013f, Vector3.up, 0.9f, 1.6f, R(0f, 360f), decalPrintsShoe);
                        else if (roll < 0.62 && Free(p, 0.35f))
                        {
                            float s = R(0.3f, 0.6f);
                            Prop(g, "Cardboard box", p, new Vector3(s, s * R(0.6f, 1f), s * R(0.8f, 1.2f)), 2f + s * 6f, cardboardMat, SurfaceType.Wood);
                        }
                        else if (roll < 0.7 && Free(p, 0.05f)) Cup(g, p, rng);
                    }
                    else if (zone.kind == ZoneKind.Toilets && roll < 0.5)
                    {
                        DecalQuad(g, p + Vector3.up * 0.014f, Vector3.up, R(0.3f, 0.6f), R(0.3f, 0.6f), R(0f, 360f), decalLitter);
                    }
                }

                // Wet-floor signs outside the toilets.
                foreach (Passage ps in MallLayout.Passages)
                {
                    if (ps.floor != f || !ps.isPublic) continue;
                    float mid = (ps.u0 + ps.u1) * 0.5f;
                    Vector3 c = MallLayout.WingPoint(ps.wing, mid + R(-0.6f, 0.6f), ps.side * (MallLayout.ConcourseHalf - 1.0f), y);
                    if (Free(c, 0.3f)) WetFloorSign(g, c, R(0f, 360f));
                }

                // Trolleys left where they rolled to a stop.
                for (int k = 0; k < 4; k++)
                {
                    WingId w = MallLayout.Wings[rng.Next(4)];
                    Vector3 c = MallLayout.WingPoint(w, R(24f, 90f), R(-3.5f, 3.5f), y);
                    if (Free(c, 0.5f)) Trolley(g, c, R(0f, 360f));
                }
            }

            // Car park: cones, litter, shoe prints... and the bare footprints.
            float cp = MallLayout.CarParkY;
            for (int i = 0; i < 260; i++)
            {
                Vector3 p = new Vector3(R(-95f, 95f), cp, R(-95f, 95f));
                if (MallLayout.CarParkRaised.Contains(p.x, p.z)) continue;
                double roll = rng.NextDouble();
                if (roll < 0.4) DecalQuad(g, p + Vector3.up * 0.014f, Vector3.up, R(0.5f, 1.0f), R(0.5f, 1.0f), R(0f, 360f), decalLitter);
                else if (roll < 0.55 && Free(p, 0.2f)) Cone(g, p);
                else if (roll < 0.7) DecalQuad(g, p + Vector3.up * 0.013f, Vector3.up, 0.9f, 1.6f, R(0f, 360f), decalPrintsShoe);
            }
            FootprintTrail(g, new Vector3(30f, cp, 66f), new Vector3(-13f, cp, 9f), rng);

            // Cobwebs high in the corners of the service corridors and stairwells; the odd dirty handprint.
            int idx = 0;
            foreach (WallRecord wr in Walls)
            {
                idx++;
                Vector3 d = wr.p1 - wr.p0;
                d.y = 0f;
                float len = d.magnitude;
                if (len < 1.5f || !IsFloorLevel(wr.y0)) continue;
                Vector3 dir = d / len;
                Vector3 n = Vector3.Cross(Vector3.up, dir);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 nn = n * side;
                    Vector3 probe = wr.p0 + dir * (len * 0.5f) + nn * (wr.thickness * 0.5f + 0.5f);
                    ZoneInfo zone = MallZones.Classify(new Vector3(probe.x, wr.y0 + 1f, probe.z));
                    bool service = zone.kind == ZoneKind.ServiceCorridor || zone.kind == ZoneKind.BackOfHouse || zone.kind == ZoneKind.Stairwell;
                    if (!service) continue;
                    int h = MallLayout.Hash(idx, side, 9, 9);
                    float top = wr.y0 + (zone.kind == ZoneKind.Stairwell ? 2.6f : 4.2f);
                    if (h % 3 == 0)
                    {
                        Vector3 c = new Vector3(wr.p0.x, top, wr.p0.z) + dir * 0.45f + nn * (wr.thickness * 0.5f + 0.006f);
                        DecalQuad(g, c, nn, 0.9f, 0.9f, 0f, decalCobweb);
                    }
                    if (h % 11 == 0)
                    {
                        Vector3 c = new Vector3(wr.p0.x, wr.y0 + R(1.0f, 1.6f), wr.p0.z) + dir * R(0.5f, len - 0.5f) + nn * (wr.thickness * 0.5f + 0.006f);
                        DecalQuad(g, c, nn, 0.22f, 0.32f, R(-25f, 25f), decalHandprint);
                    }
                }
            }
        }

        static void FootprintTrail(Transform g, Vector3 from, Vector3 to, System.Random rng)
        {
            // Long strides (much longer than a person's), fading as whatever was on its feet wore off.
            Vector3 d = to - from;
            float len = d.magnitude;
            Vector3 dir = d / len;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            int k = 0;
            for (float s = 0f; s < len * 0.65f; s += 1.35f, k++)
            {
                Vector3 p = from + dir * s + side * ((k % 2 == 0 ? 0.16f : -0.16f) + (float)(rng.NextDouble() - 0.5) * 0.05f) + Vector3.up * 0.016f;
                // One print per quad (the texture holds a pair, so use a narrow crop by scaling).
                DecalQuad(g, p, Vector3.up, 0.18f, 0.42f, -yaw + (float)(rng.NextDouble() - 0.5) * 10f, decalPrintsBare);
            }
        }

        static void Cup(Transform g, Vector3 floor, System.Random rng)
        {
            bool tipped = rng.NextDouble() < 0.6;
            if (tipped)
            {
                Vector3 dir = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * Vector3.forward;
                Cyl("Paper cup", g, floor + Vector3.up * 0.04f, floor + Vector3.up * 0.04f + dir * 0.12f, 0.08f, cupMat);
            }
            else Cyl("Paper cup", g, floor, floor + Vector3.up * 0.12f, 0.08f, cupMat);
        }

        static void Crumple(Transform g, Vector3 floor, System.Random rng)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Crumpled paper";
            Object.DestroyImmediate(s.GetComponent<Collider>());
            s.transform.SetParent(g, false);
            s.transform.localPosition = floor + Vector3.up * 0.03f;
            s.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, 0f);
            s.transform.localScale = new Vector3(0.08f, 0.06f, 0.07f);
            s.GetComponent<Renderer>().sharedMaterial = cupMat;
            s.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            s.isStatic = true;
        }

        static void WetFloorSign(Transform g, Vector3 floor, float yaw)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 c = floor + Vector3.up * 0.32f + r * new Vector3(0f, 0f, s * 0.1f);
                EditorBuild.Box("Wet floor sign", g, c, new Vector3(0.3f, 0.62f, 0.015f), yellowPaint, SurfaceType.Metal, r * Quaternion.Euler(s * 14f, 0f, 0f));
            }
            EditorBuild.Label(g, "CAUTION\nWET FLOOR", floor + Vector3.up * 0.38f + r * new Vector3(0f, 0f, -0.18f), yaw, 0.012f).color = new Color(0.1f, 0.1f, 0.1f);
        }

        static void Trolley(Transform g, Vector3 floor, float yaw)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            var root = new GameObject("Shopping trolley");
            root.transform.SetParent(g, false);
            root.transform.SetPositionAndRotation(floor, r);
            void Bar(Vector3 c, Vector3 s) => EditorBuild.Box("Trolley frame", root.transform, c, s, metal, SurfaceType.Metal, null, false);
            Bar(new Vector3(0f, 0.6f, 0f), new Vector3(0.55f, 0.02f, 0.85f));          // basket floor
            Bar(new Vector3(-0.27f, 0.8f, 0f), new Vector3(0.02f, 0.4f, 0.85f));
            Bar(new Vector3(0.27f, 0.8f, 0f), new Vector3(0.02f, 0.4f, 0.85f));
            Bar(new Vector3(0f, 0.8f, 0.42f), new Vector3(0.55f, 0.4f, 0.02f));
            Bar(new Vector3(0f, 0.8f, -0.42f), new Vector3(0.55f, 0.4f, 0.02f));
            Bar(new Vector3(0f, 1.02f, -0.5f), new Vector3(0.6f, 0.04f, 0.04f));       // handle
            Bar(new Vector3(0f, 0.3f, -0.35f), new Vector3(0.04f, 0.6f, 0.04f));
            Bar(new Vector3(0f, 0.3f, 0.35f), new Vector3(0.04f, 0.6f, 0.04f));
            for (int i = 0; i < 4; i++)
                Bar(new Vector3(i % 2 == 0 ? -0.22f : 0.22f, 0.06f, i < 2 ? -0.35f : 0.35f), new Vector3(0.03f, 0.12f, 0.1f));
            foreach (Collider c in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            BoxCollider bc = root.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.55f, 0f);
            bc.size = new Vector3(0.6f, 1.05f, 0.95f);
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 14f;
            root.AddComponent<SurfaceMaterial>().type = SurfaceType.Metal;
            var prop = root.AddComponent<PhysicsProp>();
            prop.material = SurfaceType.Metal;
            EditorBuild.SetLayerRecursive(root, AfterHours.Core.GameLayers.PropLayer);
        }

        static void Cone(Transform g, Vector3 floor)
        {
            Cyl("Traffic cone", g, floor, floor + Vector3.up * 0.5f, 0.18f, coneMat, true);
            Decor("Cone base", g, floor + new Vector3(-0.17f, 0f, -0.17f), floor + new Vector3(0.17f, 0.03f, 0.17f), coneMat);
        }
    }
}
