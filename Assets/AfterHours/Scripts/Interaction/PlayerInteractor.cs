using UnityEngine;
using AfterHours.Controls;
using AfterHours.Core;
using AfterHours.Player;

namespace AfterHours.Interaction
{
    /// <summary>
    /// The player's hands: finds what you are looking at (within reach and line of sight - never through walls),
    /// shows a small prompt, and performs the action when Interact is held long enough.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour, IInteractor
    {
        public PlayerMotor motor;
        public PlayerLook look;
        public PlayerGrab grab;
        public float reach = 2.0f;

        Interactable target;
        Interactable holding;
        float holdTimer;
        bool actionDone;
        string prompt;
        GUIStyle style;

        /// <summary>Optional item check provided by the inventory (Part 6).</summary>
        public System.Func<string, bool> itemCheck;

        public Vector3 Position => transform.position;
        public Vector3 Forward => look != null && look.playerCamera != null ? look.playerCamera.transform.forward : transform.forward;
        public GameObject Actor => gameObject;
        public float Urgency
        {
            get
            {
                if (motor == null) return 1f;
                if (motor.CurrentStance != Stance.Standing) return 0.4f;
                return motor.Locomotion.sprinting ? 1.8f : 1f;
            }
        }
        public bool Has(string itemId) => itemCheck != null && itemCheck(itemId);
        public Interactable Target => target;

        public void Tick(in PlayerInputFrame input, float dt)
        {
            if (GamePause.IsPaused) return;
            if (grab != null && grab.IsHolding)
            {
                target = null;
                prompt = null;
                return;
            }

            if (holding == null) target = FindTarget();
            prompt = target != null ? target.GetPrompt(this) : null;

            if (input.interactPressed && target != null && prompt != null)
            {
                holding = target;
                holdTimer = 0f;
                actionDone = false;
            }

            if (holding != null)
            {
                bool stillAimed = FindTarget() == holding;
                if (!input.interactHeld || !stillAimed)
                {
                    holding.HoldReleased(this);
                    holding = null;
                    return;
                }
                holdTimer += dt;
                if (!actionDone && holdTimer >= holding.HoldTime)
                {
                    actionDone = true;
                    holding.Interact(this);
                }
                if (actionDone && holding != null && holding.Continuous) holding.HoldTick(this, dt);
            }
        }

        /// <summary>While hidden, this is always the target (the hiding place you are in), whatever you look at.</summary>
        public Interactable ForcedTarget { get; set; }

        Interactable FindTarget()
        {
            if (ForcedTarget != null) return ForcedTarget;
            Camera cam = look != null ? look.playerCamera : null;
            if (cam == null) return null;
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            int mask = GameLayers.CharacterBlockingMask & ~(GameLayers.PlayerLayer >= 0 ? 1 << GameLayers.PlayerLayer : 0);
            if (!Physics.Raycast(ray, out RaycastHit hit, reach, mask, QueryTriggerInteraction.Collide)) return null;
            // A locker's door and the locker (hiding place) are both interactable: use the first that offers something.
            Interactable fallback = null;
            foreach (Interactable i in hit.collider.GetComponentsInParent<Interactable>())
            {
                if (fallback == null) fallback = i;
                if (i.GetPrompt(this) != null) return i;
            }
            return fallback;
        }

        void OnGUI()
        {
            if (GamePause.IsPaused || string.IsNullOrEmpty(prompt)) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = new Color(0.92f, 0.92f, 0.88f, 0.9f);
            }
            float w = Screen.width, h = Screen.height;
            GUI.Label(new Rect(0f, h * 0.56f, w, 28f), "E  " + prompt, style);
            if (holding != null && !actionDone && holding.HoldTime > 0.25f)
            {
                float p = Mathf.Clamp01(holdTimer / holding.HoldTime);
                GUI.DrawTexture(new Rect(w * 0.5f - 40f, h * 0.56f + 26f, 80f * p, 3f), Texture2D.whiteTexture);
            }
        }
    }
}
