using UnityEngine;
using AfterHours.Audio;

namespace AfterHours.Interaction
{
    /// <summary>
    /// Anything the player (and later Elliot) can use: doors, shutters, drawers, vent covers, switches, items.
    /// The player looks at it, a small prompt appears, and holding Interact for HoldTime performs the action.
    /// Continuous interactables (shutter cranks) keep receiving HoldTick while the button stays held.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        /// <summary>Seconds the button must be held before the action happens.</summary>
        public virtual float HoldTime => 0.2f;

        /// <summary>True for things worked continuously while holding (cranking a shutter up).</summary>
        public virtual bool Continuous => false;

        /// <summary>Text for the prompt, or null when there is nothing to do right now.</summary>
        public abstract string GetPrompt(IInteractor who);

        public abstract void Interact(IInteractor who);

        public virtual void HoldTick(IInteractor who, float deltaTime) { }

        public virtual void HoldReleased(IInteractor who) { }
    }

    /// <summary>Whoever is interacting: a player, or (later) Elliot or a remote player.</summary>
    public interface IInteractor
    {
        Vector3 Position { get; }
        Vector3 Forward { get; }
        GameObject Actor { get; }
        /// <summary>0.4 = careful (crouching), 1 = normal, 1.8 = in a hurry (sprinting).</summary>
        float Urgency { get; }
        /// <summary>Does the interactor carry an item providing this key / tool id? (Inventory: Part 6.)</summary>
        bool Has(string itemId);
    }

    /// <summary>Plays an interaction sound in the world and emits a matching noise event that creatures can hear.</summary>
    public static class InteractionFx
    {
        public static void Play(ProceduralSounds.Sfx sfx, Vector3 position, float volume, float noiseRadius, GameObject source, NoiseKind kind = NoiseKind.Interaction)
        {
            AudioPool.PlayAt(ProceduralSounds.Effect(sfx), position, volume, Random.Range(0.94f, 1.06f), Mathf.Max(12f, noiseRadius * 1.5f));
            if (noiseRadius > 0.1f) NoiseBus.Emit(new NoiseEvent(position, noiseRadius, kind, source));
        }
    }
}
