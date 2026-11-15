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

        void Start()
        {
            if (breathClip == null) breathClip = MakeBreath();
            if (screamClip == null) screamClip = MakeScream();
            if (breathing != null)
            {
                breathing.clip = breathClip;
                breathing.loop = true;
                breathing.spatialBlend = 1f;
                breathing.rolloffMode = AudioRolloffMode.Logarithmic;
                breathing.minDistance = 1.2f;
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
            if (breathing == null) return;
            breathing.pitch = Mathf.Lerp(0.85f, 1.35f, hunting);
            breathing.volume = Mathf.Lerp(0.3f, 0.6f, hunting);
        }

        public void Footstep(Vector3 position, SurfaceType surface, float weight)
        {
            AudioClip clip = ProceduralSounds.Impact(surface == SurfaceType.Carpet ? SurfaceType.Carpet : SurfaceType.Concrete);
            AudioPool.PlayAt(clip, position, Mathf.Clamp01(footstepVolume * weight), Random.Range(0.55f, 0.7f), 45f);
            NoiseBus.Emit(new NoiseEvent(position, 10f * weight, NoiseKind.Creature, gameObject));
        }

        public void Scream()
        {
            if (voice != null) voice.PlayOneShot(screamClip, 1f);
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
