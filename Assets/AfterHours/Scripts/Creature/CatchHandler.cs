using UnityEngine;
using AfterHours.Controls;
using AfterHours.Player;
using AfterHours.Puzzles;
using AfterHours.World.Layout;

namespace AfterHours.Creature
{
    /// <summary>
    /// What happens when Elliot catches a player. PLACEHOLDER for Part 11, which replaces it with the full
    /// context-sensitive grab / scream / eat sequence (seen in first person, identical for teammates).
    /// For now: it grabs and screams, the screen cuts to black, and the run resets (single-player rules).
    /// </summary>
    public class CatchHandler : MonoBehaviour
    {
        ElliotBrain brain;
        PlayerController player;
        float t;
        bool resetDone;

        public static bool Active { get; private set; }

        public static void Begin(ElliotBrain brain, PlayerController player)
        {
            if (Active) return;
            Active = true;
            var go = new GameObject("[Caught]");
            CatchHandler h = go.AddComponent<CatchHandler>();
            h.brain = brain;
            h.player = player;
            InputService.Instance?.SetGameplayEnabled(false);
            brain.animator.PlayAction(ElliotAction.Scream, player.EyePosition, 1.6f);
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t > 1.6f && !resetDone)
            {
                resetDone = true;
                ResetRun();
            }
            if (t > 3.2f)
            {
                InputService.Instance?.SetGameplayEnabled(true);
                Active = false;
                Destroy(gameObject);
            }
        }

        void ResetRun()
        {
            DevLocation start = MallLayout.DevLocations[0];
            if (player != null) player.Teleport(start.position, start.yaw);
            foreach (MonoBehaviour m in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (m is IResettable r) r.ResetState();
            }
            if (brain != null) brain.ResetToStart(new Vector3(30f, MallLayout.CarParkY + 0.05f, 70f), 180f);
        }

        void OnGUI()
        {
            if (t < 1.4f) return;
            float a = t < 1.6f ? (t - 1.4f) / 0.2f : t > 2.6f ? Mathf.Clamp01(1f - (t - 2.6f) / 0.6f) : 1f;
            GUI.color = new Color(0f, 0f, 0f, a);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = false;
    }
}
