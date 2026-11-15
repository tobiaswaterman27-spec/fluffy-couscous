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
        // (location, lock needed to get in ("" = open), items found there)
        static readonly (string where, string lockId, string[] items)[] Locations =
        {
            ("Public areas / open shops", "", new[] { "card_staff", "screwdriver", "utility_knife", "alarm_clock", "circuit_module", "key_storage", "battery" }),
            ("Food court kiosks", "", new[] { "key_storage" }),
            ("Staff room & lockers (G NW B)", "card_staff", new[] { "key_lockers" }),
            ("Locked staff locker", "key_lockers", new[] { "key_main_doors" }),
            ("Maintenance workshop (G SW B)", "card_staff", new[] { "card_maintenance", "pliers", "spanner", "replacement_switch" }),
            ("Staff canteen (L2 NW B)", "card_staff", new[] { "key_maintenance", "old_phone" }),
            ("Electrical room (G SW A)", "card_maintenance", new[] { "fuse", "power_connector" }),
            ("Maintenance L1 (L1 SW B)", "card_maintenance", new[] { "key_plantroom", "wire_cutters", "card_restricted" }),
            ("Switch room (L2 SW A)", "card_maintenance", new[] { "fuse" }),
            ("Car park plant room", "key_maintenance", new[] { "key_tunnel", "fuse" }),
            ("Car park maintenance store", "key_maintenance", new[] { "adjustable_wrench" }),
            ("Plant room (L1 NE A)", "key_plantroom", new[] { "mech_handle", "gear" }),
            ("Water tanks (L2 SW B)", "key_plantroom", new[] { "valve_wheel" }),
            ("Mall management (L1 SE A)", "card_restricted", new[] { "key_security", "security_token", "lift_key", "key_exit", "key_shop" }),
            ("Records office (L1 SE B)", "card_restricted", new[] { "lock_dial" }),
            ("Roof access plant (L2 NE B)", "card_restricted", new[] { "key_roof", "card_special" }),
            ("Security office (G NW A)", "key_security", new[] { "card_security", "key_loading", "dock_remote", "control_switch" }),
            ("Loading bay (G NE A)", "key_loading", new[] { "pry_bar", "door_actuator" }),
            ("Goods in (G NE B)", "key_loading", new[] { "machine_part" }),
        };

        // (route, everything needed: access + parts)
        static readonly (string route, string[] needs)[] Routes =
        {
            ("1 Main entrance", new[] { "key_lockers", "key_main_doors", "key_security", "card_security" }),
            ("2 Car park exit", new[] { "key_maintenance", "fuse", "key_security", "control_switch" }),
            ("3 Loading bay", new[] { "key_loading", "circuit_module", "dock_remote" }),
            ("4 Emergency exit", new[] { "wire_cutters", "key_exit" }),
            ("5 Rooftop", new[] { "key_roof", "key_loading", "pry_bar" }),
            ("6 Service tunnel", new[] { "key_maintenance", "key_plantroom", "valve_wheel" }),
            ("7 Restricted staff exit", new[] { "card_restricted", "card_special", "door_actuator" }),
            ("8 Hidden", new[] { "screwdriver", "key_plantroom", "mech_handle" }),
        };

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
                foreach (string n in r.needs) if (!have.Contains(n)) missing.Add(n);
                if (missing.Count == 0) report.AppendLine("OK   route " + r.route);
                else { ok = false; report.AppendLine("FAIL route " + r.route + " missing: " + string.Join(", ", missing)); }
            }

            Debug.Log("[AfterHours] Progression order:\n" + order + "\n" + report);
            EditorUtility.DisplayDialog("After Hours - progression check",
                (ok ? "All eight escape routes can be completed. No dead ends.\n\n" : "PROBLEMS FOUND:\n\n") + report +
                "\n(Full order of play is in the Console.)", "OK");
        }
    }
}
