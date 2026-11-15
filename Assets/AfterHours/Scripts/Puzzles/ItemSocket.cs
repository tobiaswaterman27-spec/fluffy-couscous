using System;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Interaction;
using AfterHours.Items;

namespace AfterHours.Puzzles
{
    /// <summary>
    /// A place where a specific item fits: a fuse into a fuse box, a handle onto a mechanism, a valve wheel onto a
    /// stem, a control switch into a panel. Optionally the fitted part must then be turned (hold) to finish.
    /// You can see what is missing (an empty holder), which is the clue - there are no written instructions.
    /// Fitted items stay in the world (they are not deleted) so a reset can return them.
    /// </summary>
    public class ItemSocket : Interactable, IPuzzleInput, IResettable
    {
        [Tooltip("Item id (or 'provides' id) that fits here.")]
        public string accepts = "fuse";
        public string label = "fuse";
        public Transform mountPoint;
        [Tooltip("If > 0: after fitting, hold for this many seconds to turn / work it.")]
        public float turnSeconds;
        public float turnNoiseRadius = 14f;
        public float fitNoiseRadius = 4f;

        public bool Satisfied { get; private set; }
        public GameObject LastActor { get; private set; }
        public event Action Changed;
        public bool Filled => fitted != null;

        ItemPickup fitted;
        float turned;
        float noiseTimer;

        public override float HoldTime => Filled ? 0.25f : 0.6f;
        public override bool Continuous => Filled && turnSeconds > 0f && !Satisfied;

        public override string GetPrompt(IInteractor who)
        {
            if (Satisfied) return null;
            if (!Filled)
            {
                PlayerInventory inv = who.Actor != null ? who.Actor.GetComponent<PlayerInventory>() : null;
                bool holding = inv != null && inv.Held != null && inv.Held.Definition != null && inv.Held.Definition.Provides(accepts);
                return holding ? "Fit " + label : null; // without the part there's nothing to do (the empty holder is the clue)
            }
            return turnSeconds > 0f ? "Turn " + label + " (hold)" : null;
        }

        public override void Interact(IInteractor who)
        {
            if (Filled) return;
            PlayerInventory inv = who.Actor != null ? who.Actor.GetComponent<PlayerInventory>() : null;
            if (inv == null || inv.Held == null || inv.Held.Definition == null || !inv.Held.Definition.Provides(accepts)) return;

            Fit(inv.TakeHeld(), who.Actor);
        }

        public void Fit(ItemPickup item, GameObject actor)
        {
            fitted = item;
            item.AttachTo(gameObject, mountPoint != null ? mountPoint : transform); // glides into place and stays there
            LastActor = actor;
            InteractionFx.Play(ProceduralSounds.Sfx.Click, transform.position, 0.7f, fitNoiseRadius, actor);
            if (turnSeconds <= 0f) SetSatisfied(true);
        }

        public override void HoldTick(IInteractor who, float dt)
        {
            if (!Filled || Satisfied) return;
            turned += dt;
            noiseTimer -= dt;
            if (noiseTimer <= 0f)
            {
                noiseTimer = 0.8f;
                InteractionFx.Play(ProceduralSounds.Sfx.Creak, transform.position, 0.8f, turnNoiseRadius, who.Actor);
            }
            if (turned >= turnSeconds)
            {
                LastActor = who.Actor;
                InteractionFx.Play(ProceduralSounds.Sfx.MetalClank, transform.position, 1f, turnNoiseRadius, who.Actor);
                SetSatisfied(true);
            }
        }

        public ItemPickup FittedItem => fitted;

        /// <summary>Loading a save: put the item in (and finish turning it if it had been turned).</summary>
        public void RestoreFitted(ItemPickup item, bool satisfied)
        {
            if (item == null || fitted != null) return;
            fitted = item;
            item.AttachTo(gameObject, mountPoint != null ? mountPoint : transform);
            if (satisfied || turnSeconds <= 0f) { turned = turnSeconds; SetSatisfied(true); }
        }

        void SetSatisfied(bool value)
        {
            if (Satisfied == value) return;
            Satisfied = value;
            Changed?.Invoke();
        }

        /// <summary>Back to empty; the fitted item is put back where it came from.</summary>
        public void ResetState()
        {
            if (fitted != null)
            {
                fitted.Release(fitted.HomePosition + Vector3.up * 0.05f, Vector3.zero);
                fitted = null;
            }
            turned = 0f;
            SetSatisfied(false);
        }
    }
}
