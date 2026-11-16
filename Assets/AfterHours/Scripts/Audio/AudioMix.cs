using UnityEngine;

namespace AfterHours.Audio
{
    /// <summary>
    /// The mix's one big lever: when something frightening happens (it sees you, it grabs you), the background -
    /// hum, air, music bed - drops away for a moment so the scare hits with nothing around it.
    /// </summary>
    public static class AudioMix
    {
        static float duck, holdUntil;
        public static float Duck => duck;

        /// <summary>Drop the background by 'amount' (0..1) for 'seconds', then let it come back.</summary>
        public static void Kick(float amount, float seconds)
        {
            duck = Mathf.Max(duck, Mathf.Clamp01(amount));
            holdUntil = Mathf.Max(holdUntil, Time.unscaledTime + seconds);
        }

        /// <summary>Called every frame by the music system.</summary>
        public static void Tick(float dt)
        {
            if (Time.unscaledTime > holdUntil) duck = Mathf.MoveTowards(duck, 0f, dt * 0.4f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { duck = 0f; holdUntil = 0f; }
    }
}
