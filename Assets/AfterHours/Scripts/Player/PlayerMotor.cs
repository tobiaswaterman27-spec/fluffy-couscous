using System;
using System.Collections.Generic;
using UnityEngine;
using AfterHours.Core;
using AfterHours.Controls;
using AfterHours.World;
using AfterHours.Audio;

namespace AfterHours.Player
{
    public enum MoveMode
    {
        Idle,
        Walk,
        Sprint,
        CrouchWalk,
        Crawl,
        Airborne
    }

    public struct FootstepInfo
    {
        public Vector3 position;
        public SurfaceType surface;
        public MoveMode mode;
        public float intensity;   // 0..1 for audio volume
        public float noiseRadius; // metres, for creature hearing
        public int foot;          // 0 = left, 1 = right
        public bool backward;
    }

    /// <summary>Snapshot of how the body is moving. Animation, camera bob and (later) networking read this.</summary>
    public struct LocomotionState
    {
        public Vector3 worldVelocity;
        public Vector2 localPlanarVelocity; // x = strafe (right +), y = forward (+) / backward (-)
        public float planarSpeed;
        public Stance stance;
        public float height;
        public bool grounded;
        public bool sprinting;
        public bool backward;
        public MoveMode mode;
        public float stridePhase;    // increases by 1 per footstep
        public float hipBendDegrees; // crawling: angle between torso and legs
        public float yawRate;        // degrees per second
    }

    /// <summary>
    /// The player's physical body and movement.
    ///
    /// - Uses Unity's CharacterController (a swept capsule): it can never pass through colliders,
    ///   handles steps and slopes, and does not tunnel at speed.
    /// - Stance changes resize the real collision capsule smoothly, and only grow it when the
    ///   space above is actually free, so you can never stand up inside a vent or under a low ceiling.
    /// - When crawling, the body also has LEGS: a second capsule trailing behind the torso.
    ///   Legs are dragged along like a towed trailer, so they follow the vent around bends,
    ///   you cannot crawl backwards into a wall, and you cannot spin around in a narrow duct.
    /// - Movement has momentum: gradual acceleration, sprint build-up, and sharp turns at speed cost speed.
    /// - Footsteps are generated from distance travelled (stride length), not timers, so sound,
    ///   camera bob and later animations all line up.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Body dimensions (metres)")]
        public float standHeight = 1.8f;
        public float crouchHeight = 1.3f;
        public float crawlHeight = 0.6f;          // on hands and knees
        public float standRadius = 0.3f;
        public float crawlRadius = 0.25f;
        [Tooltip("Distance from top of head to eyes.")]
        public float eyeBelowTop = 0.15f;
        [Tooltip("How fast the body raises/lowers when changing stance (m/s).")]
        public float stanceChangeSpeed = 2.4f;

        [Header("Step heights")]
        public float standStepOffset = 0.35f;
        public float crouchStepOffset = 0.3f;
        public float crawlStepOffset = 0.18f;

        [Header("Speeds (m/s)")]
        public float walkSpeed = 1.75f;           // a brisk real walk (mocap: ~0.9 m steps, 2 steps a second)
        public float sprintSpeed = 6.2f;          // creature top speed will be slightly lower (~5.9)
        public float backwardSprintSpeed = 3.4f;  // running backwards
        public float sprintStrafeSpeed = 3.6f;
        public float crouchSpeed = 1.05f;
        public float crawlSpeed = 0.45f;
        [Range(0.3f, 1f)] public float backwardMultiplier = 0.72f;
        [Range(0.3f, 1f)] public float strafeMultiplier = 0.88f;
        [Range(0.3f, 1f)] public float crawlBackwardMultiplier = 0.7f;
        [Range(0.3f, 1f)] public float crawlStrafeMultiplier = 0.6f;

        [Header("Momentum")]
        public float acceleration = 9f;
        public float sprintAcceleration = 3.2f;   // acceleration near top speed (gradual sprint build-up)
        public float deceleration = 11f;
        public float airAcceleration = 1.5f;
        [Tooltip("Fraction of speed lost per 90 degrees of sharp turning at full sprint.")]
        public float turnSpeedLossPer90 = 0.22f;

        [Header("Gravity & ground")]
        public float gravity = -15f;
        public float terminalVelocity = 40f;
        public float slopeLimit = 46f;
        public float groundSnapDistance = 0.35f;
        public float steepSlideAcceleration = 7f;

        [Header("Crawling body (thigh + shin following the crawled path)")]
        public float legLength = 0.85f;
        public float legRadius = 0.15f;
        [Tooltip("Max angle the torso can twist away from the legs.")]
        public float maxHipBend = 110f;
        [Tooltip("How fast the torso can rotate while crawling (deg/s).")]
        public float crawlTurnRate = 160f;

        [Header("Pushing loose physics objects")]
        public float pushForce = 300f;
        public float maxPushSpeed = 2.2f;

        [Header("Strides (metres per footstep)")]
        public float walkStride = 0.88f;
        public float sprintStride = 1.75f;
        public float crouchStride = 0.6f;
        public float crawlStride = 0.22f;

        [Header("Noise radius (metres, before surface multiplier)")]
        public float crawlNoise = 1.2f;
        public float crouchNoise = 1.6f;
        public float walkNoise = 7f;
        public float sprintNoise = 18f;

        // ---------- public state ----------
        public event Action<FootstepInfo> Footstep;
        public event Action<float> Landed; // fall speed m/s

        public Stance CurrentStance { get; private set; }
        public Stance DesiredStance { get; private set; }
        public bool StanceBlocked { get; private set; }
        public bool CrawlBlocked { get; private set; }
        public float CurrentHeight => currentHeight;
        public float CurrentRadius => controller != null ? controller.radius : standRadius;
        // a real crouch is a deep squat: the eyes sit lower in the crouch capsule than when standing
        public float EyeHeight => currentHeight - eyeBelowTop - 0.05f * Mathf.Clamp01(Mathf.InverseLerp(standHeight, crouchHeight, currentHeight)) * (currentHeight >= crouchHeight - 0.01f ? 1f : Mathf.InverseLerp(crawlHeight, crouchHeight, currentHeight));
        public float CrawlBlend => 1f - Mathf.InverseLerp(crawlHeight, crouchHeight, currentHeight);
        public bool IsGrounded => grounded;
        public SurfaceType GroundSurface { get; private set; } = SurfaceType.Tile;
        public float GroundSlope { get; private set; }
        public LocomotionState Locomotion { get; private set; }
        public float BodyYaw => bodyYaw;
        public bool LegsActive => legsActive;
        /// <summary>Direction from hips to knee (horizontal).</summary>
        public Vector3 LegsDirection => legsDir;
        public Vector3 KneePosition => kneePos;
        public Vector3 FeetPosition => feetPos;

        /// <summary>Set each frame by other systems (e.g. pushing a heavy object). Infinity = no limit.</summary>
        public float ExternalSpeedLimit { get; set; } = float.PositiveInfinity;
        public float ExternalYawRateLimit { get; set; } = float.PositiveInfinity;
        public bool ExternalSprintBlocked { get; set; }
        public Collider GroundCollider { get; private set; }
        public float LegLength => legLength;
        public float LegRadius => legRadius;
        public float LastNoiseRadius { get; private set; }

        // ---------- internals ----------
        CharacterController controller;
        Collider[] ownColliders = Array.Empty<Collider>();
        readonly Collider[] overlapBuffer = new Collider[24];

        float currentHeight;
        float bodyYaw;
        float lastBodyYaw;
        Vector3 horizontalVelocity;
        Vector3 slideVelocity;
        float verticalVelocity;
        float lastDesiredSpeed;
        bool grounded;

        bool legsActive;
        Vector3 legsDir = Vector3.back;
        Vector3 kneePos;
        Vector3 feetPos;
        const float TrailSpacing = 0.05f;
        readonly List<Vector3> trail = new List<Vector3>(64);        // hip first, then the path crawled
        readonly List<Vector3> trailScratch = new List<Vector3>(64); // used for "what if" checks

        float stridePhase;
        int footIndex;
        bool settled = true;

        struct PushRequest
        {
            public Vector3 direction;
            public Vector3 point;
            public float speed;
        }
        readonly Dictionary<Rigidbody, PushRequest> pushRequests = new Dictionary<Rigidbody, PushRequest>();

        int BlockMask => GameLayers.CharacterBlockingMask;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            ownColliders = GetComponentsInChildren<Collider>(true);
            controller.slopeLimit = slopeLimit;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;
            currentHeight = standHeight;
            ApplyDimensions(currentHeight);
            bodyYaw = lastBodyYaw = transform.eulerAngles.y;
            CurrentStance = DesiredStance = Stance.Standing;
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Rotates the body towards the requested yaw. Standing/crouching: exact.
        /// Crawling: limited by turn rate and by how far the torso can twist from the legs.
        /// Returns the resulting body yaw (the camera "head" covers any difference).
        /// </summary>
        public float TrySetBodyYaw(float targetYaw, float deltaTime)
        {
            if (!legsActive)
            {
                bodyYaw = float.IsPositiveInfinity(ExternalYawRateLimit)
                    ? targetYaw
                    : Mathf.MoveTowardsAngle(bodyYaw, targetYaw, ExternalYawRateLimit * deltaTime);
            }
            else
            {
                float legsBackYaw = Mathf.Atan2(-legsDir.x, -legsDir.z) * Mathf.Rad2Deg;
                float rel = Mathf.DeltaAngle(legsBackYaw, targetYaw);
                float allowed = legsBackYaw + Mathf.Clamp(rel, -maxHipBend, maxHipBend);
                bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, allowed, crawlTurnRate * deltaTime);
            }
            bodyYaw = Mathf.Repeat(bodyYaw, 360f);
            transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
            return bodyYaw;
        }

        /// <summary>Main movement update. Call once per frame with this frame's input.</summary>
        public void Tick(in PlayerInputFrame input, float dt)
        {
            if (dt <= 0f || !controller.enabled) return;

            UpdateStance(input.desiredStance, dt);

            bool wasGrounded = grounded;

            // ----- Ground probe -----
            bool hasGround = ProbeGround(groundSnapDistance, out RaycastHit groundHit, out float groundGap);
            Vector3 groundNormal = hasGround ? ResolveGroundNormal(groundHit) : Vector3.up;
            GroundSlope = Vector3.Angle(groundNormal, Vector3.up);
            bool walkable = hasGround && GroundSlope <= slopeLimit;
            grounded = (controller.isGrounded || (hasGround && groundGap <= 0.06f)) && verticalVelocity <= 0.01f;
            bool steep = grounded && hasGround && !walkable;
            GroundCollider = hasGround && groundGap <= 0.12f ? groundHit.collider : null;
            if (hasGround) GroundSurface = SurfaceLookup.Get(groundHit.collider, SurfaceType.Concrete);

            // ----- Desired velocity -----
            Vector2 mv = Vector2.ClampMagnitude(input.move, 1f);
            bool sprintAllowed = input.sprint && !ExternalSprintBlocked && CurrentStance == Stance.Standing && currentHeight > standHeight - 0.05f && mv.sqrMagnitude > 0.04f;
            Vector3 localDesired = ComputeLocalDesired(mv, sprintAllowed, out bool backward);
            Vector3 desired = Quaternion.Euler(0f, bodyYaw, 0f) * localDesired;
            if (!float.IsPositiveInfinity(ExternalSpeedLimit)) desired = Vector3.ClampMagnitude(desired, ExternalSpeedLimit);
            lastDesiredSpeed = desired.magnitude;

            Accelerate(desired, grounded && !steep, dt);

            // ----- Vertical -----
            float verticalBeforeLanding = verticalVelocity;
            if (grounded && !steep)
            {
                if (verticalVelocity < 0f) verticalVelocity = -2f; // keeps the capsule pressed to the floor
            }
            else
            {
                verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, -terminalVelocity);
            }

            if (steep)
            {
                Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
                slideVelocity += downhill * steepSlideAcceleration * dt;
            }
            else
            {
                slideVelocity = Vector3.MoveTowards(slideVelocity, Vector3.zero, 20f * dt);
            }

            // ----- Move (collision-safe sweep) -----
            Vector3 motion = (horizontalVelocity + slideVelocity + Vector3.up * verticalVelocity) * dt;
            motion = ConstrainCrawlMotion(motion);
            Vector3 carried = grounded ? GroundCarry(dt) : Vector3.zero; // lifts, moving walkways

            Vector3 before = transform.position;
            CollisionFlags flags = controller.Move(motion + carried);
            before += carried; // platform movement is not the player's own speed

            // Stay glued to stairs/ramps when walking down instead of floating off them.
            if (wasGrounded && !steep && verticalVelocity <= 0f && (flags & CollisionFlags.Below) == 0)
            {
                if (ProbeGround(groundSnapDistance, out RaycastHit snapHit, out _) &&
                    Vector3.Angle(ResolveGroundNormal(snapHit), Vector3.up) <= slopeLimit)
                {
                    flags |= controller.Move(Vector3.down * groundSnapDistance);
                }
            }

            Vector3 actualVelocity = (transform.position - before) / dt;
            Vector3 actualFlat = new Vector3(actualVelocity.x, 0f, actualVelocity.z);

            // Walls absorb momentum: running into a wall stops you instead of storing speed.
            if ((flags & CollisionFlags.Sides) != 0 && actualFlat.magnitude < horizontalVelocity.magnitude * 0.9f)
            {
                horizontalVelocity = Vector3.ClampMagnitude(actualFlat, horizontalVelocity.magnitude);
            }
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

            grounded = (flags & CollisionFlags.Below) != 0 || controller.isGrounded;
            if (grounded && !wasGrounded)
            {
                float fallSpeed = Mathf.Max(0f, -verticalBeforeLanding);
                verticalVelocity = -2f;
                HandleLanding(fallSpeed);
            }

            UpdateLegsAfterMove();
            UpdateStrides(actualFlat, backward, sprintAllowed, dt);
            PublishLocomotion(actualVelocity, actualFlat, sprintAllowed, backward, dt);
        }

        /// <summary>
        /// Instantly places the player (only for spawning, respawning and loading a save,
        /// never for normal movement).
        /// </summary>
        /// <summary>Standing still (used while hidden or carried, when Tick isn't running).</summary>
        public void ClearLocomotion()
        {
            LocomotionState l = Locomotion;
            l.mode = MoveMode.Idle;
            l.planarSpeed = 0f;
            l.sprinting = false;
            l.worldVelocity = Vector3.zero;
            l.localPlanarVelocity = Vector2.zero;
            l.yawRate = 0f;
            Locomotion = l;
        }

        /// <summary>Places the body in a given stance (after climbing into a duct: crawling; after dropping out: crouched).</summary>
        public void WarpStance(Vector3 position, float yaw, Stance stance)
        {
            Warp(position, yaw);
            currentHeight = HeightFor(stance);
            CurrentStance = DesiredStance = stance;
            ApplyDimensions(currentHeight);
        }

        public void Warp(Vector3 position, float yaw)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            controller.enabled = true;
            bodyYaw = lastBodyYaw = yaw;
            horizontalVelocity = slideVelocity = Vector3.zero;
            verticalVelocity = 0f;
            legsActive = false;
            trail.Clear();
            currentHeight = standHeight;
            ApplyDimensions(currentHeight);
        }

        // =====================================================================
        // Stance
        // =====================================================================

        float HeightFor(Stance s)
        {
            switch (s)
            {
                case Stance.Crawling: return crawlHeight;
                case Stance.Crouching: return crouchHeight;
                default: return standHeight;
            }
        }

        float RadiusForHeight(float h)
        {
            if (h >= crouchHeight) return standRadius;
            return Mathf.Lerp(crawlRadius, standRadius, Mathf.InverseLerp(crawlHeight, crouchHeight, h));
        }

        float StepForHeight(float h)
        {
            float step = h >= crouchHeight
                ? Mathf.Lerp(crouchStepOffset, standStepOffset, Mathf.InverseLerp(crouchHeight, standHeight, h))
                : Mathf.Lerp(crawlStepOffset, crouchStepOffset, Mathf.InverseLerp(crawlHeight, crouchHeight, h));
            return Mathf.Min(step, h * 0.4f);
        }

        void ApplyDimensions(float h)
        {
            float r = RadiusForHeight(h);
            controller.height = h;
            controller.radius = r;
            controller.center = new Vector3(0f, h * 0.5f, 0f);
            controller.stepOffset = StepForHeight(h);
        }

        void UpdateStance(Stance desired, float dt)
        {
            DesiredStance = desired;
            Stance effective = desired;
            CrawlBlocked = false;

            // Going prone needs room for the legs behind you.
            if (desired == Stance.Crawling && !legsActive)
            {
                Vector3 candidate = -(Quaternion.Euler(0f, bodyYaw, 0f) * Vector3.forward);
                ResetTrail(trailScratch, transform.position, candidate);
                if (LegsFitPath(trailScratch))
                {
                    legsActive = true;
                    ResetTrail(trail, transform.position, candidate);
                    RefreshLegPoints();
                }
                else
                {
                    effective = Stance.Crouching;
                    CrawlBlocked = true;
                }
            }

            float target = HeightFor(effective);
            float h = currentHeight;
            float step = stanceChangeSpeed * dt;

            if (target < h - 0.0001f)
            {
                currentHeight = Mathf.Max(target, h - step);
                ApplyDimensions(currentHeight);
                StanceBlocked = false;
            }
            else if (target > h + 0.0001f)
            {
                float next = Mathf.Min(target, h + step);
                if (CanFit(next))
                {
                    currentHeight = next;
                    ApplyDimensions(currentHeight);
                    StanceBlocked = false;
                }
                else
                {
                    StanceBlocked = true; // e.g. tried to stand up inside a vent: stay low
                }
            }
            else
            {
                StanceBlocked = false;
            }

            // Legs stop mattering once the body is back up at crouch height.
            if (legsActive && effective != Stance.Crawling && currentHeight >= crouchHeight - 0.05f)
            {
                legsActive = false;
            }

            if (currentHeight <= crawlHeight + 0.15f) CurrentStance = Stance.Crawling;
            else if (currentHeight <= crouchHeight + 0.1f) CurrentStance = Stance.Crouching;
            else CurrentStance = Stance.Standing;
        }

        /// <summary>True if a body of height h fits here without overlapping any solid geometry.</summary>
        bool CanFit(float h)
        {
            float r = RadiusForHeight(h) - 0.01f;
            float skin = controller.skinWidth;
            Vector3 p = transform.position;
            Vector3 bottom = p + Vector3.up * (r + skin + 0.02f);
            Vector3 top = p + Vector3.up * Mathf.Max(h - r + skin, r + skin + 0.02f);
            int n = Physics.OverlapCapsuleNonAlloc(bottom, top, r, overlapBuffer, BlockMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!IsOwnCollider(overlapBuffer[i])) return false;
            }
            return true;
        }

        bool IsOwnCollider(Collider c)
        {
            if (c == controller) return true;
            for (int i = 0; i < ownColliders.Length; i++)
            {
                if (ownColliders[i] == c) return true;
            }
            return false;
        }

        // =====================================================================
        // Crawling legs
        //
        // The legs are two capsules (thigh: hip->knee, shin: knee->feet) laid along the path the
        // hips have actually crawled ("trail"). Because they follow the real path, they go round
        // vent bends without cutting corners. Crawling backwards extends the path behind the feet,
        // and that new space is checked before the move is allowed.
        // =====================================================================

        void ResetTrail(List<Vector3> path, Vector3 hip, Vector3 backDir)
        {
            path.Clear();
            path.Add(hip);
            path.Add(hip + backDir.normalized * (legLength + 0.05f));
        }

        /// <summary>Moves the front of the path to the new hip position and keeps the path exactly leg-length long.</summary>
        void AdvanceTrail(List<Vector3> path, Vector3 newHip)
        {
            if (path.Count < 2) return;
            Vector3 d = newHip - path[0];
            d.y = 0f;
            Vector3 back = path[1] - path[0];
            back.y = 0f;
            float backLen = back.magnitude;
            float towardLegs = backLen > 1e-5f ? Vector3.Dot(d, back / backLen) : 0f;

            if (towardLegs > 0f)
            {
                // Hips moving back along the body: eat into the path, tail extends later.
                float amount = towardLegs;
                while (amount > 0f && path.Count >= 2)
                {
                    float seg = Vector3.Distance(path[0], path[1]);
                    if (seg <= amount && path.Count > 2)
                    {
                        amount -= seg;
                        path.RemoveAt(0);
                    }
                    else
                    {
                        path[0] = Vector3.MoveTowards(path[0], path[1], Mathf.Min(amount, seg * 0.95f));
                        amount = 0f;
                    }
                }
                path[0] = newHip;
            }
            else if (Vector3.Distance(newHip, path[1]) >= TrailSpacing * 2f)
            {
                path.Insert(0, newHip); // leave a breadcrumb
            }
            else
            {
                path[0] = newHip;
            }

            // Keep total length = leg length (+ small margin): trim or extend the tail.
            float required = legLength + 0.05f;
            float total = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Vector3.Distance(path[i - 1], path[i]);
                if (total + seg >= required)
                {
                    Vector3 dir = (path[i] - path[i - 1]).normalized;
                    path[i] = path[i - 1] + dir * (required - total);
                    if (path.Count > i + 1) path.RemoveRange(i + 1, path.Count - i - 1);
                    total = required;
                    break;
                }
                total += seg;
            }
            if (total < required)
            {
                int n = path.Count;
                Vector3 dir = n >= 2 ? path[n - 1] - path[n - 2] : -transform.forward;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-6f) dir = -transform.forward;
                path[n - 1] = path[n - 1] + dir.normalized * (required - total);
            }

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 p = path[i];
                p.y = newHip.y;
                path[i] = p;
            }
        }

        static Vector3 PointAlong(List<Vector3> path, float distance)
        {
            float walked = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Vector3.Distance(path[i - 1], path[i]);
                if (walked + seg >= distance && seg > 1e-6f)
                {
                    return Vector3.Lerp(path[i - 1], path[i], (distance - walked) / seg);
                }
                walked += seg;
            }
            return path[path.Count - 1];
        }

        void LegPoints(List<Vector3> path, out Vector3 knee, out Vector3 feet)
        {
            knee = PointAlong(path, legLength * 0.5f);
            feet = PointAlong(path, legLength);
        }

        void RefreshLegPoints()
        {
            LegPoints(trail, out kneePos, out feetPos);
            Vector3 d = kneePos - trail[0];
            d.y = 0f;
            if (d.sqrMagnitude > 1e-6f) legsDir = d.normalized;
        }

        bool LegsFitPath(List<Vector3> path)
        {
            if (path.Count < 2) return true;
            LegPoints(path, out Vector3 knee, out Vector3 feet);
            Vector3 hip = path[0];
            Vector3 thighDir = knee - hip;
            thighDir.y = 0f;
            thighDir = thighDir.sqrMagnitude > 1e-6f ? thighDir.normalized : -transform.forward;
            Vector3 up = Vector3.up * (legRadius + 0.05f);
            Vector3 hipStart = hip + thighDir * (crawlRadius * 0.8f);
            return CapsuleClear(hipStart + up, knee + up) && CapsuleClear(knee + up, feet + up);
        }

        bool CapsuleClear(Vector3 a, Vector3 b)
        {
            int n = Physics.OverlapCapsuleNonAlloc(a, b, legRadius, overlapBuffer, BlockMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = overlapBuffer[i];
                if (IsOwnCollider(c)) continue;
                if (c.gameObject.layer == GameLayers.PlayerLayer) continue; // other players: handled in co-op part
                return false;
            }
            return true;
        }

        /// <summary>Stops crawling motion that would shove the legs into a wall.</summary>
        Vector3 ConstrainCrawlMotion(Vector3 motion)
        {
            if (!legsActive || trail.Count < 2) return motion;
            Vector3 flat = new Vector3(motion.x, 0f, motion.z);
            if (flat.sqrMagnitude < 1e-8f) return motion;

            float towardFeet = Vector3.Dot(flat, legsDir);
            Vector3 lateral = flat - legsDir * towardFeet;
            bool legsLead = towardFeet > 0f;
            bool sideways = lateral.magnitude > flat.magnitude * 0.3f;
            if (!legsLead && !sideways) return motion; // head first: legs simply follow the path

            if (!LegsFitPath(trail)) return motion;    // never trap a player who is already wedged
            if (TrailFitsAfter(flat)) return motion;

            Vector3 alt = legsLead ? lateral : legsDir * Mathf.Min(0f, towardFeet);
            if (alt.sqrMagnitude > 1e-8f && TrailFitsAfter(alt)) return new Vector3(alt.x, motion.y, alt.z);
            return new Vector3(0f, motion.y, 0f);
        }

        bool TrailFitsAfter(Vector3 flatMove)
        {
            trailScratch.Clear();
            trailScratch.AddRange(trail);
            AdvanceTrail(trailScratch, transform.position + flatMove);
            return LegsFitPath(trailScratch);
        }

        void UpdateLegsAfterMove()
        {
            if (!legsActive) return;
            if (trail.Count < 2) ResetTrail(trail, transform.position, -transform.forward);
            AdvanceTrail(trail, transform.position);
            RefreshLegPoints();
        }

        // =====================================================================
        // Moving ground (lifts, platforms, travelators)
        // =====================================================================

        Vector3 GroundCarry(float dt)
        {
            if (GroundCollider == null) return Vector3.zero;
            Vector3 carry = Vector3.zero;
            MovingPlatform platform = GroundCollider.GetComponentInParent<MovingPlatform>();
            if (platform != null) carry += platform.FrameDelta;
            ConveyorSurface conveyor = GroundCollider.GetComponentInParent<ConveyorSurface>();
            if (conveyor != null) carry += conveyor.WorldVelocity * dt;
            return carry;
        }

        // =====================================================================
        // Velocity
        // =====================================================================

        Vector3 ComputeLocalDesired(Vector2 mv, bool sprinting, out bool backward)
        {
            backward = mv.y < -0.1f;
            float fwdMax, backMax, sideMax;
            switch (CurrentStance)
            {
                case Stance.Crawling:
                    fwdMax = crawlSpeed;
                    backMax = crawlSpeed * crawlBackwardMultiplier;
                    sideMax = crawlSpeed * crawlStrafeMultiplier;
                    break;
                case Stance.Crouching:
                    fwdMax = crouchSpeed;
                    backMax = crouchSpeed * backwardMultiplier;
                    sideMax = crouchSpeed * strafeMultiplier;
                    break;
                default:
                    if (sprinting)
                    {
                        fwdMax = sprintSpeed;
                        backMax = backwardSprintSpeed;
                        sideMax = sprintStrafeSpeed;
                    }
                    else
                    {
                        fwdMax = walkSpeed;
                        backMax = walkSpeed * backwardMultiplier;
                        sideMax = walkSpeed * strafeMultiplier;
                    }
                    break;
            }
            float z = mv.y >= 0f ? mv.y * fwdMax : mv.y * backMax;
            return new Vector3(mv.x * sideMax, 0f, z);
        }

        void Accelerate(Vector3 desired, bool onGround, float dt)
        {
            Vector3 v = horizontalVelocity;
            float speed = v.magnitude;

            if (!onGround)
            {
                horizontalVelocity = Vector3.MoveTowards(v, desired, airAcceleration * dt);
                return;
            }

            // Sharp turns at speed: the direction bends at a limited rate and costs momentum.
            if (desired.sqrMagnitude > 0.0001f && speed > 0.05f)
            {
                float angle = Vector3.SignedAngle(v, desired, Vector3.up);
                if (Mathf.Abs(angle) < 120f)
                {
                    float speedRatio = Mathf.Clamp01((speed - walkSpeed) / Mathf.Max(0.01f, sprintSpeed - walkSpeed));
                    float maxTurn = Mathf.Lerp(1080f, 300f, speedRatio) * dt;
                    float turn = Mathf.Clamp(angle, -maxTurn, maxTurn);
                    float loss = Mathf.Abs(turn) / 90f * turnSpeedLossPer90 * speed * (0.2f + 0.8f * speedRatio);
                    v = Quaternion.AngleAxis(turn, Vector3.up) * v;
                    v = v.normalized * Mathf.Max(0f, speed - loss);
                    speed = v.magnitude;
                }
                // Over 120 degrees (reversing) there is no turn: you brake and re-accelerate.
            }

            float targetSpeed = desired.magnitude;
            float rate;
            if (targetSpeed > speed + 0.01f)
            {
                float r = Mathf.InverseLerp(walkSpeed, sprintSpeed, speed);
                rate = Mathf.Lerp(acceleration, sprintAcceleration, r);
            }
            else
            {
                rate = deceleration;
            }
            horizontalVelocity = Vector3.MoveTowards(v, desired, rate * dt);
        }

        // =====================================================================
        // Ground
        // =====================================================================

        bool ProbeGround(float extra, out RaycastHit hit, out float gap)
        {
            float r = controller.radius * 0.9f;
            float lift = 0.05f;
            Vector3 origin = transform.position + Vector3.up * (controller.radius + lift);
            float baseOffset = lift + (controller.radius - r);
            bool found = Physics.SphereCast(origin, r, Vector3.down, out hit,
                baseOffset + extra + controller.skinWidth, BlockMask, QueryTriggerInteraction.Ignore);
            gap = found ? hit.distance - baseOffset : float.MaxValue;
            return found;
        }

        /// <summary>
        /// Sphere casts report rounded normals on step edges, which would look like steep slopes.
        /// Confirm with straight-down rays so stairs never count as a slide.
        /// </summary>
        Vector3 ResolveGroundNormal(RaycastHit hit)
        {
            Vector3 n = hit.normal;
            if (Vector3.Angle(n, Vector3.up) <= slopeLimit) return n;

            if (Physics.Raycast(hit.point + Vector3.up * 0.1f, Vector3.down, out RaycastHit r1, 0.25f, BlockMask, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(r1.normal, Vector3.up) <= slopeLimit)
            {
                return r1.normal;
            }
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit r2, 0.2f + groundSnapDistance, BlockMask, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(r2.normal, Vector3.up) <= slopeLimit)
            {
                return r2.normal;
            }
            return n;
        }

        void HandleLanding(float fallSpeed)
        {
            if (fallSpeed < 2f) return;
            float radius = Mathf.Clamp(fallSpeed * 2.5f, 3f, 25f) * SurfaceAcoustics.NoiseMultiplier(GroundSurface);
            LastNoiseRadius = radius;
            NoiseBus.Emit(new NoiseEvent(transform.position, radius, NoiseKind.Landing, gameObject));
            Footstep?.Invoke(new FootstepInfo
            {
                position = transform.position,
                surface = GroundSurface,
                mode = MoveMode.Walk,
                intensity = Mathf.Clamp01(fallSpeed / 8f),
                noiseRadius = radius,
                foot = footIndex
            });
            Landed?.Invoke(fallSpeed);
        }

        // =====================================================================
        // Strides, footsteps, noise
        // =====================================================================

        MoveMode ResolveMode(float speed, bool sprinting)
        {
            if (!grounded) return MoveMode.Airborne;
            if (speed < 0.15f) return MoveMode.Idle;
            if (CurrentStance == Stance.Crawling) return MoveMode.Crawl;
            if (CurrentStance == Stance.Crouching) return MoveMode.CrouchWalk;
            if (sprinting && speed > walkSpeed * 1.15f) return MoveMode.Sprint;
            return MoveMode.Walk;
        }

        void UpdateStrides(Vector3 actualFlat, bool backward, bool sprinting, float dt)
        {
            float speed = actualFlat.magnitude;
            MoveMode mode = ResolveMode(speed, sprinting);

            if (mode == MoveMode.Airborne) return;

            if (mode == MoveMode.Idle)
            {
                // Finish a half-taken step with a soft "settle" so the feet come together.
                float frac = stridePhase - Mathf.Floor(stridePhase);
                if (!settled && frac > 0.3f)
                {
                    EmitFootstep(MoveMode.Walk, backward, 0.4f);
                    stridePhase = Mathf.Ceil(stridePhase);
                }
                settled = true;
                return;
            }
            settled = false;

            float stride;
            switch (mode)
            {
                case MoveMode.Crawl: stride = crawlStride; break;
                case MoveMode.CrouchWalk: stride = crouchStride; break;
                default: stride = Mathf.Lerp(walkStride, sprintStride, Mathf.InverseLerp(walkSpeed, sprintSpeed, speed)); break;
            }
            if (backward) stride *= 0.8f;

            float before = stridePhase;
            stridePhase += speed * dt / stride;
            if (Mathf.Floor(stridePhase) > Mathf.Floor(before))
            {
                EmitFootstep(mode, backward, 1f);
            }
        }

        void EmitFootstep(MoveMode mode, bool backward, float scale)
        {
            float speed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
            float baseRadius;
            float intensity;
            switch (mode)
            {
                case MoveMode.Crawl: baseRadius = crawlNoise; intensity = 0.25f; break;
                case MoveMode.CrouchWalk: baseRadius = crouchNoise; intensity = 0.3f; break;
                case MoveMode.Sprint: baseRadius = sprintNoise; intensity = 1f; break;
                default:
                    float w = Mathf.InverseLerp(0.3f, walkSpeed, speed);
                    baseRadius = Mathf.Lerp(3f, walkNoise, w);
                    intensity = Mathf.Lerp(0.35f, 0.55f, w);
                    break;
            }

            float radius = baseRadius * SurfaceAcoustics.NoiseMultiplier(GroundSurface) * scale;
            LastNoiseRadius = radius;
            footIndex = 1 - footIndex;

            NoiseBus.Emit(new NoiseEvent(transform.position, radius, NoiseKind.Footstep, gameObject));
            Footstep?.Invoke(new FootstepInfo
            {
                position = transform.position,
                surface = GroundSurface,
                mode = mode,
                intensity = intensity * scale,
                noiseRadius = radius,
                foot = footIndex,
                backward = backward
            });
        }

        void PublishLocomotion(Vector3 actualVelocity, Vector3 actualFlat, bool sprinting, bool backward, float dt)
        {
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, bodyYaw, 0f)) * actualFlat;
            float yawRate = Mathf.DeltaAngle(lastBodyYaw, bodyYaw) / dt;
            lastBodyYaw = bodyYaw;

            float hipBend = 0f;
            if (legsActive)
            {
                Vector3 forward = Quaternion.Euler(0f, bodyYaw, 0f) * Vector3.forward;
                hipBend = Vector3.SignedAngle(-legsDir, forward, Vector3.up);
            }

            float speed = actualFlat.magnitude;
            MoveMode mode = ResolveMode(speed, sprinting);
            Locomotion = new LocomotionState
            {
                worldVelocity = actualVelocity,
                localPlanarVelocity = new Vector2(local.x, local.z),
                planarSpeed = speed,
                stance = CurrentStance,
                height = currentHeight,
                grounded = grounded,
                sprinting = mode == MoveMode.Sprint,
                backward = backward && speed > 0.1f,
                mode = mode,
                stridePhase = stridePhase,
                hipBendDegrees = hipBend,
                yawRate = yawRate
            };
        }

        // =====================================================================
        // Pushing loose objects
        // =====================================================================

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;
            if (hit.moveDirection.y < -0.3f) return; // standing on it, not pushing it
            Vector3 dir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (dir.sqrMagnitude < 0.01f) return;
            pushRequests[rb] = new PushRequest
            {
                direction = dir.normalized,
                point = hit.point,
                speed = lastDesiredSpeed
            };
        }

        // =====================================================================
        // Remote players (LAN co-op): the owner simulates; everyone else just shows the result.
        // =====================================================================

        /// <summary>Shows a remote player's body as reported over the network (no simulation here).</summary>
        public void ApplyRemoteState(Vector3 position, float yaw, Stance stance, float height, Vector3 velocity, MoveMode mode,
                                     float stridePhase, bool backward, bool legs, Vector3 knee, Vector3 feet, SurfaceType surface)
        {
            if (controller != null && controller.enabled) controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            bodyYaw = lastBodyYaw = yaw;
            if (Mathf.Abs(height - currentHeight) > 0.001f) { currentHeight = height; ApplyDimensions(height); }
            CurrentStance = stance;
            legsActive = legs;
            kneePos = knee;
            feetPos = feet;
            if (legs && (feet - position).sqrMagnitude > 1e-4f) legsDir = new Vector3(feet.x - position.x, 0f, feet.z - position.z).normalized;
            GroundSurface = surface;
            float prev = Locomotion.stridePhase;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * flat;
            Locomotion = new LocomotionState
            {
                worldVelocity = velocity,
                localPlanarVelocity = new Vector2(local.x, local.z),
                planarSpeed = flat.magnitude,
                stance = stance,
                height = height,
                grounded = true,
                sprinting = mode == MoveMode.Sprint,
                backward = backward,
                mode = mode,
                stridePhase = stridePhase,
            };
            // Their footsteps are heard here too (sound only: the host already heard the noise).
            if (Mathf.FloorToInt(stridePhase) != Mathf.FloorToInt(prev) && mode != MoveMode.Idle)
            {
                Footstep?.Invoke(new FootstepInfo
                {
                    position = position,
                    surface = surface,
                    mode = mode,
                    intensity = mode == MoveMode.Sprint ? 1f : mode == MoveMode.Crawl ? 0.3f : 0.6f,
                    noiseRadius = 0f,
                    foot = Mathf.FloorToInt(stridePhase) & 1,
                    backward = backward
                });
            }
        }

        void FixedUpdate()
        {
            if (pushRequests.Count == 0) return;
            float fdt = Time.fixedDeltaTime;
            foreach (KeyValuePair<Rigidbody, PushRequest> kv in pushRequests)
            {
                Rigidbody rb = kv.Key;
                if (rb == null || rb.isKinematic) continue;
                PushRequest p = kv.Value;
                float target = Mathf.Min(p.speed, maxPushSpeed);
                float along = Vector3.Dot(rb.linearVelocity, p.direction);
                float needed = (target - along) * rb.mass / fdt;
                float force = Mathf.Clamp(needed, 0f, pushForce);
                if (force > 0f) rb.AddForceAtPosition(p.direction * force, p.point, ForceMode.Force);
            }
            pushRequests.Clear();
        }

        // =====================================================================
        // Debug drawing (Scene view)
        // =====================================================================

        void OnDrawGizmos()
        {
            CharacterController cc = controller != null ? controller : GetComponent<CharacterController>();
            if (cc == null) return;
            Gizmos.color = StanceBlocked ? Color.red : new Color(0.2f, 1f, 0.4f);
            Vector3 p = transform.position;
            float r = cc.radius;
            float h = cc.height;
            Vector3 bottom = p + Vector3.up * r;
            Vector3 top = p + Vector3.up * Mathf.Max(r, h - r);
            Gizmos.DrawWireSphere(bottom, r);
            Gizmos.DrawWireSphere(top, r);
            Gizmos.DrawLine(bottom + Vector3.forward * r, top + Vector3.forward * r);
            Gizmos.DrawLine(bottom - Vector3.forward * r, top - Vector3.forward * r);
            Gizmos.DrawLine(bottom + Vector3.right * r, top + Vector3.right * r);
            Gizmos.DrawLine(bottom - Vector3.right * r, top - Vector3.right * r);

            if (legsActive && trail.Count >= 2)
            {
                Gizmos.color = new Color(0.3f, 0.7f, 1f);
                Vector3 up = Vector3.up * (legRadius + 0.05f);
                Gizmos.DrawLine(p + up, kneePos + up);
                Gizmos.DrawLine(kneePos + up, feetPos + up);
                Gizmos.DrawWireSphere(kneePos + up, legRadius);
                Gizmos.DrawWireSphere(feetPos + up, legRadius);
                Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.4f);
                for (int i = 1; i < trail.Count; i++) Gizmos.DrawLine(trail[i - 1] + up * 0.2f, trail[i] + up * 0.2f);
            }
        }
    }
}
