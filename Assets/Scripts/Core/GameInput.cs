using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReadyCheck
{
    /// <summary>
    /// All player input goes through here: gamepad first, mouse and keyboard as an equal alternative.
    /// Gameplay code never reads devices directly. That keeps rebinding possible and makes local
    /// co-op (one GameInput per player) a contained change later on.
    /// </summary>
    public class GameInput : MonoBehaviour
    {
        public static GameInput Instance { get; private set; }

        public InputAction Move { get; private set; }
        public InputAction WheelSelect { get; private set; }
        public InputAction Attack { get; private set; }
        public InputAction Dodge { get; private set; }
        public InputAction Rally { get; private set; }
        public InputAction Wheel { get; private set; }
        public InputAction CallStack { get; private set; }
        public InputAction CallSpread { get; private set; }
        public InputAction CallInterrupt { get; private set; }
        public InputAction CallBurn { get; private set; }
        public InputAction CallDefensives { get; private set; }
        public InputAction CallAsYouWere { get; private set; }
        public InputAction Confirm { get; private set; }
        public InputAction ToggleHints { get; private set; }
        public InputAction ResetProgress { get; private set; }

        /// <summary>
        /// False when Unity's "Active Input Handling" player setting is still set to the old Input Manager only.
        /// The HUD shows a fix-it message in that case.
        /// </summary>
        public static bool BackendEnabled
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>True when the last device the player touched was a gamepad (used for button glyphs).</summary>
        public bool UsingGamepad { get; private set; }

        readonly List<InputAction> all = new List<InputAction>();

        void Awake()
        {
            Instance = this;

            Move = new InputAction("Move", InputActionType.Value);
            Move.AddBinding("<Gamepad>/leftStick");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            all.Add(Move);

            WheelSelect = new InputAction("WheelSelect", InputActionType.Value);
            WheelSelect.AddBinding("<Gamepad>/rightStick");
            all.Add(WheelSelect);

            Attack = Button("Attack", "<Gamepad>/rightTrigger", "<Mouse>/leftButton", "<Keyboard>/j");
            Dodge = Button("Dodge", "<Gamepad>/buttonSouth", "<Keyboard>/space");
            Rally = Button("Rally", "<Gamepad>/buttonNorth", "<Keyboard>/r");
            Wheel = Button("RaidWheel", "<Gamepad>/leftShoulder", "<Keyboard>/q", "<Mouse>/rightButton");

            CallStack = Button("CallStack", "<Gamepad>/dpad/up", "<Keyboard>/1");
            CallSpread = Button("CallSpread", "<Gamepad>/dpad/down", "<Keyboard>/2");
            CallInterrupt = Button("CallInterrupt", "<Gamepad>/dpad/left", "<Keyboard>/3");
            CallBurn = Button("CallBurn", "<Gamepad>/dpad/right", "<Keyboard>/4");
            CallDefensives = Button("CallDefensives", "<Keyboard>/5");
            CallAsYouWere = Button("CallAsYouWere", "<Keyboard>/6");

            Confirm = Button("Confirm", "<Gamepad>/start", "<Gamepad>/buttonSouth", "<Keyboard>/enter",
                "<Keyboard>/numpadEnter", "<Keyboard>/space");
            ToggleHints = Button("ToggleHints", "<Gamepad>/select", "<Keyboard>/h");
            ResetProgress = Button("ResetProgress", "<Keyboard>/f9");

            foreach (var action in all) action.Enable();
        }

        InputAction Button(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (var binding in bindings) action.AddBinding(binding);
            all.Add(action);
            return action;
        }

        void OnDestroy()
        {
            foreach (var action in all)
            {
                action.Disable();
                action.Dispose();
            }
            all.Clear();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var pad = Gamepad.current;
            if (pad != null)
            {
                bool padUsed = pad.leftStick.ReadValue().sqrMagnitude > 0.1f
                               || pad.rightStick.ReadValue().sqrMagnitude > 0.1f
                               || pad.dpad.ReadValue().sqrMagnitude > 0.1f
                               || pad.buttonSouth.isPressed || pad.buttonNorth.isPressed
                               || pad.buttonEast.isPressed || pad.buttonWest.isPressed
                               || pad.leftShoulder.isPressed || pad.rightShoulder.isPressed
                               || pad.rightTrigger.isPressed || pad.leftTrigger.isPressed
                               || pad.startButton.isPressed || pad.selectButton.isPressed;
                if (padUsed) UsingGamepad = true;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool kbmUsed = (keyboard != null && keyboard.anyKey.isPressed)
                           || (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed
                                                 || mouse.delta.ReadValue().sqrMagnitude > 9f));
            if (kbmUsed) UsingGamepad = false;
        }

        /// <summary>Mouse position in screen pixels, or null when there is no mouse.</summary>
        public Vector2? PointerPosition
        {
            get
            {
                var mouse = Mouse.current;
                if (mouse == null) return null;
                return mouse.position.ReadValue();
            }
        }

        /// <summary>Picks the gamepad or keyboard label for a button prompt.</summary>
        public string Glyph(string gamepad, string keyboardMouse) => UsingGamepad ? gamepad : keyboardMouse;
    }
}
