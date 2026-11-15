using System;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Interaction;

namespace AfterHours.Puzzles
{
    /// <summary>A wall card reader. The right card (held in hand) turns its light green; the card is not used up.</summary>
    public class CardReader : Interactable, IPuzzleInput, IResettable
    {
        public string accepts = "card_restricted";
        public Renderer indicator;
        public Material redMat, greenMat;
        public string verb = "Swipe card";
        public string idleName = "Card reader";

        public bool Satisfied { get; private set; }
        public GameObject LastActor { get; private set; }
        public event Action Changed;

        public override float HoldTime => 0.3f;

        public override string GetPrompt(IInteractor who)
        {
            if (Satisfied) return null;
            return who.Has(accepts) ? verb : idleName;
        }

        public override void Interact(IInteractor who)
        {
            if (Satisfied) return;
            if (!who.Has(accepts))
            {
                InteractionFx.Play(ProceduralSounds.Sfx.Bump, transform.position, 0.4f, 2f, who.Actor);
                return;
            }
            LastActor = who.Actor;
            Satisfied = true;
            if (indicator != null && greenMat != null) indicator.sharedMaterial = greenMat;
            InteractionFx.Play(ProceduralSounds.Sfx.Unlock, transform.position, 0.6f, 3f, who.Actor);
            Changed?.Invoke();
        }

        public void ResetState()
        {
            Satisfied = false;
            if (indicator != null && redMat != null) indicator.sharedMaterial = redMat;
            Changed?.Invoke();
        }
    }
}
