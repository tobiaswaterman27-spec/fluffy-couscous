using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using AfterHours.Core;
using AfterHours.Interaction;
using AfterHours.Items;
using AfterHours.Player;
using AfterHours.Power;
using AfterHours.Puzzles;
using AfterHours.World;

namespace AfterHours.Flow
{
    /// <summary>The contents of a save file (JSON). 'version' lets later builds upgrade older saves.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public string savedAt;
        public float playTime;
        public Vector3 playerPosition;
        public float playerYaw;
        public string heldItem = "";
        public int runSeed;
        public float nightTime;
        public int deaths;
        public List<string> breakersOff = new List<string>();
        public List<Entry> entries = new List<Entry>();

        [Serializable]
        public class Entry
        {
            public string key;
            public string data;
        }
    }

    /// <summary>
    /// Saving and continuing (single-player). Saved: where you are and what you hold, every item, door, shutter,
    /// drawer, vent cover, switch, socket, card reader, cut chain, heavy object and breaker. Puzzles solve themselves
    /// again from their saved inputs, so everything they opened opens again. Elliot is not saved: when you continue,
    /// it is back in the car park and doesn't know where you are... yet.
    /// Writes are crash-safe (temp file + swap, previous kept as .bak). Dying deletes the save (the run resets).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }
        /// <summary>True while hosting co-op: the host's co-op game has its own save, separate from single player.</summary>
        public static bool CoopSlot;
        public static string SavePath => Path.Combine(Application.persistentDataPath, CoopSlot ? "coop_save.json" : "save.json");

        // Every saveable object gets a stable key from where it was when the scene loaded.
        readonly Dictionary<Component, string> keys = new Dictionary<Component, string>();
        readonly Dictionary<string, Component> byKey = new Dictionary<string, Component>();

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        void Awake()
        {
            Instance = this;
            Register<Door>();
            Register<RollerShutter>();
            Register<Drawer>();
            Register<VentCover>();
            Register<PhysicalSwitch>();
            Register<ItemSocket>();
            Register<CardReader>();
            Register<ToolBreakable>();
            Register<HeavyMovable>();
            {
                var stateful = new List<MonoBehaviour>();
                foreach (MonoBehaviour m in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (m is IStateful && !keys.ContainsKey(m)) stateful.Add(m);
                stateful.Sort((a, b) => string.CompareOrdinal(AfterHours.Net.NetWorld.HierarchyPath(a.transform), AfterHours.Net.NetWorld.HierarchyPath(b.transform)));
                foreach (MonoBehaviour m in stateful) Add(m, KeyFor(m));
            }
            foreach (ItemPickup p in FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Add(p, !string.IsNullOrEmpty(p.instanceId) ? "Item:" + p.instanceId : KeyFor(p));
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Register<T>() where T : Component
        {
            T[] found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(found, (a, b) => string.CompareOrdinal(AfterHours.Net.NetWorld.HierarchyPath(a.transform), AfterHours.Net.NetWorld.HierarchyPath(b.transform)));
            foreach (T c in found) Add(c, KeyFor(c));
        }

        void Add(Component c, string key)
        {
            string k = key;
            for (int i = 2; byKey.ContainsKey(k); i++) k = key + "#" + i;
            keys[c] = k;
            byKey[k] = c;
        }

        static string KeyFor(Component c)
        {
            Vector3 p = c.transform.position;
            return c.GetType().Name + "@" + Mathf.RoundToInt(p.x * 10f) + "," + Mathf.RoundToInt(p.y * 10f) + "," + Mathf.RoundToInt(p.z * 10f) + ":" + c.name;
        }

        public static bool HasSave => File.Exists(SavePath);

        public static void DeleteSave()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                if (File.Exists(SavePath + ".bak")) File.Delete(SavePath + ".bak");
            }
            catch (Exception e) { Debug.LogWarning("[AfterHours] Could not delete the save: " + e.Message); }
        }

        // ---------------- writing ----------------

        static string F(float v) => v.ToString("R", Inv);
        static string V(Vector3 v) => F(v.x) + " " + F(v.y) + " " + F(v.z);
        static string Q(Quaternion q) => F(q.x) + " " + F(q.y) + " " + F(q.z) + " " + F(q.w);

        public bool Save(PlayerController player, float playTime)
        {
            if (player == null) return false;
            var data = new SaveData
            {
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm", Inv),
                playTime = playTime,
                playerPosition = player.transform.position,
                playerYaw = player.transform.eulerAngles.y,
                runSeed = ItemShuffle.Seed,
                nightTime = AfterHours.Flow.NightDirector.Instance != null ? AfterHours.Flow.NightDirector.Instance.NightTime : 0f,
                deaths = AfterHours.Flow.GameFlow.Instance != null ? AfterHours.Flow.GameFlow.Instance.Deaths : 0,
            };
            PlayerInventory inv = player.GetComponent<PlayerInventory>();
            if (inv != null && inv.Held != null && keys.TryGetValue(inv.Held, out string heldKey)) data.heldItem = heldKey;
            foreach (string id in PowerGrid.Circuits) if (!PowerGrid.BreakerOn(id)) data.breakersOff.Add(id);

            foreach (var kv in keys)
            {
                if (kv.Key == null) continue;
                string s = Capture(kv.Key);
                if (s != null) data.entries.Add(new SaveData.Entry { key = kv.Value, data = s });
            }
            try
            {
                SafeFile.WriteAllTextAtomic(SavePath, JsonUtility.ToJson(data, false));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AfterHours] Saving failed: " + e.Message);
                return false;
            }
        }

        string Capture(Component c)
        {
            switch (c)
            {
                case Door d: return (d.locked ? "1" : "0") + " " + F(d.Angle);
                case RollerShutter r: return (r.locked ? "1" : "0") + " " + (r.jammed ? "1" : "0") + " " + F(r.OpenAmount);
                case Drawer dr: return (dr.locked ? "1" : "0") + " " + (dr.IsOpen ? "1" : "0");
                case VentCover v: return (int)v.state + " " + V(v.cover.position) + " " + Q(v.cover.rotation);
                case PhysicalSwitch sw: return sw.isOn ? "1" : "0";
                case ItemSocket so:
                    if (so.FittedItem == null || !keys.TryGetValue(so.FittedItem, out string itemKey)) return null;
                    return (so.Satisfied ? "1" : "0") + " " + itemKey;
                case CardReader cr: return cr.Satisfied ? "1" : null;
                case ToolBreakable tb: return tb.Satisfied ? "1" : null;
                case HeavyMovable hm: return V(hm.transform.position) + " " + Q(hm.transform.rotation);
                case ItemPickup ip:
                    if (!ip.gameObject.activeSelf) return "gone";
                    if (ip.IsHeld) return "held " + ip.wear;
                    return V(ip.transform.position) + " " + Q(ip.transform.rotation) + " " + ip.wear;
                case IStateful st: return st.CaptureState();
            }
            return null;
        }

        // ---------------- reading ----------------

        public static bool TryLoad(out SaveData data)
        {
            data = null;
            if (!SafeFile.TryReadAllText(SavePath, out string json) && !SafeFile.TryReadAllText(SavePath + ".bak", out json)) return false;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return false;
                if (data.version > SaveData.CurrentVersion) return false; // from a newer build
                // (Older versions would be upgraded here.)
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AfterHours] Save could not be read: " + e.Message);
                return false;
            }
        }

        public void Apply(SaveData data, PlayerController player)
        {
            var map = new Dictionary<string, string>();
            foreach (SaveData.Entry e in data.entries) if (e != null && e.key != null) map[e.key] = e.data;

            // Power first, then physical objects, then puzzle inputs (their gates re-solve and re-open things),
            // then doors / shutters as they were, then the player.
            PowerGrid.RestoreAll();
            foreach (string id in data.breakersOff) PowerGrid.SetBreaker(id, false);

            void Each<T>(Action<T, string[]> apply) where T : Component
            {
                foreach (var kv in keys)
                {
                    if (!(kv.Key is T c) || c == null) continue;
                    if (!map.TryGetValue(kv.Value, out string s) || string.IsNullOrEmpty(s)) continue;
                    try { apply(c, s.Split(' ')); }
                    catch (Exception e) { Debug.LogWarning("[AfterHours] Skipped restoring " + kv.Value + ": " + e.Message); }
                }
            }

            Each<HeavyMovable>((h, a) => h.PlaceAt(P3(a, 0), PQ(a, 3)));
            Each<ItemPickup>((i, a) =>
            {
                if (a[0] == "gone") { i.gameObject.SetActive(false); return; }
                if (a[0] != "held") i.PlaceAt(P3(a, 0), PQ(a, 3));
                int wi = a[0] == "held" ? 1 : 7;
                if (a.Length > wi) int.TryParse(a[wi], NumberStyles.Integer, Inv, out i.wear);
            });
            Each<MonoBehaviour>((m, a) => { if (m is IStateful st) st.RestoreState(string.Join(" ", a)); });
            Each<ItemSocket>((so, a) =>
            {
                if (a.Length > 1 && byKey.TryGetValue(a[1], out Component it) && it is ItemPickup item) so.RestoreFitted(item, a[0] == "1");
            });
            Each<CardReader>((cr, a) => cr.RestoreSatisfied());
            Each<ToolBreakable>((tb, a) => tb.RestoreSatisfied());
            Each<PhysicalSwitch>((sw, a) => sw.Set(a[0] == "1", null));
            Each<Door>((d, a) => { d.locked = a[0] == "1"; d.SetAngleInstant(PF(a[1])); });
            Each<RollerShutter>((r, a) => { r.locked = a[0] == "1"; r.jammed = a[1] == "1"; r.SetRaisedInstant(PF(a[2])); });
            Each<Drawer>((dr, a) => { dr.locked = a[0] == "1"; dr.SetOpenInstant(a[1] == "1"); });
            Each<VentCover>((v, a) => v.RestoreState((VentCover.State)int.Parse(a[0], Inv), P3(a, 1), PQ(a, 4)));

            if (player != null)
            {
                player.Teleport(data.playerPosition, data.playerYaw);
                if (!string.IsNullOrEmpty(data.heldItem) && byKey.TryGetValue(data.heldItem, out Component held) && held is ItemPickup hi)
                {
                    PlayerInventory inv = player.GetComponent<PlayerInventory>();
                    if (inv != null) inv.PickUp(hi);
                }
            }
        }

        static float PF(string s) => float.Parse(s, Inv);
        static Vector3 P3(string[] a, int i) => new Vector3(PF(a[i]), PF(a[i + 1]), PF(a[i + 2]));
        static Quaternion PQ(string[] a, int i) => new Quaternion(PF(a[i]), PF(a[i + 1]), PF(a[i + 2]), PF(a[i + 3]));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;
    }
}
