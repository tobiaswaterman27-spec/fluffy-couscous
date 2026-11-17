using UnityEngine;
using AfterHours.Core;

namespace AfterHours.Creature
{
    /// <summary>
    /// Elliot's physical movement: a collision-aware capsule (it cannot pass through walls, doors or furniture),
    /// smooth acceleration and deceleration, a turn rate (it never snaps to a new facing), and only slight loss
    /// of speed in corners. The AI (Part 9) sets a desired velocity and gait; this does the rest.
    /// Top running speed (5.9 m/s) is a little below the player's sprint (6.2 m/s).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ElliotMotor : MonoBehaviour
    {
        public float slowWalkSpeed = 0.8f;
        public float walkSpeed = 1.35f;
        public float fastWalkSpeed = 2.3f;
        public float runSpeed = 5.9f;
        public float crawlSpeed = 3.4f;     // top speed scuttling through a duct (it creeps at a fraction of this)
        public float acceleration = 3.2f;
        public float runAcceleration = 5f;
        public float deceleration = 6f;
        public float turnRate = 300f;        // deg/s at walking pace
        public float runTurnRate = 420f;
        [Tooltip("Speed lost per 90 degrees of turning while running (player loses ~22%).")]
        public float cornerLoss = 0.06f;
        public float gravity = -20f;

        [Header("Body height")]
        public float standHeight = 2.55f;
        public float hunchHeight = 1.95f;
        public float crawlHeight = 0.9f;
        public float pushForce = 900f;

        /// <summary>0 = upright, 1 = hunched right down (ducking under door frames and low ceilings).</summary>
        public float Hunch { get; private set; }

        public ElliotGait Gait { get; private set; } = ElliotGait.Idle;
        public Vector3 Velocity => velocity;
        public float Speed => new Vector3(velocity.x, 0f, velocity.z).magnitude;
        public bool Grounded { get; private set; }
        public float YawRate { get; private set; }

        CharacterController controller;
        Vector3 desiredDirection;
        float desiredSpeedFactor = 1f;
        Vector3 velocity;
        float verticalVelocity;
        Vector3? faceTarget;
        float frozenUntil;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        public float MaxSpeedFor(ElliotGait g)
        {
            switch (g)
            {
                case ElliotGait.SlowWalk: return slowWalkSpeed;
                case ElliotGait.Walk: return walkSpeed;
                case ElliotGait.FastWalk: return fastWalkSpeed;
                case ElliotGait.Run: return runSpeed;
                case ElliotGait.Crawl: return crawlSpeed;
                default: return 0f;
            }
        }

        /// <summary>Move in this direction at this gait (direction zero = stop).</summary>
        public void Drive(Vector3 direction, ElliotGait gait, float speedFactor = 1f)
        {
            direction.y = 0f;
            desiredDirection = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.zero;
            Gait = gait;
            desiredSpeedFactor = Mathf.Clamp01(speedFactor);
        }

        /// <summary>Turn to face a point even while standing still (null = face the way it moves).</summary>
        public void Face(Vector3? point) => faceTarget = point;

        // ---------------- scripted moves (hauling itself up into a duct, slithering out of one) ----------------
        Vector3[] scriptPath;
        float scriptT, scriptDur, scriptYaw;

        /// <summary>True while it's being moved along a scripted path.</summary>
        public bool Scripted => scriptPath != null;

        /// <summary>
        /// Moves along the given points over 'seconds' (eased), ignoring collisions, turning to 'yaw'. Its
        /// velocity is reported as normal so the legs and arms animate the climb.
        /// </summary>
        public void ScriptedMove(Vector3[] points, float seconds, float yaw)
        {
            if (points == null || points.Length < 2) return;
            scriptPath = points;
            scriptT = 0f;
            scriptDur = Mathf.Max(0.1f, seconds);
            scriptYaw = yaw;
            controller.enabled = false;
        }

        void StepScript(float dt)
        {
            scriptT += dt;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(scriptT / scriptDur));
            float f = u * (scriptPath.Length - 1);
            int i = Mathf.Min(scriptPath.Length - 2, Mathf.FloorToInt(f));
            Vector3 p = Vector3.Lerp(scriptPath[i], scriptPath[i + 1], f - i);
            Vector3 before = transform.position;
            transform.position = p;
            velocity = (p - before) / Mathf.Max(dt, 1e-4f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, scriptYaw, 0f), 360f * dt);
            if (scriptT >= scriptDur)
            {
                scriptPath = null;
                controller.enabled = !remote;
                velocity = Vector3.zero;
            }
        }

        /// <summary>Holds position for a while (actions, staring).</summary>
        public void Freeze(float seconds) => frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);

        public void Warp(Vector3 position, float yaw)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            controller.enabled = !remote;
            remotePos = position;
            remoteYaw = yaw;
            velocity = Vector3.zero;
        }

        int blockMask;

        void Start()
        {
            blockMask = GameLayers.CharacterBlockingMask;
            foreach (int l in new[] { GameLayers.CreatureLayer, GameLayers.PlayerLayer, GameLayers.PropLayer })
            {
                if (l >= 0) blockMask &= ~(1 << l);
            }
        }

        bool Clear(Vector3 at, float height)
        {
            float r = controller.radius - 0.02f;
            Vector3 a = at + Vector3.up * (r + 0.1f), b = at + Vector3.up * (height - r);
            return !Physics.CheckCapsule(a, b, r, blockMask, QueryTriggerInteraction.Ignore);
        }

        void UpdateHeight(float dt)
        {
            float target;
            if (Gait == ElliotGait.Crawl) target = crawlHeight;
            else
            {
                Vector3 ahead = transform.position + transform.forward * 0.7f;
                bool roomHere = Clear(transform.position, standHeight);
                bool roomAhead = Clear(ahead, standHeight);
                Hunch = Mathf.MoveTowards(Hunch, roomHere && roomAhead ? 0f : 1f, dt * 2.5f);
                target = Mathf.Lerp(standHeight, hunchHeight, Hunch);
            }
            float h = controller.height;
            float next = Mathf.MoveTowards(h, target, dt * 2f);
            if (next > h && !Clear(transform.position, next)) next = h; // never stand up into something
            controller.height = next;
            controller.center = new Vector3(0f, next * 0.5f, 0f);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic || hit.moveDirection.y < -0.3f) return;
            Vector3 dir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z).normalized;
            rb.AddForceAtPosition(dir * pushForce * Mathf.Clamp01(Speed / 2f + 0.3f), hit.point, ForceMode.Force);
        }

        // ---------------- LAN co-op: on clients the host's Elliot is shown, not simulated ----------------
        public bool remote;
        Vector3 remotePos;
        float remoteYaw;

        public void ApplyRemote(Vector3 position, float yaw, Vector3 vel, ElliotGait gait, float hunch)
        {
            remote = true;
            if (controller != null && controller.enabled) controller.enabled = false;
            if ((remotePos - position).sqrMagnitude > 25f || remotePos == Vector3.zero) transform.position = position;
            remotePos = position;
            remoteYaw = yaw;
            velocity = vel;
            Gait = gait;
            Hunch = hunch;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (remote)
            {
                float k = 1f - Mathf.Exp(-14f * dt);
                transform.position = Vector3.Lerp(transform.position, remotePos + velocity * 0.05f, k);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, remoteYaw, 0f), k);
                return;
            }
            if (scriptPath != null) { StepScript(dt); return; }
            UpdateHeight(dt);
            bool frozen = Time.time < frozenUntil;
            float maxSpeed = frozen ? 0f : MaxSpeedFor(Gait) * desiredSpeedFactor;
            Vector3 desired = desiredDirection * maxSpeed;

            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            if (desired.sqrMagnitude > 0.01f && speed > 0.1f)
            {
                float angle = Vector3.SignedAngle(flat, desired, Vector3.up);
                float rate = Mathf.Lerp(turnRate, runTurnRate, Mathf.InverseLerp(walkSpeed, runSpeed, speed)) * dt;
                float turn = Mathf.Clamp(angle, -rate, rate);
                flat = Quaternion.AngleAxis(turn, Vector3.up) * flat;
                flat = flat.normalized * Mathf.Max(0f, speed - Mathf.Abs(turn) / 90f * cornerLoss * speed);
            }
            float accel = desired.magnitude > flat.magnitude ? (Gait == ElliotGait.Run ? runAcceleration : acceleration) : deceleration;
            flat = Vector3.MoveTowards(flat, desired, accel * dt);

            verticalVelocity = controller.isGrounded ? -2f : verticalVelocity + gravity * dt;
            Vector3 before = transform.position;
            controller.Move((flat + Vector3.up * verticalVelocity) * dt);
            Grounded = controller.isGrounded;
            Vector3 moved = (transform.position - before) / dt;
            velocity = new Vector3(moved.x, verticalVelocity, moved.z);
            // If a wall stopped it, keep only the speed it actually made.
            Vector3 movedFlat = new Vector3(moved.x, 0f, moved.z);
            if (movedFlat.magnitude < flat.magnitude * 0.8f) velocity = movedFlat + Vector3.up * verticalVelocity;
            else velocity = flat + Vector3.up * verticalVelocity;

            // Facing: towards the target if given, otherwise along its movement. Never instant.
            Vector3 look = faceTarget.HasValue ? faceTarget.Value - transform.position : (movedFlat.sqrMagnitude > 0.04f ? movedFlat : transform.forward);
            look.y = 0f;
            float prevYaw = transform.eulerAngles.y;
            if (look.sqrMagnitude > 1e-4f)
            {
                Quaternion want = Quaternion.LookRotation(look);
                float rate = Gait == ElliotGait.Run ? runTurnRate : turnRate;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, rate * dt);
            }
            YawRate = Mathf.DeltaAngle(prevYaw, transform.eulerAngles.y) / dt;
        }
    }
}
