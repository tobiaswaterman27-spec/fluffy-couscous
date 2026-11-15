using UnityEngine;
using AfterHours.Core;
using AfterHours.Settings;

namespace AfterHours.Controls
{
    /// <summary>
    /// Reads the local keyboard/mouse/gamepad and turns it into a <see cref="PlayerInputFrame"/>.
    ///
    /// Stance controls:
    ///   Ctrl  = crouch (hold by default, or toggle in Settings)
    ///   C     = crawl on/off
    ///   Pressing Ctrl while crawling rises to a crouch. Starting a sprint stands you up.
    ///   If there is no room to rise, the body simply stays low until there is.
    /// </summary>
    public class LocalPlayerInput : MonoBehaviour, IPlayerInputSource
    {
        bool crouchToggled;
        bool crawling;

        public PlayerInputFrame ReadFrame(float deltaTime)
        {
            var frame = new PlayerInputFrame();
            var input = InputService.Instance;
            GameSettings s = SettingsService.CurrentOrDefault;

            if (input == null || GamePause.IsPaused)
            {
                frame.desiredStance = crawling ? Stance.Crawling : (s.crouchToggle && crouchToggled ? Stance.Crouching : Stance.Standing);
                return frame;
            }

            frame.move = Vector2.ClampMagnitude(input.Move.ReadValue<Vector2>(), 1f);

            Vector2 mouse = input.Look.ReadValue<Vector2>() * s.mouseSensitivity;
            Vector2 stick = input.LookStick.ReadValue<Vector2>();
            stick = stick * stick.magnitude * s.gamepadSensitivity * deltaTime; // response curve for precision
            Vector2 look = mouse + stick;
            if (s.invertY) look.y = -look.y;
            frame.lookDelta = look;

            if (input.Crawl.WasPressedThisFrame())
            {
                crawling = !crawling;
            }

            if (input.Crouch.WasPressedThisFrame())
            {
                if (crawling)
                {
                    crawling = false;          // crawl -> crouch
                    crouchToggled = true;
                }
                else if (s.crouchToggle)
                {
                    crouchToggled = !crouchToggled;
                }
            }

            bool sprintHeld = input.Sprint.IsPressed();
            if (input.Sprint.WasPressedThisFrame())
            {
                crawling = false;
                crouchToggled = false;
            }

            // In hold mode, the "toggled" flag only bridges the moment you leave crawl while holding Ctrl.
            bool crouchHeld = input.Crouch.IsPressed();
            if (!s.crouchToggle && !crouchHeld) crouchToggled = false;
            bool crouching = s.crouchToggle ? crouchToggled : crouchHeld;

            frame.desiredStance = crawling ? Stance.Crawling : (crouching ? Stance.Crouching : Stance.Standing);
            frame.sprint = sprintHeld;
            frame.interactHeld = input.Interact.IsPressed();
            frame.interactPressed = input.Interact.WasPressedThisFrame();
            frame.dropPressed = input.Drop.WasPressedThisFrame();
            return frame;
        }

        /// <summary>Back to standing (used on respawn / reset).</summary>
        public void ResetStance()
        {
            crawling = false;
            crouchToggled = false;
        }
    }
}
