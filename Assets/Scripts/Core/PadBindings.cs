using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace PackTheTrunk
{
    /// <summary>
    /// Remappable gamepad buttons for the packing actions (Settings → Controls), like
    /// <see cref="Bindings"/> for keys. Only buttons that have no other job can be given to an action:
    /// A (click), B (back), Menu (pause), LB (the other way / redo), the sticks and the triggers (look
    /// and zoom) stay put, so the menus and the camera always work whatever is bound. Buttons are
    /// positions (Xbox names: the bottom face button is A).
    /// </summary>
    public static class PadBindings
    {
        public enum Action { Turn, Tip, Roll, ShelfUp, ShelfDown, Undo, Hint, XRay, Close }

        public static readonly Action[] All = (Action[])Enum.GetValues(typeof(Action));

        /// <summary>The buttons an action can be given.</summary>
        public static readonly GamepadButton[] Bindable =
        {
            GamepadButton.West, GamepadButton.North, GamepadButton.RightShoulder, GamepadButton.Select,
            GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight,
            GamepadButton.LeftStick, GamepadButton.RightStick,
        };

        public static event System.Action Changed;

        static readonly GamepadButton[] Defaults =
        {
            GamepadButton.West, GamepadButton.North, GamepadButton.RightShoulder, GamepadButton.DpadUp,
            GamepadButton.DpadDown, GamepadButton.Select, GamepadButton.DpadLeft, GamepadButton.LeftStick,
            GamepadButton.DpadRight,
        };

        public static string Name(Action action)
        {
            switch (action)
            {
                case Action.Turn: return "Turn";
                case Action.Tip: return "Tip over";
                case Action.Roll: return "Roll sideways";
                case Action.ShelfUp: return "Shelf up";
                case Action.ShelfDown: return "Shelf down";
                case Action.Undo: return "Undo";
                case Action.Hint: return "Ask Grandpa";
                case Action.XRay: return GameSettings.XRayToggle ? "X-ray (press)" : "X-ray (hold)";
                default: return "Close the trunk";
            }
        }

        static string PrefKey(Action action) => "ptt.pad." + action.ToString().ToLowerInvariant();

        // Read every packing frame, so cached (the save is only read when buttons change).
        static GamepadButton[] cache;

        public static GamepadButton ButtonFor(Action action)
        {
            if (cache == null) Reload();
            return cache[(int)action];
        }

        static void Reload()
        {
            cache = new GamepadButton[All.Length];
            foreach (var action in All)
            {
                int stored = Prefs.GetInt(PrefKey(action), -1);
                cache[(int)action] = stored >= 0 && Array.IndexOf(Bindable, (GamepadButton)stored) >= 0 ? (GamepadButton)stored : Defaults[(int)action];
            }
        }

        public static bool CanBind(GamepadButton button) => Array.IndexOf(Bindable, button) >= 0;

        /// <summary>Give an action a button; if another action had it, that action takes this one's old button.</summary>
        public static void Bind(Action action, GamepadButton button)
        {
            if (!CanBind(button)) return;
            var old = ButtonFor(action);
            if (old == button) return;
            foreach (var other in All)
                if (other != action && ButtonFor(other) == button)
                    Prefs.SetInt(PrefKey(other), (int)old);
            Prefs.SetInt(PrefKey(action), (int)button);
            Prefs.Save();
            Reload();
            Changed?.Invoke();
        }

        public static void ResetAll()
        {
            foreach (var action in All) Prefs.DeleteKey(PrefKey(action));
            Prefs.Save();
            Reload();
            Changed?.Invoke();
        }

        static ButtonControl Control(Gamepad pad, Action action) => pad[ButtonFor(action)];

        public static bool Pressed(Action action)
        {
            var pad = Gamepad.current;
            return pad != null && Control(pad, action).wasPressedThisFrame;
        }

        public static bool Held(Action action)
        {
            var pad = Gamepad.current;
            return pad != null && Control(pad, action).isPressed;
        }

        /// <summary>The bindable button pressed this frame on the current pad, if any (for rebinding).</summary>
        public static GamepadButton? PressedBindable()
        {
            var pad = Gamepad.current;
            if (pad == null) return null;
            foreach (var b in Bindable)
                if (pad[b].wasPressedThisFrame) return b;
            return null;
        }

        /// <summary>The button's name in caps, Xbox style ("X", "RB", "D-PAD LEFT", "L3").</summary>
        public static string Label(Action action) => Label(ButtonFor(action));

        /// <summary>The shelf buttons together: "D-PAD UP / DOWN", or both names when they've been moved.</summary>
        public static string ShelfLabel()
        {
            if (ButtonFor(Action.ShelfUp) == GamepadButton.DpadUp && ButtonFor(Action.ShelfDown) == GamepadButton.DpadDown) return "D-PAD UP / DOWN";
            return Label(Action.ShelfUp) + " / " + Label(Action.ShelfDown);
        }

        /// <summary>A short form for the key-hint strip ("D-PAD &lt;" rather than "D-PAD LEFT").</summary>
        public static string Short(Action action)
        {
            switch (ButtonFor(action))
            {
                case GamepadButton.DpadUp: return "D-PAD ^";
                case GamepadButton.DpadDown: return "D-PAD v";
                case GamepadButton.DpadLeft: return "D-PAD <";
                case GamepadButton.DpadRight: return "D-PAD >";
                default: return Label(action);
            }
        }

        public static string Label(GamepadButton button)
        {
            switch (button)
            {
                case GamepadButton.West: return "X";
                case GamepadButton.North: return "Y";
                case GamepadButton.South: return "A";
                case GamepadButton.East: return "B";
                case GamepadButton.LeftShoulder: return "LB";
                case GamepadButton.RightShoulder: return "RB";
                case GamepadButton.Select: return "VIEW";
                case GamepadButton.Start: return "MENU";
                case GamepadButton.DpadUp: return "D-PAD UP";
                case GamepadButton.DpadDown: return "D-PAD DOWN";
                case GamepadButton.DpadLeft: return "D-PAD LEFT";
                case GamepadButton.DpadRight: return "D-PAD RIGHT";
                case GamepadButton.LeftStick: return "L3";
                case GamepadButton.RightStick: return "R3";
                default: return button.ToString().ToUpperInvariant();
            }
        }
    }
}
