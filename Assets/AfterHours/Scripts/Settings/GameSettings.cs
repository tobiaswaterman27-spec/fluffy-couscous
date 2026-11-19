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
        [Tooltip("Custom key bindings (Input System override JSON). Empty = defaults.")]
        public string bindingOverrides = "";

        [Header("Camera")]
        public float fieldOfView = 72f;
        public bool headBob = true;
        public bool cameraShake = true;
        public bool rumble = true;               // controller vibration

        [Header("Content")]
        [Tooltip("Blood and gore in the death sequence.")]
        public bool gore = true;
        public string character = "tom";        // which person you play as (PlayerCharacters)
        [Tooltip("0 easy, 1 normal, 2 hard, 3 nightmare, 4 one life (the host's applies in co-op).")]
        public int difficulty = 1;

        [Header("Audio")]
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;

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
            musicVolume = Mathf.Clamp01(musicVolume);
            targetFrameRate = Mathf.Clamp(targetFrameRate, 0, 360);
            if (string.IsNullOrEmpty(character)) character = "tom";
            difficulty = Mathf.Clamp(difficulty, 0, 4);
            // Future versions migrate older files here before stamping the new version.
            version = CurrentVersion;
        }
    }
}
