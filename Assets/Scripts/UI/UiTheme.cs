using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PackTheTrunk
{
    /// <summary>
    /// "Summer road-trip scrapbook" look: paper cards, tape, stamps, handwriting and chunky
    /// pill buttons. Fonts are bundled OFL fonts (Lilita One, Varela Round, Patrick Hand);
    /// every sprite is generated at startup so there are no texture assets to manage.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Paper = new Color(1f, 0.968f, 0.91f);
        public static readonly Color PaperShade = new Color(0.92f, 0.86f, 0.76f);
        public static readonly Color Kraft = new Color(0.82f, 0.68f, 0.5f);
        public static readonly Color Ink = new Color(0.17f, 0.16f, 0.22f);
        public static readonly Color InkSoft = new Color(0.42f, 0.39f, 0.47f);
        public static readonly Color Accent = new Color(1f, 0.48f, 0.24f);
        public static readonly Color Teal = new Color(0.16f, 0.66f, 0.62f);
        public static readonly Color Stamp = new Color(0.86f, 0.25f, 0.26f);
        public static readonly Color Good = new Color(0.22f, 0.7f, 0.42f);
        public static readonly Color Gold = new Color(0.97f, 0.72f, 0.18f);
        public static readonly Color Night = new Color(0.16f, 0.19f, 0.3f);
        public static readonly Color Marker = new Color(1f, 0.9f, 0.3f, 0.55f);

        static Font display, body, hand;
        static Sprite paper, shadow, tape, circle, ring, check, keycap, dot;

        public static Font Display => display != null ? display : display = LoadFont("Fonts/LilitaOne-Regular");
        public static Font Body => body != null ? body : body = LoadFont("Fonts/VarelaRound-Regular");
        public static Font Hand => hand != null ? hand : hand = LoadFont("Fonts/PatrickHand-Regular");

        static Font LoadFont(string path)
        {
            var f = Resources.Load<Font>(path);
            return f != null ? f : UiKit.Font;
        }

        public static Sprite PaperSprite => paper != null ? paper : paper = MakePaper(96, 18);
        public static Sprite ShadowSprite => shadow != null ? shadow : shadow = MakeShadow(96, 22, 18);
        public static Sprite TapeSprite => tape != null ? tape : tape = MakeTape(128, 40);
        public static Sprite Circle => circle != null ? circle : circle = MakeCircle(128, false);
        public static Sprite Ring => ring != null ? ring : ring = MakeCircle(128, true);
        public static Sprite Check => check != null ? check : check = MakeCheck(96);
        public static Sprite Keycap => keycap != null ? keycap : keycap = MakeKeycap(64, 12);
        public static Sprite Dot => dot != null ? dot : dot = MakeCircle(32, false);
        static Sprite gradient, gradientUp, glow;
        /// <summary>Opaque on the left fading to clear on the right.</summary>
        public static Sprite Gradient => gradient != null ? gradient : gradient = MakeGradient(true);
        /// <summary>Opaque at the bottom fading to clear at the top.</summary>
        public static Sprite GradientUp => gradientUp != null ? gradientUp : gradientUp = MakeGradient(false);
        /// <summary>Soft round glow.</summary>
        public static Sprite Glow => glow != null ? glow : glow = MakeGlow(128);

        // ------------------------------------------------------------------ widgets

        /// <summary>A paper card with a soft drop shadow. Returns the card (children go inside it).</summary>
        public static RectTransform Card(string name, Transform parent, Color color, float rotation = 0f, bool shadowed = true)
        {
            var holder = UiKit.Rect(name, parent);
            if (shadowed)
            {
                var s = UiKit.Image("Shadow", holder, new Color(0.1f, 0.07f, 0.12f, 0.32f), false);
                s.sprite = ShadowSprite;
                s.type = Image.Type.Sliced;
                s.raycastTarget = false;
                s.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(-14, -22), new Vector2(18, 8));
            }
            var card = UiKit.Image("Card", holder, color, false);
            card.sprite = PaperSprite;
            card.type = Image.Type.Sliced;
            card.rectTransform.Fill();
            holder.localRotation = Quaternion.Euler(0, 0, rotation);
            return card.rectTransform;
        }

        public static Image Tape(Transform parent, Vector2 anchor, Vector2 position, float rotation, float width = 150f)
        {
            var img = UiKit.Image("Tape", parent, new Color(1f, 0.95f, 0.75f, 0.78f), false);
            img.sprite = TapeSprite;
            img.raycastTarget = false;
            img.rectTransform.Pin(anchor, new Vector2(0.5f, 0.5f), position, new Vector2(width, 42));
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
            return img;
        }

        public static Text Label(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var t = UiKit.Label(name, parent, text, size, color, anchor);
            t.font = font;
            t.supportRichText = true;
            return t;
        }

        /// <summary>A rubber-stamp label: outlined, tilted, slightly faded.</summary>
        public static RectTransform StampLabel(Transform parent, string text, Color color, int size = 18, float rotation = -4f, float padding = 14f)
        {
            var rt = UiKit.Rect("Stamp " + text, parent);
            var border = UiKit.Image("Border", rt, new Color(color.r, color.g, color.b, 0.95f), true);
            border.rectTransform.Fill();
            border.raycastTarget = false;
            var fill = UiKit.Image("Fill", rt, new Color(1f, 1f, 1f, 0.0f), true);
            fill.rectTransform.Fill(3);
            fill.color = Color.Lerp(color, Color.white, 0.86f);
            fill.raycastTarget = false;
            var t = Label("Text", rt, text, Display, size, color, TextAnchor.MiddleCenter);
            t.rectTransform.Fill(2);
            float width = t.preferredWidth + 22f;
            rt.sizeDelta = new Vector2(width, size + padding);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = size + padding;
            rt.localRotation = Quaternion.Euler(0, 0, rotation);
            return rt;
        }

        static readonly Color SealWax = new Color(0.72f, 0.16f, 0.14f);

        /// <summary>Grandpa's seal (three stars packed without asking for a hint): a red wax disc with a gold star.</summary>
        public static RectTransform Seal(Transform parent, float size)
        {
            var rt = UiKit.Rect("Seal", parent);
            rt.sizeDelta = new Vector2(size, size);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
            var wax = UiKit.Image("Wax", rt, SealWax, false);
            wax.sprite = Circle;
            wax.raycastTarget = false;
            wax.rectTransform.Fill();
            var ring = UiKit.Image("Ring", rt, new Color(1f, 0.82f, 0.7f, 0.5f), false);
            ring.sprite = Ring;
            ring.raycastTarget = false;
            ring.rectTransform.Fill(size * 0.09f);
            var star = UiKit.Image("Star", rt, Gold, false);
            star.sprite = UiKit.Star;
            star.raycastTarget = false;
            star.rectTransform.Fill(size * 0.22f);
            rt.localRotation = Quaternion.Euler(0, 0, -8f);
            return rt;
        }

        /// <summary>Chunky pill button with a darker base edge and a little hover lift.</summary>
        public static Button Pill(string name, Transform parent, string text, Color color, int size, UnityEngine.Events.UnityAction onClick, out Text label)
        {
            var rt = UiKit.Rect(name, parent);
            var edge = UiKit.Image("Edge", rt, Color.Lerp(color, Color.black, 0.32f));
            edge.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(0, -6), Vector2.zero);
            edge.raycastTarget = false;
            var face = UiKit.Image("Face", rt, color);
            face.rectTransform.Fill();
            var shine = UiKit.Image("Shine", face.transform, new Color(1f, 1f, 1f, 0.18f));
            shine.rectTransform.Place(new Vector2(0, 0.55f), Vector2.one, new Vector2(8, 0), new Vector2(-8, -5));
            shine.raycastTarget = false;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.62f, 0.62f, 0.62f, 0.8f);
            colors.colorMultiplier = 1.2f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            rt.gameObject.AddComponent<PillHover>();
            label = Label("Label", face.transform, text, Display, size, Color.white, TextAnchor.MiddleCenter);
            label.rectTransform.Fill(4);
            var sh = label.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.25f);
            sh.effectDistance = new Vector2(0, -2);
            return button;
        }

        public static Button Pill(string name, Transform parent, string text, Color color, int size, UnityEngine.Events.UnityAction onClick) =>
            Pill(name, parent, text, color, size, onClick, out _);

        /// <summary>Little keyboard key with a caption, for control hints.</summary>
        public static RectTransform KeyHint(Transform parent, string key, string caption)
        {
            var row = UiKit.Rect("Key " + key, parent);
            var h = UiKit.Horizontal(row.gameObject, 8, TextAnchor.MiddleLeft);
            h.childControlWidth = true;
            var cap = UiKit.Image("Cap", row, Color.white, false);
            cap.sprite = Keycap;
            cap.type = Image.Type.Sliced;
            cap.color = new Color(1f, 0.98f, 0.94f, 0.95f);
            var kt = Label("Key", cap.transform, key, Display, 20, Ink, TextAnchor.MiddleCenter);
            kt.rectTransform.Fill(2);
            kt.horizontalOverflow = HorizontalWrapMode.Overflow;
            var c = Label("Caption", row, caption, Body, 19, Color.white, TextAnchor.MiddleLeft);
            c.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
            c.horizontalOverflow = HorizontalWrapMode.Overflow;
            FitKeyHint(row);
            return row;
        }

        /// <summary>
        /// Size a key hint's cap and caption to their text. Text measured while inactive (say, rebuilt
        /// behind Settings after a rebind) comes out a little narrow, so the HUD layout calls this again
        /// once the strip is showing.
        /// </summary>
        public static void FitKeyHint(Transform hint)
        {
            var cap = hint.Find("Cap");
            var kt = cap != null ? cap.Find("Key")?.GetComponent<Text>() : null;
            if (kt != null) UiKit.Size(cap, Mathf.Max(38, kt.preferredWidth + 20), 38);
            var c = hint.Find("Caption")?.GetComponent<Text>();
            if (c != null) UiKit.Size(c, c.preferredWidth + 4, 38);
        }

        // ------------------------------------------------------------------ sprites

        static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        /// <summary>Transparent margin so edges stay anti-aliased when cards are rotated.</summary>
        const int Pad = 3;

        static float RoundedMask(float x, float y, int size, float radius)
        {
            float lo = Pad + radius, hi = size - Pad - radius;
            float cx = Mathf.Clamp(x, lo, hi);
            float cy = Mathf.Clamp(y, lo, hi);
            return Mathf.Clamp01(radius - Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) + 0.5f);
        }

        static Sprite MakePaper(int size, int radius)
        {
            // Clean paper with a soft darker rim, like a slightly curled edge.
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = RoundedMask(x + 0.5f, y + 0.5f, size, radius);
                float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y)) - Pad;
                float n = Mathf.Lerp(0.93f, 1f, Mathf.Clamp01(edge / 10f));
                byte v = (byte)(255 * n);
                px[y * size + x] = new Color32(v, v, v, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            float b = radius + Pad + 10;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite MakeShadow(int size, int radius, float blur)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float inner = blur;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = Mathf.Clamp(x + 0.5f, inner + radius * 0.3f, size - inner - radius * 0.3f);
                float fy = Mathf.Clamp(y + 0.5f, inner + radius * 0.3f, size - inner - radius * 0.3f);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(fx, fy));
                float a = Mathf.Clamp01(1f - d / (inner + radius * 0.3f));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            float b = size * 0.45f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite MakeTape(int w, int h)
        {
            var tex = NewTex(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float teeth = 5f + 3f * Mathf.Abs(Mathf.Sin(y * 0.9f));
                float ax = Mathf.Clamp01(Mathf.Min(x + 0.5f - teeth, w - teeth - x - 0.5f) + 0.5f);
                float ay = Mathf.Clamp01(Mathf.Min(y + 0.5f - Pad, h - Pad - y - 0.5f) + 0.5f);
                float stripe = 0.94f + 0.06f * Mathf.Sin(x * 0.35f);
                byte v = (byte)(255 * stripe);
                px[y * w + x] = new Color32(v, v, v, (byte)(ax * ay * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeCircle(int size, bool ringOnly)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float r = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (ringOnly) a *= Mathf.Clamp01(d - (r - size * 0.09f) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeCheck(int size)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            var a0 = new Vector2(size * 0.12f, size * 0.52f);
            var a1 = new Vector2(size * 0.4f, size * 0.2f);
            var a2 = new Vector2(size * 0.92f, size * 0.9f);
            float thick = size * 0.085f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(SegDist(p, a0, a1), SegDist(p, a1, a2));
                float a = Mathf.Clamp01(thick - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeKeycap(int size, int radius)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = RoundedMask(x + 0.5f, y + 0.5f, size, radius);
                float shade = y < 6 + Pad ? 0.72f : 1f;
                byte v = (byte)(255 * shade);
                px[y * size + x] = new Color32(v, v, v, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius + Pad, radius + 6 + Pad, radius + Pad, radius + Pad));
        }

        static Sprite MakeGradient(bool horizontal)
        {
            const int n = 256;
            var tex = horizontal ? NewTex(n, 4) : NewTex(4, n);
            var px = new Color32[n * 4];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                byte a = (byte)(255 * Mathf.Pow(1f - t, 1.5f) * (1f - 0.15f * t));
                for (int j = 0; j < 4; j++)
                {
                    int index = horizontal ? j * n + i : i * 4 + j;
                    px[index] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeGlow(int size)
        {
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
            return Vector2.Distance(p, a + (b - a) * t);
        }
    }
}
