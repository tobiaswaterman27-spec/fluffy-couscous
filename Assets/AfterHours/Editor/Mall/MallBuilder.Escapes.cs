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

            generator = BuildGenerator(Group("Standby generator", parent));
            RouteMainEntrance(Group("Route 1 - Main entrance", parent));
            RouteCarParkExit(Group("Route 2 - Car park exit", parent));
            RouteLoadingBay(Group("Route 3 - Loading bay", parent));
            RouteEmergencyExit(Group("Route 4 - Emergency exit", parent));
            RouteRooftop(Group("Route 5 - Rooftop", parent));
            RouteServiceTunnel(Group("Route 6 - Service tunnel", parent));
            RouteStaffExit(Group("Route 7 - Restricted staff exit", parent));
            RouteHidden(Group("Route 8 - Hidden", parent));
            AxeableDoors();
            if (generator != null) generator.emergencyLights = emergencyLamps.ToArray();
        }

        // ------------------------------------------------------------------ standby generator and the emergency circuit
        static Generator generator;
        static readonly List<Light> emergencyLamps = new List<Light>();

        /// <summary>A small amber lamp that lights when the standby generator is running (the clue that it matters).</summary>
        static void EmergencyLamp(Transform g, Vector3 pos, Vector3 facing)
        {
            Detail("Emergency power lamp", g, pos, new Vector3(0.05f, 0.05f, 0.03f), yellowPaint, Quaternion.LookRotation(facing));
            var go = new GameObject("Emergency power lamp light");
            go.transform.SetParent(g, false);
            go.transform.position = pos + facing * 0.06f;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 1.6f;
            l.intensity = 0.9f;
            l.color = new Color(1f, 0.62f, 0.18f);
            l.shadows = LightShadows.None;
            l.enabled = false;
            emergencyLamps.Add(l);
            Sign(g, "EMERGENCY CIRCUIT", pos + Vector3.up * 0.12f + facing * 0.01f, -facing, 0.5f, 0.016f);
        }

        /// <summary>
        /// The standby generator in the car park plant room (south-west corner). Needs petrol and its control fuse,
        /// then a pull-start. Feeds the emergency circuit: main door release, car park exit shutter, dock controller,
        /// staff exit mechanism.
        /// </summary>
        static Generator BuildGenerator(Transform g)
        {
            emergencyLamps.Clear();
            float y = MallLayout.CarParkY;
            Vector3 c = new Vector3(-97.4f, y, -35.7f);
            GameObject body = EditorBuild.Box("Standby generator", g, c + Vector3.up * 0.65f, new Vector3(2.2f, 1.3f, 1.1f), plantMat, SurfaceType.Metal, null, false);
            EditorBuild.SetLayerRecursive(body, GameLayers.InteractableLayer);
            Detail("Generator skid", g, c + Vector3.up * 0.06f, new Vector3(2.4f, 0.12f, 1.2f), blackMat);
            Detail("Generator radiator grille", g, c + new Vector3(1.11f, 0.7f, 0f), new Vector3(0.02f, 0.9f, 0.9f), blackMat);
            Detail("Generator control panel", g, c + new Vector3(-0.5f, 1.0f, 0.56f), new Vector3(0.6f, 0.4f, 0.04f), blackMat);
            Cyl("Generator exhaust", g, c + new Vector3(-0.8f, 1.3f, -0.3f), c + new Vector3(-0.8f, 3.0f, -0.3f), 0.09f, metal);
            Sign(g, "STANDBY GENERATOR", c + new Vector3(0.3f, 1.5f, 0.56f), Vector3.back, 1.1f, 0.03f);
            Sign(g, "FEEDS: DOOR RELEASE / EXIT SHUTTER / DOCK / STAFF EXIT", c + new Vector3(0.3f, 1.38f, 0.56f), Vector3.back, 1.6f, 0.014f);

            Generator gen = body.AddComponent<Generator>();
            ItemSocket fuel = Socket(g, "petrol", "fuel", c + new Vector3(0.5f, 1.0f, 0.62f), Vector3.forward, 3f);
            fuel.turnNoiseRadius = 6f;
            ItemSocket fuse = Socket(g, "fuse", "fuse", c + new Vector3(-0.5f, 0.95f, 0.62f), Vector3.forward);
            Sign(g, "GEN CONTROL", c + new Vector3(-0.5f, 1.25f, 0.6f), Vector3.back, 0.5f, 0.016f);
            gen.fuel = fuel;
            gen.controlFuse = fuse;
            // Two emergency fittings in the plant room come on with it.
            foreach (Vector3 lp in new[] { new Vector3(-92f, -1.2f, -27f), new Vector3(-92f, -1.2f, -36.8f) })
            {
                var go = new GameObject("Emergency fitting");
                go.transform.SetParent(g, false);
                go.transform.position = lp;
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 9f;
                l.intensity = 0.7f;
                l.color = new Color(1f, 0.85f, 0.65f);
                l.shadows = LightShadows.Soft;
                l.enabled = false;
                emergencyLamps.Add(l);
            }
            return gen;
        }

        /// <summary>Wooden office doors can be chopped through with the fire axe.</summary>
        static void AxeableDoors()
        {
            int n = 0;
            foreach (Door d in Object.FindObjectsByType<Door>(FindObjectsInactive.Include))
            {
                if (!d.locked || d.leaf == null) continue;
                if (d.kind != DoorKind.Staff && d.kind != DoorKind.Restricted && d.kind != DoorKind.Storage) continue;
                Collider leafCol = d.leaf.GetComponent<Collider>();
                if (leafCol == null) continue;
                ForceableLock f = d.leaf.gameObject.AddComponent<ForceableLock>();
                f.door = d;
                n++;
            }
            Debug.Log("[AfterHours] " + n + " wooden doors can be broken open with the fire axe.");
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
            foreach (DoorSlot slot in Object.FindObjectsByType<DoorSlot>(FindObjectsInactive.Include))
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
            foreach (RollerShutter r in Object.FindObjectsByType<RollerShutter>(FindObjectsInactive.Include))
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
            // Two-factor: the release also wants the security token in its slot, and the emergency circuit live.
            ItemSocket token = Socket(g, "token", "security_token", panel + Vector3.right * 0.5f, Vector3.back);
            EmergencyLamp(g, panel + Vector3.right * 0.95f + Vector3.up * 0.1f, Vector3.back);

            PuzzleGate gate = Gate(g, "Main entrance", reader, release, keyLock, token, generator);
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
            EmergencyLamp(g, panel + Vector3.up * 0.5f + Vector3.left * 0.25f, Vector3.back);

            // The shutter's gearbox has a stripped gear: unbolt the cover (spanner), fit the replacement gear.
            Vector3 box = panel + Vector3.right * 1.3f;
            Detail("Shutter gearbox", g, box + Vector3.forward * 0.12f, new Vector3(0.55f, 0.55f, 0.25f), plantMat);
            GameObject cover = Blocker(g, "Gearbox cover (bolted)", box + Vector3.back * 0.04f, new Vector3(0.5f, 0.5f, 0.03f), metal);
            ItemSocket gear = Socket(g, "gear", "gear", box + Vector3.forward * 0.02f, Vector3.back);
            var bolts = new GameObject("Gearbox cover bolts");
            bolts.transform.SetParent(g, false);
            bolts.transform.position = box + Vector3.back * 0.08f;
            BoxCollider bb = bolts.AddComponent<BoxCollider>();
            bb.isTrigger = true;
            bb.size = new Vector3(0.55f, 0.55f, 0.06f);
            EditorBuild.SetLayerRecursive(bolts, GameLayers.InteractableLayer);
            ToolBreakable unbolt = bolts.AddComponent<ToolBreakable>();
            unbolt.toolId = "spanner";
            unbolt.verb = "Undo the gearbox cover";
            unbolt.seconds = 4f;
            unbolt.noiseRadius = 12f;
            unbolt.blockers = new[] { cover };

            PuzzleGate gate = Gate(g, "Car park exit", fuse, control, motor, unbolt, gear, generator);
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
            ItemSocket connector = Socket(g, "power connector", "power_connector", panel + Vector3.up * 0.2f + Vector3.back * 0.3f + Vector3.left * 0.01f, Vector3.left);
            EmergencyLamp(g, panel + Vector3.up * 0.55f + Vector3.forward * 0.25f + Vector3.left * 0.02f, Vector3.left);

            PuzzleGate gate = Gate(g, "Loading bay", module, remote, connector, generator);
            Output<RaiseShutterOutput>(gate).shutter = NearestShutter(new Vector3(q - 0.15f, 1.8f, 23.25f));
            Exit(g, EscapeRouteId.LoadingBay, new Vector3(q + 0.5f, 1.4f, 23.25f), new Vector3(0.8f, 2.6f, 4f), gate);
        }

        // ------------------------------------------------------------------ 4. Emergency exit
        // The fire exit is chained shut: cut the chain (wire cutters) OR open its padlock (fire exit key).
        // Then it's a real delayed-egress door: push the bar, the alarm goes off, and the magnetic lock only lets go
        // after 15 seconds - unless you first cut the maglock's power cable (utility knife): fail-safe, it just opens.
        static void RouteEmergencyExit(Transform g)
        {
            float end = MallLayout.AnchorEnd;
            GameObject chain = Blocker(g, "Chain and padlock", new Vector3(-end + 0.4f, 1.05f, 0f), new Vector3(0.06f, 0.5f, 1.9f), metal);
            GameObject outer = Blocker(g, "Chain (outside)", new Vector3(-end - 0.4f, 1.05f, 0f), new Vector3(0.06f, 0.5f, 1.9f), metal);
            outer.transform.SetParent(chain.transform, true);
            Blocker(g, "Chain padlock", new Vector3(-end + 0.45f, 0.9f, 0.25f), new Vector3(0.05f, 0.08f, 0.06f), yellowPaint).transform.SetParent(chain.transform, true);

            // The cutting point lives on its own object so it survives the chain disappearing.
            var cutter = new GameObject("Chain (cut point)");
            cutter.transform.SetParent(g, false);
            cutter.transform.position = new Vector3(-end + 0.45f, 1.05f, -0.3f);
            BoxCollider cc = cutter.AddComponent<BoxCollider>();
            cc.isTrigger = true;
            cc.size = new Vector3(0.2f, 0.6f, 1.3f);
            EditorBuild.SetLayerRecursive(cutter, GameLayers.InteractableLayer);
            ToolBreakable cut = cutter.AddComponent<ToolBreakable>();
            cut.toolId = "cut_wire";
            cut.verb = "Cut the chain";
            cut.seconds = 3f;
            cut.blockers = new[] { chain };

            var padlock = new GameObject("Chain padlock (key)");
            padlock.transform.SetParent(g, false);
            padlock.transform.position = new Vector3(-end + 0.47f, 0.9f, 0.25f);
            BoxCollider pc = padlock.AddComponent<BoxCollider>();
            pc.isTrigger = true;
            pc.size = new Vector3(0.2f, 0.3f, 0.5f);
            EditorBuild.SetLayerRecursive(padlock, GameLayers.InteractableLayer);
            ToolBreakable unlock = padlock.AddComponent<ToolBreakable>();
            unlock.toolId = "key_exit";
            unlock.verb = "Unlock the padlock";
            unlock.seconds = 0.8f;
            unlock.noiseRadius = 4f;
            unlock.sound = AfterHours.Audio.ProceduralSounds.Sfx.Unlock;
            unlock.wearsTool = false;
            unlock.blockers = new[] { chain };
            cut.alternatives = new[] { unlock };
            unlock.alternatives = new[] { cut };
            AnyOfInput chainGone = g.gameObject.AddComponent<AnyOfInput>();
            chainGone.options.Add(cut);
            chainGone.options.Add(unlock);

            // Magnetic lock along the head of the door, its cable running to a junction box beside it.
            Detail("Maglock", g, new Vector3(-end + 0.08f, 2.1f, 0f), new Vector3(0.07f, 0.06f, 0.9f), metal);
            Detail("Maglock cable conduit", g, new Vector3(-end + 0.05f, 2.22f, 0.75f), new Vector3(0.03f, 0.03f, 0.7f), blackMat);
            Detail("Maglock junction box", g, new Vector3(-end + 0.07f, 2.1f, 1.2f), new Vector3(0.08f, 0.16f, 0.16f), plantMat);
            Sign(g, "PUSH BAR - ALARM WILL SOUND - DOOR RELEASES AFTER 15 SECONDS", new Vector3(-end + 0.02f, 1.55f, 0f), Vector3.left, 1.1f, 0.016f);
            var cable = new GameObject("Maglock cable");
            cable.transform.SetParent(g, false);
            cable.transform.position = new Vector3(-end + 0.12f, 2.1f, 1.2f);
            BoxCollider kc = cable.AddComponent<BoxCollider>();
            kc.isTrigger = true;
            kc.size = new Vector3(0.15f, 0.3f, 0.3f);
            EditorBuild.SetLayerRecursive(cable, GameLayers.InteractableLayer);
            ToolBreakable cutPower = cable.AddComponent<ToolBreakable>();
            cutPower.toolId = "cut_light";
            cutPower.verb = "Cut the maglock cable";
            cutPower.seconds = 1.5f;
            cutPower.noiseRadius = 4f;
            cutPower.sound = AfterHours.Audio.ProceduralSounds.Sfx.Crack;
            cutPower.wearsTool = false;
            cutPower.blockers = new GameObject[0];

            var bar = new GameObject("Push bar (delayed egress)");
            bar.transform.SetParent(g, false);
            bar.transform.position = new Vector3(-end + 0.12f, 1.0f, 0f);
            BoxCollider barCol = bar.AddComponent<BoxCollider>();
            barCol.isTrigger = true;
            barCol.size = new Vector3(0.1f, 0.25f, 1.1f);
            EditorBuild.SetLayerRecursive(bar, GameLayers.InteractableLayer);
            Detail("Push bar", g, new Vector3(-end + 0.09f, 1.0f, 0f), new Vector3(0.05f, 0.06f, 0.85f), metal);
            DelayedEgress egress = bar.AddComponent<DelayedEgress>();
            egress.powerCut = cutPower;
            var beaconGo = new GameObject("Alarm beacon");
            beaconGo.transform.SetParent(g, false);
            beaconGo.transform.position = new Vector3(-end + 0.25f, 2.45f, 0f);
            Detail("Alarm beacon", g, new Vector3(-end + 0.06f, 2.45f, 0f), new Vector3(0.1f, 0.1f, 0.1f), redMat);
            Light beacon = beaconGo.AddComponent<Light>();
            beacon.type = LightType.Point;
            beacon.color = new Color(1f, 0.1f, 0.05f);
            beacon.range = 7f;
            beacon.intensity = 2.5f;
            beacon.shadows = LightShadows.None;
            beacon.enabled = false;
            egress.beacon = beacon;

            // The door is held by the maglock (no key opens it); the gate lets it go.
            Door door = NearestBuiltDoor(new Vector3(-end, 0f, 0f));
            if (door != null)
            {
                door.locked = true;
                door.lockId = "maglock";
                door.forceable = false;
            }
            PuzzleGate gate = Gate(g, "Emergency exit", chainGone, egress);
            if (door != null) Output<UnlockDoorOutput>(gate).door = door;
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

            // The hatch itself is bolted down and rusted: a spanner and some effort (loud, up here in the wind).
            var hb = new GameObject("Hatch bolts");
            hb.transform.SetParent(g, false);
            hb.transform.position = c + Vector3.up * 0.1f;
            BoxCollider hbc = hb.AddComponent<BoxCollider>();
            hbc.isTrigger = true;
            hbc.size = new Vector3(0.9f, 0.2f, 0.9f);
            EditorBuild.SetLayerRecursive(hb, GameLayers.InteractableLayer);
            for (int i = 0; i < 4; i++)
                Detail("Hatch bolt", g, c + new Vector3(i < 2 ? -0.38f : 0.38f, 0.05f, i % 2 == 0 ? -0.38f : 0.38f), new Vector3(0.04f, 0.03f, 0.04f), metal);
            ToolBreakable bolts = hb.AddComponent<ToolBreakable>();
            bolts.toolId = "spanner";
            bolts.verb = "Undo the rusted hatch bolts";
            bolts.seconds = 5f;
            bolts.noiseRadius = 16f;
            bolts.blockers = new GameObject[0];

            PuzzleGate pg = Gate(g, "Rooftop", force, bolts);
            Exit(g, EscapeRouteId.Rooftop, c + Vector3.up * 0.7f, new Vector3(0.9f, 1.2f, 0.9f), pg, false);
        }

        // ------------------------------------------------------------------ 6. Service tunnel
        // The pressure door at the far end has lost its valve wheel: fit it, then turn it (long and loud).
        static void RouteServiceTunnel(Transform g)
        {
            Vector3 doorPos = new Vector3(-180f, MallLayout.CarParkY, -18.5f);
            ItemSocket valve = Socket(g, "valve wheel", "tunnel_valve", doorPos + new Vector3(0.35f, 1.25f, 0.9f), Vector3.right, 6f);
            valve.turnNoiseRadius = 22f;
            // Years without use: the stem is seized. Work it loose with a spanner first.
            var stem = new GameObject("Seized valve stem");
            stem.transform.SetParent(g, false);
            stem.transform.position = doorPos + new Vector3(0.45f, 1.25f, 0.9f);
            BoxCollider sc = stem.AddComponent<BoxCollider>();
            sc.isTrigger = true;
            sc.size = new Vector3(0.12f, 0.4f, 0.4f);
            EditorBuild.SetLayerRecursive(stem, GameLayers.InteractableLayer);
            ToolBreakable free = stem.AddComponent<ToolBreakable>();
            free.toolId = "spanner";
            free.verb = "Work the seized stem loose";
            free.seconds = 4f;
            free.noiseRadius = 18f;
            free.blockers = new GameObject[0];
            PuzzleGate gate = Gate(g, "Service tunnel", free, valve);
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
            Detail("Door control box", g, new Vector3(38.2f, 1.3f, -q + 0.3f), new Vector3(0.36f, 0.42f, 0.12f), plantMat);
            ItemSocket sw = Socket(g, "switch", "replacement_switch", new Vector3(38.2f, 1.3f, -q + 0.38f), Vector3.forward);
            EmergencyLamp(g, new Vector3(38.9f, 1.75f, -q + 0.33f), Vector3.forward);
            PuzzleGate gate = Gate(g, "Staff exit", reader, actuator, sw, generator);
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
            // The latch's drive gear is missing - the same kind the car park shutter gearbox needs. There's only one.
            ItemSocket gear = Socket(g, "gear", "gear", MallLayout.WingPoint(w, 107.9f, 16.8f, y + 0.25f), MallLayout.WingForward(w) * -1f);
            PuzzleGate gate = Gate(g, "Hidden", gear, latch);
            EscapeTrigger t = Exit(g, EscapeRouteId.Hidden, hatch + Vector3.up * 0.3f, new Vector3(0.7f, 0.6f, 0.7f), gate, false);
            t.requiresOpenCover = cover;
        }
    }
}
