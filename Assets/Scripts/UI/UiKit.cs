using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PackTheTrunk
{
    /// <summary>Small helpers for building uGUI hierarchies from code, plus generated sprites.</summary>
    public static class UiKit
    {
        public static readonly Color Panel = new Color(0.10f, 0.11f, 0.18f, 0.88f);
        public static readonly Color PanelLight = new Color(0.18f, 0.20f, 0.30f, 0.95f);
        public static readonly Color Accent = new Color(1f, 0.54f, 0.24f);
        public static readonly Color Text = new Color(0.97f, 0.97f, 1f);
        public static readonly Color Muted = new Color(0.68f, 0.71f, 0.82f);
        public static readonly Color Good = new Color(0.33f, 0.82f, 0.5f);
        public static readonly Color Bad = new Color(1f, 0.38f, 0.40f);
        public static readonly Color Gold = new Color(1f, 0.79f, 0.27f);

        static Font font;
        static Sprite rounded;
        static Sprite star;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Sprite Rounded
        {
            get
            {
                if (rounded == null) rounded = MakeRounded(64, 18);
                return rounded;
            }
        }

        public static Sprite Star
        {
            get
            {
                if (star == null) star = MakeStar(128);
                return star;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>Anchor to a single point with a fixed size.</summary>
        public static RectTransform Pin(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float padding = 0f) =>
            rt.Place(Vector2.zero, Vector2.one, new Vector2(padding, padding), new Vector2(-padding, -padding));

        public static Image Image(string name, Transform parent, Color color, bool roundedCorners = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (roundedCorners)
            {
                img.sprite = Rounded;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1.4f;
            }
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, int size, Color color, UnityAction onClick, out Text label)
        {
            var img = Image(name, parent, color);
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            colors.colorMultiplier = 1.2f;
            button.colors = colors;
            button.targetGraphic = img;
            if (onClick != null) button.onClick.AddListener(onClick);

            label = Label("Label", img.transform, text, size, Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.rectTransform.Fill(6f);
            return button;
        }

        public static Button Button(string name, Transform parent, string text, int size, Color color, UnityAction onClick) =>
            Button(name, parent, text, size, color, onClick, out _);

        public static Image StarImage(Transform parent, bool lit, float size)
        {
            var rt = Rect("Star", parent);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Star;
            img.color = lit ? Gold : new Color(1f, 1f, 1f, 0.15f);
            img.raycastTarget = false;
            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = size;
            layout.preferredHeight = size;
            return img;
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                const int pad = 3;
                float cx = Mathf.Clamp(x + 0.5f, radius + pad, size - radius - pad);
                float cy = Mathf.Clamp(y + 0.5f, radius + pad, size - radius - pad);
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - dist + 0.5f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 3, radius + 3, radius + 3, radius + 3));
        }

        static Sprite MakeStar(int size)
        {
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0 ? 0.48f : 0.2f) * size;
                points[i] = new Vector2(size / 2f + Mathf.Cos(angle) * r, size / 2f + Mathf.Sin(angle) * r - size * 0.03f);
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            const int samples = 3;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                    if (InPolygon(points, new Vector2(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples))) inside++;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * inside / (samples * samples)));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static bool InPolygon(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        public static VerticalLayoutGroup Vertical(GameObject go, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(0, 0, 0, 0);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup Horizontal(GameObject go, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        public static LayoutElement Size(Component c, float width = -1, float height = -1, float flexibleWidth = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) le.preferredWidth = width;
            if (height >= 0) le.preferredHeight = height;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
