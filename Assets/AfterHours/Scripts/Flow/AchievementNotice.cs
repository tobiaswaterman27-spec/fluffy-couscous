using System.Collections.Generic;
using UnityEngine;

namespace AfterHours.Flow
{
    /// <summary>The little "Achievement unlocked" notice in the top right corner.</summary>
    public class AchievementNotice : MonoBehaviour
    {
        static AchievementNotice instance;
        readonly Queue<string> queue = new Queue<string>();
        string current;
        float t;
        GUIStyle head, body;

        public static void Show(string title)
        {
            if (instance == null)
            {
                var go = new GameObject("[Achievements]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<AchievementNotice>();
            }
            instance.queue.Enqueue(title);
        }

        void Update()
        {
            if (current == null && queue.Count > 0) { current = queue.Dequeue(); t = 0f; }
            if (current == null) return;
            t += Time.unscaledDeltaTime;
            if (t > 5f) current = null;
        }

        void OnGUI()
        {
            if (current == null) return;
            if (head == null)
            {
                head = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperLeft };
                head.normal.textColor = new Color(0.88f, 0.64f, 0.23f);
                body = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Bold };
                body.normal.textColor = new Color(0.92f, 0.9f, 0.85f);
            }
            float a = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((5f - t) / 0.6f);
            float slide = (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.4f))) * 40f;
            Rect r = new Rect(Screen.width - 360f + slide, 30f, 330f, 70f);
            GUI.color = new Color(0.05f, 0.05f, 0.06f, 0.82f * a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(r.x + 16f, r.y + 10f, r.width, 20f), "ACHIEVEMENT", head);
            GUI.Label(new Rect(r.x + 16f, r.y + 30f, r.width, 30f), current, body);
            GUI.color = Color.white;
        }
    }
}
