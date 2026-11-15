using UnityEngine;
using MallHorror.Core;
using MallHorror.Settings;

namespace MallHorror.Controls
{
    /// <summary>
    /// Reads the local keyboard/mouse/gamepad and turns it into a <see cref="PlayerInputFrame"/>.
    /// Also resolves hold/toggle crouch and the crawl toggle into one desired stance.
    /// </summary>
    public class LocalPlayerInput : MonoBehaviour, IPlayerInputSource
    {
        bool crouchToggled;
        bool crawlToggled;

        public PlayerInputFrame ReadFrame(float deltaTime)
        {
            var frame = new PlayerInputFrame();
            var input = InputService.Instance;
            GameSettings s = SettingsService.CurrentOrDefault;

            if (input == null || GamePause.IsPaused)
            {
                frame.desiredStance = crawlToggled ? Stance.Crawling : (crouchToggled ? Stance.Crouching : Stance.Standing);
                return frame;
            }

            frame.move = Vector2.ClampMagnitude(input.Move.ReadValue<Vector2>(), 1f);

            Vector2 mouse = input.Look.ReadValue<Vector2>() * s.mouseSensitivity;
            Vector2 stick = input.LookStick.ReadValue<Vector2>();
            stick = stick * stick.magnitude * s.gamepadSensitivity * deltaTime; // response curve for precision
            Vector2 look = mouse + stick;
            if (s.invertY) look.y = -look.y;
            frame.lookDelta = look;

            bool sprintHeld = input.Sprint.IsPressed();

            if (input.Crawl.WasPressedThisFrame())
            {
                crawlToggled = !crawlToggled;
                if (!crawlToggled && s.crouchToggle) crouchToggled = true; // leaving crawl -> crouch first
            }

            if (input.Crouch.WasPressedThisFrame())
            {
                if (crawlToggled)
                {
                    crawlToggled = false;              // crawl -> crouch
                    crouchToggled = s.crouchToggle;
                }
                else if (s.crouchToggle)
                {
                    crouchToggled = !crouchToggled;
                }
            }

            // Starting to sprint cancels toggled crouch / crawl (only if there is space to stand).
            if (sprintHeld && input.Sprint.WasPressedThisFrame())
            {
                crouchToggled = false;
                crawlToggled = false;
            }

            bool crouching = s.crouchToggle ? crouchToggled : input.Crouch.IsPressed();
            frame.desiredStance = crawlToggled ? Stance.Crawling : (crouching ? Stance.Crouching : Stance.Standing);

            frame.sprint = sprintHeld;
            frame.interactHeld = input.Interact.IsPressed();
            frame.interactPressed = input.Interact.WasPressedThisFrame();
            frame.dropPressed = input.Drop.WasPressedThisFrame();
            return frame;
        }

        /// <summary>Clears toggles (used on respawn / reset).</summary>
        public void ResetStanceToggles()
        {
            crouchToggled = false;
            crawlToggled = false;
        }
    }
}
