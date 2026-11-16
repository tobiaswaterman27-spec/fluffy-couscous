using UnityEngine;
using AfterHours.Audio;
using AfterHours.World;

namespace AfterHours.Creature
{
    /// <summary>
    /// Elliot's sounds, all fully 3D so you can tell direction and distance: heavy echoing footsteps fired by the
    /// animator exactly when a foot lands, slow wet breathing that quickens when hunting, and vocal sounds.
    /// Footsteps are also real noise events (other systems can use them; Elliot ignores its own).
    /// </summary>
    public class ElliotAudio : MonoBehaviour
    {
        public AudioSource breathing;
        public AudioSource voice;
        public float footstepVolume = 0.9f;

        static AudioClip breathClip, screamClip;
        AudioSource huntBreath;
        float intensity, voiceTimer = 8f;

        void Start()
        {
            if (breathClip == null) breathClip = AudioLibrary.Get("elliot_breath_calm") ?? MakeBreath();
            if (screamClip == null) screamClip = MakeScream();
            if (breathing != null)
            {
                // Two breaths crossfaded: slow and wet when calm, fast and growling when hunting.
                AudioClip hunt = AudioLibrary.Get("elliot_breath_hunt");
                if (hunt != null)
                {
                    huntBreath = breathing.gameObject.AddComponent<AudioSource>();
                    huntBreath.clip = hunt;
                    huntBreath.loop = true;
                    huntBreath.spatialBlend = 1f;
                    huntBreath.rolloffMode = AudioRolloffMode.Logarithmic;
                    huntBreath.minDistance = 1.5f;
                    huntBreath.maxDistance = 30f;
                    huntBreath.volume = 0f;
                    huntBreath.timeSamples = Random.Range(0, hunt.samples);
                    huntBreath.Play();
                }
                breathing.clip = breathClip;
                breathing.loop = true;
                breathing.spatialBlend = 1f;
                breathing.rolloffMode = AudioRolloffMode.Logarithmic;
                breathing.minDistance = 2.5f; // right behind you, it's loud
                breathing.maxDistance = 22f;
                breathing.volume = 0.35f;
                breathing.Play();
            }
            if (voice != null)
            {
                voice.spatialBlend = 1f;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;
                voice.minDistance = 2f;
                voice.maxDistance = 60f;
            }
        }

        public void SetIntensity(float hunting)
        {
            intensity = Mathf.MoveTowards(intensity, hunting, Time.deltaTime * 0.8f);
            if (breathing == null) return;
            if (huntBreath != null)
            {
                breathing.pitch = Mathf.Lerp(0.95f, 1.1f, intensity);
                breathing.volume = Mathf.Lerp(0.4f, 0.05f, intensity);
                huntBreath.volume = Mathf.Lerp(0f, 0.7f, intensity);
            }
            else
            {
                breathing.pitch = Mathf.Lerp(0.85f, 1.35f, intensity);
                breathing.volume = Mathf.Lerp(0.3f, 0.6f, intensity);
            }
        }

        void Update()
        {
            // Its voice: rare throat clicks while wandering, clicking and sniffing while searching, growls in a hunt.
            voiceTimer -= Time.deltaTime;
            if (voiceTimer > 0f || voice == null) return;
            string key;
            float r = Random.value;
            if (intensity > 0.7f) { key = r < 0.65f ? "elliot_growl" : "elliot_clicks"; voiceTimer = Random.Range(4f, 9f); }
            else if (intensity > 0.2f) { key = r < 0.4f ? "elliot_sniff" : r < 0.75f ? "elliot_clicks" : "elliot_growl"; voiceTimer = Random.Range(5f, 10f); }
            else { key = r < 0.4f ? "elliot_growl" : r < 0.8f ? "elliot_clicks" : "elliot_twitch"; voiceTimer = Random.Range(10f, 24f); }
            AudioClip c = AudioLibrary.Get(key);
            if (c != null) voice.PlayOneShot(c, key == "elliot_twitch" ? 0.6f : 0.85f);
        }

        /// <summary>It has seen you: a short rising screech, loud, from where it stands.</summary>
        public void Spot()
        {
            AudioClip c = AudioLibrary.Get("elliot_spot");
            if (c != null && voice != null) voice.PlayOneShot(c, 1f);
            voiceTimer = Random.Range(3f, 5f);
        }

        /// <summary>Its joints cracking (the animator's twitches).</summary>
        public void Twitch()
        {
            AudioClip c = AudioLibrary.Get("elliot_twitch");
            if (c != null && voice != null) voice.PlayOneShot(c, 0.5f);
        }

        public void Footstep(Vector3 position, SurfaceType surface, float weight)
        {
            string kind = surface == SurfaceType.Carpet ? "carpet" : surface == SurfaceType.Metal || surface == SurfaceType.VentMetal ? "metal" : "hard";
            AudioClip clip = AudioLibrary.Get("elliot_step_" + kind);
            // Running: heavier, louder, carrying further - you hear the gap closing.
            float run = Mathf.InverseLerp(0.7f, 1f, weight);
            if (clip != null) AudioPool.PlayAt(clip, position, Mathf.Clamp01(footstepVolume * weight * (1f + 0.4f * run)), Random.Range(0.92f, 1.05f) - 0.06f * run, 45f + 25f * run);
            else AudioPool.PlayAt(ProceduralSounds.Impact(surface == SurfaceType.Carpet ? SurfaceType.Carpet : SurfaceType.Concrete), position, Mathf.Clamp01(footstepVolume * weight), Random.Range(0.55f, 0.7f), 45f);
            NoiseBus.Emit(new NoiseEvent(position, 10f * weight, NoiseKind.Creature, gameObject));
        }

        public void Scream()
        {
            AudioClip s = AudioLibrary.Get("elliot_scream");
            if (voice != null) voice.PlayOneShot(s != null ? s : screamClip, 1f);
            NoiseBus.Emit(new NoiseEvent(transform.position, 40f, NoiseKind.Creature, gameObject));
        }

        // Slow, wet, rasping breath: filtered noise shaped into inhale / exhale with a throaty rattle.
        static AudioClip MakeBreath()
        {
            const int rate = 44100;
            float seconds = 4f;
            int n = (int)(seconds * rate);
            float[] d = new float[n];
            var rng = new System.Random(77);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float cycle = t % 2f;
                float env = cycle < 0.9f ? Mathf.Sin(cycle / 0.9f * Mathf.PI) : (cycle < 1.1f ? 0f : Mathf.Sin((cycle - 1.1f) / 0.9f * Mathf.PI) * 0.8f);
                float white = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.12f * (white - lp);
                lp2 += 0.05f * (white - lp2);
                float rattle = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 38f * t));
                d[i] = (lp * 0.9f + lp2 * 1.5f * rattle) * env;
            }
            float peak = 0.0001f;
            foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < n; i++) d[i] *= 0.8f / peak;
            AudioClip c = AudioClip.Create("Elliot_Breath", n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }

        // A harsh, rising, tearing shriek (detuned saw stack + noise, with a ragged tremolo).
        static AudioClip MakeScream()
        {
            const int rate = 44100;
            float seconds = 1.8f;
            int n = (int)(seconds * rate);
            float[] d = new float[n];
            var rng = new System.Random(666);
            float[] ph = new float[5];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f = Mathf.Lerp(260f, 520f, Mathf.Clamp01(t / 0.5f)) * (1f + 0.03f * Mathf.Sin(2f * Mathf.PI * 7f * t));
                float s = 0f;
                for (int k = 0; k < ph.Length; k++)
                {
                    ph[k] += f * (1f + (k - 2) * 0.013f) / rate;
                    s += (ph[k] % 1f) * 2f - 1f;
                }
                float noise = (float)(rng.NextDouble() * 2 - 1);
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((seconds - t) / 0.4f);
                float trem = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 23f * t + noise * 0.5f);
                d[i] = (s / ph.Length * 0.8f + noise * 0.35f) * env * trem;
            }
            AudioClip c = AudioClip.Create("Elliot_Scream", n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
