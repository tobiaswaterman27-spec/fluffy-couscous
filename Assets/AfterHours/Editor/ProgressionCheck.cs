using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Menu: After Hours > Check Progression.
    /// Simulates a player who starts with nothing: repeatedly collects every item they can reach and opens every
    /// lock those items fit, until nothing new opens. Then checks all eight escape routes can be completed and
    /// that no room holds its own key. Mirrors docs/PROGRESSION.md; keep both in sync with the item placement.
    /// </summary>
    public static class ProgressionCheck
    {
        // (location, lock needed to get in ("" = open), items found there). Items move between rooms that share a lock
        // each run (ItemShuffle), so a room's list here is "what this lock tier holds", which is what matters.
        static readonly (string where, string lockId, string[] items)[] Locations =
        {
            ("Public areas / open shops", "", new[] { "card_staff", "screwdriver", "utility_knife", "circuit_module", "key_storage", "battery" }),
            ("Stairwell fire point (break glass)", "", new[] { "fire_axe" }),
            ("Food court kiosks", "", new[] { "key_storage" }),
            ("Staff rooms (lockers / workshop / canteen)", "card_staff", new[] { "key_lockers", "card_maintenance", "pliers", "spanner", "replacement_switch", "key_maintenance" }),
            ("Locked staff locker", "key_lockers", new[] { "key_main_doors" }),
            ("Maintenance rooms (electrical / maintenance L1 / switch room)", "card_maintenance", new[] { "fuse", "power_connector", "key_plantroom", "wire_cutters", "card_restricted" }),
            ("Car park plant room & store", "key_maintenance", new[] { "key_tunnel", "fuse", "adjustable_wrench", "fuel_can" }),
            ("Plant rooms (plant room / water tanks / air handling)", "key_plantroom", new[] { "mech_handle", "gear", "valve_wheel" }),
            ("Restricted rooms (management / records / roof plant / staff exit lobby)", "card_restricted", new[] { "key_security", "security_token", "lift_key", "key_exit", "key_shop", "lock_dial", "key_roof", "card_special" }),
            ("Security office (G NW A)", "key_security", new[] { "card_security", "key_loading", "dock_remote", "control_switch" }),
            ("Loading bay & goods in", "key_loading", new[] { "pry_bar", "door_actuator", "machine_part" }),
        };

        // The standby generator (car park plant room): petrol, its control fuse, a pull-start.
        static readonly string[] Generator = { "key_maintenance", "fuel_can", "fuse" };

        // (route, everything needed). "a|b" = either will do. "GEN" = the generator must be running.
        static readonly (string route, string[] needs)[] Routes =
        {
            ("1 Main entrance", new[] { "key_lockers", "key_main_doors", "key_security", "card_security", "security_token", "GEN" }),
            ("2 Car park exit", new[] { "key_maintenance", "fuse", "key_security", "control_switch", "spanner|adjustable_wrench", "gear", "GEN" }),
            ("3 Loading bay", new[] { "key_loading", "circuit_module", "dock_remote", "power_connector", "GEN" }),
            ("4 Emergency exit", new[] { "wire_cutters|key_exit" }),
            ("5 Rooftop", new[] { "key_roof", "pry_bar|fire_axe", "spanner|adjustable_wrench" }),
            ("6 Service tunnel", new[] { "key_maintenance", "key_plantroom", "valve_wheel", "spanner|adjustable_wrench" }),
            ("7 Restricted staff exit", new[] { "card_restricted", "card_special", "door_actuator", "replacement_switch", "GEN" }),
            ("8 Hidden", new[] { "screwdriver", "key_plantroom", "mech_handle", "gear" }),
        };

        public static bool LastOk = true;

        [MenuItem("After Hours/Check Progression", priority = 50)]
        public static void Run()
        {
            var have = new HashSet<string>();
            var reached = new HashSet<string>();
            var order = new StringBuilder();
            int step = 0;
            bool changed = true;
            while (changed)
            {
                changed = false;
                step++;
                foreach (var loc in Locations)
                {
                    if (reached.Contains(loc.where)) continue;
                    if (loc.lockId != "" && !have.Contains(loc.lockId)) continue;
                    reached.Add(loc.where);
                    order.AppendLine($"Step {step}: {loc.where}  ->  {string.Join(", ", loc.items)}");
                    foreach (string i in loc.items) have.Add(i);
                    changed = true;
                }
            }

            var report = new StringBuilder();
            bool ok = true;
            foreach (var loc in Locations)
            {
                if (!reached.Contains(loc.where)) { ok = false; report.AppendLine("UNREACHABLE: " + loc.where); }
                foreach (string i in loc.items)
                {
                    if (i == loc.lockId) { ok = false; report.AppendLine($"SELF-LOCKED: {loc.where} holds its own key {i}"); }
                }
            }
            foreach (var r in Routes)
            {
                var missing = new List<string>();
                var needs = new List<string>();
                foreach (string n in r.needs) { if (n == "GEN") needs.AddRange(Generator); else needs.Add(n); }
                foreach (string n in needs)
                {
                    bool ok1 = false;
                    foreach (string alt in n.Split('|')) if (have.Contains(alt)) ok1 = true;
                    if (!ok1) missing.Add(n);
                }
                if (missing.Count == 0) report.AppendLine("OK   route " + r.route);
                else { ok = false; report.AppendLine("FAIL route " + r.route + " missing: " + string.Join(", ", missing)); }
            }

            // The built scene: every shuffled item's group must have enough spots, and no item may share its group's lock.
            var spots = new Dictionary<string, int>();
            foreach (AfterHours.Items.ItemSpot sp in Object.FindObjectsByType<AfterHours.Items.ItemSpot>(FindObjectsInactive.Include))
                spots[sp.group] = (spots.TryGetValue(sp.group, out int c) ? c : 0) + 1;
            var counts = new Dictionary<string, int>();
            foreach (AfterHours.Items.ItemPickup ip in Object.FindObjectsByType<AfterHours.Items.ItemPickup>(FindObjectsInactive.Include))
            {
                if (string.IsNullOrEmpty(ip.shuffleGroup)) continue;
                counts[ip.shuffleGroup] = (counts.TryGetValue(ip.shuffleGroup, out int c) ? c : 0) + 1;
                AfterHours.Items.ItemDefinition d = ip.Definition;
                if (ip.itemId == ip.shuffleGroup || (d != null && d.Provides(ip.shuffleGroup))) { ok = false; report.AppendLine("SELF-LOCKED (shuffle): " + ip.itemId + " in group " + ip.shuffleGroup); }
            }
            if (counts.Count == 0) report.AppendLine("(No mall in the open scene - shuffle spots not checked. Build the mall first.)");
            foreach (var kv in counts)
            {
                int have2 = spots.TryGetValue(kv.Key, out int c) ? c : 0;
                if (have2 < kv.Value) { ok = false; report.AppendLine($"TOO FEW SPOTS: group {kv.Key} has {kv.Value} items, {have2} spots"); }
                else report.AppendLine($"OK   shuffle group {kv.Key}: {kv.Value} items over {have2} spots");
            }

            Debug.Log("[AfterHours] Progression order:\n" + order + "\n" + report);
            LastOk = ok;
            if (!OneClickBuild.Quiet) EditorUtility.DisplayDialog("After Hours - progression check",
                (ok ? "All eight escape routes can be completed without the fire axe. No dead ends.\n\n" : "PROBLEMS FOUND:\n\n") + report +
                "\n(Full order of play is in the Console.)", "OK");
        }
    }
}
