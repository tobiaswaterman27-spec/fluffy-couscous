using System;
using UnityEngine;

namespace AfterHours.Settings
{
    /// <summary>Player-adjustable options. Saved as JSON in the persistent data folder.</summary>
    [Serializable]
    public class GameSettings
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        [Header("Controls")]
        public float mouseSensitivity = 0.12f;   // degrees per mouse pixel
        public float gamepadSensitivity = 180f;  // degrees per second at full stick
        public bool invertY = false;
        [Tooltip("Off = hold Ctrl to crouch. On = press Ctrl to crouch, press again to stand.")]
        public bool crouchToggle = false;
        [Tooltip("Custom key bindings (Input System override JSON). Empty = defaults.")]
        public string bindingOverrides = "";

        [Header("Camera")]
        public float fieldOfView = 72f;
        public bool headBob = true;

        [Header("Audio")]
        public float masterVolume = 1f;

        [Header("Display")]
        public bool vSync = true;
        public int targetFrameRate = 0;          // 0 = unlimited / vsync controlled

        public void Sanitize()
        {
            mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.01f, 1f);
            gamepadSensitivity = Mathf.Clamp(gamepadSensitivity, 20f, 600f);
            if (bindingOverrides == null) bindingOverrides = "";
            fieldOfView = Mathf.Clamp(fieldOfView, 55f, 100f);
            masterVolume = Mathf.Clamp01(masterVolume);
            targetFrameRate = Mathf.Clamp(targetFrameRate, 0, 360);
            // Future versions migrate older files here before stamping the new version.
            version = CurrentVersion;
        }
    }
}
