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
        public ProceduralSounds.Sfx sound = ProceduralSounds.Sfx.MetalClank;
        [Tooltip("Hard use: wears the tool (a fire axe only lasts a few jobs).")]
        public bool wearsTool = true;

        [Tooltip("Other ways to clear the same obstacle (cut the chain / unlock its padlock): done with this, they go too.")]
        public ToolBreakable[] alternatives;

        bool NeedsTool => !string.IsNullOrEmpty(toolId);

        /// <summary>Once done, its aiming zone goes so it doesn't get in the way of whatever is behind.</summary>
        void SetAimable(bool on)
        {
            foreach (Collider c in GetComponents<Collider>()) if (c.isTrigger) c.enabled = on;
        }

        void Cleared()
        {
            SetAimable(false);
            if (alternatives != null) foreach (ToolBreakable t in alternatives) if (t != null) t.SetAimable(false);
        }
        bool CanDo(IInteractor who) => !NeedsTool || who.Has(toolId);

        public bool Satisfied { get; private set; }
        public GameObject LastActor { get; private set; }
        public event Action Changed;

        public override float HoldTime => seconds;

        public override string GetPrompt(IInteractor who)
        {
            if (Satisfied) return null;
            if (blockers != null && blockers.Length > 0)
            {
                bool any = false;
                foreach (GameObject b in blockers) if (b != null && b.activeSelf) any = true;
                if (!any) return null; // already cleared another way
            }
            return CanDo(who) ? verb + (seconds > 0.4f ? " (hold)" : "") : null;
        }

        public override void Interact(IInteractor who)
        {
            if (Satisfied || !CanDo(who)) return;
            LastActor = who.Actor;
            Satisfied = true;
            InteractionFx.Play(sound, transform.position, 1f, noiseRadius, who.Actor);
            if (NeedsTool && wearsTool)
            {
                AfterHours.Items.PlayerInventory inv = who.Actor != null ? who.Actor.GetComponent<AfterHours.Items.PlayerInventory>() : null;
                if (inv != null && inv.Held != null) inv.Held.Wear(who.Actor);
            }
            foreach (GameObject b in blockers) if (b != null) b.SetActive(false);
            Cleared();
            Changed?.Invoke();
        }

        /// <summary>Loading a save.</summary>
        public void RestoreSatisfied()
        {
            if (Satisfied) return;
            Satisfied = true;
            foreach (GameObject b in blockers) if (b != null) b.SetActive(false);
            Cleared();
            Changed?.Invoke();
        }

        public void ResetState()
        {
            Satisfied = false;
            SetAimable(true);
            foreach (GameObject b in blockers) if (b != null) b.SetActive(true);
            Changed?.Invoke();
        }
    }
}
