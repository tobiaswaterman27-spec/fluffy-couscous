using System;
using System.Collections.Generic;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Interaction;
using AfterHours.Player;
using AfterHours.Puzzles;

namespace AfterHours.Hiding
{
    /// <summary>
    /// A place to hide (locker, cupboard, wardrobe...). Open its door, then look inside and hold E to hide: you step in
    /// (smoothly, never teleported) and pull the door shut. Inside, you can only look around a little (through the slats).
    /// The doors still work normally from outside, so items kept in lockers can be taken.
    /// Getting in and out makes noise (careful = quiet, rushed = loud). You can carry your item in with you.
    ///
    /// Elliot: if it SAW you go in, it comes straight here and rips it open. Otherwise it only sometimes checks
    /// hiding places while searching (better spots get checked less), may wait outside, and remembers spots.
    /// </summary>
    public class HidingSpot : Interactable, IResettable
    {
        public Door[] doors;
        [Tooltip("Where a hidden person stands (world space, set by the builder).")]
        public Vector3 insidePoint;
        public Vector3 outsidePoint;
        public float facingYaw;
        [Range(0f, 1f)] [Tooltip("How good a spot it is: lower = checked more often.")]
        public float quality = 0.5f;
        public float lookRange = 40f;
        [Tooltip("No door: under a desk or counter, behind a changing-room curtain. Elliot can see you if he gets close.")]
        public bool open;
        public float exposedWithin = 2.2f;
        [Tooltip("The posture you hide in (crawl under a desk, crouch behind a counter...).")]
        public AfterHours.Core.Stance hideStance = AfterHours.Core.Stance.Standing;

        static readonly List<HidingSpot> all = new List<HidingSpot>();
        public static IReadOnlyList<HidingSpot> All => all;

        /// <summary>Raised when someone gets in (Elliot uses it if it was watching).</summary>
        public static event Action<HidingSpot, PlayerController> SomeoneHid;

        public PlayerController Occupant { get; private set; }
        public bool Busy => moveT >= 0f;

        PlayerController mover;
        float moveT = -1f;
        bool entering;
        Vector3 moveFrom, moveTo;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public override float HoldTime => 0.35f;

        public override string GetPrompt(IInteractor who)
        {
            if (Busy) return null;
            PlayerController p = who.Actor != null ? who.Actor.GetComponent<PlayerController>() : null;
            if (p == null) return null;
            if (Occupant == p) return "Get out";
            if (Occupant != null || p.IsHidden) return null;
            if (AnyLocked) return null;
            return hideStance == AfterHours.Core.Stance.Crawling ? "Hide underneath" : "Hide";
        }

        public override void Interact(IInteractor who)
        {
            PlayerController p = who.Actor != null ? who.Actor.GetComponent<PlayerController>() : null;
            if (p == null || Busy) return;
            if (Occupant == p) StartMove(p, false, who.Urgency);
            else if (Occupant == null && !p.IsHidden && !AnyLocked) StartMove(p, true, who.Urgency);
        }

        bool AnyLocked
        {
            get
            {
                if (doors == null) return false;
                foreach (Door d in doors) if (d != null && d.locked) return true;
                return false;
            }
        }

        void OpenDoors(Vector3 from, float urgency, GameObject actor)
        {
            if (doors == null) return;
            foreach (Door d in doors) if (d != null && !d.IsOpen) d.Open(from, urgency, actor);
        }

        void CloseDoors(float urgency, GameObject actor)
        {
            if (doors == null) return;
            foreach (Door d in doors) if (d != null && d.IsOpen) d.Close(urgency, actor);
        }

        /// <summary>Are all the doors shut? (Elliot is less suspicious of a closed one.)</summary>
        public bool Closed
        {
            get
            {
                if (doors == null) return true;
                foreach (Door d in doors) if (d != null && d.IsOpen) return false;
                return true;
            }
        }

        void StartMove(PlayerController p, bool enter, float urgency)
        {
            mover = p;
            entering = enter;
            moveT = 0f;
            moveFrom = p.transform.position;
            moveTo = enter ? insidePoint : outsidePoint;
            p.SetControlLock(true, true);
            OpenDoors(outsidePoint + (outsidePoint - insidePoint), Mathf.Max(0.6f, urgency), p.gameObject);
            NoiseBus.Emit(new NoiseEvent(transform.position, Mathf.Lerp(2f, 9f, Mathf.InverseLerp(0.4f, 1.8f, urgency)), NoiseKind.Interaction, p.gameObject));
            if (enter && hideStance != AfterHours.Core.Stance.Standing && p.Motor != null)
                p.Motor.WarpStance(p.transform.position, p.transform.eulerAngles.y, hideStance);   // get down first
            if (enter)
            {
                // Claimed now, but only actually hidden once inside with the door shut (anyone watching sees you go in).
                Occupant = p;
                p.CurrentHidingSpot = this;
                SomeoneHid?.Invoke(this, p);
            }
            else p.IsHidden = false;
        }

        void Update()
        {
            if (moveT < 0f || mover == null) return;
            if (AfterHours.Creature.CatchSequence.IsBeingCaught(mover)) { moveT = -1f; mover = null; return; }
            moveT += Time.deltaTime / 0.9f;
            // Wait for the door to swing, then step through it.
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((moveT - 0.25f) / 0.75f));
            mover.MoveBodyTo(Vector3.Lerp(moveFrom, moveTo, t), Mathf.LerpAngle(mover.transform.eulerAngles.y, facingYaw, 0.2f));
            if (moveT >= 1f)
            {
                moveT = -1f;
                if (entering) CloseDoors(0.5f, mover.gameObject);
                if (entering)
                {
                    mover.IsHidden = true;
                    mover.SetControlLock(true, false); // hidden: may look around (limited)
                }
                else
                {
                    Occupant = null;
                    mover.IsHidden = false;
                    mover.CurrentHidingSpot = null;
                    mover.SetControlLock(false, false);
                }
                mover = null;
            }
        }

        /// <summary>Elliot pulls it open. Returns whoever was inside (or null).</summary>
        public PlayerController RipOpen(GameObject actor)
        {
            if (doors != null)
                foreach (Door d in doors)
                {
                    if (d == null) continue;
                    d.locked = false;
                    if (!d.IsOpen) d.Open(outsidePoint + (outsidePoint - insidePoint), 2.4f, actor);
                }
            InteractionFx.Play(ProceduralSounds.Sfx.Slam, transform.position, 1f, 20f, actor, NoiseKind.Door);
            return Occupant;
        }

        /// <summary>Removes the occupant (caught, or reset).</summary>
        public void Release()
        {
            if (Occupant != null)
            {
                Occupant.IsHidden = false;
                Occupant.CurrentHidingSpot = null;
            }
            Occupant = null;
            moveT = -1f;
            mover = null;
        }

        public void ResetState() => Release();
    }
}
