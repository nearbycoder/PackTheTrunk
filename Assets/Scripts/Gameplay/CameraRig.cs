using UnityEngine;
using UnityEngine.InputSystem;

namespace PackTheTrunk
{
    /// <summary>Orbit camera: right/middle-drag or Q/E to orbit, wheel to zoom when the wheel is free.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        public bool ZoomEnabled { get; set; } = true;
        public bool IsDragging { get; private set; }

        /// <summary>The right stick is swinging the camera this frame.</summary>
        public bool IsPadOrbiting { get; private set; }
        public float Distance => distance;
        /// <summary>Slow cinematic drift for the title screen.</summary>
        public bool Attract { get; set; }
        /// <summary>Player camera controls (off in menus and cutscenes).</summary>
        public bool InputEnabled { get; set; } = true;

        Vector3 target;
        float yaw, pitch = 52f, distance = 16f;
        float targetYaw, targetPitch = 52f, targetDistance = 16f;
        float minDistance = 6f, maxDistance = 40f;
        Vector2 dragStart;
        bool dragArmed;
        float shakeTime, shakeDuration, shakeStrength;
        float glideTime, glideRate = 10f;
        float attractYaw, attractPitch;

        void Awake()
        {
            Camera = GetComponent<Camera>();
        }

        /// <summary>
        /// Fit the bounds into the left <paramref name="usableWidth"/> fraction of the screen
        /// (the rest is covered by UI panels) by projecting its corners and solving for distance.
        /// <paramref name="regionBottom"/> keeps that much of the screen's height free below it.
        /// </summary>
        public void Frame(Bounds bounds, bool instant, float usableWidth = 0.75f, float regionLeft = 0.03f, float yaw = -6f, float pitchAngle = 54f,
                          float regionBottom = 0.08f)
        {
            targetYaw = yaw;
            targetPitch = pitchAngle;
            attractYaw = yaw;
            attractPitch = pitchAngle;
            var rotation = Quaternion.Euler(targetPitch, targetYaw, 0f);
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));

            // Screen region the content should occupy (viewport units).
            var region = new Rect(regionLeft, regionBottom, usableWidth - 0.05f, 0.86f - regionBottom);
            var look = bounds.center;
            float dist = 20f;
            for (int pass = 0; pass < 3; pass++)
            {
                float lo = 2f, hi = 400f;
                for (int i = 0; i < 40; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    var r = Projected(corners, look, rotation, mid);
                    if (r.width <= region.width && r.height <= region.height) hi = mid; else lo = mid;
                }
                dist = hi;

                // Re-centre: move the look point so the projected box sits in the middle of the region.
                var box = Projected(corners, look, rotation, dist);
                float tanV = Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float viewH = 2f * dist * tanV;
                float viewW = viewH * Camera.aspect;
                var dx = box.center.x - region.center.x;
                var dy = box.center.y - region.center.y;
                look += rotation * Vector3.right * (dx * viewW) + rotation * Vector3.up * (dy * viewH);
            }

            target = look;
            targetDistance = dist;
            minDistance = dist * 0.45f;
            maxDistance = dist * 1.8f;
            if (instant)
            {
                yaw = targetYaw;
                pitch = targetPitch;
                distance = targetDistance;
                Apply();
            }
        }

        Rect Projected(Vector3[] points, Vector3 look, Quaternion rotation, float dist)
        {
            transform.SetPositionAndRotation(look - rotation * Vector3.forward * dist, rotation);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in points)
            {
                var v = Camera.WorldToViewportPoint(p);
                if (v.z <= 0.01f) return new Rect(0, 0, 99, 99);
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// <summary>Unit vector along the world axis that best matches the camera's right.</summary>
        public Vector3 SnappedRight() => Snap(transform.right);

        public Vector3 SnappedForward()
        {
            var f = transform.forward;
            f.y = 0f;
            return Snap(f);
        }

        static Vector3 Snap(Vector3 v)
        {
            v.y = 0f;
            return Mathf.Abs(v.x) >= Mathf.Abs(v.z) ? new Vector3(Mathf.Sign(v.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(v.z));
        }

        /// <summary>Start the camera off to one side and let it drift slowly into place.</summary>
        public void SweepIn(float yawOffset, float pitchOffset, float distanceScale, float seconds, float rate = 1.4f)
        {
            // Reduce motion: no sweep, the camera is simply there.
            if (GameSettings.ReduceMotion) { yawOffset = pitchOffset = 0f; distanceScale = 1f; seconds = 0f; }
            yaw = targetYaw + yawOffset;
            pitch = Mathf.Clamp(targetPitch + pitchOffset, 15f, 85f);
            distance = targetDistance * distanceScale;
            Glide(seconds, rate);
            Apply();
        }

        /// <summary>Use a slower, softer follow for a while (for cinematic moves).</summary>
        public void Glide(float seconds, float rate = 1.6f)
        {
            glideTime = seconds;
            glideRate = rate;
        }

        void LateUpdate()
        {
            var mouse = InputEnabled ? Mouse.current : null;
            var keyboard = InputEnabled ? Keyboard.current : null;
            float speed = GameSettings.OrbitSpeed;

            if (mouse != null)
            {
                bool orbitButton = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
                if (mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                {
                    dragStart = mouse.position.ReadValue();
                    dragArmed = true;
                    IsDragging = false;
                }
                if (orbitButton && dragArmed)
                {
                    if (!IsDragging && (mouse.position.ReadValue() - dragStart).magnitude > 6f) IsDragging = true;
                    if (IsDragging)
                    {
                        var delta = mouse.delta.ReadValue();
                        targetYaw += delta.x * 0.25f * speed;
                        targetPitch = Mathf.Clamp(targetPitch - delta.y * 0.2f * speed * (GameSettings.InvertOrbit ? -1f : 1f), 20f, 85f);
                    }
                }
                if (!orbitButton) dragArmed = false;

                if (ZoomEnabled)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                        targetDistance = Mathf.Clamp(targetDistance * (scroll > 0 ? 0.9f : 1.1f), minDistance, maxDistance);
                }
            }

            if (keyboard != null)
            {
                if (Bindings.Held(Bindings.Action.LookLeft)) targetYaw += 90f * speed * UiTime.Delta;
                if (Bindings.Held(Bindings.Action.LookRight)) targetYaw -= 90f * speed * UiTime.Delta;
            }
            var pad = InputEnabled ? Gamepad.current : null;
            if (pad != null)
            {
                // Right stick looks around; the triggers zoom while your hands are empty.
                var look = pad.rightStick.ReadValue();
                if (look.magnitude > 0.15f)
                {
                    targetYaw -= look.x * 110f * speed * UiTime.Delta;
                    targetPitch = Mathf.Clamp(targetPitch - look.y * 70f * speed * UiTime.Delta * (GameSettings.InvertOrbit ? -1f : 1f), 20f, 85f);
                    IsPadOrbiting = true;
                }
                else IsPadOrbiting = false;
                float zoom = pad.leftTrigger.ReadValue() - pad.rightTrigger.ReadValue();
                if (ZoomEnabled && Mathf.Abs(zoom) > 0.1f)
                    targetDistance = Mathf.Clamp(targetDistance * (1f + zoom * 1.4f * UiTime.Delta), minDistance, maxDistance);
            }
            if (Attract && !GameSettings.ReduceMotion)
            {
                float t = UiTime.Now;
                targetYaw = attractYaw + Mathf.Sin(t * 0.11f) * 22f;
                targetPitch = attractPitch + Mathf.Sin(t * 0.083f + 1f) * 4f;
            }
            targetYaw = Mathf.Clamp(targetYaw, -100f, 100f);

            float rate = 10f;
            if (Attract) rate = 1.2f;
            if (glideTime > 0f)
            {
                glideTime -= UiTime.Delta;
                rate = Mathf.Lerp(rate, glideRate, Mathf.Clamp01(glideTime / 0.6f));
            }
            float k = 1f - Mathf.Exp(-rate * UiTime.Delta);
            yaw = Mathf.Lerp(yaw, targetYaw, k);
            pitch = Mathf.Lerp(pitch, targetPitch, k);
            distance = Mathf.Lerp(distance, targetDistance, k);
            Apply();
        }

        /// <summary>A two-finger drag on a touch screen, in screen pixels (y up): orbits as a right-drag does.</summary>
        public void Orbit(Vector2 delta)
        {
            if (!InputEnabled || Attract) return;
            float speed = GameSettings.OrbitSpeed;
            targetYaw += delta.x * 0.25f * speed;
            targetPitch = Mathf.Clamp(targetPitch - delta.y * 0.2f * speed * (GameSettings.InvertOrbit ? -1f : 1f), 20f, 85f);
        }

        /// <summary>A pinch on a touch screen: how much the fingers spread (above 1 comes closer). Works with something in hand too.</summary>
        public void Pinch(float spread)
        {
            if (!InputEnabled || Attract || spread <= 0f) return;
            targetDistance = Mathf.Clamp(targetDistance / spread, minDistance, maxDistance);
        }

        /// <summary>The camera's yaw in degrees (for the page's test tools).</summary>
        public float Yaw => yaw;

        /// <summary>True on the frame a right-click is released without having dragged.</summary>
        public bool RightClickReleased()
        {
            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.wasReleasedThisFrame && !IsDragging;
        }

        /// <summary>A screen shake is playing (for the self-test).</summary>
        public bool IsShaking => shakeTime > 0f;

        /// <summary>The camera has reached where it's heading (for the self-test).</summary>
        public bool Settled => Mathf.Abs(yaw - targetYaw) < 0.5f && Mathf.Abs(pitch - targetPitch) < 0.5f && Mathf.Abs(distance - targetDistance) < 0.05f;

        public void Shake(float strength, float duration)
        {
            if (!GameSettings.ScreenShake || GameSettings.ReduceMotion) return;
            shakeStrength = strength;
            shakeDuration = shakeTime = duration;
        }

        void Apply()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var shake = Vector3.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= UiTime.Delta;
                shake = Random.insideUnitSphere * shakeStrength * Mathf.Clamp01(shakeTime / shakeDuration);
            }
            transform.position = target - rotation * Vector3.forward * distance + shake;
            transform.rotation = rotation;
        }
    }
}
