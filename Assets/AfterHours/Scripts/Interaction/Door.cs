using UnityEngine;
using AfterHours.Audio;
using AfterHours.Core;
using AfterHours.Puzzles;
using AfterHours.World;

namespace AfterHours.Interaction
{
    public enum DoorWeight { Light, Normal, Heavy, VeryHeavy }

    /// <summary>
    /// A hinged door. This object sits on the hinge line and rotates; the leaf (child) carries the collider.
    /// - Swings away from whoever opens it, at a speed set by its weight and how carefully it is handled.
    /// - Never moves into a person, creature or prop: it stops and bumps instead (no clipping, no shoving).
    /// - Makes noise to match: easing it open while crouched is quiet; sprinting through or slamming is loud.
    /// - Can be locked (needs the right key: Part 6), squeaky, or faulty (sticks part-way the first time).
    /// - Elliot uses the same API (Open / Close / Bang / Force) in Part 9.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Door : Interactable, IResettable
    {
        [Tooltip("False when something else (a hiding place) handles using this door.")]
        public bool interactable = true;
        [Header("Shape")]
        public Transform leaf;
        public float width = 0.9f;
        public float height = 2.1f;
        public float thickness = 0.045f;
        public float maxAngle = 100f;

        [Header("Character")]
        public DoorKind kind = DoorKind.Staff;
        public DoorWeight weight = DoorWeight.Normal;
        public bool locked;
        public string lockId = "";
        public bool squeaky;
        public bool faulty;
        [Tooltip("Can a strong force (Elliot) break it open?")]
        public bool forceable = true;
        [Tooltip("0 = swings away from whoever opens it. +1 / -1 = always swings to that side (cupboards, lockers).")]
        public int fixedSide;

        public float Angle => angle;
        public bool IsOpen => Mathf.Abs(angle) > 8f || Mathf.Abs(target) > 8f;
        public bool IsMoving => moving;

        float angle;
        float target;
        float speed;
        float currentSpeed;
        float urgency = 1f;
        bool moving;
        bool faultTriggered;
        float creakTimer;
        Quaternion closedRotation;
        Rigidbody body;
        BoxCollider leafCollider;
        readonly Collider[] overlap = new Collider[8];

        void Awake()
        {
            closedRotation = transform.localRotation;
            startLocked = locked;
            startLockId = lockId;
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            if (leaf != null) leafCollider = leaf.GetComponent<BoxCollider>();
            enabled = false; // only updates while moving
        }

        float BaseSpeed
        {
            get
            {
                switch (weight)
                {
                    case DoorWeight.Light: return 210f;
                    case DoorWeight.Heavy: return 85f;
                    case DoorWeight.VeryHeavy: return 55f;
                    default: return 140f;
                }
            }
        }

        public override float HoldTime
        {
            get
            {
                if (locked) return 0.15f;
                switch (weight)
                {
                    case DoorWeight.Heavy: return 0.35f;
                    case DoorWeight.VeryHeavy: return 0.55f;
                    default: return 0.18f;
                }
            }
        }

        bool startLocked;
        string startLockId;

        /// <summary>Back to its starting state: closed, and locked again if it started locked.</summary>
        public void ResetState()
        {
            moving = false;
            angle = 0f;
            target = 0f;
            blows = 0;
            faultTriggered = false;
            locked = startLocked;
            lockId = startLockId;
            Quaternion world = (transform.parent != null ? transform.parent.rotation : Quaternion.identity) * closedRotation;
            transform.rotation = world;
            if (body != null) body.rotation = world;
        }

        public float TargetAngle => target;

        /// <summary>LAN co-op client: swing to the angle the host reports (no sound or noise of its own).</summary>
        public void NetMoveTo(float a)
        {
            if (Mathf.Abs(a - target) < 0.5f && (moving || Mathf.Abs(a - angle) < 0.5f)) return;
            StartMove(a, 1f);
        }

        /// <summary>Loading a save: jump straight to an angle (no sound, no noise).</summary>
        public void SetAngleInstant(float a)
        {
            moving = false;
            angle = target = Mathf.Clamp(a, -maxAngle, maxAngle);
            Quaternion world = (transform.parent != null ? transform.parent.rotation : Quaternion.identity) * closedRotation * Quaternion.Euler(0f, angle, 0f);
            transform.rotation = world;
            if (body != null) body.rotation = world;
        }

        public override string GetPrompt(IInteractor who)
        {
            if (!interactable) return null;
            if (locked) return who.Has(lockId) ? "Unlock" : "Locked";
            if (moving) return null;
            return IsOpen ? "Close" : "Open";
        }

        public override void Interact(IInteractor who)
        {
            if (locked)
            {
                if (!string.IsNullOrEmpty(lockId) && who.Has(lockId))
                {
                    locked = false;
                    InteractionFx.Play(ProceduralSounds.Sfx.Unlock, HandlePosition, 0.7f, 3f, who.Actor, NoiseKind.Door);
                }
                else
                {
                    // a calm try: one firm press of the handle that won't give. In a panic: you rattle it like mad.
                    bool panic = who.Urgency > 1.3f;
                    InteractionFx.Play(panic ? ProceduralSounds.Sfx.Rattle : ProceduralSounds.Sfx.LockedTry, HandlePosition, panic ? 0.85f : 0.6f,
                        (panic ? 9f : 4f) * who.Urgency, who.Actor, NoiseKind.Door);
                    Hands(who, panic ? Player.Gesture.Rattle : Player.Gesture.LockedTry, panic ? 1.0f : 0.6f);
                }
                if (!locked) Hands(who, Player.Gesture.Handle, 0.5f);
                return;
            }
            if (IsOpen)
            {
                Hands(who, who.Urgency > 1.4f ? Player.Gesture.Slam : Player.Gesture.Push, who.Urgency > 1.4f ? 0.4f : 0.5f);
                Close(who.Urgency, who.Actor);
            }
            else
            {
                Hands(who, Player.Gesture.Handle, Mathf.Lerp(0.7f, 0.4f, Mathf.InverseLerp(0.4f, 1.8f, who.Urgency)));
                Open(who.Position, who.Urgency, who.Actor);
            }
        }

        /// <summary>Where the handle is (the free edge of the leaf, at hand height).</summary>
        public Vector3 HandleWorld
        {
            get
            {
                if (leaf == null) return transform.position + Vector3.up;
                Vector3 lp = leaf.localPosition;
                float edge = Mathf.Abs(lp.x) > 0.01f ? lp.x + Mathf.Sign(lp.x) * (width * 0.5f - 0.08f) : lp.x;
                return transform.TransformPoint(new Vector3(edge, lp.y - height * 0.5f + 1.0f, lp.z));
            }
        }

        void Hands(IInteractor who, Player.Gesture g, float duration)
        {
            Player.PlayerActions a = Player.PlayerActions.Of(who.Actor);
            if (a == null) return;
            Vector3 handle = HandleWorld;
            Vector3 n = who.Position - handle; n.y = 0f;
            Vector3 normal = n.sqrMagnitude > 1e-4f ? n.normalized : -transform.forward;
            a.Play(g, handle + normal * 0.05f, normal, (leaf != null ? leaf.position - transform.position : transform.right).normalized, duration);
        }

        /// <summary>
        /// Someone sprinted into this door: it bursts open away from them and smashes into the wall.
        /// Returns false if it's locked (they just hit it).
        /// </summary>
        public bool Barge(Vector3 fromPosition, GameObject actor)
        {
            if (locked || !interactable) return false;
            barged = true;
            InteractionFx.Play(ProceduralSounds.Sfx.DoorBarge, HandlePosition, 1f, 20f, actor, NoiseKind.Door);
            bool ok = OpenSilently(fromPosition, 2.5f);
            return ok;
        }

        /// <summary>Someone ran full tilt into this door and it didn't open.</summary>
        public void BodyHit(GameObject actor)
        {
            InteractionFx.Play(ProceduralSounds.Sfx.BodyDoor, HandlePosition, 1f, 16f, actor, NoiseKind.Door);
            WorldFX.Dust(HandlePosition + Vector3.up * 1.0f, Vector3.down, 6);
        }

        bool barged;

        Vector3 HandlePosition => leaf != null ? leaf.position : transform.position;

        bool OpenSilently(Vector3 fromPosition, float urgency)
        {
            Vector3 normal = transform.parent != null ? transform.parent.rotation * closedRotation * Vector3.forward : closedRotation * Vector3.forward;
            float side = fixedSide != 0 ? fixedSide : (Vector3.Dot(fromPosition - transform.position, normal) >= 0f ? 1f : -1f);
            faultTriggered = true;
            StartMove(side * maxAngle, urgency);
            return true;
        }

        /// <summary>Opens away from 'fromPosition'. urgency: 0.4 careful .. 1 normal .. 2 forceful.</summary>
        public bool Open(Vector3 fromPosition, float urgency, GameObject actor)
        {
            if (locked) return false;
            barged = false;
            Vector3 normal = transform.parent != null ? transform.parent.rotation * closedRotation * Vector3.forward : closedRotation * Vector3.forward;
            float side = fixedSide != 0 ? fixedSide : (Vector3.Dot(fromPosition - transform.position, normal) >= 0f ? 1f : -1f);
            float goal = side * maxAngle;
            if (faulty && !faultTriggered)
            {
                goal = side * 32f; // sticks part-way; works properly next time
                faultTriggered = true;
            }
            StartMove(goal, urgency);
            float noise = Mathf.Lerp(1.5f, 10f, Mathf.InverseLerp(0.4f, 1.8f, urgency)) + (squeaky ? 5f : 0f);
            InteractionFx.Play(squeaky ? ProceduralSounds.Sfx.Creak : ProceduralSounds.Sfx.Handle, HandlePosition, Mathf.Clamp01(0.3f + 0.4f * urgency), noise, actor, NoiseKind.Door);
            return true;
        }

        public void Close(float urgency, GameObject actor)
        {
            StartMove(0f, urgency);
            if (squeaky) InteractionFx.Play(ProceduralSounds.Sfx.Creak, HandlePosition, 0.5f, 5f, actor, NoiseKind.Door);
            closer = actor;
        }

        GameObject closer;

        /// <summary>Loud banging (Elliot hammering on a door it cannot open).</summary>
        int blows; // how battered this door is: every blow cracks it a bit more

        public void Bang(GameObject actor)
        {
            blows++;
            InteractionFx.Play(ProceduralSounds.Sfx.Bang, HandlePosition, 1f, 24f, actor, NoiseKind.Door);
            // the frame shakes: dust and grit sift down from the top of it, more with every blow
            WorldFX.Dust(HandlePosition + Vector3.up * 1.0f, Vector3.down, 6 + blows * 4);
            // The wood splitting gets louder with every blow - you can hear the door giving up.
            if (blows >= 2) AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.Crack), HandlePosition, Mathf.Clamp01(0.45f + 0.15f * blows), Random.Range(0.9f, 1.05f), 60f);
            // A second, deeper layer from the same spot carries much further through the building...
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.Bang), HandlePosition, 1f, Random.Range(0.72f, 0.8f), 80f);
            // ...and if you're right on the other side of the door, you feel it.
            Camera cam = Camera.main;
            if (cam != null && Vector3.Distance(cam.transform.position, HandlePosition) < 7f)
            {
                AudioPool.Play2D(AudioLibrary.Get("sub_hit"), Mathf.InverseLerp(7f, 1f, Vector3.Distance(cam.transform.position, HandlePosition)), Random.Range(0.9f, 1.1f));
                AudioMix.Kick(0.6f, 0.8f);
            }
        }

        /// <summary>Breaks a locked door open (if this door can be forced).</summary>
        public bool Force(Vector3 fromPosition, GameObject actor)
        {
            if (!forceable) { Bang(actor); return false; }
            locked = false;
            // It gives way: the frame splits, the lock tears out, the door crashes back into the wall.
            InteractionFx.Play(ProceduralSounds.Sfx.DoorBreak, HandlePosition, 1f, 40f, actor, NoiseKind.Door);
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.DoorBreak), HandlePosition, 1f, 0.75f, 90f);
            // splinters burst out of the frame where the lock was, away from him
            Vector3 away = HandlePosition - fromPosition; away.y = 0f;
            WorldFX.Splinters(HandlePosition, away.normalized + Vector3.up * 0.3f, 70);
            Camera cam = Camera.main;
            if (cam != null && Vector3.Distance(cam.transform.position, HandlePosition) < 12f)
            {
                AudioPool.Play2D(AudioLibrary.Get("sub_hit"), 1f, 0.8f);
                AudioMix.Kick(1f, 1.2f);
            }
            blows = 0;
            return Open(fromPosition, 2.6f, actor);
        }

        void StartMove(float goal, float u)
        {
            target = Mathf.Clamp(goal, -maxAngle, maxAngle);
            urgency = Mathf.Clamp(u, 0.3f, 2.5f);
            speed = BaseSpeed * urgency;
            currentSpeed = 0f;
            moving = true;
            enabled = true;
        }

        void FixedUpdate()
        {
            if (!moving) { enabled = false; return; }
            float dt = Time.fixedDeltaTime;
            currentSpeed = Mathf.MoveTowards(currentSpeed, speed, speed * 4f * dt); // doors have inertia
            float next = Mathf.MoveTowards(angle, target, currentSpeed * dt);

            if (Blocked(next))
            {
                moving = false;
                currentSpeed = 0f;
                InteractionFx.Play(ProceduralSounds.Sfx.Bump, HandlePosition, 0.5f, 3f, gameObject, NoiseKind.Door);
                return;
            }

            angle = next;
            Quaternion world = (transform.parent != null ? transform.parent.rotation : Quaternion.identity) * closedRotation * Quaternion.Euler(0f, angle, 0f);
            body.MoveRotation(world);

            if (squeaky)
            {
                creakTimer -= dt;
                if (creakTimer <= 0f && Mathf.Abs(target - angle) > 15f)
                {
                    creakTimer = 0.9f;
                    InteractionFx.Play(ProceduralSounds.Sfx.Creak, HandlePosition, 0.35f, 4f, gameObject, NoiseKind.Door);
                }
            }

            if (Mathf.Approximately(angle, target))
            {
                moving = false;
                // flung open hard: it crashes into the wall (a barge has that crash in its own sound)
                if (Mathf.Abs(target) >= maxAngle - 0.5f && urgency > 1.6f && !barged)
                    InteractionFx.Play(ProceduralSounds.Sfx.WallHit, HandlePosition, 0.9f, 14f, gameObject, NoiseKind.Door);
                barged = false;
                if (Mathf.Abs(target) < 0.01f)
                {
                    // Closing: a gentle latch, or a slam if it was swung shut hard.
                    bool slam = urgency > 1.4f;
                    InteractionFx.Play(slam ? ProceduralSounds.Sfx.Slam : ProceduralSounds.Sfx.Latch, HandlePosition, slam ? 1f : 0.45f,
                        slam ? 18f : Mathf.Lerp(1.5f, 5f, urgency), closer != null ? closer : gameObject, NoiseKind.Door);
                }
            }
        }

        /// <summary>Would the leaf overlap a person, creature or loose prop at this angle?</summary>
        bool Blocked(float testAngle)
        {
            if (leaf == null || leafCollider == null) return false;
            Quaternion parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Quaternion rot = parentRot * closedRotation * Quaternion.Euler(0f, testAngle, 0f);
            Vector3 centre = transform.position + rot * leaf.localPosition;
            Vector3 half = Vector3.Scale(leaf.localScale, leafCollider.size) * 0.5f * 0.96f;
            int mask = 0;
            if (GameLayers.PlayerLayer >= 0) mask |= 1 << GameLayers.PlayerLayer;
            if (GameLayers.CreatureLayer >= 0) mask |= 1 << GameLayers.CreatureLayer;
            if (GameLayers.PropLayer >= 0) mask |= 1 << GameLayers.PropLayer;
            int n = Physics.OverlapBoxNonAlloc(centre, half, overlap, rot, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!overlap[i].transform.IsChildOf(transform)) return true;
            }
            return false;
        }
    }
}
