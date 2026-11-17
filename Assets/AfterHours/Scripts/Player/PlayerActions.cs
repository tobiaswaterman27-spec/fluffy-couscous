using UnityEngine;
using AfterHours.Audio;
using AfterHours.Core;
using AfterHours.Creature;
using AfterHours.Interaction;
using AfterHours.World;

namespace AfterHours.Player
{
    /// <summary>What the hands are doing right now (drawn by PlayerBodyAnimator, in first person and for others).</summary>
    public enum Gesture { None, Handle, LockedTry, Rattle, Push, Slam, Barge, Flinch, Grille, Climb, Drop }

    /// <summary>
    /// The physical side of using the world, for one player:
    ///  - Doors: you reach for the handle and press it; try a locked one and it won't give; panic and you rattle it.
    ///    Push one shut or slam it. Sprint into a closed door and you barge it open - it crashes into the wall.
    ///    Sprint into a locked one and you hit it, hard.
    ///  - Vents: pull a grille off with both hands. Climb up into a duct (hands on the sill, haul, knee up, in).
    ///    Climb back out - from a high duct that means tumbling out head first onto the floor.
    /// The climbs take the camera along a path (the body follows); everything else is hands only.
    /// </summary>
    public class PlayerActions : MonoBehaviour
    {
        PlayerController pc;
        PlayerLook look;
        PlayerMotor motor;

        public Gesture Current { get; private set; }
        public float GestureT { get; private set; }
        public float GestureDuration { get; private set; } = 1f;
        public Vector3 GestureTarget { get; private set; }
        public Vector3 GestureNormal { get; private set; }
        public Vector3 GestureAlong { get; private set; }

        /// <summary>True during a scripted climb (the body is moved along a path, not by the motor).</summary>
        public bool Climbing => climb != null;

        float bargeCooldown;
        readonly RaycastHit[] hits = new RaycastHit[6];

        void Awake()
        {
            pc = GetComponent<PlayerController>();
            look = GetComponent<PlayerLook>();
            motor = GetComponent<PlayerMotor>();
        }

        public static PlayerActions Of(GameObject actor)
        {
            if (actor == null) return null;
            var a = actor.GetComponentInParent<PlayerActions>();
            if (a == null && actor.GetComponentInParent<PlayerController>() is PlayerController p) a = p.gameObject.AddComponent<PlayerActions>();
            return a;
        }

        public void Play(Gesture g, Vector3 target, Vector3 normal, Vector3 along, float duration)
        {
            Current = g; GestureT = 0f; GestureDuration = Mathf.Max(0.05f, duration);
            GestureTarget = target; GestureNormal = normal; GestureAlong = along;
        }

        /// <summary>0..1 through the current gesture (1 when idle).</summary>
        public float Progress => Current == Gesture.None ? 1f : Mathf.Clamp01(GestureT / GestureDuration);

        void Update()
        {
            float dt = Time.deltaTime;
            if (Current != Gesture.None)
            {
                GestureT += dt;
                if (GestureT >= GestureDuration && climb == null) Current = Gesture.None;
            }
            bargeCooldown -= dt;
            if (climb != null) { StepClimb(dt); return; }
            if (pc != null && pc.isLocallyControlled && !pc.ControlLocked) CheckBarge();
        }

        // =====================================================================
        // Running into doors
        // =====================================================================

        void CheckBarge()
        {
            if (bargeCooldown > 0f || motor == null) return;
            LocomotionState l = motor.Locomotion;
            if (!l.sprinting || l.planarSpeed < 3.8f) return;
            Vector3 dir = new Vector3(l.worldVelocity.x, 0f, l.worldVelocity.z).normalized;
            Vector3 from = transform.position + Vector3.up * 1.1f;
            int mask = 1 << Mathf.Max(0, GameLayers.InteractableLayer);
            int n = Physics.RaycastNonAlloc(from, dir, hits, 0.75f, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Door d = hits[i].collider.GetComponentInParent<Door>();
                if (d == null || !d.interactable || d.IsOpen || d.IsMoving) continue;
                bargeCooldown = 1.2f;
                Vector3 normal = -dir;
                if (d.Barge(transform.position, gameObject))
                {
                    Play(Gesture.Barge, hits[i].point, normal, Vector3.Cross(Vector3.up, dir), 0.55f);
                    if (look != null) { look.Jolt(0.8f); look.OnLanded(5f); }
                }
                else
                {
                    // locked (or jammed): you hit it full tilt
                    d.BodyHit(gameObject);
                    Play(Gesture.Flinch, hits[i].point, normal, Vector3.Cross(Vector3.up, dir), 0.7f);
                    if (look != null) { look.Jolt(1.4f); look.OnLanded(9f); }
                }
                return;
            }
        }

        // =====================================================================
        // Vents: climbing in and out
        // =====================================================================

        class Climb
        {
            public VentCover vent;
            public bool into;
            public float t, dur;
            public Vector3 eye0, root0, slot, outward, inside, outsideFloor, ductAxis;
            public Quaternion view0;
            public float sill, yawEnd;
            public bool high;
            public int fired;
        }
        Climb climb;
        Vector3 savedRigLocal;
        Quaternion view;

        /// <summary>Can this person climb through this opening now?</summary>
        public bool InsideDuct => VentDuct.Contains(transform.position + Vector3.up * 0.3f);

        public void ClimbIn(VentCover v) => StartClimb(v, true);
        public void ClimbOut(VentCover v) => StartClimb(v, false);

        void StartClimb(VentCover v, bool into)
        {
            if (climb != null || v == null || pc == null || look == null) return;
            VentDuct duct = VentDuct.Find(v.SlotPosition - v.Outward * 0.6f);
            Vector3 outward = v.Outward; outward.y = 0f; outward.Normalize();
            float floorY = FloorBelow(v.SlotPosition + outward * 0.8f);
            float ductFloor = duct != null ? duct.transform.position.y - duct.GetComponent<BoxCollider>().size.y * 0.5f : v.SlotPosition.y - 0.4f;
            Vector3 axis = duct != null ? duct.transform.forward : Vector3.Cross(Vector3.up, outward);
            // look along the duct the way you were already facing
            if (Vector3.Dot(axis, transform.forward) < 0f) axis = -axis;
            climb = new Climb
            {
                vent = v, into = into, t = 0f,
                eye0 = pc.EyePosition, root0 = transform.position,
                view0 = look.playerCamera != null ? look.playerCamera.transform.rotation : transform.rotation,
                slot = v.SlotPosition, outward = outward, ductAxis = axis,
                sill = ductFloor - floorY,
                inside = new Vector3(v.SlotPosition.x, ductFloor, v.SlotPosition.z) - outward * 0.55f + axis * 0.2f,
                outsideFloor = new Vector3(v.SlotPosition.x, floorY, v.SlotPosition.z) + outward * 0.85f,
            };
            climb.high = climb.sill > 0.8f;
            climb.dur = into ? (climb.high ? 1.9f : 1.3f) : (climb.high ? 1.5f : 1.2f);
            climb.yawEnd = into ? Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg : Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
            view = climb.view0;
            pc.SetControlLock(true, true);
            look.ExternallyDriven = true;
            savedRigLocal = look.cameraRig.localPosition;
            Play(into ? Gesture.Climb : Gesture.Drop, v.SlotPosition, outward, Vector3.Cross(Vector3.up, outward), climb.dur);
            InteractionFx.Play(ProceduralSounds.Sfx.VentClimb, v.SlotPosition, 0.6f, into ? 5f : 6f, gameObject, NoiseKind.Interaction);
        }

        float FloorBelow(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit h, 3f, GameLayers.CharacterBlockingMask, QueryTriggerInteraction.Ignore)) return h.point.y;
            return transform.position.y;
        }

        static float E(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / Mathf.Max(1e-4f, b - a)));

        void StepClimb(float dt)
        {
            Climb c = climb;
            c.t += dt;
            float u = Mathf.Clamp01(c.t / c.dur);
            Vector3 eye; Vector3 lookAt; float roll = 0f;
            float crawlEye = 0.32f;
            Vector3 along = c.ductAxis;
            if (c.into)
            {
                // to the wall -> hands on the sill -> haul up (head through the opening) -> knee up -> in, turning along the duct
                Vector3 atWall = new Vector3(c.slot.x, c.root0.y, c.slot.z) + c.outward * 0.42f;
                Vector3 p1 = atWall + Vector3.up * (c.high ? 1.45f : 1.25f);
                Vector3 p2 = new Vector3(c.slot.x, c.inside.y + crawlEye + 0.12f, c.slot.z) + c.outward * 0.05f;
                Vector3 p3 = c.inside + Vector3.up * crawlEye;
                float a = c.high ? 0.22f : 0.2f, b = c.high ? 0.62f : 0.5f, cc = c.high ? 0.82f : 0.75f;
                if (u < a) eye = Vector3.Lerp(c.eye0, p1, E(0f, a, u));
                else if (u < b) eye = Vector3.Lerp(p1, p2, E(a, b, u)) + Vector3.down * Mathf.Sin(E(a, b, u) * Mathf.PI) * 0.12f;
                else eye = Vector3.Lerp(p2, p3, E(b, 1f, u));
                lookAt = u < cc ? c.slot - c.outward * 2f + Vector3.down * 0.1f : p3 + along * 3f;
                if (u > cc) lookAt = Vector3.Lerp(c.slot - c.outward * 2f, p3 + along * 3f, E(cc, 1f, u));
                roll = Mathf.Sin(E(a, b, u) * Mathf.PI) * 8f;
                if (Fire(c, 0, a)) Thump(c.slot, 0.45f);
                if (Fire(c, 1, b)) Thump(c.slot - c.outward * 0.3f, 0.6f);
            }
            else
            {
                Vector3 p1 = new Vector3(c.slot.x, c.inside.y + crawlEye + 0.05f, c.slot.z) + c.outward * 0.15f;   // head out of the opening
                Vector3 floorEyeCrouch = c.outsideFloor + Vector3.up * 1.05f;
                if (c.high)
                {
                    // you tip out head first and fall: hands down, roll, a hard landing on your side, scramble to a crouch
                    Vector3 p2 = c.outsideFloor + Vector3.up * 0.3f + c.outward * 0.15f;    // on the floor
                    if (u < 0.3f) eye = Vector3.Lerp(c.root0 + Vector3.up * crawlEye, p1, E(0f, 0.3f, u));
                    else if (u < 0.48f) { float k = (u - 0.3f) / 0.18f; eye = Vector3.Lerp(p1, p2, k * k); }
                    else eye = Vector3.Lerp(p2, floorEyeCrouch, E(0.62f, 1f, u));
                    lookAt = u < 0.3f ? c.slot + c.outward * 3f + Vector3.down * 0.6f : u < 0.62f ? c.outsideFloor + c.outward * 0.6f : c.outsideFloor + c.outward * 4f + Vector3.up * 1f;
                    roll = u < 0.3f ? 0f : u < 0.62f ? Mathf.Sin(E(0.3f, 0.62f, u) * Mathf.PI) * 55f : 0f;
                    if (Fire(c, 0, 0.45f))
                    {
                        InteractionFx.Play(ProceduralSounds.Sfx.VentDrop, c.outsideFloor, 0.9f, 12f, gameObject, NoiseKind.Landing);
                        if (look != null) look.Jolt(1.3f);
                        Play(Gesture.Flinch, c.outsideFloor, c.outward, along, 0.6f);
                    }
                }
                else
                {
                    if (u < 0.4f) eye = Vector3.Lerp(c.root0 + Vector3.up * crawlEye, p1, E(0f, 0.4f, u));
                    else eye = Vector3.Lerp(p1, floorEyeCrouch, E(0.4f, 1f, u)) + Vector3.down * Mathf.Sin(E(0.4f, 1f, u) * Mathf.PI) * 0.1f;
                    lookAt = c.slot + c.outward * 3f + Vector3.down * (u < 0.5f ? 0.6f : 0f);
                    if (Fire(c, 0, 0.5f)) Thump(c.slot + c.outward * 0.4f, 0.5f);
                }
            }

            // body: stand on the floor under the eye (the animator bends it to fit)
            Vector3 dir = lookAt - eye;
            if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            float yaw = flat.sqrMagnitude > 1e-4f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : transform.eulerAngles.y;
            float rootY = c.into ? (u < 0.5f ? c.root0.y : Mathf.Lerp(c.root0.y, c.inside.y, E(0.4f, 0.8f, u))) : (u < (c.high ? 0.45f : 0.5f) ? c.inside.y : c.outsideFloor.y);
            Vector3 root = new Vector3(eye.x, rootY, eye.z) - Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 0.15f;
            pc.MoveBodyTo(root, yaw);
            look.cameraRig.position = eye;
            Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
            view = Quaternion.Slerp(view, want, 1f - Mathf.Exp(-9f * dt));
            look.cameraRig.rotation = view;
            if (look.playerCamera != null) { look.playerCamera.transform.localPosition = Vector3.zero; look.playerCamera.transform.localRotation = Quaternion.identity; }

            if (c.t >= c.dur) EndClimb();
        }

        bool Fire(Climb c, int bit, float at)
        {
            if (c.t / c.dur < at || (c.fired & (1 << bit)) != 0) return false;
            c.fired |= 1 << bit;
            return true;
        }

        void Thump(Vector3 at, float vol)
        {
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.DuctBuckle), at, vol * 0.5f, Random.Range(0.9f, 1.15f), 25f);
            NoiseBus.Emit(new NoiseEvent(at, 7f * vol, NoiseKind.Interaction, gameObject));
        }

        void EndClimb()
        {
            Climb c = climb;
            climb = null;
            Current = Gesture.None;
            look.ExternallyDriven = false;
            look.cameraRig.localPosition = savedRigLocal;
            pc.SetControlLock(false, false);
            if (c.into) motor.WarpStance(c.inside, c.yawEnd, Stance.Crawling);
            else motor.WarpStance(c.outsideFloor, c.yawEnd, Stance.Crouching);
            look.Initialize(c.yawEnd);
        }
    }
}
