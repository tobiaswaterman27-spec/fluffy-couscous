using UnityEngine;
using UnityEngine.InputSystem;
using AfterHours.Core;

namespace AfterHours.Controls
{
    /// <summary>
    /// Controller rumble (Settings > Controller rumble). A slow double thump like a heartbeat when Elliot is near
    /// (faster the closer he is), a knock for each of his footsteps close by, heavy jolts when a door is hammered
    /// near you, thunder overhead, and the hits of being caught. Only the local player's gamepad; nothing on keyboard.
    /// </summary>
    public class Rumble : MonoBehaviour
    {
        static Rumble instance;
        static float low, high;          // one-off pulses (decaying)
        float beatT;
        bool applied;

        static bool On => AfterHours.Settings.SettingsService.CurrentOrDefault.rumble && Gamepad.current != null;

        /// <summary>A jolt: low = the big heavy motor, high = the small buzzy one (0..1 each).</summary>
        public static void Pulse(float lowAmount, float highAmount)
        {
            if (!On) return;
            Ensure();
            low = Mathf.Max(low, Mathf.Clamp01(lowAmount));
            high = Mathf.Max(high, Mathf.Clamp01(highAmount));
        }

        /// <summary>A jolt from somewhere in the world, fading with distance from your ears.</summary>
        public static void At(Vector3 position, float lowAmount, float highAmount, float range)
        {
            if (!On) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            float k = 1f - Vector3.Distance(cam.transform.position, position) / Mathf.Max(0.1f, range);
            if (k <= 0f) return;
            Pulse(lowAmount * k * k, highAmount * k * k);
        }

        static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("[Rumble]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Rumble>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => Ensure();

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Gamepad pad = Gamepad.current;
            if (pad == null) return;
            if (!AfterHours.Settings.SettingsService.CurrentOrDefault.rumble || GamePause.IsPaused || !Application.isFocused)
            {
                if (applied) { pad.SetMotorSpeeds(0f, 0f); applied = false; }
                low = high = 0f;
                return;
            }
            // his closeness: a heartbeat in your hands
            float nearest = 999f;
            Camera cam = Camera.main;
            if (cam != null)
                foreach (AfterHours.Creature.ElliotBrain b in AfterHours.Creature.ElliotBrain.All)
                    if (b != null && b.gameObject.activeInHierarchy) nearest = Mathf.Min(nearest, Vector3.Distance(b.transform.position, cam.transform.position));
            float close = Mathf.Clamp01(Mathf.InverseLerp(16f, 2f, nearest));
            float beat = 0f;
            if (close > 0f)
            {
                beatT += dt * Mathf.Lerp(1.0f, 2.4f, close);          // ~60 to ~145 beats a minute
                float ph = Mathf.Repeat(beatT, 1f);
                beat = (ph < 0.08f ? 1f : ph > 0.22f && ph < 0.28f ? 0.6f : 0f) * Mathf.Lerp(0.15f, 0.55f, close);
            }
            float L = Mathf.Clamp01(Mathf.Max(low, beat)), H = Mathf.Clamp01(high);
            pad.SetMotorSpeeds(L, H);
            applied = L > 0f || H > 0f;
            low = Mathf.MoveTowards(low, 0f, dt * 3.5f);
            high = Mathf.MoveTowards(high, 0f, dt * 5f);
        }

        void OnDisable() { if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f); }
        void OnApplicationQuit() { if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; low = high = 0f; }
    }
}
