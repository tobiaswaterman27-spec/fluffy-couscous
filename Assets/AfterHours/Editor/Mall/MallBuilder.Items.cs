using System.Collections.Generic;
using UnityEngine;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    public static partial class MallBuilder
    {
        /// <summary>
        /// Part 6: places items where they would plausibly be (keys and cards in offices, tools in maintenance,
        /// fuses in electrical rooms, junk lying about). Part 7 ties them into the escape-route puzzle chains.
        /// Spots are chosen in areas kept clear of furniture, so items never start inside anything.
        /// </summary>
        static void BuildItems(Transform parent)
        {
            ItemFactory.Reset();
            var rng = new System.Random(4242);

            // ---- back-of-house rooms: items set down near the room's inner doorway (always kept clear) ----
            void Room(MallFloor f, int sx, int sz, bool roomA, params string[] ids)
            {
                float y = MallLayout.FloorY(f);
                float zAbs = roomA ? MallLayout.QuadrantDivider - 2f : MallLayout.QuadrantDivider + 2f;
                for (int i = 0; i < ids.Length; i++)
                {
                    float xAbs = 39.0f + (i % 3) * 0.6f;
                    float zz = zAbs + (roomA ? -1f : 1f) * (i / 3) * 0.5f;
                    ItemFactory.Create(ids[i], parent, new Vector3(sx * xAbs, y + 0.01f, sz * zz), (float)rng.NextDouble() * 360f);
                }
            }

            Room(MallFloor.Ground, -1, 1, true, "card_security", "key_loading", "dock_remote", "control_switch");   // Security office
            Room(MallFloor.Ground, -1, 1, false, "key_lockers");                                                    // Staff room & lockers
            Room(MallFloor.Ground, -1, -1, true, "fuse", "power_connector", "fuse");                                 // Electrical room
            Room(MallFloor.Ground, -1, -1, false, "card_maintenance", "pliers", "spanner", "replacement_switch");    // Maintenance workshop
            Room(MallFloor.Ground, 1, 1, true, "pry_bar", "door_actuator");                                          // Loading bay
            Room(MallFloor.Ground, 1, 1, false, "machine_part", "metal_can");                                        // Goods in
            Room(MallFloor.Level1, 1, 1, true, "mech_handle", "gear");                                               // Plant room
            Room(MallFloor.Level1, 1, -1, true, "key_security", "security_token", "lift_key", "key_exit");          // Mall management
            Room(MallFloor.Level1, 1, -1, false, "card_restricted");                                                 // Records office
            Room(MallFloor.Level1, -1, -1, false, "key_plantroom", "wire_cutters");                                  // Maintenance L1
            Room(MallFloor.Level2, -1, 1, false, "key_maintenance", "old_phone");                                    // Staff canteen
            Room(MallFloor.Level2, -1, -1, true, "fuse", "fuse");                                                    // Switch room
            Room(MallFloor.Level2, -1, -1, false, "valve_wheel");                                                    // Water tanks
            Room(MallFloor.Level2, 1, 1, false, "key_roof", "card_special");                                         // Roof access plant

            // Main entrance key: left inside one of the staff lockers.
            {
                float zAbs = MallLayout.QuadrantDivider + 0.1f + 0.31f;
                ItemFactory.Create("key_main_doors", parent, new Vector3(-(26f + 0.64f * 2f), 0.14f, zAbs), 30f);
            }

            // ---- shops: on counters and in stock rooms ----
            var shopItems = new Queue<string>(new[]
            {
                "card_staff", "screwdriver", "utility_knife", "alarm_clock", "circuit_module", "key_storage",
                "coin", "battery", "metal_can", "glass_bottle", "coin", "screwdriver"
            });
            foreach (MallUnit u in MallLayout.Units)
            {
                if (shopItems.Count == 0) break;
                if (u.isAnchor || !u.Enterable) continue;
                if (MallLayout.Hash((int)u.floor, (int)u.wing, u.side, (int)u.u0) % 3 != 0) continue;
                float y = MallLayout.FloorY(u.floor);
                string id = shopItems.Dequeue();
                bool onCounter = MallLayout.Hash((int)u.u0, 7, 7, 7) % 2 == 0;
                Vector3 p = onCounter
                    ? MallLayout.WingPoint(u.wing, u.u0 + 1.4f, u.side * 11.6f, y + 1.06f)
                    : MallLayout.WingPoint(u.wing, u.u1 - 0.9f, u.side * 14.6f, y + 0.01f);
                ItemFactory.Create(id, parent, p, (float)rng.NextDouble() * 360f);
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

            // ---- car park ----
            float cp = MallLayout.CarParkY;
            ItemFactory.Create("key_tunnel", parent, new Vector3(-88.5f, cp + 0.01f, -32.2f), 0f);       // plant room, by the door
            ItemFactory.Create("fuse", parent, new Vector3(-88.5f, cp + 0.01f, -31.2f), 40f);
            ItemFactory.Create("adjustable_wrench", parent, new Vector3(-88.4f, cp + 0.01f, -44.6f), 0f); // maintenance store
            ItemFactory.Create("battery", parent, new Vector3(72.5f, cp + 0.01f, 84f), 0f);               // delivery bay
            ItemFactory.Create("glass_bottle", parent, new Vector3(12f, cp + 0.01f, -38f), 0f);
            ItemFactory.Create("glass_bottle", parent, new Vector3(-30f, cp + 0.01f, 55f), 0f);
            ItemFactory.Create("metal_can", parent, new Vector3(40f, cp + 0.01f, 12f), 0f);
            ItemFactory.Create("metal_can", parent, new Vector3(-60f, cp + 0.01f, -70f), 0f);
            ItemFactory.Create("coin", parent, new Vector3(5f, cp + 0.01f, -12f), 0f);
        }
    }
}
