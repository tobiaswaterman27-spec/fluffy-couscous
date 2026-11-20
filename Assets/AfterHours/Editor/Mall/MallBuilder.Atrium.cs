using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    public static partial class MallBuilder
    {
        static void BuildFloorPlates(Transform parent)
        {
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                Transform g = Group(MallLayout.FloorName(f), parent);
                float y = MallLayout.FloorY(f);
                List<AreaRect> holes = MallLayout.FloorHoles(f);
                List<AreaRect> ceilingHoles = f == MallFloor.Level2 ? MallLayout.FloorHoles(MallFloor.Level2) : MallLayout.FloorHoles(f + 1);

                foreach (AreaRect region in MallLayout.FloorRegions(f))
                {
                    foreach (AreaRect part in SplitByZoneLines(region))
                    {
                        Vector2 c = part.Center;
                        ZoneInfo zone = MallZones.Classify(new Vector3(c.x, y + 1f, c.y));
                        Material m = FloorFor(zone, out SurfaceType surface);
                        Slab("Floor", g, part, holes, y, MallLayout.SlabThickness, m, surface);
                    }
                    Slab("Ceiling", g, region, ceilingHoles, y + MallLayout.CeilingHeight + 0.05f, 0.05f, ceilingMat, SurfaceType.Concrete);
                }
            }
        }

        static readonly float[] ZoneCuts = { 2.1f, MallLayout.ConcourseHalf, MallLayout.ShopBack, MallLayout.QuadrantDivider };

        /// <summary>Cuts a floor region along the lines where floor finishes change.</summary>
        static List<AreaRect> SplitByZoneLines(AreaRect r)
        {
            var xs = new List<float> { r.minX, r.maxX };
            var zs = new List<float> { r.minZ, r.maxZ };
            foreach (float c in ZoneCuts)
            {
                foreach (float v in new[] { c, -c })
                {
                    if (v > r.minX + 0.01f && v < r.maxX - 0.01f) xs.Add(v);
                    if (v > r.minZ + 0.01f && v < r.maxZ - 0.01f) zs.Add(v);
                }
            }
            xs = SortedUnique(xs);
            zs = SortedUnique(zs);
            var parts = new List<AreaRect>();
            for (int i = 0; i < xs.Count - 1; i++)
            {
                for (int j = 0; j < zs.Count - 1; j++) parts.Add(new AreaRect(xs[i], zs[j], xs[i + 1], zs[j + 1]));
            }
            return parts;
        }

        // =====================================================================
        // Atrium
        // =====================================================================

        static void BuildAtrium(Transform parent)
        {
            // Fountain: octagonal basin wall, water, centre column.
            Transform fountain = Group("Fountain", parent);
            const float r = 4f;
            float side = 2f * r * Mathf.Tan(22.5f * Mathf.Deg2Rad);
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f;
                Quaternion rot = Quaternion.Euler(0f, ang, 0f);
                Vector3 c = rot * new Vector3(0f, 0.275f, r - 0.2f);
                EditorBuild.Box("Basin wall", fountain, c, new Vector3(side + 0.05f, 0.55f, 0.4f), concrete, SurfaceType.Concrete, rot);
            }
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "Water";
            water.transform.SetParent(fountain, false);
            water.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            water.transform.localScale = new Vector3(r * 2f - 0.6f, 0.01f, r * 2f - 0.6f);
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.GetComponent<Renderer>().sharedMaterial = waterMat;
            BoxMinMax("Basin floor", fountain, new Vector3(-r + 0.4f, 0f, -r + 0.4f), new Vector3(r - 0.4f, 0.25f, r - 0.4f), concrete, SurfaceType.Concrete);
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Fountain column";
            column.transform.SetParent(fountain, false);
            column.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            column.transform.localScale = new Vector3(1.2f, 1.5f, 1.2f);
            column.GetComponent<Renderer>().sharedMaterial = concrete;
            column.AddComponent<SurfaceMaterial>().type = SurfaceType.Concrete;

            // Information desk.
            BoxMinMax("Information desk", parent, new Vector3(-2.5f, 0f, -7.8f), new Vector3(2.5f, 1.1f, -6.2f), counterMat, SurfaceType.Wood);
            Sign(parent, "INFORMATION", new Vector3(0f, 1.45f, -7.85f), Vector3.forward, 2.4f, 0.04f);
            InfoDeskDetail(parent);

            // Seating and planters.
            BoxMinMax("Bench", parent, new Vector3(-13f, 0f, -5f), new Vector3(-12.5f, 0.45f, -1f), woodMat, SurfaceType.Wood);
            BoxMinMax("Bench", parent, new Vector3(-13f, 0f, 1f), new Vector3(-12.5f, 0.45f, 5f), woodMat, SurfaceType.Wood);
            BoxMinMax("Bench", parent, new Vector3(12.5f, 0f, -8f), new Vector3(13f, 0.45f, -4f), woodMat, SurfaceType.Wood);
            BoxMinMax("Bench", parent, new Vector3(2f, 0f, -13f), new Vector3(6f, 0.45f, -12.5f), woodMat, SurfaceType.Wood);
            BoxMinMax("Planter", parent, new Vector3(12.2f, 0f, -14.8f), new Vector3(14.8f, 0.9f, -12.2f), concrete, SurfaceType.Concrete);
            BoxMinMax("Planter", parent, new Vector3(-14.8f, 0f, -14.8f), new Vector3(-12.2f, 0.9f, -12.2f), concrete, SurfaceType.Concrete);
            BoxMinMax("Planter", parent, new Vector3(12.2f, 0f, 12.2f), new Vector3(14.8f, 0.9f, 14.8f), concrete, SurfaceType.Concrete);

            // Hanging clock above the information desk: two white faces back to back, hour ticks, real hands that
            // tell the night's time from midnight (MallClock).
            {
                Transform clock = Group("Clock", parent);
                Vector3 c = new Vector3(0f, 8f, -8f);
                Material faceMat = ArtLibrary.Plain("ClockFace", new Color(0.9f, 0.89f, 0.85f), 0.5f, 0f);
                GameObject body = Cyl("Clock body", clock, c + Vector3.forward * 0.06f, c - Vector3.forward * 0.06f, 1.5f, blackMat);
                body.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
                var hourHands = new Transform[2];
                var minuteHands = new Transform[2];
                var signs = new float[2];
                for (int s = 0; s < 2; s++)
                {
                    float dir = s == 0 ? -1f : 1f;                               // front face looks along -z, back along +z
                    Vector3 fc = c + Vector3.forward * (0.061f * dir);
                    Cyl("Clock face", clock, fc, fc + Vector3.forward * (0.004f * dir), 1.38f, faceMat);
                    for (int k = 0; k < 12; k++)
                    {
                        float an = k * 30f * Mathf.Deg2Rad;
                        Vector3 pos = fc + Vector3.forward * (0.005f * dir) + new Vector3(Mathf.Sin(an), Mathf.Cos(an), 0f) * 0.6f;
                        bool major = k % 3 == 0;
                        Detail("Clock tick", clock, pos, new Vector3(major ? 0.035f : 0.02f, major ? 0.12f : 0.07f, 0.004f), blackMat,
                            Quaternion.Euler(0f, 0f, -k * 30f));
                    }
                    var pivotH = new GameObject("Hour hand pivot").transform;
                    pivotH.SetParent(clock, false);
                    pivotH.position = fc + Vector3.forward * (0.012f * dir);
                    GameObject hh = Detail("Hour hand", pivotH, pivotH.position + Vector3.up * 0.17f, new Vector3(0.045f, 0.4f, 0.008f), blackMat);
                    hh.transform.SetParent(pivotH, true);
                    var pivotM = new GameObject("Minute hand pivot").transform;
                    pivotM.SetParent(clock, false);
                    pivotM.position = fc + Vector3.forward * (0.02f * dir);
                    GameObject mh = Detail("Minute hand", pivotM, pivotM.position + Vector3.up * 0.25f, new Vector3(0.03f, 0.58f, 0.008f), blackMat);
                    mh.transform.SetParent(pivotM, true);
                    foreach (GameObject hand in new[] { hh, mh }) GameObjectUtility.SetStaticEditorFlags(hand, 0);   // they move
                    Cyl("Clock centre cap", clock, fc + Vector3.forward * (0.02f * dir), fc + Vector3.forward * (0.03f * dir), 0.07f, blackMat);
                    hourHands[s] = pivotH;
                    minuteHands[s] = pivotM;
                    signs[s] = s == 0 ? -1f : 1f;                                 // clockwise as each viewer sees it
                }
                Decor("Clock rod", clock, c + new Vector3(-0.03f, 0.74f, -0.03f), new Vector3(0.03f, MallLayout.RoofY - 0.4f, -7.97f), metal);
                AfterHours.World.MallClock mc = clock.gameObject.AddComponent<AfterHours.World.MallClock>();
                mc.hourHands = hourHands;
                mc.minuteHands = minuteHands;
                mc.faceSign = signs;
            }

            // Escalators: Ground <-> Level 1 on the east side, Level 1 <-> Level 2 on the west side.
            float l1 = MallLayout.FloorY(MallFloor.Level1), l2 = MallLayout.FloorY(MallFloor.Level2);
            Escalator("Escalator G-1 up", parent, new Vector3(8f, 0f, -0.4f), new Vector3(8f, l1, MallLayout.VoidHalf), 1.1f);
            Escalator("Escalator G-1 down", parent, new Vector3(9.4f, 0f, -0.4f), new Vector3(9.4f, l1, MallLayout.VoidHalf), 1.1f);
            Escalator("Escalator 1-2 up", parent, new Vector3(-9.4f, l1, -MallLayout.VoidHalf), new Vector3(-9.4f, l2, 0.4f), 1.1f);
            Escalator("Escalator 1-2 down", parent, new Vector3(-8f, l1, -MallLayout.VoidHalf), new Vector3(-8f, l2, 0.4f), 1.1f);

            // Level 2 bridge: carries the west escalators' top landing to the north gallery.
            BoxMinMax("L2 bridge", parent, new Vector3(-10f, l2 - 0.4f, 0.4f), new Vector3(-7.2f, l2, 10f), floorTile, SurfaceType.Tile);
            Railing("Bridge railing", parent, new Vector3(-7.25f, 0f, 0.4f), new Vector3(-7.25f, 0f, 10f), l2);
            Railing("Bridge end railing", parent, new Vector3(-7.4f, 0f, 0.45f), new Vector3(-7.2f, 0f, 0.45f), l2);

            // Void balustrades with gaps where the escalators and bridge meet the galleries.
            float h = MallLayout.VoidHalf;
            Railing("L1 north", parent, new Vector3(-h, 0f, h), new Vector3(7.3f, 0f, h), l1);
            Railing("L1 south", parent, new Vector3(-7.3f, 0f, -h), new Vector3(h, 0f, -h), l1);
            Railing("L1 east", parent, new Vector3(h, 0f, -h), new Vector3(h, 0f, h), l1);
            Railing("L1 west", parent, new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), l1);
            Railing("L2 north", parent, new Vector3(-7.2f, 0f, h), new Vector3(h, 0f, h), l2);
            Railing("L2 south", parent, new Vector3(-h, 0f, -h), new Vector3(h, 0f, -h), l2);
            Railing("L2 east", parent, new Vector3(h, 0f, -h), new Vector3(h, 0f, h), l2);
            Railing("L2 west", parent, new Vector3(-h, 0f, -h), new Vector3(-h, 0f, 0.4f), l2);

            // Lights: gallery ring on every floor and big pendants in the void.
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                PointLight(parent, new Vector3(13f, y + 4.2f, 0f), 10f, 0.9f, FluorescentWhite);
                PointLight(parent, new Vector3(-13f, y + 4.2f, 0f), 10f, 0.9f, FluorescentWhite);
                PointLight(parent, new Vector3(0f, y + 4.2f, 13f), 10f, 0.9f, FluorescentWhite);
                PointLight(parent, new Vector3(0f, y + 4.2f, -13f), 10f, 0.9f, FluorescentWhite);
            }
            PointLight(parent, new Vector3(5f, 15f, 5f), 26f, 1.4f, WarmWhite, false);
            PointLight(parent, new Vector3(-5f, 15f, -5f), 26f, 1.4f, WarmWhite, false);
            PointLight(parent, new Vector3(0f, 3.5f, -10f), 10f, 0.8f, WarmWhite, false);
        }

        // =====================================================================
        // Lift shafts (cars, doors and buttons: MallBuilder.Machines)
        // =====================================================================

        static readonly MallFloor[] LiftLevels = { MallFloor.CarPark, MallFloor.Ground, MallFloor.Level1, MallFloor.Level2 };

        static void BuildLiftShafts(Transform parent)
        {
            BuildShaft(Group("Public lift A", parent), MallLayout.PublicLiftA, "Public lift A");
            BuildShaft(Group("Public lift B", parent), MallLayout.PublicLiftB, "Public lift B");
            BuildShaft(Group("Service lift", parent), MallLayout.ServiceLift, "Service lift");
        }

        static void BuildShaft(Transform parent, AreaRect r, string name)
        {
            const float t = 0.15f;
            float bottom = MallLayout.CarParkY;
            float top = MallLayout.FloorY(MallFloor.Level2) + MallLayout.WallTop;

            BoxMinMax("Shaft wall back", parent, new Vector3(r.minX, bottom, r.maxZ - t), new Vector3(r.maxX, top, r.maxZ), wallService, SurfaceType.Concrete);
            BoxMinMax("Shaft wall left", parent, new Vector3(r.minX, bottom, r.minZ), new Vector3(r.minX + t, top, r.maxZ - t), wallService, SurfaceType.Concrete);
            BoxMinMax("Shaft wall right", parent, new Vector3(r.maxX - t, bottom, r.minZ), new Vector3(r.maxX, top, r.maxZ - t), wallService, SurfaceType.Concrete);
            BoxMinMax("Shaft cap", parent, new Vector3(r.minX, top, r.minZ), new Vector3(r.maxX, top + 0.3f, r.maxZ), wallService, SurfaceType.Concrete);

            float width = r.Width - 2f * t;
            bool service = name.StartsWith("Service");
            float doorW = LiftDoorWidth(service);
            for (int i = 0; i < LiftLevels.Length; i++)
            {
                float y0 = MallLayout.FloorY(LiftLevels[i]);
                float y1 = i + 1 < LiftLevels.Length ? MallLayout.FloorY(LiftLevels[i + 1]) : top;
                Vector3 p0 = new Vector3(r.minX + t, 0f, r.minZ + t * 0.5f);
                Vector3 p1 = new Vector3(r.maxX - t, 0f, r.minZ + t * 0.5f);
                float s0 = (width - doorW) * 0.5f;
                WallSpan("Shaft front", parent, p0, p1, t, y0, y1, new List<Opening> { new Opening(s0, s0 + doorW, DoorHeight) }, wallService, SurfaceType.Concrete);

                float cx = (r.minX + r.maxX) * 0.5f;
                EditorBuild.Label(parent, MallLayout.FloorCode(LiftLevels[i]), new Vector3(cx, y0 + 2.75f, r.minZ - 0.02f), 0f, 0.03f);
            }
            BuildLift(parent, r, name, service);
        }

        // =====================================================================
        /// <summary>The storm: rain sources at the skylight, the entrance glass and on the roof, and the lightning light.</summary>
        static void BuildWeather(Transform parent)
        {
            float roof = MallLayout.RoofY;
            AudioSource Src(string name, Vector3 p, float vol, float min, float max)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = p;
                AudioSource s = go.AddComponent<AudioSource>();
                s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = min; s.maxDistance = max;
                s.volume = vol; s.dopplerLevel = 0f; s.playOnAwake = false; s.spread = 120f;
                go.AddComponent<AfterHours.Audio.AudioOcclusion>();
                return s;
            }
            var sources = new[]
            {
                Src("Rain on the skylight", new Vector3(0f, roof - 0.5f, 0f), 0.55f, 8f, 45f),
                Src("Rain on the entrance glass", new Vector3(0f, 2.5f, -MallLayout.EntranceHallEnd + 1f), 0.35f, 4f, 30f),
                Src("Rain on the roof", new Vector3(0f, roof + 2f, 25f), 0.8f, 30f, 80f),
                Src("Rain on the roof", new Vector3(0f, roof + 2f, -25f), 0.8f, 30f, 80f),
            };
            var lg = new GameObject("Lightning");
            lg.transform.SetParent(parent, false);
            lg.transform.rotation = Quaternion.Euler(70f, 30f, 0f);
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(0.75f, 0.82f, 1f);
            l.intensity = 0f;
            l.shadows = LightShadows.Hard;          // only what the sky can see: through the skylight and glass
            l.enabled = false;
            AfterHours.World.Weather w = parent.gameObject.AddComponent<AfterHours.World.Weather>();
            w.rainSources = sources;
            w.lightning = l;
            // The sky itself: sheets above the skylight and beyond the road, lit up by every strike.
            Material sky = AdditiveMaterial("Storm_Sky", "decal_light_beam.png", new Color(0.75f, 0.82f, 1f, 0.025f));
            sky.SetTexture("_BaseMap", null);
            sky.mainTexture = null;
            Renderer SkySheet(string name, Vector3 centre, Quaternion rot, Vector2 size)
            {
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = name;
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(parent, false);
                q.transform.SetPositionAndRotation(centre, rot);
                q.transform.localScale = new Vector3(size.x, size.y, 1f);
                Renderer r = q.GetComponent<Renderer>();
                r.sharedMaterial = sky;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                GameObjectUtility.SetStaticEditorFlags(q, 0);
                return r;
            }
            float e = MallLayout.EntranceHallEnd;
            w.skyFlash = new[]
            {
                SkySheet("Sky over the skylight", new Vector3(0f, roof + 35f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector2(240f, 240f)),
                SkySheet("Sky beyond the road", MallLayout.WingPoint(WingId.South, e + 34f, 0f, 10f), Quaternion.LookRotation(MallLayout.WingForward(WingId.South)), new Vector2(120f, 30f)),
            };

            // Rain you can see: a sheet of it outside the entrance glass (under the street lamps and every flash),
            // and splashes hitting the skylight panes, seen from below.
            Material streak = AdditiveMaterial("Rain_Streak", "decal_dust_mote.png", new Color(0.6f, 0.68f, 0.8f, 0.25f));
            void Rain(string name, Vector3 centre, Vector3 size, bool splash, float rate)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = centre;
                AfterHours.World.RainView rv = go.AddComponent<AfterHours.World.RainView>();
                rv.material = streak;
                rv.size = size;
                rv.splashes = splash;
                rv.rate = rate;
            }
            BuildForecourt(parent);
            float end = MallLayout.EntranceHallEnd;
            // beyond the canopy (it keeps the strip by the doors dry)
            Rain("Rain outside the entrance", MallLayout.WingPoint(WingId.South, end + 8f, 0f, 3.5f), new Vector3(30f, 7f, 9f), false, 2600f);
            Rain("Rain dripping off the canopy", MallLayout.WingPoint(WingId.South, end + 3.45f, 0f, 1.9f), new Vector3(16f, 3.8f, 0.1f), false, 260f);
            {
                // out on the roof it rains on you (the box follows whoever is up there)
                var go = new GameObject("Rain on the roof (around you)");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(32f, roof + 3f, 32f);
                AfterHours.World.RainView rv = go.AddComponent<AfterHours.World.RainView>();
                rv.material = streak;
                rv.size = new Vector3(18f, 8f, 18f);
                rv.rate = 2600f;
                rv.followCamera = true;
                float q0 = MallLayout.OuterWall, q1 = MallLayout.QuadrantOuter;
                rv.followArea = new Bounds(new Vector3((q0 + q1) * 0.5f, roof + 4f, (q0 + q1) * 0.5f), new Vector3(q1 - q0 + 4f, 8f, q1 - q0 + 4f));
            }
            Rain("Rain on the skylight", new Vector3(0f, roof + 0.02f, 0f), new Vector3(MallLayout.VoidHalf * 2f, 0f, MallLayout.VoidHalf * 2f), true, 500f);
        }

        /// <summary>
        /// Outside the main entrance, seen through the smoked glass: a strip of wet paving under a canopy, a kerb and the
        /// road, bollards, and one sodium street lamp throwing orange light through the rain (the other one is dead).
        /// You can't get out there - the doors are locked - but you can see the storm.
        /// </summary>
        static void BuildForecourt(Transform parent)
        {
            Transform g = Group("Forecourt", parent);
            WingId w = WingId.South;
            float e = MallLayout.EntranceHallEnd;
            Material paving = ArtLibrary.Surface("Paving_Wet", "concrete", 1.5f, new Color(0.42f, 0.42f, 0.44f), 1f, 1f);
            Material road = ArtLibrary.Plain("Asphalt_Wet", new Color(0.05f, 0.05f, 0.055f), 0.88f);
            Material sodiumGlow = ArtLibrary.Plain("Lamp_Sodium", new Color(1f, 0.6f, 0.25f), 0.5f, 0f, new Color(6f, 2.6f, 0.6f), false);

            WingBox("Forecourt paving", g, w, e + 0.15f, e + 11f, -26f, 26f, -0.2f, 0f, paving, SurfaceType.Concrete);
            WingBox("Kerb", g, w, e + 11f, e + 11.2f, -26f, 26f, -0.2f, 0.02f, concrete, SurfaceType.Concrete);
            WingBox("Road", g, w, e + 11.2f, e + 30f, -40f, 40f, -0.35f, -0.13f, road, SurfaceType.Concrete);
            for (float v = -24f; v <= 24.1f; v += 6f)
                WingBox("Road marking", g, w, e + 20f, e + 20.12f, v - 1.5f, v + 1.5f, -0.13f, -0.125f, lineMat, SurfaceType.Concrete, false);
            // Standing water: puddles in the dips of the paving and along the gutter, catching the lamp.
            (float u, float v, float a, float b)[] puddles = { (5.2f, -3f, 1.6f, 0.9f), (7.5f, 4.2f, 2.4f, 1.3f), (6.1f, 11f, 1.1f, 0.7f), (8.8f, -9.5f, 1.8f, 1.1f), (10.7f, 0f, 0.45f, 14f), (13.5f, 6f, 3f, 1.8f) };
            foreach (var (u, v, a, b) in puddles)
            {
                GameObject pd = WingBox("Puddle", g, w, e + u - a * 0.5f, e + u + a * 0.5f, v - b * 0.5f, v + b * 0.5f, u > 11f ? -0.13f : 0f, (u > 11f ? -0.13f : 0f) + 0.004f, waterMat, SurfaceType.Concrete, false);
                Object.DestroyImmediate(pd.GetComponent<Collider>());
            }
            // Canopy over the doors, dripping at its edge.
            WingBox("Entrance canopy", g, w, e + 0.15f, e + 3.4f, -8f, 8f, 3.75f, 4.05f, metal, SurfaceType.Metal);
            WingBox("Canopy fascia", g, w, e + 3.3f, e + 3.4f, -8f, 8f, 3.55f, 4.05f, darkPanel, SurfaceType.Metal);
            // Bollards along the kerb.
            for (float v = -18f; v <= 18.1f; v += 3f)
            {
                if (Mathf.Abs(v) < 1f) continue;
                WingBox("Bollard", g, w, e + 9.4f, e + 9.6f, v - 0.1f, v + 0.1f, 0f, 0.9f, darkPanel, SurfaceType.Metal);
                WingBox("Bollard band", g, w, e + 9.38f, e + 9.62f, v - 0.11f, v + 0.11f, 0.72f, 0.78f, whiteFixture, SurfaceType.Metal, false);
            }
            // Two street lamps; only one works.
            for (int i = 0; i < 2; i++)
            {
                float v = i == 0 ? 6.5f : -14f;
                WingBox("Lamp post", g, w, e + 10.3f, e + 10.45f, v - 0.075f, v + 0.075f, 0f, 6.5f, metal, SurfaceType.Metal);
                WingBox("Lamp arm", g, w, e + 9f, e + 10.45f, v - 0.05f, v + 0.05f, 6.4f, 6.5f, metal, SurfaceType.Metal, false);
                WingBox("Lamp head", g, w, e + 8.7f, e + 9.5f, v - 0.22f, v + 0.22f, 6.3f, 6.45f, darkPanel, SurfaceType.Metal, false);
                bool lit = i == 0;
                GameObject lens = WingBox("Lamp lens", g, w, e + 8.75f, e + 9.45f, v - 0.18f, v + 0.18f, 6.27f, 6.3f, lit ? sodiumGlow : darkGlass, SurfaceType.Glass, false);
                Object.DestroyImmediate(lens.GetComponent<Collider>());
                if (!lit) continue;
                var lg = new GameObject("Street lamp (sodium)");
                lg.transform.SetParent(g, false);
                lg.transform.position = MallLayout.WingPoint(w, e + 9.1f, v, 6.2f);
                lg.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                Light l = lg.AddComponent<Light>();
                l.type = LightType.Spot;
                l.spotAngle = 120f;
                l.innerSpotAngle = 60f;
                l.range = 16f;
                l.intensity = 9f;
                l.color = new Color(1f, 0.62f, 0.3f);
                l.shadows = LightShadows.Soft;
                l.lightmapBakeType = LightmapBakeType.Realtime;
                // a faint orange halo hanging in the wet air around the head
                Material halo = AdditiveMaterial("Lamp_Halo", "decal_light_beam.png", new Color(1f, 0.55f, 0.2f, 0.05f));
                Shaft(g, MallLayout.WingPoint(w, e + 9.1f, v, 6.25f), new Vector3(0.02f, -1f, 0f).normalized, 6.2f, 3.2f, halo);
            }
            ForecourtDetail(g);
            // Nobody can get out here (the doors are the escape), so nothing outside is solid: no stray island on
            // Elliot's navigation map, nothing for physics to bother with.
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        // Roof
        // =====================================================================

        static void BuildRoof(Transform parent)
        {
            float y = MallLayout.RoofY;
            var holes = new List<AreaRect>
            {
                new AreaRect(-MallLayout.VoidHalf, -MallLayout.VoidHalf, MallLayout.VoidHalf, MallLayout.VoidHalf),
                MallLayout.CoreRect(1, 1)
            };
            foreach (AreaRect region in MallLayout.FloorRegions(MallFloor.Level2))
            {
                Slab("Roof", parent, region, holes, y, MallLayout.SlabThickness, concrete, SurfaceType.Concrete);
            }
            // Clear glazing on a steel grid: after baking, moonlight comes through in stripes.
            BoxMinMax("Atrium skylight", parent, new Vector3(-MallLayout.VoidHalf, y - 0.15f, -MallLayout.VoidHalf),
                new Vector3(MallLayout.VoidHalf, y - 0.05f, MallLayout.VoidHalf), glassMat, SurfaceType.Glass);
            for (float m = -MallLayout.VoidHalf; m <= MallLayout.VoidHalf + 0.01f; m += 2.5f)
            {
                BoxMinMax("Skylight mullion", parent, new Vector3(m - 0.05f, y - 0.35f, -MallLayout.VoidHalf), new Vector3(m + 0.05f, y - 0.15f, MallLayout.VoidHalf), metal, SurfaceType.Metal);
                BoxMinMax("Skylight mullion", parent, new Vector3(-MallLayout.VoidHalf, y - 0.33f, m - 0.04f), new Vector3(MallLayout.VoidHalf, y - 0.17f, m + 0.04f), metal, SurfaceType.Metal);
            }

            // Accessible roof area above the north-east back-of-house block: parapets and security fencing.
            float q0 = MallLayout.OuterWall, q1 = MallLayout.QuadrantOuter;
            AreaRect core = MallLayout.CoreRect(1, 1);
            BoxMinMax("Parapet E", parent, new Vector3(q1 - 0.3f, y, q0), new Vector3(q1, y + 1.2f, q1), concrete, SurfaceType.Concrete);
            BoxMinMax("Parapet N", parent, new Vector3(q0, y, q1 - 0.3f), new Vector3(q1 - 0.3f, y + 1.2f, q1), concrete, SurfaceType.Concrete);
            BoxMinMax("Fence W", parent, new Vector3(q0, y, core.maxZ), new Vector3(q0 + 0.08f, y + 2.6f, q1 - 0.3f), metal, SurfaceType.Metal);
            BoxMinMax("Fence S", parent, new Vector3(core.maxX, y, q0), new Vector3(q1 - 0.3f, y + 2.6f, q0 + 0.08f), metal, SurfaceType.Metal);
            BoxMinMax("Roof plant unit", parent, new Vector3(34f, y, 34f), new Vector3(39f, y + 2.2f, 37f), plantMat, SurfaceType.Metal);
            BoxMinMax("Roof plant unit", parent, new Vector3(28f, y, 38f), new Vector3(31f, y + 1.6f, 43f), plantMat, SurfaceType.Metal);
            PointLight(parent, new Vector3(32f, y + 4f, 32f), 22f, 0.35f, new Color(0.6f, 0.7f, 0.9f), false);
            // Rainwater lying in the dips of the flat roof.
            (float x, float z, float a, float b)[] puddles = { (24f, 40f, 2.6f, 1.4f), (36.5f, 26f, 1.8f, 3.2f), (42f, 41.5f, 3.4f, 1.6f), (27f, 29.5f, 1.2f, 0.9f), (40.5f, 33f, 1.5f, 1.1f) };
            foreach (var (px, pz, pa, pb) in puddles)
            {
                GameObject pd = BoxMinMax("Roof puddle", parent, new Vector3(px - pa * 0.5f, y, pz - pb * 0.5f), new Vector3(px + pa * 0.5f, y + 0.004f, pz + pb * 0.5f), waterMat, SurfaceType.Concrete, false);
                Object.DestroyImmediate(pd.GetComponent<Collider>());
            }
            RoofDetail(parent);
        }

        // =====================================================================
        // Escape routes (exit points; puzzles and sequences are Part 7)
        // =====================================================================

        static void BuildEscapeMarkers(Transform parent)
        {
            foreach (EscapeRouteId id in System.Enum.GetValues(typeof(EscapeRouteId)))
            {
                var go = new GameObject("Escape " + (int)id + " - " + id);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = MallLayout.EscapePoint(id);
                go.AddComponent<EscapeRouteMarker>().route = id;
            }
        }
    }
}
