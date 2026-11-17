using System;
using UnityEngine;

namespace AfterHours.Player
{
    /// <summary>The people you can play as (built by tools/chargen; the list is Resources/Player/Characters/roster.json).</summary>
    public static class PlayerCharacters
    {
        [Serializable] public class Entry { public string id, name; }
        [Serializable] class Wrapper { public Entry[] items; }

        public const string Default = "tom";
        static Entry[] all;

        public static Entry[] All
        {
            get
            {
                if (all != null) return all;
                TextAsset t = Resources.Load<TextAsset>("Player/Characters/roster");
                all = t != null ? JsonUtility.FromJson<Wrapper>("{\"items\":" + t.text + "}").items : new Entry[0];
                return all;
            }
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i;
            return 0;
        }

        public static Entry Get(string id) => All.Length > 0 ? All[IndexOf(id)] : null;
    }
}
