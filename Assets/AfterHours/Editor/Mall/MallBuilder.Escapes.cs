using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AfterHours.Core;
using AfterHours.Interaction;
using AfterHours.Puzzles;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Part 7: the eight escape routes. Each has a physical clue (an empty holder, a card reader, a chained door...),
    /// its own requirements, and a final sequence. Progression chain: see docs/PROGRESSION.md.
    /// </summary>
    public static partial class MallBuilder
    {
        static Material ledRed, ledGreen;

        static void BuildEscapeRoutes(Transform parent)
        {
            Physics.SyncTransforms();
            ledRed = ArtLibrary.Plain("LED_Red", new Color(0.6f, 0.05f, 0.05f), 0.6f, 0f, new Color(1.5f, 0.1f, 0.1f), false);
            ledGreen = ArtLibrary.Plain("LED_Green", new Color(0.05f, 0.6f, 0.1f), 0.6f, 0f, new Color(0.1f, 1.6f, 0.2f), false);

            RouteMainEntrance(Group("Route 1 - Main entrance", parent));
            RouteCarParkExit(Group("Route 2 - Car park exit", parent));
            RouteLoadingBay(Group("Route 3 - Loading bay", parent));
            RouteEmergencyExit(Group("Route 4 - Emergency exit", parent));
            RouteRooftop(Group("Route 5 - Rooftop", parent));
            RouteServiceTunnel(Group("Route 6 - Service tunnel", parent));
            RouteStaffExit(Group("Route 7 - Restricted staff exit", parent));
            RouteHidden(Group("Route 8 - Hidden", parent));
        }

        // ------------------------------------------------------------------ building blocks

        static PuzzleGate Gate(Transform g, string name, params MonoBehaviour[] inputs)
        {
            var go = new GameObject("Gate: " + name);
            go.transform.SetParent(g, false);
            PuzzleGate gate = go.AddComponent<PuzzleGate>();
            gate.routeName = name;
            gate.inputs.AddRange(inputs);
            return gate;
        }

        static T Output<T>(PuzzleGate gate) where T : PuzzleOutput
        {
            T o = gate.gameObject.AddComponent<T>();
            gate.outputs.Add(o);
            return o;
        }

        static EscapeTrigger Exit(Transform g, EscapeRouteId route, Vector3 centre, Vector3 size, PuzzleGate gate, bool withFloor = true)
        {
            var go = new GameObject("Way out: " + route);
            go.transform.SetParent(g, false);
            go.transform.position = centre;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;
            EditorBuild.SetLayerRecursive(go, GameLayers.TriggerZoneLayer);
            EscapeTrigger t = go.AddComponent<EscapeTrigger>();
            t.route = route;
            if (gate != null) Output<EnableEscapeOutput>(gate).trigger = t;
            if (withFloor)
            {
                // A little ground beyond the exit so nobody steps into the void before the ending starts.
                BoxMinMax("Threshold", g, centre - new Vector3(size.x * 0.5f + 0.5f, size.y * 0.5f + 0.4f, size.z * 0.5f + 0.5f),
                    centre + new Vector3(size.x * 0.5f + 0.5f, -size.y * 0.5f, size.z * 0.5f + 0.5f), concrete, SurfaceType.Concrete);
            }
            return t;
        }

        static ItemSocket Socket(Transform g, string label, string accepts, Vector3 pos, Vector3 facing, float turnSeconds = 0f)
        {
            var go = new GameObject("Socket: " + label);
            go.transform.SetParent(g, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(facing);
            // The visible empty holder is the clue.
            Detail("Holder plate", g, pos - facing * 0.03f, new Vector3(0.3f, 0.3f, 0.06f), plantMat, go.transform.rotation);
            Detail("Empty holder", g, pos + facing * 0.005f, new Vector3(0.14f, 0.1f, 0.02f), blackMat, go.transform.rotation);
            BoxCollider c = go.AddComponent<BoxCollider>();
            c.size = new Vector3(0.35f, 0.35f, 0.15f);
            EditorBuild.SetLayerRecursive(go, GameLayers.InteractableLayer);
            var mount = new GameObject("Mount");
            mount.transform.SetParent(go.transform, false);
            mount.transform.localPosition = new Vector3(0f, 0f, 0.04f);
            ItemSocket s = go.AddComponent<ItemSocket>();
            s.accepts = accepts;
            s.label = label;
            s.mountPoint = mount.transform;
            s.turnSeconds = turnSeconds;
            return s;
        }

        static CardReader Reader(Transform g, string accepts, Vector3 pos, Vector3 facing, string verb = "Swipe card", string idle = "Card reader")
        {
            var go = new GameObject("Card reader (" + accepts + ")");
            go.transform.SetParent(g, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(facing);
            GameObject body = EditorBuild.Box("Reader body", g, pos, new Vector3(0.1f, 0.16f, 0.04f), blackMat, SurfaceType.Metal, go.transform.rotation, false);
            body.transform.SetParent(go.transform, true);
            EditorBuild.SetLayerRecursive(body, GameLayers.InteractableLayer);
            GameObject led = Detail("Reader light", g, pos + facing * 0.021f + Vector3.up * 0.05f, new Vector3(0.025f, 0.012f, 0.004f), ledRed, go.transform.rotation);
            GameObjectUtility.SetStaticEditorFlags(led, 0); // its colour changes, so it must stay a separate object
            CardReader r = go.AddComponent<CardReader>();
            r.accepts = accepts;
            r.indicator = led.GetComponent<Renderer>();
            r.redMat = ledRed;
            r.greenMat = ledGreen;
            r.verb = verb;
            r.idleName = idle;
            return r;
        }

        static Door NearestBuiltDoor(Vector3 near, float maxDistance = 3f)
        {
            Door best = null;
            float bestD = maxDistance;
            foreach (DoorSlot slot in Object.FindObjectsByType<DoorSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (slot.built == null) continue;
                float d = Vector3.Distance(slot.transform.position, near);
                if (d < bestD) { bestD = d; best = slot.built; }
            }
            if (best == null) Debug.LogWarning("[AfterHours] No door found near " + near);
            return best;
        }

        static RollerShutter NearestShutter(Vector3 near, float maxDistance = 4f)
        {
            RollerShutter best = null;
            float bestD = maxDistance;
            foreach (RollerShutter r in Object.FindObjectsByType<RollerShutter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(r.GetComponent<Collider>().bounds.center, near);
                if (d < bestD) { bestD = d; best = r; }
            }
            if (best == null) Debug.LogWarning("[AfterHours] No shutter found near " + near);
            return best;
        }

        static GameObject Blocker(Transform g, string name, Vector3 centre, Vector3 size, Material m)
        {
            GameObject b = EditorBuild.Box(name, g, centre, size, m, SurfaceType.Metal, null, false);
            EditorBuild.SetLayerRecursive(b, GameLayers.PropLayer);
            return b;
        }

        // ------------------------------------------------------------------ 1. Main entrance
        // Door release in the security office (security card + release switch) AND the main entrance key in the
        // door lock. The automatic doors then grind open.
        static void RouteMainEntrance(Transform g)
        {
            Vector3 panel = new Vector3(-35f, 1.3f, 31.88f);
            CardReader reader = Reader(g, "card_security", panel, Vector3.back);
            PhysicalSwitch release = InteractiveFactory.Switch(g, panel + Vector3.left * 0.5f + Vector3.forward * 0.02f, Vector3.back, "door release", plantMat, redMat);
            Sign(g, "MAIN DOORS", panel + Vector3.up * 0.45f + Vector3.back * 0.02f, Vector3.forward, 0.9f, 0.025f);

            float end = MallLayout.EntranceHallEnd;
            ItemSocket keyLock = Socket(g, "key", "key_main_doors", new Vector3(1.6f, 1.05f, -end + 0.35f), Vector3.forward);

            PuzzleGate gate = Gate(g, "Main entrance", reader, release, keyLock);
            SlidePanelsOutput slide = Output<SlidePanelsOutput>(gate);
            slide.panels = MainEntrancePanels.ToArray();
            slide.offsets = new Vector3[slide.panels.Length];
            for (int i = 0; i < slide.panels.Length; i++)
            {
                float x = slide.panels[i].position.x;
                slide.offsets[i] = new Vector3(x < 0f ? -3.05f : 3.05f, 0f, 0f);
            }
            slide.seconds = 4f;
            Output<MachineStartOutput>(gate).noiseRadius = 30f;
            Exit(g, EscapeRouteId.MainEntrance, new Vector3(0f, 1.2f, -end - 0.7f), new Vector3(11f, 2.4f, 0.8f), gate);
        }

        // ------------------------------------------------------------------ 2. Car park exit
        // A fuse in the car park plant room's fuse box (power) + the control switch fitted into the ramp control
        // panel + the motor switch: the exit shutter grinds all the way up (very loud, takes a while).
        static void RouteCarParkExit(Transform g)
        {
            ItemSocket fuse = Socket(g, "fuse", "fuse", new Vector3(-99.8f, -2.6f, -29.5f), Vector3.right);
            Sign(g, "SHUTTER SUPPLY", new Vector3(-99.78f, -2.15f, -29.5f), Vector3.left, 0.9f, 0.022f);

            AreaRect ramp = MallLayout.CarParkExitRamp;
            float px = ramp.maxX - 7f;
            float surface = MallLayout.CarParkY + (px - ramp.minX) / ramp.Width * (0f - MallLayout.CarParkY);
            Vector3 panel = new Vector3(px, surface + 1.25f, ramp.maxZ - 0.25f);
            Detail("Control cabinet", g, panel + Vector3.forward * 0.12f, new Vector3(0.6f, 0.8f, 0.25f), plantMat);
            ItemSocket control = Socket(g, "control switch", "control_switch", panel + Vector3.up * 0.15f, Vector3.back);
            PhysicalSwitch motor = InteractiveFactory.Switch(g, panel + Vector3.down * 0.25f + Vector3.back * 0.02f, Vector3.back, "shutter motor", plantMat, redMat);

            PuzzleGate gate = Gate(g, "Car park exit", fuse, control, motor);
            Output<RaiseShutterOutput>(gate).shutter = NearestShutter(new Vector3(ramp.maxX + 0.1f, 1.5f, (ramp.minZ + ramp.maxZ) * 0.5f));
            Exit(g, EscapeRouteId.CarParkExit, new Vector3(ramp.maxX + 0.7f, 1.4f, (ramp.minZ + ramp.maxZ) * 0.5f), new Vector3(0.8f, 2.6f, ramp.Depth - 0.6f), gate);
        }

        // ------------------------------------------------------------------ 3. Loading bay
        // The dock door controller is missing its circuit module; with it fitted, the loading bay remote raises a dock shutter.
        static void RouteLoadingBay(Transform g)
        {
            float q = MallLayout.QuadrantOuter;
            Vector3 panel = new Vector3(q - 0.32f, 1.3f, 26.25f);
            Detail("Dock controller", g, panel + Vector3.right * 0.1f, new Vector3(0.2f, 0.9f, 0.7f), plantMat);
            ItemSocket module = Socket(g, "circuit module", "circuit_module", panel + Vector3.up * 0.2f + Vector3.left * 0.01f, Vector3.left);
            CardReader remote = Reader(g, "dock_control", panel + Vector3.down * 0.25f + Vector3.left * 0.01f, Vector3.left, "Use remote", "Remote receiver");

            PuzzleGate gate = Gate(g, "Loading bay", module, remote);
            Output<RaiseShutterOutput>(gate).shutter = NearestShutter(new Vector3(q - 0.15f, 1.8f, 23.25f));
            Exit(g, EscapeRouteId.LoadingBay, new Vector3(q + 0.5f, 1.4f, 23.25f), new Vector3(0.8f, 2.6f, 4f), gate);
        }

        // ------------------------------------------------------------------ 4. Emergency exit
        // The fire exit is chained shut (cut the chain with wire cutters) and locked (fire exit key).
        static void RouteEmergencyExit(Transform g)
        {
            float end = MallLayout.AnchorEnd;
            GameObject chain = Blocker(g, "Chain and padlock", new Vector3(-end + 0.4f, 1.05f, 0f), new Vector3(0.06f, 0.5f, 1.9f), metal);
            GameObject outer = Blocker(g, "Chain (outside)", new Vector3(-end - 0.4f, 1.05f, 0f), new Vector3(0.06f, 0.5f, 1.9f), metal);
            outer.transform.SetParent(chain.transform, true);

            // The cutting point lives on its own object so it survives the chain disappearing.
            var cutter = new GameObject("Chain (cut point)");
            cutter.transform.SetParent(g, false);
            cutter.transform.position = new Vector3(-end + 0.45f, 1.05f, 0f);
            BoxCollider cc = cutter.AddComponent<BoxCollider>();
            cc.isTrigger = true;
            cc.size = new Vector3(0.2f, 0.6f, 1.9f);
            EditorBuild.SetLayerRecursive(cutter, GameLayers.InteractableLayer);
            ToolBreakable cut = cutter.AddComponent<ToolBreakable>();
            cut.toolId = "cut_wire";
            cut.verb = "Cut the chain";
            cut.seconds = 3f;
            cut.blockers = new[] { chain };

            // The door itself is locked with the fire exit key and must be unlocked and pushed open by hand.
            Door door = NearestBuiltDoor(new Vector3(-end, 0f, 0f));
            if (door != null) door.lockId = "key_exit";
            PuzzleGate gate = Gate(g, "Emergency exit", cut);
            Exit(g, EscapeRouteId.EmergencyExit, new Vector3(-end - 0.8f, 1.2f, 0f), new Vector3(0.8f, 2.2f, 2f), gate);
        }

        // ------------------------------------------------------------------ 5. Rooftop
        // Roof door (roof hatch key), then the fire escape ladder hatch is inside a padlocked cage: force it with a pry bar.
        static void RouteRooftop(Transform g)
        {
            float y = MallLayout.RoofY;
            Vector3 c = new Vector3(43.5f, y, 43.5f);
            // Cage around the ladder hatch (two sides are the parapets).
            BoxMinMax("Cage bars", g, new Vector3(42.4f, y, 42.4f), new Vector3(44.6f, y + 2.2f, 42.46f), metal, SurfaceType.Metal);
            BoxMinMax("Cage bars", g, new Vector3(42.4f, y, 42.4f), new Vector3(42.46f, y + 2.2f, 43.3f), metal, SurfaceType.Metal);
            BoxMinMax("Cage top", g, new Vector3(42.4f, y + 2.2f, 42.4f), new Vector3(44.7f, y + 2.26f, 44.7f), metal, SurfaceType.Metal);
            GameObject gate = Blocker(g, "Cage gate (padlocked)", new Vector3(42.43f, y + 1.1f, 44.0f), new Vector3(0.06f, 2.2f, 1.4f), metal);
            Detail("Ladder hatch", g, c + Vector3.up * 0.02f, new Vector3(0.9f, 0.04f, 0.9f), plantMat);
            Cyl("Ladder rail", g, c + new Vector3(-0.3f, 0f, 0.5f), c + new Vector3(-0.3f, 1.2f, 0.7f), 0.05f, metal);
            Cyl("Ladder rail", g, c + new Vector3(0.3f, 0f, 0.5f), c + new Vector3(0.3f, 1.2f, 0.7f), 0.05f, metal);

            var pry = new GameObject("Cage padlock");
            pry.transform.SetParent(g, false);
            pry.transform.position = new Vector3(42.3f, y + 1.1f, 43.4f);
            BoxCollider bc = pry.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(0.3f, 0.4f, 0.3f);
            EditorBuild.SetLayerRecursive(pry, GameLayers.InteractableLayer);
            Detail("Padlock", g, pry.transform.position, new Vector3(0.06f, 0.08f, 0.04f), yellowPaint);
            ToolBreakable force = pry.AddComponent<ToolBreakable>();
            force.toolId = "pry";
            force.verb = "Force the cage open";
            force.seconds = 3.5f;
            force.noiseRadius = 26f;
            force.blockers = new[] { gate };

            PuzzleGate pg = Gate(g, "Rooftop", force);
            Exit(g, EscapeRouteId.Rooftop, c + Vector3.up * 0.7f, new Vector3(0.9f, 1.2f, 0.9f), pg, false);
        }

        // ------------------------------------------------------------------ 6. Service tunnel
        // The pressure door at the far end has lost its valve wheel: fit it, then turn it (long and loud).
        static void RouteServiceTunnel(Transform g)
        {
            Vector3 doorPos = new Vector3(-180f, MallLayout.CarParkY, -18.5f);
            ItemSocket valve = Socket(g, "valve wheel", "tunnel_valve", doorPos + new Vector3(0.35f, 1.25f, 0.9f), Vector3.right, 6f);
            valve.turnNoiseRadius = 22f;
            PuzzleGate gate = Gate(g, "Service tunnel", valve);
            UnlockDoorOutput unlock = Output<UnlockDoorOutput>(gate);
            unlock.door = NearestBuiltDoor(doorPos);
            Exit(g, EscapeRouteId.ServiceTunnel, doorPos + new Vector3(-0.8f, 1.1f, 0f), new Vector3(0.8f, 2.1f, 1.4f), gate);
        }

        // ------------------------------------------------------------------ 7. Restricted staff exit
        // Special access card at the exit's reader + the heavy-duty door actuator fitted into its mechanism.
        static void RouteStaffExit(Transform g)
        {
            float q = MallLayout.QuadrantOuter;
            Vector3 doorPos = new Vector3(40f, 0f, -q);
            CardReader reader = Reader(g, "card_special", new Vector3(38.9f, 1.3f, -q + 0.32f), Vector3.forward);
            Detail("Door mechanism housing", g, new Vector3(41.5f, 2.3f, -q + 0.4f), new Vector3(0.7f, 0.5f, 0.3f), plantMat);
            ItemSocket actuator = Socket(g, "door actuator", "heavy_door_part", new Vector3(41.5f, 2.3f, -q + 0.56f), Vector3.forward);
            PuzzleGate gate = Gate(g, "Staff exit", reader, actuator);
            Output<UnlockDoorOutput>(gate).door = NearestBuiltDoor(doorPos);
            Output<MachineStartOutput>(gate).noiseRadius = 18f;
            Exit(g, EscapeRouteId.RestrictedStaffExit, doorPos + new Vector3(0f, 1.1f, -0.8f), new Vector3(1.4f, 2.1f, 0.8f), gate);
        }

        // ------------------------------------------------------------------ 8. Hidden route
        // Behind cinema screen 3: an unmarked stiff door, a disused passage blocked by a heavy crate stack (pull it
        // back), and a screwed-down floor hatch whose latch needs the mechanical handle.
        static void RouteHidden(Transform g)
        {
            float y = MallLayout.FloorY(MallFloor.Level2);
            WingId w = WingId.East;
            GameObject debris = WingBox("Crate stack (heavy)", g, w, 113f, 114.4f, 15.8f, 19.2f, y, y + 1.6f, propMat, SurfaceType.Wood, false);
            Rigidbody rb = debris.AddComponent<Rigidbody>();
            rb.mass = 160f;
            HeavyMovable hm = debris.AddComponent<HeavyMovable>();
            hm.soloSpeed = 0.4f;
            hm.material = SurfaceType.Wood;
            EditorBuild.SetLayerRecursive(debris, GameLayers.PropLayer);

            Vector3 hatch = MallLayout.EscapePoint(EscapeRouteId.Hidden);
            VentCover cover = InteractiveFactory.Vent(g, hatch + Vector3.up * 0.01f, Vector3.up, new Vector2(0.8f, 0.8f), VentCover.State.Screwed, plantMat);
            ItemSocket latch = Socket(g, "handle", "mech_handle", MallLayout.WingPoint(w, 107.9f, 17.5f, y + 0.25f), MallLayout.WingForward(w) * -1f, 1.5f);
            PuzzleGate gate = Gate(g, "Hidden", latch);
            EscapeTrigger t = Exit(g, EscapeRouteId.Hidden, hatch + Vector3.up * 0.3f, new Vector3(0.7f, 0.6f, 0.7f), gate, false);
            t.requiresOpenCover = cover;
        }
    }
}
