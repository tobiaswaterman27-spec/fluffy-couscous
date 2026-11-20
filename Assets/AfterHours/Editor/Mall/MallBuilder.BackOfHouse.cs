using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    public static partial class MallBuilder
    {
        // =====================================================================
        // Stair cores: switchback stairs from the car park to Level 2 (north-east core continues to the roof).
        // Core-local coordinates: lx = 0..6 outwards from the wing-corridor wall, lz = 0..9 along the wing.
        // =====================================================================

        const float CoreW = 6f, CoreL = 9f, CoreT = 0.25f;

        static Vector3 CoreP(int sx, int sz, float lx, float y, float lz)
        {
            return new Vector3(sx * (MallLayout.CoreLocal.minX + lx), y, sz * (MallLayout.CoreLocal.minZ + lz));
        }

        static GameObject CoreBox(string name, Transform parent, int sx, int sz, float lx0, float lz0, float y0, float lx1, float lz1, float y1, Material m, SurfaceType s)
        {
            return BoxMinMax(name, parent, CoreP(sx, sz, lx0, y0, lz0), CoreP(sx, sz, lx1, y1, lz1), m, s);
        }

        static void BuildCores(Transform parent)
        {
            foreach (var (sx, sz) in MallLayout.Quadrants)
            {
                bool toRoof = sx > 0 && sz > 0;
                string name = "Stair core " + (sz > 0 ? "N" : "S") + (sx > 0 ? "E" : "W");
                BuildCore(Group(name, parent), sx, sz, toRoof);
            }
        }

        static void BuildCore(Transform g, int sx, int sz, bool toRoof)
        {
            var levels = new List<float> { MallLayout.CarParkY, 0f, MallLayout.FloorY(MallFloor.Level1), MallLayout.FloorY(MallFloor.Level2) };
            if (toRoof) levels.Add(MallLayout.RoofY);
            float bottom = levels[0];
            float top = toRoof ? MallLayout.RoofY + 3f : MallLayout.FloorY(MallFloor.Level2) + MallLayout.WallTop;

            // Fixed walls.
            CoreBox("Core wall (wing side)", g, sx, sz, 0f, 0f, bottom, CoreW, CoreT, top, wallService, SurfaceType.Concrete);
            CoreBox("Core wall (far end)", g, sx, sz, 0f, CoreL - CoreT, bottom, CoreW, CoreL, top, wallService, SurfaceType.Concrete);
            CoreBox("Core cap", g, sx, sz, 0f, 0f, top, CoreW, CoreL, top + 0.3f, concrete, SurfaceType.Concrete);
            CoreBox("Spine wall", g, sx, sz, 2.9f, 2.25f, bottom, 3.1f, 6.75f, top, wallService, SurfaceType.Concrete);

            for (int i = 0; i < levels.Count; i++)
            {
                float y0 = levels[i];
                float y1 = i + 1 < levels.Count ? levels[i + 1] : top;
                bool roofLevel = toRoof && i == levels.Count - 1;

                // Corridor-side wall: a door on every level (except the roof landing).
                var inner = new List<Opening>();
                if (!roofLevel) inner.Add(new Opening(0.6f - CoreT, 1.8f - CoreT, DoorHeight));
                WallSpan("Core wall (door side)", g, CoreP(sx, sz, CoreT * 0.5f, 0f, CoreT), CoreP(sx, sz, CoreT * 0.5f, 0f, CoreL - CoreT),
                    CoreT, y0, y1, inner, wallService, SurfaceType.Concrete);
                if (!roofLevel)
                {
                    DoorMarker(g, "Stairwell door", CoreP(sx, sz, 0f, y0, 1.2f), DoorKind.Stairwell, 1.2f, DoorHeight, false, "Fire door into stairwell");
                }

                // Outer wall: solid, except the roof door.
                var outer = new List<Opening>();
                if (roofLevel) outer.Add(new Opening(0.6f - CoreT, 1.8f - CoreT, DoorHeight));
                WallSpan("Core wall (outer)", g, CoreP(sx, sz, CoreW - CoreT * 0.5f, 0f, CoreT), CoreP(sx, sz, CoreW - CoreT * 0.5f, 0f, CoreL - CoreT),
                    CoreT, y0, y1, outer, wallService, SurfaceType.Concrete);
                if (roofLevel)
                {
                    DoorMarker(g, "Roof door", CoreP(sx, sz, CoreW, y0, 1.2f), DoorKind.Restricted, 1.2f, DoorHeight, true, "Roof access door (escape route 5)", "key_roof");
                }

                // Landing at this level.
                CoreBox("Landing", g, sx, sz, CoreT, CoreT, y0 - 0.3f, CoreW - CoreT, 2.25f, y0, concrete, SurfaceType.Concrete);
                string code = i == 0 ? "P" : roofLevel ? "R" : MallLayout.FloorCode((MallFloor)(i - 1));
                EditorBuild.Label(g, code, CoreP(sx, sz, 3f, y0 + 2.3f, 2.2f), sz > 0 ? 0f : 180f, 0.08f);
                PointLight(g, CoreP(sx, sz, 1.5f, y0 + 2.6f, 1.2f), 7f, 0.6f, FluorescentWhite, false);

                if (i + 1 >= levels.Count) continue;

                // Two flights with a half landing.
                float h = levels[i + 1] - y0;
                float half = h * 0.5f;
                int n = Mathf.Max(1, Mathf.RoundToInt(half / 0.18f));
                float rise = half / n;
                float run = 4.5f / n;
                for (int k = 0; k < n; k++)
                {
                    float topA = y0 + (k + 1) * rise;
                    CoreBox("Step", g, sx, sz, CoreT, 2.25f + k * run, topA - 0.4f, 2.9f, 2.25f + (k + 1) * run, topA, concrete, SurfaceType.Concrete);
                    float topB = y0 + half + (k + 1) * rise;
                    CoreBox("Step", g, sx, sz, 3.1f, 6.75f - (k + 1) * run, topB - 0.4f, CoreW - CoreT, 6.75f - k * run, topB, concrete, SurfaceType.Concrete);
                }
                CoreBox("Half landing", g, sx, sz, CoreT, 6.75f, y0 + half - 0.3f, CoreW - CoreT, CoreL - CoreT, y0 + half, concrete, SurfaceType.Concrete);
            }
        }

        // =====================================================================
        // Back-of-house quadrants between the wings
        // =====================================================================

        static void BuildQuadrants(Transform parent)
        {
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                foreach (var (sx, sz) in MallLayout.Quadrants)
                {
                    string name = $"{MallLayout.FloorCode(f)} {(sz > 0 ? "N" : "S")}{(sx > 0 ? "E" : "W")} block";
                    BuildQuadrant(Group(name, parent), f, sx, sz);
                }
            }
        }

        static void BuildQuadrant(Transform g, MallFloor f, int sx, int sz)
        {
            float y = MallLayout.FloorY(f);
            float o = MallLayout.OuterWall, q = MallLayout.QuadrantOuter, d = MallLayout.QuadrantDivider;
            bool loadingBay = f == MallFloor.Ground && sx > 0 && sz > 0;
            bool staffExit = f == MallFloor.Ground && sx > 0 && sz < 0;

            // Divider between room A and room B, with a connecting door.
            WallSpan("Room divider", g, new Vector3(sx * o, 0f, sz * d), new Vector3(sx * (q - 0.3f), 0f, sz * d), 0.2f, y, y + MallLayout.WallTop,
                new List<Opening> { new Opening(39.4f - o, 40.6f - o, DoorHeight) }, wallService, SurfaceType.Concrete);
            DoorMarker(g, "Room A-B door", new Vector3(sx * 40f, y, sz * d), DoorKind.Staff, 1.2f, DoorHeight, true,
                "Internal door between back-of-house rooms: sealed (no key), so locked rooms can't be bypassed", "sealed");

            // Exterior walls.
            var eastOpenings = new List<Opening>();
            if (loadingBay)
            {
                eastOpenings.Add(new Opening(21f - o, 25.5f - o, ShopOpeningHeight));
                eastOpenings.Add(new Opening(27f - o, 31.5f - o, ShopOpeningHeight));
            }
            WallSpan("Exterior wall", g, new Vector3(sx * (q - 0.15f), 0f, sz * o), new Vector3(sx * (q - 0.15f), 0f, sz * q), 0.3f, y, y + MallLayout.WallTop,
                eastOpenings, wallService, SurfaceType.Concrete);
            var northOpenings = new List<Opening>();
            if (staffExit) northOpenings.Add(new Opening(39.4f - o, 40.6f - o, DoorHeight));
            WallSpan("Exterior wall", g, new Vector3(sx * o, 0f, sz * (q - 0.15f)), new Vector3(sx * (q - 0.3f), 0f, sz * (q - 0.15f)), 0.3f, y, y + MallLayout.WallTop,
                northOpenings, wallService, SurfaceType.Concrete);

            if (loadingBay)
            {
                foreach (var (z0, z1) in new[] { (21f, 25.5f), (27f, 31.5f) })
                {
                    GameObject s = BoxMinMax("Dock shutter", g, new Vector3(sx * (q - 0.2f), y, sz * z0), new Vector3(sx * (q - 0.1f), y + ShopOpeningHeight, sz * z1), shutterMat, SurfaceType.Metal);
                    Slot(s, DoorKind.Shutter, z1 - z0, ShopOpeningHeight, true, "Loading dock roller shutter (escape route 3)");
                    BoxMinMax("Dock leveller", g, new Vector3(sx * (q - 3f), y, sz * (z0 + 0.3f)), new Vector3(sx * (q - 0.3f), y + 0.05f, sz * (z1 - 0.3f)), metal, SurfaceType.Metal);
                }
            }
            if (loadingBay) LoadingDockDetail(g, y);
            if (staffExit)
            {
                DoorMarker(g, "Staff exit", new Vector3(sx * 40f, y, sz * q), DoorKind.Fire, 1.2f, DoorHeight, true, "Restricted staff exit (escape route 7)", "staff_exit");
            }

            // Rooms.
            MallLayout.QuadrantRoom ra = MallLayout.QuadrantRoomA(f, sx, sz);
            MallLayout.QuadrantRoom rb = MallLayout.QuadrantRoomB(f, sx, sz);
            AreaRect rectA = MallLayout.QuadrantRoomARect(sx, sz);
            AreaRect rectB = MallLayout.QuadrantRoomBRect(sx, sz);
            AreaRect core = MallLayout.CoreRect(sx, sz).Expanded(1.2f);
            var clearA = new List<AreaRect>
            {
                core,
                new AreaRect(33.5f, o, 36.5f, 22.5f).Mirrored(sx, sz),          // corridor door
                new AreaRect(38.2f, d - 3f, 41.8f, d).Mirrored(sx, sz),         // divider door
                new AreaRect(25.5f, d - 1.6f, 33.5f, d).Mirrored(sx, sz),       // lockers / cupboards row
            };
            if (loadingBay) clearA.Add(new AreaRect(38f, o, q, d).Mirrored(sx, sz));
            var clearB = new List<AreaRect>
            {
                new AreaRect(o, 36.3f, 22.8f, 39.7f).Mirrored(sx, sz),          // corridor door
                new AreaRect(38.2f, d, 41.8f, d + 3f).Mirrored(sx, sz),         // divider door
                MallLayout.ServiceLift.Expanded(2f),
                new AreaRect(37f, 41f, 43f, q).Mirrored(sx, sz),                 // exterior door (staff exit)
                new AreaRect(25.5f, d, 33.5f, d + 1.6f).Mirrored(sx, sz),        // lockers / cupboards row
            };
            int seed = MallLayout.Hash((int)f, sx, sz, 99);
            if (ra.type == FacilityType.Security && sx < 0 && sz > 0)
            {
                clearA.Add(SecurityConsoleRect);
                SecurityMonitors(g, y);
            }
            StaffRoomFeature(g, f, sx, sz, y, rb.label, clearB);
            plantRoomLabel = ra.label;
            Furnish(g, Inset(rectA, 0.4f), y, ra.style, clearA, seed, propMat);
            plantRoomLabel = rb.label;
            Furnish(g, Inset(rectB, 0.4f), y, rb.style, clearB, seed + 1, propMat);
            plantRoomLabel = null;

            Vector2 ca = rectA.Center, cb = rectB.Center;
            EditorBuild.Label(g, ra.label.ToUpperInvariant(), new Vector3(ca.x, y + 3.6f, ca.y), sz > 0 ? 0f : 180f, 0.06f);
            EditorBuild.Label(g, rb.label.ToUpperInvariant(), new Vector3(cb.x, y + 3.6f, cb.y), sz > 0 ? 0f : 180f, 0.06f);
            PointLight(g, new Vector3(sx * 36f, y + 4.1f, sz * 26f), 13f, 0.7f, FluorescentWhite);
            PointLight(g, new Vector3(sx * 32f, y + 4.1f, sz * 38.5f), 13f, 0.7f, FluorescentWhite);
        }

        static AreaRect Inset(AreaRect r, float m) => new AreaRect(r.minX + m, r.minZ + m, r.maxX - m, r.maxZ - m);

        // =====================================================================
        // Anchor stores at the end of every wing
        // =====================================================================

        static readonly List<Transform> MainEntrancePanels = new List<Transform>();

        static void BuildAnchors(Transform parent)
        {
            MainEntrancePanels.Clear();
            foreach (MallUnit a in MallLayout.Units)
            {
                if (!a.isAnchor) continue;
                BuildAnchor(Group(a.code + " " + a.name, parent), a);
            }
        }

        /// <summary>A frame or bar that slides with its glass door: no collider of its own, never combined.</summary>
        static void DoorPart(GameObject part, GameObject door)
        {
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(door.transform, true);
        }

        static void BuildAnchor(Transform g, MallUnit a)
        {
            MallFloor f = a.floor;
            WingId w = a.wing;
            float y = MallLayout.FloorY(f);
            float u0 = MallLayout.WingEnd, end = MallLayout.AnchorEndFor(f, w);
            float half = MallLayout.OuterWall;
            bool entranceHall = f == MallFloor.Ground && w == WingId.South;
            bool fireExitHall = f == MallFloor.Ground && w == WingId.West;
            bool cinema = f == MallFloor.Level2 && w == WingId.East;
            bool foodCourt = f == MallFloor.Level2 && w == WingId.West;
            bool bowling = f == MallFloor.Level2 && w == WingId.North;

            // ---- Front wall (where the concourse ends) ----
            var front = new List<(float, float, float)>
            {
                (-18.1f, -16.9f, DoorHeight),
                (16.9f, 18.1f, DoorHeight)
            };
            if (entranceHall) front.Add((-6f, 6f, 4.2f));
            else if (fireExitHall)
            {
                front.Add((-5.5f, -2.5f, 2.4f));
                front.Add((-2f, 2f, 3f));
                front.Add((2.5f, 5.5f, ShopOpeningHeight));
            }
            else front.Add((-5f, 5f, 4f));
            WingWallV("Anchor front wall", g, w, y, u0 + 0.15f, -half, half, 0.3f, front, wallPaint, SurfaceType.Concrete);
            DoorMarker(g, "Corridor door", MallLayout.WingPoint(w, u0 + 0.15f, -17.5f, y), DoorKind.Staff, 1.2f, DoorHeight, false, "Service corridor into store");
            DoorMarker(g, "Corridor door", MallLayout.WingPoint(w, u0 + 0.15f, 17.5f, y), DoorKind.Staff, 1.2f, DoorHeight, false, "Service corridor into store");

            if (fireExitHall)
            {
                BuildClosure(g, a.code, MallLayout.WingPoint(w, u0 + 0.15f, 2.5f, y), MallLayout.WingPoint(w, u0 + 0.15f, 5.5f, y), y, a.access, a.name);
                DoorMarker(g, "Toilets", MallLayout.WingPoint(w, u0 + 0.15f, -4f, y), DoorKind.Toilet, 3f, 2.4f, false, "Public toilets");
            }
            else if (!entranceHall)
            {
                BuildClosure(g, a.code, MallLayout.WingPoint(w, u0 + 0.15f, -5f, y), MallLayout.WingPoint(w, u0 + 0.15f, 5f, y), y, a.access, a.name);
            }
            Vector3 view = MallLayout.WingForward(w);
            Sign(g, a.name.ToUpperInvariant(), MallLayout.WingPoint(w, u0 - 0.05f, 0f, y + 4.7f), view, 10f, 0.09f);

            // ---- End (exterior) wall ----
            var endOpenings = new List<(float, float, float)>();
            if (entranceHall) endOpenings.Add((-6f, 6f, 3f));
            if (fireExitHall) endOpenings.Add((-1f, 1f, 2.2f));
            WingWallV("Exterior wall", g, w, y, end - 0.15f, -half, half, 0.3f, endOpenings, wallService, SurfaceType.Concrete);

            if (entranceHall)
            {
                // Smoked glass: from inside at night you see the storm on the forecourt, under one orange street lamp.
                Material entranceGlass = TransparentMat("Glass_Entrance_Night", new Color(0.16f, 0.19f, 0.21f, 0.5f));
                for (int i = 0; i < 4; i++)
                {
                    float v0 = -6f + i * 3f;
                    GameObject door = WingBox("Main entrance glass door", g, w, end - 0.2f, end - 0.1f, v0 + 0.05f, v0 + 2.95f, y, y + 3f, entranceGlass, SurfaceType.Glass, false);
                    door.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    DoorPart(WingBox("Door frame", g, w, end - 0.22f, end - 0.08f, v0, v0 + 0.06f, y, y + 3f, metal, SurfaceType.Metal, false), door);
                    DoorPart(WingBox("Door frame", g, w, end - 0.22f, end - 0.08f, v0 + 2.94f, v0 + 3f, y, y + 3f, metal, SurfaceType.Metal, false), door);
                    DoorPart(WingBox("Push bar", g, w, end - 0.3f, end - 0.22f, v0 + 0.5f, v0 + 2.5f, y + 1.0f, y + 1.05f, metal, SurfaceType.Metal, false), door);
                    MainEntrancePanels.Add(door.transform);
                    if (i == 1) Slot(door, DoorKind.Glass, 12f, 3f, true, "Main entrance doors (escape route 1): locked, outside is black");
                }
                WingBox("Entrance mat", g, w, end - 4f, end - 0.3f, -6f, 6f, y, y + 0.02f, floorCarpet, SurfaceType.Carpet);
                WingBox("Bench", g, w, u0 + 3f, u0 + 5f, -14f, -13.5f, y, y + 0.45f, woodMat, SurfaceType.Wood);
                WingBox("Bench", g, w, u0 + 3f, u0 + 5f, 13.5f, 14f, y, y + 0.45f, woodMat, SurfaceType.Wood);
                Sign(g, "MAIN ENTRANCE", MallLayout.WingPoint(w, end - 0.35f, 0f, y + 3.5f), view, 8f, 0.08f);
                EntranceHallDetail(g, w, y, u0, end);
                AnchorLights(g, w, y, u0, end);
                return;
            }

            if (fireExitHall)
            {
                WingWallU("Corridor wall", g, w, y, u0 + 0.3f, end - 0.3f, 2.1f, 0.2f, null, wallService, SurfaceType.Concrete);
                WingWallU("Corridor wall", g, w, y, u0 + 0.3f, end - 0.3f, -2.1f, 0.2f, null, wallService, SurfaceType.Concrete);
                DoorMarker(g, "Fire exit", MallLayout.WingPoint(w, end, 0f, y), DoorKind.Fire, 2f, 2.2f, true, "Emergency exit doors (escape route 4)");
                Sign(g, "FIRE EXIT", MallLayout.WingPoint(w, end - 0.35f, 0f, y + 2.6f), view, 2f, 0.05f);
                Furnish(g, MallLayout.WingRect(w, u0 + 0.6f, end - 0.6f, -18.8f, -2.5f), y, FurnishStyle.Toilets,
                    new List<AreaRect> { MallLayout.WingRect(w, u0, u0 + 3f, -6f, -2f), MallLayout.WingRect(w, u0, u0 + 3f, -19.3f, -15.5f) }, 5, toiletMat);
                Furnish(g, MallLayout.WingRect(w, u0 + 0.6f, end - 0.6f, 2.5f, 18.8f), y, a.style,
                    new List<AreaRect> { MallLayout.WingRect(w, u0, u0 + 3f, 2f, 6f), MallLayout.WingRect(w, u0, u0 + 3f, 15.5f, 19.3f) }, 6, categoryMats[a.category]);
                PointLight(g, MallLayout.WingPoint(w, (u0 + end) * 0.5f, 0f, y + 3.6f), 14f, 0.5f, new Color(1f, 0.4f, 0.35f));
                AnchorLights(g, w, y, u0, end);
                return;
            }

            if (cinema) { BuildCinema(g, w, y, u0, end); return; }

            if (!a.HasInterior)
            {
                AnchorLights(g, w, y, u0, end, 0.3f);
                return;
            }

            AreaRect inside = MallLayout.WingRect(w, u0 + 0.8f, end - 0.8f, -18.6f, 18.6f);
            var clear = new List<AreaRect>
            {
                MallLayout.WingRect(w, u0, u0 + 4f, -6f, 6f),
                MallLayout.WingRect(w, u0, u0 + 3.5f, -19.3f, -15.5f),
                MallLayout.WingRect(w, u0, u0 + 3.5f, 15.5f, 19.3f),
            };

            if (a.category == UnitCategory.Department)
            {
                clear.Add(MallLayout.DepartmentStairHole.Expanded(1.5f));
                if (f == MallFloor.Ground) BuildDepartmentStair(g);
                else
                {
                    AreaRect h = MallLayout.DepartmentStairHole;
                    Railing("Stair hole railing", g, new Vector3(h.minX, 0f, h.minZ), new Vector3(h.minX, 0f, h.maxZ), y);
                    Railing("Stair hole railing", g, new Vector3(h.minX, 0f, h.minZ), new Vector3(h.maxX, 0f, h.minZ), y);
                    Railing("Stair hole railing", g, new Vector3(h.maxX, 0f, h.minZ), new Vector3(h.maxX, 0f, h.maxZ), y);
                }
            }

            if (foodCourt)
            {
                BuildFoodCourtKiosks(g, w, y, end);
                inside = MallLayout.WingRect(w, u0 + 1f, end - 7f, -18f, 18f);
            }
            if (bowling)
            {
                for (float v = -12f; v <= 12.1f; v += 4f)
                {
                    WingBox("Bowling lane", g, w, u0 + 8f, end - 1f, v - 0.6f, v + 0.6f, y, y + 0.04f, woodMat, SurfaceType.Wood);
                    WingBox("Ball return", g, w, u0 + 6f, u0 + 7.5f, v + 1.2f, v + 1.8f, y, y + 0.9f, metal, SurfaceType.Metal);
                }
                inside = MallLayout.WingRect(w, u0 + 0.8f, u0 + 5.5f, -18.6f, 18.6f);
                BowlingDetail(g, w, y, u0, end);
            }

            if (a.category == UnitCategory.Supermarket)
            {
                clear.Add(MallLayout.WingRect(w, u0, u0 + 4.6f, -19.3f, 19.3f));     // checkouts
                clear.Add(MallLayout.WingRect(w, end - 2.6f, end, -19.3f, 19.3f));   // freezers
                SupermarketDetail(g, w, y, u0, end);
            }
            if (a.category == UnitCategory.Department)
            {
                if (f == MallFloor.Ground) clear.Add(MallLayout.WingRect(w, u0 + 4.2f, u0 + 8f, -3.5f, 3.5f));
                clear.Add(MallLayout.WingRect(w, u0 + 4f, u0 + 5.2f, 8.8f, 10.2f));
                clear.Add(MallLayout.WingRect(w, u0 + 8.4f, u0 + 9.6f, 17.6f, 19.3f));
                HartleysFeature(g, f, w, y, u0, end);
            }
            if (a.name == "Toy Kingdom")
            {
                clear.Add(MallLayout.WingRect(w, u0 + 4f, u0 + 9.8f, -4f, 4f));
                ToyKingdomFeature(g, w, y, u0, end);
            }
            if (a.name == "Pro Sport")
            {
                clear.Add(MallLayout.WingRect(w, u0 + 4f, u0 + 9.8f, -4.5f, 4.5f));
                clear.Add(MallLayout.WingRect(w, end - 1.5f, end, -7.5f, 7.5f));
                ProSportFeature(g, w, y, u0, end);
            }
            if (a.name == "VoltMart Electronics")
            {
                clear.Add(MallLayout.WingRect(w, end - 5f, end, -8.5f, 8.5f));    // tech-help counter and peg wall
                ElectronicsAnchorDetail(g, w, y, u0, end);
            }
            if (a.name == "Pulse Gym") GymFloor(g, w, y, u0, end);
            else Furnish(g, inside, y, a.style, clear, MallLayout.Hash((int)f, (int)w, 3, 3), categoryMats[a.category]);
            AnchorLights(g, w, y, u0, end, a.Enterable ? 0.8f : 0.45f);
        }

        static void AnchorLights(Transform g, WingId w, float y, float u0, float end, float intensity = 0.7f)
        {
            for (float u = u0 + 5f; u < end - 2f; u += 9f)
            {
                for (float v = -12f; v <= 12.1f; v += 12f)
                {
                    PointLight(g, MallLayout.WingPoint(w, u, v, y + 4.1f), 12f, intensity, FluorescentWhite);
                }
            }
        }

        /// <summary>Straight staircase inside the department store, Ground to Level 1 (rises northwards).</summary>
        static void BuildDepartmentStair(Transform g)
        {
            AreaRect h = MallLayout.DepartmentStairHole;
            float height = MallLayout.FloorY(MallFloor.Level1);
            int n = Mathf.RoundToInt(height / 0.18f);
            float rise = height / n;
            float run = h.Depth / n;
            for (int k = 0; k < n; k++)
            {
                float z0 = h.minZ + k * run;
                BoxMinMax("Store stair step", g, new Vector3(h.minX + 0.1f, 0f, z0), new Vector3(h.maxX - 0.1f, (k + 1) * rise, z0 + run), woodMat, SurfaceType.Wood);
            }
            Railing("Store stair railing", g, new Vector3(h.minX + 0.05f, 0f, h.minZ), new Vector3(h.minX + 0.05f, 0f, h.minZ + 2f), 0f);
        }

        static void BuildFoodCourtKiosks(Transform g, WingId w, float y, float end)
        {
            const int count = 6;
            float span = 2f * MallLayout.OuterWall - 1f;
            float width = span / count;
            string[] names = { "Burger Barn", "Wok This Way", "Pizza Piazza", "Taco Shack", "Fish Bar", "Smoothie Stop" };
            for (int i = 0; i < count; i++)
            {
                float v0 = -MallLayout.OuterWall + 0.5f + i * width;
                float v1 = v0 + width;
                WingBox("Kiosk side wall", g, w, end - 6f, end - 0.3f, v0, v0 + 0.15f, y, y + 3.2f, wallService, SurfaceType.Concrete);
                WingBox("Kiosk counter", g, w, end - 6.6f, end - 6f, v0 + 0.6f, v1 - 0.8f, y, y + 1.1f, counterMat, SurfaceType.Wood);
                WingBox("Kiosk back counter", g, w, end - 1.2f, end - 0.3f, v0 + 0.4f, v1 - 0.4f, y, y + 0.95f, metal, SurfaceType.Metal);
                Sign(g, names[i].ToUpperInvariant(), MallLayout.WingPoint(w, end - 6.1f, (v0 + v1) * 0.5f, y + 2.8f), MallLayout.WingForward(w), width - 1f, 0.04f);
                FoodCourtKioskDetail(g, w, y, end, i, v0, v1);
            }
            FoodCourtTrayStation(g, w, y, end);
        }

        static void BuildCinema(Transform g, WingId w, float y, float u0, float end)
        {
            float lobbyEnd = 105f;
            WingBox("Ticket counter", g, w, 100f, 101f, -9f, -3f, y, y + 1.1f, counterMat, SurfaceType.Wood);
            WingBox("Concessions counter", g, w, 100f, 101f, 3f, 10f, y, y + 1.1f, counterMat, SurfaceType.Wood);
            Sign(g, "TICKETS", MallLayout.WingPoint(w, 100f, -6f, y + 2.2f), MallLayout.WingForward(w), 3f, 0.05f);
            Sign(g, "SNACKS", MallLayout.WingPoint(w, 100f, 6.5f, y + 2.2f), MallLayout.WingForward(w), 3f, 0.05f);

            // Screens: (name, v range, door range)
            var screens = new[]
            {
                ("SCREEN 1", -19.15f, -6.85f, -13.7f, -12.3f),
                ("SCREEN 2", -6.65f, 6.65f, -0.7f, 0.7f),
                ("SCREEN 3", 6.85f, 15.5f, 10.4f, 11.8f),
            };
            var doors = new List<(float, float, float)>();
            foreach (var s in screens) doors.Add((s.Item4, s.Item5, 2.2f));
            WingWallV("Lobby back wall", g, w, y, lobbyEnd, -MallLayout.OuterWall, MallLayout.OuterWall, 0.25f, doors, wallPaint, SurfaceType.Concrete);

            WingWallU("Screen partition", g, w, y, lobbyEnd, end - 0.3f, -6.75f, 0.2f, null, wallPaint, SurfaceType.Concrete);
            WingWallU("Screen partition", g, w, y, lobbyEnd, end - 0.3f, 6.75f, 0.2f, null, wallPaint, SurfaceType.Concrete);
            // Screen 3's back wall hides a disused passage (escape route 8). The door is easy to miss.
            WingWallU("Screen 3 back wall", g, w, y, lobbyEnd, end - 0.3f, 15.6f, 0.2f,
                new List<(float, float, float)> { (122.6f, 123.6f, 2f) }, wallPaint, SurfaceType.Concrete);
            DoorMarker(g, "Hidden door", MallLayout.WingPoint(w, 123.1f, 15.6f, y), DoorKind.Hidden, 1f, 2f, false,
                "Unmarked door behind screen 3, painted like the wall (escape route 8)");
            PointLight(g, MallLayout.WingPoint(w, 116f, 17.5f, y + 3f), 6f, 0.12f, WarmWhite, false);

            foreach (var s in screens)
            {
                float v0 = s.Item2, v1 = s.Item3;
                Sign(g, s.Item1, MallLayout.WingPoint(w, lobbyEnd - 0.15f, (s.Item4 + s.Item5) * 0.5f, y + 2.6f), MallLayout.WingForward(w), 2f, 0.04f);
                // Stepped seating rising towards the back of the room.
                for (int k = 0; k < 6; k++)
                {
                    float r0 = lobbyEnd + 1.5f + k * 1.3f, r1 = r0 + 1.3f;
                    float riserTop = y + (6 - k) * 0.3f;
                    WingBox("Seating riser", g, w, r0, r1, v0 + 0.1f, v1 - 0.1f, y, riserTop, carParkFloor, SurfaceType.Carpet);
                    GameObject row = WingBox("Seat row", g, w, r0 + 0.25f, r0 + 0.75f, v0 + 1.4f, v1 - 1.4f, riserTop, riserTop + 0.5f, seatMat, SurfaceType.Carpet);
                    CinemaSeats(g, w, row, r0, v0 + 1.4f, v1 - 1.4f, riserTop, k);
                }
                WingBox("Screen", g, w, end - 0.6f, end - 0.45f, v0 + 0.6f, v1 - 0.6f, y + 1.0f, y + 4.3f, screenMat, SurfaceType.Wood);
                PointLight(g, MallLayout.WingPoint(w, 116f, (v0 + v1) * 0.5f, y + 4f), 12f, 0.25f, WarmWhite);
            }
            AnchorLights(g, w, y, u0, lobbyEnd, 0.6f);
            CinemaLobbyDetail(g, w, y, lobbyEnd, screens);
        }
    }
}
