using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PackTheTrunk
{
    public partial class AutoPilot
    {
        /// <summary>Point the gamepad cursor at a button and press A, as a player would.</summary>
        IEnumerator PadClick(Gamepad pad, string name)
        {
            var button = FindButton(name);
            if (button == null)
            {
                Check(false, "button " + name + " is on screen");
                yield break;
            }
            var rt = (RectTransform)button.transform;
            yield return PadStickTo(pad, rt.TransformPoint(rt.rect.center));
            yield return PadPress(pad, GamepadButton.South);
            yield return Wait(0.3f);
        }

        IEnumerator PadOpenControls(Gamepad pad)
        {
            yield return PadPress(pad, GamepadButton.Start);
            yield return Wait(1f);
            yield return PadClick(pad, "Pause Settings");
            yield return Wait(0.8f);
            yield return PadClick(pad, "Tab CONTROLS");
            yield return Wait(0.6f);
        }

        IEnumerator PadCloseControlsAndResume(Gamepad pad)
        {
            yield return PadPress(pad, GamepadButton.East);
            yield return Wait(0.6f);
            yield return PadPress(pad, GamepadButton.East);
            yield return Wait(0.8f);
        }

        /// <summary>
        /// Gamepad button remapping through Settings → Controls with the pad alone: turn → R3, B cancels a
        /// rebind, R3 then turns (and X doesn't), the hint strip follows, binding R3 to undo swaps it with
        /// turn, and Defaults restores every button.
        /// </summary>
        IEnumerator PadRebindChecks(Gamepad pad)
        {
            PerfProbe.Begin("menus");
            game.AutoStartLevel(LevelIndex("weekend"));
            yield return Wait(2.5f);
            yield return PadOpenControls(pad);
            Check(Visible("Pad Bind Turn") && PadBindings.ButtonFor(PadBindings.Action.Turn) == GamepadButton.West,
                "pad remap: Settings → Controls lists the gamepad buttons (turn is X)");
            yield return PadClick(pad, "Pad Bind Turn");
            Check(game.Ui.IsRebinding, "pad remap: clicking an action (with A) waits for a button, and A itself isn't taken");
            yield return PadPress(pad, GamepadButton.East);
            yield return Wait(0.3f);
            Check(!game.Ui.IsRebinding && PadBindings.ButtonFor(PadBindings.Action.Turn) == GamepadButton.West && Visible("Settings Back"),
                "pad remap: B cancels the rebind and leaves Settings open");
            yield return PadClick(pad, "Pad Bind Turn");
            yield return PadPress(pad, GamepadButton.RightStick);
            yield return Wait(0.3f);
            Check(PadBindings.ButtonFor(PadBindings.Action.Turn) == GamepadButton.RightStick && !game.Ui.IsRebinding,
                "pad remap: pressing R3 binds turn to R3");
            yield return Shot("pad-controls-rebound");
            yield return PadCloseControlsAndResume(pad);

            var item = game.Items.First(i => i.State == ItemState.Pile && i.Def.Shape.Orientations().Count > 1);
            game.AutoHold(item);
            yield return Wait(0.3f);
            var before = item.Orientation;
            yield return PadPress(pad, GamepadButton.West);
            yield return Wait(0.3f);
            bool xIgnored = item.Orientation == before;
            yield return PadPress(pad, GamepadButton.RightStick);
            yield return Wait(0.3f);
            Check(game.IsPlaying && !game.IsPaused && xIgnored && item.Orientation != before, "pad remap: R3 turns the held item and X no longer does");
            var hintKeys = game.Ui.PadHintKeys();
            Check(game.Ui.PadHintsShown && hintKeys.Contains("R3") && !hintKeys.Contains("X"), "pad remap: the controller hints show R3 (" + string.Join(" ", hintKeys) + ")");
            yield return PadPress(pad, GamepadButton.East);
            yield return Wait(0.4f);

            yield return PadOpenControls(pad);
            yield return PadClick(pad, "Pad Bind Undo");
            yield return PadPress(pad, GamepadButton.RightStick);
            yield return Wait(0.3f);
            Check(PadBindings.ButtonFor(PadBindings.Action.Undo) == GamepadButton.RightStick && PadBindings.ButtonFor(PadBindings.Action.Turn) == GamepadButton.Select,
                "pad remap: binding R3 to undo swaps it with turn (turn is now VIEW)");
            yield return PadClick(pad, "Settings Defaults");
            yield return Wait(0.6f);
            yield return PadClick(pad, "Confirm Yes");
            yield return Wait(0.8f);
            Uncap();
            Check(PadBindings.All.All(a => PadBindings.ButtonFor(a) == DefaultPadButton(a)) && game.Ui.PadHintKeys().Contains("X"),
                "pad remap: Defaults restores every button (and the hints)");
            yield return PadCloseControlsAndResume(pad);
        }

        static GamepadButton DefaultPadButton(PadBindings.Action action)
        {
            switch (action)
            {
                case PadBindings.Action.Turn: return GamepadButton.West;
                case PadBindings.Action.Tip: return GamepadButton.North;
                case PadBindings.Action.Roll: return GamepadButton.RightShoulder;
                case PadBindings.Action.ShelfUp: return GamepadButton.DpadUp;
                case PadBindings.Action.ShelfDown: return GamepadButton.DpadDown;
                case PadBindings.Action.Undo: return GamepadButton.Select;
                case PadBindings.Action.Hint: return GamepadButton.DpadLeft;
                case PadBindings.Action.XRay: return GamepadButton.LeftStick;
                default: return GamepadButton.DpadRight;
            }
        }
    }
}
