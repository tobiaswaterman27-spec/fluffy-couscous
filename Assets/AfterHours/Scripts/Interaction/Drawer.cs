using UnityEngine;
using AfterHours.Audio;

namespace AfterHours.Interaction
{
    /// <summary>A drawer that slides out along its local forward axis (desks, filing cabinets, counters).</summary>
    public class Drawer : Interactable, AfterHours.Puzzles.IResettable
    {
        public void ResetState()
        {
            pos = target = 0f;
            transform.localPosition = closedLocal;
            enabled = false;
        }

        public float travel = 0.38f;
        public float speed = 0.9f;
        public bool locked;
        public string lockId = "";

        Vector3 closedLocal;
        float pos;
        float target;

        public bool IsOpen => target > 0f;

        void Awake()
        {
            closedLocal = transform.localPosition;
            enabled = false;
        }

        public override float HoldTime => 0.15f;

        public override string GetPrompt(IInteractor who)
        {
            if (locked) return who.Has(lockId) ? "Unlock" : "Locked";
            return IsOpen ? "Close drawer" : "Open drawer";
        }

        public override void Interact(IInteractor who)
        {
            if (locked)
            {
                if (!string.IsNullOrEmpty(lockId) && who.Has(lockId)) locked = false;
                InteractionFx.Play(ProceduralSounds.Sfx.Rattle, transform.position, 0.4f, 3f * who.Urgency, who.Actor);
                return;
            }
            target = IsOpen ? 0f : travel;
            enabled = true;
            InteractionFx.Play(ProceduralSounds.Sfx.DrawerSlide, transform.position, 0.5f, 2.5f * who.Urgency, who.Actor);
        }

        void Update()
        {
            pos = Mathf.MoveTowards(pos, target, speed * Time.deltaTime);
            Vector3 fwd = transform.parent != null ? transform.parent.InverseTransformDirection(transform.forward) : transform.forward;
            transform.localPosition = closedLocal + fwd * pos;
            if (Mathf.Approximately(pos, target)) enabled = false;
        }
    }
}
