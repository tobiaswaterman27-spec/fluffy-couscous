using System.Collections.Generic;
using UnityEngine;
using AfterHours.World;

namespace AfterHours.Items
{
    public enum ItemCategory { Key, Card, Tool, Electrical, Puzzle, Progression, Distraction }

    public enum ItemShape { Key, Card, Screwdriver, Pliers, Spanner, Wrench, PryBar, Knife, Cutters, Fuse, Module, Connector, Battery,
        Handle, Gear, Switch, Token, Dial, Coin, Can, Bottle, AlarmClock, Phone, Valve, Remote, Box, Jerrycan, Axe }

    /// <summary>What an item is. Items are identified by id; 'provides' lists the lock / tool ids it satisfies.</summary>
    public class ItemDefinition
    {
        public string id;
        public string displayName;
        public ItemCategory category;
        public ItemShape shape;
        public string[] provides;
        public float mass = 0.3f;
        /// <summary>Hard uses before it breaks (0 = never).</summary>
        public int durability;
        public SurfaceType material = SurfaceType.Metal;
        public Color tint = new Color(0.7f, 0.7f, 0.72f);
        public bool Provides(string lockId)
        {
            if (string.IsNullOrEmpty(lockId)) return false;
            if (lockId == id) return true;
            if (provides != null) foreach (string p in provides) if (p == lockId) return true;
            return false;
        }
    }

    /// <summary>Every item type in the game. Keys and cards never substitute for each other.</summary>
    public static class ItemCatalog
    {
        static Dictionary<string, ItemDefinition> items;

        public static IEnumerable<ItemDefinition> All { get { Ensure(); return items.Values; } }

        public static ItemDefinition Get(string id)
        {
            Ensure();
            return id != null && items.TryGetValue(id, out ItemDefinition d) ? d : null;
        }

        static void Add(string id, string name, ItemCategory cat, ItemShape shape, float mass, SurfaceType mat, Color tint, params string[] provides)
        {
            items[id] = new ItemDefinition { id = id, displayName = name, category = cat, shape = shape, mass = mass, material = mat, tint = tint, provides = provides };
        }

        static void Ensure()
        {
            if (items != null) return;
            items = new Dictionary<string, ItemDefinition>();
            Color brass = new Color(0.78f, 0.64f, 0.3f), steel = new Color(0.68f, 0.69f, 0.72f), red = new Color(0.7f, 0.12f, 0.1f),
                  blue = new Color(0.2f, 0.35f, 0.7f), green = new Color(0.15f, 0.45f, 0.25f), white = new Color(0.9f, 0.9f, 0.88f),
                  yellow = new Color(0.85f, 0.7f, 0.15f), black = new Color(0.08f, 0.08f, 0.09f);

            // Keys (each opens one kind of lock only)
            Add("key_shop", "Shop master key", ItemCategory.Key, ItemShape.Key, 0.05f, SurfaceType.Metal, brass);
            Add("key_storage", "Storage key", ItemCategory.Key, ItemShape.Key, 0.05f, SurfaceType.Metal, steel);
            Add("key_maintenance", "Maintenance key", ItemCategory.Key, ItemShape.Key, 0.05f, SurfaceType.Metal, yellow);
            Add("key_security", "Security key", ItemCategory.Key, ItemShape.Key, 0.05f, SurfaceType.Metal, blue);
            Add("key_plantroom", "Plant room key", ItemCategory.Key, ItemShape.Key, 0.05f, SurfaceType.Metal, green);
            Add("key_lockers", "Locker key", ItemCategory.Key, ItemShape.Key, 0.04f, SurfaceType.Metal, steel);
            Add("key_stairwell", "Stairwell key", ItemCategory.Key, ItemShape.Key, 0.05f, SurfaceType.Metal, red);
            Add("key_exit", "Fire exit key", ItemCategory.Key, ItemShape.Key, 0.06f, SurfaceType.Metal, red);
            Add("key_roof", "Roof hatch key", ItemCategory.Key, ItemShape.Key, 0.06f, SurfaceType.Metal, black);
            Add("key_tunnel", "Service tunnel key", ItemCategory.Key, ItemShape.Key, 0.07f, SurfaceType.Metal, new Color(0.45f, 0.3f, 0.2f));
            Add("key_loading", "Loading bay key", ItemCategory.Key, ItemShape.Key, 0.06f, SurfaceType.Metal, new Color(0.6f, 0.4f, 0.15f));
            Add("key_main_doors", "Main entrance key", ItemCategory.Key, ItemShape.Key, 0.07f, SurfaceType.Metal, brass);

            // Access cards
            Add("card_staff", "Staff access card", ItemCategory.Card, ItemShape.Card, 0.01f, SurfaceType.Wood, white);
            Add("card_security", "Security card", ItemCategory.Card, ItemShape.Card, 0.01f, SurfaceType.Wood, blue);
            Add("card_maintenance", "Maintenance card", ItemCategory.Card, ItemShape.Card, 0.01f, SurfaceType.Wood, yellow);
            Add("card_restricted", "Restricted area card", ItemCategory.Card, ItemShape.Card, 0.01f, SurfaceType.Wood, red);
            Add("card_special", "Special access card", ItemCategory.Card, ItemShape.Card, 0.01f, SurfaceType.Wood, black);

            // Tools
            Add("screwdriver", "Screwdriver", ItemCategory.Tool, ItemShape.Screwdriver, 0.15f, SurfaceType.Metal, yellow);
            Add("pliers", "Pliers", ItemCategory.Tool, ItemShape.Pliers, 0.25f, SurfaceType.Metal, red);
            Add("spanner", "Spanner", ItemCategory.Tool, ItemShape.Spanner, 0.3f, SurfaceType.Metal, steel);
            Add("adjustable_wrench", "Adjustable wrench", ItemCategory.Tool, ItemShape.Wrench, 0.45f, SurfaceType.Metal, steel, "spanner");
            Add("pry_bar", "Small pry bar", ItemCategory.Tool, ItemShape.PryBar, 0.8f, SurfaceType.Metal, new Color(0.3f, 0.3f, 0.32f), "pry");
            Add("utility_knife", "Utility knife", ItemCategory.Tool, ItemShape.Knife, 0.1f, SurfaceType.Metal, yellow, "cut_light");
            Add("wire_cutters", "Wire cutters", ItemCategory.Tool, ItemShape.Cutters, 0.3f, SurfaceType.Metal, blue, "cut_wire");

            // Electrical
            Add("fuse", "Fuse", ItemCategory.Electrical, ItemShape.Fuse, 0.05f, SurfaceType.Glass, white);
            Add("replacement_switch", "Replacement switch", ItemCategory.Electrical, ItemShape.Switch, 0.1f, SurfaceType.Wood, black);
            Add("circuit_module", "Circuit module", ItemCategory.Electrical, ItemShape.Module, 0.2f, SurfaceType.Wood, green);
            Add("power_connector", "Power connector", ItemCategory.Electrical, ItemShape.Connector, 0.2f, SurfaceType.Wood, black);
            Add("battery", "Spare battery", ItemCategory.Electrical, ItemShape.Battery, 0.5f, SurfaceType.Metal, new Color(0.15f, 0.15f, 0.18f));

            // Puzzle components
            Add("mech_handle", "Mechanical handle", ItemCategory.Puzzle, ItemShape.Handle, 0.9f, SurfaceType.Metal, red);
            Add("machine_part", "Machine part", ItemCategory.Puzzle, ItemShape.Box, 0.7f, SurfaceType.Metal, steel);
            Add("gear", "Replacement gear", ItemCategory.Puzzle, ItemShape.Gear, 0.6f, SurfaceType.Metal, steel);
            Add("control_switch", "Control switch", ItemCategory.Puzzle, ItemShape.Switch, 0.2f, SurfaceType.Metal, yellow);
            Add("security_token", "Security token", ItemCategory.Puzzle, ItemShape.Token, 0.03f, SurfaceType.Metal, blue);
            Add("lock_dial", "Combination dial", ItemCategory.Puzzle, ItemShape.Dial, 0.2f, SurfaceType.Metal, black);

            // Special progression items
            Add("lift_key", "Lift control key", ItemCategory.Progression, ItemShape.Key, 0.08f, SurfaceType.Metal, black, "lift_control");
            Add("dock_remote", "Loading bay remote", ItemCategory.Progression, ItemShape.Remote, 0.15f, SurfaceType.Wood, black, "dock_control");
            Add("valve_wheel", "Valve wheel", ItemCategory.Progression, ItemShape.Valve, 1.6f, SurfaceType.Metal, red, "tunnel_valve");
            Add("fuel_can", "Jerry can (petrol)", ItemCategory.Progression, ItemShape.Jerrycan, 4.5f, SurfaceType.Metal, new Color(0.55f, 0.08f, 0.06f), "fuel");
            Add("fire_axe", "Fire axe", ItemCategory.Tool, ItemShape.Axe, 2.4f, SurfaceType.Metal, new Color(0.7f, 0.07f, 0.05f), "axe", "pry");
            items["fire_axe"].durability = 3;
            Add("door_actuator", "Door mechanism actuator", ItemCategory.Progression, ItemShape.Box, 1.4f, SurfaceType.Metal, yellow, "heavy_door_part");

            // Distractions
            Add("coin", "Coin", ItemCategory.Distraction, ItemShape.Coin, 0.01f, SurfaceType.Metal, brass);
            Add("metal_can", "Empty can", ItemCategory.Distraction, ItemShape.Can, 0.05f, SurfaceType.Metal, red);
            Add("glass_bottle", "Glass bottle", ItemCategory.Distraction, ItemShape.Bottle, 0.4f, SurfaceType.Glass, green);
            Add("alarm_clock", "Alarm clock", ItemCategory.Distraction, ItemShape.AlarmClock, 0.4f, SurfaceType.Metal, white);
            Add("old_phone", "Old mobile phone", ItemCategory.Distraction, ItemShape.Phone, 0.12f, SurfaceType.Wood, black);
        }
    }
}
