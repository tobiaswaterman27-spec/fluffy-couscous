using UnityEngine;
using AfterHours.Core;
using AfterHours.Settings;

namespace AfterHours.Controls
{
    /// <summary>
    /// Reads the local keyboard/mouse/gamepad and turns it into a <see cref="PlayerInputFrame"/>.
    ///
    /// Stance and sprint are all HOLD controls:
    ///   Sprint = double-tap a movement key and keep holding it (any direction, e.g. double-tap S
    ///            to run backwards). Letting go of movement ends the sprint. Gamepad: hold the left
    ///            stick click, or flick the stick twice in the same direction.
    ///   Shift  = hold to crouch.
    ///   Ctrl   = hold to crawl (takes priority over crouch).
    /// Letting go when there is no room above you keeps you low until there is
    /// (the movement code checks the real space before rising).
    /// </summary>
    public class LocalPlayerInput : MonoBehaviour, IPlayerInputSource
    {
        [Tooltip("Maximum time between the two taps of a double-tap (seconds).")]
        public float doubleTapWindow = 0.3f;

        Vector2 lastTapDirection;
        float lastTapTime = -10f;
        bool wasMoving;
        Vector2 sprintDirection;
        bool sprintLatched;

        public PlayerInputFrame ReadFrame(float deltaTime)
        {
            var frame = new PlayerInputFrame();
            var input = InputService.Instance;
            GameSettings s = SettingsService.CurrentOrDefault;

            if (input == null || GamePause.IsPaused)
            {
                sprintLatched = false;
                wasMoving = false;
                return frame; // standing request is harmless: no time passes while paused
            }

            Vector2 move = Vector2.ClampMagnitude(input.Move.ReadValue<Vector2>(), 1f);
            frame.move = move;

            Vector2 mouse = input.Look.ReadValue<Vector2>() * s.mouseSensitivity;
            Vector2 stick = input.LookStick.ReadValue<Vector2>();
            stick = stick * stick.magnitude * s.gamepadSensitivity * deltaTime; // response curve for precision
            Vector2 look = mouse + stick;
            if (s.invertY) look.y = -look.y;
            frame.lookDelta = look;

            UpdateDoubleTapSprint(move);
            frame.sprint = sprintLatched || input.Sprint.IsPressed();

            bool crawlHeld = input.Crawl.IsPressed();
            bool crouchHeld = input.Crouch.IsPressed();
            frame.desiredStance = crawlHeld ? Stance.Crawling : (crouchHeld ? Stance.Crouching : Stance.Standing);

            frame.interactHeld = input.Interact.IsPressed();
            frame.interactPressed = input.Interact.WasPressedThisFrame();
            frame.dropPressed = input.Drop.WasPressedThisFrame();
            return frame;
        }

        void UpdateDoubleTapSprint(Vector2 move)
        {
            bool moving = move.sqrMagnitude > 0.25f; // key pressed / stick pushed past half way
            float now = Time.unscaledTime;

            if (moving)
            {
                Vector2 dir = Quantise(move);
                if (!wasMoving)
                {
                    // A new tap. Same direction as the last tap, quickly enough? -> sprint.
                    if (dir == lastTapDirection && now - lastTapTime <= doubleTapWindow)
                    {
                        sprintLatched = true;
                        sprintDirection = dir;
                    }
                    lastTapDirection = dir;
                    lastTapTime = now;
                }
                // Sprint lasts while the sprint direction is still held (you may add a sideways key).
                if (sprintLatched && Vector2.Dot(dir, sprintDirection) <= 0f) sprintLatched = false;
            }
            else
            {
                sprintLatched = false;
            }
            wasMoving = moving;
        }

        /// <summary>Snaps a direction to one of 8 compass directions so double-taps compare reliably.</summary>
        static Vector2 Quantise(Vector2 v)
        {
            float angle = Mathf.Atan2(v.y, v.x);
            float step = Mathf.PI / 4f;
            float snapped = Mathf.Round(angle / step) * step;
            return new Vector2(Mathf.Round(Mathf.Cos(snapped)), Mathf.Round(Mathf.Sin(snapped)));
        }

        /// <summary>Clears sprint state (used on respawn / reset).</summary>
        public void ResetState()
        {
            sprintLatched = false;
            wasMoving = false;
            lastTapTime = -10f;
        }
    }
}
