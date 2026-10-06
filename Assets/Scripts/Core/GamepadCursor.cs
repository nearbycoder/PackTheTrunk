using System;
using UnityEngine;
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
    /// Lets a gamepad point and click: the left stick moves a drawn cursor and A clicks, through a
    /// virtual mouse, so the game's mouse picking and every uGUI button work unchanged. The virtual
    /// mouse only appears once a gamepad is actually used; moving the real mouse or typing hands
    /// control straight back (and shows the system cursor again).
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class GamepadCursor : MonoBehaviour
    {
        /// <summary>The gamepad was the last thing used (drives the cursor and the controller key hints).</summary>
        public static bool Active { get; private set; }

        public static event Action ActiveChanged;

        /// <summary>The drawn cursor's screen position.</summary>
        public static Vector2 Position { get; private set; }

        Mouse virtualMouse;
        RectTransform cursor, canvasRect;
        Canvas canvas;
        Vector2 position;
        bool aDown;

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
            scaler.matchWidthOrHeight = 0.5f;
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
        }

        void OnDestroy()
        {
            if (virtualMouse != null && virtualMouse.added) InputSystem.RemoveDevice(virtualMouse);
            if (Active) SetActive(false);
        }

        void Update()
        {
            var pad = Gamepad.current;
            Vector2 stick = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
            if (stick.magnitude < 0.15f) stick = Vector2.zero;
            bool a = pad != null && pad.buttonSouth.isPressed;
            bool padUsed = pad != null && (stick != Vector2.zero || a != aDown || pad.wasUpdatedThisFrame && AnyPadInput(pad));

            // Hand back to the mouse / keyboard as soon as they're touched.
            if (Active && (RealMouseMoved() || (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)))
                SetActive(false);
            if (padUsed && !Active) SetActive(true);
            if (!Active)
            {
                aDown = a;
                return;
            }

            // Speed scales with the screen, eases in for fine aiming.
            float speed = Screen.height * 1.25f;
            position += stick.normalized * Mathf.Pow(stick.magnitude, 1.6f) * speed * Time.unscaledDeltaTime;
            position = new Vector2(Mathf.Clamp(position.x, 0f, Screen.width - 1), Mathf.Clamp(position.y, 0f, Screen.height - 1));
            Position = position;

            if (virtualMouse == null || !virtualMouse.added)
                virtualMouse = InputSystem.AddDevice<Mouse>("PTT Gamepad Cursor");
            var state = new MouseState { position = position, delta = stick * speed * Time.unscaledDeltaTime };
            if (a) state = state.WithButton(MouseButton.Left, true);
            InputSystem.QueueStateEvent(virtualMouse, state);
            virtualMouse.MakeCurrent();
            aDown = a;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, position, null, out var local);
            cursor.anchoredPosition = local;
            float press = a ? 0.82f : 1f;
            cursor.localScale = Vector3.Lerp(cursor.localScale, Vector3.one * press, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
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

        void SetActive(bool active)
        {
            Active = active;
            if (cursor != null) cursor.gameObject.SetActive(active);
            Cursor.visible = !active;
            if (active)
            {
                var real = Mouse.current;
                if (real != null && real != virtualMouse) position = real.position.ReadValue();
                Position = position;
            }
            ActiveChanged?.Invoke();
        }
    }
}
