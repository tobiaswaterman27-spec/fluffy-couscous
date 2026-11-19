using UnityEngine;
using AfterHours.Audio;
using AfterHours.Core;
using AfterHours.Net;
using AfterHours.Power;

namespace AfterHours.Flow
{
    /// <summary>
    /// Section blackouts: every few minutes the supply to one part of the building fails. The tubes stutter and
    /// die, the battery emergency lights come on, and for about a minute that wing is nearly dark - for you and
    /// for Elliot. Then it comes back by itself (nothing to reset). More often as the night gets worse.
    /// Runs on the host in co-op; everyone sees the same blackout (synced with the power state).
    /// </summary>
    public class BlackoutDirector : MonoBehaviour
    {
        static readonly string[] Sections = { "north", "east", "south", "west", "atrium", "carpark" };

        public static bool Active => current != null;
        public static string Current => current;

        static string current;
        float next = -1f, endAt;

        void Update()
        {
            GameFlow flow = GameFlow.Instance;
            if (flow == null || flow.State != FlowState.Playing || NetSession.IsClient || TutorialDirector.Running)
            {
                if (current != null && (flow == null || flow.State != FlowState.Playing)) End();
                return;
            }
            if (GamePause.IsPaused) return;
            if (next < 0f) next = Time.time + Random.Range(180f, 300f);
            if (current != null)
            {
                if (Time.time >= endAt) End();
                return;
            }
            if (Time.time < next) return;
            int threat = AfterHours.Creature.ElliotBrain.Threat;
            next = Time.time + Random.Range(200f, 420f) / (1f + 0.25f * threat);
            Begin(Sections[Random.Range(0, Sections.Length)], Random.Range(45f, 80f));
        }

        public void Begin(string section, float seconds)
        {
            if (!PowerGrid.IsPowered(section)) return;     // already dark (a tripped breaker)
            current = section;
            endAt = Time.time + seconds;
            PowerGrid.SetFault(section, true);
            // a deep electrical thunk from somewhere in the building
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            if (cam != null) AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.BreakerOff), cam.position + Vector3.up * 6f, 0.5f, 0.55f, 80f);
        }

        public void End()
        {
            if (current == null) return;
            PowerGrid.SetFault(current, false);
            current = null;
        }

        void OnDisable() => End();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;
    }
}
