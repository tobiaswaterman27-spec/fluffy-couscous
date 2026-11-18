using UnityEngine;
using AfterHours.Flow;

namespace AfterHours.World
{
    /// <summary>
    /// The big hanging clock in the atrium: two faces, real hands, telling the night's time from midnight
    /// (one real minute is two and a half minutes of night). The minute hand steps forward with a small tick.
    /// </summary>
    public class MallClock : MonoBehaviour
    {
        public Transform[] hourHands;
        public Transform[] minuteHands;
        [Tooltip("+1 / -1 per hand pair: which way is clockwise for someone looking at that face.")]
        public float[] faceSign;

        void Update()
        {
            float night = 0f;
            if (NightDirector.Instance != null && !AfterHours.Net.NetSession.IsClient) night = NightDirector.Instance.NightTime;
            else if (GameFlow.Instance != null) night = GameFlow.Instance.PlayTime;
            float minutes = night * NightDirector.ClockScale / 60f;          // since midnight
            float shown = Mathf.Floor(minutes);                                 // the minute hand jumps once a minute
            float tick = Mathf.Clamp01((minutes - shown) * 12f);                // ...with a quick little settle
            float minuteAngle = (shown + Mathf.SmoothStep(0f, 1f, tick)) / 60f * 360f;
            float hourAngle = (minutes / 720f) * 360f;
            for (int i = 0; i < hourHands.Length; i++)
            {
                float s = faceSign != null && i < faceSign.Length ? faceSign[i] : 1f;
                if (hourHands[i] != null) hourHands[i].localRotation = Quaternion.Euler(0f, 0f, s * hourAngle);
                if (minuteHands[i] != null) minuteHands[i].localRotation = Quaternion.Euler(0f, 0f, s * minuteAngle);
            }
        }
    }
}
