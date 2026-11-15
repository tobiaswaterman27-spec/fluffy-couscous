using System.Collections.Generic;
using UnityEngine;
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
                    bool backOfHouse = Mathf.Min(Mathf.Abs(region.minX), Mathf.Abs(region.maxX)) >= MallLayout.OuterWall - 0.01f
                                       && Mathf.Min(Mathf.Abs(region.minZ), Mathf.Abs(region.maxZ)) >= MallLayout.OuterWall - 0.01f;
                    Slab("Floor", g, region, holes, y, MallLayout.SlabThickness, backOfHouse ? concrete : floorTile,
                        backOfHouse ? SurfaceType.Concrete : SurfaceType.Tile);
                    Slab("Ceiling", g, region, ceilingHoles, y + MallLayout.CeilingHeight + 0.05f, 0.05f, ceilingMat, SurfaceType.Concrete);
                }
            }
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

            // Seating and planters.
            BoxMinMax("Bench", parent, new Vector3(-13f, 0f, -5f), new Vector3(-12.5f, 0.45f, -1f), woodMat, SurfaceType.Wood);
            BoxMinMax("Bench", parent, new Vector3(-13f, 0f, 1f), new Vector3(-12.5f, 0.45f, 5f), woodMat, SurfaceType.Wood);
            BoxMinMax("Bench", parent, new Vector3(12.5f, 0f, -8f), new Vector3(13f, 0.45f, -4f), woodMat, SurfaceType.Wood);
            BoxMinMax("Bench", parent, new Vector3(2f, 0f, -13f), new Vector3(6f, 0.45f, -12.5f), woodMat, SurfaceType.Wood);
            BoxMinMax("Planter", parent, new Vector3(12.2f, 0f, -14.8f), new Vector3(14.8f, 0.9f, -12.2f), concrete, SurfaceType.Concrete);
            BoxMinMax("Planter", parent, new Vector3(-14.8f, 0f, -14.8f), new Vector3(-12.2f, 0.9f, -12.2f), concrete, SurfaceType.Concrete);
            BoxMinMax("Planter", parent, new Vector3(12.2f, 0f, 12.2f), new Vector3(14.8f, 0.9f, 14.8f), concrete, SurfaceType.Concrete);

            // Hanging clock above the information desk (Part 13 makes it tell the in-game time).
            Transform clock = Group("Clock", parent);
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            face.name = "Clock face";
            face.transform.SetParent(clock, false);
            face.transform.localPosition = new Vector3(0f, 8f, -8f);
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            face.transform.localScale = new Vector3(1.4f, 0.06f, 1.4f);
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.GetComponent<Renderer>().sharedMaterial = screenMat;
            EditorBuild.Label(clock, "11:30", new Vector3(0f, 8f, -8.08f), 0f, 0.05f);
            EditorBuild.Label(clock, "11:30", new Vector3(0f, 8f, -7.92f), 180f, 0.05f);
            Decor("Clock rod", clock, new Vector3(-0.03f, 8.7f, -8.03f), new Vector3(0.03f, MallLayout.RoofY - 0.4f, -7.97f), metal);

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
        // Lift shafts (lift cars and working doors: Part 12)
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
            float doorW = Mathf.Min(1.1f, width - 0.4f);
            for (int i = 0; i < LiftLevels.Length; i++)
            {
                float y0 = MallLayout.FloorY(LiftLevels[i]);
                float y1 = i + 1 < LiftLevels.Length ? MallLayout.FloorY(LiftLevels[i + 1]) : top;
                Vector3 p0 = new Vector3(r.minX + t, 0f, r.minZ + t * 0.5f);
                Vector3 p1 = new Vector3(r.maxX - t, 0f, r.minZ + t * 0.5f);
                float s0 = (width - doorW) * 0.5f;
                WallSpan("Shaft front", parent, p0, p1, t, y0, y1, new List<Opening> { new Opening(s0, s0 + doorW, DoorHeight) }, wallService, SurfaceType.Concrete);

                float cx = (r.minX + r.maxX) * 0.5f;
                GameObject door = BoxMinMax($"{name} doors ({MallLayout.FloorCode(LiftLevels[i])})", parent,
                    new Vector3(cx - doorW * 0.5f, y0, r.minZ + 0.02f), new Vector3(cx + doorW * 0.5f, y0 + DoorHeight, r.minZ + 0.08f), liftDoorMat, SurfaceType.Metal);
                Slot(door, DoorKind.Lift, doorW, DoorHeight, true, name + " landing doors (no power yet)");
                EditorBuild.Label(parent, MallLayout.FloorCode(LiftLevels[i]), new Vector3(cx, y0 + 2.4f, r.minZ - 0.02f), 0f, 0.03f);
            }
        }

        // =====================================================================
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
            BoxMinMax("Atrium skylight", parent, new Vector3(-MallLayout.VoidHalf, y - 0.15f, -MallLayout.VoidHalf),
                new Vector3(MallLayout.VoidHalf, y - 0.05f, MallLayout.VoidHalf), darkGlass, SurfaceType.Glass);

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
