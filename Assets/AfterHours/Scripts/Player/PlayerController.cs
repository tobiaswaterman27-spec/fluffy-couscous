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

        /// <summary>True while hidden in a hiding place (Part 11).</summary>
        public bool IsHidden { get; set; }
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
            float dt = Time.deltaTime;
            PlayerInputFrame frame = input != null ? input.ReadFrame(dt) : default;
            if (interactor != null) interactor.Tick(frame, dt);
            if (inventory != null) inventory.Tick(frame);
            if (grab != null && (interactor == null || interactor.Target == null)) grab.PreMove(frame);
            look.ApplyLook(frame.lookDelta, motor, dt);
            motor.Tick(frame, dt);
            if (grab != null) grab.PostMove();
        }

        void LateUpdate()
        {
            look.UpdateCamera(motor, Time.deltaTime);
        }
    }
}
