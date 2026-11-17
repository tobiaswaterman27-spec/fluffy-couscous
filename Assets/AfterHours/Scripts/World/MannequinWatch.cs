using UnityEngine;

namespace AfterHours.World
{
    /// <summary>
    /// A shop mannequin that, very occasionally, is facing a different way when you look back at it: only ever
    /// while nobody's camera has it in view, and only when someone is near. Never while watched. That's all.
    /// </summary>
    public class MannequinWatch : MonoBehaviour
    {
        public int seed;
        [Range(0f, 1f)] public float chance = 0.035f;     // per check (every few seconds) while conditions are right
        float nextCheck, cooldownUntil;
        System.Random rng;
        Bounds bounds;
        static readonly Plane[] planes = new Plane[6];

        void Start()
        {
            rng = new System.Random(seed);
            nextCheck = Time.time + 3f + (float)rng.NextDouble() * 5f;
            Renderer r = GetComponentInChildren<Renderer>();
            bounds = r != null ? r.bounds : new Bounds(transform.position + Vector3.up, new Vector3(0.6f, 2f, 0.6f));
        }

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 2.5f + (float)rng.NextDouble() * 2f;
            if (Time.time < cooldownUntil) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 to = cam.transform.position - transform.position;
            to.y = 0f;
            float d = to.magnitude;
            if (d < 3.5f || d > 16f) return;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            if (GeometryUtility.TestPlanesAABB(planes, bounds)) return;      // being looked at: never
            if (rng.NextDouble() > chance) return;
            // turn to face them (or nearly: a few degrees off is worse)
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + ((float)rng.NextDouble() - 0.5f) * 14f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cooldownUntil = Time.time + 40f + (float)rng.NextDouble() * 60f;
        }
    }
}
