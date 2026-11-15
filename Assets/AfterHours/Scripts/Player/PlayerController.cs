using UnityEngine;
using AfterHours.Controls;

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
        IPlayerInputSource input;

        public PlayerMotor Motor => motor;
        public PlayerLook Look => look;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            look = GetComponent<PlayerLook>();
            grab = GetComponent<PlayerGrab>();
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

        void Update()
        {
            float dt = Time.deltaTime;
            PlayerInputFrame frame = input != null ? input.ReadFrame(dt) : default;
            if (grab != null) grab.PreMove(frame);
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
