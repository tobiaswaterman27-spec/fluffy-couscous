using UnityEngine;
using MallHorror.Core;
using MallHorror.Settings;

namespace MallHorror.Controls
{
    /// <summary>
    /// Reads the local keyboard/mouse/gamepad and turns it into a <see cref="PlayerInputFrame"/>.
    ///
    /// One button for crouch AND crawl (Ctrl or C by default):
    ///   standing:  press = crouch immediately; keep holding = go prone (crawl)
    ///   crouching: tap = stand up;  hold = go prone
    ///   crawling:  tap = up to crouch; hold = stand straight up
    /// Sprint always tries to stand you up. A dedicated Crawl key can also be assigned in Controls.
    /// If there is no room to stand, the body simply stays low until there is.
    /// </summary>
    public class LocalPlayerInput : MonoBehaviour, IPlayerInputSource
    {
        Stance target = Stance.Standing;
        bool pressActive;
        bool holdFired;
        float pressTime;
        Stance stanceAtPress;

        public PlayerInputFrame ReadFrame(float deltaTime)
        {
            var frame = new PlayerInputFrame();
            var input = InputService.Instance;
            GameSettings s = SettingsService.CurrentOrDefault;

            if (input == null || GamePause.IsPaused)
            {
                pressActive = false;
                frame.desiredStance = target;
                return frame;
            }

            frame.move = Vector2.ClampMagnitude(input.Move.ReadValue<Vector2>(), 1f);

            Vector2 mouse = input.Look.ReadValue<Vector2>() * s.mouseSensitivity;
            Vector2 stick = input.LookStick.ReadValue<Vector2>();
            stick = stick * stick.magnitude * s.gamepadSensitivity * deltaTime; // response curve for precision
            Vector2 look = mouse + stick;
            if (s.invertY) look.y = -look.y;
            frame.lookDelta = look;

            UpdateStanceButton(input, s);

            bool sprintHeld = input.Sprint.IsPressed();
            if (input.Sprint.WasPressedThisFrame())
            {
                target = Stance.Standing;
                pressActive = false;
            }

            frame.desiredStance = target;
            frame.sprint = sprintHeld;
            frame.interactHeld = input.Interact.IsPressed();
            frame.interactPressed = input.Interact.WasPressedThisFrame();
            frame.dropPressed = input.Drop.WasPressedThisFrame();
            return frame;
        }

        void UpdateStanceButton(InputService input, GameSettings s)
        {
            float now = Time.unscaledTime;

            if (input.Crouch.WasPressedThisFrame())
            {
                pressActive = true;
                holdFired = false;
                pressTime = now;
                stanceAtPress = target;
                if (target == Stance.Standing) target = Stance.Crouching; // respond instantly
            }

            if (pressActive && !holdFired && input.Crouch.IsPressed() && now - pressTime >= s.stanceHoldTime)
            {
                holdFired = true;
                target = stanceAtPress == Stance.Crawling ? Stance.Standing : Stance.Crawling;
            }

            if (pressActive && input.Crouch.WasReleasedThisFrame())
            {
                pressActive = false;
                if (!holdFired)
                {
                    if (stanceAtPress == Stance.Crouching) target = Stance.Standing;
                    else if (stanceAtPress == Stance.Crawling) target = Stance.Crouching;
                }
            }

            if (input.Crawl.WasPressedThisFrame())
            {
                target = target == Stance.Crawling ? Stance.Crouching : Stance.Crawling;
                pressActive = false;
            }
        }

        /// <summary>Back to standing (used on respawn / reset).</summary>
        public void ResetStance()
        {
            target = Stance.Standing;
            pressActive = false;
        }
    }
}
