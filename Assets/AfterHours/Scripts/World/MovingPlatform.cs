using UnityEngine;
using AfterHours.Core;

namespace AfterHours.World
{
    /// <summary>
    /// A platform that travels smoothly between its start position and start + travel (lift floors,
    /// service platforms). Characters standing on it are carried by exactly FrameDelta each frame.
    /// With safetyStop on, it refuses to move into a character (no crushing, no pushing people
    /// through walls); it simply waits until the space is clear. Real lifts in Part 12 build on this.
    /// </summary>
    [DefaultExecutionOrder(-50)] // moves before players and creatures update
    public class MovingPlatform : MonoBehaviour
    {
        [Tooltip("World-space offset from the start position to the end position.")]
        public Vector3 travel = new Vector3(0f, 2.16f, 0f);
        public float travelTime = 4f;
        public float waitTime = 3f;
        public bool safetyStop = true;
        [Tooltip("Moved by another script (lifts) through ManualMove instead of travelling back and forth.")]
        public bool manual;

        public Vector3 FrameDelta { get; private set; }
        public bool IsMoving { get; private set; }

        Vector3 startPos;
        float progress;
        int direction = 1;
        float waitTimer;
        Collider body;
        readonly Collider[] buffer = new Collider[8];

        void Awake()
        {
            startPos = transform.position;
            body = GetComponent<Collider>();
            waitTimer = waitTime;
        }

        /// <summary>Manual mode: moves by delta this frame (riders are carried). Call every frame, even with zero.</summary>
        public void ManualMove(Vector3 delta)
        {
            FrameDelta = delta;
            IsMoving = delta.sqrMagnitude > 0f;
            if (!IsMoving) return;
            transform.position += delta;
            Physics.SyncTransforms();
        }

        /// <summary>Would moving by delta push into a character?</summary>
        public bool WouldHit(Vector3 delta) => safetyStop && IsObstructed(delta);

        void Update()
        {
            if (manual) return;
            FrameDelta = Vector3.zero;
            IsMoving = false;
            float dt = Time.deltaTime;
            if (dt <= 0f || travelTime <= 0f) return;

            if (waitTimer > 0f)
            {
                waitTimer -= dt;
                return;
            }

            float next = Mathf.Clamp01(progress + direction * dt / travelTime);
            Vector3 from = Evaluate(progress);
            Vector3 to = Evaluate(next);
            Vector3 delta = to - from;

            if (safetyStop && IsObstructed(delta)) return;

            transform.position = to;
            FrameDelta = delta;
            IsMoving = true;
            progress = next;
            Physics.SyncTransforms();

            if (progress <= 0f || progress >= 1f)
            {
                direction = -direction;
                waitTimer = waitTime;
            }
        }

        Vector3 Evaluate(float t)
        {
            float eased = t * t * (3f - 2f * t); // smooth start and stop
            return startPos + travel * eased;
        }

        bool IsObstructed(Vector3 delta)
        {
            if (body == null || delta.sqrMagnitude < 1e-10f) return false;
            int mask = 0;
            if (GameLayers.PlayerLayer >= 0) mask |= 1 << GameLayers.PlayerLayer;
            if (GameLayers.CreatureLayer >= 0) mask |= 1 << GameLayers.CreatureLayer;
            if (mask == 0) return false;

            Bounds b = body.bounds;
            Vector3 half = b.extents - Vector3.one * 0.03f;
            int n = Physics.OverlapBoxNonAlloc(b.center + delta, half, buffer, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
            return n > 0; // riders stand ON the top surface, so they never overlap the moved box
        }
    }
}
