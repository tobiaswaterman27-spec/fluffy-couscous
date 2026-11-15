using UnityEngine;
using AfterHours.World.Layout;

namespace AfterHours.World
{
    /// <summary>Marks the final exit point of one of the eight escape routes. Puzzles are added in Part 7.</summary>
    public class EscapeRouteMarker : MonoBehaviour
    {
        public EscapeRouteId route;
        [TextArea] public string note = "";

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(1.5f, 2f, 1.5f));
        }
    }
}
