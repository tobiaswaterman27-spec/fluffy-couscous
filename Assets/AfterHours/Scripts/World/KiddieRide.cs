using UnityEngine;
using AfterHours.Audio;

namespace AfterHours.World
{
    /// <summary>
    /// The coin-operated ride by the atrium. Once in a long while, with nobody near it, it starts on its own:
    /// rocking, its bulbs chasing, a tinny jingle warbling out of a blown speaker. It's loud, and Elliot hears it
    /// too. It needs power. Its tune is synthesised once, here.
    /// </summary>
    public class KiddieRide : MonoBehaviour
    {
        public Transform car;
        public Light bulbs;
        public float chancePerMinute = 0.08f;
        AudioSource src;
        float runUntil = -1f, nextRoll, noiseTimer, baseIntensity;
        Vector3 carRest;
        Quaternion carRot;
        static AudioClip tune;

        void Start()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 2f; src.maxDistance = 40f;
            src.loop = true; src.volume = 0.55f; src.playOnAwake = false;
            src.clip = Tune();
            if (car != null) { carRest = car.localPosition; carRot = car.localRotation; }
            if (bulbs != null) { baseIntensity = bulbs.intensity; bulbs.intensity = 0f; }
            nextRoll = Time.time + 120f;
        }

        void Update()
        {
            bool powered = AfterHours.Power.PowerGrid.LightFactorAt(transform.position) > 0.5f;
            Camera cam = Camera.main;
            float d = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 999f;
            if (runUntil < 0f)
            {
                if (Time.time >= nextRoll)
                {
                    nextRoll = Time.time + 60f;
                    if (powered && d > 8f && d < 45f && Random.value < chancePerMinute) Begin();
                }
                return;
            }
            if (!powered || Time.time > runUntil) { Stop(); return; }

            float t = Time.time;
            // a slightly lopsided rock, slowing and catching as the old motor struggles
            float wob = Mathf.Sin(t * 2.6f) * 7f + Mathf.Sin(t * 5.3f) * 1.5f;
            if (car != null)
            {
                car.localRotation = carRot * Quaternion.Euler(wob, 0f, Mathf.Sin(t * 1.3f) * 2f);
                car.localPosition = carRest + Vector3.up * Mathf.Abs(Mathf.Sin(t * 2.6f)) * 0.04f;
            }
            if (bulbs != null) bulbs.intensity = baseIntensity * (Mathf.Repeat(t * 3f, 1f) < 0.55f ? 1f : 0.25f);
            src.pitch = 0.94f + 0.04f * Mathf.Sin(t * 0.7f) + 0.015f * Mathf.Sin(t * 5.1f);   // tape wow
            noiseTimer -= Time.deltaTime;
            if (noiseTimer <= 0f)
            {
                noiseTimer = 2f;
                NoiseBus.Emit(new NoiseEvent(transform.position, 28f, NoiseKind.Machinery, gameObject));
            }
        }

        void Begin()
        {
            runUntil = Time.time + Random.Range(18f, 32f);
            src.time = 0f;
            src.Play();
        }

        void Stop()
        {
            runUntil = -1f;
            src.Stop();
            if (car != null) { car.localRotation = carRot; car.localPosition = carRest; }
            if (bulbs != null) bulbs.intensity = 0f;
        }

        /// <summary>A nursery-rhyme-ish loop on a cheap square-wave chip through a blown speaker.</summary>
        static AudioClip Tune()
        {
            if (tune != null) return tune;
            const int rate = 22050;
            // melody: (semitones from C5, beats); slightly sour on purpose
            int[] notes = { 0, 0, 7, 7, 9, 9, 7, -99, 5, 5, 4, 4, 2, 2, 0, -99, 7, 7, 5, 5, 4, 4, 2, -99, 7, 7, 5, 5, 4, 4, 2, -99 };
            float beat = 0.32f;
            int n = (int)(notes.Length * beat * rate);
            var data = new float[n];
            var rng = new System.Random(77);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float time = i / (float)rate;
                int k = Mathf.Min(notes.Length - 1, (int)(time / beat));
                float inNote = time - k * beat;
                float s = 0f;
                if (notes[k] > -50)
                {
                    float f = 523.25f * Mathf.Pow(2f, (notes[k] + (k % 5 == 3 ? 0.35f : 0f)) / 12f);
                    phase += f / rate;
                    float sq = (phase % 1f) < 0.5f ? 1f : -1f;
                    float env = Mathf.Clamp01(inNote / 0.01f) * Mathf.Exp(-inNote * 4f);
                    s = sq * env * 0.5f;
                }
                // blown speaker: clipped, buzzing, band-limited, with crackle
                s = Mathf.Clamp(s * 1.8f, -0.6f, 0.6f);
                lp += (s - lp) * 0.25f;
                float crackle = rng.NextDouble() < 0.0015 ? (float)(rng.NextDouble() - 0.5) * 0.8f : 0f;
                data[i] = lp * 0.8f + crackle + (float)(rng.NextDouble() - 0.5) * 0.01f;
            }
            tune = AudioClip.Create("kiddie_ride_tune", n, 1, rate, false);
            tune.SetData(data, 0);
            return tune;
        }
    }
}
