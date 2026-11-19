using UnityEngine;

namespace AfterHours.Rendering
{
    public enum FlickerPattern
    {
        Stutter,  // works, but every so often stutters on/off for a moment
        Dying,    // dim, unstable, fluttering; occasionally comes up to full brightness
        Broken    // mostly dark; now and then tries to strike and fails
    }

    /// <summary>
    /// A faulty fluorescent fitting: drives a real-time light, the fitting's glowing panel and an electrical buzz.
    /// Deterministic per fitting (seeded), so in co-op every player sees the same rhythm once time is synced.
    /// </summary>
    public class FluorescentFlicker : MonoBehaviour
    {
        public Light lightSource;
        public Renderer[] panels;
        public AudioSource buzz;
        public FlickerPattern pattern = FlickerPattern.Stutter;
        public int seed = 1;
        [ColorUsage(false, true)] public Color emission = new Color(3.2f, 3.35f, 3.5f);

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        System.Random rng;
        MaterialPropertyBlock block;
        float baseIntensity;
        float level = 1f;
        float stepTimer;
        float burstUntil;
        float nextBurst;
        float surgeStep, surgeLevel = 1f;

        static float surgeUntil = -1f;

        /// <summary>A close lightning strike: every faulty tube in the building stutters for a moment.</summary>
        public static void Surge(float seconds) => surgeUntil = Time.time + seconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => surgeUntil = -1f;

        void Start()
        {
            rng = new System.Random(seed);
            block = new MaterialPropertyBlock();
            baseIntensity = lightSource != null ? lightSource.intensity : 1f;
            nextBurst = Time.time + Range(2f, 12f);
            if (buzz != null)
            {
                buzz.clip = AfterHours.Audio.ProceduralSounds.AmbienceLoop(AfterHours.Audio.ProceduralSounds.Ambience.Buzz);
                buzz.loop = true;
                buzz.spatialBlend = 1f;
                buzz.rolloffMode = AudioRolloffMode.Linear;
                buzz.maxDistance = 9f;
                buzz.volume = 0.12f;
                buzz.Play();
            }
        }

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void Update()
        {
            float t = Time.time;
            stepTimer -= Time.deltaTime;

            switch (pattern)
            {
                case FlickerPattern.Stutter:
                    if (t >= nextBurst)
                    {
                        burstUntil = t + Range(0.2f, 1.4f);
                        nextBurst = t + Range(4f, 25f);
                    }
                    if (t < burstUntil)
                    {
                        if (stepTimer <= 0f) { stepTimer = Range(0.02f, 0.11f); level = rng.NextDouble() < 0.5 ? Range(0f, 0.15f) : 1f; }
                    }
                    else level = 1f;
                    break;

                case FlickerPattern.Dying:
                    if (stepTimer <= 0f)
                    {
                        stepTimer = Range(0.03f, 0.18f);
                        double r = rng.NextDouble();
                        level = r < 0.08 ? 1f : r < 0.3 ? Range(0f, 0.1f) : Range(0.25f, 0.55f);
                    }
                    break;

                case FlickerPattern.Broken:
                    if (t >= nextBurst)
                    {
                        burstUntil = t + Range(0.4f, 2.2f);
                        nextBurst = t + Range(6f, 30f);
                    }
                    if (t < burstUntil)
                    {
                        if (stepTimer <= 0f) { stepTimer = Range(0.03f, 0.25f); level = rng.NextDouble() < 0.35 ? Range(0.6f, 1f) : 0f; }
                    }
                    else level = 0f;
                    break;
            }

            // (a surge uses its own dice, so the seeded rhythm stays the same for everyone afterwards)
            if (t < surgeUntil)
            {
                surgeStep -= Time.deltaTime;
                if (surgeStep <= 0f) { surgeStep = Random.Range(0.02f, 0.09f); surgeLevel = Random.value < 0.55f ? Random.Range(0f, 0.2f) : 1f; }
                level = Mathf.Min(level, surgeLevel);
            }
            if (lightSource != null) lightSource.intensity = baseIntensity * level;
            if (panels != null && block != null)
            {
                block.SetColor(EmissionId, emission * level);
                foreach (Renderer r in panels) if (r != null) r.SetPropertyBlock(block);
            }
            if (buzz != null)
            {
                bool unstable = level < 0.95f || t < burstUntil || t < surgeUntil;
                buzz.volume = Mathf.MoveTowards(buzz.volume, unstable ? 0.5f : 0.12f, Time.deltaTime * 4f);
            }
        }
    }
}
