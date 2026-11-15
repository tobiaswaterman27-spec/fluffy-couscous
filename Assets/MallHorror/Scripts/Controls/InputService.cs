using UnityEngine;
using UnityEngine.InputSystem;

namespace MallHorror.Controls
{
    /// <summary>
    /// Builds all input actions in code (no asset to set up by hand).
    /// Keyboard + mouse and gamepad are both supported.
    /// </summary>
    public class InputService : MonoBehaviour
    {
        public static InputService Instance { get; private set; }

        public InputActionMap GameplayMap { get; private set; }
        public InputActionMap SystemMap { get; private set; }

        public InputAction Move { get; private set; }
        public InputAction Look { get; private set; }
        public InputAction LookStick { get; private set; }
        public InputAction Sprint { get; private set; }
        public InputAction Crouch { get; private set; }
        public InputAction Crawl { get; private set; }
        public InputAction Interact { get; private set; }
        public InputAction Drop { get; private set; }

        public InputAction Pause { get; private set; }
        public InputAction ToggleStats { get; private set; }
        public InputAction ToggleDebugView { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Build();
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

        void Build()
        {
            GameplayMap = new InputActionMap("Gameplay");

            Move = GameplayMap.AddAction("Move", InputActionType.Value);
            Move.expectedControlType = "Vector2";
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");

            Look = GameplayMap.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            Look.expectedControlType = "Vector2";

            LookStick = GameplayMap.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");
            LookStick.expectedControlType = "Vector2";

            Sprint = GameplayMap.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");

            Crouch = GameplayMap.AddAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
            Crouch.AddBinding("<Gamepad>/buttonEast");

            Crawl = GameplayMap.AddAction("Crawl", InputActionType.Button, "<Keyboard>/c");
            Crawl.AddBinding("<Gamepad>/dpad/down");

            Interact = GameplayMap.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            Interact.AddBinding("<Gamepad>/buttonSouth");

            Drop = GameplayMap.AddAction("Drop", InputActionType.Button, "<Keyboard>/g");
            Drop.AddBinding("<Gamepad>/buttonWest");

            SystemMap = new InputActionMap("System");
            Pause = SystemMap.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            Pause.AddBinding("<Gamepad>/start");
            ToggleStats = SystemMap.AddAction("ToggleStats", InputActionType.Button, "<Keyboard>/f3");
            ToggleDebugView = SystemMap.AddAction("ToggleDebugView", InputActionType.Button, "<Keyboard>/f5");
        }
    }
}
