using UnityEngine;
using UnityEngine.Events;
using AfterHours.Audio;

namespace AfterHours.Interaction
{
    /// <summary>
    /// A switch, lever or breaker. The moving part rotates between off/on angles; state changes fire an event
    /// (power circuits, machinery and puzzles connect to it in Parts 7 and 12).
    /// </summary>
    public class PhysicalSwitch : Interactable
    {
        public Transform handle;
        public Vector3 offEuler = new Vector3(-35f, 0f, 0f);
        public Vector3 onEuler = new Vector3(35f, 0f, 0f);
        public bool isOn;
        public bool locked;
        public string label = "switch";
        public float noiseRadius = 4f;
        public UnityEvent<bool> onChanged = new UnityEvent<bool>();

        float anim = 1f;
        Quaternion from, to;

        void Start()
        {
            if (handle != null) handle.localRotation = Quaternion.Euler(isOn ? onEuler : offEuler);
        }

        public override float HoldTime => 0.25f;

        public override string GetPrompt(IInteractor who)
        {
            if (locked) return "Locked";
            return (isOn ? "Turn off " : "Turn on ") + label;
        }

        public override void Interact(IInteractor who) => Set(!isOn, who.Actor);

        public void Set(bool on, GameObject actor)
        {
            if (locked || on == isOn) return;
            isOn = on;
            if (handle != null)
            {
                from = handle.localRotation;
                to = Quaternion.Euler(isOn ? onEuler : offEuler);
                anim = 0f;
            }
            InteractionFx.Play(ProceduralSounds.Sfx.Click, transform.position, 0.6f, noiseRadius, actor);
            onChanged.Invoke(isOn);
        }

        void Update()
        {
            if (anim >= 1f || handle == null) return;
            anim = Mathf.Min(1f, anim + Time.deltaTime / 0.15f);
            handle.localRotation = Quaternion.Slerp(from, to, anim);
        }
    }
}
