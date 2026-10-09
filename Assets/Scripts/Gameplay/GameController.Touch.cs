using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PackTheTrunk
{
    /// <summary>
    /// The browser's on-screen touch controls (Assets/WebGLTemplates/PackTheTrunk/index.html). The page turns a one-finger
    /// touch on the game into the mouse (press, move, release), so picking up, dragging into the trunk and every button
    /// work as they do with a mouse. While the touch controls show (Web.TouchActive):
    ///  - a held thing aims Web.TouchAimOffset pixels above the finger, so the finger doesn't hide where it will land;
    ///  - it drops when the finger lifts (after dragging it out of the pile, or after a touch that began with it already
    ///    in hand), not on the next press; a lift where it doesn't fit, or off the trunk, keeps it in hand, and the
    ///    page's put-back button returns it to the blanket;
    ///  - things only light up under a finger that's down (a resting pointer would leave one lit);
    ///  - the page's buttons arrive as verbs (WebBridge.Verb): turn, tip, roll, up / down (the resting heights, the mouse
    ///    wheel's job), back, undo, redo, hint, pause, xray-down / xray-up, and cancel (a second finger turned the touch
    ///    into an orbit, so its lift mustn't drop anything). They're queued and run in the frame loop, like keys.
    /// Two fingers orbit and pinch through WebBridge.Orbit and WebBridge.Pinch.
    /// </summary>
    public partial class GameController
    {
        readonly Queue<string> touchVerbs = new Queue<string>();
        bool touchXRayHeld, touchXRayPressed;
        bool touchPressHeld, touchCancelled;

        public void QueueTouchVerb(string verb)
        {
            if (touchVerbs.Count < 16) touchVerbs.Enqueue(verb);
        }

        public void TouchOrbit(Vector2 delta)
        {
            if (mode != Mode.Playing || paused) return;
            rig.Orbit(delta);
            tipOrbited = true;
        }

        public void TouchPinch(float spread)
        {
            if (mode == Mode.Playing && !paused) rig.Pinch(spread);
        }

        /// <summary>Grandpa's hint ghost is showing (for the page's test tools).</summary>
        public bool HintShowing => hintGhost != null && hintGhost.gameObject.activeSelf;

        /// <summary>X-ray is on (for the page's test tools).</summary>
        public bool XRayOn => xray;

        /// <summary>The held thing's orientation as Euler angles (for the page's test tools).</summary>
        public Vector3 HeldRotation => held != null ? held.Orientation.eulerAngles : Vector3.zero;

        /// <summary>Run the verbs the page's buttons queued since the last frame (before the paused and playing checks).</summary>
        void RunTouchVerbs()
        {
            touchXRayPressed = false;
            while (touchVerbs.Count > 0)
            {
                string verb = touchVerbs.Dequeue();
                switch (verb)
                {
                    case "xray-down": touchXRayHeld = touchXRayPressed = true; continue;
                    case "xray-up": touchXRayHeld = false; continue;
                    case "cancel": touchCancelled = true; continue;
                    case "pause":
                        if (mode == Mode.Playing && !ui.OverlayOpen)
                        {
                            if (paused) ResumeGame();
                            else PauseGame();
                        }
                        continue;
                }
                if (mode != Mode.Playing || paused || ui.OverlayOpen) continue;
                switch (verb)
                {
                    case "undo": Undo(); break;
                    case "redo": Redo(); break;
                    case "hint": AskGrandpa(); break;
                    case "turn": Rotate(Vector3.up, false); break;
                    case "tip": Rotate(rig.SnappedRight(), false); break;
                    case "roll": Rotate(rig.SnappedForward(), false); break;
                    case "up": if (held != null) { heightBias++; tipShelfPicked = true; } break;
                    case "down": if (held != null) { heightBias--; tipShelfPicked = true; } break;
                    case "back": PutBack(); break;
                }
            }
        }

        /// <summary>Where a held thing aims from: the pointer, or a fingertip above the finger with the touch controls on.</summary>
        Vector2 AimPoint(Mouse mouse)
        {
            var p = mouse.position.ReadValue();
            if (Web.TouchActive && held != null) p.y = Mathf.Min(p.y + Web.TouchAimOffset, Screen.height - 1f);
            return p;
        }

        /// <summary>Touch: what's in hand drops when the finger lifts, if it fits where it's aimed.</summary>
        void TouchDrop(Mouse mouse, bool overUi)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                // A touch that begins with something in hand aims it, and lifting the finger drops it (not on the HUD).
                touchPressHeld = !overUi;
                touchCancelled = false;
                dragArmed = false;
            }
            if (dragArmed && mouse.leftButton.isPressed)
            {
                float min = Mathf.Max(12f, Screen.height * 0.02f);
                if ((mouse.position.ReadValue() - dragFrom).sqrMagnitude > min * min) dragMoved = true;
            }
            if (!mouse.leftButton.wasReleasedThisFrame) return;
            // Picked up by this touch: only a drag drops it (a tap on the blanket just picks it up).
            bool drop = (touchPressHeld || (dragArmed && dragMoved)) && !touchCancelled && !overUi;
            touchPressHeld = dragArmed = false;
            touchCancelled = false;
            if (!drop) return;
            if (hasTarget && targetValid) PlaceHeld();
            else if (hasTarget) RefusePlacement();
        }

        /// <summary>The name of a control in the words of whatever is being used: a touch button, a pad button or a key.</summary>
        static string ControlName(string touch, PadBindings.Action pad, Bindings.Action key) =>
            Web.TouchActive ? touch : GamepadCursor.Active ? PadBindings.Label(pad) : Bindings.Label(key);
    }
}
