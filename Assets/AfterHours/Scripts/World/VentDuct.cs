using System.Collections.Generic;
using UnityEngine;

namespace AfterHours.World
{
    /// <summary>
    /// A ventilation duct section (the inside volume, as a trigger box). Used to know when someone is "in the vents":
    /// Elliot's catch changes, its hearing is muffled through the metal, and it rarely follows you in.
    /// Ground-floor ducts are big enough for Elliot to squeeze through on all fours; upper ones are not.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class VentDuct : MonoBehaviour
    {
        [Tooltip("Can Elliot crawl through this duct?")]
        public bool creatureSized;

        static readonly List<VentDuct> all = new List<VentDuct>();
        public static IReadOnlyList<VentDuct> All => all;

        BoxCollider box;

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public bool ContainsPoint(Vector3 p)
        {
            if (box == null) box = GetComponent<BoxCollider>();
            Vector3 local = transform.InverseTransformPoint(p) - box.center;
            Vector3 h = box.size * 0.5f;
            return Mathf.Abs(local.x) <= h.x && Mathf.Abs(local.y) <= h.y && Mathf.Abs(local.z) <= h.z;
        }

        public static bool Contains(Vector3 p) => Find(p) != null;

        public static VentDuct Find(Vector3 p)
        {
            foreach (VentDuct d in all) if (d.ContainsPoint(p)) return d;
            return null;
        }
    }
}
