using UnityEngine;
using AfterHours.Audio;
using AfterHours.Core;
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
    public class Door : Interactable
    {
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

        public override string GetPrompt(IInteractor who)
        {
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
                    InteractionFx.Play(ProceduralSounds.Sfx.Rattle, HandlePosition, 0.8f, 7f * who.Urgency, who.Actor, NoiseKind.Door);
                }
                return;
            }
            if (IsOpen) Close(who.Urgency, who.Actor);
            else Open(who.Position, who.Urgency, who.Actor);
        }

        Vector3 HandlePosition => leaf != null ? leaf.position : transform.position;

        /// <summary>Opens away from 'fromPosition'. urgency: 0.4 careful .. 1 normal .. 2 forceful.</summary>
        public bool Open(Vector3 fromPosition, float urgency, GameObject actor)
        {
            if (locked) return false;
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
            InteractionFx.Play(squeaky ? ProceduralSounds.Sfx.Creak : ProceduralSounds.Sfx.Latch, HandlePosition, Mathf.Clamp01(0.3f + 0.4f * urgency), noise, actor, NoiseKind.Door);
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
        public void Bang(GameObject actor)
        {
            InteractionFx.Play(ProceduralSounds.Sfx.Slam, HandlePosition, 1f, 24f, actor, NoiseKind.Door);
        }

        /// <summary>Breaks a locked door open (if this door can be forced).</summary>
        public bool Force(Vector3 fromPosition, GameObject actor)
        {
            if (!forceable) { Bang(actor); return false; }
            locked = false;
            InteractionFx.Play(ProceduralSounds.Sfx.Slam, HandlePosition, 1f, 30f, actor, NoiseKind.Door);
            return Open(fromPosition, 2.2f, actor);
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
