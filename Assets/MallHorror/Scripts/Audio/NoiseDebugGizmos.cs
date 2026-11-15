using UnityEngine;

namespace MallHorror.Audio
{
    /// <summary>Draws recent noise events as fading spheres in the Scene view (and Game view with Gizmos on).</summary>
    public class NoiseDebugGizmos : MonoBehaviour
    {
        public float showSeconds = 2.5f;

        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            for (int i = 0; i < NoiseBus.RecentCount; i++)
            {
                NoiseEvent e = NoiseBus.GetRecent(i);
                float age = Time.time - e.Timestamp;
                if (age > showSeconds || age < 0f) continue;
                float a = 1f - age / showSeconds;
                Gizmos.color = new Color(1f, 0.4f, 0.1f, a * 0.8f);
                Gizmos.DrawWireSphere(e.Position, e.Radius);
                Gizmos.DrawSphere(e.Position, 0.08f);
            }
        }
    }
}
