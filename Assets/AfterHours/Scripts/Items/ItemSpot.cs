using UnityEngine;

namespace AfterHours.Items
{
    /// <summary>
    /// A place an item might be lying at the start of a run. Placed by the mall builder: several in every
    /// back-of-house room, on shop counters and in stock rooms. Each belongs to a group named after the lock you
    /// need to get in there, so shuffling within a group can never put a key behind its own lock.
    /// </summary>
    public class ItemSpot : MonoBehaviour
    {
        public string group = "";
        [Tooltip("Lie exactly as this spot is turned (an axe in its cabinet) instead of at a random angle.")]
        public bool fixedRotation;
    }
}
