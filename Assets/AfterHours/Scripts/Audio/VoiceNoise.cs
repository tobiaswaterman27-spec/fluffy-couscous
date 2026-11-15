using UnityEngine;

namespace AfterHours.Audio
{
    /// <summary>
    /// Turns a player's voice loudness into noise Elliot can hear (wired to the microphone in Part 15).
    /// Whispering is only audible very close; normal talk at a moderate range; shouting far; screaming gives you
    /// away. Deliberately not over-sensitive: ordinary microphone noise is ignored, and only the loudest moment in
    /// each half-second counts.
    /// </summary>
    public class VoiceNoise : MonoBehaviour
    {
        [Tooltip("Microphone level (0..1, RMS) below which nothing counts (room noise, breathing).")]
        public float gate = 0.02f;
        float peak;
        float timer;

        /// <summary>Feed the current voice level (0..1) every frame.</summary>
        public void Feed(float level)
        {
            peak = Mathf.Max(peak, level);
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.5f;
            float l = peak;
            peak = 0f;
            if (l < gate) return;
            float radius = RadiusFor(l);
            NoiseBus.Emit(new NoiseEvent(transform.position + Vector3.up * 1.5f, radius, NoiseKind.Voice, gameObject));
        }

        /// <summary>whisper ~2.5 m, talking ~8 m, shouting ~20 m, screaming ~35 m.</summary>
        public static float RadiusFor(float level)
        {
            if (level < 0.06f) return 2.5f;
            if (level < 0.2f) return Mathf.Lerp(4f, 9f, Mathf.InverseLerp(0.06f, 0.2f, level));
            if (level < 0.45f) return Mathf.Lerp(12f, 22f, Mathf.InverseLerp(0.2f, 0.45f, level));
            return 35f;
        }
    }
}
