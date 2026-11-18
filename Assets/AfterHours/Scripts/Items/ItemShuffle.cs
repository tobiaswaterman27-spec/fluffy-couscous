using System.Collections.Generic;
using UnityEngine;

namespace AfterHours.Items
{
    /// <summary>
    /// Every run the important items are somewhere different: a key that was in Mall management last time may be
    /// in the Records office or the Roof access plant room now. Same seed = same layout (saves, co-op host).
    /// Clients never shuffle; they get item positions from the host.
    /// </summary>
    public static class ItemShuffle
    {
        public static int Seed { get; private set; }

        public static int NewSeed() => Random.Range(1, int.MaxValue);

        public static void Apply(int seed)
        {
            Seed = seed;
            if (seed == 0) return;
            var rng = new System.Random(seed);

            var spots = new Dictionary<string, List<(Vector3 p, Quaternion r, bool keep)>>();
            var items = new Dictionary<string, List<ItemPickup>>();
            foreach (ItemSpot s in Object.FindObjectsByType<ItemSpot>(FindObjectsInactive.Include))
            {
                if (string.IsNullOrEmpty(s.group)) continue;
                if (!spots.TryGetValue(s.group, out var l)) spots[s.group] = l = new List<(Vector3, Quaternion, bool)>();
                l.Add((s.transform.position, s.transform.rotation, s.fixedRotation));
            }
            foreach (ItemPickup i in Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include))
            {
                if (string.IsNullOrEmpty(i.shuffleGroup)) continue;
                if (!items.TryGetValue(i.shuffleGroup, out var l)) items[i.shuffleGroup] = l = new List<ItemPickup>();
                l.Add(i);
            }

            // Deterministic order on every machine.
            var groups = new List<string>(items.Keys);
            groups.Sort(string.CompareOrdinal);
            foreach (string g in groups)
            {
                List<ItemPickup> list = items[g];
                list.Sort((a, b) => string.CompareOrdinal(a.instanceId, b.instanceId));
                if (!spots.TryGetValue(g, out var places)) continue;
                places.Sort((a, b) => a.p.x != b.p.x ? a.p.x.CompareTo(b.p.x) : a.p.z != b.p.z ? a.p.z.CompareTo(b.p.z) : a.p.y.CompareTo(b.p.y));
                // Fisher-Yates over the spots, then the first N take the items.
                for (int k = places.Count - 1; k > 0; k--)
                {
                    int j = rng.Next(k + 1);
                    (places[k], places[j]) = (places[j], places[k]);
                }
                for (int k = 0; k < list.Count && k < places.Count; k++)
                {
                    float yaw = (float)rng.NextDouble() * 360f;
                    list[k].gameObject.SetActive(true);
                    Quaternion rot = places[k].keep ? places[k].r : Quaternion.Euler(0f, yaw, 0f);
                    list[k].SetHome(places[k].p + Vector3.up * 0.01f, rot);
                }
            }
        }
    }
}
