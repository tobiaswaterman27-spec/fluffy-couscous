using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AfterHours.World.Layout;

namespace AfterHours.Flow
{
    /// <summary>
    /// Your record across every game: nights played, ways out found, fastest escape, times caught - and achievements.
    /// Kept in stats.json next to the settings; never reset by a run starting over. A small notice slides in when an
    /// achievement unlocks.
    /// </summary>
    public static class Stats
    {
        [Serializable]
        public class Data
        {
            public int nights, escapes, caught, generatorStarts, axeDoors, silentFireExits;
            public float timePlayed, fastestEscape = -1f;
            public int[] routes = new int[8];
            public List<string> achievements = new List<string>();
        }

        public struct Achievement { public string id, title, how; }

        public static readonly Achievement[] All =
        {
            new Achievement { id = "first_light", title = "First Light", how = "Get out of the mall." },
            new Achievement { id = "every_way", title = "Every Way Out", how = "Get out by all eight ways." },
            new Achievement { id = "untouched", title = "Untouched", how = "Get out without being caught once." },
            new Achievement { id = "quick", title = "In and Out", how = "Get out in under 30 minutes." },
            new Achievement { id = "dawn", title = "Last One Standing", how = "Still be inside at 4 AM on the clock, and get out." },
            new Achievement { id = "hard", title = "No Mercy", how = "Get out on Hard." },
            new Achievement { id = "together", title = "Nobody Left Behind", how = "Get out in co-op with everyone." },
            new Achievement { id = "lumberjack", title = "Here's Johnny", how = "Break down three doors with the fire axe." },
            new Achievement { id = "power", title = "Power to the People", how = "Start the standby generator." },
            new Achievement { id = "silent", title = "No Alarms", how = "Open the fire exit without setting off the alarm." },
            new Achievement { id = "stubborn", title = "Stubborn", how = "Get out after being caught five times in one game." },
            new Achievement { id = "two_of_them", title = "Two of Them", how = "Get out on Nightmare (or One Life)." },
            new Achievement { id = "one_shot", title = "One Shot", how = "Get out on One Life." },
            new Achievement { id = "ghost", title = "Ghost", how = "Get out without Elliot ever chasing you." },
            new Achievement { id = "hold_door", title = "Hold the Door", how = "Make Elliot smash through a door you barricaded." },
            new Achievement { id = "lights_out", title = "Lights Out", how = "Get out while part of the mall is blacked out." },
            new Achievement { id = "regular", title = "Regular", how = "Start ten nights." },
            new Achievement { id = "escape_artist", title = "Escape Artist", how = "Get out ten times." },
            new Achievement { id = "orientation", title = "Orientation", how = "Finish the tutorial." },
        };

        static Data data;
        static string Path => System.IO.Path.Combine(Application.persistentDataPath, "stats.json");

        public static Data Current
        {
            get
            {
                if (data != null) return data;
                try { if (File.Exists(Path)) data = JsonUtility.FromJson<Data>(File.ReadAllText(Path)); }
                catch (Exception e) { Debug.LogWarning("[AfterHours] Stats could not be read: " + e.Message); }
                if (data == null) data = new Data();
                if (data.routes == null || data.routes.Length != 8) data.routes = new int[8];
                if (data.achievements == null) data.achievements = new List<string>();
                return data;
            }
        }

        static void Save()
        {
            try { File.WriteAllText(Path, JsonUtility.ToJson(Current, true)); }
            catch (Exception e) { Debug.LogWarning("[AfterHours] Stats could not be saved: " + e.Message); }
        }

        public static bool Has(string id) => Current.achievements.Contains(id);

        static void Unlock(string id)
        {
            if (Has(id)) return;
            Current.achievements.Add(id);
            foreach (Achievement a in All) if (a.id == id) AchievementNotice.Show(a.title);
        }

        public static void NightStarted() { Current.nights++; if (Current.nights >= 10) Unlock("regular"); Save(); }
        public static void Caught() { Current.caught++; Save(); }
        public static void AddTime(float seconds) { Current.timePlayed += seconds; }

        public static void Event(string what)
        {
            Data d = Current;
            switch (what)
            {
                case "generator": d.generatorStarts++; Unlock("power"); break;
                case "axe_door": d.axeDoors++; if (d.axeDoors >= 3) Unlock("lumberjack"); break;
                case "silent_fire_exit": d.silentFireExits++; Unlock("silent"); break;
                case "barricade_smashed": Unlock("hold_door"); break;
                case "tutorial": Unlock("orientation"); break;
            }
            Save();
        }

        /// <summary>Got out. time = time inside this game, caughtThisGame, nightSeconds = the night clock.</summary>
        public static void Escaped(EscapeRouteId route, float time, int caughtThisGame, float nightSeconds, int difficulty, bool coopAllOut)
            => Escaped(route, time, caughtThisGame, nightSeconds, difficulty, coopAllOut, true);

        /// <param name="chased">Did Elliot hunt you at any point this game?</param>
        public static void Escaped(EscapeRouteId route, float time, int caughtThisGame, float nightSeconds, int difficulty, bool coopAllOut, bool chased)
        {
            Data d = Current;
            d.escapes++;
            int r = (int)route - 1;
            if (r >= 0 && r < 8) d.routes[r]++;
            if (d.fastestEscape < 0f || time < d.fastestEscape) d.fastestEscape = time;
            Unlock("first_light");
            bool all = true;
            foreach (int n in d.routes) if (n == 0) all = false;
            if (all) Unlock("every_way");
            if (caughtThisGame == 0) Unlock("untouched");
            if (time < 30f * 60f) Unlock("quick");
            if (nightSeconds * NightDirector.ClockScale >= 4f * 3600f) Unlock("dawn");
            if (difficulty >= 2) Unlock("hard");
            if (difficulty >= 3) Unlock("two_of_them");
            if (difficulty == 4) Unlock("one_shot");
            if (!chased) Unlock("ghost");
            if (BlackoutDirector.Active) Unlock("lights_out");
            if (d.escapes >= 10) Unlock("escape_artist");
            if (coopAllOut) Unlock("together");
            if (caughtThisGame >= 5) Unlock("stubborn");
            Save();
        }

        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) return "-";
            int s = Mathf.FloorToInt(seconds);
            return s >= 3600 ? $"{s / 3600}:{(s / 60) % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => data = null;
    }
}
