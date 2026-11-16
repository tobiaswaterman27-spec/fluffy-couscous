using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.Core;
using AfterHours.Rendering;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Part 4 detail pass: dresses every ceiling, wall and floor according to what kind of space it is in
    /// (concourse, shop, staff corridor, toilets, car park...). Placement follows fixed rules and a fixed seed,
    /// so the mall is identical on every run. Small details have no colliders (you cannot snag on a socket);
    /// anything at body size is solid.
    /// </summary>
    public static partial class MallBuilder
    {
        static System.Random dressRng;
        static Material decalStainA, decalStainB, decalScuffs, decalGum, decalCrack, decalOil, decalTyre, decalPuddle,
            decalWater, decalGrime, decalDamp, decalTape;
        static readonly List<Material> adMaterials = new List<Material>();

        static readonly string[] AdSlogans =
        {
            "SUMMER SALE\nUP TO 50% OFF", "NEW SEASON\nSTYLES IN STORE", "PHONES FROM\n£49.99", "THE PLACE\nTO BE",
            "FAMILY FUN\nSTARLITE BOWL", "NOW SHOWING\nGALAXY CINEMA", "FREE PARKING\nAFTER 6PM", "LATE NIGHT\nSHOPPING THURSDAYS",
            "GIFT CARDS\nAVAILABLE HERE", "BACK TO\nSCHOOL"
        };

        static void PrepareDressing()
        {
            dressRng = new System.Random(20031130);
            decalStainA = ArtLibrary.Decal("StainA", "stain_a", Color.white, 0.3f);
            decalStainB = ArtLibrary.Decal("StainB", "stain_b", Color.white, 0.3f);
            decalScuffs = ArtLibrary.Decal("Scuffs", "scuffs", Color.white, 0.2f);
            decalGum = ArtLibrary.Decal("Gum", "gum", Color.white, 0.3f);
            decalCrack = ArtLibrary.Decal("Crack", "crack", Color.white, 0.1f);
            decalOil = ArtLibrary.Decal("Oil", "oil", Color.white, 0.75f);
            decalTyre = ArtLibrary.Decal("Tyre", "tyre", Color.white, 0.3f);
            decalPuddle = ArtLibrary.Decal("Puddle", "puddle", Color.white, 0.95f);
            decalWater = ArtLibrary.Decal("WaterDamage", "water_damage", Color.white, 0.1f);
            decalGrime = ArtLibrary.Decal("Grime", "grime", Color.white, 0.1f);
            decalDamp = ArtLibrary.Decal("Damp", "damp", Color.white, 0.2f);
            decalTape = ArtLibrary.Decal("TapeMarks", "tape_marks", Color.white, 0.2f);

            adMaterials.Clear();
            Color[] adColours =
            {
                new Color(0.9f, 0.3f, 0.25f), new Color(0.25f, 0.45f, 0.85f), new Color(0.95f, 0.7f, 0.2f),
                new Color(0.35f, 0.7f, 0.45f), new Color(0.6f, 0.3f, 0.7f)
            };
            for (int i = 0; i < adColours.Length; i++)
            {
                Material m = ArtLibrary.Plain("Advert_" + i, adColours[i] * 0.8f, 0.7f, 0f, adColours[i] * 0.9f, true);
                ArtLibrary.MarkDetail(m);
                adMaterials.Add(m);
            }
        }

        static float Rand(float a, float b) => a + (float)dressRng.NextDouble() * (b - a);
        static bool Chance(double p) => dressRng.NextDouble() < p;

        // ------------------------------------------------------------------ primitives

        /// <summary>Small detail box without a collider.</summary>
        static GameObject Detail(string name, Transform parent, Vector3 centre, Vector3 size, Material m, Quaternion? rot = null)
        {
            GameObject go = EditorBuild.Box(name, parent, centre, size, m, SurfaceType.Metal, rot);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().shadowCastingMode = size.x * size.y * size.z < 0.02f ? ShadowCastingMode.Off : ShadowCastingMode.On;
            return go;
        }

        /// <summary>Cylinder from 'a' to 'b' (pipes, rails, sprinklers...). No collider unless asked.</summary>
        static GameObject Cyl(string name, Transform parent, Vector3 a, Vector3 b, float diameter, Material m, bool collider = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            Vector3 d = b - a;
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.up);
            go.transform.localScale = new Vector3(diameter, d.magnitude * 0.5f, diameter);
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            else go.AddComponent<SurfaceMaterial>().type = SurfaceType.Metal;
            go.isStatic = true;
            return go;
        }

        /// <summary>A flat see-through decal lying on a surface with the given normal.</summary>
        static void DecalQuad(Transform parent, Vector3 centre, Vector3 normal, float width, float height, float angle, Material m)
        {
            Quaternion rot = Quaternion.LookRotation(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up) * Quaternion.Euler(0f, 0f, angle);
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Decal";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre + normal * 0.004f;
            go.transform.localRotation = rot;
            go.transform.localScale = new Vector3(width, height, 0.001f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = true;
        }

        // =====================================================================
        // Ceilings: light panels (some dead, some flickering), sprinklers, detectors, speakers, vents,
        // CCTV domes, missing / hanging / water-stained tiles.
        // =====================================================================

        static void DressCeilings(Transform parent)
        {
            const float T = 1.2f;
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                Transform g = Group(MallLayout.FloorName(f) + " ceiling", parent);
                float y = MallLayout.FloorY(f);
                float cy = y + MallLayout.CeilingHeight;
                List<AreaRect> holes = f == MallFloor.Level2 ? MallLayout.FloorHoles(MallFloor.Level2) : MallLayout.FloorHoles(f + 1);

                for (int i = -105; i < 105; i++)
                {
                    for (int j = -105; j < 105; j++)
                    {
                        float x = (i + 0.5f) * T, z = (j + 0.5f) * T;
                        bool inHole = false;
                        foreach (AreaRect hole in holes) if (hole.Expanded(0.3f).Contains(x, z)) { inHole = true; break; }
                        if (inHole) continue;

                        ZoneInfo zone = MallZones.Classify(new Vector3(x, y + 2f, z));
                        if (!HasDressedCeiling(zone)) continue;
                        // Must be well inside the building at this point (not straddling an outer wall).
                        if (MallZones.Classify(new Vector3(x + 0.55f, y + 2f, z + 0.55f)).kind == ZoneKind.Outside ||
                            MallZones.Classify(new Vector3(x - 0.55f, y + 2f, z - 0.55f)).kind == ZoneKind.Outside) continue;

                        int h = MallLayout.Hash(i, j, (int)f, 17);
                        bool publicArea = zone.kind == ZoneKind.Concourse || zone.kind == ZoneKind.Atrium || zone.kind == ZoneKind.EntranceHall;
                        Vector3 c = new Vector3(x, cy, z);

                        bool light = publicArea ? (Mod(i, 3) == 1 && Mod(j, 3) == 1)
                                   : zone.kind == ZoneKind.ServiceCorridor ? (Mod(i + j, 5) == 0)
                                   : (Mod(i, 3) == 1 && Mod(j, 2) == 1);
                        if (light)
                        {
                            CeilingLight(g, c, zone, h, zone.kind == ZoneKind.ServiceCorridor);
                            continue;
                        }

                        if (Mod(i, 2) == 0 && Mod(j, 2) == 0)
                        {
                            Cyl("Sprinkler", g, c, c + Vector3.down * 0.07f, 0.04f, metal);
                        }
                        else if (Mod(i, 6) == 3 && Mod(j, 6) == 0)
                        {
                            Cyl("Smoke detector", g, c, c + Vector3.down * 0.05f, 0.12f, whiteFixture);
                        }
                        else if (publicArea && Mod(i, 8) == 4 && Mod(j, 8) == 4)
                        {
                            Cyl("Speaker", g, c, c + Vector3.down * 0.02f, 0.24f, whiteFixture);
                        }
                        else if (Mod(i, 5) == 2 && Mod(j, 4) == 2)
                        {
                            Detail("Air vent", g, c + Vector3.down * 0.015f, new Vector3(0.58f, 0.03f, 0.58f), metal);
                            Detail("Vent slots", g, c + Vector3.down * 0.031f, new Vector3(0.44f, 0.005f, 0.44f), blackMat);
                        }
                        else if (publicArea && Mod(i, 12) == 6 && Mod(j, 12) == 0)
                        {
                            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            dome.name = "CCTV dome";
                            dome.transform.SetParent(g, false);
                            dome.transform.localPosition = c + Vector3.down * 0.02f;
                            dome.transform.localScale = new Vector3(0.22f, 0.14f, 0.22f);
                            Object.DestroyImmediate(dome.GetComponent<Collider>());
                            dome.GetComponent<Renderer>().sharedMaterial = blackMat;
                            dome.isStatic = true;
                        }

                        // Neglect: water-stained, missing and hanging tiles.
                        if (h % 41 == 0)
                        {
                            DecalQuad(g, c + Vector3.down * 0.002f, Vector3.down, Rand(0.6f, 1.3f), Rand(0.6f, 1.3f), Rand(0f, 360f), decalWater);
                        }
                        else if (h % 173 == 0 && zone.kind != ZoneKind.Toilets)
                        {
                            MissingTile(g, new Vector3(x - 0.3f, cy, z - 0.3f), y, h);
                        }
                    }
                }
            }
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        static bool HasDressedCeiling(ZoneInfo zone)
        {
            switch (zone.kind)
            {
                case ZoneKind.Outside:
                case ZoneKind.LiftShaft:
                case ZoneKind.Stairwell:
                    return false;
                case ZoneKind.Shop:
                case ZoneKind.Anchor:
                    return zone.unit != null && zone.unit.HasInterior;
                default:
                    return true;
            }
        }

        /// <summary>A fluorescent fitting: usually working; some dead; a few flickering (real-time light + buzz).</summary>
        static void CeilingLight(Transform g, Vector3 c, ZoneInfo zone, int h, bool batten)
        {
            Vector3 size = batten ? new Vector3(1.2f, 0.07f, 0.16f) : new Vector3(1.18f, 0.03f, 0.58f);
            if (batten && (zone.kind == ZoneKind.ServiceCorridor) && Mathf.Abs(Vector3.Dot(MallLayout.WingForward(zone.wing), Vector3.right)) < 0.5f)
            {
                size = new Vector3(size.z, size.y, size.x); // run the tube along the corridor
            }
            Vector3 centre = c + Vector3.down * (size.y * 0.5f);
            if (h % 13 == 0)
            {
                Detail("Light (dead)", g, centre, size, trofferDead);
                return;
            }
            if (h % 29 == 0)
            {
                GameObject panel = Detail("Light (faulty)", g, centre, size, trofferFlicker);
                var go = new GameObject("Faulty light");
                go.transform.SetParent(g, false);
                go.transform.localPosition = c + Vector3.down * 0.3f;
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 7f;
                l.intensity = 0.9f;
                l.color = FluorescentWhite;
                l.shadows = LightShadows.None;
                AudioSource buzz = go.AddComponent<AudioSource>();
                buzz.playOnAwake = false;
                FluorescentFlicker fl = go.AddComponent<FluorescentFlicker>();
                fl.lightSource = l;
                fl.panels = new[] { panel.GetComponent<Renderer>() };
                fl.buzz = buzz;
                fl.pattern = (FlickerPattern)(h % 3);
                fl.seed = h;
                return;
            }
            Detail("Light panel", g, centre, size, trofferMat);
        }

        static void MissingTile(Transform g, Vector3 tileCentre, float floorY, int h)
        {
            // Dark void where the tile should be...
            Detail("Missing ceiling tile", g, tileCentre + Vector3.down * 0.003f, new Vector3(0.58f, 0.004f, 0.58f), blackMat);
            // Cables and an insulation scrap dangling out of the hole.
            int cables = 1 + h % 3;
            for (int c = 0; c < cables; c++)
            {
                Vector3 top = tileCentre + new Vector3(Rand(-0.22f, 0.22f), 0.05f, Rand(-0.22f, 0.22f));
                Vector3 bottom = top + new Vector3(Rand(-0.25f, 0.25f), -Rand(0.4f, 1.3f), Rand(-0.25f, 0.25f));
                Cyl("Hanging cable", g, top, bottom, Rand(0.008f, 0.016f), blackMat);
            }
            if (h % 3 == 0) Detail("Insulation scrap", g, tileCentre + new Vector3(0.1f, -0.12f, -0.05f), new Vector3(0.3f, 0.2f, 0.06f), yellowPaint, Quaternion.Euler(Rand(-30f, 30f), Rand(0f, 90f), Rand(60f, 100f)));
            if (h % 2 == 0)
            {
                // ...hanging down from one edge,
                Quaternion tilt = Quaternion.Euler(Rand(35f, 70f), Rand(-20f, 20f), 0f);
                Detail("Hanging ceiling tile", g, tileCentre + new Vector3(0f, -0.22f, 0.12f), new Vector3(0.58f, 0.015f, 0.58f), ceilingMat, tilt);
            }
            else
            {
                // ...or fallen on the floor (a light prop that can be kicked about).
                Vector3 floorPos = new Vector3(tileCentre.x + Rand(-0.6f, 0.6f), floorY, tileCentre.z + Rand(-0.6f, 0.6f));
                GameObject fallen = EditorBuild.Box("Fallen ceiling tile", g, floorPos + Vector3.up * 0.02f, new Vector3(0.58f, 0.015f, 0.58f), ceilingMat, SurfaceType.Wood,
                    Quaternion.Euler(0f, Rand(0f, 90f), 0f), false);
                Rigidbody rb = fallen.AddComponent<Rigidbody>();
                rb.mass = 1.2f;
                fallen.AddComponent<PhysicsProp>().material = SurfaceType.Wood;
                EditorBuild.SetLayerRecursive(fallen, GameLayers.PropLayer);
            }
        }

        // =====================================================================
        // Walls: skirting, grime, sockets, switches, extinguishers, call points, crash rails, damp,
        // toilet tiling, car park bands, and glowing advert panels on public walls.
        // =====================================================================

        static void DressWalls(Transform parent)
        {
            Transform g = Group("Wall details", parent);
            int index = 0;
            foreach (WallRecord w in Walls)
            {
                index++;
                Vector3 d = w.p1 - w.p0;
                d.y = 0f;
                float length = d.magnitude;
                if (length < 0.4f) continue;
                Vector3 dir = d / length;
                Vector3 across = Vector3.Cross(Vector3.up, dir);
                bool atFloor = IsFloorLevel(w.y0);

                // Solid spans between doorways.
                var spans = new List<(float a, float b)>();
                float cursor = 0f;
                foreach (Opening o in w.openings)
                {
                    if (o.s0 > cursor + 0.05f) spans.Add((cursor, o.s0));
                    cursor = Mathf.Max(cursor, o.s1);
                }
                if (cursor < length - 0.05f) spans.Add((cursor, length));

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 n = across * side;
                    Vector3 mid = w.p0 + dir * (length * 0.5f) + n * (w.thickness * 0.5f + 0.5f);
                    ZoneInfo zone = MallZones.Classify(new Vector3(mid.x, w.y0 + 1f, mid.z));
                    if (zone.kind == ZoneKind.Outside || zone.kind == ZoneKind.LiftShaft) continue;
                    if ((zone.kind == ZoneKind.Shop || zone.kind == ZoneKind.Anchor) && (zone.unit == null || !zone.unit.HasInterior)) continue;

                    foreach (var (a, b) in spans)
                    {
                        float len = b - a;
                        if (len < 0.3f) continue;
                        Vector3 face(float s, float height, float off) =>
                            new Vector3(w.p0.x, 0f, w.p0.z) + dir * s + n * (w.thickness * 0.5f + off) + Vector3.up * (w.y0 + height);
                        Quaternion facing = Quaternion.LookRotation(n);
                        int h = MallLayout.Hash(index, (int)(a * 10f), side, 5);

                        if (atFloor) DressWallSpan(g, w, zone, dir, n, facing, a, b, len, h, face);
                    }
                }
            }
        }

        delegate Vector3 FaceFn(float s, float height, float off);

        static bool IsFloorLevel(float y)
        {
            foreach (MallFloor f in MallLayout.ShopFloors) if (Mathf.Abs(y - MallLayout.FloorY(f)) < 0.05f) return true;
            return Mathf.Abs(y - MallLayout.CarParkY) < 0.05f;
        }

        static void DressWallSpan(Transform g, WallRecord w, ZoneInfo zone, Vector3 dir, Vector3 n, Quaternion facing,
                                  float a, float b, float len, int h, FaceFn face)
        {
            float mid = (a + b) * 0.5f;
            bool publicArea = zone.kind == ZoneKind.Concourse || zone.kind == ZoneKind.Atrium || zone.kind == ZoneKind.EntranceHall
                              || zone.kind == ZoneKind.FoodCourt || zone.kind == ZoneKind.Cinema || zone.kind == ZoneKind.Anchor || zone.kind == ZoneKind.Shop;
            bool serviceArea = zone.kind == ZoneKind.ServiceCorridor || zone.kind == ZoneKind.BackOfHouse || zone.kind == ZoneKind.Passage;
            bool carPark = zone.kind == ZoneKind.CarPark || zone.kind == ZoneKind.ServiceTunnel || zone.kind == ZoneKind.ExitRamp;

            // Skirting / base finish.
            if (zone.kind == ZoneKind.Toilets)
            {
                Detail("Wall tiling", g, face(mid, 1.05f, 0.005f), new Vector3(len, 2.1f, 0.01f), toiletMat, facing);
            }
            else if (carPark)
            {
                Detail("Painted base band", g, face(mid, 0.5f, 0.004f), new Vector3(len, 1.0f, 0.006f), darkPanel, facing);
                Detail("Hazard stripe", g, face(mid, 1.05f, 0.005f), new Vector3(len, 0.1f, 0.008f), yellowPaint, facing);
            }
            else if (zone.kind != ZoneKind.Stairwell)
            {
                Detail("Skirting", g, face(mid, 0.05f, 0.008f), new Vector3(len, 0.1f, 0.016f), serviceArea ? blackMat : darkPanel, facing);
            }

            // Grime rising from the floor in hard-worn areas.
            if (serviceArea || carPark || zone.kind == ZoneKind.Stairwell || zone.kind == ZoneKind.Toilets)
            {
                DecalQuad(g, face(mid, 0.35f, 0.012f), n, len, 0.7f, 0f, decalGrime);
            }

            // Crash rail protecting service corridor walls from trolleys.
            if (zone.kind == ZoneKind.ServiceCorridor && len > 1.5f)
            {
                Detail("Crash rail", g, face(mid, 0.9f, 0.03f), new Vector3(len - 0.2f, 0.14f, 0.05f), woodMat, facing);
            }

            // Sockets every ~7 m, switches beside doorways.
            if (!carPark && zone.kind != ZoneKind.Stairwell)
            {
                for (float s = a + 2.5f; s < b - 0.5f; s += 7f)
                {
                    Detail("Socket", g, face(s, 0.3f, 0.006f), new Vector3(0.146f, 0.086f, 0.012f), whiteFixture, facing);
                }
                if (a > 0.05f) Detail("Light switch", g, face(a + 0.25f, 1.2f, 0.006f), new Vector3(0.086f, 0.086f, 0.012f), whiteFixture, facing);
            }

            // Fire safety: extinguishers with signs, call points by doors.
            if ((serviceArea || zone.kind == ZoneKind.Stairwell || carPark || zone.kind == ZoneKind.Anchor) && len > 3f && h % 4 == 0)
            {
                float s = a + Mathf.Min(1.2f, len * 0.3f);
                Vector3 baseP = face(s, 0f, 0.12f);
                Cyl("Fire extinguisher", g, baseP + Vector3.up * 0.35f, baseP + Vector3.up * 0.9f, 0.17f, redMat);
                Detail("Extinguisher bracket", g, face(s, 0.75f, 0.02f), new Vector3(0.12f, 0.05f, 0.04f), blackMat, facing);
                Detail("Fire point sign", g, face(s, 1.65f, 0.006f), new Vector3(0.2f, 0.28f, 0.006f), redMat, facing);
            }
            if ((serviceArea || zone.kind == ZoneKind.Stairwell || zone.kind == ZoneKind.Concourse) && b < (w.p1 - w.p0).magnitude - 0.05f && h % 3 == 0)
            {
                Detail("Fire alarm call point", g, face(b - 0.3f, 1.4f, 0.025f), new Vector3(0.09f, 0.09f, 0.05f), redMat, facing);
            }

            // Damp patches in neglected places.
            if ((serviceArea || carPark || zone.kind == ZoneKind.Stairwell) && h % 7 == 0)
            {
                float s = Mathf.Lerp(a, b, (h % 100) / 100f);
                DecalQuad(g, face(s, Rand(0.4f, 2.5f), 0.01f), n, Rand(0.8f, 2.2f), Rand(0.8f, 2f), Rand(0f, 360f), decalDamp);
            }

            // Public walls: glowing advert panels, or tape marks where posters used to be.
            if (publicArea && zone.kind != ZoneKind.Shop && len >= 3.5f)
            {
                int count = Mathf.Min(3, Mathf.FloorToInt(len / 4f));
                for (int k = 0; k < count; k++)
                {
                    float s = a + len * (k + 0.5f) / count;
                    int ah = MallLayout.Hash(h, k, 3, 3);
                    if (ah % 5 == 0)
                    {
                        DecalQuad(g, face(s, 1.6f, 0.008f), n, 1.1f, 1.5f, 0f, decalTape);
                        continue;
                    }
                    Material ad = adMaterials[ah % adMaterials.Count];
                    Detail("Advert frame", g, face(s, 1.45f, 0.04f), new Vector3(1.3f, 1.9f, 0.07f), metal, facing);
                    Detail("Advert panel", g, face(s, 1.45f, 0.076f), new Vector3(1.16f, 1.76f, 0.004f), ad, facing);
                    string slogan = AdSlogans[ah % AdSlogans.Length];
                    float yaw = Mathf.Atan2(-n.x, -n.z) * Mathf.Rad2Deg;
                    EditorBuild.Label(g, slogan, face(s, 1.55f, 0.09f), yaw, 0.022f);
                }
            }
        }

        // =====================================================================
        // Floors: stains, scuffs, gum, cracks, puddles (car park decals are in the car park pass).
        // =====================================================================

        static void DressFloors(Transform parent)
        {
            Transform g = Group("Floor decals", parent);
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                for (int i = 0; i < 2600; i++)
                {
                    float x = Rand(-125f, 125f), z = Rand(-125f, 125f);
                    ZoneInfo zone = MallZones.Classify(new Vector3(x, y + 1f, z));
                    if (zone.kind == ZoneKind.Outside || zone.kind == ZoneKind.LiftShaft || zone.kind == ZoneKind.Stairwell) continue;
                    if ((zone.kind == ZoneKind.Shop || zone.kind == ZoneKind.Anchor) && (zone.unit == null || !zone.unit.HasInterior)) continue;
                    if (MallLayout.FloorHoles(f).Exists(hh => hh.Expanded(0.6f).Contains(x, z))) continue;

                    Vector3 p = new Vector3(x, y + 0.012f, z);
                    double r = dressRng.NextDouble();
                    bool service = zone.kind == ZoneKind.ServiceCorridor || zone.kind == ZoneKind.BackOfHouse || zone.kind == ZoneKind.Passage;
                    float angle = Rand(0f, 360f);
                    if (zone.kind == ZoneKind.Toilets)
                    {
                        DecalQuad(g, p, Vector3.up, Rand(0.6f, 1.4f), Rand(0.6f, 1.4f), angle, r < 0.5 ? decalPuddle : decalStainB);
                    }
                    else if (service)
                    {
                        Material m = r < 0.5 ? decalScuffs : r < 0.8 ? decalStainB : decalCrack;
                        DecalQuad(g, p, Vector3.up, Rand(0.8f, 2f), Rand(0.8f, 2f), angle, m);
                    }
                    else
                    {
                        Material m = r < 0.3 ? decalScuffs : r < 0.55 ? decalStainA : r < 0.75 ? decalGum : r < 0.9 ? decalStainB : decalCrack;
                        float size = m == decalGum ? Rand(0.5f, 0.9f) : Rand(0.7f, 1.8f);
                        DecalQuad(g, p, Vector3.up, size, size * Rand(0.7f, 1.3f), angle, m);
                    }
                }
            }
        }

        // =====================================================================
        // Door frames and green EXIT signs (real doors come in Part 5).
        // =====================================================================

        static void DressDoorways(Transform parent)
        {
            Transform g = Group("Door frames", parent);
            Physics.SyncTransforms();
            foreach (DoorSlot slot in Object.FindObjectsByType<DoorSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                switch (slot.kind)
                {
                    case DoorKind.Shutter:
                    case DoorKind.Glass:
                    case DoorKind.Lift:
                    case DoorKind.Hidden:
                        continue;
                }
                if (slot.GetComponent<Collider>() != null) continue; // slots that are physical panels already
                Vector3 p = slot.transform.position;

                // Which way does the wall run? The jambs are hit when casting along the wall.
                Vector3 axis = Vector3.right;
                Vector3 origin = p + Vector3.up * 1f;
                float reach = slot.width * 0.5f + 0.35f;
                bool hitX = Physics.Raycast(origin, Vector3.right, reach) && Physics.Raycast(origin, Vector3.left, reach);
                bool hitZ = Physics.Raycast(origin, Vector3.forward, reach) && Physics.Raycast(origin, Vector3.back, reach);
                if (hitZ && !hitX) axis = Vector3.forward;
                Vector3 normal = Vector3.Cross(Vector3.up, axis);
                Quaternion rot = Quaternion.LookRotation(normal);
                Material frameMat = slot.kind == DoorKind.Stairwell || slot.kind == DoorKind.Fire || slot.kind == DoorKind.Maintenance ? metal : woodMat;
                float half = slot.width * 0.5f;
                const float depth = 0.36f;

                Detail("Door frame jamb", g, p + axis * (half + 0.03f) + Vector3.up * (slot.height * 0.5f), new Vector3(0.06f, slot.height, depth), frameMat, rot);
                Detail("Door frame jamb", g, p - axis * (half + 0.03f) + Vector3.up * (slot.height * 0.5f), new Vector3(0.06f, slot.height, depth), frameMat, rot);
                Detail("Door frame head", g, p + Vector3.up * (slot.height + 0.03f), new Vector3(slot.width + 0.12f, 0.06f, depth), frameMat, rot);

                if (slot.kind == DoorKind.Stairwell || slot.kind == DoorKind.Fire)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 c = p + Vector3.up * (slot.height + 0.25f) + normal * (side * (depth * 0.5f + 0.03f));
                        Detail("Exit sign", g, c, new Vector3(0.42f, 0.16f, 0.05f), exitSignMat, rot);
                        float yaw = Mathf.Atan2(-normal.x * side, -normal.z * side) * Mathf.Rad2Deg;
                        EditorBuild.Label(g, "EXIT", c + normal * (side * 0.03f), yaw, 0.03f);
                    }
                }
            }
        }
    }
}
