using System.Collections.Generic;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Power;
using AfterHours.Puzzles;

namespace AfterHours.Machines
{
    /// <summary>
    /// The building is old and badly maintained. Every few minutes something goes wrong: a wing's breaker trips
    /// (its lights stutter and die), an escalator develops a fault, or the lift supply cuts out (anyone in a lift is
    /// stuck between floors). The breakers are reset on the switchboard in the car park plant room.
    /// Seeded, so in co-op the host decides and everyone sees the same thing (Part 16).
    /// </summary>
    public class MalfunctionDirector : MonoBehaviour, IResettable
    {
        public float firstDelayMin = 150f, firstDelayMax = 300f;
        public float intervalMin = 210f, intervalMax = 480f;
        public int seed = 1130;
        [Tooltip("Where trips are heard from (the switchboard).")]
        public Transform switchboard;

        static readonly string[] Trippable = { "north", "east", "south", "west", "atrium", "carpark", "lifts" };

        System.Random rng;
        float next;

        void Start() => Schedule(true);

        void Schedule(bool first)
        {
            if (rng == null) rng = new System.Random(seed);
            next = Time.time + (first ? Range(firstDelayMin, firstDelayMax) : Range(intervalMin, intervalMax));
        }

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void Update()
        {
            if (Time.time < next) return;
            Schedule(false);
            Trigger();
        }

        /// <summary>Makes something go wrong right now (also used by the developer menu).</summary>
        public void Trigger()
        {
            double roll = rng.NextDouble();
            if (roll < 0.3)
            {
                var running = new List<Escalator>();
                foreach (Escalator e in FindObjectsByType<Escalator>())
                    if (e.state == EscalatorState.Running) running.Add(e);
                if (running.Count > 0) { running[rng.Next(running.Count)].Break(); return; }
            }
            var on = new List<string>();
            foreach (string id in Trippable) if (PowerGrid.BreakerOn(id)) on.Add(id);
            if (on.Count == 0) return;
            string trip = on[rng.Next(on.Count)];
            PowerGrid.SetBreaker(trip, false);
            Vector3 at = switchboard != null ? switchboard.position : Vector3.zero;
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.BreakerOff), at, 1f, 0.9f, 40f);
            NoiseBus.Emit(new NoiseEvent(at, 18f, NoiseKind.Machinery, gameObject));
        }

        public void ResetState() => Schedule(true);
    }
}
