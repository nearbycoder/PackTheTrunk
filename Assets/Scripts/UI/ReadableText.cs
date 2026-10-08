using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PackTheTrunk
{
    /// <summary>
    /// Keeps every screen-space label at 12 screen pixels or more (Valve's recommended minimum on a Steam
    /// Deck). The HUD and menus are laid out in 1920x1080 units, so on a small window (1024x768) their
    /// smallest labels would come out at 10-11 px; those grow just enough, and nothing else changes. At
    /// 1280x720 and up every label is already big enough, so this does nothing there. Each label remembers
    /// the size it was given (and any size code gives it later), so a bigger window puts it back.
    /// </summary>
    public static class ReadableText
    {
        public const float MinPixels = 12f;

        sealed class Entry
        {
            public Text Text;
            public int Base, Applied;
            public float MinPixels = ReadableText.MinPixels;
        }

        static readonly List<Entry> entries = new List<Entry>();
        static float lastFactor = -1f;

        public static void Register(Text text)
        {
            var e = new Entry { Text = text, Base = text.fontSize, Applied = text.fontSize };
            entries.Add(e);
            if (lastFactor > 0f) Apply(e, lastFactor);
        }

        /// <summary>
        /// A label drawn as large text (a settings button's label) keeps at least this many screen pixels, so it
        /// still counts as large text for the contrast rules (18.7 px bold) on a small window.
        /// </summary>
        public static void KeepLarge(Text text, float pixels = 19f)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].Text != text) continue;
                entries[i].MinPixels = pixels;
                if (lastFactor > 0f) Apply(entries[i], lastFactor);
                return;
            }
        }

        /// <summary>The smallest font size, in canvas units, that comes out at this many screen pixels (12 by default) at this scale.</summary>
        public static int Floor(float scaleFactor, float pixels = MinPixels) => Mathf.CeilToInt(pixels / Mathf.Max(0.01f, scaleFactor) - 0.05f);

        /// <summary>
        /// A size for a &lt;size=…&gt; tag inside a label (a smaller second line), at least the floor at the current
        /// scale. Tags are part of the text, so the label's own floor doesn't reach them; code that builds such a
        /// text calls this each time it sets it.
        /// </summary>
        public static int Tag(int units) => lastFactor > 0f ? Mathf.Max(units, Floor(lastFactor)) : units;

        /// <summary>Called every frame by the UI with its canvas scale; only does work when the scale changes.</summary>
        public static void Follow(float scaleFactor)
        {
            if (Mathf.Approximately(scaleFactor, lastFactor)) return;
            lastFactor = scaleFactor;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].Text == null) { entries.RemoveAt(i); continue; }
                Apply(entries[i], scaleFactor);
            }
        }

        static void Apply(Entry e, float scaleFactor)
        {
            var t = e.Text;
            // Code that sizes a label itself (the packing list, the polaroids) owns that size from then on.
            if (t.fontSize != e.Applied) e.Base = t.fontSize;
            var canvas = t.GetComponentInParent<Canvas>(true);
            if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace) return;
            e.Applied = Mathf.Max(e.Base, Floor(scaleFactor, e.MinPixels));
            if (t.fontSize != e.Applied) t.fontSize = e.Applied;
        }
    }

    /// <summary>
    /// Keeps a layout element as wide as its label (plus padding) whenever the label's size changes, which the 12 px
    /// floor does on a small window. Keycaps are sized from their text when they're built, before the floor is known.
    /// </summary>
    public class FitToText : MonoBehaviour
    {
        public Text Label;
        public float Pad, Min;
        int size = -1;
        LayoutElement element;

        public static void Attach(Component target, Text label, float pad, float min = 0f)
        {
            var fit = target.gameObject.AddComponent<FitToText>();
            fit.Label = label;
            fit.Pad = pad;
            fit.Min = min;
        }

        void LateUpdate()
        {
            if (Label == null || Label.fontSize == size) return;
            size = Label.fontSize;
            if (element == null) element = GetComponent<LayoutElement>();
            if (element != null) element.preferredWidth = Mathf.Max(Min, Label.preferredWidth + Pad);
        }
    }
}
