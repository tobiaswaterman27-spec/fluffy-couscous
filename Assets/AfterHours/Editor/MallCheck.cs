using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using AfterHours.Creature;
using AfterHours.Flow;
using AfterHours.Gore;
using AfterHours.Hiding;
using AfterHours.Interaction;
using AfterHours.Items;
using AfterHours.Machines;
using AfterHours.Net;
using AfterHours.Player;
using AfterHours.Power;
using AfterHours.Puzzles;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// "After Hours > 7 - Check Mall": an automatic inspection of the built mall scene. It counts and checks every
    /// system (lifts, escalators, power, vents, hiding places, doors, items, exits, the menu and ending, Elliot) and
    /// lists anything missing or broken, so problems are caught before playing.
    /// </summary>
    public static class MallCheck
    {
        [MenuItem("After Hours/7 - Check Mall", priority = 7)]
        public static int LastProblems;

        public static void Run()
        {
            var problems = new List<string>();
            var info = new StringBuilder();
            T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            void Need(bool ok, string what) { if (!ok) problems.Add(what); }

            // The game.
            Need(All<GameFlow>().Length == 1, "Game flow object missing (rebuild the mall)");
            Need(All<SaveSystem>().Length == 1, "Save system missing");
            GameFlow flow = All<GameFlow>().Length > 0 ? All<GameFlow>()[0] : null;
            if (flow != null)
            {
                Need(flow.menuCamera != null, "Main menu camera missing");
                Need(flow.endingSet != null && flow.endingSpawn != null, "Daylight ending set missing");
                Need(flow.figurePoints != null && flow.figurePoints.Length >= 2, "Ending figure points missing");
                Need(flow.daylightVolume != null, "Daylight post-processing missing");
            }
            PlayerController[] players = All<PlayerController>();
            Need(players.Length == 1, $"Expected 1 player in the scene, found {players.Length}");

            // Elliot.
            ElliotBrain[] brains = All<ElliotBrain>();
            Need(brains.Length == 1, $"Expected 1 Elliot, found {brains.Length}");
            foreach (ElliotBrain b in brains)
            {
                Need(b.motor != null && b.animator != null && b.senses != null && b.memory != null, "Elliot is missing a part");
                Need(b.GetComponent<GoreKit>() != null, "Elliot has no gore kit (blood materials)");
                if (b.animator != null) Need(b.animator.jaw != null && b.animator.head != null, "Elliot's head / jaw missing");
            }
            NavMeshDataLoader nav = Object.FindAnyObjectByType<NavMeshDataLoader>();
            Need(nav != null && nav.data != null, "Elliot's navigation map missing (After Hours > Rebuild AI Navigation)");

            // Machines and power.
            LiftController[] lifts = All<LiftController>();
            Need(lifts.Length == 3, $"Expected 3 lifts, found {lifts.Length}");
            foreach (LiftController l in lifts)
            {
                Need(l.car != null && l.carDoors != null, l.name + ": car or doors missing");
                Need(l.levelY != null && l.landingDoors != null && l.levelY.Length == l.landingDoors.Length && l.levelY.Length == 4, l.name + ": floors / landing doors don't match");
            }
            Escalator[] escalators = All<Escalator>();
            Need(escalators.Length == 4, $"Expected 4 escalators, found {escalators.Length}");
            foreach (Escalator e in escalators) Need(e.belt != null && e.steps != null && e.steps.Length > 0, e.name + ": belt or steps missing");
            Breaker[] breakers = All<Breaker>();
            Need(breakers.Length == PowerGrid.Circuits.Length, $"Switchboard has {breakers.Length} breakers, expected {PowerGrid.Circuits.Length}");
            PowerZone[] zones = All<PowerZone>();
            Need(zones.Length == 6, $"Expected 6 lighting areas, found {zones.Length}");
            Need(All<MalfunctionDirector>().Length == 1, "Malfunction director missing");

            // Vents and hiding.
            VentDuct[] ducts = All<VentDuct>();
            Need(ducts.Length == 24, $"Expected 24 vent ducts, found {ducts.Length}");
            VentCover[] covers = All<VentCover>();
            Need(covers.Length >= 96, $"Expected at least 96 vent grilles, found {covers.Length}");
            HidingSpot[] spots = All<HidingSpot>();
            Need(spots.Length > 0, "No hiding places");
            foreach (HidingSpot s in spots) Need(s.doors != null && s.doors.Length > 0 && s.doors[0] != null, s.transform.parent != null ? s.transform.parent.name + ": hiding place has no door" : "hiding place has no door");

            // Doors, items, exits.
            Door[] doors = All<Door>();
            foreach (Door d in doors) Need(d.leaf != null, d.name + ": door without a leaf");
            ItemPickup[] items = All<ItemPickup>();
            var ids = new HashSet<string>();
            foreach (ItemPickup i in items)
            {
                Need(ItemCatalog.Get(i.itemId) != null, $"Item '{i.itemId}' isn't in the catalogue");
                Need(!string.IsNullOrEmpty(i.instanceId) && ids.Add(i.instanceId), $"Item {i.name} has a missing or repeated id");
            }
            bool axe = false;
            foreach (ItemPickup i in items) if (i.itemId == "fire_axe") axe = true;
            Need(axe, "No fire axe (no stairwell wall had room for its cabinet)");
            Need(All<AfterHours.Puzzles.Generator>().Length == 1, "The standby generator is missing");
            Need(All<AfterHours.Items.ItemSpot>().Length > 40, "Too few item spots for the run shuffle");
            EscapeTrigger[] exits = All<EscapeTrigger>();
            var routes = new HashSet<EscapeRouteId>();
            foreach (EscapeTrigger t in exits) routes.Add(t.route);
            Need(routes.Count == 8, $"Expected exits for all 8 routes, found {routes.Count}");
            PuzzleGate[] gates = All<PuzzleGate>();
            foreach (PuzzleGate g in gates)
            {
                Need(g.inputs.Count > 0 && g.outputs.Count > 0, g.name + ": puzzle with no inputs or outputs");
                foreach (MonoBehaviour m in g.inputs) Need(m is IPuzzleInput, g.name + ": an input isn't a puzzle input");
            }

            // Co-op: every machine must name things the same way.
            var w = new NetWorld();
            w.Build();

            info.AppendLine($"Lifts {lifts.Length} · escalators {escalators.Length} · breakers {breakers.Length} · lighting areas {zones.Length}");
            info.AppendLine($"Vent ducts {ducts.Length} · grilles {covers.Length} · hiding places {spots.Length}");
            info.AppendLine($"Doors {doors.Length} · items {items.Length} · puzzles {gates.Length} · exits {exits.Length}");

            string summary = problems.Count == 0 ? "Everything checks out.\n\n" + info : $"{problems.Count} problem(s):\n- " + string.Join("\n- ", problems.GetRange(0, Mathf.Min(15, problems.Count))) + (problems.Count > 15 ? "\n(more in the Console)" : "") + "\n\n" + info;
            foreach (string p in problems) Debug.LogWarning("[AfterHours check] " + p);
            Debug.Log("[AfterHours check] " + info);
            LastProblems = problems.Count;
            if (!OneClickBuild.Quiet) EditorUtility.DisplayDialog("After Hours - mall check", summary, "OK");
        }
    }
}
