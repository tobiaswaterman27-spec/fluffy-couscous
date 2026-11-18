using UnityEngine;
using UnityEngine.InputSystem;
using AfterHours.Controls;
using AfterHours.Core;
using AfterHours.Player;
using AfterHours.Settings;
using AfterHours.World.Layout;

namespace AfterHours.UI
{
    /// <summary>
    /// Temporary pause menu (Esc) with a Settings page and a Controls page for rebinding keys.
    /// The final styled menus come in Part 13/22; the rebinding logic carries over unchanged.
    /// </summary>
    public class DevPauseMenu : MonoBehaviour
    {
        enum Page { Settings, Controls, Teleport }

        Page page = Page.Settings;
        bool cursorLockedOnce;
        Vector2 scroll;
        InputActionRebindingExtensions.RebindingOperation rebind;
        string rebindLabel;
        int ignorePauseUntilFrame;
        GUIStyle title, rowLabel, waiting;

        static DevPauseMenu instance;
        void Awake() => instance = this;

        /// <summary>The main menu's "Settings" button: opens this menu on the settings page.</summary>
        public static void OpenFromMenu()
        {
            if (instance == null) return;
            instance.page = Page.Settings;
            instance.SetPaused(true);
        }

        void Update()
        {
            InputService input = InputService.Instance;
            if (input == null) return;
            if (GameFlowFlags.InMainMenu)
            {
                // The main menu owns the screen; Esc only closes the settings opened from it.
                if (GamePause.IsPaused && rebind == null && input.Pause.WasPressedThisFrame() && Time.frameCount > ignorePauseUntilFrame) SetPaused(false);
                return;
            }

            if (!cursorLockedOnce)
            {
                GamePause.SetCursorLocked(true);
                cursorLockedOnce = true;
            }

            bool pausePressed = input.Pause.WasPressedThisFrame();
            if (pausePressed && rebind == null && Time.frameCount > ignorePauseUntilFrame) SetPaused(!GamePause.IsPaused);

            // Clicking back into the game window re-captures the mouse.
            if (!GamePause.IsPaused && Cursor.lockState != CursorLockMode.Locked &&
                Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                GamePause.SetCursorLocked(true);
            }
        }

        void OnDestroy()
        {
            rebind?.Dispose();
        }

        void SetPaused(bool paused)
        {
            if (!paused) CancelRebind();
            GamePause.SetPaused(paused);
            if (GameFlowFlags.InMainMenu) GamePause.SetCursorLocked(false);
            InputService.Instance?.SetGameplayEnabled(!paused && !GameFlowFlags.InMainMenu);
            if (!paused) SettingsService.Instance?.Save();
        }

        // ------------------------------------------------------------------

        void OnGUI()
        {
            if (!GamePause.IsPaused) return;
            SettingsService service = SettingsService.Instance;
            InputService input = InputService.Instance;
            if (service == null || input == null) return;
            EnsureStyles();
            GUISkin oldSkin = GUI.skin;
            GUI.skin = UiSkin.Get();
            // Darken the game behind the menu.
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float w = 600f, h = 700f;
            float x = (Screen.width / scale - w) * 0.5f;
            float y = (1080f - h) * 0.5f;

            GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
            GUILayout.Label(GameFlowFlags.InMainMenu ? "SETTINGS" : "PAUSED", title);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(page == Page.Settings, "Settings", GUI.skin.button, GUILayout.Height(30))) page = Page.Settings;
            if (GUILayout.Toggle(page == Page.Controls, "Controls", GUI.skin.button, GUILayout.Height(30))) page = Page.Controls;
            if (GUILayout.Toggle(page == Page.Teleport, "Teleport (dev)", GUI.skin.button, GUILayout.Height(30))) page = Page.Teleport;
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            if (page == Page.Settings) DrawSettings(service);
            else if (page == Page.Controls) DrawControls(input);
            else DrawTeleport();

            GUILayout.FlexibleSpace();
            GUI.enabled = rebind == null;
            if (GUILayout.Button(GameFlowFlags.InMainMenu ? "Back" : "Resume", GUILayout.Height(36))) SetPaused(false);
            if (!GameFlowFlags.InMainMenu && GameFlowFlags.QuitToMenu != null && GUILayout.Button("Save and quit to main menu", GUILayout.Height(30)))
            {
                service.Save();
                System.Action quit = GameFlowFlags.QuitToMenu;
                SetPaused(false);
                quit();
                GUI.enabled = true;
                GUILayout.EndArea();
                GUI.matrix = Matrix4x4.identity;
                GUI.skin = oldSkin;
                return;
            }
            if (GUILayout.Button("Quit", GUILayout.Height(30)))
            {
                service.Save();
                if (!GameFlowFlags.InMainMenu && AfterHours.Flow.GameFlow.Instance != null) AfterHours.Flow.GameFlow.Instance.SaveNow();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            GUI.enabled = true;
            GUILayout.EndArea();
            GUI.matrix = Matrix4x4.identity;
            GUI.skin = oldSkin;
        }

        void DrawSettings(SettingsService service)
        {
            GameSettings s = service.Current;
            GUI.changed = false;
            GUILayout.Label($"Mouse sensitivity: {s.mouseSensitivity:0.000}");
            s.mouseSensitivity = GUILayout.HorizontalSlider(s.mouseSensitivity, 0.02f, 0.5f);
            GUILayout.Label($"Gamepad look speed: {s.gamepadSensitivity:0}");
            s.gamepadSensitivity = Mathf.Round(GUILayout.HorizontalSlider(s.gamepadSensitivity, 40f, 500f));
            GUILayout.Label($"Field of view: {s.fieldOfView:0}");
            s.fieldOfView = Mathf.Round(GUILayout.HorizontalSlider(s.fieldOfView, 55f, 100f));
            GUILayout.Label($"Master volume: {s.masterVolume * 100f:0}%");
            s.masterVolume = GUILayout.HorizontalSlider(s.masterVolume, 0f, 1f);
            GUILayout.Label($"Music: {s.musicVolume * 100f:0}%");
            s.musicVolume = GUILayout.HorizontalSlider(s.musicVolume, 0f, 1f);
            s.invertY = GUILayout.Toggle(s.invertY, " Invert look Y");
            s.headBob = GUILayout.Toggle(s.headBob, " Head bob");
            s.cameraShake = GUILayout.Toggle(s.cameraShake, " Camera shake");
            s.gore = GUILayout.Toggle(s.gore, " Blood and gore");
            s.vSync = GUILayout.Toggle(s.vSync, " V-Sync");
            // who you play as
            GUILayout.Space(6);
            PlayerCharacters.Entry me = PlayerCharacters.Get(s.character);
            GUILayout.Label("You are: " + (me != null ? me.name : s.character));
            PlayerCharacters.Entry[] all = PlayerCharacters.All;
            for (int i = 0; i < all.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int k = i; k < Mathf.Min(i + 2, all.Length); k++)
                {
                    bool on = all[k].id == s.character;
                    if (GUILayout.Toggle(on, all[k].name, GUI.skin.button, GUILayout.Height(26)) && !on) { s.character = all[k].id; GUI.changed = true; }
                }
                GUILayout.EndHorizontal();
            }
            if (GUI.changed) service.ApplyRuntime();
        }

        void DrawTeleport()
        {
            GUILayout.Label("Developer testing only: jump to a place in the mall.");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(400));
            foreach (DevLocation loc in MallLayout.DevLocations)
            {
                if (GUILayout.Button(loc.name, GUILayout.Height(26)))
                {
                    PlayerController player = FindAnyObjectByType<PlayerController>();
                    if (player != null)
                    {
                        player.Teleport(loc.position, loc.yaw);
                        SetPaused(false);
                    }
                }
            }
            GUILayout.EndScrollView();
        }

        void DrawControls(InputService input)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Action", rowLabel, GUILayout.Width(170));
            GUILayout.Label("Keyboard / mouse", rowLabel, GUILayout.Width(170));
            GUILayout.Label("Gamepad", rowLabel, GUILayout.Width(170));
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(330));
            foreach (string actionName in InputService.RebindableActions)
            {
                InputAction action = input.GameplayMap.FindAction(actionName);
                if (action == null) continue;
                DrawActionRows(action);
            }
            GUILayout.EndScrollView();

            if (rebind != null)
            {
                GUILayout.Label($"Press a key for \"{rebindLabel}\"...  (Esc to cancel)", waiting);
            }
            else
            {
                GUILayout.Label("Click a binding, then press the new key or button. Changes save automatically.");
                if (GUILayout.Button("Reset all controls to defaults", GUILayout.Height(28))) input.ResetAllBindings();
            }
        }

        void DrawActionRows(InputAction action)
        {
            // Composite parts (e.g. Move Up/Down/Left/Right) each get their own row; plain bindings share a row.
            int keyboardPlain = -1, gamepadPlain = -1;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding b = action.bindings[i];
                if (b.isComposite) continue;
                bool pad = IsGamepad(b);
                if (b.isPartOfComposite)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{action.name} {Capitalise(b.name)}", GUILayout.Width(170));
                    BindingButton(action, i, pad ? 340 : 170);
                    GUILayout.EndHorizontal();
                }
                else if (pad && gamepadPlain < 0) gamepadPlain = i;
                else if (!pad && keyboardPlain < 0) keyboardPlain = i;
            }

            if (keyboardPlain < 0 && gamepadPlain < 0) return;
            GUILayout.BeginHorizontal();
            GUILayout.Label(action.name, GUILayout.Width(170));
            if (keyboardPlain >= 0) BindingButton(action, keyboardPlain, 170);
            else GUILayout.Label("", GUILayout.Width(170));
            if (gamepadPlain >= 0) BindingButton(action, gamepadPlain, 170);
            GUILayout.EndHorizontal();

            // Extra keyboard keys on the same action (e.g. Crouch on both Ctrl and C).
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite || i == keyboardPlain || i == gamepadPlain) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(action.name + " (alt)", GUILayout.Width(170));
                if (IsGamepad(b)) GUILayout.Label("", GUILayout.Width(170));
                BindingButton(action, i, 170);
                GUILayout.EndHorizontal();
            }
        }

        void BindingButton(InputAction action, int index, float width)
        {
            string shown = action.GetBindingDisplayString(index);
            if (string.IsNullOrEmpty(shown)) shown = "— unbound —";
            if (GUILayout.Button(shown, GUILayout.Width(width - 6)) && rebind == null)
            {
                StartRebind(action, index);
            }
        }

        void StartRebind(InputAction action, int index)
        {
            InputBinding b = action.bindings[index];
            bool pad = IsGamepad(b);
            rebindLabel = b.isPartOfComposite ? $"{action.name} {Capitalise(b.name)}" : action.name;
            action.Disable();

            var op = action.PerformInteractiveRebinding(index)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .OnMatchWaitForAnother(0.1f);
            op = pad ? op.WithControlsHavingToMatchPath("<Gamepad>") : op.WithControlsExcluding("<Gamepad>");
            op.OnComplete(o => FinishRebind(true))
              .OnCancel(o => FinishRebind(false));
            rebind = op;
            op.Start();
        }

        void FinishRebind(bool changed)
        {
            rebind?.Dispose();
            rebind = null;
            ignorePauseUntilFrame = Time.frameCount + 2; // Esc used to cancel must not also close the menu
            if (changed) InputService.Instance?.SaveBindingOverrides();
        }

        void CancelRebind()
        {
            if (rebind == null) return;
            rebind.Cancel();
        }

        static bool IsGamepad(InputBinding b)
        {
            return !string.IsNullOrEmpty(b.groups) && b.groups.Contains(InputService.GamepadGroup);
        }

        static string Capitalise(string s)
        {
            return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(UiSkin.Get().label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            rowLabel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            waiting = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            waiting.normal.textColor = new Color(1f, 0.85f, 0.3f);
        }
    }
}
