using System;
using System.Collections.Generic;
using UnityEngine;
using AfterHours.World.Layout;

namespace AfterHours.Power
{
    /// <summary>
    /// The mall's electricity. A main incomer feeds circuits; each circuit has a breaker on the main switchboard
    /// (car park plant room). Lose a circuit and its lights go out (emergency lighting only), escalators stop,
    /// lifts stick between floors. Breakers trip on their own sometimes (Part 12 malfunctions); you have to go
    /// down to the plant room and reset them... which is where Elliot lives.
    /// </summary>
    public static class PowerGrid
    {
        public const string Main = "main";
        public static readonly string[] Circuits = { Main, "north", "east", "south", "west", "atrium", "carpark", "lifts", "escalators" };

        static readonly Dictionary<string, bool> breakers = new Dictionary<string, bool>();
        static readonly HashSet<string> faults = new HashSet<string>();

        /// <summary>Raised with the circuit id whenever any breaker changes.</summary>
        public static event Action<string> Changed;

        public static string Label(string id)
        {
            switch (id)
            {
                case Main: return "MAIN INCOMER";
                case "carpark": return "CAR PARK";
                default: return id.ToUpperInvariant() + (id == "lifts" || id == "escalators" ? "" : " WING");
            }
        }

        public static bool BreakerOn(string id) => string.IsNullOrEmpty(id) || !breakers.TryGetValue(id, out bool on) || on;

        public static bool IsPowered(string circuit) => BreakerOn(Main) && BreakerOn(circuit) && !faults.Contains(circuit ?? "");

        /// <summary>A section blackout: the supply to this circuit fails for a while (no breaker to reset - it comes back by itself).</summary>
        public static bool HasFault(string id) => !string.IsNullOrEmpty(id) && faults.Contains(id);

        public static void SetFault(string id, bool fault)
        {
            if (string.IsNullOrEmpty(id) || HasFault(id) == fault) return;
            if (fault) faults.Add(id); else faults.Remove(id);
            Changed?.Invoke(id);
        }

        public static IEnumerable<string> Faults => faults;

        public static void SetBreaker(string id, bool on)
        {
            if (BreakerOn(id) == on) return;
            breakers[id] = on;
            Changed?.Invoke(id);
        }

        /// <summary>Everything back on (run reset).</summary>
        public static void RestoreAll()
        {
            var off = new List<string>();
            foreach (var kv in breakers) if (!kv.Value) off.Add(kv.Key);
            off.AddRange(faults);
            breakers.Clear();
            faults.Clear();
            foreach (string id in off) Changed?.Invoke(id);
        }

        /// <summary>The area each lighting circuit covers (plan rectangles, from just below the ground floor to the roof).</summary>
        public static readonly (string id, AreaRect rect)[] Areas =
        {
            ("atrium", new AreaRect(-16f, -16f, 16f, 16f)),
            ("north", new AreaRect(-20.5f, 16f, 20.5f, 127f)),
            ("north", new AreaRect(19.3f, 19.3f, 45.5f, 45.5f)),
            ("east", new AreaRect(16f, -20.5f, 127f, 20.5f)),
            ("east", new AreaRect(19.3f, -45.5f, 45.5f, -19.3f)),
            ("south", new AreaRect(-20.5f, -127f, 20.5f, -16f)),
            ("south", new AreaRect(-45.5f, -45.5f, -19.3f, -19.3f)),
            ("west", new AreaRect(-127f, -20.5f, -16f, 20.5f)),
            ("west", new AreaRect(-45.5f, 19.3f, -19.3f, 45.5f)),
        };
        public const float AreaBottom = -0.6f, AreaTop = 19f;
        public static readonly AreaRect CarParkArea = new AreaRect(-150f, -105f, 105f, 105f);
        public const float CarParkBottom = -4.6f;

        /// <summary>Which lighting circuit feeds a point in the building.</summary>
        public static string CircuitAt(Vector3 p)
        {
            if (p.y < AreaBottom) return "carpark";
            foreach (var a in Areas) if (a.rect.Contains(p.x, p.z)) return a.id;
            if (Mathf.Abs(p.x) < MallLayout.AtriumHalf && Mathf.Abs(p.z) < MallLayout.AtriumHalf) return "atrium";
            if (Mathf.Abs(p.z) >= Mathf.Abs(p.x)) return p.z > 0f ? "north" : "south";
            return p.x > 0f ? "east" : "west";
        }

        /// <summary>1 = normal, lower in areas whose lights are off (used by Elliot's eyes and the player's).</summary>
        public static float LightFactorAt(Vector3 p) => IsPowered(CircuitAt(p)) ? 1f : 0.2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            breakers.Clear();
            faults.Clear();
            Changed = null;
        }
    }
}
