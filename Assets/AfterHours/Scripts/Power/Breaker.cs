using UnityEngine;
using AfterHours.Audio;
using AfterHours.Interaction;
using AfterHours.Puzzles;

namespace AfterHours.Power
{
    /// <summary>
    /// One breaker on the switchboard: a chunky lever with a lamp above it (green = on, dark = off).
    /// Switching it back on takes a firm hold and makes a loud clunk. Tripping is louder still.
    /// </summary>
    public class Breaker : Interactable, IResettable
    {
        public string circuit = "north";
        public Transform handle;
        public Renderer lamp;
        public Vector3 offEuler = new Vector3(40f, 0f, 0f);
        public Vector3 onEuler = new Vector3(-40f, 0f, 0f);

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        float anim = 1f;
        Quaternion from, to;

        void OnEnable() => PowerGrid.Changed += OnChanged;
        void OnDisable() => PowerGrid.Changed -= OnChanged;

        void Start() => Refresh(true);

        public override float HoldTime => PowerGrid.BreakerOn(circuit) ? 0.4f : 0.9f;

        public override string GetPrompt(IInteractor who)
        {
            string name = PowerGrid.Label(circuit);
            return PowerGrid.BreakerOn(circuit) ? "Switch off " + name : "Reset breaker: " + name;
        }

        public override void Interact(IInteractor who)
        {
            bool on = !PowerGrid.BreakerOn(circuit);
            PowerGrid.SetBreaker(circuit, on);
            InteractionFx.Play(on ? ProceduralSounds.Sfx.BreakerOn : ProceduralSounds.Sfx.BreakerOff, transform.position, on ? 0.9f : 0.7f, on ? 14f : 8f, who.Actor, NoiseKind.Machinery);
            // old switchgear arcs when it's thrown back on under load
            if (on && Random.value < 0.6f) AfterHours.World.WorldFX.Sparks(transform.position + transform.forward * 0.05f, transform.forward + Vector3.down * 0.5f, 25, 0.6f);
        }

        void OnChanged(string id)
        {
            if (id == circuit || id == PowerGrid.Main) Refresh(false);
        }

        void Refresh(bool instant)
        {
            bool on = PowerGrid.BreakerOn(circuit);
            if (handle != null)
            {
                from = handle.localRotation;
                to = Quaternion.Euler(on ? onEuler : offEuler);
                anim = instant ? 1f : 0f;
                if (instant) handle.localRotation = to;
            }
            if (lamp != null)
            {
                if (block == null) block = new MaterialPropertyBlock();
                bool lit = on && PowerGrid.IsPowered(circuit);
                block.SetColor(EmissionId, lit ? new Color(0.2f, 1.6f, 0.3f) : Color.black);
                lamp.SetPropertyBlock(block);
            }
        }

        void Update()
        {
            if (anim >= 1f || handle == null) return;
            anim = Mathf.Min(1f, anim + Time.deltaTime / 0.12f);
            handle.localRotation = Quaternion.Slerp(from, to, anim);
        }

        public void ResetState()
        {
            PowerGrid.RestoreAll();
            Refresh(true);
        }
    }
}
