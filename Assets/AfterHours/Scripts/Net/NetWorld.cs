using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using AfterHours.Hiding;
using AfterHours.Interaction;
using AfterHours.Items;
using AfterHours.Machines;
using AfterHours.Player;
using AfterHours.Power;
using AfterHours.Puzzles;
using AfterHours.World;

namespace AfterHours.Net
{
    /// <summary>
    /// The shared world in co-op. The host's copy is the real one: doors, shutters, drawers, vent covers, switches,
    /// sockets, card readers, cut chains, items, heavy objects, lifts, escalators, exits and the power. Ten times a
    /// second the host sends whatever changed; clients show it (doors swing, items glide, lifts travel).
    /// When a client uses something, the request goes to the host, which does it as that player.
    /// Every machine runs the same built scene, so each object's key (type + where it started + name) matches.
    /// </summary>
    public class NetWorld
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        readonly Dictionary<string, Component> byKey = new Dictionary<string, Component>();
        readonly Dictionary<Component, string> keys = new Dictionary<Component, string>();
        readonly Dictionary<string, string> lastSent = new Dictionary<string, string>();
        readonly Dictionary<HeavyMovable, (Vector3 p, Quaternion r)> heavyTargets = new Dictionary<HeavyMovable, (Vector3, Quaternion)>();
        public const string PowerKey = "power";

        public void Build()
        {
            byKey.Clear();
            keys.Clear();
            foreach (Interactable i in UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Add(i, i is ItemPickup ip && !string.IsNullOrEmpty(ip.instanceId) ? "Item:" + ip.instanceId : KeyFor(i));
            foreach (HeavyMovable h in UnityEngine.Object.FindObjectsByType<HeavyMovable>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Add(h, KeyFor(h));
            foreach (LiftController l in UnityEngine.Object.FindObjectsByType<LiftController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Add(l, KeyFor(l));
            foreach (Escalator e in UnityEngine.Object.FindObjectsByType<Escalator>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Add(e, KeyFor(e));
            foreach (EscapeTrigger t in UnityEngine.Object.FindObjectsByType<EscapeTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Add(t, KeyFor(t));
        }

        static string KeyFor(Component c)
        {
            Vector3 p = c.transform.position;
            return c.GetType().Name + "@" + Mathf.RoundToInt(p.x * 10f) + "," + Mathf.RoundToInt(p.y * 10f) + "," + Mathf.RoundToInt(p.z * 10f) + ":" + c.name;
        }

        void Add(Component c, string key)
        {
            string k = key;
            for (int i = 2; byKey.ContainsKey(k); i++) k = key + "#" + i;
            byKey[k] = c;
            keys[c] = k;
        }

        public string KeyOf(Component c) => c != null && keys.TryGetValue(c, out string k) ? k : null;
        public T Find<T>(string key) where T : Component => key != null && byKey.TryGetValue(key, out Component c) ? c as T : null;

        // ---------------- host: what changed ----------------

        static string F(float v) => v.ToString("0.###", Inv);
        static string V(Vector3 v) => F(v.x) + " " + F(v.y) + " " + F(v.z);
        static string Q(Quaternion q) => F(q.x) + " " + F(q.y) + " " + F(q.z) + " " + F(q.w);

        public void CollectChanges(List<(string key, string data)> into, Func<GameObject, int> playerIdOf)
        {
            string power = "";
            foreach (string id in PowerGrid.Circuits) if (!PowerGrid.BreakerOn(id)) power += id + ",";
            Check(PowerKey, power, into);
            foreach (var kv in keys)
            {
                if (kv.Key == null) continue;
                string s = Capture(kv.Key, playerIdOf);
                if (s != null) Check(kv.Value, s, into);
            }
        }

        void Check(string key, string data, List<(string, string)> into)
        {
            if (lastSent.TryGetValue(key, out string old) && old == data) return;
            lastSent[key] = data;
            into.Add((key, data));
        }

        string Capture(Component c, Func<GameObject, int> playerIdOf)
        {
            switch (c)
            {
                case Door d: return (d.locked ? "1 " : "0 ") + F(d.TargetAngle);
                case RollerShutter r: return (r.locked ? "1 " : "0 ") + (r.jammed ? "1 " : "0 ") + F(r.OpenAmount);
                case Drawer dr: return (dr.locked ? "1 " : "0 ") + (dr.IsOpen ? "1" : "0");
                case VentCover v: return (int)v.state + (v.state == VentCover.State.Removed ? " " + V(v.cover.position) + " " + Q(v.cover.rotation) : "");
                case PhysicalSwitch sw: return sw.isOn ? "1" : "0";
                case ItemSocket so: return so.FittedItem != null ? (so.Satisfied ? "1 " : "0 ") + KeyOf(so.FittedItem) : "e";
                case CardReader cr: return cr.Satisfied ? "1" : "0";
                case ToolBreakable tb: return tb.Satisfied ? "1" : "0";
                case HeavyMovable hm: return V(hm.transform.position) + " " + Q(hm.transform.rotation);
                case ItemPickup ip:
                    if (ip.IsHeld)
                    {
                        int pid = playerIdOf(ip.Holder);
                        return pid >= 0 ? "h " + pid : "s";
                    }
                    return "p " + V(ip.transform.position) + " " + Q(ip.transform.rotation);
                case LiftController l: return l.NetCapture();
                case Escalator e: return e.NetCapture();
                case EscapeTrigger t: return t.open ? "1" : "0";
            }
            return null;
        }

        // ---------------- client: show it ----------------

        public void Apply(string key, string data, Func<int, PlayerController> playerById, PlayerController local)
        {
            if (key == PowerKey)
            {
                var off = new HashSet<string>(data.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                foreach (string id in PowerGrid.Circuits) PowerGrid.SetBreaker(id, !off.Contains(id));
                return;
            }
            if (!byKey.TryGetValue(key, out Component c) || c == null) return;
            string[] a = data.Split(' ');
            try
            {
                switch (c)
                {
                    case Door d: d.locked = a[0] == "1"; d.NetMoveTo(P(a[1])); break;
                    case RollerShutter r: r.locked = a[0] == "1"; r.jammed = a[1] == "1"; r.SetRaisedInstant(P(a[2])); break;
                    case Drawer dr: dr.locked = a[0] == "1"; if (dr.IsOpen != (a[1] == "1")) dr.SetOpenInstant(a[1] == "1"); break;
                    case VentCover v:
                    {
                        var st = (VentCover.State)int.Parse(a[0], Inv);
                        if (st == VentCover.State.Removed) v.RestoreState(st, V3(a, 1), Q4(a, 4));
                        else v.RestoreState(st, Vector3.zero, Quaternion.identity);
                        break;
                    }
                    case PhysicalSwitch sw: if (sw.isOn != (a[0] == "1")) sw.Set(a[0] == "1", null); break;
                    case ItemSocket so:
                        if (a[0] != "e" && so.FittedItem == null && a.Length > 1 && byKey.TryGetValue(a[1], out Component it) && it is ItemPickup fit)
                        {
                            DropFromHands(fit, local);
                            so.RestoreFitted(fit, a[0] == "1");
                        }
                        break;
                    case CardReader cr: if (a[0] == "1") cr.RestoreSatisfied(); break;
                    case ToolBreakable tb: if (a[0] == "1") tb.RestoreSatisfied(); break;
                    case HeavyMovable hm:
                    {
                        Rigidbody rb = hm.GetComponent<Rigidbody>();
                        if (rb != null) rb.isKinematic = true;
                        heavyTargets[hm] = (V3(a, 0), Q4(a, 3));
                        break;
                    }
                    case ItemPickup ip: ApplyItem(ip, a, playerById, local); break;
                    case LiftController l: l.NetApply(a); break;
                    case Escalator e: e.NetApply(a); break;
                    case EscapeTrigger t: t.open = a[0] == "1"; break;
                }
            }
            catch (Exception e) { Debug.LogWarning("[AfterHours] Couldn't apply " + key + ": " + e.Message); }
        }

        static void DropFromHands(ItemPickup item, PlayerController local)
        {
            PlayerInventory inv = local != null ? local.GetComponent<PlayerInventory>() : null;
            if (inv != null && inv.Held == item) inv.NetSetHeld(null);
        }

        void ApplyItem(ItemPickup ip, string[] a, Func<int, PlayerController> playerById, PlayerController local)
        {
            PlayerInventory mine = local != null ? local.GetComponent<PlayerInventory>() : null;
            if (a[0] == "h")
            {
                PlayerController holder = playerById(int.Parse(a[1], Inv));
                if (holder == null) return;
                PlayerInventory inv = holder.GetComponent<PlayerInventory>();
                if (mine != null && mine.Held == ip && inv != mine) mine.NetSetHeld(null);
                if (inv != null && inv.Held != ip) inv.NetSetHeld(ip);
                return;
            }
            if (a[0] == "s") return; // in a socket (the socket's entry places it)
            if (mine != null && mine.Held == ip) mine.NetSetHeld(null);
            foreach (PlayerController p in PlayerController.All)
            {
                PlayerInventory inv = p != null ? p.GetComponent<PlayerInventory>() : null;
                if (inv != null && inv != mine && inv.Held == ip) inv.NetSetHeld(null);
            }
            if (ip.IsHeld) ip.Release(V3(a, 1), Vector3.zero);
            ip.NetSetPose(V3(a, 1), Q4(a, 4));
        }

        /// <summary>Clients: glide heavy objects to where the host has them.</summary>
        public void Smooth(float dt)
        {
            float k = 1f - Mathf.Exp(-12f * dt);
            foreach (var kv in heavyTargets)
            {
                if (kv.Key == null) continue;
                Transform t = kv.Key.transform;
                t.SetPositionAndRotation(Vector3.Lerp(t.position, kv.Value.p, k), Quaternion.Slerp(t.rotation, kv.Value.r, k));
            }
        }

        static float P(string s) => float.Parse(s, Inv);
        static Vector3 V3(string[] a, int i) => new Vector3(P(a[i]), P(a[i + 1]), P(a[i + 2]));
        static Quaternion Q4(string[] a, int i) => new Quaternion(P(a[i]), P(a[i + 1]), P(a[i + 2]), P(a[i + 3]));

        // ---------------- host: doing things for remote players ----------------

        public void Interact(string key, PlayerController asPlayer)
        {
            Interactable i = Find<Interactable>(key);
            PlayerInteractor who = asPlayer != null ? asPlayer.GetComponent<PlayerInteractor>() : null;
            if (i == null || who == null) return;
            if (i.GetPrompt(who) == null && !(i is ItemPickup)) return; // not possible for them (e.g. no key)
            i.Interact(who);
        }

        public void HoldTick(string key, PlayerController asPlayer, float dt)
        {
            Interactable i = Find<Interactable>(key);
            PlayerInteractor who = asPlayer != null ? asPlayer.GetComponent<PlayerInteractor>() : null;
            if (i != null && who != null) i.HoldTick(who, dt);
        }
    }
}
