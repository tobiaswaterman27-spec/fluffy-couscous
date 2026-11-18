using System.Collections.Generic;
using UnityEngine;
using AfterHours.Core;
using AfterHours.World;
using AfterHours.Items;
using AfterHours.Puzzles;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    public static partial class MallBuilder
    {
        /// <summary>Stairwell walls with room for a fire axe cabinet (recorded while dressing the walls).</summary>
        static readonly List<(Vector3 foot, Vector3 n, Vector3 dir)> AxeWalls = new List<(Vector3, Vector3, Vector3)>();

        /// <summary>
        /// Part 6: places items where they would plausibly be (keys and cards in offices, tools in maintenance,
        /// fuses in electrical rooms, junk lying about). Part 7 ties them into the escape-route puzzle chains.
        /// Spots are chosen in areas kept clear of furniture, so items never start inside anything.
        ///
        /// Every important item also gets a shuffle group: the lock you need to get where it lies. Every room behind
        /// that lock offers several spots, and each run (ItemShuffle) the group's items are dealt out over all of
        /// them - so the same key can be in any room that needs the same card. Progression can never break: an item
        /// only ever moves between rooms with the same lock, and none of them holds its own key.
        /// </summary>
        static void BuildItems(Transform parent)
        {
            ItemFactory.Reset();
            var rng = new System.Random(4242);
            Transform spotsGroup = Group("Item spots (shuffled each run)", parent);

            void Spot(string group, Vector3 p, Quaternion? fixedRot = null)
            {
                var go = new GameObject("Spot (" + group + ")");
                go.transform.SetParent(spotsGroup, false);
                go.transform.position = p;
                if (fixedRot.HasValue) go.transform.rotation = fixedRot.Value;
                ItemSpot s = go.AddComponent<ItemSpot>();
                s.group = group;
                s.fixedRotation = fixedRot.HasValue;
            }

            ItemPickup Make(string id, string group, Vector3 p, float yaw)
            {
                ItemPickup i = ItemFactory.Create(id, parent, p, yaw);
                if (i == null) return null;
                ItemDefinition d = ItemCatalog.Get(id);
                bool important = d != null && d.category != ItemCategory.Distraction;
                if (important && !string.IsNullOrEmpty(group))
                {
                    i.shuffleGroup = group;
                    Spot(group, p); // where it is in the editor is one of its possible spots too
                }
                return i;
            }

            // ---- back-of-house rooms: items set down near the room's inner doorway (always kept clear) ----
            Vector3 RoomPoint(MallFloor f, int sx, int sz, bool roomA, int i)
            {
                float y = MallLayout.FloorY(f);
                float zAbs = roomA ? MallLayout.QuadrantDivider - 2f : MallLayout.QuadrantDivider + 2f;
                float xAbs = 39.0f + (i % 3) * 0.6f;
                float zz = zAbs + (roomA ? -1f : 1f) * (i / 3) * 0.5f;
                return new Vector3(sx * xAbs, y + 0.01f, sz * zz);
            }
            string RoomLock(MallFloor f, int sx, int sz, bool roomA) =>
                (roomA ? MallLayout.QuadrantRoomA(f, sx, sz) : MallLayout.QuadrantRoomB(f, sx, sz)).lockId;

            void Room(MallFloor f, int sx, int sz, bool roomA, params string[] ids)
            {
                string group = RoomLock(f, sx, sz, roomA);
                for (int i = 0; i < ids.Length; i++)
                    Make(ids[i], group, RoomPoint(f, sx, sz, roomA, i), (float)rng.NextDouble() * 360f);
                // The rest of the clear patch: places something else might turn up on another run.
                for (int i = ids.Length; i < 6; i++) Spot(group, RoomPoint(f, sx, sz, roomA, i));
            }

            Room(MallFloor.Ground, -1, 1, true, "card_security", "key_loading", "dock_remote", "control_switch");   // Security office
            Room(MallFloor.Ground, -1, 1, false, "key_lockers");                                                    // Staff room & lockers
            Room(MallFloor.Ground, -1, -1, true, "fuse", "power_connector");                                         // Electrical room
            Room(MallFloor.Ground, -1, -1, false, "card_maintenance", "pliers", "spanner", "replacement_switch");    // Maintenance workshop
            Room(MallFloor.Ground, 1, 1, true, "pry_bar", "door_actuator");                                          // Loading bay
            Room(MallFloor.Ground, 1, 1, false, "machine_part", "metal_can");                                        // Goods in
            Room(MallFloor.Ground, 1, -1, false);                                                                    // Staff exit lobby
            Room(MallFloor.Level1, 1, 1, true, "mech_handle", "gear");                                               // Plant room
            Room(MallFloor.Level1, 1, -1, true, "key_security", "security_token", "lift_key", "key_exit", "key_shop");          // Mall management
            Room(MallFloor.Level1, 1, -1, false, "lock_dial");                                                       // Records office
            Room(MallFloor.Level1, -1, -1, false, "key_plantroom", "wire_cutters", "card_restricted");               // Maintenance L1
            Room(MallFloor.Level2, -1, 1, false, "key_maintenance", "old_phone");                                    // Staff canteen
            Room(MallFloor.Level2, -1, -1, true, "fuse");                                                            // Switch room
            Room(MallFloor.Level2, -1, -1, false, "valve_wheel");                                                    // Water tanks
            Room(MallFloor.Level2, 1, 1, true);                                                                      // Air handling plant
            Room(MallFloor.Level2, 1, 1, false, "key_roof", "card_special");                                         // Roof access plant

            // Main entrance key: left inside one of the staff lockers.
            {
                float zAbs = MallLayout.QuadrantDivider + 0.1f + 0.31f;
                ItemFactory.Create("key_main_doors", parent, new Vector3(-(26f + 0.64f * 2f), 0.14f, zAbs), 30f);
            }

            // ---- shops: on counters and in stock rooms (every open shop is a possible spot) ----
            var shopItems = new Queue<string>(new[]
            {
                "card_staff", "screwdriver", "utility_knife", "alarm_clock", "circuit_module", "key_storage",
                "coin", "battery", "metal_can", "glass_bottle", "coin", "screwdriver"
            });
            foreach (MallUnit u in MallLayout.Units)
            {
                if (u.isAnchor || !u.Enterable) continue;
                float y = MallLayout.FloorY(u.floor);
                bool onCounter = MallLayout.Hash((int)u.u0, 7, 7, 7) % 2 == 0;
                Vector3 p = onCounter
                    ? MallLayout.WingPoint(u.wing, u.u0 + 1.4f, u.side * 11.6f, y + 1.06f)
                    : MallLayout.WingPoint(u.wing, u.u1 - 0.9f, u.side * 14.6f, y + 0.01f);
                if (shopItems.Count > 0 && MallLayout.Hash((int)u.floor, (int)u.wing, u.side, (int)u.u0) % 3 == 0)
                {
                    string id = shopItems.Dequeue();
                    ItemPickup it = Make(id, "", p, (float)rng.NextDouble() * 360f);
                    if (it != null && ItemCatalog.Get(id).category != ItemCategory.Distraction) it.shuffleGroup = "open";
                }
                Spot("open", p);
            }

            // ---- food court kiosk counters ----
            {
                float y = MallLayout.FloorY(MallFloor.Level2);
                float end = MallLayout.AnchorEnd;
                float width = (2f * MallLayout.OuterWall - 1f) / 6f;
                string[] kioskItems = { "key_storage", "glass_bottle", "coin", "metal_can", "glass_bottle", "coin" };
                for (int i = 0; i < 6; i++)
                {
                    float v = -MallLayout.OuterWall + 0.5f + (i + 0.5f) * width;
                    ItemFactory.Create(kioskItems[i], parent, MallLayout.WingPoint(WingId.West, end - 0.75f, v, y + 0.96f), i * 47f);
                }
            }

            // ---- loose junk in public areas ----
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f + 10f;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.01f, 4.9f);
                if (i % 2 == 0) ItemFactory.Create("coin", parent, p, a);
            }
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                foreach (WingId w in MallLayout.Wings)
                {
                    if (MallLayout.Hash((int)f, (int)w, 3, 1) % 2 == 0) continue;
                    ItemFactory.Create("metal_can", parent, MallLayout.WingPoint(w, 37f, -2.7f, y + 0.01f), 15f);
                }
            }

            // ---- car park: plant room (generator, fuse box) and maintenance store, both on the maintenance key ----
            float cp = MallLayout.CarParkY;
            Make("key_tunnel", "key_maintenance", new Vector3(-88.5f, cp + 0.01f, -32.2f), 0f);      // plant room, by the door
            Make("fuse", "key_maintenance", new Vector3(-88.5f, cp + 0.01f, -31.2f), 40f);
            Make("adjustable_wrench", "key_maintenance", new Vector3(-88.4f, cp + 0.01f, -44.6f), 0f); // maintenance store
            Make("fuel_can", "key_maintenance", new Vector3(-88.5f, cp + 0.01f, -45.6f), 80f);
            foreach (Vector3 p in new[]
            {
                new Vector3(-88.5f, cp, -30.5f), new Vector3(-94.8f, cp, -36.6f), new Vector3(-93.6f, cp, -36.6f),
                new Vector3(-88.4f, cp, -43.6f), new Vector3(-87.6f, cp, -45.2f), new Vector3(-87.6f, cp, -44.0f),
            }) Spot("key_maintenance", p + Vector3.up * 0.01f);

            ItemFactory.Create("battery", parent, new Vector3(72.5f, cp + 0.01f, 84f), 0f);               // delivery bay
            ItemFactory.Create("glass_bottle", parent, new Vector3(12f, cp + 0.01f, -38f), 0f);
            ItemFactory.Create("glass_bottle", parent, new Vector3(-30f, cp + 0.01f, 55f), 0f);
            ItemFactory.Create("metal_can", parent, new Vector3(40f, cp + 0.01f, 12f), 0f);
            ItemFactory.Create("metal_can", parent, new Vector3(-60f, cp + 0.01f, -70f), 0f);
            ItemFactory.Create("coin", parent, new Vector3(5f, cp + 0.01f, -12f), 0f);

            BuildAxeCabinets(parent, Spot, Make);
        }

        /// <summary>
        /// Fire axe cabinets on the stairwell fire points: red steel box, glass front, "FIRE AXE - BREAK GLASS".
        /// Several stairwells have one; only one cabinet holds the axe (which one changes every run). Breaking the
        /// glass is loud. The axe chops through wooden office doors and forces cages, but its handle splits after
        /// three hard jobs.
        /// </summary>
        static void BuildAxeCabinets(Transform parent, System.Action<string, Vector3, Quaternion?> spot,
                                     System.Func<string, string, Vector3, float, ItemPickup> make)
        {
            Transform g = Group("Fire axe cabinets", parent);
            var chosen = new List<(Vector3 foot, Vector3 n, Vector3 dir)>();
            foreach (var w in AxeWalls)
            {
                bool farEnough = true;
                foreach (var c in chosen) if (Vector3.Distance(c.foot, w.foot) < 25f) { farEnough = false; break; }
                if (farEnough) chosen.Add(w);
                if (chosen.Count >= 4) break;
            }
            if (chosen.Count == 0) { Debug.LogWarning("[AfterHours] No stairwell wall for a fire axe cabinet."); return; }

            Material red = ArtLibrary.Plain("AxeCabinet_Red", new Color(0.62f, 0.06f, 0.05f), 0.55f, 0.3f);
            bool placed = false;
            foreach (var w in chosen)
            {
                Quaternion facing = Quaternion.LookRotation(w.n, Vector3.up);
                Vector3 back = w.foot + Vector3.up * 1.15f;     // shelf height 1.0, cabinet 1.0 .. 1.4
                Vector3 c = back + w.n * 0.19f;
                const float W = 1.05f, H = 0.42f, D = 0.36f;
                Detail("Axe cabinet back", g, back + w.n * 0.01f, new Vector3(W, H, 0.02f), red, facing);
                Detail("Axe cabinet side", g, c + w.dir * (W * 0.5f - 0.01f), new Vector3(0.02f, H, D), red, facing);
                Detail("Axe cabinet side", g, c - w.dir * (W * 0.5f - 0.01f), new Vector3(0.02f, H, D), red, facing);
                Detail("Axe cabinet top", g, c + Vector3.up * (H * 0.5f - 0.01f), new Vector3(W, 0.02f, D), red, facing);
                EditorBuild.Box("Axe cabinet shelf", g, c - Vector3.up * (H * 0.5f - 0.01f), new Vector3(W, 0.02f, D), red, SurfaceType.Metal, facing);
                Sign(g, "FIRE AXE - BREAK GLASS", c + Vector3.up * (H * 0.5f + 0.08f), -w.n, 0.8f, 0.03f);

                GameObject glass = Blocker(g, "Axe cabinet glass", c + w.n * (D * 0.5f - 0.01f), new Vector3(W - 0.04f, H - 0.04f, 0.008f), glassMat);
                glass.transform.rotation = facing;
                var breaker = new GameObject("Axe cabinet (break glass)");
                breaker.transform.SetParent(g, false);
                breaker.transform.position = c + w.n * (D * 0.5f);
                breaker.transform.rotation = facing;
                BoxCollider bc = breaker.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(W, H, 0.06f);
                EditorBuild.SetLayerRecursive(breaker, GameLayers.InteractableLayer);
                ToolBreakable tb = breaker.AddComponent<ToolBreakable>();
                tb.toolId = "";
                tb.verb = "Break the glass";
                tb.seconds = 0.5f;
                tb.noiseRadius = 22f;
                tb.sound = AfterHours.Audio.ProceduralSounds.Sfx.GlassBreak;
                tb.blockers = new[] { glass };

                // The axe lies along the shelf, blade towards the glass.
                Vector3 shelfTop = c - Vector3.up * (H * 0.5f - 0.02f);
                Quaternion axeRot = Quaternion.LookRotation(w.n, Vector3.up);
                // Item local X is the handle: align it with the wall.
                axeRot = Quaternion.FromToRotation(axeRot * Vector3.right, w.dir) * axeRot;
                Vector3 axePos = shelfTop - w.dir * 0.38f - w.n * 0.02f;
                if (!placed)
                {
                    ItemPickup axe = make("fire_axe", "", axePos, 0f);
                    if (axe != null)
                    {
                        axe.transform.rotation = axeRot;
                        axe.shuffleGroup = "axe";
                    }
                    placed = true;
                }
                spot("axe", axePos, axeRot);
            }
        }
    }
}
