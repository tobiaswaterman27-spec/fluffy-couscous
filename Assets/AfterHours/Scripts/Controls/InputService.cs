using System;
using UnityEngine;
using UnityEngine.InputSystem;
using AfterHours.Settings;

namespace AfterHours.Controls
{
    /// <summary>
    /// Builds all input actions in code (no asset to set up by hand), and handles key rebinding.
    /// Every binding belongs to a group ("KeyboardMouse" or "Gamepad") so the controls menu can show
    /// and rebind each device separately. Custom bindings are stored in the settings file.
    /// </summary>
    public class InputService : MonoBehaviour
    {
        public const string KeyboardGroup = "KeyboardMouse";
        public const string GamepadGroup = "Gamepad";

        public static InputService Instance { get; private set; }

        /// <summary>Actions shown in the Controls menu, in display order.</summary>
        public static readonly string[] RebindableActions = { "Move", "Sprint", "Crouch", "Crawl", "Interact", "Drop" };

        public InputActionMap GameplayMap { get; private set; }
        public InputActionMap SystemMap { get; private set; }

        public InputAction Move { get; private set; }
        public InputAction Look { get; private set; }
        public InputAction LookStick { get; private set; }
        public InputAction Sprint { get; private set; }
        /// <summary>Hold to crouch.</summary>
        public InputAction Crouch { get; private set; }
        /// <summary>Hold to crawl.</summary>
        public InputAction Crawl { get; private set; }
        public InputAction Interact { get; private set; }
        public InputAction Drop { get; private set; }

        public InputAction Pause { get; private set; }
        public InputAction ToggleStats { get; private set; }
        public InputAction ToggleDebugView { get; private set; }
        public InputAction Screenshot { get; private set; }
        public InputAction ToggleFullbright { get; private set; }

        public event Action BindingsChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Build();
            LoadBindingOverrides();
            GameplayMap.Enable();
            SystemMap.Enable();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            GameplayMap?.Disable();
            SystemMap?.Disable();
            Instance = null;
        }

        public void SetGameplayEnabled(bool enabled)
        {
            if (enabled) GameplayMap.Enable();
            else GameplayMap.Disable();
        }

        // ---------------- rebinding ----------------

        public void LoadBindingOverrides()
        {
            string json = SettingsService.CurrentOrDefault.bindingOverrides;
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                GameplayMap.LoadBindingOverridesFromJson(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AfterHours] Saved key bindings could not be loaded, using defaults: " + e.Message);
                GameplayMap.RemoveAllBindingOverrides();
            }
        }

        public void SaveBindingOverrides()
        {
            SettingsService service = SettingsService.Instance;
            if (service == null) return;
            service.Current.bindingOverrides = GameplayMap.SaveBindingOverridesAsJson();
            service.Save();
            BindingsChanged?.Invoke();
        }

        public void ResetAllBindings()
        {
            GameplayMap.RemoveAllBindingOverrides();
            SaveBindingOverrides();
        }

        // ---------------- default bindings ----------------

        void Build()
        {
            GameplayMap = new InputActionMap("Gameplay");

            Move = GameplayMap.AddAction("Move", InputActionType.Value);
            Move.expectedControlType = "Vector2";
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", KeyboardGroup)
                .With("Down", "<Keyboard>/s", KeyboardGroup)
                .With("Left", "<Keyboard>/a", KeyboardGroup)
                .With("Right", "<Keyboard>/d", KeyboardGroup);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", KeyboardGroup)
                .With("Down", "<Keyboard>/downArrow", KeyboardGroup)
                .With("Left", "<Keyboard>/leftArrow", KeyboardGroup)
                .With("Right", "<Keyboard>/rightArrow", KeyboardGroup);
            Move.AddBinding("<Gamepad>/leftStick", groups: GamepadGroup);

            Look = GameplayMap.AddAction("Look", InputActionType.Value);
            Look.expectedControlType = "Vector2";
            Look.AddBinding("<Mouse>/delta", groups: KeyboardGroup);

            LookStick = GameplayMap.AddAction("LookStick", InputActionType.Value);
            LookStick.expectedControlType = "Vector2";
            LookStick.AddBinding("<Gamepad>/rightStick", groups: GamepadGroup);

            // Sprint is normally a double-tap of a movement key (see LocalPlayerInput).
            // This optional hold-to-sprint key is unbound on keyboard by default.
            Sprint = Button("Sprint", "", "<Gamepad>/leftStickPress");

            Crouch = Button("Crouch", "<Keyboard>/leftShift", "<Gamepad>/buttonEast");
            Crawl = Button("Crawl", "<Keyboard>/leftCtrl", "<Gamepad>/rightShoulder");

            Interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            Drop = Button("Drop", "<Keyboard>/g", "<Gamepad>/buttonWest");

            SystemMap = new InputActionMap("System");
            Pause = SystemMap.AddAction("Pause", InputActionType.Button);
            Pause.AddBinding("<Keyboard>/escape", groups: KeyboardGroup);
            Pause.AddBinding("<Gamepad>/start", groups: GamepadGroup);
            ToggleStats = SystemMap.AddAction("ToggleStats", InputActionType.Button);
            ToggleStats.AddBinding("<Keyboard>/f3", groups: KeyboardGroup);
            ToggleDebugView = SystemMap.AddAction("ToggleDebugView", InputActionType.Button);
            ToggleDebugView.AddBinding("<Keyboard>/f5", groups: KeyboardGroup);
            Screenshot = SystemMap.AddAction("Screenshot", InputActionType.Button);
            Screenshot.AddBinding("<Keyboard>/f12", groups: KeyboardGroup);
            ToggleFullbright = SystemMap.AddAction("ToggleFullbright", InputActionType.Button);
            ToggleFullbright.AddBinding("<Keyboard>/f6", groups: KeyboardGroup);
        }

        /// <summary>Button action with one keyboard/mouse and one gamepad binding (either may be empty = unbound).</summary>
        InputAction Button(string name, string keyboardPath, string gamepadPath)
        {
            InputAction a = GameplayMap.AddAction(name, InputActionType.Button);
            a.AddBinding(keyboardPath, groups: KeyboardGroup);
            a.AddBinding(gamepadPath, groups: GamepadGroup);
            return a;
        }
    }
}
