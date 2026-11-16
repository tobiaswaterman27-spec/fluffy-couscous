using System.Collections.Generic;
using UnityEngine;

namespace AfterHours.Audio
{
    /// <summary>Reusable pool of 3D audio sources so one-shot world sounds never allocate new objects.</summary>
    public static class AudioPool
    {
        const int Size = 24;
        static readonly List<AudioSource> sources = new List<AudioSource>();
        static Transform root;
        static int next;

        public static void PlayAt(AudioClip clip, Vector3 position, float volume, float pitch = 1f, float maxDistance = 40f)
        {
            if (clip == null) return;
            Ensure();
            AudioSource s = sources[next];
            next = (next + 1) % sources.Count;
            s.Stop();
            s.transform.position = position;
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume);
            s.pitch = pitch;
            s.maxDistance = maxDistance;
            // Muffle one-shot sounds that start behind walls.
            AudioLowPassFilter lp = s.GetComponent<AudioLowPassFilter>();
            float occ = AudioOcclusion.Occlusion(position);
            lp.cutoffFrequency = Mathf.Lerp(22000f, 900f, occ);
            s.volume = Mathf.Clamp01(volume) * (1f - 0.35f * occ);
            s.Play();
        }

        static readonly List<AudioSource> flat = new List<AudioSource>();
        static int nextFlat;

        /// <summary>A sound in your head, not in the world (scare hits, the scream right in your face).</summary>
        public static void Play2D(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null) return;
            Ensure();
            AudioSource s = flat[nextFlat];
            nextFlat = (nextFlat + 1) % flat.Count;
            s.Stop();
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume);
            s.pitch = pitch;
            s.Play();
        }

        static void Ensure()
        {
            if (root != null) return;
            sources.Clear();
            flat.Clear();
            next = 0;
            nextFlat = 0;
            var go = new GameObject("[Audio Pool]");
            Object.DontDestroyOnLoad(go);
            root = go.transform;
            for (int i = 0; i < 4; i++)
            {
                var f = new GameObject("Pooled2D" + i);
                f.transform.SetParent(go.transform, false);
                AudioSource fs = f.AddComponent<AudioSource>();
                fs.playOnAwake = false;
                fs.spatialBlend = 0f;
                flat.Add(fs);
            }
            for (int i = 0; i < Size; i++)
            {
                var child = new GameObject("PooledSource" + i);
                child.transform.SetParent(root, false);
                var s = child.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = 1f;
                s.maxDistance = 40f;
                s.dopplerLevel = 0f;
                child.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
                sources.Add(s);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            root = null;
            sources.Clear();
            next = 0;
        }
    }
}
