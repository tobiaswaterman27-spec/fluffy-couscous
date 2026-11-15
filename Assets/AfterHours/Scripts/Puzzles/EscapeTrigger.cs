using UnityEngine;
using AfterHours.Player;
using AfterHours.World.Layout;

namespace AfterHours.Puzzles
{
    /// <summary>The way out of one escape route. Live only once that route's final sequence is solved.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class EscapeTrigger : MonoBehaviour, IResettable
    {
        public EscapeRouteId route;
        public bool open;
        [Tooltip("Optional: only usable while this vent / hatch cover is off.")]
        public AfterHours.Interaction.VentCover requiresOpenCover;

        void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        void OnTriggerStay(Collider other) => OnTriggerEnter(other);

        void OnTriggerEnter(Collider other)
        {
            if (!open) return;
            if (requiresOpenCover != null && !requiresOpenCover.IsOpen) return;
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.isLocallyControlled) return;
            EscapeDirector.BeginEscape(route, player);
        }

        public void ResetState() => open = false;
    }
}
