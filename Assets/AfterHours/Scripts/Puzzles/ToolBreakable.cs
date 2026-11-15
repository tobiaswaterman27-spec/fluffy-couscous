using System;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Interaction;

namespace AfterHours.Puzzles
{
    /// <summary>
    /// A physical obstacle you break with the right tool: a padlocked chain on a fire exit (wire cutters), a caged
    /// ladder hatch (pry bar). Takes a long hold and is loud - and once broken, the blocker parts fall / vanish.
    /// </summary>
    public class ToolBreakable : Interactable, IPuzzleInput, IResettable
    {
        public string toolId = "cut_wire";
        public string verb = "Cut chain";
        public float seconds = 3f;
        public float noiseRadius = 20f;
        public GameObject[] blockers;

        public bool Satisfied { get; private set; }
        public GameObject LastActor { get; private set; }
        public event Action Changed;

        public override float HoldTime => seconds;

        public override string GetPrompt(IInteractor who)
        {
            if (Satisfied) return null;
            return who.Has(toolId) ? verb + " (hold)" : null;
        }

        public override void Interact(IInteractor who)
        {
            if (Satisfied || !who.Has(toolId)) return;
            LastActor = who.Actor;
            Satisfied = true;
            InteractionFx.Play(ProceduralSounds.Sfx.MetalClank, transform.position, 1f, noiseRadius, who.Actor);
            foreach (GameObject b in blockers) if (b != null) b.SetActive(false);
            Changed?.Invoke();
        }

        /// <summary>Loading a save.</summary>
        public void RestoreSatisfied()
        {
            if (Satisfied) return;
            Satisfied = true;
            foreach (GameObject b in blockers) if (b != null) b.SetActive(false);
            Changed?.Invoke();
        }

        public void ResetState()
        {
            Satisfied = false;
            foreach (GameObject b in blockers) if (b != null) b.SetActive(true);
            Changed?.Invoke();
        }
    }
}
