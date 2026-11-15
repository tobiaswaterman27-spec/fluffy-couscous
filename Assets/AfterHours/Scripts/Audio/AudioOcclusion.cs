using UnityEngine;
using AfterHours.Core;

namespace AfterHours.Audio
{
    /// <summary>
    /// Muffles a sound when walls or closed doors are between it and the listener (a low-pass filter plus a little
    /// volume drop), and clears it when there is a direct line. Put on looping sources (breathing, machinery);
    /// one-shot pooled sounds get the same check when they start.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioOcclusion : MonoBehaviour
    {
        public float openCutoff = 22000f;
        public float blockedCutoff = 900f;

        AudioLowPassFilter filter;
        AudioSource source;
        float baseVolume = -1f;
        float target = 1f;
        float timer;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            filter = GetComponent<AudioLowPassFilter>();
            if (filter == null) filter = gameObject.AddComponent<AudioLowPassFilter>();
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = 0.12f;
                target = 1f - Occlusion(transform.position);
            }
            float cur = Mathf.InverseLerp(blockedCutoff, openCutoff, filter.cutoffFrequency);
            cur = Mathf.MoveTowards(cur, target, Time.deltaTime * 3f);
            filter.cutoffFrequency = Mathf.Lerp(blockedCutoff, openCutoff, cur * cur);
            if (baseVolume < 0f) baseVolume = source.volume;
        }

        static readonly RaycastHit[] hits = new RaycastHit[6];

        /// <summary>0 = clear line to the listener, 1 = heavily blocked.</summary>
        public static float Occlusion(Vector3 from)
        {
            AudioListener listener = Object.FindAnyObjectByType<AudioListener>();
            if (listener == null) return 0f;
            Vector3 to = listener.transform.position;
            Vector3 d = to - from;
            int mask = GameLayers.CharacterBlockingMask;
            foreach (int l in new[] { GameLayers.PlayerLayer, GameLayers.CreatureLayer, GameLayers.PropLayer })
            {
                if (l >= 0) mask &= ~(1 << l);
            }
            int n = Physics.RaycastNonAlloc(from, d.normalized, hits, d.magnitude, mask, QueryTriggerInteraction.Ignore);
            return Mathf.Clamp01(n * 0.45f);
        }
    }
}
