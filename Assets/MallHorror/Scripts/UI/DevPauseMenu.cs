using UnityEngine;
using UnityEngine.InputSystem;
using MallHorror.Controls;
using MallHorror.Core;
using MallHorror.Settings;

namespace MallHorror.UI
{
    /// <summary>
    /// Temporary pause + settings menu (Esc). The final styled main menu / pause menu comes in Part 13/22.
    /// </summary>
    public class DevPauseMenu : MonoBehaviour
    {
        bool cursorLockedOnce;

        void Update()
        {
            InputService input = InputService.Instance;
            if (input == null) return;

            if (!cursorLockedOnce)
            {
                GamePause.SetCursorLocked(true);
                cursorLockedOnce = true;
            }

            if (input.Pause.WasPressedThisFrame()) SetPaused(!GamePause.IsPaused);

            // Clicking back into the game window re-captures the mouse.
            if (!GamePause.IsPaused && Cursor.lockState != CursorLockMode.Locked &&
                Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                GamePause.SetCursorLocked(true);
            }
        }

        void SetPaused(bool paused)
        {
            GamePause.SetPaused(paused);
            InputService.Instance?.SetGameplayEnabled(!paused);
            if (!paused) SettingsService.Instance?.Save();
        }

        void OnGUI()
        {
            if (!GamePause.IsPaused) return;
            SettingsService service = SettingsService.Instance;
            if (service == null) return;
            GameSettings s = service.Current;

            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float w = 420f, h = 470f;
            float x = (Screen.width / scale - w) * 0.5f;
            float y = (1080f - h) * 0.5f;

            GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
            GUILayout.Label("<b><size=22>PAUSED</size></b>", new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(10);

            GUI.changed = false;
            GUILayout.Label($"Mouse sensitivity: {s.mouseSensitivity:0.000}");
            s.mouseSensitivity = GUILayout.HorizontalSlider(s.mouseSensitivity, 0.02f, 0.5f);
            GUILayout.Label($"Field of view: {s.fieldOfView:0}");
            s.fieldOfView = Mathf.Round(GUILayout.HorizontalSlider(s.fieldOfView, 55f, 100f));
            GUILayout.Label($"Master volume: {s.masterVolume * 100f:0}%");
            s.masterVolume = GUILayout.HorizontalSlider(s.masterVolume, 0f, 1f);
            s.invertY = GUILayout.Toggle(s.invertY, " Invert mouse Y");
            s.crouchToggle = GUILayout.Toggle(s.crouchToggle, " Crouch is a toggle (off = hold)");
            s.headBob = GUILayout.Toggle(s.headBob, " Head bob");
            s.vSync = GUILayout.Toggle(s.vSync, " V-Sync");
            if (GUI.changed) service.ApplyRuntime();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Resume", GUILayout.Height(36))) SetPaused(false);
            if (GUILayout.Button("Quit", GUILayout.Height(30)))
            {
                service.Save();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            GUILayout.EndArea();
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
