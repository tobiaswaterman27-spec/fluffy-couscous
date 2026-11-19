using UnityEngine;
using AfterHours.Audio;
using AfterHours.Flow;

namespace AfterHours.World
{
    /// <summary>
    /// A storm outside. Rain drums on the atrium skylight and patters on the entrance glass (and is loud up on the
    /// roof), rising and easing off over minutes. Every minute or two, lightning: a cold flash through the skylight
    /// and glass (a shadowed light, so it only reaches what the sky can see), then thunder a few seconds later -
    /// a sharp crack when it's close, a long roll when it isn't. Stops for the daylight ending.
    /// </summary>
    public class Weather : MonoBehaviour
    {
        public AudioSource[] rainSources;
        public Light lightning;
        [Tooltip("Additive sky sheets seen through the skylight and the entrance glass: they flare white with each strike.")]
        public Renderer[] skyFlash;
        public float minGap = 45f, maxGap = 120f;

        /// <summary>How hard it's raining right now, 0..1 (leaks drip faster in heavy rain).</summary>
        public static float Intensity { get; private set; } = 0.6f;

        float[] baseVolume;
        float nextStrike, flashT = -1f, thunderAt = -1f;
        bool near;
        float seed;
        MaterialPropertyBlock skyBlock;
        float skyLevel = -1f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Start()
        {
            seed = Random.Range(0f, 100f);
            baseVolume = new float[rainSources.Length];
            AudioClip rain = AudioLibrary.Get("amb_rain");
            for (int i = 0; i < rainSources.Length; i++)
            {
                AudioSource s = rainSources[i];
                if (s == null) continue;
                baseVolume[i] = s.volume;
                if (rain != null) { s.clip = rain; s.loop = true; s.timeSamples = Random.Range(0, rain.samples); s.Play(); }
            }
            if (lightning != null) { lightning.enabled = false; }
            nextStrike = Time.time + Random.Range(20f, 50f);
        }

        /// <summary>The night sky through the glass: almost black, a cold white in a flash.</summary>
        void SetSky(float flash)
        {
            if (skyFlash == null || skyFlash.Length == 0 || Mathf.Abs(flash - skyLevel) < 0.005f) return;
            skyLevel = flash;
            if (skyBlock == null) skyBlock = new MaterialPropertyBlock();
            skyBlock.SetColor(BaseColorId, new Color(0.75f, 0.82f, 1f, 0.025f + 0.95f * flash));
            foreach (Renderer r in skyFlash) if (r != null) r.SetPropertyBlock(skyBlock);
        }

        void Update()
        {
            bool ending = GameFlow.Instance != null && GameFlow.Instance.State == FlowState.Ending;
            // how hard it's raining: slow swells over a couple of minutes
            float intensity = ending ? 0f : Mathf.Lerp(0.45f, 1f, Mathf.PerlinNoise(Time.time * 0.008f, seed));
            Intensity = intensity;
            for (int i = 0; i < rainSources.Length; i++)
                if (rainSources[i] != null) rainSources[i].volume = Mathf.MoveTowards(rainSources[i].volume, baseVolume[i] * intensity, Time.deltaTime * 0.2f);
            if (ending) { if (lightning != null) lightning.enabled = false; SetSky(0f); return; }

            if (Time.time >= nextStrike)
            {
                nextStrike = Time.time + Random.Range(minGap, maxGap) / Mathf.Max(0.5f, intensity);
                near = Random.value < 0.35f;
                flashT = 0f;
                thunderAt = Time.time + (near ? Random.Range(0.4f, 1.5f) : Random.Range(3f, 7f));
                if (near) AfterHours.Rendering.FluorescentFlicker.Surge(Random.Range(0.4f, 0.9f));   // the surge gets into the wiring
            }
            if (flashT >= 0f && lightning != null)
            {
                // two or three flickers in under half a second
                flashT += Time.deltaTime;
                float f = flashT < 0.06f ? 1f : flashT < 0.12f ? 0.15f : flashT < 0.2f ? 0.8f : flashT < 0.26f ? 0.1f : flashT < 0.34f ? 0.5f * (near ? 1f : 0f) : 0f;
                lightning.enabled = f > 0.01f;
                lightning.intensity = f * (near ? 2.8f : 1.6f);
                if (flashT > 0.4f) { flashT = -1f; lightning.enabled = false; }
            }
            SetSky(lightning != null && lightning.enabled ? Mathf.Clamp01(lightning.intensity / 2.8f) : 0f);
            if (thunderAt > 0f && Time.time >= thunderAt)
            {
                thunderAt = -1f;
                AudioClip c = AudioLibrary.Get(near ? "thunder_near" : "thunder_far");
                Transform cam = Camera.main != null ? Camera.main.transform : null;
                if (c != null && cam != null)
                    AudioPool.PlayAt(c, cam.position + Vector3.up * 30f, near ? 1f : 0.75f, Random.Range(0.9f, 1.05f), 300f);
            }
        }
    }
}
