using System.Collections.Generic;
using UnityEngine;

namespace AfterHours.Audio
{
    /// <summary>
    /// The game's sound files (rendered by tools/sfxgen into Resources/Audio). Each sound has several takes
    /// ("step_tile_walk_0", "_1", ...); Get("step_tile_walk") returns one at random, never the same take twice in a
    /// row, so repeated sounds don't sound mechanical. Missing sounds return null and the caller falls back to
    /// the sounds generated in code.
    /// </summary>
    public static class AudioLibrary
    {
        static Dictionary<string, AudioClip[]> groups;
        static readonly Dictionary<string, int> last = new Dictionary<string, int>();

        static void Ensure()
        {
            if (groups != null) return;
            groups = new Dictionary<string, AudioClip[]>();
            var temp = new Dictionary<string, List<AudioClip>>();
            foreach (AudioClip c in Resources.LoadAll<AudioClip>("Audio"))
            {
                string name = c.name;
                int us = name.LastIndexOf('_');
                string key = us > 0 && int.TryParse(name.Substring(us + 1), out _) ? name.Substring(0, us) : name;
                if (!temp.TryGetValue(key, out List<AudioClip> list)) temp[key] = list = new List<AudioClip>();
                list.Add(c);
            }
            foreach (var kv in temp)
            {
                kv.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                groups[kv.Key] = kv.Value.ToArray();
            }
        }

        public static bool Has(string key)
        {
            Ensure();
            return groups.ContainsKey(key);
        }

        public static AudioClip Get(string key)
        {
            Ensure();
            if (!groups.TryGetValue(key, out AudioClip[] clips) || clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];
            last.TryGetValue(key, out int prev);
            int i = Random.Range(0, clips.Length - 1);
            if (i >= prev) i++;
            last[key] = i;
            return clips[i];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { groups = null; last.Clear(); }
    }
}
