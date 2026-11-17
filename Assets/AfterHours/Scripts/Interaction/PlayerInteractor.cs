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

        /// <summary>
        /// LAN co-op client: the host owns the world, so finished interactions are sent there instead of being done here
        /// (returns true if it took the interaction). Hiding places still run here as well, because they move you.
        /// </summary>
        public static System.Func<Interactable, IInteractor, bool> NetInteract;
        public static System.Func<Interactable, IInteractor, float, bool> NetHoldTick;

        void DoInteract(Interactable i)
        {
            var hands = GetComponent<AfterHours.Player.PlayerActions>();
            int before = hands != null ? hands.Stamp : 0;
            bool sent = NetInteract != null && NetInteract(i, this) && !(i is AfterHours.Hiding.HidingSpot);
            if (!sent) i.Interact(this);
            // whatever it was, your hand goes to it (doors and vents animate the hands their own way)
            if (hands != null && hands.Stamp == before && !hands.Climbing && !(i is AfterHours.Hiding.HidingSpot))
            {
                Camera cam = look != null ? look.playerCamera : null;
                Vector3 f = cam != null ? cam.transform.forward : transform.forward, r = cam != null ? cam.transform.right : transform.right;
                AfterHours.Player.Gesture g = i is AfterHours.Items.ItemPickup ? AfterHours.Player.Gesture.PickUp
                    : i is AfterHours.Puzzles.ToolBreakable ? AfterHours.Player.Gesture.Strike : AfterHours.Player.Gesture.Reach;
                hands.Play(g, lastHitPoint, -f, r, g == AfterHours.Player.Gesture.Strike ? 0.6f : 0.5f);
            }
        }

        Vector3 lastHitPoint;

        void DoHoldTick(Interactable i, float dt)
        {
            if (NetHoldTick != null && NetHoldTick(i, this, dt)) return;
            i.HoldTick(this, dt);
        }

        /// <summary>Shows the key currently bound to Interact (set by the input service; "E" if not).</summary>
        public static System.Func<string> KeyName;

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
                    DoInteract(holding);
                }
                if (actionDone && holding != null && holding.Continuous) DoHoldTick(holding, dt);
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
            lastHitPoint = hit.point;
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
            if (GamePause.IsPaused || string.IsNullOrEmpty(prompt) || !isActiveAndEnabled) return;
            PlayerController pc = GetComponent<PlayerController>();
            if (pc != null && !pc.isLocallyControlled) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = new Color(0.9f, 0.89f, 0.85f, 0.85f);
            }
            float w = AfterHours.UI.UiSkin.Scale(), h = 1080f;
            // A faint dot where you're looking, only when there's something to use.
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            GUI.DrawTexture(new Rect(w * 0.5f - 2f, h * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            string key = KeyName != null ? KeyName() : "E";
            GUI.Label(new Rect(0f, h * 0.555f, w, 30f), key + "   " + prompt, style);
            if (holding != null && !actionDone && holding.HoldTime > 0.25f)
            {
                float p = Mathf.Clamp01(holdTimer / holding.HoldTime);
                GUI.color = new Color(1f, 1f, 1f, 0.6f);
                GUI.DrawTexture(new Rect(w * 0.5f - 50f, h * 0.555f + 32f, 100f * p, 2f), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            AfterHours.UI.UiSkin.Unscale();
        }
    }
}
