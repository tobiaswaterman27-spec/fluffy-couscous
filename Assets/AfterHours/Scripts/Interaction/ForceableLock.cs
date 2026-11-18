using UnityEngine;
using AfterHours.Audio;
using AfterHours.Items;

namespace AfterHours.Interaction
{
    /// <summary>
    /// A wooden office door you can chop through with the fire axe instead of finding its key or card: hold to keep
    /// swinging. Every blow is very loud, the lock gives after several, and the axe handle only survives a few doors.
    /// Steel doors (plant rooms, security, electrical) don't get one of these.
    /// </summary>
    public class ForceableLock : Interactable
    {
        public Door door;
        public string toolId = "axe";
        public int blows = 6;
        public float blowEvery = 0.8f;
        public float noiseRadius = 38f;

        int hits;
        float timer;

        public override float HoldTime => 0.3f;
        public override bool Continuous => door != null && door.locked;

        public override string GetPrompt(IInteractor who)
        {
            if (door == null || !door.locked || !who.Has(toolId)) return null;
            return "Break the door open (hold)";
        }

        public override void Interact(IInteractor who)
        {
            timer = 0f; // first blow straight away
        }

        public override void HoldTick(IInteractor who, float dt)
        {
            if (door == null || !door.locked || !who.Has(toolId)) return;
            timer -= dt;
            if (timer > 0f) return;
            timer = blowEvery;
            hits++;
            Vector3 at = door.HandleWorld;
            Player.PlayerActions a = Player.PlayerActions.Of(who.Actor);
            Vector3 n = who.Position - at; n.y = 0f;
            Vector3 normal = n.sqrMagnitude > 1e-4f ? n.normalized : -door.transform.forward;
            if (a != null) a.Play(Player.Gesture.Strike, at + normal * 0.05f, normal, Vector3.up, blowEvery * 0.9f);
            InteractionFx.Play(hits >= blows ? ProceduralSounds.Sfx.DoorBreak : ProceduralSounds.Sfx.Bang, at, 1f, noiseRadius, who.Actor, NoiseKind.Door);
            if (hits < blows) return;

            hits = 0;
            door.locked = false;
            door.Open(who.Position, 2f, who.Actor);
            PlayerInventory inv = who.Actor != null ? who.Actor.GetComponent<PlayerInventory>() : null;
            if (inv != null && inv.Held != null) inv.Held.Wear(who.Actor);
        }

        public override void HoldReleased(IInteractor who)
        {
            // Stopping halfway: the damage stays (the next go needs fewer blows), but never fewer than two.
            hits = Mathf.Min(hits, blows - 2);
        }
    }
}
