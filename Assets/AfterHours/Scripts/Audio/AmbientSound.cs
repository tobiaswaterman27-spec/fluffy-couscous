using UnityEngine;

namespace AfterHours.Audio
{
    /// <summary>
    /// A looping environmental sound (ventilation, hum, machinery, fountain...). The clip is generated at run
    /// time, so scenes need no audio files. maskingLevel tells the hearing system (Part 10) how much this
    /// background noise hides quieter sounds nearby.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbientSound : MonoBehaviour
    {
        public ProceduralSounds.Ambience kind = ProceduralSounds.Ambience.Ventilation;
        [Range(0f, 1f)] public float volume = 0.3f;
        public float maxDistance = 20f;
        [Tooltip("0 = masks nothing, 1 = loud machinery that hides footsteps nearby.")]
        [Range(0f, 1f)] public float maskingLevel = 0.2f;

        static readonly System.Collections.Generic.List<AmbientSound> all = new System.Collections.Generic.List<AmbientSound>();
        public static System.Collections.Generic.IReadOnlyList<AmbientSound> All => all;
        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        /// <summary>How much background noise there is at a point (0 = silent, 1 = loud machinery).</summary>
        public static float MaskingAt(Vector3 p)
        {
            float m = 0f;
            foreach (AmbientSound a in all)
            {
                float d = Vector3.Distance(a.transform.position, p);
                if (d < a.maxDistance) m = Mathf.Max(m, a.maskingLevel * (1f - d / a.maxDistance));
            }
            return m;
        }

        void Start()
        {
            AudioSource s = GetComponent<AudioSource>();
            s.clip = ProceduralSounds.AmbienceLoop(kind);
            s.loop = true;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1f;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
            s.volume = volume;
            // Start each loop at a different point so many identical sources don't phase together.
            Vector3 p = transform.position;
            int offset = Mathf.Abs((int)(p.x * 73.1f) * 7919 + (int)(p.z * 37.7f) * 104729 + (int)(p.y * 11f));
            s.timeSamples = offset % Mathf.Max(1, s.clip.samples);
            s.Play();
            source = s;
        }

        AudioSource source;

        float lastDuck;

        void Update()
        {
            // Only touch the volume while the mix is ducking (machines that drive their own volume are left alone).
            float d = AudioMix.Duck;
            if (source == null || Mathf.Approximately(d, lastDuck)) return;
            lastDuck = d;
            source.volume = volume * (1f - 0.8f * d);
        }
    }
}
