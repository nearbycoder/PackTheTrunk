using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace PackTheTrunk
{
    /// <summary>
    /// Gamepad buttons, for the places that also read the keyboard. Layout (Xbox names; the same
    /// positions on other pads): A click, B back / put back, X turn, Y tip, RB roll, LB held turns the
    /// other way, D-pad up/down shelf, D-pad left hint, D-pad right close the trunk, View undo,
    /// Menu pause (and "next" on story texts and postcards), right stick look around, triggers zoom.
    /// </summary>
    public static class Pad
    {
        public static Gamepad Current => Gamepad.current;

        public static bool Down(Func<Gamepad, ButtonControl> button)
        {
            var pad = Gamepad.current;
            return pad != null && button(pad).wasPressedThisFrame;
        }

        public static bool Held(Func<Gamepad, ButtonControl> button)
        {
            var pad = Gamepad.current;
            return pad != null && button(pad).isPressed;
        }

        /// <summary>B: back out, like Escape.</summary>
        public static bool Back => Down(p => p.buttonEast);

        /// <summary>Menu / Start: pause while packing, carry on through story texts and postcards.</summary>
        public static bool Start => Down(p => p.startButton);

        public static bool AnyButton =>
            Down(p => p.buttonSouth) || Down(p => p.buttonEast) || Down(p => p.buttonWest) || Down(p => p.buttonNorth) || Down(p => p.startButton);
    }

    /// <summary>
    /// On a control that has a value (a settings slider, switch or choice), D-pad left / right changes
    /// the value instead of moving the cursor away. Step gets -1 or +1.
    /// </summary>
    public class PadStep : MonoBehaviour
    {
        public Action<int> Step;
    }

    /// <summary>
    /// A direction that fires on the press and then repeats while it's held (D-pad and arrow-key
    /// navigation, keyboard aiming).
    /// </summary>
    public class StepRepeat
    {
        const float RepeatDelay = 0.42f, RepeatInterval = 0.12f;
        Vector2Int heldDirection;
        float repeatAt;

        public Vector2Int Next(Vector2Int d)
        {
            if (d == Vector2Int.zero)
            {
                heldDirection = d;
                return d;
            }
            float now = Time.unscaledTime;
            if (d != heldDirection)
            {
                heldDirection = d;
                repeatAt = now + RepeatDelay;
                return d;
            }
            if (now < repeatAt) return Vector2Int.zero;
            repeatAt = now + RepeatInterval;
            return d;
        }

        /// <summary>The arrow keys held right now (one axis at a time, left / right first).</summary>
        public static Vector2Int Arrows(Keyboard kb)
        {
            var d = Vector2Int.zero;
            if (kb == null) return d;
            d.x = (kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0);
            if (d.x == 0) d.y = (kb.upArrowKey.isPressed ? 1 : 0) - (kb.downArrowKey.isPressed ? 1 : 0);
            return d;
        }

        public static bool AnyArrowPressed(Keyboard kb) =>
            kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame);
    }

    /// <summary>
    /// Lets a gamepad, or the keyboard alone, point and click. The left stick moves a drawn cursor and A
    /// clicks, through a virtual mouse, so the game's mouse picking and every uGUI button work unchanged.
    /// In menus the D-pad, or the arrow keys, jump the cursor to the nearest button in that direction
    /// (holding repeats), and with the keyboard Enter clicks what it's on. The virtual mouse only appears
    /// once a pad or an arrow key is used; moving the real mouse hands control straight back (and shows
    /// the system cursor again), and so does typing after using the pad.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class GamepadCursor : MonoBehaviour
    {
        enum Source { None, Pad, Keys }

        static Source source;
        static GamepadCursor instance;

        /// <summary>The gamepad was the last thing used (drives the cursor and the controller key hints).</summary>
        public static bool Active => source == Source.Pad;

        /// <summary>The arrow keys are pointing (menus, the packing list) or aiming (holding something); the key hints stay keyboard keys.</summary>
        public static bool KeysActive => source == Source.Keys;

        public static event Action ActiveChanged;

        /// <summary>The arrow keys started or stopped pointing (the keyboard hints say how to use them).</summary>
        public static event Action KeysActiveChanged;

        /// <summary>The drawn cursor's screen position.</summary>
        public static Vector2 Position { get; private set; }

        /// <summary>Set by the game: whether the D-pad moves between buttons right now (not while packing, where it has its own jobs).</summary>
        public static Func<bool> MenuNavigation;

        /// <summary>Set by the game: whether the arrow keys move between buttons right now (menus, and packing with empty hands).</summary>
        public static Func<bool> KeyNavigation;

        /// <summary>The last D-pad or arrow-key jump, for the log and the self-test: the control it landed on, or what it stepped.</summary>
        public static string LastNavigation { get; private set; } = "";

        /// <summary>
        /// Enter clicked the control under the keyboard cursor this frame, so the screen-wide Enter jobs
        /// (next trip, start packing, close the trunk) stand aside.
        /// </summary>
        public static bool KeyClickedThisFrame => keyClickFrame == Time.frameCount;

        static int keyClickFrame = -1;

        /// <summary>Hand pointing to the keyboard (keyboard aiming starts this, so the mouse can take it back).</summary>
        public static void UseKeys()
        {
            if (instance != null && source != Source.Keys) instance.SetSource(Source.Keys);
        }

        Mouse virtualMouse;
        RectTransform cursor, canvasRect;
        Canvas canvas;
        Vector2 position;
        bool aDown, keyButton;
        Vector2? glide;
        readonly StepRepeat dpadRepeat = new StepRepeat(), keyRepeat = new StepRepeat();
        readonly List<RaycastResult> hits = new List<RaycastResult>();

        void Awake()
        {
            var go = new GameObject("Gamepad Cursor", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasRect = (RectTransform)go.transform;

            cursor = UiKit.Rect("Cursor", canvasRect);
            cursor.sizeDelta = new Vector2(46, 46);
            var ring = UiKit.Image("Ring", cursor, UiTheme.Ink, false);
            ring.sprite = UiTheme.Circle;
            ring.rectTransform.Fill();
            ring.raycastTarget = false;
            var fill = UiKit.Image("Fill", cursor, Color.white, false);
            fill.sprite = UiTheme.Circle;
            fill.rectTransform.Fill(5);
            fill.raycastTarget = false;
            var dot = UiKit.Image("Dot", cursor, UiTheme.Accent, false);
            dot.sprite = UiTheme.Circle;
            dot.rectTransform.Fill(15);
            dot.raycastTarget = false;
            cursor.gameObject.SetActive(false);
            position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            instance = this;
        }

        void OnDestroy()
        {
            if (virtualMouse != null && virtualMouse.added) InputSystem.RemoveDevice(virtualMouse);
            if (source != Source.None) SetSource(Source.None);
            if (instance == this) instance = null;
        }

        void Update()
        {
            var pad = Gamepad.current;
            var kb = Keyboard.current;
            Vector2 stick = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
            if (stick.magnitude < 0.15f) stick = Vector2.zero;
            bool padA = pad != null && pad.buttonSouth.isPressed, a = padA;
            bool padUsed = pad != null && (stick != Vector2.zero || padA != aDown || pad.wasUpdatedThisFrame && AnyPadInput(pad));
            bool keyNav = KeyNavigation != null && KeyNavigation();
            bool arrowNav = keyNav && StepRepeat.AnyArrowPressed(kb);

            // Hand back to the mouse as soon as it's touched; typing after the pad hands back to the
            // keyboard (pointing with the arrows if that's what was pressed).
            if (source != Source.None && RealMouseMoved()) SetSource(Source.None);
            else if (source == Source.Pad && kb != null && kb.anyKey.wasPressedThisFrame) SetSource(arrowNav ? Source.Keys : Source.None);
            else if (source == Source.None && arrowNav) SetSource(Source.Keys);
            if (padUsed && source != Source.Pad) SetSource(Source.Pad);
            var dpad = dpadRepeat.Next(DpadDirection(pad));
            var arrows = keyRepeat.Next(source == Source.Keys && keyNav ? StepRepeat.Arrows(kb) : Vector2Int.zero);
            if (source == Source.None)
            {
                aDown = padA;
                return;
            }

            if (source == Source.Pad && dpad != Vector2Int.zero && MenuNavigation != null && MenuNavigation()) Navigate(dpad);
            if (source == Source.Keys)
            {
                if (arrows != Vector2Int.zero) Navigate(arrows);
                // The drawn cursor only shows while the arrows point (not while they aim something held).
                if (cursor.gameObject.activeSelf != keyNav) cursor.gameObject.SetActive(keyNav);
                // Enter clicks the control under the cursor (Space keeps its own jobs); let go next frame.
                bool click = kb != null && keyNav && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
                if (click && glide.HasValue) { position = glide.Value; glide = null; }
                if (click && SelectableAt(position) != null)
                {
                    keyButton = true;
                    keyClickFrame = Time.frameCount;
                }
                else keyButton = false;
                a = keyButton;
            }

            // Speed scales with the screen, eases in for fine aiming.
            float speed = Screen.height * 1.25f;
            if (stick != Vector2.zero) glide = null;
            if (glide.HasValue)
            {
                position = Vector2.Lerp(position, glide.Value, 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime));
                if ((position - glide.Value).sqrMagnitude < 1f) { position = glide.Value; glide = null; }
            }
            position += stick.normalized * Mathf.Pow(stick.magnitude, 1.6f) * speed * Time.unscaledDeltaTime;
            position = new Vector2(Mathf.Clamp(position.x, 0f, Screen.width - 1), Mathf.Clamp(position.y, 0f, Screen.height - 1));
            Position = position;

            if (virtualMouse == null || !virtualMouse.added)
                virtualMouse = InputSystem.AddDevice<Mouse>("PTT Gamepad Cursor");
            var state = new MouseState { position = position, delta = stick * speed * Time.unscaledDeltaTime };
            if (a) state = state.WithButton(MouseButton.Left, true);
            InputSystem.QueueStateEvent(virtualMouse, state);
            virtualMouse.MakeCurrent();
            aDown = padA;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, position, null, out var local);
            cursor.anchoredPosition = local;
            float press = a ? 0.82f : 1f;
            cursor.localScale = Vector3.Lerp(cursor.localScale, Vector3.one * press, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
        }

        /// <summary>The D-pad direction held right now.</summary>
        static Vector2Int DpadDirection(Gamepad pad)
        {
            var d = Vector2Int.zero;
            if (pad != null)
            {
                d.x = (pad.dpad.right.isPressed ? 1 : 0) - (pad.dpad.left.isPressed ? 1 : 0);
                if (d.x == 0) d.y = (pad.dpad.up.isPressed ? 1 : 0) - (pad.dpad.down.isPressed ? 1 : 0);
            }
            return d;
        }

        /// <summary>
        /// Jump to the nearest clickable control in that direction (on top, on screen), or step the value
        /// of the control under the cursor. Nothing under the cursor and nothing that way: the nearest one.
        /// </summary>
        void Navigate(Vector2Int step)
        {
            var from = glide ?? position;
            var current = SelectableAt(from);
            if (current != null && step.y == 0 && current.TryGetComponent<PadStep>(out var stepper) && stepper.Step != null)
            {
                stepper.Step(step.x);
                LastNavigation = "step " + current.name;
                return;
            }
            Vector2 dir = step;
            Selectable best = null, nearest = null;
            Vector2 bestCentre = default, nearestCentre = default;
            float bestScore = float.MaxValue, nearestDistance = float.MaxValue;
            foreach (var s in Selectable.allSelectablesArray)
            {
                if (s == current || !Reachable(s, out var centre, out var min, out var max)) continue;
                var d = centre - from;
                if (d.magnitude < nearestDistance) { nearestDistance = d.magnitude; nearest = s; nearestCentre = centre; }
                float along = Vector2.Dot(d, dir);
                if (along < 2f) continue;
                // Sideways distance counts from the control's edge, so a wide button straight below is "below".
                float across = step.x != 0 ? Mathf.Max(0f, min.y - from.y, from.y - max.y) : Mathf.Max(0f, min.x - from.x, from.x - max.x);
                float score = along + 2f * across;
                if (score < bestScore) { bestScore = score; best = s; bestCentre = centre; }
            }
            if (best == null && current == null) { best = nearest; bestCentre = nearestCentre; }
            if (best == null) return;
            glide = bestCentre;
            LastNavigation = best.name;
        }

        /// <summary>The control a click at this point would reach, if any.</summary>
        Selectable SelectableAt(Vector2 point)
        {
            var top = TopHit(point);
            return top != null ? top.GetComponentInParent<Selectable>() : null;
        }

        GameObject TopHit(Vector2 point)
        {
            var events = EventSystem.current;
            if (events == null) return null;
            hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = point }, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }

        /// <summary>Clickable, on screen, and nothing covering its middle.</summary>
        bool Reachable(Selectable s, out Vector2 centre, out Vector2 min, out Vector2 max)
        {
            centre = min = max = default;
            if (!s.isActiveAndEnabled || !s.IsInteractable() || s.transform is not RectTransform rect) return false;
            var canvas = s.GetComponentInParent<Canvas>();
            if (canvas == null) return false;
            var root = canvas.rootCanvas;
            var cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]), b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            min = Vector2.Min(a, b);
            max = Vector2.Max(a, b);
            centre = (min + max) * 0.5f;
            if (max.x - min.x < 4f || max.y - min.y < 4f) return false;
            if (centre.x < 0f || centre.y < 0f || centre.x >= Screen.width || centre.y >= Screen.height) return false;
            var top = TopHit(centre);
            return top != null && top.transform.IsChildOf(s.transform);
        }

        static bool AnyPadInput(Gamepad pad)
        {
            foreach (var c in pad.allControls)
                if (c is ButtonControl b && b.wasPressedThisFrame) return true;
            return pad.rightStick.ReadValue().magnitude > 0.3f;
        }

        bool RealMouseMoved()
        {
            foreach (var device in InputSystem.devices)
            {
                if (device is not Mouse m || m == virtualMouse) continue;
                if (m.delta.ReadValue().sqrMagnitude > 4f || m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame)
                {
                    m.MakeCurrent();
                    position = m.position.ReadValue();
                    return true;
                }
            }
            return false;
        }

        void SetSource(Source next)
        {
            bool wasPad = source == Source.Pad, wasKeys = source == Source.Keys;
            if (source == Source.None)
            {
                var real = Mouse.current;
                if (real != null && real != virtualMouse) position = real.position.ReadValue();
                Position = position;
            }
            source = next;
            glide = null;
            keyButton = false;
            if (cursor != null) cursor.gameObject.SetActive(next != Source.None);
            Cursor.visible = next == Source.None;
            if (wasPad != (next == Source.Pad)) ActiveChanged?.Invoke();
            if (wasKeys != (next == Source.Keys)) KeysActiveChanged?.Invoke();
        }
    }
}
