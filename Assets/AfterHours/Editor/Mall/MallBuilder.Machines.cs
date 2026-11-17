using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Core;
using AfterHours.Machines;
using AfterHours.Power;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Part 12: electricity and machinery - working lifts, moving escalators, the main switchboard in the car park
    /// plant room, the areas each lighting circuit covers (with battery emergency lights), and the malfunctions.
    /// </summary>
    public static partial class MallBuilder
    {
        static Material buttonMat, lampMat, carWallMat, carFloorMat, emergencyMat;

        static void MachineMaterials()
        {
            buttonMat = ArtLibrary.Plain("Lift_Button", new Color(0.78f, 0.75f, 0.68f), 0.6f, 0.6f, new Color(0.01f, 0.01f, 0.01f), false);
            lampMat = ArtLibrary.Plain("Indicator_Lamp", new Color(0.12f, 0.14f, 0.12f), 0.8f, 0f, new Color(0.01f, 0.01f, 0.01f), false);
            carWallMat = ArtLibrary.Plain("Lift_Car_Wall", new Color(0.55f, 0.56f, 0.58f), 0.75f, 0.9f);
            carFloorMat = ArtLibrary.Plain("Lift_Car_Floor", new Color(0.16f, 0.16f, 0.17f), 0.3f);
            emergencyMat = ArtLibrary.Plain("Emergency_Light", new Color(0.9f, 0.9f, 0.85f), 0.5f, 0f, new Color(0.25f, 0.25f, 0.22f), true);
        }

        /// <summary>A dynamic (not merged) box positioned in its parent's local space.</summary>
        static GameObject Part(string name, Transform parent, Vector3 local, Vector3 size, Material m, SurfaceType s, bool collider = true, Quaternion? rot = null)
        {
            GameObject go = EditorBuild.Box(name, parent, local, size, m, s, rot, false);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // =====================================================================
        // Lifts
        // =====================================================================

        static float LiftDoorWidth(bool service) => service ? 1.6f : 1.1f;

        static void BuildLift(Transform parent, AreaRect r, string name, bool service)
        {
            MachineMaterials();
            const float t = 0.15f;
            float doorW = LiftDoorWidth(service);
            float cx = (r.minX + r.maxX) * 0.5f;
            var ys = new float[LiftLevels.Length];
            var names = new string[LiftLevels.Length];
            for (int i = 0; i < ys.Length; i++) { ys[i] = MallLayout.FloorY(LiftLevels[i]); names[i] = MallLayout.FloorCode(LiftLevels[i]); }

            LiftController lift = parent.gameObject.AddComponent<LiftController>();
            var landings = new SlidingDoorPair[ys.Length];
            var indicators = new List<TextMesh>();
            int interactable = GameLayers.InteractableLayer;

            for (int i = 0; i < ys.Length; i++)
            {
                float y = ys[i];
                // Landing doors in the shaft opening.
                var pairGo = new GameObject($"{name} landing doors {names[i]}");
                pairGo.transform.SetParent(parent, false);
                pairGo.transform.localPosition = new Vector3(cx, y, r.minZ + t * 0.5f);
                SlidingDoorPair pair = pairGo.AddComponent<SlidingDoorPair>();
                pair.left = Part("Leaf L", pairGo.transform, new Vector3(-doorW * 0.25f, DoorHeight * 0.5f, 0f), new Vector3(doorW * 0.5f + 0.02f, DoorHeight, 0.04f), liftDoorMat, SurfaceType.Metal).transform;
                pair.right = Part("Leaf R", pairGo.transform, new Vector3(doorW * 0.25f, DoorHeight * 0.5f, 0f), new Vector3(doorW * 0.5f + 0.02f, DoorHeight, 0.04f), liftDoorMat, SurfaceType.Metal).transform;
                pair.travel = doorW * 0.5f;
                EditorBuild.SetLayerRecursive(pairGo, interactable);
                landings[i] = pair;

                // Call button and its panel beside the doors; floor indicator above.
                float bx = cx + doorW * 0.5f + 0.3f;
                Part("Call panel", parent, new Vector3(bx, y + 1.1f, r.minZ - 0.012f), new Vector3(0.12f, 0.26f, 0.02f), metal, SurfaceType.Metal, false);
                GameObject btn = Part("Call button", parent, new Vector3(bx, y + 1.1f, r.minZ - 0.03f), new Vector3(0.05f, 0.05f, 0.02f), buttonMat, SurfaceType.Metal);
                btn.GetComponent<BoxCollider>().size = new Vector3(3f, 3f, 3f);
                btn.layer = interactable;
                LiftButton call = btn.AddComponent<LiftButton>();
                call.lift = lift;
                call.level = i;
                call.isCall = true;
                call.lamp = btn.GetComponent<Renderer>();
                Part("Indicator panel", parent, new Vector3(cx, y + DoorHeight + 0.2f, r.minZ - 0.012f), new Vector3(0.3f, 0.16f, 0.02f), blackMat, SurfaceType.Metal, false);
                TextMesh ind = EditorBuild.Label(parent, names[i], new Vector3(cx, y + DoorHeight + 0.2f, r.minZ - 0.026f), 0f, 0.02f, false);
                ind.color = new Color(1f, 0.35f, 0.1f);
                indicators.Add(ind);
            }

            // ---- the car ----
            float x0 = r.minX + t + 0.04f, x1 = r.maxX - t - 0.04f, z0 = r.minZ + t + 0.05f, z1 = r.maxZ - t - 0.04f;
            float cw = x1 - x0, cd = z1 - z0;
            const float ch = 2.45f;
            int start = 1; // waits at the ground floor
            var carGo = new GameObject(name + " car");
            carGo.transform.SetParent(parent, false);
            carGo.transform.localPosition = new Vector3(cx, ys[start], (z0 + z1) * 0.5f);
            BoxCollider floorCol = carGo.AddComponent<BoxCollider>();
            floorCol.center = new Vector3(0f, -0.06f, 0f);
            floorCol.size = new Vector3(cw, 0.12f, cd);
            carGo.AddComponent<SurfaceMaterial>().type = SurfaceType.Metal;
            MovingPlatform platform = carGo.AddComponent<MovingPlatform>();
            platform.manual = true;
            platform.travel = Vector3.zero;
            Transform car = carGo.transform;

            Part("Car floor", car, new Vector3(0f, -0.06f, 0f), new Vector3(cw, 0.12f, cd), carFloorMat, SurfaceType.Metal, false);
            Part("Car sill", car, new Vector3(0f, -0.03f, -cd * 0.5f - 0.08f), new Vector3(doorW, 0.06f, 0.16f), metal, SurfaceType.Metal);
            Part("Car back", car, new Vector3(0f, ch * 0.5f, cd * 0.5f - 0.02f), new Vector3(cw, ch, 0.04f), carWallMat, SurfaceType.Metal);
            Part("Car side", car, new Vector3(-cw * 0.5f + 0.02f, ch * 0.5f, 0f), new Vector3(0.04f, ch, cd), carWallMat, SurfaceType.Metal);
            Part("Car side", car, new Vector3(cw * 0.5f - 0.02f, ch * 0.5f, 0f), new Vector3(0.04f, ch, cd), carWallMat, SurfaceType.Metal);
            Part("Car ceiling", car, new Vector3(0f, ch + 0.02f, 0f), new Vector3(cw, 0.04f, cd), carWallMat, SurfaceType.Metal);
            float jamb = (cw - doorW) * 0.5f;
            Part("Car front", car, new Vector3(-(doorW + jamb) * 0.5f, ch * 0.5f, -cd * 0.5f + 0.02f), new Vector3(jamb, ch, 0.04f), carWallMat, SurfaceType.Metal);
            Part("Car front", car, new Vector3((doorW + jamb) * 0.5f, ch * 0.5f, -cd * 0.5f + 0.02f), new Vector3(jamb, ch, 0.04f), carWallMat, SurfaceType.Metal);
            Part("Car door header", car, new Vector3(0f, (DoorHeight + ch) * 0.5f, -cd * 0.5f + 0.02f), new Vector3(doorW, ch - DoorHeight, 0.04f), carWallMat, SurfaceType.Metal);
            Part("Handrail", car, new Vector3(0f, 0.95f, cd * 0.5f - 0.08f), new Vector3(cw - 0.3f, 0.04f, 0.04f), metal, SurfaceType.Metal, false);
            Part("Mirror", car, new Vector3(0f, 1.6f, cd * 0.5f - 0.045f), new Vector3(cw - 0.5f, 1.0f, 0.01f), mirrorMat, SurfaceType.Glass, false);
            Part("Light panel", car, new Vector3(0f, ch - 0.01f, 0f), new Vector3(cw * 0.6f, 0.02f, cd * 0.5f), emergencyMat, SurfaceType.Metal, false);

            var doorsGo = new GameObject("Car doors");
            doorsGo.transform.SetParent(car, false);
            doorsGo.transform.localPosition = new Vector3(0f, 0f, -cd * 0.5f + 0.07f);
            SlidingDoorPair carDoors = doorsGo.AddComponent<SlidingDoorPair>();
            carDoors.left = Part("Leaf L", doorsGo.transform, new Vector3(-doorW * 0.25f, DoorHeight * 0.5f, 0f), new Vector3(doorW * 0.5f + 0.02f, DoorHeight, 0.03f), liftDoorMat, SurfaceType.Metal).transform;
            carDoors.right = Part("Leaf R", doorsGo.transform, new Vector3(doorW * 0.25f, DoorHeight * 0.5f, 0f), new Vector3(doorW * 0.5f + 0.02f, DoorHeight, 0.03f), liftDoorMat, SurfaceType.Metal).transform;
            carDoors.travel = doorW * 0.5f;

            // Panel on the right-hand wall inside: floor buttons, open doors, alarm.
            float px = cw * 0.5f - 0.05f, pz = -cd * 0.5f + 0.4f;
            Part("Car panel", car, new Vector3(px + 0.015f, 1.1f, pz), new Vector3(0.01f, 0.75f, 0.16f), metal, SurfaceType.Metal, false);
            for (int i = -2; i < ys.Length; i++)
            {
                float by = i >= 0 ? 1.05f + i * 0.13f : (i == LiftButton.OpenDoors ? 0.9f : 0.78f);
                GameObject b = Part("Car button", car, new Vector3(px, by, pz), new Vector3(0.02f, 0.05f, 0.05f), i == LiftButton.AlarmBell ? redMat : buttonMat, SurfaceType.Metal);
                b.GetComponent<BoxCollider>().size = new Vector3(3f, 2.4f, 2.4f);
                LiftButton lb = b.AddComponent<LiftButton>();
                lb.lift = lift;
                lb.level = i;
                lb.lamp = b.GetComponent<Renderer>();
                string text = i >= 0 ? names[i] : i == LiftButton.OpenDoors ? "<|>" : "ALARM";
                EditorBuild.Label(car, text, new Vector3(px - 0.006f, by, pz - 0.07f), -90f, 0.012f);
            }
            TextMesh carInd = EditorBuild.Label(car, names[start], new Vector3(0f, DoorHeight + 0.16f, -cd * 0.5f + 0.045f), 180f, 0.02f, false);
            carInd.color = new Color(1f, 0.35f, 0.1f);
            indicators.Add(carInd);

            var lightGo = new GameObject("Car light");
            lightGo.transform.SetParent(car, false);
            lightGo.transform.localPosition = new Vector3(0f, ch - 0.2f, 0f);
            Light carLight = lightGo.AddComponent<Light>();
            carLight.type = LightType.Point;
            carLight.range = 4f;
            carLight.intensity = 1.1f;
            carLight.shadows = LightShadows.None;
            carLight.lightmapBakeType = LightmapBakeType.Realtime;

            var motorGo = new GameObject("Motor");
            motorGo.transform.SetParent(car, false);
            motorGo.transform.localPosition = new Vector3(0f, ch + 0.5f, 0f);
            AudioSource motorSrc = motorGo.AddComponent<AudioSource>();
            motorSrc.playOnAwake = false;
            AmbientSound hum = motorGo.AddComponent<AmbientSound>();
            hum.kind = ProceduralSounds.Ambience.Machinery;
            hum.volume = 0f;
            hum.maxDistance = 18f;
            hum.maskingLevel = 0.25f;

            EditorBuild.SetLayerRecursive(carGo, interactable);
            foreach (Transform tr in carGo.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(tr.gameObject, 0);

            lift.car = platform;
            lift.levelY = ys;
            lift.levelNames = names;
            lift.carDoors = carDoors;
            lift.landingDoors = landings;
            lift.indicators = indicators.ToArray();
            lift.carLight = carLight;
            lift.motorAudio = motorSrc;
            lift.startLevel = start;
            lift.seed = MallLayout.Hash((int)(r.minX * 10f), (int)(r.minZ * 10f), 12, 1);
            lift.speed = service ? 1.0f : 1.4f;
        }

        // =====================================================================
        // Escalators
        // =====================================================================

        static void Escalator(string name, Transform parent, Vector3 bottom, Vector3 top, float width)
        {
            Transform g = Group(name, parent);
            GameObject treads = EditorBuild.Ramp(name + " treads", g, bottom, top, width, 0.45f, metal, SurfaceType.Metal);
            GameObjectUtility.SetStaticEditorFlags(treads, StaticEditorFlags.ContributeGI);
            ConveyorSurface belt = treads.AddComponent<ConveyorSurface>();
            belt.running = false;

            Vector3 dir = (top - bottom).normalized;
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            Vector3 right = rot * Vector3.right;
            Vector3 normal = rot * Vector3.up;
            float length = Vector3.Distance(bottom, top);
            Vector3 mid = (bottom + top) * 0.5f;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 c = mid + right * (sgn * (width * 0.5f + 0.06f)) + normal * 0.45f;
                EditorBuild.Box(name + " balustrade", g, c, new Vector3(0.1f, 1.3f, length + 1.0f), railMat, SurfaceType.Metal, rot);
                Vector3 hr = mid + right * (sgn * (width * 0.5f + 0.06f)) + normal * 1.12f;
                EditorBuild.Box(name + " handrail", g, hr, new Vector3(0.12f, 0.05f, length + 1.1f), blackMat, SurfaceType.Metal, rot);
            }

            // The moving steps (visual; the tread surface underneath carries you).
            int n = Mathf.Max(6, Mathf.RoundToInt(length / 0.4f));
            var steps = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                GameObject s = Part(name + " step", g, Vector3.Lerp(bottom, top, i / (float)n) + normal * 0.02f, new Vector3(width - 0.06f, 0.03f, 0.37f), darkPanel, SurfaceType.Metal, false, rot);
                steps[i] = s.transform;
            }
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = i == 0 ? bottom - dir * 0.5f : top + new Vector3(dir.x, 0f, dir.z).normalized * 0.5f;
                Decor(name + " comb plate", g, p + new Vector3(-width * 0.5f, 0f, -0.3f), p + new Vector3(width * 0.5f, 0.012f, 0.3f), metal);
            }

            Escalator e = g.gameObject.AddComponent<Escalator>();
            e.belt = belt;
            e.steps = steps;
            e.bottom = bottom + normal * 0.02f;
            e.top = top + normal * 0.02f;
            e.goesUp = name.Contains(" up");
            e.seed = MallLayout.Hash((int)(bottom.x * 10f), (int)(bottom.z * 10f), 5, 5);
            e.state = name.Contains("1-2 down") ? EscalatorState.Faulty : name.Contains("G-1 down") ? EscalatorState.Stopped : EscalatorState.Running;

            var motorGo = new GameObject("Escalator machinery");
            motorGo.transform.SetParent(g, false);
            motorGo.transform.localPosition = bottom + Vector3.up * 0.3f;
            AudioSource src = motorGo.AddComponent<AudioSource>();
            src.playOnAwake = false;
            AmbientSound amb = motorGo.AddComponent<AmbientSound>();
            amb.kind = ProceduralSounds.Ambience.Machinery;
            amb.volume = 0.4f;
            amb.maxDistance = 18f;
            amb.maskingLevel = 0.55f;
            e.motor = src;

            // Emergency stop at the foot, on the balustrade end.
            Vector3 bp = bottom - dir * 0.35f + right * (width * 0.5f + 0.06f) + Vector3.up * 0.95f;
            GameObject stop = EditorBuild.Box(name + " emergency stop", g, bp, new Vector3(0.09f, 0.09f, 0.09f), redMat, SurfaceType.Metal, null, false);
            stop.GetComponent<BoxCollider>().size = new Vector3(2.2f, 2.2f, 2.2f);
            stop.layer = GameLayers.InteractableLayer;
            stop.AddComponent<EscalatorControl>().escalator = e;
        }

        // =====================================================================
        // Power: switchboard, circuit areas, emergency lights, malfunctions
        // =====================================================================

        static Transform BuildSwitchboard(Transform parent)
        {
            MachineMaterials();
            float y = MallLayout.CarParkY;
            const float wallFace = -25.125f, depth = 0.35f;
            Transform g = Group("Main switchboard", parent);
            BoxMinMax("Switchboard cabinet", g, new Vector3(-98.2f, y + 0.1f, wallFace - depth), new Vector3(-89.3f, y + 2.3f, wallFace), plantMat, SurfaceType.Metal);
            EditorBuild.Label(g, "MAIN SWITCHBOARD  -  DANGER 415V", new Vector3(-93.75f, y + 2.12f, wallFace - depth - 0.012f), 0f, 0.03f);
            float x = -97.5f;
            foreach (string id in PowerGrid.Circuits)
            {
                var root = new GameObject("Breaker " + id);
                root.transform.SetParent(g, false);
                root.transform.localPosition = new Vector3(x, y + 1.3f, wallFace - depth - 0.05f);
                BoxCollider col = root.AddComponent<BoxCollider>();
                col.size = new Vector3(0.3f, 0.5f, 0.15f);
                root.layer = GameLayers.InteractableLayer;
                Part("Breaker body", root.transform, new Vector3(0f, 0f, 0.02f), new Vector3(0.2f, 0.32f, 0.06f), blackMat, SurfaceType.Metal, false);
                var pivot = new GameObject("Lever pivot");
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                Part("Lever", pivot.transform, new Vector3(0f, 0f, -0.08f), new Vector3(0.05f, 0.05f, 0.16f), id == PowerGrid.Main ? redMat : yellowPaint, SurfaceType.Metal, false);
                GameObject lamp = Part("Lamp", root.transform, new Vector3(0f, 0.26f, 0f), new Vector3(0.05f, 0.05f, 0.03f), lampMat, SurfaceType.Glass, false);
                Breaker b = root.AddComponent<Breaker>();
                b.circuit = id;
                b.handle = pivot.transform;
                b.lamp = lamp.GetComponent<Renderer>();
                EditorBuild.Label(root.transform, PowerGrid.Label(id).Replace(" ", "\n"), new Vector3(0f, -0.3f, -0.03f), 0f, 0.014f);
                x += id == PowerGrid.Main ? 1.3f : 0.9f;
            }
            PointLight(g, new Vector3(-93.75f, y + 2.9f, -26.4f), 5f, 0.35f, FluorescentWhite, true);
            return g;
        }

        static void BuildPowerZones(Transform parent)
        {
            MachineMaterials();
            Transform g = Group("Power zones", parent);
            int zoneLayer = GameLayers.TriggerZoneLayer >= 0 ? GameLayers.TriggerZoneLayer : 2;
            var byCircuit = new Dictionary<string, PowerZone>();
            PowerZone Zone(string id)
            {
                if (byCircuit.TryGetValue(id, out PowerZone z)) return z;
                var go = new GameObject("Power zone " + id);
                go.transform.SetParent(g, false);
                go.layer = zoneLayer;
                go.AddComponent<UnityEngine.Rendering.Volume>();
                z = go.AddComponent<PowerZone>();
                z.circuit = id;
                byCircuit[id] = z;
                return z;
            }
            void AddBox(PowerZone z, AreaRect r, float y0, float y1)
            {
                BoxCollider b = z.gameObject.AddComponent<BoxCollider>();
                b.isTrigger = true;
                b.center = new Vector3(r.Center.x, (y0 + y1) * 0.5f, r.Center.y);
                b.size = new Vector3(r.Width, y1 - y0, r.Depth);
            }
            foreach (var a in PowerGrid.Areas) AddBox(Zone(a.id), a.rect, PowerGrid.AreaBottom, PowerGrid.AreaTop);
            AddBox(Zone("carpark"), PowerGrid.CarParkArea, PowerGrid.CarParkBottom, PowerGrid.AreaBottom);

            // Battery emergency lights: dim, warm, only on when their circuit is dead.
            var lights = new Dictionary<string, List<Light>>();
            void Emergency(Vector3 p)
            {
                string id = PowerGrid.CircuitAt(p);
                var go = new GameObject("Emergency light");
                go.transform.SetParent(g, false);
                go.transform.localPosition = p;
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 9f;
                l.intensity = 0.45f;
                l.color = new Color(1f, 0.9f, 0.75f);
                l.shadows = LightShadows.None;
                l.lightmapBakeType = LightmapBakeType.Realtime;
                l.enabled = false;
                Decor("Emergency light fitting", g, p + new Vector3(-0.18f, 0.12f, -0.06f), p + new Vector3(0.18f, 0.18f, 0.06f), emergencyMat);
                if (!lights.TryGetValue(id, out List<Light> list)) lights[id] = list = new List<Light>();
                list.Add(l);
            }
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                foreach (WingId w in MallLayout.Wings)
                {
                    foreach (float u in new[] { 28f, 60f, 92f }) Emergency(MallLayout.WingPoint(w, u, 0f, y + 3.6f));
                    Emergency(MallLayout.WingPoint(w, 50f, 17.5f, y + 3.4f));
                    Emergency(MallLayout.WingPoint(w, 80f, -17.5f, y + 3.4f));
                }
                Emergency(new Vector3(13f, y + 3.6f, 0f));
                Emergency(new Vector3(-13f, y + 3.6f, 0f));
            }
            for (int ix = -2; ix <= 2; ix++)
                for (int iz = -1; iz <= 1; iz++)
                    Emergency(new Vector3(ix * 38f, MallLayout.CarParkY + 2.8f, iz * 45f));
            foreach (var kv in byCircuit)
                kv.Value.emergencyLights = lights.TryGetValue(kv.Key, out List<Light> l) ? l.ToArray() : new Light[0];
        }

        static void BuildMalfunctions(Transform parent, Transform switchboard)
        {
            var go = new GameObject("Malfunctions");
            go.transform.SetParent(parent, false);
            MalfunctionDirector d = go.AddComponent<MalfunctionDirector>();
            d.switchboard = switchboard;
        }
    }
}
