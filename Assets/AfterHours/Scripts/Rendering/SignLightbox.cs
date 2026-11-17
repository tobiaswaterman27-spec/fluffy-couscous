using UnityEngine;

namespace AfterHours.Rendering
{
    /// <summary>
    /// A backlit shop fascia sign. Most were left on a timer that still runs; some tubes are failing behind the
    /// acrylic (stutter, or one half dimmer than the other), and some are long dead. Every sign goes dark with
    /// its circuit when the power is cut, and comes back with a stutter when it's restored.
    /// </summary>
    public class SignLightbox : MonoBehaviour
    {
        public enum State { Lit, Failing, HalfDead, Dead }

        public State state = State.Lit;
        public Renderer[] faces;              // one per half of the box (the halves can fail separately)
        [ColorUsage(false, true)] public Color emission = new Color(1.6f, 1.55f, 1.4f);
        public int seed;
        [Tooltip("Optional real light the box throws (a vending machine lighting the floor in front of it).")]
        public Light glow;
        float glowBase = -1f;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        System.Random rng;
        float level = 1f, stepTimer, burstUntil, nextBurst, power = 1f;
        readonly float[] halfLevel = { 1f, 1f };

        void Start()
        {
            rng = new System.Random(seed);
            block = new MaterialPropertyBlock();
            nextBurst = Time.time + R(1f, 15f);
            halfLevel[1] = state == State.HalfDead ? R(0.05f, 0.25f) : 1f;
            Apply();
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void Update()
        {
            if (state == State.Dead) return;
            // follow the circuit: off quickly, on again with a little stutter
            bool on = AfterHours.Power.PowerGrid.LightFactorAt(transform.position) > 0.5f;
            float target = on ? 1f : 0f;
            if (on && power < 1f && rng.NextDouble() < 0.15) power = R(0f, 1f);
            power = Mathf.MoveTowards(power, target, Time.deltaTime * (on ? 1.2f : 6f));

            float t = Time.time;
            stepTimer -= Time.deltaTime;
            if (state == State.Failing)
            {
                if (t >= nextBurst) { burstUntil = t + R(0.3f, 2f); nextBurst = t + R(3f, 20f); }
                if (t < burstUntil) { if (stepTimer <= 0f) { stepTimer = R(0.03f, 0.15f); level = rng.NextDouble() < 0.5 ? R(0f, 0.2f) : R(0.6f, 1f); } }
                else level = 0.85f;
            }
            Apply();
        }

        void Apply()
        {
            if (glow != null)
            {
                if (glowBase < 0f) glowBase = glow.intensity;
                glow.intensity = state == State.Dead ? 0f : glowBase * level * power;
            }
            if (faces == null || block == null) return;
            for (int i = 0; i < faces.Length; i++)
            {
                if (faces[i] == null) continue;
                float k = state == State.Dead ? 0f : level * power * halfLevel[Mathf.Min(i, 1)];
                block.SetColor(EmissionId, emission * k);
                faces[i].SetPropertyBlock(block);
            }
        }
    }
}
