using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PackTheTrunk
{
    public static class Ease
    {
        public static float OutCubic(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }
        public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float InOutCubic(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f; }
        public static float OutQuint(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t * t * t; }

        public static float OutBack(float t, float overshoot = 1.5f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        public static float OutElastic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f || t >= 1f) return t;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
        }
    }

    public static class UiMotion
    {
        /// <summary>Animate <paramref name="step"/> from 0 to 1 over unscaled time.</summary>
        public static Coroutine Run(MonoBehaviour host, float duration, Action<float> step, float delay = 0f) =>
            host.StartCoroutine(Routine(duration, step, delay));

        /// <summary>Wait in unscaled game time (keeps step with frame-locked video capture, unlike real time).</summary>
        public static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += UiTime.Delta) yield return null;
        }

        static IEnumerator Routine(float duration, Action<float> step, float delay)
        {
            for (float d = 0f; d < delay; d += UiTime.Delta) yield return null;
            for (float t = 0f; t < duration; t += UiTime.Delta)
            {
                step(t / duration);
                yield return null;
            }
            step(1f);
        }

        /// <summary>Give an element an entrance animation that plays whenever it is shown.</summary>
        public static UiIntro Intro(Component c, Vector2 offset, float delay = 0f, float fromScale = 1f, float fromRotation = 0f, float duration = 0.55f, bool back = true)
        {
            var intro = c.gameObject.GetComponent<UiIntro>();
            if (intro == null) intro = c.gameObject.AddComponent<UiIntro>();
            intro.Offset = offset;
            intro.Delay = delay;
            intro.FromScale = fromScale;
            intro.FromRotation = fromRotation;
            intro.Duration = duration;
            intro.Back = back;
            if (intro.isActiveAndEnabled) intro.Replay();
            return intro;
        }
    }

    /// <summary>
    /// Entrance animation (slide, scale, tilt and fade with an overshoot) that replays each time
    /// the object is enabled. Works in unscaled time so it runs in the pause menu too.
    /// </summary>
    public class UiIntro : MonoBehaviour
    {
        public Vector2 Offset;
        public float Delay, FromScale = 1f, FromRotation, Duration = 0.55f;
        public bool Back = true;

        RectTransform rt;
        CanvasGroup group;
        Vector2 home;
        Quaternion homeRotation;
        Vector3 homeScale;
        bool captured;
        float t = -1f;

        void Capture()
        {
            if (captured) return;
            rt = (RectTransform)transform;
            home = rt.anchoredPosition;
            homeRotation = rt.localRotation;
            homeScale = rt.localScale;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            captured = true;
        }

        /// <summary>Call after moving the element so the intro lands at the new spot.</summary>
        public void Rehome()
        {
            captured = false;
            Capture();
        }

        void OnEnable()
        {
            Capture();
            t = -Delay;
            Pose(0f);
        }

        void OnDisable()
        {
            if (!captured) return;
            Pose(1f);
            t = -1f;
        }

        public void Replay()
        {
            Capture();
            t = -Delay;
            Pose(0f);
        }

        void Update()
        {
            if (t >= Duration || t < -Delay - 1f) return;
            t += UiTime.Delta;
            Pose(Mathf.Clamp01(t / Duration));
        }

        void Pose(float k)
        {
            if (GameSettings.ReduceMotion)
            {
                // Reduce motion: just a fade, in place.
                rt.anchoredPosition = home;
                rt.localScale = homeScale;
                rt.localRotation = homeRotation;
                group.alpha = Mathf.Clamp01(k * 2.5f);
                return;
            }
            float e = Back ? Ease.OutBack(k, 1.2f) : Ease.OutCubic(k);
            float lin = Ease.OutCubic(k);
            rt.anchoredPosition = home + Offset * (1f - e);
            rt.localScale = homeScale * Mathf.LerpUnclamped(FromScale, 1f, e);
            rt.localRotation = homeRotation * Quaternion.Euler(0f, 0f, FromRotation * (1f - lin));
            group.alpha = Mathf.Clamp01(k * 2.5f);
        }
    }

    /// <summary>Hover lift, press squash and UI sounds for anything clickable.</summary>
    public class PillHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public bool IsBack;
        public bool Silent;
        public float HoverScale = 1.06f;

        float target = 1f, scale = 1f, velocity;
        bool over, down;
        Selectable selectable;

        void Awake() => selectable = GetComponent<Selectable>();

        bool Interactable => selectable == null || selectable.IsInteractable();

        public void OnPointerEnter(PointerEventData e)
        {
            over = true;
            if (Interactable && !Silent) Sfx.Instance?.Hover();
        }

        public void OnPointerExit(PointerEventData e) { over = false; down = false; }
        public void OnPointerDown(PointerEventData e) => down = true;
        public void OnPointerUp(PointerEventData e) => down = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (!Interactable || Silent || e.button != PointerEventData.InputButton.Left) return;
            if (IsBack) Sfx.Instance?.Back(); else Sfx.Instance?.Click();
        }

        void OnDisable()
        {
            over = down = false;
            scale = target = 1f;
            velocity = 0f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            target = !Interactable ? 1f : down ? 0.94f : over ? HoverScale : 1f;
            // Critically-damped-ish spring: snappy with a hint of bounce.
            float dt = Mathf.Min(UiTime.Delta, 0.05f);
            velocity += (target - scale) * 520f * dt;
            velocity *= Mathf.Exp(-26f * dt);
            scale += velocity * dt;
            if (Mathf.Abs(transform.localScale.x - scale) > 0.0001f) transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    /// <summary>Slow idle bob and sway, for title letters and the like.</summary>
    public class Bob : MonoBehaviour
    {
        public float Phase, Amount = 6f, Speed = 1.4f, Tilt = 2f;
        Vector2 home;
        bool captured;
        RectTransform rt;

        void OnEnable()
        {
            rt = (RectTransform)transform;
            if (!captured) { home = rt.anchoredPosition; captured = true; }
        }

        void Update()
        {
            if (GameSettings.ReduceMotion)
            {
                rt.anchoredPosition = home;
                rt.localRotation = Quaternion.identity;
                return;
            }
            float s = Mathf.Sin(UiTime.Now * Speed + Phase);
            rt.anchoredPosition = home + new Vector2(0f, s * Amount);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(UiTime.Now * Speed * 0.7f + Phase * 1.3f) * Tilt);
        }
    }
}
