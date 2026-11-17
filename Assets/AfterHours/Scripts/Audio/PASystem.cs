using System.Collections.Generic;
using UnityEngine;

namespace AfterHours.Audio
{
    /// <summary>
    /// The mall's public address. Every so often, with the power on, the old two-tone chime plays through the
    /// ceiling speakers... and then nobody speaks. Just an open mic: hiss, crackle, a hum, for a few seconds too
    /// long. Then a click. (Sounds are synthesised here; it plays through the speakers nearest the listener.)
    /// </summary>
    public class PASystem : MonoBehaviour
    {
        public Vector3[] speakers;
        public float minInterval = 360f, maxInterval = 840f;
        float next;
        readonly List<AudioSource> outs = new List<AudioSource>();
        static AudioClip clip;

        void Start()
        {
            next = Time.time + Random.Range(minInterval * 0.5f, maxInterval);
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("PA out " + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 4f; s.maxDistance = 45f;
                s.playOnAwake = false; s.volume = 0.5f; s.dopplerLevel = 0f;
                outs.Add(s);
            }
        }

        void Update()
        {
            if (Time.time < next || speakers == null || speakers.Length == 0) return;
            next = Time.time + Random.Range(minInterval, maxInterval);
            if (!AfterHours.Power.PowerGrid.IsPowered(AfterHours.Power.PowerGrid.Main)) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Play(cam.transform.position);
        }

        public void Play(Vector3 listener)
        {
            // the four speakers nearest the listener (small delays between them read as a big space)
            var order = new List<Vector3>(speakers);
            order.Sort((a, b) => (a - listener).sqrMagnitude.CompareTo((b - listener).sqrMagnitude));
            AudioClip c = Clip();
            for (int i = 0; i < outs.Count && i < order.Count; i++)
            {
                outs[i].transform.position = order[i];
                outs[i].clip = c;
                outs[i].pitch = 1f;
                outs[i].PlayDelayed(i * 0.035f);
            }
        }

        static AudioClip Clip()
        {
            if (clip != null) return clip;
            const int rate = 22050;
            float len = 10.5f;
            int n = (int)(len * rate);
            var d = new float[n];
            var rng = new System.Random(5150);
            float lp = 0f, hum = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float s = 0f;
                // click on, then the chime: two bell tones (E5 then C5) with partials, a little sour
                if (t > 0.15f && t < 0.17f) s += 0.5f * (float)(rng.NextDouble() - 0.5);
                s += Bell(t - 0.35f, 659.3f) + Bell(t - 1.05f, 523.3f * 1.004f);
                // open mic: hiss + crackle + mains hum, then a click off
                float mic = t > 1.6f && t < 10.1f ? 1f : 0f;
                float hiss = (float)(rng.NextDouble() - 0.5) * 0.05f;
                lp += (hiss - lp) * 0.3f;
                hum = 0.012f * Mathf.Sin(2f * Mathf.PI * 50f * t) + 0.006f * Mathf.Sin(2f * Mathf.PI * 150f * t);
                float crackle = rng.NextDouble() < 0.002 ? (float)(rng.NextDouble() - 0.5) * 0.5f : 0f;
                // somewhere far off in it, something like breathing: two slow swells of filtered noise
                float breath = (Swell(t, 4.2f) + Swell(t, 7.4f)) * lp * 2.5f;
                s += mic * (lp + hum + crackle + breath);
                if (t > 10.1f && t < 10.12f) s += 0.5f * (float)(rng.NextDouble() - 0.5);
                // tinny speaker: soft clip
                d[i] = Mathf.Clamp(s * 1.2f, -0.8f, 0.8f);
            }
            clip = AudioClip.Create("pa_chime", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static float Bell(float t, float f)
        {
            if (t < 0f) return 0f;
            float env = Mathf.Exp(-t * 2.2f) * Mathf.Clamp01(t / 0.005f);
            return env * 0.22f * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.45f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t * 3f)
                                 + 0.25f * Mathf.Sin(2f * Mathf.PI * f * 5.4f * t) * Mathf.Exp(-t * 6f));
        }

        static float Swell(float t, float at)
        {
            float x = (t - at) / 0.9f;
            return Mathf.Exp(-x * x);
        }
    }
}
