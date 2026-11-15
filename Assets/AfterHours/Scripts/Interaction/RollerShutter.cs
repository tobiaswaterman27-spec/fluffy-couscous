using UnityEngine;
using AfterHours.Audio;
using AfterHours.Core;

namespace AfterHours.Interaction
{
    /// <summary>
    /// A roller shutter. Manual ones are cranked: hold Interact and it rises (or lowers) slowly, rattling loudly the
    /// whole time - a terrible thing to do with something listening. Locked ones need a key; jammed ones need a tool
    /// or another route (Part 7). Lowering stops if anything is underneath (never crushes).
    /// The shutter panel moves up into the wall above the opening (it is never scaled or teleported).
    /// </summary>
    public class RollerShutter : Interactable
    {
        public Transform panel;
        public float travel = 3.3f;       // how far it can rise
        public float crankSpeed = 0.22f;  // m/s while cranking
        public bool locked;
        public string lockId = "";
        public bool jammed;
        [Tooltip("How far up it already is (0..1), e.g. 0.18 for a shutter stuck part-way.")]
        [Range(0f, 1f)] public float startOpen;

        public float OpenAmount => travel > 0f ? raised / travel : 0f;

        float raised;
        Vector3 closedLocal;
        int direction = 1;
        float noiseTimer;
        readonly Collider[] overlap = new Collider[8];

        public override float HoldTime => 0.25f;
        public override bool Continuous => !locked && !jammed;

        void Awake()
        {
            if (panel == null) panel = transform;
            closedLocal = panel.localPosition - Vector3.up * (startOpen * travel);
            raised = startOpen * travel;
        }

        public override string GetPrompt(IInteractor who)
        {
            if (locked) return who.Has(lockId) ? "Unlock shutter" : "Locked";
            if (jammed) return "Jammed";
            return direction > 0 && OpenAmount < 0.99f ? "Crank up (hold)" : "Crank down (hold)";
        }

        public override void Interact(IInteractor who)
        {
            if (locked)
            {
                if (!string.IsNullOrEmpty(lockId) && who.Has(lockId))
                {
                    locked = false;
                    InteractionFx.Play(ProceduralSounds.Sfx.Unlock, panel.position, 0.7f, 3f, who.Actor);
                }
                else InteractionFx.Play(ProceduralSounds.Sfx.Rattle, panel.position, 1f, 12f, who.Actor, NoiseKind.Door);
                return;
            }
            if (jammed)
            {
                InteractionFx.Play(ProceduralSounds.Sfx.ShutterStop, panel.position, 1f, 14f, who.Actor, NoiseKind.Door);
                return;
            }
            if (OpenAmount >= 0.99f) direction = -1;
            else if (OpenAmount <= 0.01f) direction = 1;
        }

        public override void HoldTick(IInteractor who, float dt)
        {
            float next = Mathf.Clamp(raised + direction * crankSpeed * dt, 0f, travel);
            if (direction < 0 && Obstructed(next)) return;
            if (Mathf.Approximately(next, raised)) return;
            raised = next;
            panel.localPosition = closedLocal + Vector3.up * raised;

            noiseTimer -= dt;
            if (noiseTimer <= 0f)
            {
                noiseTimer = 0.55f;
                InteractionFx.Play(ProceduralSounds.Sfx.ShutterRoll, panel.position, 0.9f, 22f, who.Actor, NoiseKind.Door);
            }
            if (raised <= 0f || raised >= travel)
            {
                InteractionFx.Play(ProceduralSounds.Sfx.ShutterStop, panel.position, 0.9f, 16f, who.Actor, NoiseKind.Door);
                direction = raised >= travel ? -1 : 1;
            }
        }

        public override void HoldReleased(IInteractor who)
        {
            noiseTimer = 0f;
        }

        bool Obstructed(float nextRaised)
        {
            Collider c = panel.GetComponent<Collider>();
            if (c == null) return false;
            Bounds b = c.bounds;
            Vector3 centre = b.center + Vector3.up * (nextRaised - raised);
            int mask = 0;
            if (GameLayers.PlayerLayer >= 0) mask |= 1 << GameLayers.PlayerLayer;
            if (GameLayers.CreatureLayer >= 0) mask |= 1 << GameLayers.CreatureLayer;
            if (GameLayers.PropLayer >= 0) mask |= 1 << GameLayers.PropLayer;
            return Physics.OverlapBoxNonAlloc(centre, b.extents * 0.97f, overlap, Quaternion.identity, mask, QueryTriggerInteraction.Ignore) > 0;
        }
    }
}
