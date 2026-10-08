using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PackTheTrunk
{
    /// <summary>
    /// Remappable keyboard controls. Each action has one key the player can change (Settings →
    /// Controls) plus a few fixed alternates (arrows for the shelf, Backspace for undo, Enter to
    /// close) that drop out when another action is bound to them. Escape, Shift (turn the other
    /// way), the mouse and the gamepad aren't remappable. Keys are physical positions, so the
    /// labels come from the active keyboard layout.
    /// </summary>
    public static class Bindings
    {
        public enum Action { Turn, Tip, Roll, ShelfUp, ShelfDown, Undo, Hint, XRay, LookLeft, LookRight, Close, Music }

        public static readonly Action[] All = (Action[])Enum.GetValues(typeof(Action));

        public static event System.Action Changed;

        static readonly Dictionary<Action, Key> Defaults = new Dictionary<Action, Key>
        {
            { Action.Turn, Key.R }, { Action.Tip, Key.T }, { Action.Roll, Key.F },
            { Action.ShelfUp, Key.W }, { Action.ShelfDown, Key.S }, { Action.Undo, Key.Z },
            { Action.Hint, Key.H }, { Action.XRay, Key.Tab }, { Action.LookLeft, Key.Q },
            { Action.LookRight, Key.E }, { Action.Close, Key.Space }, { Action.Music, Key.M },
        };

        // Indexed by action (an array, not an enum-keyed dictionary: no boxing in the per-frame path).
        static readonly Key[][] Alternates = BuildAlternates();

        static Key[][] BuildAlternates()
        {
            var alts = new Key[Enum.GetValues(typeof(Action)).Length][];
            for (int i = 0; i < alts.Length; i++) alts[i] = Array.Empty<Key>();
            alts[(int)Action.ShelfUp] = new[] { Key.UpArrow };
            alts[(int)Action.ShelfDown] = new[] { Key.DownArrow };
            alts[(int)Action.Undo] = new[] { Key.Backspace };
            alts[(int)Action.Close] = new[] { Key.Enter, Key.NumpadEnter };
            return alts;
        }

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
                case Action.LookLeft: return "Look left";
                case Action.LookRight: return "Look right";
                case Action.Close: return "Close the trunk";
                default: return "Music on / off";
            }
        }

        static string PrefKey(Action action) => "ptt.key." + action.ToString().ToLowerInvariant();

        // Read every frame by the packing loop, so cached (the save is only read when keys change).
        static Key[] cache;

        public static Key KeyFor(Action action)
        {
            if (cache == null) Reload();
            return cache[(int)action];
        }

        static void Reload()
        {
            cache = new Key[All.Length];
            foreach (var action in All)
            {
                int stored = Prefs.GetInt(PrefKey(action), -1);
                cache[(int)action] = stored > 0 && Enum.IsDefined(typeof(Key), stored) ? (Key)stored : Defaults[action];
            }
        }

        /// <summary>Keys that can't be bound: Escape stays "back", Shift stays "the other way".</summary>
        public static bool CanBind(Key key) =>
            key != Key.None && key != Key.Escape &&
            key != Key.LeftShift && key != Key.RightShift &&
            key != Key.LeftCtrl && key != Key.RightCtrl &&
            key != Key.LeftAlt && key != Key.RightAlt &&
            key != Key.LeftMeta && key != Key.RightMeta &&
            key != Key.ContextMenu;

        /// <summary>Bind a key; if another action had it, that action takes this one's old key.</summary>
        public static void Bind(Action action, Key key)
        {
            if (!CanBind(key)) return;
            var old = KeyFor(action);
            if (old == key) return;
            foreach (var other in All)
                if (other != action && KeyFor(other) == key)
                    Prefs.SetInt(PrefKey(other), (int)old);
            Prefs.SetInt(PrefKey(action), (int)key);
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

        /// <summary>Some action has this key as its own (not just as a spare), so it keeps that job.</summary>
        public static bool IsBound(Key key)
        {
            foreach (var action in All)
                if (KeyFor(action) == key) return true;
            return false;
        }

        static bool BoundElsewhere(Key key, Action except)
        {
            foreach (var other in All)
                if (other != except && KeyFor(other) == key) return true;
            return false;
        }

        static KeyControl Control(Keyboard kb, Key key) => key == Key.None ? null : kb[key];

        public static bool Pressed(Action action)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (Control(kb, KeyFor(action))?.wasPressedThisFrame == true) return true;
            var extras = Alternates[(int)action];
            for (int i = 0; i < extras.Length; i++)
                    if (!BoundElsewhere(extras[i], action) && kb[extras[i]].wasPressedThisFrame) return true;
            return false;
        }

        public static bool Held(Action action)
        {
            var kb = Keyboard.current;
            return kb != null && Control(kb, KeyFor(action))?.isPressed == true;
        }

        /// <summary>The key's name on the player's keyboard layout, in caps ("R", "SPACE", "TAB").</summary>
        public static string Label(Action action) => Label(KeyFor(action));

        public static string Label(Key key)
        {
            switch (key)
            {
                case Key.Space: return "SPACE";
                case Key.Tab: return "TAB";
                case Key.Enter: return "ENTER";
                case Key.Backspace: return "BACKSPACE";
                case Key.UpArrow: return "UP";
                case Key.DownArrow: return "DOWN";
                case Key.LeftArrow: return "LEFT";
                case Key.RightArrow: return "RIGHT";
            }
            var kb = Keyboard.current;
            string name = kb != null ? kb[key].displayName : null;
            if (string.IsNullOrWhiteSpace(name)) name = key.ToString();
            return name.ToUpperInvariant();
        }
    }
}
