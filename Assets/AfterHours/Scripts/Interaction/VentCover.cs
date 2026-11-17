using UnityEngine;
using AfterHours.Audio;
using AfterHours.Core;
using AfterHours.World;

namespace AfterHours.Interaction
{
    /// <summary>
    /// A vent grille over a duct opening.
    /// - Screwed: needs a screwdriver to undo (long, quiet hold). Loose: lift it off (it drops as a physics object).
    /// - Removed covers can be put back: look at the open vent with the cover nearby and hold Interact; the cover
    ///   moves back into place (smoothly, not teleported) so you can close a vent behind you.
    /// - Elliot can pull covers off too (Part 9) and is less likely to search vents that look closed.
    /// </summary>
    public class VentCover : Interactable, AfterHours.Puzzles.IResettable
    {
        State startState;
        static readonly System.Collections.Generic.List<VentCover> all = new System.Collections.Generic.List<VentCover>();
        public static System.Collections.Generic.IReadOnlyList<VentCover> All => all;
        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);
        /// <summary>Where the opening is (centre of the grille slot).</summary>
        public Vector3 SlotPosition => slotPos;
        public Vector3 Outward => transform.forward;

        public void ResetState()
        {
            animT = -1f;
            coverBody.isKinematic = true;
            cover.SetPositionAndRotation(slotPos, slotRot);
            state = startState;
        }

        public enum State { Screwed, Loose, Removed }

        public State state = State.Loose;
        public string toolId = "screwdriver";
        public Transform cover;            // the grille object (has collider, renderer)
        public float replaceReach = 2.2f;

        Vector3 slotPos;
        Quaternion slotRot;
        Rigidbody coverBody;
        float animT = -1f;
        Vector3 animFromPos;
        Quaternion animFromRot;

        public bool IsOpen => state == State.Removed;

        void Awake()
        {
            startState = state;
            if (cover == null) cover = transform.GetChild(0);
            slotPos = cover.position;
            slotRot = cover.rotation;
            coverBody = cover.GetComponent<Rigidbody>();
            if (coverBody == null) coverBody = cover.gameObject.AddComponent<Rigidbody>();
            coverBody.mass = 2.5f;
            coverBody.isKinematic = state != State.Removed;
            if (cover.GetComponent<PhysicsProp>() == null) cover.gameObject.AddComponent<PhysicsProp>().material = SurfaceType.Metal;

            // an invisible target filling the opening, so you can climb through it (or pull the cover back)
            // from either side once the grille is off
            Collider cc = cover.GetComponent<Collider>();
            Vector3 size = cc != null ? cc.bounds.size : new Vector3(0.9f, 0.9f, 0.05f);
            var opening = new GameObject("Vent opening (target)");
            opening.transform.SetParent(transform, false);
            opening.transform.SetPositionAndRotation(slotPos, slotRot);
            opening.layer = cover.gameObject.layer;
            BoxCollider box = opening.AddComponent<BoxCollider>();
            box.isTrigger = true;
            Vector3 local = Quaternion.Inverse(slotRot) * size;
            box.size = new Vector3(Mathf.Max(0.3f, Mathf.Abs(local.x)), Mathf.Max(0.3f, Mathf.Abs(local.y)), 0.5f);
        }

        public override float HoldTime
        {
            get
            {
                switch (state)
                {
                    case State.Screwed: return 2.5f;   // unscrewing takes a while
                    case State.Loose: return 0.5f;
                    default: return 0.8f;
                }
            }
        }

        public override string GetPrompt(IInteractor who)
        {
            if (animT >= 0f) return null;
            bool inside = VentDuct.Contains(who.Position + Vector3.up * 0.3f);
            switch (state)
            {
                case State.Screwed: return inside ? null : who.Has(toolId) ? "Unscrew vent cover" : "Vent cover is screwed shut";
                case State.Loose: return inside ? "Push the vent cover off" : "Remove vent cover";
                default:
                    if (inside) return CoverNearby() && Vector3.Distance(cover.position, slotPos) > 0.05f && CoverReachableFromInside() ? "Pull the vent cover back" : "Climb out";
                    return "Climb into the vent";
            }
        }

        public override void Interact(IInteractor who)
        {
            switch (state)
            {
                case State.Screwed:
                    if (who.Has(toolId))
                    {
                        state = State.Loose;
                        InteractionFx.Play(ProceduralSounds.Sfx.Click, cover.position, 0.4f, 1.5f, who.Actor);
                    }
                    return;
                case State.Loose:
                    Remove(who.Position, who.Actor);
                    if (Player.PlayerActions.Of(who.Actor) is Player.PlayerActions pa)
                        pa.Play(Player.Gesture.Grille, slotPos, Outward * (VentDuct.Contains(who.Position + Vector3.up * 0.3f) ? -1f : 1f), Vector3.Cross(Vector3.up, Outward), 0.8f);
                    return;
                default:
                {
                    bool inside = VentDuct.Contains(who.Position + Vector3.up * 0.3f);
                    var a = Player.PlayerActions.Of(who.Actor);
                    if (inside && CoverNearby() && CoverReachableFromInside()) { Replace(who.Actor); return; }
                    if (a == null) return;
                    if (inside) a.ClimbOut(this); else a.ClimbIn(this);
                    return;
                }
            }
        }

        /// <summary>Takes the cover off: it comes away towards the person and falls as a physics object.</summary>
        public void Remove(Vector3 towards, GameObject actor)
        {
            if (state == State.Removed) return;
            state = State.Removed;
            coverBody.isKinematic = false;
            Vector3 away = (towards - cover.position);
            away.y = 0f;
            // pushed off from inside, it falls out into the corridor instead
            if (VentDuct.Contains(towards + Vector3.up * 0.3f)) away = Outward * 0.6f;
            coverBody.AddForce(away.normalized * 1.2f + Vector3.up * 0.5f, ForceMode.VelocityChange);
            InteractionFx.Play(ProceduralSounds.Sfx.GrillePull, cover.position, 0.75f, 8f, actor, NoiseKind.Interaction);
        }

        /// <summary>Elliot tears it out of the wall and flings it aside.</summary>
        public void RipOut(Vector3 from, GameObject actor)
        {
            if (state == State.Removed) return;
            state = State.Removed;
            coverBody.isKinematic = false;
            Vector3 side = Vector3.Cross(Vector3.up, Outward) * (Random.value < 0.5f ? -1f : 1f);
            coverBody.AddForce(Outward * 3.5f + side * 2.5f + Vector3.up * 1.5f, ForceMode.VelocityChange);
            coverBody.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);
            InteractionFx.Play(ProceduralSounds.Sfx.GrilleRip, slotPos, 1f, 22f, actor, NoiseKind.Interaction);
            WorldFX.Dust(slotPos, Outward, 18);
        }

        /// <summary>From inside you can only pull back a cover that is lying right by the opening.</summary>
        bool CoverReachableFromInside() => Vector3.Distance(cover.position, slotPos) < 1.0f;

        /// <summary>Loading a save: the cover's state and (if off) where it lies.</summary>
        public void RestoreState(State s, Vector3 coverPos, Quaternion coverRot)
        {
            animT = -1f;
            state = s;
            if (s == State.Removed)
            {
                coverBody.isKinematic = false;
                cover.SetPositionAndRotation(coverPos, coverRot);
                coverBody.position = coverPos;
                coverBody.rotation = coverRot;
            }
            else
            {
                coverBody.isKinematic = true;
                cover.SetPositionAndRotation(slotPos, slotRot);
            }
        }

        public void Replace(GameObject actor)
        {
            coverBody.isKinematic = true;
            animFromPos = cover.position;
            animFromRot = cover.rotation;
            animT = 0f;
            InteractionFx.Play(ProceduralSounds.Sfx.MetalClank, slotPos, 0.4f, 4f, actor, NoiseKind.Interaction);
        }

        bool CoverNearby() => Vector3.Distance(cover.position, slotPos) <= replaceReach;

        void Update()
        {
            if (animT < 0f) return;
            animT += Time.deltaTime / 0.6f;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(animT));
            coverBody.MovePosition(Vector3.Lerp(animFromPos, slotPos, t));
            coverBody.MoveRotation(Quaternion.Slerp(animFromRot, slotRot, t));
            if (animT >= 1f)
            {
                animT = -1f;
                state = State.Loose;
                InteractionFx.Play(ProceduralSounds.Sfx.Click, slotPos, 0.4f, 2f, gameObject);
            }
        }
    }
}
