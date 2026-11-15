using UnityEngine;

namespace AfterHours.World
{
    public enum DoorKind
    {
        ShopBack, Staff, Fire, Storage, Security, Maintenance, Lift, Restricted, Glass, Toilet, Stairwell, Hidden, Shutter
    }

    /// <summary>
    /// Marks a doorway in the mall layout. Part 3 leaves the opening walkable so the layout can be tested;
    /// Part 5 replaces every DoorSlot with a real hinged door / shutter of the right kind, weight and lock.
    /// </summary>
    public class DoorSlot : MonoBehaviour
    {
        public DoorKind kind = DoorKind.Staff;
        public float width = 1.2f;
        public float height = 2.1f;
        public bool startsLocked;
        public string lockId = "";
        [TextArea] public string note = "";
    }
}
