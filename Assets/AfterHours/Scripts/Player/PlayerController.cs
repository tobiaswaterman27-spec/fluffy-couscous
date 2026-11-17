using UnityEngine;
using AfterHours.Controls;
using AfterHours.Interaction;
using AfterHours.Items;

namespace AfterHours.Player
{
    /// <summary>
    /// Wires input -> look -> movement -> camera each frame.
    /// For LAN co-op (Part 14), remote players use the same components with a network input source,
    /// and isLocallyControlled = false turns off their camera and listener.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerLook))]
    public class PlayerController : MonoBehaviour
    {
        public bool isLocallyControlled = true;

        PlayerMotor motor;
        PlayerLook look;
        PlayerGrab grab;
        PlayerInteractor interactor;
        PlayerInventory inventory;
        IPlayerInputSource input;

        static readonly System.Collections.Generic.List<PlayerController> all = new System.Collections.Generic.List<PlayerController>();
        /// <summary>Every player in the scene (used by Elliot's senses).</summary>
        public static System.Collections.Generic.IReadOnlyList<PlayerController> All => all;

        /// <summary>True while hidden in a hiding place.</summary>
        public bool IsHidden { get; set; }
        public AfterHours.Hiding.HidingSpot CurrentHidingSpot { get; set; }

        /// <summary>True while something else moves the body (hiding, being caught). lookAllowed: limited looking.</summary>
        public bool ControlLocked { get; private set; }
        bool lockLook;

        public void SetControlLock(bool locked, bool noLook)
        {
            ControlLocked = locked;
            lockLook = noLook;
            if (motor != null) motor.ClearLocomotion();
            if (grab != null && locked) grab.Release();
            if (interactor != null) interactor.ForcedTarget = locked && !noLook ? CurrentHidingSpot : null;
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = !locked;
        }

        /// <summary>Moves the body while control is locked (hiding / being carried). Never used for normal movement.</summary>
        public void MoveBodyTo(Vector3 position, float yaw)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>Full orientation (lifted, upside down, lying on the floor) - only while control is locked.</summary>
        public void MoveBodyTo(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }
        public Vector3 EyePosition => look != null && look.playerCamera != null ? look.playerCamera.transform.position : transform.position + Vector3.up * 1.6f;

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable() => all.Remove(this);

        public PlayerMotor Motor => motor;
        public PlayerLook Look => look;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            look = GetComponent<PlayerLook>();
            grab = GetComponent<PlayerGrab>();
            interactor = GetComponent<PlayerInteractor>();
            inventory = GetComponent<PlayerInventory>();
            input = GetComponent<IPlayerInputSource>();
        }

        void Start()
        {
            // the real body (older scenes were built with only the debug capsule)
            if (GetComponent<PlayerBody>() == null) gameObject.AddComponent<PlayerBody>().controller = this;
            if (GetComponent<PlayerBodyAnimator>() == null) gameObject.AddComponent<PlayerBodyAnimator>().controller = this;
            // shadows from the nearest mall lights follow your camera
            if (isLocallyControlled && look != null && look.playerCamera != null && look.playerCamera.GetComponent<AfterHours.Rendering.ShadowBudget>() == null)
                look.playerCamera.gameObject.AddComponent<AfterHours.Rendering.ShadowBudget>();
            if (isLocallyControlled && GetComponent<AfterHours.Audio.PlayerAudio>() == null)
            {
                // Your breathing and heart, the music, and the building's random noises follow you.
                gameObject.AddComponent<AfterHours.Audio.PlayerAudio>();
                gameObject.AddComponent<AfterHours.Audio.TensionMusic>();
                gameObject.AddComponent<AfterHours.Audio.AmbientEvents>();
            }
            look.Initialize(transform.eulerAngles.y);
            motor.Landed += look.OnLanded;

            if (!isLocallyControlled && look.playerCamera != null)
            {
                look.playerCamera.enabled = false;
                var listener = look.playerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
        }

        void OnDestroy()
        {
            if (motor != null && look != null) motor.Landed -= look.OnLanded;
        }

        /// <summary>
        /// Places the player instantly. Only for spawning, respawning, loading saves and developer testing.
        /// </summary>
        public void Teleport(Vector3 position, float yaw)
        {
            if (grab != null) grab.Release();
            if (inventory != null) inventory.DropAtFeet();
            motor.Warp(position, yaw);
            look.Initialize(yaw);
            if (input is LocalPlayerInput local) local.ResetState();
        }

        void Update()
        {
            if (!isLocallyControlled) return; // a remote player: moved by the network (AfterHours.Net.NetProxy)
            float dt = Time.deltaTime;
            PlayerInputFrame frame = input != null ? input.ReadFrame(dt) : default;
            if (ControlLocked)
            {
                // Hidden: look around a little and use the hiding place; no walking.
                if (!lockLook && CurrentHidingSpot != null)
                {
                    look.ApplyLimitedLook(frame.lookDelta, CurrentHidingSpot.facingYaw, CurrentHidingSpot.lookRange);
                    if (interactor != null) interactor.Tick(frame, dt);
                }
                return;
            }
            if (interactor != null) interactor.Tick(frame, dt);
            if (inventory != null) inventory.Tick(frame);
            if (grab != null && (interactor == null || interactor.Target == null)) grab.PreMove(frame);
            look.ApplyLook(frame.lookDelta, motor, dt);
            motor.Tick(frame, dt);
            if (grab != null) grab.PostMove();
        }

        void LateUpdate()
        {
            if (!isLocallyControlled) return;
            look.UpdateCamera(motor, Time.deltaTime);
        }
    }
}
