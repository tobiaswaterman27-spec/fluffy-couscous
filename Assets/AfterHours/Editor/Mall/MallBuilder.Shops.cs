using System.Collections.Generic;
using UnityEngine;
using AfterHours.Rendering;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    public static partial class MallBuilder
    {
        const float ShopOpeningHeight = 3.6f;
        const float DoorHeight = 2.1f;

        static void BuildWing(Transform parent, MallFloor f, WingId w)
        {
            float y = MallLayout.FloorY(f);
            for (int side = -1; side <= 1; side += 2)
            {
                BuildShopRow(Group(side > 0 ? "Right side" : "Left side", parent), f, w, side, y);
            }
            BuildConcourseFurniture(Group("Concourse", parent), f, w, y);
        }

        static void BuildShopRow(Transform parent, MallFloor f, WingId w, int side, float y)
        {
            var shops = new List<MallUnit>();
            foreach (MallUnit u in MallLayout.Units)
            {
                if (u.floor == f && u.wing == w && u.side == side && !u.isAnchor) shops.Add(u);
            }
            shops.Sort((a, b) => a.u0.CompareTo(b.u0));
            var passages = new List<Passage>();
            foreach (Passage p in MallLayout.Passages)
            {
                if (p.floor == f && p.wing == w && p.side == side) passages.Add(p);
            }

            Vector3 toConcourse = -MallLayout.WingRight(w) * side; // direction from the shops towards the concourse
            float frontV = side * (MallLayout.ConcourseHalf + 0.125f);
            float backV = side * (MallLayout.ShopBack + 0.1f);
            float corridorV = side * (MallLayout.CorridorOuter + 0.15f);

            var front = new List<(float, float, float)>();
            var back = new List<(float, float, float)>();
            var boundaries = new SortedSet<float>();

            foreach (MallUnit u in shops)
            {
                front.Add((u.u0 + 0.3f, u.u1 - 0.3f, ShopOpeningHeight));
                if (u.HasInterior) back.Add((u.u1 - 2.2f, u.u1 - 1.2f, DoorHeight));
                boundaries.Add(u.u0);
                boundaries.Add(u.u1);
                BuildShop(Group(u.code + " " + u.name, parent), u, y, side, toConcourse);
            }

            foreach (Passage p in passages)
            {
                front.Add((p.u0 + 0.9f, p.u1 - 0.9f, DoorHeight));
                back.Add((p.u0 + 0.9f, p.u1 - 0.9f, DoorHeight));
                boundaries.Add(p.u0);
                boundaries.Add(p.u1);
                float mid = (p.u0 + p.u1) * 0.5f;
                string label = p.isPublic ? "TOILETS" : "STAFF ONLY";
                DoorMarker(parent, label + " passage (front)", MallLayout.WingPoint(w, mid, side * MallLayout.ConcourseHalf, y),
                    p.isPublic ? DoorKind.Toilet : DoorKind.Staff, 1.2f, DoorHeight, !p.isPublic, "Passage from concourse to service corridor");
                DoorMarker(parent, label + " passage (back)", MallLayout.WingPoint(w, mid, side * MallLayout.ShopBack, y),
                    DoorKind.Staff, 1.2f, DoorHeight, false, "Passage into service corridor");
                Sign(parent, label, MallLayout.WingPoint(w, mid, side * (MallLayout.ConcourseHalf - 0.05f), y + 2.6f), -toConcourse, 2.2f, 0.035f);
                PointLight(parent, MallLayout.WingPoint(w, mid, side * 11f, y + 3.9f), 6f, 0.5f, FluorescentWhite);
            }

            // Shopfront line, back wall, corridor outer wall.
            WingWallU("Shopfront wall", parent, w, y, MallLayout.WingStart, MallLayout.WingEnd, frontV, 0.25f, front, wallPaint, SurfaceType.Concrete);
            WingWallU("Shop back wall", parent, w, y, MallLayout.WingStart, MallLayout.WingEnd, backV, 0.2f, back, wallService, SurfaceType.Concrete);

            var corridorDoors = new List<(float, float, float)>();
            var (qx, qz) = MallLayout.QuadrantBeside(w, side);
            bool alongZ = w == WingId.North || w == WingId.South;
            if (alongZ)
            {
                corridorDoors.Add((MallLayout.CoreDoorU0, MallLayout.CoreDoorU1, DoorHeight));
                corridorDoors.Add((MallLayout.RoomBDoorU0, MallLayout.RoomBDoorU1, DoorHeight));
                MallLayout.QuadrantRoom rb = MallLayout.QuadrantRoomB(f, qx, qz);
                CorridorDoorSign(parent, w, side, y, MallLayout.CoreDoorU0, MallLayout.CoreDoorU1, "STAIRS", DoorKind.Stairwell, "");
                CorridorDoorSign(parent, w, side, y, MallLayout.RoomBDoorU0, MallLayout.RoomBDoorU1, rb.label.ToUpperInvariant(),
                    rb.type == FacilityType.Toilets ? DoorKind.Toilet : DoorKind.Restricted, rb.lockId);
            }
            else
            {
                corridorDoors.Add((MallLayout.RoomADoorU0, MallLayout.RoomADoorU1, DoorHeight));
                MallLayout.QuadrantRoom ra = MallLayout.QuadrantRoomA(f, qx, qz);
                CorridorDoorSign(parent, w, side, y, MallLayout.RoomADoorU0, MallLayout.RoomADoorU1, ra.label.ToUpperInvariant(),
                    ra.type == FacilityType.Toilets ? DoorKind.Toilet : DoorKind.Restricted, ra.lockId);
            }
            corridorDoors.AddRange(VentOpenings(f, w, side));
            float end = MallLayout.AnchorEndFor(f, w);
            WingWallU("Corridor outer wall", parent, w, y, MallLayout.OuterWall, end, corridorV, 0.3f, corridorDoors, wallService, SurfaceType.Concrete);

            // Party walls between units (the one at the anchor end is the anchor's own wall).
            foreach (float u in boundaries)
            {
                if (u >= MallLayout.WingEnd - 0.01f) continue;
                float uc = u <= MallLayout.WingStart + 0.01f ? u + 0.1f : u;
                float v0 = side * MallLayout.ConcourseHalf, v1 = side * MallLayout.ShopBack;
                WingWallV("Party wall", parent, w, y, uc, Mathf.Min(v0, v1), Mathf.Max(v0, v1), 0.2f, null, wallPaint, SurfaceType.Concrete);
            }

            // Corridor lights.
            for (float u = 26f; u < MallLayout.WingEnd; u += 22f)
            {
                PointLight(parent, MallLayout.WingPoint(w, u, side * 17.5f, y + 4.1f), 9f, 0.7f, FluorescentWhite);
            }
        }

        static readonly string[] PosterText =
        {
            "CLOSING\nDOWN", "EVERYTHING\nMUST GO", "FINAL\nDAYS", "SALE\n70% OFF", "TO LET", "THANK YOU\nFOR 34 YEARS", "ALL STOCK\nREDUCED",
            "STAFF\nWANTED", "CLOSED\nUNTIL FURTHER\nNOTICE", "LAST\nWEEK", "NO CASH\nKEPT ON\nPREMISES", "SALE"
        };

        /// <summary>
        /// Paper posters taped to the inside of the shop window (or the shutter): closing-down notices, sales, "to let",
        /// some faded by years of light, some peeling at a corner, a few on fluorescent card.
        /// </summary>
        static void ShopPosters(Transform parent, MallUnit u, WingId w, int side, float y, Vector3 intoShop)
        {
            int h = Mathf.Abs(MallLayout.Hash((int)(u.u0 * 10f), (int)(u.u1 * 10f), side, 977));
            int count = h % 3;
            if (count == 0) return;
            float yaw = Mathf.Atan2(-intoShop.x, -intoShop.z) * Mathf.Rad2Deg;   // reads from the concourse
            for (int i = 0; i < count; i++)
            {
                int k = Mathf.Abs(MallLayout.Hash(h, i, 3, 31));
                float along = Mathf.Lerp(u.u0 + 0.9f, u.u1 - 0.9f, ((k % 1000) / 1000f));
                float py = y + 1.25f + (k / 1000 % 40) / 100f;
                Vector3 pos = MallLayout.WingPoint(w, along, side * (MallLayout.ConcourseHalf + 0.03f), py);
                bool fluoro = k % 5 == 0, faded = k % 3 == 0;
                Color paper = fluoro ? new Color(0.95f, 0.85f, 0.25f) : faded ? new Color(0.86f, 0.82f, 0.7f) : new Color(0.92f, 0.91f, 0.88f);
                Material pm = EditorBuild.Material(EditorBuild.Root + "/Materials/Posters", fluoro ? "Poster_Fluoro" : faded ? "Poster_Faded" : "Poster_White", paper, 0.1f);
                float roll = ((k / 7 % 9) - 4) * 1.5f;
                Quaternion rot = Quaternion.Euler(0f, yaw, roll);
                Vector3 size = new Vector3(0.6f, 0.82f, 0.003f);
                Detail("Poster", parent, pos, size, pm, rot);
                if (k % 4 == 0)
                {
                    // a corner peeling away from the glass
                    Vector3 corner = pos + rot * new Vector3(0.24f, 0.35f, 0f);
                    Detail("Poster corner (peeling)", parent, corner - intoShop * 0.02f, new Vector3(0.12f, 0.12f, 0.002f), pm, rot * Quaternion.Euler(-35f, 25f, 0f));
                }
                TextMesh t = EditorBuild.Label(parent, PosterText[k % PosterText.Length], pos - intoShop * 0.004f, yaw, 0.028f);
                t.transform.rotation = rot;
                t.color = k % 2 == 0 ? new Color(0.62f, 0.06f, 0.05f) : new Color(0.08f, 0.08f, 0.09f);
                if (faded) t.color = Color.Lerp(t.color, paper, 0.45f);
            }
        }

        static void CorridorDoorSign(Transform parent, WingId w, int side, float y, float u0, float u1, string label, DoorKind kind, string lockId)
        {
            float mid = (u0 + u1) * 0.5f;
            Vector3 view = MallLayout.WingRight(w) * side; // standing in the corridor, looking at the outer wall
            bool locked = !string.IsNullOrEmpty(lockId);
            DoorMarker(parent, label, MallLayout.WingPoint(w, mid, side * MallLayout.CorridorOuter, y), kind, u1 - u0, DoorHeight, locked, "Corridor door", lockId);
            Sign(parent, label, MallLayout.WingPoint(w, mid, side * (MallLayout.CorridorOuter - 0.03f), y + 2.45f), view, 1.6f, 0.03f);
        }

        static void BuildShop(Transform parent, MallUnit u, float y, int side, Vector3 toConcourse)
        {
            WingId w = u.wing;
            Vector3 intoShop = -toConcourse;
            float frontLineV = side * (MallLayout.ConcourseHalf + 0.06f);
            Vector3 a = MallLayout.WingPoint(w, u.u0 + 0.3f, frontLineV, y);
            Vector3 b = MallLayout.WingPoint(w, u.u1 - 0.3f, frontLineV, y);

            BuildClosure(parent, u.code, a, b, y, u.access, u.name);
            if (u.access == UnitAccess.LockedGlass && Mathf.Abs(MallLayout.Hash(u.code.GetHashCode(), 3, 9, 27)) % 3 == 0)
                GlassImpact(parent, u, a, b, y, toConcourse);

            // Fascia sign above the shopfront, readable from the concourse.
            float mid = (u.u0 + u.u1) * 0.5f;
            Vector3 signCentre = MallLayout.WingPoint(w, mid, side * (MallLayout.ConcourseHalf - 0.08f), y + 4.0f);
            FasciaSign(parent, u, signCentre, intoShop, (u.u1 - u.u0) * 0.75f, Mathf.Clamp((u.u1 - u.u0) * 0.006f, 0.035f, 0.06f));
            EditorBuild.Label(parent, u.code, MallLayout.WingPoint(w, u.u0 + 0.6f, side * (MallLayout.ConcourseHalf - 0.15f), y + 2.9f),
                Mathf.Atan2(intoShop.x, intoShop.z) * Mathf.Rad2Deg, 0.018f);
            ShopPosters(parent, u, w, side, y, intoShop);
            BladeSign(parent, u, y, side, toConcourse);
            OpeningHours(parent, u, y, side, toConcourse);

            if (!u.HasInterior) return;

            // Floor finish for the shop type (a thin layer on top of the slab).
            Material finish = ShopFloorFinish(u.category, out SurfaceType finishSurface);
            if (finish != null)
            {
                WingBox("Shop floor finish", parent, w, u.u0 + 0.1f, u.u1 - 0.1f, side * (MallLayout.ConcourseHalf + 0.25f), side * 13.5f, y, y + 0.012f, finish, finishSurface);
            }

            // Stock room partition with a doorway near the back door.
            float partitionV = side * 13.6f;
            WingWallU("Stock room wall", parent, w, y, u.u0 + 0.1f, u.u1 - 0.1f, partitionV, 0.15f,
                new List<(float, float, float)> { (u.u1 - 2.7f, u.u1 - 1.6f, DoorHeight) }, wallService, SurfaceType.Concrete);
            DoorMarker(parent, "Stock room", MallLayout.WingPoint(w, u.u1 - 2.15f, partitionV, y), DoorKind.Storage, 1.1f, DoorHeight, false, "");
            DoorMarker(parent, "Back door", MallLayout.WingPoint(w, u.u1 - 1.7f, side * MallLayout.ShopBack, y), DoorKind.ShopBack, 1.0f, DoorHeight,
                u.access == UnitAccess.LockedGlass, "Shop back door to service corridor");

            // Shop floor fittings, keeping the entrance and the stock room clear.
            AreaRect floorArea = MallLayout.WingRect(w, u.u0 + 0.2f, u.u1 - 0.2f, side * (MallLayout.ConcourseHalf + 0.2f), side * 13.5f);
            var keepClear = new List<AreaRect>
            {
                MallLayout.WingRect(w, u.u0, u.u1, side * MallLayout.ConcourseHalf, side * (MallLayout.ConcourseHalf + 2.2f)),
                MallLayout.WingRect(w, u.u1 - 3.2f, u.u1, side * 11.5f, side * 13.6f),
                MallLayout.WingRect(w, u.u0, u.u0 + 4.2f, side * 10.6f, side * 12.6f)
            };
            int seed = MallLayout.Hash((int)u.floor, (int)w, side, (int)(u.u0 * 10f));
            Furnish(parent, floorArea, y, u.style, keepClear, seed, categoryMats[u.category]);

            // Shelving along both side walls (products facing into the shop), for shops that sell things off shelves.
            if (u.style == FurnishStyle.Shelving || u.style == FurnishStyle.Rails || u.style == FurnishStyle.Tables ||
                u.style == FurnishStyle.Department || u.style == FurnishStyle.Supermarket)
            {
                FixtureMaterials();
                var kit = new MeshKit();
                Material[] pal = u.style == FurnishStyle.Supermarket ? groceryMats : u.style == FurnishStyle.Rails ? clothMats : productMats;
                float v0 = MallLayout.ConcourseHalf + 2.6f, v1 = 13.3f;
                Transform wg = Group("Wall shelving", parent);
                Vector3 fwdW = MallLayout.WingForward(w);
                WallShelving(kit, wg, MallLayout.WingPoint(w, u.u0 + 0.1f, side * (v0 + v1) * 0.5f, y), Quaternion.LookRotation(fwdW), v1 - v0, 2.2f, pal, seed + 101);
                WallShelving(kit, wg, MallLayout.WingPoint(w, u.u1 - 0.1f, side * (v0 + v1) * 0.5f, y), Quaternion.LookRotation(-fwdW), v1 - v0, 2.2f, pal, seed + 202);
                kit.Build(wg, "Wall shelving");
            }

            // Counter near the back.
            Vector3 c0 = MallLayout.WingPoint(w, u.u0 + 1f, side * 11.2f, y);
            Vector3 c1 = MallLayout.WingPoint(w, Mathf.Min(u.u0 + 3.5f, u.u1 - 3.4f), side * 12f, y + 1.05f);
            if (Mathf.Abs(Vector3.Dot(c1 - c0, MallLayout.WingForward(w))) > 0.8f) BoxMinMax("Counter", parent, c0, c1, counterMat, SurfaceType.Wood);

            // Stock room shelving.
            Vector3 s0 = MallLayout.WingPoint(w, u.u0 + 0.3f, side * 15.3f, y);
            Vector3 s1 = MallLayout.WingPoint(w, u.u1 - 3.4f, side * 15.8f, y + 2.0f);
            if (Mathf.Abs(Vector3.Dot(s1 - s0, MallLayout.WingForward(w))) > 1f) BoxMinMax("Stock shelving", parent, s0, s1, metal, SurfaceType.Metal);

            PointLight(parent, MallLayout.WingPoint(w, mid, side * 9.5f, y + 4.1f), 8f, u.Enterable ? 0.55f : 0.35f, WarmWhite);
        }

        static Material ShopFloorFinish(UnitCategory c, out SurfaceType surface)
        {
            switch (c)
            {
                case UnitCategory.Fashion:
                case UnitCategory.Footwear:
                case UnitCategory.Jewellery:
                    surface = SurfaceType.Carpet; return floorCarpet;
                case UnitCategory.Books:
                case UnitCategory.Music:
                case UnitCategory.Home:
                case UnitCategory.Cafe:
                    surface = SurfaceType.Wood; return woodMat;
                case UnitCategory.Electronics:
                case UnitCategory.Services:
                case UnitCategory.Health:
                    surface = SurfaceType.Tile; return vinylMat;
                default:
                    surface = SurfaceType.Tile; return null; // keeps the mall's porcelain tiles
            }
        }

        /// <summary>How a shopfront opening is closed tonight. a/b are the opening's ends on the front line.</summary>
        static void BuildClosure(Transform parent, string code, Vector3 a, Vector3 b, float y, UnitAccess access, string name)
        {
            Vector3 along = (b - a);
            along.y = 0f;
            bool alongX = Mathf.Abs(along.x) > Mathf.Abs(along.z);
            Vector3 lo = Vector3.Min(a, b), hi = Vector3.Max(a, b);
            Vector3 Thick(float t) => alongX ? new Vector3(0f, 0f, t) : new Vector3(t, 0f, 0f);

            switch (access)
            {
                case UnitAccess.Shuttered:
                {
                    GameObject s = BoxMinMax(code + " shutter (closed)", parent, new Vector3(lo.x, y, lo.z) - Thick(0.04f), new Vector3(hi.x, y + ShopOpeningHeight, hi.z) + Thick(0.04f), shutterMat, SurfaceType.Metal);
                    Slot(s, DoorKind.Shutter, along.magnitude, ShopOpeningHeight, true, name + ": closed shutter");
                    break;
                }
                case UnitAccess.ShutterJammed:
                {
                    GameObject s = BoxMinMax(code + " shutter (jammed 0.65 m up)", parent, new Vector3(lo.x, y + 0.65f, lo.z) - Thick(0.04f), new Vector3(hi.x, y + ShopOpeningHeight, hi.z) + Thick(0.04f), shutterMat, SurfaceType.Metal);
                    Slot(s, DoorKind.Shutter, along.magnitude, ShopOpeningHeight, true, name + ": shutter jammed part-way; crawl underneath");
                    break;
                }
                case UnitAccess.OpenShutter:
                {
                    Decor(code + " shutter (rolled up)", parent, new Vector3(lo.x, y + ShopOpeningHeight - 0.25f, lo.z) - Thick(0.12f), new Vector3(hi.x, y + ShopOpeningHeight, hi.z) + Thick(0.12f), shutterMat);
                    break;
                }
                case UnitAccess.LockedGlass:
                {
                    GameObject g = BoxMinMax(code + " glass front (locked)", parent, new Vector3(lo.x, y, lo.z) - Thick(0.02f), new Vector3(hi.x, y + ShopOpeningHeight, hi.z) + Thick(0.02f), glassMat, SurfaceType.Glass);
                    Slot(g, DoorKind.Glass, along.magnitude, ShopOpeningHeight, true, name + ": locked glass doors");
                    break;
                }
                case UnitAccess.BrokenLock:
                {
                    // Glass panels either side of a 1.8 m doorway whose lock has failed.
                    Vector3 dir = along.normalized;
                    float len = along.magnitude;
                    float doorHalf = 0.9f;
                    Vector3 m0 = a + dir * (len * 0.5f - doorHalf);
                    Vector3 m1 = a + dir * (len * 0.5f + doorHalf);
                    BoxMinMax(code + " glass panel", parent, new Vector3(Mathf.Min(a.x, m0.x), y, Mathf.Min(a.z, m0.z)) - Thick(0.02f), new Vector3(Mathf.Max(a.x, m0.x), y + ShopOpeningHeight, Mathf.Max(a.z, m0.z)) + Thick(0.02f), glassMat, SurfaceType.Glass);
                    BoxMinMax(code + " glass panel", parent, new Vector3(Mathf.Min(m1.x, b.x), y, Mathf.Min(m1.z, b.z)) - Thick(0.02f), new Vector3(Mathf.Max(m1.x, b.x), y + ShopOpeningHeight, Mathf.Max(m1.z, b.z)) + Thick(0.02f), glassMat, SurfaceType.Glass);
                    BoxMinMax(code + " glass transom", parent, new Vector3(Mathf.Min(m0.x, m1.x), y + 2.4f, Mathf.Min(m0.z, m1.z)) - Thick(0.02f), new Vector3(Mathf.Max(m0.x, m1.x), y + ShopOpeningHeight, Mathf.Max(m0.z, m1.z)) + Thick(0.02f), glassMat, SurfaceType.Glass);
                    DoorMarker(parent, name + " (broken lock)", (m0 + m1) * 0.5f, DoorKind.Glass, 1.8f, 2.4f, false, "Glass double door, lock broken");
                    break;
                }
            }
        }

        static void BuildConcourseFurniture(Transform parent, MallFloor f, WingId w, float y)
        {
            int i = 0;
            for (float u = 22f; u < MallLayout.WingEnd - 4f; u += 12f, i++)
            {
                PointLight(parent, MallLayout.WingPoint(w, u, 0f, y + 4.2f), 11f, 0.9f, FluorescentWhite);
                if (i % 3 == 0)
                {
                    WingBox("Planter", parent, w, u - 0.8f, u + 0.8f, -0.8f, 0.8f, y, y + 0.9f, concrete, SurfaceType.Concrete);
                }
                else if (i % 3 == 1)
                {
                    WingBox("Bench", parent, w, u - 1.0f, u + 1.0f, -2.3f, -1.8f, y, y + 0.45f, woodMat, SurfaceType.Wood);
                    WingBox("Bench", parent, w, u - 1.0f, u + 1.0f, 1.8f, 2.3f, y, y + 0.45f, woodMat, SurfaceType.Wood);
                }
                else
                {
                    WingBox("Bin", parent, w, u - 0.25f, u + 0.25f, -0.25f, 0.25f, y, y + 0.9f, metal, SurfaceType.Metal);
                }
            }
            // Hanging floor/wing sign at the atrium end.
            Vector3 view = MallLayout.WingForward(w);
            Sign(parent, MallLayout.FloorName(f) + "  -  " + w.ToString().ToUpperInvariant() + " MALL",
                MallLayout.WingPoint(w, 18f, 0f, y + 3.6f), view, 5f, 0.05f);
        }

        static readonly Color[] FasciaFaces =
        {
            new Color(0.92f, 0.9f, 0.86f), new Color(0.55f, 0.08f, 0.07f), new Color(0.08f, 0.16f, 0.4f),
            new Color(0.1f, 0.3f, 0.16f), new Color(0.12f, 0.12f, 0.13f), new Color(0.75f, 0.55f, 0.1f),
        };
        static readonly Color[] FasciaLetters =
        {
            new Color(0.08f, 0.08f, 0.09f), new Color(0.95f, 0.92f, 0.85f), new Color(0.95f, 0.92f, 0.85f),
            new Color(0.95f, 0.92f, 0.85f), new Color(0.85f, 0.7f, 0.35f), new Color(0.1f, 0.08f, 0.06f),
        };

        /// <summary>
        /// A shop's fascia: an aluminium-trimmed lightbox with an acrylic face in the brand colour, the name in
        /// letters across it. Lit on its timer, failing, half dead, or long dead (see SignLightbox).
        /// </summary>
        /// <summary>
        /// The projecting sign at the shop's corner: a bracket off the shopfront and a double-sided panel in the
        /// brand's colours, so you can read the shop from far down the concourse.
        /// </summary>
        static void BladeSign(Transform parent, MallUnit u, float y, int side, Vector3 toConcourse)
        {
            int h = MallLayout.Hash(u.code.GetHashCode(), 7, 31, 5);
            int brand = Mathf.Abs(h) % FasciaFaces.Length;
            WingId w = u.wing;
            Vector3 along = MallLayout.WingForward(w);
            Vector3 wall = MallLayout.WingPoint(w, u.u0 + 0.35f, side * (MallLayout.ConcourseHalf - 0.05f), y + 3.25f);
            Vector3 c = wall + toConcourse * 0.5f;
            Cyl("Blade sign bracket", parent, wall + Vector3.up * 0.33f, c + toConcourse * 0.32f + Vector3.up * 0.33f, 0.03f, metal);
            Quaternion face = Quaternion.LookRotation(along);
            Detail("Blade sign", parent, c, new Vector3(0.62f, 0.58f, 0.06f), ArtLibrary.Plain("SignFace_" + brand, FasciaFaces[brand] * 0.7f, 0.85f, 0f, FasciaFaces[brand], false), face);
            Detail("Blade sign rim", parent, c, new Vector3(0.66f, 0.62f, 0.04f), fixtureMat, face);
            string ini = u.name.Length > 0 ? u.name.Substring(0, 1).ToUpperInvariant() : "?";
            string shortName = u.name.Length > 10 ? u.name.Substring(0, 10) : u.name;
            foreach (float s in new[] { 1f, -1f })
            {
                float yaw = Mathf.Atan2(-along.x * s, -along.z * s) * Mathf.Rad2Deg;
                TextMesh big = EditorBuild.Label(parent, ini, c + along * (0.035f * s) + Vector3.up * 0.06f, yaw, 0.07f, false);
                big.color = FasciaLetters[brand];
                TextMesh small = EditorBuild.Label(parent, shortName.ToUpperInvariant(), c + along * (0.035f * s) - Vector3.up * 0.2f, yaw, 0.012f, false);
                small.color = FasciaLetters[brand];
            }
        }

        /// <summary>A small vinyl plaque by the entrance: the opening hours, which nobody will be keeping tonight.</summary>
        static void OpeningHours(Transform parent, MallUnit u, float y, int side, Vector3 toConcourse)
        {
            if (u.access == UnitAccess.BrokenLock) return;
            WingId w = u.wing;
            Vector3 p = MallLayout.WingPoint(w, u.u1 - 0.2f, side * (MallLayout.ConcourseHalf - 0.02f), y + 1.45f);
            Quaternion face = Quaternion.LookRotation(toConcourse);
            Detail("Opening hours plaque", parent, p, new Vector3(0.24f, 0.3f, 0.006f), darkPanel, face);
            int h = Mathf.Abs(MallLayout.Hash(u.code.GetHashCode(), 2, 4, 8));
            string[] times = { "MON - SAT  9:00 - 20:00\nSUN  10:00 - 17:00", "MON - SAT  9:30 - 19:30\nSUN  11:00 - 17:00", "OPEN 7 DAYS\n9:00 - 21:00" };
            float yaw = Mathf.Atan2(-toConcourse.x, -toConcourse.z) * Mathf.Rad2Deg;
            TextMesh t1 = EditorBuild.Label(parent, "OPENING HOURS", p + toConcourse * 0.006f + Vector3.up * 0.09f, yaw, 0.0075f, false);
            t1.color = new Color(0.9f, 0.9f, 0.88f);
            TextMesh t2 = EditorBuild.Label(parent, times[h % times.Length], p + toConcourse * 0.006f - Vector3.up * 0.03f, yaw, 0.006f, false);
            t2.color = new Color(0.85f, 0.85f, 0.82f);
        }

        static void FasciaSign(Transform parent, MallUnit u, Vector3 centre, Vector3 viewDir, float width, float textSize)
        {
            int h = MallLayout.Hash(u.code.GetHashCode(), 7, 31, 5);
            int brand = Mathf.Abs(h) % FasciaFaces.Length;
            float height = textSize * 4.4f;
            float yaw = Mathf.Atan2(viewDir.x, viewDir.z) * Mathf.Rad2Deg;
            Vector3 across = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

            // box: a slim metal frame proud of the wall, two acrylic halves on its face
            Vector3 half = across * (width * 0.5f + 0.05f) + Vector3.up * (height * 0.5f + 0.05f);
            Decor("Sign box", parent, centre - half, centre + half + viewDir * 0.13f, fixtureMat);
            Color faceCol = FasciaFaces[brand];
            Material face = ArtLibrary.Plain("SignFace_" + brand, faceCol * 0.7f, 0.85f, 0f, faceCol, false);
            var faces = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                Vector3 c0 = centre - viewDir * 0.006f + across * (width * (i == 0 ? -0.25f : 0.25f));
                Vector3 hh = across * (width * 0.25f - 0.004f) + Vector3.up * (height * 0.5f) + viewDir * 0.004f;
                faces[i] = Decor("Sign face", parent, c0 - hh, c0 + hh, face).GetComponent<Renderer>();
                faces[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            int roll = Mathf.Abs(h / 7) % 100;
            var state = roll < 55 ? SignLightbox.State.Lit : roll < 70 ? SignLightbox.State.Failing : roll < 82 ? SignLightbox.State.HalfDead : SignLightbox.State.Dead;
            // letters on a dead sign are just paint, lit by the room; on a working one they're backlit
            // the name: built-up letters (a few layers stepping back, darker) beside a round logo badge with the initial
            float badge = height * 0.85f;
            bool roomForBadge = width > 2.4f;
            Vector3 textCentre = centre + (roomForBadge ? across * (badge * 0.55f) : Vector3.zero);
            string label = u.name.ToUpperInvariant();
            Color letter = FasciaLetters[brand];
            for (int layer = 3; layer >= 1; layer--)
            {
                TextMesh depth = EditorBuild.Label(parent, label, textCentre - viewDir * (0.018f - 0.004f * layer), yaw, textSize, false);
                depth.color = Color.Lerp(letter, Color.black, 0.45f + 0.12f * layer);
            }
            TextMesh tm = EditorBuild.Label(parent, label, textCentre - viewDir * 0.019f, yaw, textSize, state == SignLightbox.State.Dead);
            tm.color = letter;
            if (roomForBadge)
            {
                Vector3 bc = centre - across * (width * 0.5f - badge * 0.6f) - viewDir * 0.012f;
                GameObject disc = Cyl("Logo badge", parent, bc + viewDir * 0.012f, bc - viewDir * 0.012f, badge, ArtLibrary.Plain("SignBadge_" + brand, letter, 0.6f, 0f));
                disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                TextMesh ini = EditorBuild.Label(parent, label.Substring(0, 1), bc - viewDir * 0.014f, yaw, textSize * 1.25f, false);
                ini.color = faceCol;
            }

            var box = faces[0].gameObject.AddComponent<SignLightbox>();
            box.faces = faces;
            box.emission = faceCol * 1.4f + new Color(0.15f, 0.15f, 0.15f);
            box.seed = h;
            box.state = state;
        }

        /// <summary>
        /// Something hit this window hard: a spider-web crack (seen from both sides), a second smaller one
        /// sometimes, and glass fragments on the concourse floor below.
        /// </summary>
        static void GlassImpact(Transform parent, MallUnit u, Vector3 a, Vector3 b, float y, Vector3 toConcourse)
        {
            Material crack = ArtLibrary.Decal("GlassCrack", "glass_crack", Color.white, 0.9f);
            Material glassBit = Mat("Glass fragment", new Color(0.75f, 0.85f, 0.85f), 0.95f);
            int h = Mathf.Abs(MallLayout.Hash(u.code.GetHashCode(), 5, 1, 77));
            var rng = new System.Random(h);
            float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            int hits = 1 + (h % 4 == 0 ? 1 : 0);
            for (int k = 0; k < hits; k++)
            {
                Vector3 p = Vector3.Lerp(a, b, R(0.15f, 0.85f)) + Vector3.up * (R(0.5f, 1.7f));
                float size = k == 0 ? R(0.7f, 1.2f) : R(0.3f, 0.5f);
                float spin = R(0f, 360f);
                foreach (int sd in new[] { -1, 1 })
                    DecalQuad(parent, p + toConcourse * (0.021f * sd), toConcourse * sd, size, size, spin, crack);
                // fragments that fell on the concourse side
                int bits = 6 + rng.Next(10);
                Vector3 floor = new Vector3(p.x, y, p.z);
                // an invisible skin of glass over that patch, so footsteps there crunch on glass
                {
                    Vector3 al = (b - a).normalized;
                    Vector3 c0 = floor + toConcourse * 0.5f;
                    Vector3 ext = new Vector3(Mathf.Abs(al.x) * 0.5f + Mathf.Abs(toConcourse.x) * 0.5f, 0f, Mathf.Abs(al.z) * 0.5f + Mathf.Abs(toConcourse.z) * 0.5f);
                    GameObject skin = BoxMinMax("Broken glass (floor)", parent, c0 - ext, c0 + ext + Vector3.up * 0.004f, glassBit, SurfaceType.Glass);
                    Object.DestroyImmediate(skin.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(skin.GetComponent<MeshFilter>());
                }
                for (int i = 0; i < bits; i++)
                {
                    GameObject f = Decor("Glass fragment", parent, Vector3.zero, new Vector3(R(0.01f, 0.05f), 0.003f, R(0.01f, 0.04f)), glassBit);
                    Vector3 along = (b - a).normalized;
                    f.transform.localPosition = floor + toConcourse * R(0.05f, 0.9f) + along * R(-0.4f, 0.4f) + Vector3.up * 0.0035f;
                    f.transform.localRotation = Quaternion.Euler(R(-6f, 6f), R(0f, 360f), R(-6f, 6f));
                }
            }
        }
    }
}
