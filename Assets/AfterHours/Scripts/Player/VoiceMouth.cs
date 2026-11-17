using UnityEngine;

namespace AfterHours.Player
{
    /// <summary>
    /// How loud a player is talking (voice chat), 0..1, for their mouth. Your own from your microphone; other
    /// players' from their voice as it plays. Anything can feed it via Source.
    /// </summary>
    public class VoiceMouth : MonoBehaviour
    {
        /// <summary>Raw loudness (RMS) of this player's voice right now; set by the networking.</summary>
        public System.Func<float> Source;
        /// <summary>The local player's microphone (set by the voice chat).</summary>
        public static System.Func<float> LocalSource;
        public float Open { get; private set; }
        float env;

        void Update()
        {
            if (Source == null && LocalSource != null && GetComponent<PlayerController>() is PlayerController pc && pc.isLocallyControlled) Source = LocalSource;
            float rms = Source != null ? Source() : 0f;
            // a talking mouth: quick to open on each syllable, a touch slower to close; whisper barely moves it
            float target = Mathf.Clamp01((rms - 0.01f) / 0.18f);
            env = target > env ? Mathf.Lerp(env, target, 1f - Mathf.Exp(-40f * Time.deltaTime)) : Mathf.Lerp(env, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            Open = env;
        }
    }
}
