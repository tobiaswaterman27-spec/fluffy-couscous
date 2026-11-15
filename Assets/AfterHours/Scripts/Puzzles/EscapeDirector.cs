using UnityEngine;
using AfterHours.Controls;
using AfterHours.Player;
using AfterHours.World.Layout;

namespace AfterHours.Puzzles
{
    /// <summary>
    /// Runs the escape: the player's controls stop, the view fades to white, and the ending begins.
    /// Part 13 replaces the placeholder text with the full daylight ending scene; Part 17 adds the co-op
    /// five-second rule (everyone must get out together).
    /// </summary>
    public class EscapeDirector : MonoBehaviour
    {
        static EscapeDirector active;

        EscapeRouteId route;
        float t;
        GUIStyle style;

        public static bool Escaping => active != null;

        public static void BeginEscape(EscapeRouteId route, PlayerController player)
        {
            if (active != null) return;
            var go = new GameObject("[Escape]");
            active = go.AddComponent<EscapeDirector>();
            active.route = route;
            InputService.Instance?.SetGameplayEnabled(false);
            Debug.Log("[AfterHours] Escaped via route " + (int)route + " (" + route + ")");
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
        }

        void OnDestroy()
        {
            if (active == this) active = null;
        }

        void OnGUI()
        {
            float fade = Mathf.Clamp01(t / 3f);
            GUI.color = new Color(1f, 1f, 1f, fade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            if (t < 4f) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = new Color(0.25f, 0.25f, 0.27f);
            }
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((t - 4f) / 2f));
            GUI.Label(new Rect(0f, Screen.height * 0.45f, Screen.width, 40f), "Daylight.", style);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active = null;
    }
}
