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
            switch (state)
            {
                case State.Screwed: return who.Has(toolId) ? "Unscrew vent cover" : "Vent cover is screwed shut";
                case State.Loose: return "Remove vent cover";
                default: return CoverNearby() ? "Put vent cover back" : null;
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
                    return;
                default:
                    if (CoverNearby()) Replace(who.Actor);
                    return;
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
            coverBody.AddForce(away.normalized * 1.2f + Vector3.up * 0.5f, ForceMode.VelocityChange);
            InteractionFx.Play(ProceduralSounds.Sfx.MetalClank, cover.position, 0.7f, 8f, actor, NoiseKind.Interaction);
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
