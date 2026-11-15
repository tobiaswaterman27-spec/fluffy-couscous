using System.Collections.Generic;
using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    public static partial class MallBuilder
    {
        static readonly Color CarParkLight = new Color(0.85f, 0.95f, 0.88f);

        static void BuildCarPark(Transform parent)
        {
            float y = MallLayout.CarParkY;
            const float H = MallLayout.CarParkHalf;
            const float ceilingBottom = -0.5f;
            AreaRect raised = MallLayout.CarParkRaised;
            const float raisedY = -2.5f;

            // ---- Floor, raised section, ramps ----
            BoxMinMax("Car park floor", parent, new Vector3(-H, y - 0.4f, -H), new Vector3(H, y, H), carParkFloor, SurfaceType.Concrete);
            BoxRect("Raised section", parent, raised, y, raisedY, carParkFloor, SurfaceType.Concrete);
            EditorBuild.Ramp("Ramp (split level)", parent, new Vector3(-66f, raisedY, raised.minZ), new Vector3(-66f, y, raised.minZ - 12f), 8f, 0.4f, carParkFloor, SurfaceType.Concrete);
            EditorBuild.Ramp("Ramp (split level)", parent, new Vector3(raised.maxX, raisedY, 64f), new Vector3(raised.maxX + 12f, y, 64f), 8f, 0.4f, carParkFloor, SurfaceType.Concrete);
            // Crash barriers along the raised edge (gaps at the ramps).
            WallSpan("Edge barrier", parent, new Vector3(-H, 0f, raised.minZ + 0.15f), new Vector3(raised.maxX, 0f, raised.minZ + 0.15f), 0.25f, raisedY, raisedY + 1f,
                new List<Opening> { new Opening(-70f + H, -62f + H, 2f) }, concrete, SurfaceType.Concrete);
            WallSpan("Edge barrier", parent, new Vector3(raised.maxX - 0.15f, 0f, raised.minZ), new Vector3(raised.maxX - 0.15f, 0f, H), 0.25f, raisedY, raisedY + 1f,
                new List<Opening> { new Opening(60f - raised.minZ, 68f - raised.minZ, 2f) }, concrete, SurfaceType.Concrete);

            // ---- Ceiling (underside of the ground floor) ----
            var holes = new List<AreaRect>();
            foreach (var (sx, sz) in MallLayout.Quadrants) holes.Add(MallLayout.CoreRect(sx, sz));
            foreach (AreaRect lift in MallLayout.LiftShafts()) holes.Add(lift);
            holes.Add(MallLayout.CarParkExitRamp);
            Slab("Car park ceiling", parent, new AreaRect(-H, -H, H, H), holes, -0.4f, 0.1f, concrete, SurfaceType.Concrete);

            // ---- Perimeter walls ----
            WallSpan("Car park wall N", parent, new Vector3(-H, 0f, H + 0.15f), new Vector3(H, 0f, H + 0.15f), 0.3f, y, -0.4f, null, concrete, SurfaceType.Concrete);
            WallSpan("Car park wall S", parent, new Vector3(-H, 0f, -H - 0.15f), new Vector3(H, 0f, -H - 0.15f), 0.3f, y, -0.4f, null, concrete, SurfaceType.Concrete);
            WallSpan("Car park wall E", parent, new Vector3(H + 0.15f, 0f, -H), new Vector3(H + 0.15f, 0f, H), 0.3f, y, -0.4f,
                new List<Opening> { new Opening(-36.2f + H, -27.8f + H, 10f) }, concrete, SurfaceType.Concrete);
            WallSpan("Car park wall W", parent, new Vector3(-H - 0.15f, 0f, -H), new Vector3(-H - 0.15f, 0f, H), 0.3f, y, -0.4f,
                new List<Opening> { new Opening(-20f + H, -17f + H, 3f) }, concrete, SurfaceType.Concrete);

            // ---- Lift lobby (below the atrium lifts) ----
            AreaRect lobby = new AreaRect(-17f, 9f, -9f, 16.2f);
            WallSpan("Lift lobby wall", parent, new Vector3(lobby.minX, 0f, lobby.minZ), new Vector3(lobby.maxX, 0f, lobby.minZ), 0.2f, y, ceilingBottom,
                new List<Opening> { new Opening(3f, 5f, 2.2f) }, wallPaint, SurfaceType.Concrete);
            WallSpan("Lift lobby wall", parent, new Vector3(lobby.minX, 0f, lobby.minZ), new Vector3(lobby.minX, 0f, lobby.maxZ), 0.2f, y, ceilingBottom, null, wallPaint, SurfaceType.Concrete);
            WallSpan("Lift lobby wall", parent, new Vector3(lobby.maxX, 0f, lobby.minZ), new Vector3(lobby.maxX, 0f, lobby.maxZ), 0.2f, y, ceilingBottom, null, wallPaint, SurfaceType.Concrete);
            WallSpan("Lift lobby wall", parent, new Vector3(lobby.minX, 0f, lobby.maxZ), new Vector3(lobby.maxX, 0f, lobby.maxZ), 0.2f, y, ceilingBottom, null, wallPaint, SurfaceType.Concrete);
            DoorMarker(parent, "Lift lobby", new Vector3(-13f, y, lobby.minZ), DoorKind.Glass, 2f, 2.2f, false, "Glass doors into the car park lift lobby");
            Sign(parent, "LIFTS TO MALL", new Vector3(-13f, y + 2.6f, lobby.minZ - 0.12f), Vector3.forward, 3f, 0.05f);
            PointLight(parent, new Vector3(-13f, -1f, 11f), 7f, 0.6f, FluorescentWhite, false);

            // ---- Maintenance and plant rooms on the west side ----
            WallSpan("Plant room wall", parent, new Vector3(-86f, 0f, -50f), new Vector3(-86f, 0f, -25f), 0.25f, y, ceilingBottom,
                new List<Opening> { new Opening(5f, 6.2f, DoorHeight), new Opening(18f, 19.2f, DoorHeight) }, wallService, SurfaceType.Concrete);
            WallSpan("Plant room wall", parent, new Vector3(-H, 0f, -50f), new Vector3(-86f, 0f, -50f), 0.25f, y, ceilingBottom, null, wallService, SurfaceType.Concrete);
            WallSpan("Plant room wall", parent, new Vector3(-H, 0f, -25f), new Vector3(-86f, 0f, -25f), 0.25f, y, ceilingBottom, null, wallService, SurfaceType.Concrete);
            WallSpan("Plant room wall", parent, new Vector3(-H, 0f, -37.75f), new Vector3(-86f, 0f, -37.75f), 0.2f, y, ceilingBottom, null, wallService, SurfaceType.Concrete);
            DoorMarker(parent, "Maintenance", new Vector3(-86f, y, -44.4f), DoorKind.Maintenance, 1.2f, DoorHeight, true, "Car park maintenance store");
            DoorMarker(parent, "Plant room", new Vector3(-86f, y, -31.4f), DoorKind.Maintenance, 1.2f, DoorHeight, true, "Car park plant room");
            Furnish(parent, new AreaRect(-99.5f, -49.5f, -86.5f, -38.2f), y, FurnishStyle.Storage,
                new List<AreaRect> { new AreaRect(-89f, -46f, -86f, -43f) }, 17, propMat);
            Furnish(parent, new AreaRect(-99.5f, -37.3f, -86.5f, -25.5f), y, FurnishStyle.Plant,
                new List<AreaRect> { new AreaRect(-89f, -33f, -86f, -30f) }, 18, plantMat);
            PointLight(parent, new Vector3(-93f, -1f, -44f), 9f, 0.4f, FluorescentWhite, false);
            PointLight(parent, new Vector3(-93f, -1f, -31f), 9f, 0.4f, FluorescentWhite, false);

            // ---- Service tunnel (escape route 6) ----
            const float tz0 = -20f, tz1 = -17f, tunnelEnd = -180f;
            BoxMinMax("Service tunnel floor", parent, new Vector3(tunnelEnd, y - 0.4f, tz0), new Vector3(-H, y, tz1), concrete, SurfaceType.Concrete);
            BoxMinMax("Service tunnel ceiling", parent, new Vector3(tunnelEnd, -1f, tz0 - 0.3f), new Vector3(-H, -0.8f, tz1 + 0.3f), concrete, SurfaceType.Concrete);
            BoxMinMax("Service tunnel wall", parent, new Vector3(tunnelEnd, y, tz0 - 0.3f), new Vector3(-H - 0.3f, -1f, tz0), concrete, SurfaceType.Concrete);
            BoxMinMax("Service tunnel wall", parent, new Vector3(tunnelEnd, y, tz1), new Vector3(-H - 0.3f, -1f, tz1 + 0.3f), concrete, SurfaceType.Concrete);
            WallSpan("Service tunnel end", parent, new Vector3(tunnelEnd - 0.15f, 0f, tz0), new Vector3(tunnelEnd - 0.15f, 0f, tz1), 0.3f, y, -1f,
                new List<Opening> { new Opening(0.9f, 2.1f, DoorHeight) }, concrete, SurfaceType.Concrete);
            DoorMarker(parent, "Service tunnel end door", new Vector3(tunnelEnd, y, -18.5f), DoorKind.Maintenance, 1.2f, DoorHeight, true,
                "Pressure door at the end of the service tunnel (escape route 6): opened by its valve wheel", "tunnel_door");
            for (float x = -110f; x > tunnelEnd; x -= 16f) PointLight(parent, new Vector3(x, -1.3f, -18.5f), 8f, 0.35f, WarmWhite, false);
            DoorMarker(parent, "Service tunnel gate", new Vector3(-H, y, -18.5f), DoorKind.Maintenance, 3f, 3f, true, "Gate from the car park into the service tunnel");

            // ---- Exit ramp to street level (escape route 2) ----
            AreaRect ramp = MallLayout.CarParkExitRamp;
            float rz = (ramp.minZ + ramp.maxZ) * 0.5f;
            EditorBuild.Ramp("Exit ramp", parent, new Vector3(ramp.minX, y, rz), new Vector3(ramp.maxX, 0f, rz), 8f, 0.4f, carParkFloor, SurfaceType.Concrete);
            BoxMinMax("Exit ramp wall", parent, new Vector3(ramp.minX, y, ramp.minZ), new Vector3(ramp.maxX, 3f, ramp.minZ + 0.2f), concrete, SurfaceType.Concrete);
            BoxMinMax("Exit ramp wall", parent, new Vector3(ramp.minX, y, ramp.maxZ - 0.2f), new Vector3(ramp.maxX, 3f, ramp.maxZ), concrete, SurfaceType.Concrete);
            BoxMinMax("Exit ramp roof", parent, new Vector3(ramp.minX, 3f, ramp.minZ), new Vector3(ramp.maxX + 0.3f, 3.3f, ramp.maxZ), concrete, SurfaceType.Concrete);
            BoxMinMax("Exit ramp end wall", parent, new Vector3(ramp.maxX, y, ramp.minZ), new Vector3(ramp.maxX + 0.3f, 0f, ramp.maxZ), concrete, SurfaceType.Concrete);
            GameObject shutter = BoxMinMax("Exit shutter", parent, new Vector3(ramp.maxX, 0f, ramp.minZ + 0.2f), new Vector3(ramp.maxX + 0.2f, 3f, ramp.maxZ - 0.2f), shutterMat, SurfaceType.Metal);
            Slot(shutter, DoorKind.Shutter, ramp.Depth, 3f, true, "Car park exit shutter (escape route 2)");
            float barrierX = ramp.maxX - 6f;
            float surfaceY = y + (barrierX - ramp.minX) / ramp.Width * (0f - y);
            BoxMinMax("Barrier arm", parent, new Vector3(barrierX - 0.05f, surfaceY + 0.95f, ramp.minZ + 0.6f), new Vector3(barrierX + 0.05f, surfaceY + 1.05f, ramp.maxZ - 1f), lineMat, SurfaceType.Metal);
            BoxMinMax("Barrier post", parent, new Vector3(barrierX - 0.2f, surfaceY - 0.3f, ramp.maxZ - 1f), new Vector3(barrierX + 0.2f, surfaceY + 1.2f, ramp.maxZ - 0.6f), metal, SurfaceType.Metal);
            Sign(parent, "EXIT", new Vector3(ramp.minX + 2f, -1.2f, ramp.maxZ - 0.25f), Vector3.back, 2f, 0.06f, false);
            for (float x = ramp.minX + 6f; x < ramp.maxX; x += 12f) PointLight(parent, new Vector3(x, 2.4f, rz), 10f, 0.5f, CarParkLight, false);

            // ---- Delivery bay (fenced) ----
            AreaRect bay = new AreaRect(70f, 70f, H, H);
            WallSpan("Delivery bay fence", parent, new Vector3(bay.minX, 0f, bay.minZ), new Vector3(bay.minX, 0f, bay.maxZ), 0.1f, y, y + 2.4f,
                new List<Opening> { new Opening(10f, 16f, 2.4f) }, metal, SurfaceType.Metal);
            WallSpan("Delivery bay fence", parent, new Vector3(bay.minX, 0f, bay.minZ), new Vector3(bay.maxX, 0f, bay.minZ), 0.1f, y, y + 2.4f, null, metal, SurfaceType.Metal);
            DoorMarker(parent, "Delivery bay gate", new Vector3(bay.minX, y, 83f), DoorKind.Shutter, 6f, 2.4f, false, "Sliding fence gate");
            foreach (var (z0, z1) in new[] { (78f, 84f), (88f, 94f) })
            {
                GameObject s = BoxMinMax("Delivery shutter", parent, new Vector3(H - 0.1f, y, z0), new Vector3(H, y + 3.2f, z1), shutterMat, SurfaceType.Metal);
                Slot(s, DoorKind.Shutter, z1 - z0, 3.2f, true, "Delivery roller shutter");
            }
            Furnish(parent, new AreaRect(72f, 72f, 98f, 98f), y, FurnishStyle.Storage, new List<AreaRect> { new AreaRect(70f, 78f, 76f, 92f) }, 23, propMat);

            // ---- Pillars (8 m grid) ----
            var avoid = new List<AreaRect>(holes);
            avoid.Add(lobby);
            avoid.Add(new AreaRect(-H, -52f, -84f, -23f));
            avoid.Add(new AreaRect(55f, -38f, H, -26f));
            avoid.Add(new AreaRect(-71f, 26f, -61f, raised.minZ));
            avoid.Add(new AreaRect(raised.maxX, 59f, raised.maxX + 14f, 69f));
            avoid.Add(new AreaRect(68f, 68f, H, H));
            Transform pillars = Group("Pillars", parent);
            for (float x = -96f; x <= 96.1f; x += 8f)
            {
                for (float z = -96f; z <= 96.1f; z += 8f)
                {
                    var r = new AreaRect(x - 0.3f, z - 0.3f, x + 0.3f, z + 0.3f);
                    bool skip = false;
                    foreach (AreaRect a in avoid) if (r.Expanded(1.2f).Overlaps(a)) { skip = true; break; }
                    // Not on the raised section's edge line.
                    if (Mathf.Abs(z - raised.minZ) < 1.5f && x < raised.maxX + 1f) skip = true;
                    if (Mathf.Abs(x - raised.maxX) < 1.5f && z > raised.minZ - 1f) skip = true;
                    if (skip) continue;
                    float bottom = raised.Contains(x, z) ? raisedY : y;
                    BoxMinMax("Pillar", pillars, new Vector3(r.minX, bottom, r.minZ), new Vector3(r.maxX, ceilingBottom, r.maxZ), concrete, SurfaceType.Concrete);
                    // Painted bay number (for orientation; the car park has no cars).
                    if (((int)(x + 96f) / 8 + (int)(z + 96f) / 8) % 3 == 0)
                    {
                        string label = $"{(char)('A' + (int)((x + 96f) / 8f))}{(int)((z + 96f) / 8f) + 1}";
                        EditorBuild.Label(pillars, label, new Vector3(x, bottom + 1.8f, z - 0.32f), 0f, 0.04f);
                    }
                }
            }

            // Painted lines on the main aisles.
            for (float x = -88f; x <= 88f; x += 32f)
            {
                Decor("Aisle line", parent, new Vector3(x - 0.06f, y + 0.005f, -H + 2f), new Vector3(x + 0.06f, y + 0.012f, H - 2f), lineMat);
            }

            // ---- Lights (some areas left dark on purpose) ----
            Transform lights = Group("Car park lights", parent);
            for (float x = -90f; x <= 90.1f; x += 22.5f)
            {
                for (float z = -90f; z <= 90.1f; z += 22.5f)
                {
                    int h = MallLayout.Hash((int)x, (int)z, 5, 5);
                    if (h % 5 == 0) continue; // dead fitting: near-dark area
                    if (h % 7 == 3) continue; // faulty fitting: the detail pass adds a flickering light here
                    PointLight(lights, new Vector3(x, -0.9f, z), 15f, h % 7 == 0 ? 0.35f : 1.0f, CarParkLight);
                }
            }
        }
    }
}
