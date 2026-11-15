using UnityEngine;
using MallHorror.Controls;
using MallHorror.Core;
using MallHorror.World;

namespace MallHorror.Player
{
    /// <summary>
    /// Look at a heavy object and hold the interact key (E) to grab it. Walk forwards to push,
    /// backwards to pull, turn slowly to rotate it. You move at the object's speed, cannot sprint,
    /// and lose your grip if it jams (e.g. against a wall) or if you go prone.
    /// </summary>
    public class PlayerGrab : MonoBehaviour
    {
        public PlayerMotor motor;
        public PlayerLook look;
        public float reach = 1.7f;
        public float releaseError = 0.75f;

        HeavyMovable lookTarget;
        HeavyMovable held;
        Vector3 localOffset;
        float relativeYaw;
        GUIStyle promptStyle;

        public HeavyMovable Held => held;
        public bool IsHolding => held != null;

        /// <summary>Runs before the motor moves this frame.</summary>
        public void PreMove(in PlayerInputFrame input)
        {
            lookTarget = FindTarget();

            if (held == null)
            {
                if (input.interactHeld && lookTarget != null && motor.IsGrounded && motor.CurrentStance != Stance.Crawling)
                {
                    Begin(lookTarget);
                }
            }
            else if (!input.interactHeld || motor.CurrentStance == Stance.Crawling || held.GripError(this) > releaseError)
            {
                Release();
            }

            if (held != null)
            {
                motor.ExternalSpeedLimit = held.AllowedSpeed * 1.1f;
                motor.ExternalYawRateLimit = held.AllowedTurnRate * 1.2f;
                motor.ExternalSprintBlocked = true;
            }
            else
            {
                motor.ExternalSpeedLimit = float.PositiveInfinity;
                motor.ExternalYawRateLimit = float.PositiveInfinity;
                motor.ExternalSprintBlocked = false;
            }
        }

        /// <summary>Runs after the motor moved: tells the object where it should now be.</summary>
        public void PostMove()
        {
            if (held == null) return;
            Quaternion bodyRot = Quaternion.Euler(0f, motor.BodyYaw, 0f);
            Vector3 target = transform.position + bodyRot * localOffset;
            target.y = held.Body.position.y;
            held.SetGrip(this, target, motor.BodyYaw + relativeYaw);
        }

        void Begin(HeavyMovable obj)
        {
            held = obj;
            Quaternion inv = Quaternion.Inverse(Quaternion.Euler(0f, motor.BodyYaw, 0f));
            Vector3 offset = obj.Body.position - transform.position;
            offset.y = 0f;
            localOffset = inv * offset;
            relativeYaw = Mathf.DeltaAngle(motor.BodyYaw, obj.Body.rotation.eulerAngles.y);
            obj.SetGrip(this, obj.Body.position, obj.Body.rotation.eulerAngles.y);
        }

        public void Release()
        {
            if (held != null) held.Release(this);
            held = null;
        }

        void OnDisable()
        {
            Release();
        }

        HeavyMovable FindTarget()
        {
            if (look == null || look.playerCamera == null) return null;
            Transform cam = look.playerCamera.transform;
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, reach, GameLayers.CharacterBlockingMask, QueryTriggerInteraction.Ignore))
            {
                Rigidbody rb = hit.collider.attachedRigidbody;
                if (rb != null) return rb.GetComponent<HeavyMovable>();
            }
            return null;
        }

        void OnGUI()
        {
            if (GamePause.IsPaused) return;
            string text = held != null ? "Pushing / pulling — let go of E to release"
                        : lookTarget != null ? "Hold E — push / pull" : null;
            if (text == null) return;
            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                promptStyle.normal.textColor = new Color(0.9f, 0.9f, 0.85f, 0.85f);
            }
            GUI.Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 30f), text, promptStyle);
        }
    }
}
