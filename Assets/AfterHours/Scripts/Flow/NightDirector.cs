using System.Collections.Generic;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Core;
using AfterHours.Creature;
using AfterHours.Net;
using AfterHours.Puzzles;

namespace AfterHours.Flow
{
    /// <summary>
    /// The night gets worse. Two things push it on, whichever is further along:
    ///   - time:     the clock runs from midnight (one real minute = two and a half minutes of night);
    ///   - progress: how close anyone is to getting out (parts fitted, chains cut, the generator running).
    /// Threat 0 - it keeps to the car park unless something draws it out.
    /// Threat 1 - it roams the whole building; hears and sees a little better.
    /// Threat 2 - it starts drifting towards places where someone has been working on a way out.
    /// Threat 3 - one step from escape (or very late): it moves with purpose, listens hard and checks those places.
    /// Each rise is marked by a scream somewhere in the building. Never goes down until the run starts over.
    /// Runs on the host only in co-op (the creatures live there).
    /// </summary>
    public class NightDirector : MonoBehaviour
    {
        public static NightDirector Instance { get; private set; }

        public const float ClockScale = 2.5f;
        public static readonly float[] TimeLevels = { 0f, 15f * 60f, 40f * 60f, 70f * 60f };

        public int Threat { get; private set; }
        public float NightTime { get; set; }

        struct Base { public float slow, walk, fast, run, crawl, hear, sight; }
        readonly Dictionary<ElliotBrain, Base> bases = new Dictionary<ElliotBrain, Base>();
        float evalTimer;
        PuzzleGate[] gates;
        Generator[] generators;

        void Awake()
        {
            Instance = this;
            if (GetComponent<BlackoutDirector>() == null) gameObject.AddComponent<BlackoutDirector>();
        }

        public const int Easy = 0, Normal = 1, Hard = 2, Nightmare = 3, OneLife = 4;
        public static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard", "Nightmare", "One Life" };
        public static readonly string[] DifficultyHelp =
        {
            "It takes longer to get worse, and hears and sees less.",
            "As intended.",
            "It roams everywhere from the start, gets worse fast, hears and sees more.",
            "Harder than Hard - and at 3 AM a second one comes up from the car park. It's faster.",
            "Nightmare, and you only get caught once: then the night is over. (In co-op it plays as Nightmare.)",
        };

        /// <summary>The Nightmare second creature arrives at this time on the night clock (3 AM).</summary>
        public const float LateArrivalClock = 3f * 3600f;
        public static readonly Vector3 LateStart = new Vector3(-40f, AfterHours.World.Layout.MallLayout.CarParkY + 0.05f, -60f);
        bool lateReleased;

        /// <summary>Single player on Nightmare / One Life: makes the second creature (hidden until 3 AM).</summary>
        void EnsureLateCreature()
        {
            if (NetSession.Active) return;                 // co-op: NetCreatures made it at the start
            foreach (ElliotBrain b in ElliotBrain.All) if (b != null && b.lateArrival) return;
            ElliotBrain first = null;
            foreach (ElliotBrain b in ElliotBrain.All) if (b != null && b.gameObject.activeSelf) { first = b; break; }
            if (first == null) return;
            GameObject copy = Instantiate(first.gameObject, LateStart, Quaternion.Euler(0f, 90f, 0f));
            copy.name = "Elliot (the second one)";
            ElliotBrain late = copy.GetComponent<ElliotBrain>();
            late.lateArrival = true;
            late.lateStart = LateStart;
            copy.SetActive(false);
        }

        void ReleaseLate()
        {
            lateReleased = true;
            foreach (ElliotBrain b in ElliotBrain.All)
            {
                if (b == null || !b.lateArrival || b.gameObject.activeSelf) continue;
                b.transform.position = b.lateStart;
                b.gameObject.SetActive(true);
                b.Rouse();
                // it announces itself: a scream from deep in the car park
                AudioClip c = AudioLibrary.Get("elliot_scream");
                if (c != null) AudioPool.PlayAt(c, b.transform.position + Vector3.up * 2f, 1f, Random.Range(1.05f, 1.15f), 200f);
            }
        }

        void HoldLate()
        {
            lateReleased = false;
            foreach (ElliotBrain b in ElliotBrain.All)
                if (b != null && b.lateArrival && b.gameObject.activeSelf) { b.transform.position = b.lateStart; b.gameObject.SetActive(false); }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ElliotBrain.Threat = 0;
            ElliotBrain.Hotspots.Clear();
        }

        /// <summary>The clock as a person would read it: midnight + night time.</summary>
        public static string ClockText(float nightSeconds)
        {
            int minutes = Mathf.FloorToInt(nightSeconds * ClockScale / 60f) % (24 * 60);   // from midnight
            int h = minutes / 60, m = minutes % 60;
            int h12 = h % 12 == 0 ? 12 : h % 12;
            return $"{h12}:{m:00} {(h < 12 ? "AM" : "PM")}";
        }

        public void ResetRun()
        {
            NightTime = 0f;
            Threat = 0;
            HoldLate();
            BlackoutDirector bd = GetComponent<BlackoutDirector>();
            if (bd != null) bd.End();
            Apply();
        }

        void Update()
        {
            GameFlow flow = GameFlow.Instance;
            if (flow == null || flow.State != FlowState.Playing || NetSession.IsClient || TutorialDirector.Running) return;
            if (!GamePause.IsPaused) NightTime += Time.deltaTime;
            evalTimer -= Time.deltaTime;
            if (evalTimer > 0f) return;
            evalTimer = 2f;

            int level = 0;
            int diff = Difficulty;
            float tScale = diff == Easy ? 1.5f : diff == Hard ? 0.6f : diff >= Nightmare ? 0.45f : 1f;
            for (int i = TimeLevels.Length - 1; i >= 0; i--) if (NightTime >= TimeLevels[i] * tScale) { level = i; break; }
            if (diff >= Hard) level = Mathf.Max(level, 1);              // hard and up: it's already roaming
            if (diff >= Nightmare)
            {
                EnsureLateCreature();
                if (!lateReleased && NightTime * ClockScale >= LateArrivalClock) ReleaseLate();
            }
            level = Mathf.Max(level, ProgressLevel());
            if (level > Threat)
            {
                Threat = level;
                Stinger();
            }
            Apply();
        }

        int ProgressLevel()
        {
            if (gates == null || gates.Length == 0) gates = FindObjectsByType<PuzzleGate>();
            if (generators == null) generators = FindObjectsByType<Generator>();
            ElliotBrain.Hotspots.Clear();
            int level = 0, totalDone = 0;
            foreach (PuzzleGate g in gates)
            {
                if (g == null || g.inputs.Count == 0) continue;
                int done = 0;
                foreach (MonoBehaviour m in g.inputs)
                {
                    if (!(m is IPuzzleInput i) || !i.Satisfied) continue;
                    done++;
                    ElliotBrain.Hotspots.Add(m is AnyOfInput any ? any.Where : m.transform.position);
                }
                totalDone += done;
                float f = done / (float)g.inputs.Count;
                if (g.Done || (g.inputs.Count >= 3 && done == g.inputs.Count - 1)) level = Mathf.Max(level, 3);
                else if (f >= 0.5f) level = Mathf.Max(level, 2);
                else if (f >= 0.34f) level = Mathf.Max(level, 1);
            }
            foreach (Generator gen in generators) if (gen != null && gen.Running) ElliotBrain.Hotspots.Add(gen.transform.position);
            if (totalDone >= 7) level = Mathf.Max(level, 2);
            else if (totalDone >= 3) level = Mathf.Max(level, 1);
            return level;
        }

        /// <summary>0 easy, 1 normal, 2 hard (from the settings of whoever runs the creatures).</summary>
        public static int Difficulty
        {
            get
            {
                var s = AfterHours.Settings.SettingsService.CurrentOrDefault;
                return s != null ? Mathf.Clamp(s.difficulty, 0, 4) : 1;
            }
        }

        void Apply()
        {
            ElliotBrain.Threat = Threat;
            foreach (ElliotBrain b in FindObjectsByType<ElliotBrain>())
            {
                if (b.motor == null || b.senses == null) continue;
                if (!bases.TryGetValue(b, out Base o))
                {
                    o = new Base
                    {
                        slow = b.motor.slowWalkSpeed, walk = b.motor.walkSpeed, fast = b.motor.fastWalkSpeed,
                        run = b.motor.runSpeed, crawl = b.motor.crawlSpeed, hear = b.senses.hearingScale, sight = b.senses.sightRange
                    };
                    bases[b] = o;
                }
                float k = Threat / 3f;
                int diff = Difficulty;
                float senses = diff == Easy ? 0.8f : diff == Hard ? 1.25f : diff >= Nightmare ? 1.35f : 1f;
                float pace = diff == Easy ? 0.9f : diff == Hard ? 1.08f : diff >= Nightmare ? 1.12f : 1f;
                if (b.lateArrival) pace *= 1.15f;                       // the second one is quicker
                // Searching and stalking get much quicker; its sprint barely changes (you can still outrun it, just).
                b.motor.slowWalkSpeed = o.slow * (1f + 0.25f * k) * pace;
                b.motor.walkSpeed = o.walk * (1f + 0.3f * k) * pace;
                b.motor.fastWalkSpeed = o.fast * (1f + 0.25f * k) * pace;
                b.motor.runSpeed = Mathf.Min(o.run * (1f + 0.03f * k) * pace, 6.1f);   // never faster than you can sprint
                b.motor.crawlSpeed = o.crawl * (1f + 0.15f * k) * pace;
                b.senses.hearingScale = o.hear * (1f + 0.45f * k) * senses;
                b.senses.sightRange = (o.sight + 12f * k) * senses;
                if (Threat >= 1) b.Rouse();
            }
        }

        void Stinger()
        {
            // Somewhere in the building, far off.
            AudioClip c = AudioLibrary.Get("elliot_scream");
            ElliotBrain e = FindAnyObjectByType<ElliotBrain>();
            if (c == null || e == null) return;
            AudioPool.PlayAt(c, e.transform.position + Vector3.up * 2f, 0.9f, Random.Range(0.85f, 0.95f), 160f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            ElliotBrain.Threat = 0;
            ElliotBrain.Hotspots.Clear();
        }
    }
}
