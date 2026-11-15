using UnityEngine;

namespace AfterHours.Rendering
{
    /// <summary>
    /// Marks a light that is real-time until the lighting is baked ("After Hours > 5 - Bake Lighting"),
    /// and becomes a pre-calculated (baked) light afterwards. Baked lights cost nothing at run time,
    /// which is what lets hundreds of mall lights run on integrated graphics.
    /// </summary>
    [DisallowMultipleComponent]
    public class BakeableLight : MonoBehaviour
    {
    }
}
