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
            s.timeSamples = Mathf.Abs(GetInstanceID() * 7919) % Mathf.Max(1, s.clip.samples);
            s.Play();
        }
    }
}
