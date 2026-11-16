using UnityEngine;
using AfterHours.Audio;
using AfterHours.Controls;
using AfterHours.Core;
using AfterHours.Interaction;
using AfterHours.Player;

namespace AfterHours.Items
{
    /// <summary>
    /// The one-item inventory. The item is held in view (right hand). G drops it gently in front of you;
    /// Throw (right mouse) throws it - it lands with a real noise that creatures can hear (a distraction).
    /// Keys / cards / tools in hand satisfy locks and tool checks. Works while crouching, crawling and hiding.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        public Transform handAnchor;
        public PlayerLook look;
        public PlayerMotor motor;
        public PlayerInteractor interactor;
        public float throwSpeed = 8f;

        public ItemPickup Held { get; private set; }

        void Awake()
        {
            if (interactor != null)
            {
                interactor.itemCheck = id => Held != null && Held.Definition != null && Held.Definition.Provides(id);
            }
        }

        public void Tick(in PlayerInputFrame input)
        {
            if (Held == null || GamePause.IsPaused) return;
            // Setting a device you are holding (alarm clock / phone): Interact with nothing in front of you.
            if (input.interactPressed && (interactor == null || interactor.Target == null))
            {
                TimedDistraction device = Held.GetComponent<TimedDistraction>();
                if (device != null && !device.Armed) device.Arm();
            }
            if (input.dropPressed) Drop(0.6f, false);
            else if (input.throwPressed) Drop(throwSpeed, true);
        }

        /// <summary>LAN co-op client: dropping / throwing is decided by the host (item, release point, velocity).</summary>
        public static System.Action<ItemPickup, Vector3, Vector3> NetDropRequest;

        /// <summary>LAN co-op: the host says this player now holds / no longer holds an item.</summary>
        public void NetSetHeld(ItemPickup item)
        {
            if (Held == item) return;
            if (item == null) { Held = null; return; }
            Held = item;
            item.AttachTo(gameObject, handAnchor);
            ShowCaption(item.DisplayName);
        }

        /// <summary>Host: a remote player let go of their item.</summary>
        public void NetRelease(Vector3 position, Vector3 velocity)
        {
            ItemPickup item = Held;
            if (item == null) return;
            Held = null;
            item.Release(position, velocity);
        }

        float captionUntil;
        string caption;

        void OnGUI()
        {
            if (Time.time > captionUntil || GamePause.IsPaused) return;
            PlayerController pc = GetComponent<PlayerController>();
            if (pc != null && !pc.isLocallyControlled) return;
            float w = AfterHours.UI.UiSkin.Scale();
            float a = Mathf.Clamp01((captionUntil - Time.time) / 0.8f);
            var st = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleRight };
            st.normal.textColor = new Color(0.86f, 0.85f, 0.8f, 0.75f * a);
            GUI.Label(new Rect(w - 640f, 1080f - 90f, 600f, 34f), caption, st);
            AfterHours.UI.UiSkin.Unscale();
        }

        void ShowCaption(string text)
        {
            caption = text;
            captionUntil = Time.time + 3f;
        }

        public void PickUp(ItemPickup item)
        {
            if (item == null || item.IsHeld) return;
            if (Held != null) Drop(0.3f, false);
            Held = item;
            item.AttachTo(gameObject, handAnchor);
            ShowCaption(item.DisplayName);
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.Pickup), item.transform.position, 0.35f, Random.Range(0.9f, 1.1f), 6f);
            NoiseBus.Emit(new NoiseEvent(item.transform.position, 1.2f, NoiseKind.Interaction, gameObject));
        }

        /// <summary>Lets go of the held item: speed ~0.5 = set down, 8 = throw.</summary>
        public void Drop(float speed, bool isThrow)
        {
            ItemPickup item = Held;
            if (item == null) return;
            Held = null;

            Camera cam = look != null ? look.playerCamera : null;
            Vector3 eye = cam != null ? cam.transform.position : transform.position + Vector3.up * 1.5f;
            Vector3 fwd = cam != null ? cam.transform.forward : transform.forward;
            Vector3 hand = handAnchor != null ? handAnchor.position : eye + fwd * 0.4f;

            // Never let go inside a wall: if something is between the eyes and the hand, let go just in front of it.
            Vector3 toHand = hand - eye;
            Vector3 release = hand;
            if (Physics.SphereCast(eye, 0.06f, toHand.normalized, out RaycastHit hit, toHand.magnitude, GameLayers.CharacterBlockingMask & ~(1 << Mathf.Max(0, GameLayers.PlayerLayer)), QueryTriggerInteraction.Ignore))
            {
                release = eye + toHand.normalized * Mathf.Max(0.05f, hit.distance - 0.08f);
            }

            Vector3 carried = motor != null ? motor.Locomotion.worldVelocity : Vector3.zero;
            Vector3 v = isThrow ? fwd * speed + Vector3.up * 1.5f + carried : fwd * speed + carried * 0.5f;
            NetDropRequest?.Invoke(item, release, v);
            item.Release(release, v);
            if (isThrow) NoiseBus.Emit(new NoiseEvent(eye, 1.5f, NoiseKind.Interaction, gameObject));
        }

        /// <summary>Hands the held item over (to a socket / mechanism) without dropping it.</summary>
        public ItemPickup TakeHeld()
        {
            ItemPickup item = Held;
            Held = null;
            return item;
        }

        /// <summary>Drops whatever is held where you stand (used when caught / reset).</summary>
        public void DropAtFeet()
        {
            if (Held == null) return;
            ItemPickup item = Held;
            Held = null;
            item.Release(transform.position + Vector3.up * 0.3f, Vector3.zero);
        }
    }
}
