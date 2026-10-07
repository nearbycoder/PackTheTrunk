using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PackTheTrunk
{
    /// <summary>
    /// All screens, in a "summer road-trip scrapbook" style: the trip map, the text-message
    /// story intro, the in-game luggage tag + packing checklist, and the postcard results.
    /// </summary>
    public partial class GameUI : MonoBehaviour
    {
        public event Action<int> LevelChosen;
        public event Action HintPressed;
        RectTransform keyboardHintsRow;

        /// <summary>The keyboard key hints, from the current bindings (rebuilt when they change).</summary>
        void BuildKeyboardHints()
        {
            if (keyboardHintsRow == null) return;
            UiKit.Clear(keyboardHintsRow);
            string L(Bindings.Action a) => Bindings.Label(a);
            Hint(keyboardHintsRow, "CLICK", "grab / drop", 0);
            Hint(keyboardHintsRow, L(Bindings.Action.Turn), "turn", 1);
            Hint(keyboardHintsRow, L(Bindings.Action.Tip), "tip", 2);
            Hint(keyboardHintsRow, L(Bindings.Action.Roll), "roll", 3);
            Hint(keyboardHintsRow, "WHEEL", "shelf", 6);
            Hint(keyboardHintsRow, "ESC", "back / pause", 9);
            Hint(keyboardHintsRow, L(Bindings.Action.Undo), "undo", 5);
            Hint(keyboardHintsRow, L(Bindings.Action.LookLeft) + " " + L(Bindings.Action.LookRight), "orbit", 8);
            keyboardXRayHint = UiTheme.KeyHint(keyboardHintsRow, L(Bindings.Action.XRay), "x-ray");
            hintRank[keyboardXRayHint] = 7;
            Hint(keyboardHintsRow, L(Bindings.Action.Close), "close", 4);
            hudLayoutWidth = -1f;
            // The rebuilt hint starts out plain: put toggled X-ray's "on" back if it was showing.
            bool on = xrayShownOn;
            xrayShownOn = false;
            SetXRayOn(on);
        }

        RectTransform keyboardXRayHint, padXRayHint;
        bool xrayShownOn;

        /// <summary>
        /// Toggled X-ray is on: its key hint says so in the accent colour and is the last to drop out
        /// of a narrow strip, so it's never on without the player being able to see why.
        /// </summary>
        public void SetXRayOn(bool on)
        {
            if (on == xrayShownOn) return;
            xrayShownOn = on;
            foreach (var hint in new[] { keyboardXRayHint, padXRayHint })
            {
                var caption = hint != null ? hint.Find("Caption")?.GetComponent<Text>() : null;
                if (caption == null) continue;
                caption.text = on ? "x-ray on" : "x-ray";
                caption.color = on ? UiTheme.Accent : Color.white;
                hintRank[hint] = on ? -1 : 7;
            }
            hudLayoutWidth = -1f;
        }

        /// <summary>Key hint with a rank: when the strip doesn't fit, the highest ranks drop out first.</summary>
        void Hint(RectTransform row, string key, string caption, int rank) => hintRank[UiTheme.KeyHint(row, key, caption)] = rank;

        readonly Dictionary<RectTransform, int> hintRank = new Dictionary<RectTransform, int>();
        float hudLayoutWidth = -1f, hudLayoutHeight = -1f;
        const float ListReserve = 470f, HudMargin = 40f;

        /// <summary>
        /// Fit the HUD to the canvas (screen shape and interface size): the key-hint strip keeps to the
        /// space left of the packing list, dropping its least important hints (then shrinking a little)
        /// rather than running into the list; Grandpa's tip moves under the trip tag when the gap at the
        /// top is too narrow; the toast centres in the free space.
        /// </summary>
        void LayoutHud()
        {
            float width = root.rect.width;
            hudLayoutWidth = width;
            hudLayoutHeight = root.rect.height;
            float strip = width - 44f - ListReserve - 24f;
            keyHints.sizeDelta = new Vector2(strip, keyHints.sizeDelta.y);
            foreach (var row in new[] { keyboardHintsRow, padHintsRow })
            {
                if (row == null) continue;
                var children = new List<RectTransform>();
                foreach (Transform c in row) { c.gameObject.SetActive(true); children.Add((RectTransform)c); }
                if (row.gameObject.activeInHierarchy) foreach (var c in children) UiTheme.FitKeyHint(c);
                row.localScale = Vector3.one;
                float Needed() { LayoutRebuilder.ForceRebuildLayoutImmediate(row); return LayoutUtility.GetPreferredWidth(row); }
                var byRank = children.OrderByDescending(c => hintRank.TryGetValue(c, out var r) ? r : 0).ToList();
                int dropped = 0;
                while (Needed() > strip && dropped < byRank.Count - 5)
                    byRank[dropped++].gameObject.SetActive(false);
                float needed = Needed();
                if (needed > strip) row.localScale = Vector3.one * Mathf.Max(0.75f, strip / needed);
            }
            if (tipShown) PlaceTip();
            PlaceToast();
            if (nowPlaying.gameObject.activeSelf && !PlaceNowPlaying()) RetractNowPlaying();
            // A different canvas height (interface size changed mid-trip, window resized): new rows.
            if (listItems != null && Mathf.Abs(listViewport.rect.height - listBuiltFor) > 1f)
            {
                BuildListRows(listItems);
                RefreshHud(listItems, lastRefresh.Held, lastRefresh.CanClose, lastRefresh.AllPacked, lastRefresh.FreeCells);
                ScrollToRow(lastRefresh.Held);
            }
        }

        /// <summary>The toast sits in the middle of the space left of the packing list, as tall as its text.</summary>
        void PlaceToast()
        {
            float free = root.rect.width - ListReserve - 2f * HudMargin - 30f;
            float w = Mathf.Min(860f, free);
            toastHolder.sizeDelta = new Vector2(w, 74f);
            float h = Mathf.Max(74f, toast.preferredHeight + 22f);
            toastHolder.sizeDelta = new Vector2(w, h);
            toastHolder.anchoredPosition = new Vector2(HudMargin + free * 0.5f - root.rect.width * 0.5f, 300f);
        }

        // ------------------------------------------------------------------ layout self-test

        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = Mathf.Min(c[0].x, c[1].x, c[2].x, c[3].x), xMax = Mathf.Max(c[0].x, c[1].x, c[2].x, c[3].x);
            float yMin = Mathf.Min(c[0].y, c[1].y, c[2].y, c[3].y), yMax = Mathf.Max(c[0].y, c[1].y, c[2].y, c[3].y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static Rect? ChildrenRect(Transform parent)
        {
            Rect? union = null;
            foreach (Transform child in parent)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                // Decoration outside the layout (the main menu's soft shade) isn't part of what has to fit.
                if (child.TryGetComponent<LayoutElement>(out var element) && element.ignoreLayout) continue;
                var r = ScreenRect((RectTransform)child);
                union = union is Rect u ? Rect.MinMaxRect(Mathf.Min(u.xMin, r.xMin), Mathf.Min(u.yMin, r.yMin), Mathf.Max(u.xMax, r.xMax), Mathf.Max(u.yMax, r.yMax)) : r;
            }
            return union;
        }

        /// <summary>Screen rectangles of every HUD piece that's showing (for the layout self-test).</summary>
        public Dictionary<string, Rect> HudRects()
        {
            var rects = new Dictionary<string, Rect>();
            void Add(string name, Transform t)
            {
                if (t != null && t.gameObject.activeInHierarchy) rects[name] = ScreenRect((RectTransform)t);
            }
            Add("trip tag", hud.Find("Trip Tag"));
            if (hud.Find("Buttons") is Transform b && ChildrenRect(b) is Rect br) rects["buttons"] = br;
            Add("packing list", hud.Find("Packing List"));
            Add("held card", heldPanel);
            foreach (var row in new[] { keyboardHintsRow, padHintsRow })
                if (row != null && row.gameObject.activeInHierarchy && ChildrenRect(row) is Rect kr) rects["key hints"] = kr;
            if (tipShown) Add("tip", tipHolder);
            if (toastGroup.alpha > 0.5f) Add("toast", toastHolder);
            if (nowPlayingTimer > 0.5f && nowPlayingTimer < 4f) Add("now playing", nowPlaying);
            return rects;
        }

        /// <summary>
        /// Every text showing on screen with its rendered size in screen pixels (font size times every
        /// scale above it, including smaller &lt;size&gt; tags inside it), smallest first. Faded-out,
        /// scrolled-away and off-screen text is skipped (legibility self-test).
        /// </summary>
        public List<(string Name, string Text, float Px)> VisibleTextSizes()
        {
            var sizes = new List<(string, string, float)>();
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            // The album's close-up covers everything behind it.
            foreach (var t in (IsAlbumZoomOpen ? albumZoom : root).GetComponentsInChildren<Text>(false))
            {
                if (!t.enabled || string.IsNullOrWhiteSpace(t.text) || t.canvasRenderer.cull) continue;
                if (t.color.a * t.canvasRenderer.GetInheritedAlpha() < 0.35f) continue;
                if (!ScreenRect(t.rectTransform).Overlaps(screen)) continue;
                int size = t.fontSize;
                if (t.supportRichText)
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(t.text, "<size=(\\d+)>"))
                        size = Mathf.Min(size, int.Parse(m.Groups[1].Value));
                string name = t.transform.parent != null ? t.transform.parent.name + "/" + t.name : t.name;
                sizes.Add((name, t.text, size * Mathf.Abs(t.transform.lossyScale.y)));
            }
            sizes.Sort((a, b) => a.Item3.CompareTo(b.Item3));
            return sizes;
        }

        /// <summary>One visible text, for the contrast check.</summary>
        public struct TextSample
        {
            public string Name, Text;
            /// <summary>Glyph bounds on screen, from the generated quads, so it hugs the letters.</summary>
            public Rect Glyphs;
            public float Px;
            /// <summary>Lilita One is a heavy display face: it counts as bold.</summary>
            public bool Bold;
            /// <summary>The text's own colour, and its alpha including faded parents.</summary>
            public Color Color;
            public float Alpha;
            /// <summary>A solid outline around the letters (alpha 0.9 or more), if any. WCAG counts a narrow border as part of the letter.</summary>
            public Color? Halo;
            /// <summary>The flat colour it sits on, when it's known (stamps).</summary>
            public Color? Backing;
            /// <summary>Part of a control that can't be used right now (a locked trip): WCAG doesn't hold inactive parts to a ratio.</summary>
            public bool Inactive;
            /// <summary>Something drawn on top covers the middle of it (an overlay or a dim).</summary>
            public bool Covered;
        }

        readonly List<UnityEngine.EventSystems.RaycastResult> sampleHits = new List<UnityEngine.EventSystems.RaycastResult>();

        /// <summary>Every visible text (the same ones as <see cref="VisibleTextSizes"/>), for the contrast check.</summary>
        public List<TextSample> VisibleTextSamples()
        {
            var list = new List<TextSample>();
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            var events = UnityEngine.EventSystems.EventSystem.current;
            foreach (var t in (IsAlbumZoomOpen ? albumZoom : root).GetComponentsInChildren<Text>(false))
            {
                if (!t.enabled || string.IsNullOrWhiteSpace(t.text) || t.canvasRenderer.cull) continue;
                float alpha = t.color.a * t.canvasRenderer.GetInheritedAlpha();
                if (alpha < 0.35f) continue;
                if (!ScreenRect(t.rectTransform).Overlaps(screen)) continue;
                var verts = t.cachedTextGenerator.verts;
                float ppu = Mathf.Max(0.0001f, t.pixelsPerUnit);
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
                for (int i = 0; i + 3 < verts.Count; i += 4)
                {
                    Vector3 a = verts[i].position, c = verts[i + 2].position;
                    if (Mathf.Abs(a.x - c.x) < 0.01f || Mathf.Abs(a.y - c.y) < 0.01f) continue;
                    for (int k = 0; k < 4; k++)
                    {
                        Vector2 w = t.transform.TransformPoint(verts[i + k].position / ppu);
                        min = Vector2.Min(min, w);
                        max = Vector2.Max(max, w);
                    }
                }
                if (min.x > max.x) continue;
                int size = t.fontSize;
                if (t.supportRichText)
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(t.text, "<size=(\\d+)>"))
                        size = Mathf.Min(size, int.Parse(m.Groups[1].Value));
                Color? halo = null;
                foreach (var effect in t.GetComponents<Outline>())
                    if (effect.enabled && effect.effectColor.a >= 0.9f) halo = effect.effectColor;
                bool covered = false;
                if (events != null)
                {
                    sampleHits.Clear();
                    events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events) { position = (min + max) * 0.5f }, sampleHits);
                    var canvas = t.canvas;
                    foreach (var hit in sampleHits)
                    {
                        if (hit.gameObject == t.gameObject || t.transform.IsChildOf(hit.gameObject.transform)) continue;
                        var g = hit.gameObject.GetComponent<Graphic>();
                        if (g == null) continue;
                        bool sameCanvas = g.canvas != null && canvas != null && g.canvas.rootCanvas == canvas.rootCanvas;
                        if (sameCanvas ? g.depth > t.depth : g.canvas != null && canvas != null && g.canvas.rootCanvas.sortingOrder > canvas.rootCanvas.sortingOrder)
                        {
                            covered = true;
                            break;
                        }
                    }
                }
                list.Add(new TextSample
                {
                    Name = t.transform.parent != null ? t.transform.parent.name + "/" + t.name : t.name,
                    Text = t.text,
                    Glyphs = Rect.MinMaxRect(min.x, min.y, max.x, max.y),
                    Px = size * Mathf.Abs(t.transform.lossyScale.y),
                    Bold = t.font == UiTheme.Display || t.fontStyle == FontStyle.Bold,
                    Color = t.color,
                    Alpha = alpha,
                    Halo = halo,
                    Backing = t.TryGetComponent<TextBacking>(out var backing) ? backing.Color : null,
                    Covered = covered,
                    Inactive = t.GetComponentInParent<Selectable>() is Selectable control && !control.IsInteractable(),
                });
            }
            return list;
        }

        /// <summary>
        /// The smallest font size, in canvas units, that still comes out at 12 screen pixels (Valve's
        /// recommended minimum on a Steam Deck) at this screen size and interface size.
        /// </summary>
        int MinReadableFont => Mathf.CeilToInt(12f / Mathf.Max(0.01f, root.GetComponent<Canvas>().scaleFactor) - 0.05f);

        /// <summary>Canvas units per screen pixel (screen shape and interface size).</summary>
        public float UnitsPerPixel => 1f / root.GetComponent<Canvas>().scaleFactor;

        /// <summary>How far the tallest row name's glyphs (about 1.1x the font size) overflow the row, in canvas units.</summary>
        public float ListTextOverflow()
        {
            float worst = float.MinValue;
            foreach (var row in rows.Values)
                worst = Mathf.Max(worst, row.Name.fontSize * 1.1f - ((RectTransform)row.Name.transform.parent).rect.height);
            return rows.Count == 0 ? 0f : worst;
        }

        /// <summary>The packing list's item area and each row in it, on screen.</summary>
        public (Rect Area, List<Rect> Rows) ListRects()
        {
            // In the list's own (unrotated) space and canvas units: the notepad is tilted a degree, so
            // screen-space boxes of neighbouring rows would always overlap a little.
            var list = new List<Rect>();
            foreach (Transform row in itemList)
            {
                if (!row.gameObject.activeInHierarchy) continue;
                var rt = (RectTransform)row;
                var r = rt.rect;
                list.Add(new Rect((Vector2)itemList.localPosition + (Vector2)rt.localPosition + r.min, r.size));
            }
            return (listViewport.rect, list);
        }

        /// <summary>Key-hint captions in the strip that have wrapped onto a second line (layout self-test).</summary>
        public List<string> WrappedKeyHints()
        {
            var wrapped = new List<string>();
            foreach (var row in new[] { keyboardHintsRow, padHintsRow })
            {
                if (row == null || !row.gameObject.activeInHierarchy) continue;
                foreach (Transform hint in row)
                {
                    var caption = hint.gameObject.activeSelf ? hint.Find("Caption")?.GetComponent<Text>() : null;
                    if (caption != null && caption.cachedTextGenerator.lineCount > 1) wrapped.Add(caption.text);
                }
            }
            return wrapped;
        }

        /// <summary>The CLOSE THE TRUNK button's pulse (for the self-test).</summary>
        public float ClosePulseScale => closePulse != null ? closePulse.localScale.x : 1f;

        /// <summary>Toggled X-ray is showing as on in the key hints (for the self-test).</summary>
        public bool XRayOnShown => xrayShownOn;

        static List<string> Captions(RectTransform row)
        {
            var captions = new List<string>();
            if (row == null) return captions;
            foreach (Transform hint in row)
                if (hint.Find("Caption")?.GetComponent<Text>() is Text t) captions.Add(t.text);
            return captions;
        }

        /// <summary>The captions in the keyboard and controller hint strips (for the self-test).</summary>
        public List<string> KeyboardHintCaptions() => Captions(keyboardHintsRow);
        public List<string> PadHintCaptions() => Captions(padHintsRow);

        /// <summary>The key caps currently shown in the keyboard hint strip (for the self-test).</summary>
        public List<string> KeyboardHintKeys()
        {
            var keys = new List<string>();
            if (keyboardHintsRow == null) return keys;
            foreach (Transform row in keyboardHintsRow)
            {
                var cap = row.Find("Cap/Key");
                if (cap != null) keys.Add(cap.GetComponent<Text>().text);
            }
            return keys;
        }
        RectTransform padHintsRow;

        /// <summary>The key-hint strip is showing controller buttons.</summary>
        public bool PadHintsShown => padHintsRow != null && padHintsRow.gameObject.activeInHierarchy;
        Button hintButton;

        /// <summary>The HINT button only shows on trips that have a usable solution.</summary>
        public void SetHintAvailable(bool available) => hintButton.gameObject.SetActive(available);
        public event Action UndoPressed, RestartPressed, MenuPressed, ClosePressed, NextPressed, PutBackPressed, QuitPressed;
        public event Action StartPressed;
        public event Action<PackItem> ItemRowClicked;

        RectTransform root;
        RectTransform menu, story, hud, results;
        CanvasScaler scaler;
        RectTransform keyHints;
        bool hudHidden;
        int escConsumedFrame = -1;
        Button albumBack;
        Text albumBackLabel;

        /// <summary>True on a frame where the UI already used Escape to close something.</summary>
        public bool EscConsumed => escConsumedFrame == Time.frameCount;

        // Menu
        RectTransform mapArea;
        Text mapChapter, mapTitle, mapSub;
        Button pagePrev, pageNext;
        RectTransform pageDots;
        int menuPage = -1;
        IReadOnlyList<LevelDef> menuLevels;
        Func<int, int> menuStars;
        /// <summary>Has this trip (by index) earned Grandpa's seal? Set by the game controller.</summary>
        public Func<int, bool> SealedFor;

        /// <summary>How many things are packed in a trip's waiting trunk (0 if none).</summary>
        public Func<int, int> WaitingFor;
        Text mapSeals;

        int SealCount(IReadOnlyList<LevelDef> levels) => SealedFor == null ? 0 : levels.Count(l => SealedFor(l.Index));

        static string SealsLine(int sealedCount, int total) =>
            (sealedCount == 0 ? "Grandpa's seal" : $"Grandpa's seal on {sealedCount} of {total} trips") +
            "\n<size=21>Three stars without asking him for a hint.</size>";
        Func<int, bool> menuUnlocked;

        // Chapter card + note
        RectTransform chapterCard;
        CanvasGroup chapterGroup;
        Text chapterNumeral, chapterName, chapterYears, chapterIntro;
        RectTransform phone, note, noteList;
        Text noteHeader;
        bool noteMode;

        // Album finale
        bool albumFinale;
        RectTransform album, albumGrid;
        Text albumTitle, albumThanks;
        Button albumDone;
        public event Action AlbumClosed;

        // Story
        RectTransform messageList;
        Text phoneName, phoneAvatarLetter, tripNumber, tripPlace, tripTitle, tripDetails;
        Image phoneAvatar;
        Button startButton, backButton;
        Text startLabel;
        Coroutine messageRoutine;

        // HUD
        Text tagTrip, tagTitle, tagBlurb, listFrom;
        RectTransform itemList, listViewport;
        ScrollRect listScroll;
        Text moreBelow, moreAbove;
        bool listScrolls;
        Text countsText, spaceText;
        Image[] meterStars;
        RectTransform meterRect;
        int countsReq, countsBonus;
        int meterShown;
        static readonly Color MeterUnlit = new Color(0.4f, 0.32f, 0.25f, 0.32f);
        RectTransform spaceFill;
        Button closeButton;
        Text closeLabel;
        Image closeFace;
        RectTransform closePulse;
        RectTransform heldPanel;
        Text heldName, heldDesc;
        RectTransform heldStamps;
        Text toast;
        CanvasGroup toastGroup;
        float toastTimer;
        Text hoverText;
        int totalSpace = 1;

        // Results
        Text greetings, placeText, resultsTitle, epilogueText, leftBehindText, resultsCounts;
        RectTransform resultsStars;
        Text nextLabel;

        readonly Dictionary<PackItem, ItemRow> rows = new Dictionary<PackItem, ItemRow>();

        // Now playing
        RectTransform nowPlaying;
        Text nowPlayingTitle;
        float nowPlayingTimer;
        RectTransform[] reels;
        bool pulseClose;

        class ItemRow
        {
            public Image Marker, Check, Strike;
            public Text Name;
            public RectTransform Stamps;
            public bool Packed;
            public float StrikeWidth;
        }

        RectTransform toastHolder;
        float spaceShown, spaceTarget;
        float toastShown;

        public bool PointerOverUi => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        void Awake()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                DontDestroyOnLoad(es);
            }

            var canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // Expand: the whole 1920x1080 design (divided by the interface size) is always on screen,
            // and wider or taller screens just get extra room. (Matching half width / half height made
            // the canvas shorter than the design on ultrawide screens and squeezed the packing list.)
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root = (RectTransform)canvasGo.transform;

            BuildTitle();
            BuildMenu();
            BuildStory();
            BuildHud();
            BuildResults();
            BuildAlbum();
            BuildChapterCard();
            BuildPause();
            BuildSettings();
            BuildCredits();
            BuildConfirm();
            BuildNowPlaying();
            BuildCurtain();
            ApplyUiSettings();
            ShowOnly(title);
        }

        void Update()
        {
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);

            if (toastTimer > 0f)
            {
                toastTimer -= UiTime.Delta;
                toastGroup.alpha = Mathf.Clamp01(toastTimer / 0.4f) * Mathf.Clamp01(toastShown * 3f);
            }

            FollowScreenSize();
            UpdateNowPlaying();
            UpdateTitle();
            UpdateHudMotion();
            UpdateTip();
            if (moreBelow != null)
            {
                bool more = listScrolls && itemList.anchoredPosition.y + listViewport.rect.height < itemList.rect.height - 2f;
                if (moreBelow.gameObject.activeSelf != more) moreBelow.gameObject.SetActive(more);
                bool above = listScrolls && itemList.anchoredPosition.y > 2f;
                if (moreAbove.gameObject.activeSelf != above) moreAbove.gameObject.SetActive(above);
            }
            if (hud.gameObject.activeSelf && (!Mathf.Approximately(root.rect.width, hudLayoutWidth) || !Mathf.Approximately(root.rect.height, hudLayoutHeight)))
                LayoutHud();

            var kb = UnityEngine.InputSystem.Keyboard.current;
            bool escUsed = UpdateRebind();
            if (!escUsed && !IsRebinding && ((kb != null && kb.escapeKey.wasPressedThisFrame) || Pad.Down(p => p.buttonEast)))
            {
                if (confirm.gameObject.activeSelf) { CloseConfirm(false); Sfx.Instance?.Back(); escConsumedFrame = Time.frameCount; }
                else if (settings.gameObject.activeSelf) { HideSettings(); Sfx.Instance?.Back(); escConsumedFrame = Time.frameCount; }
                else if (credits.gameObject.activeSelf) { CloseCredits(); escConsumedFrame = Time.frameCount; }
                else if (IsAlbumZoomOpen) { CloseAlbumZoom(); Sfx.Instance?.Back(); escConsumedFrame = Time.frameCount; }
            }
            UpdateAlbumZoom();

            float s = closeButton != null && closeButton.interactable && pulseClose && !GameSettings.ReduceMotion ? 1f + Mathf.Sin(UiTime.Now * 6f) * 0.035f : 1f;
            if (closePulse != null) closePulse.localScale = new Vector3(s, s, 1f);

            if (messageRoutine != null && story.gameObject.activeSelf && !chapterCard.gameObject.activeSelf)
            {
                var k = UnityEngine.InputSystem.Keyboard.current;
                var m = UnityEngine.InputSystem.Mouse.current;
                if ((k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) || (m != null && m.leftButton.wasPressedThisFrame && !PointerOverUi))
                    skipMessages = true;
            }

            if (startButton != null && startButton.gameObject.activeInHierarchy)
            {
                float k = GameSettings.ReduceMotion ? 1f : 1f + Mathf.Sin(UiTime.Now * 5f) * 0.03f;
                startButton.transform.GetChild(1).localScale = new Vector3(k, k, 1f);
            }
        }

        void ShowOnly(RectTransform screen)
        {
            if (title != null) title.gameObject.SetActive(screen == title);
            if (pause != null) pause.gameObject.SetActive(false);
            menu.gameObject.SetActive(screen == menu);
            story.gameObject.SetActive(screen == story);
            hud.gameObject.SetActive(screen == hud || screen == results);
            results.gameObject.SetActive(screen == results);
            if (album != null) album.gameObject.SetActive(screen == album);
        }

        // ================================================================== MENU

        void BuildMenu()
        {
            menu = UiKit.Rect("Menu", root).Fill();
            var wash = UiKit.Image("Wash", menu, new Color(0.98f, 0.82f, 0.62f, 0.18f), false);
            wash.rectTransform.Fill();

            mapHeader = UiTheme.Label("Header", menu, "THE FAMILY TRIPS", UiTheme.Display, 84, Color.white, TextAnchor.UpperLeft);
            mapHeader.rectTransform.Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -150), new Vector2(800, 110));
            mapHeader.gameObject.AddComponent<Outline>().effectColor = UiTheme.Ink;
            var mh = mapHeader.gameObject.AddComponent<Shadow>();
            mh.effectColor = new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 1f);
            mh.effectDistance = new Vector2(6, -7);
            UiMotion.Intro(mapHeader.rectTransform, new Vector2(-400, 0), 0.05f, 1f, -4f, 0.55f);
            var back = UiTheme.Pill("Map Back", menu, "< BACK", UiTheme.Night, 26, () => TitleBackPressed?.Invoke());
            back.GetComponent<PillHover>().IsBack = true;
            ((RectTransform)back.transform).Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -50), new Vector2(170, 60));

            var note = UiTheme.Card("Story Note", menu, UiTheme.Paper, -1f);
            mapNote = (RectTransform)note.parent;
            mapNote.Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -330), new Vector2(700, 380));
            UiMotion.Intro(note.parent, new Vector2(-600, 0), 0.12f, 0.95f, -10f, 0.6f);
            UiTheme.Tape(note, new Vector2(0.5f, 1f), new Vector2(0, -2), 4f);
            var noteText = UiTheme.Label("Text", note,
                "Every family has one person who can make anything fit.\nIn yours, it's you. Grandpa Joe taught you everything.",
                UiTheme.Hand, 33, UiTheme.Ink, TextAnchor.UpperLeft);
            noteText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(34, 150), new Vector2(-30, -36));
            var how = UiKit.Rect("How", note).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(30, 24), new Vector2(-24, 160));
            UiKit.Vertical(how.gameObject, 6).childControlHeight = false;
            void Hint(string key, string text)
            {
                var r = UiKit.Rect("Line", how);
                r.sizeDelta = new Vector2(600, 40);
                var h = UiKit.Horizontal(r.gameObject, 10, TextAnchor.MiddleLeft);
                var cap = UiKit.Image("Cap", r, Color.white, false);
                cap.sprite = UiTheme.Keycap;
                cap.type = Image.Type.Sliced;
                var kt = UiTheme.Label("K", cap.transform, key, UiTheme.Display, 19, UiTheme.Ink, TextAnchor.MiddleCenter);
                kt.rectTransform.Fill(2);
                UiKit.Size(cap, Mathf.Max(40, kt.preferredWidth + 20), 36);
                var lt = UiTheme.Label("T", r, text, UiTheme.Body, 21, UiTheme.InkSoft, TextAnchor.MiddleLeft);
                UiKit.Size(lt, 480, 36);
            }
            Hint("CLICK", "pick something up, then drop it in the trunk");
            Hint("R  T  F", "turn, tip and roll it to fit");
            Hint("WHEEL", "choose a shelf when there's a gap");

            var seals = UiTheme.Card("Seals Note", menu, UiTheme.Paper, 1.2f);
            ((RectTransform)seals.parent).Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(150, -740), new Vector2(640, 100));
            UiMotion.Intro(seals.parent, new Vector2(-600, 0), 0.2f, 0.95f, -10f, 0.6f);
            UiTheme.Seal(seals, 64).Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(54, 0), new Vector2(64, 64));
            mapSeals = UiTheme.Label("Text", seals, "", UiTheme.Hand, 27, UiTheme.Ink, TextAnchor.MiddleLeft);
            mapSeals.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(104, 8), new Vector2(-20, -8));


            var map = UiTheme.Card("Map", menu, new Color(0.99f, 0.95f, 0.86f), 1.5f);
            mapPage = (RectTransform)map.parent;
            mapPage.Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-80, -10), new Vector2(880, 940));
            UiMotion.Intro(map.parent, new Vector2(700, 0), 0f, 0.95f, 8f, 0.6f);
            UiTheme.Tape(map, new Vector2(0, 1), new Vector2(40, -10), -35f);
            UiTheme.Tape(map, new Vector2(1, 1), new Vector2(-40, -10), 35f);
            var water = UiKit.Image("Lake", map, new Color(0.55f, 0.78f, 0.9f, 0.55f), false);
            water.sprite = UiTheme.Circle;
            water.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-250, 230), new Vector2(240, 150));
            water.raycastTarget = false;
            var forest = UiKit.Image("Forest", map, new Color(0.45f, 0.7f, 0.45f, 0.35f), false);
            forest.sprite = UiTheme.Circle;
            forest.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260, 60), new Vector2(300, 220));
            forest.raycastTarget = false;
            var sea = UiKit.Image("Sea", map, new Color(0.5f, 0.75f, 0.92f, 0.45f), false);
            sea.sprite = UiTheme.Circle;
            sea.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(250, -300), new Vector2(300, 190));
            sea.raycastTarget = false;
            mapChapter = UiTheme.Label("Chapter", map, "", UiTheme.Body, 20, UiTheme.AccentInk, TextAnchor.MiddleCenter);
            mapChapter.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -62), new Vector2(0, -32));
            mapTitle = UiTheme.Label("Title", map, "", UiTheme.Display, 46, UiTheme.Ink, TextAnchor.MiddleCenter);
            mapTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -116), new Vector2(0, -58));
            mapSub = UiTheme.Label("Sub", map, "", UiTheme.Hand, 26, UiTheme.InkSoft, TextAnchor.UpperCenter);
            mapSub.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -190), new Vector2(-60, -118));
            mapArea = UiKit.Rect("Route", map).Place(Vector2.zero, Vector2.one, new Vector2(30, 100), new Vector2(-30, -200));
            pagePrev = UiTheme.Pill("Prev Page", map, "<", UiTheme.Night, 30, () => FlipPage(-1));
            ((RectTransform)pagePrev.transform).Pin(new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 30), new Vector2(70, 56));
            pageNext = UiTheme.Pill("Next Page", map, ">", UiTheme.Night, 30, () => FlipPage(1));
            ((RectTransform)pageNext.transform).Pin(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 30), new Vector2(70, 56));
            pageDots = UiKit.Rect("Dots", map).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(400, 20));
            UiKit.Horizontal(pageDots.gameObject, 12, TextAnchor.MiddleCenter);
        }

        /// <summary>Zig-zag pin positions for n stops, top to bottom.</summary>
        static Vector2[] PinsFor(int n)
        {
            var pins = new Vector2[n];
            float top = 260f, bottom = -270f;
            for (int i = 0; i < n; i++)
            {
                float y = n == 1 ? 0f : Mathf.Lerp(top, bottom, i / (float)(n - 1));
                pins[i] = new Vector2(i % 2 == 0 ? -300f : 60f, y);
            }
            return pins;
        }

        RectTransform mapPage, mapNote;
        Text mapHeader;

        /// <summary>
        /// What overlaps or leaves the screen on the main menu or the trip map, whichever is showing
        /// (menu layout self-test). Cards are tilted a degree or two, so their screen boxes are a little
        /// bigger than the cards; the gaps the layout leaves are wider than that.
        /// </summary>
        public List<string> MenuLayoutProblems()
        {
            var problems = new List<string>();
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            bool Overlaps(Rect a, Rect b) => Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 2f && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 2f;
            bool OffScreen(Rect r) => r.xMin < -1 || r.yMin < -1 || r.xMax > screen.xMax + 1 || r.yMax > screen.yMax + 1;
            if (title.gameObject.activeSelf && titleMenu.gameObject.activeInHierarchy && ChildrenRect(titleMenu) is Rect entries)
            {
                if (Overlaps(entries, ScreenRect(titleTagline))) problems.Add("the main menu runs into the tagline");
                if (OffScreen(entries)) problems.Add("the main menu is off screen");
            }
            if (menu.gameObject.activeSelf)
            {
                var page = ScreenRect(mapPage);
                var header = ScreenRect(mapHeader.rectTransform);
                header.xMax = header.xMin + mapHeader.preferredWidth * mapHeader.transform.lossyScale.x;
                if (Overlaps(page, ScreenRect(mapNote))) problems.Add("the map page covers the story note");
                if (Overlaps(page, header)) problems.Add("the map page covers the heading");
                if (OffScreen(page)) problems.Add("the map page is off screen");
            }
            return problems;
        }

        /// <summary>
        /// The scrapbook page sits right of the note. On a narrower canvas (a bigger interface size) it
        /// first moves closer to the screen edge, then shrinks, rather than covering the note. Runs before
        /// the menu shows, so the page's entrance lands on the new spot.
        /// </summary>
        void FitMapPage()
        {
            const float NoteRight = 110f + 700f, Gap = 25f, Width = 880f, Height = 940f;
            float w = root.rect.width, h = root.rect.height;
            float margin = Mathf.Clamp(w - NoteRight - Gap - Width, 30f, 80f);
            float scale = Mathf.Min(1f, (w - margin - NoteRight - Gap) / Width, (h - 70f) / Height);
            mapPage.anchoredPosition = new Vector2(-margin, -10f);
            mapPage.localScale = Vector3.one * scale;
            mapPage.GetComponent<UiIntro>()?.Rehome();
        }

        public void ShowMenu(IReadOnlyList<LevelDef> levels, Func<int, int> starsFor, Func<int, bool> unlocked)
        {
            if (!menu.gameObject.activeSelf) FitMapPage();
            ShowOnly(menu);
            menuLevels = levels;
            menuStars = starsFor;
            menuUnlocked = unlocked;
            // Open on the chapter holding the next trip to play (or the last one if everything's done).
            int target = levels.Count - 1;
            for (int i = 0; i < levels.Count; i++)
                if (unlocked(i) && starsFor(i) == 0) { target = i; break; }
            menuPage = levels[target].Chapter.Index;
            mapSeals.text = SealsLine(SealCount(levels), levels.Count);
            DrawPage();
        }

        void FlipPage(int delta)
        {
            if (menuLevels == null) return;
            int page = Mathf.Clamp(menuPage + delta, 0, GameDatabase.Chapters.Count - 1);
            if (page == menuPage) return;
            menuPage = page;
            Sfx.Instance?.Page();
            DrawPage();
        }

        /// <summary>One chapter of the family album: a dashed route through that chapter's trips.</summary>
        void DrawPage()
        {
            UiKit.Clear(mapArea);
            UiKit.Clear(pageDots);
            var chapters = GameDatabase.Chapters;
            var chapter = chapters[menuPage];
            mapChapter.text = $"CHAPTER {chapter.Numeral}  ·  {chapter.Years.ToUpperInvariant()}";
            mapTitle.text = chapter.Title;
            mapSub.text = chapter.Intro;
            pagePrev.interactable = menuPage > 0;
            pageNext.interactable = menuPage < chapters.Count - 1;
            for (int i = 0; i < chapters.Count; i++)
            {
                var dot = UiKit.Image("Dot", pageDots, i == menuPage ? UiTheme.Accent : new Color(0.45f, 0.4f, 0.35f, 0.35f), false);
                dot.sprite = UiTheme.Dot;
                dot.raycastTarget = false;
                UiKit.Size(dot, i == menuPage ? 18 : 12, i == menuPage ? 18 : 12);
            }

            var stops = chapter.Levels;
            var pins = PinsFor(stops.Count);
            if (pins.Length > 1)
            {
                var path = new List<Vector2>();
                for (int i = 0; i < pins.Length - 1; i++)
                {
                    var p0 = pins[Mathf.Max(0, i - 1)];
                    var p1 = pins[i];
                    var p2 = pins[i + 1];
                    var p3 = pins[Mathf.Min(pins.Length - 1, i + 2)];
                    for (int k = 0; k < 14; k++) path.Add(CatmullRom(p0, p1, p2, p3, k / 14f));
                }
                path.Add(pins[pins.Length - 1]);
                for (int i = 0; i < path.Count - 1; i += 2)
                {
                    var a = path[i];
                    var b = path[i + 1];
                    var dash = UiKit.Image("Dash", mapArea, new Color(0.55f, 0.42f, 0.3f, 0.75f), false);
                    dash.raycastTarget = false;
                    dash.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), (a + b) / 2f, new Vector2((b - a).magnitude, 7));
                    dash.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                }
            }

            int next = -1;
            for (int i = 0; i < menuLevels.Count; i++)
                if (menuUnlocked(i) && menuStars(i) == 0) { next = i; break; }
            for (int s = 0; s < stops.Count; s++)
            {
                var level = stops[s];
                int index = level.Index;
                bool open = menuUnlocked(index);
                int stars = menuStars(index);
                var pos = pins[s];

                if (open && index == next)
                {
                    var halo = UiKit.Image("Halo", mapArea, new Color(1f, 0.85f, 0.3f, 0.6f), false);
                    halo.sprite = UiTheme.Circle;
                    halo.raycastTarget = false;
                    halo.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(124, 124));
                    halo.gameObject.AddComponent<Breathe>();
                }

                var pinSlot = UiKit.Rect("Pin " + (index + 1), mapArea).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(88, 88));
                UiMotion.Intro(pinSlot, new Vector2(0, 40), 0.15f + s * 0.07f, 0.2f, -30f, 0.5f);
                var pin = UiKit.Rect("Level " + (index + 1), pinSlot).Fill();
                var shadow = UiKit.Image("Shadow", pin, new Color(0, 0, 0, 0.25f), false);
                shadow.sprite = UiTheme.Circle;
                shadow.raycastTarget = false;
                shadow.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(4, -8), new Vector2(4, -8));
                var face = UiKit.Image("Face", pin, open ? level.BodyColor : new Color(0.62f, 0.6f, 0.58f), false);
                face.sprite = UiTheme.Circle;
                face.rectTransform.Fill();
                var ring = UiKit.Image("Ring", pin, Color.white, false);
                ring.sprite = UiTheme.Ring;
                ring.raycastTarget = false;
                ring.rectTransform.Fill(4);
                // White on the car's colour, unless the car is a light one (the yellow Mini, the white house car): then ink.
                bool inkNumber = UiTheme.Contrast(Color.white, face.color) < 3f;
                var num = UiTheme.Label("Num", pin, open ? (index + 1).ToString() : "?", UiTheme.Display, 38, inkNumber ? UiTheme.Ink : Color.white, TextAnchor.MiddleCenter);
                num.rectTransform.Fill();
                if (!inkNumber) num.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.35f);
                var button = pin.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                button.interactable = open;
                button.onClick.AddListener(() => LevelChosen?.Invoke(index));
                pin.gameObject.AddComponent<PillHover>();

                var labelCard = UiTheme.Card("Label", mapArea, UiTheme.Paper, -2f);
                var holder = (RectTransform)labelCard.parent;
                holder.Pin(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), pos + new Vector2(58, -4), new Vector2(290, 82));
                UiMotion.Intro(holder, new Vector2(-30, 0), 0.2f + s * 0.07f, 0.8f, 6f, 0.45f);
                var tt = UiTheme.Label("Title", labelCard, open ? level.Title : "???", UiTheme.Hand, 27, open ? UiTheme.Ink : UiTheme.InkSoft, TextAnchor.UpperLeft);
                tt.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -38), new Vector2(-10, -4));
                tt.horizontalOverflow = HorizontalWrapMode.Overflow;
                while (tt.fontSize > 18 && tt.preferredWidth > 262f) tt.fontSize--;
                string month = (level.Trip ?? "").Split('·')[0].Trim().ToUpperInvariant();
                string when = level.Year > 0 ? $"{month} {level.Year}" : month;
                int waiting = open && WaitingFor != null ? WaitingFor(index) : 0;
                if (waiting > 0) when += $"  ·  {waiting} PACKED, WAITING";
                var sub = UiTheme.Label("When", labelCard, open ? when : "LOCKED", UiTheme.Body, 18, waiting > 0 ? UiTheme.AccentInk : UiTheme.InkSoft, TextAnchor.UpperLeft);
                sub.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -58), new Vector2(-10, -38));
                sub.horizontalOverflow = HorizontalWrapMode.Overflow;
                // A long "waiting" line drops the date rather than shrinking or running off the card.
                if (waiting > 0 && sub.preferredWidth > 264f) sub.text = $"{waiting} PACKED, WAITING";
                var starRow = UiKit.Rect("Stars", labelCard).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(14, 4), new Vector2(-10, 28));
                UiKit.Horizontal(starRow.gameObject, 3, TextAnchor.MiddleLeft);
                for (int k = 0; k < 3; k++)
                {
                    var star = UiKit.StarImage(starRow, k < stars, 22);
                    if (k >= stars) star.color = new Color(0.4f, 0.35f, 0.3f, 0.25f);
                }
                if (open && SealedFor != null && SealedFor(index)) UiTheme.Seal(starRow, 24).name = "Seal " + (index + 1);
            }
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // ================================================================== STORY

        void BuildStory()
        {
            story = UiKit.Rect("Story", root).Fill();
            var dim = UiKit.Image("Dim", story, new Color(0.08f, 0.06f, 0.14f, 0.35f), false);
            dim.rectTransform.Fill();

            phone = UiKit.Rect("Phone", story).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-360, -10), new Vector2(500, 880));
            phone.localRotation = Quaternion.Euler(0, 0, -3f);
            UiMotion.Intro(phone, new Vector2(0, -1000), 0.05f, 1f, -10f, 0.7f);
            var phoneShadow = UiKit.Image("Shadow", phone, new Color(0, 0, 0, 0.35f), false);
            phoneShadow.sprite = UiTheme.ShadowSprite;
            phoneShadow.type = Image.Type.Sliced;
            phoneShadow.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(-20, -40), new Vector2(30, 0));
            var frame = UiKit.Image("Frame", phone, new Color(0.13f, 0.13f, 0.17f));
            frame.pixelsPerUnitMultiplier = 0.45f;
            frame.rectTransform.Fill();
            var screen = UiKit.Image("Screen", phone, new Color(0.95f, 0.95f, 0.97f));
            screen.pixelsPerUnitMultiplier = 0.7f;
            screen.rectTransform.Fill(16);
            var notch = UiKit.Image("Notch", phone, new Color(0.13f, 0.13f, 0.17f));
            notch.rectTransform.Pin(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(150, 30));

            var header = UiKit.Image("Header", screen.transform, Color.white);
            header.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -160), Vector2.zero);
            phoneAvatar = UiKit.Image("Avatar", header.transform, UiTheme.Accent, false);
            phoneAvatar.sprite = UiTheme.Circle;
            phoneAvatar.rectTransform.Pin(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 46), new Vector2(64, 64));
            phoneAvatarLetter = UiTheme.Label("Letter", phoneAvatar.transform, "M", UiTheme.Display, 34, Color.white, TextAnchor.MiddleCenter);
            phoneAvatarLetter.rectTransform.Fill();
            phoneName = UiTheme.Label("Name", header.transform, "Mom", UiTheme.Body, 22, UiTheme.Ink, TextAnchor.MiddleCenter);
            phoneName.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 6), new Vector2(0, 40));

            messageList = UiKit.Rect("Messages", screen.transform).Place(Vector2.zero, Vector2.one, new Vector2(18, 18), new Vector2(-18, -180));
            var v = UiKit.Vertical(messageList.gameObject, 14, null, TextAnchor.UpperLeft);
            v.childControlWidth = false;
            v.childForceExpandWidth = false;

            var card = UiTheme.Card("Trip Card", story, UiTheme.Paper, 2f);
            ((RectTransform)card.parent).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(330, 90), new Vector2(640, 470));
            UiTheme.Tape(card, new Vector2(0.5f, 1f), new Vector2(0, -4), -3f, 180f);
            UiMotion.Intro(card.parent, new Vector2(0, 700), 0.15f, 0.9f, 12f, 0.7f);
            tripNumber = UiTheme.Label("Number", card, "", UiTheme.Body, 20, UiTheme.InkSoft, TextAnchor.UpperLeft);
            tripNumber.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -80), new Vector2(-30, -46));
            tripPlace = UiTheme.Label("Place", card, "", UiTheme.Hand, 34, UiTheme.AccentInk, TextAnchor.UpperLeft);
            tripPlace.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -126), new Vector2(-30, -78));
            tripTitle = UiTheme.Label("Title", card, "", UiTheme.Display, 66, UiTheme.Ink, TextAnchor.UpperLeft);
            tripTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(38, -220), new Vector2(-30, -124));
            tripTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            tripDetails = UiTheme.Label("Details", card, "", UiTheme.Body, 23, UiTheme.InkSoft, TextAnchor.UpperLeft);
            tripDetails.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(40, 30), new Vector2(-30, -232));
            tripDetails.lineSpacing = 1.3f;

            BuildNote();

            var startSlot = UiKit.Rect("Start Slot", story).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(330, -250), new Vector2(380, 92));
            startButton = UiTheme.Pill("Start", startSlot, "LET'S PACK!", UiTheme.Accent, 38, () => StartPressed?.Invoke(), out startLabel);
            ((RectTransform)startButton.transform).Fill();
            var startKey = UiTheme.Label("Key", startSlot, "or press SPACE", UiTheme.Body, 18, new Color(1f, 0.95f, 0.88f), TextAnchor.UpperCenter);
            // It sits over the driveway, so it gets a solid dark outline like the title's.
            startKey.gameObject.AddComponent<Outline>().effectColor = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.9f);
            startKey.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, -30), new Vector2(0, -4));
            startKey.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.55f);
            UiMotion.Intro(startButton.transform.parent, new Vector2(0, -30), 0f, 0.3f, -8f, 0.55f);
            startButton.transform.parent.gameObject.SetActive(false);
            backButton = UiTheme.Pill("Back", story, "MAP", UiTheme.Night, 22, () => MenuPressed?.Invoke());
            backButton.GetComponent<PillHover>().IsBack = true;
            ((RectTransform)backButton.transform).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(330, -372), new Vector2(150, 54));
        }

        /// <summary>A handwritten note on lined paper, for Grandpa's and Grandma's letters.</summary>
        void BuildNote()
        {
            var card = UiTheme.Card("Note", story, new Color(1f, 0.97f, 0.88f), -4f);
            note = (RectTransform)card.parent;
            note.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-360, -10), new Vector2(560, 760));
            UiMotion.Intro(note, new Vector2(-900, 0), 0.05f, 0.95f, -14f, 0.7f);
            UiTheme.Tape(card, new Vector2(0.5f, 1f), new Vector2(0, -6), 3f, 160f);
            for (int k = 0; k < 16; k++)
            {
                var rule = UiKit.Image("Rule", card, new Color(0.55f, 0.7f, 0.9f, 0.35f), false);
                rule.raycastTarget = false;
                rule.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -150 - k * 38), new Vector2(-30, -148 - k * 38));
            }
            var margin = UiKit.Image("Margin", card, new Color(0.95f, 0.45f, 0.45f, 0.4f), false);
            margin.raycastTarget = false;
            margin.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(64, 20), new Vector2(66, -20));
            noteHeader = UiTheme.Label("Header", card, "", UiTheme.Hand, 30, UiTheme.InkSoft, TextAnchor.UpperLeft);
            noteHeader.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(84, -100), new Vector2(-30, -48));
            noteList = UiKit.Rect("Lines", card).Place(Vector2.zero, Vector2.one, new Vector2(84, 30), new Vector2(-36, -120));
            var v = UiKit.Vertical(noteList.gameObject, 18, null, TextAnchor.UpperLeft);
            v.childControlHeight = true;
            note.gameObject.SetActive(false);
        }

        /// <summary>Full-screen title card at the start of each chapter.</summary>
        void BuildChapterCard()
        {
            chapterCard = UiKit.Rect("Chapter Card", root).Fill();
            chapterGroup = chapterCard.gameObject.AddComponent<CanvasGroup>();
            var bg = UiKit.Image("Bg", chapterCard, new Color(0.13f, 0.11f, 0.16f, 0.96f), false);
            bg.rectTransform.Fill();
            chapterNumeral = UiTheme.Label("Numeral", chapterCard, "", UiTheme.Body, 30, UiTheme.Accent, TextAnchor.MiddleCenter);
            chapterNumeral.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1400, 50));
            chapterName = UiTheme.Label("Name", chapterCard, "", UiTheme.Display, 110, new Color(1f, 0.97f, 0.9f), TextAnchor.MiddleCenter);
            chapterName.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(1600, 150));
            chapterYears = UiTheme.Label("Years", chapterCard, "", UiTheme.Hand, 44, new Color(1f, 0.85f, 0.6f), TextAnchor.MiddleCenter);
            chapterYears.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(1200, 60));
            chapterIntro = UiTheme.Label("Intro", chapterCard, "", UiTheme.Hand, 36, new Color(0.85f, 0.82f, 0.9f), TextAnchor.UpperCenter);
            chapterIntro.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(1100, 200));
            UiMotion.Intro(chapterNumeral.rectTransform, new Vector2(0, 30), 0.3f, 1f, 0f, 0.9f, false);
            UiMotion.Intro(chapterName.rectTransform, new Vector2(0, -30), 0.5f, 0.92f, 0f, 1.1f, false);
            UiMotion.Intro(chapterYears.rectTransform, new Vector2(0, -20), 0.9f, 1f, 0f, 0.9f, false);
            UiMotion.Intro(chapterIntro.rectTransform, new Vector2(0, -20), 1.3f, 1f, 0f, 1f, false);
            chapterCard.gameObject.SetActive(false);
        }

        IEnumerator PlayChapterCard(ChapterDef chapter)
        {
            chapterNumeral.text = $"CHAPTER {chapter.Numeral}";
            chapterName.text = chapter.Title;
            chapterYears.text = chapter.Years;
            chapterIntro.text = chapter.Intro;
            chapterCard.gameObject.SetActive(true);
            chapterCard.SetAsLastSibling();
            Sfx.Instance?.Chapter();
            for (float t = 0; t < 0.6f; t += Time.deltaTime) { chapterGroup.alpha = t / 0.6f; yield return null; }
            chapterGroup.alpha = 1f;
            for (float t = 0; t < 3.6f * GameSettings.TextDelayScale && !skipMessages; t += Time.deltaTime) yield return null;
            skipMessages = false;
            for (float t = 0; t < 0.6f; t += Time.deltaTime) { chapterGroup.alpha = 1f - t / 0.6f; yield return null; }
            chapterCard.gameObject.SetActive(false);
        }

        /// <summary>
        /// The person you're packing for texts (or writes to) you about the trip. Coming back to a waiting
        /// trunk, the texts are all there at once (no chapter card) and the button goes straight back to it.
        /// </summary>
        public void ShowStory(LevelDef level, int tripIndex, int tripCount, int waiting = 0, string bestSoFar = null)
        {
            ShowOnly(story);
            string sender = string.IsNullOrEmpty(level.Sender) ? "Mom" : level.Sender;
            ShowConversation(sender, level.Messages, waiting > 0 ? "BACK TO PACKING" : "LET'S PACK!", true, level.IsNote,
                waiting > 0 || !level.IsFirstInChapter ? null : level.Chapter, waiting > 0);
            tripNumber.text = $"CHAPTER {level.Chapter.Numeral}  ·  TRIP {tripIndex + 1} OF {tripCount}";
            tripPlace.text = level.Year > 0 ? $"{level.Trip} · {level.Year}" : level.Trip;
            tripTitle.text = level.Title;
            tripTitle.fontSize = 66;
            while (tripTitle.fontSize > 34 && tripTitle.preferredWidth > 560f) tripTitle.fontSize--;
            int req = level.Required.Count, bonus = level.Bonus.Count;
            tripDetails.text = $"Today's ride: <b>{level.Vehicle}</b>\nTrunk: {level.Size.x} wide, {level.Size.y} tall, {level.Size.z} deep\n" +
                               $"To pack: {req} essentials + {bonus} extras" +
                               (waiting > 0 ? $"\n<color=#{ColorUtility.ToHtmlStringRGB(UiTheme.AccentInk)}><b>Your trunk is waiting: {waiting} packed</b></color>" : "") +
                               (string.IsNullOrEmpty(bestSoFar) ? "" : $"\n<b>{bestSoFar}</b>");
            TripCardBestSoFar = bestSoFar;
            ((RectTransform)tripTitle.transform.parent.parent).gameObject.SetActive(true);
        }

        /// <summary>The trip card's "best so far" line, if it has one (for the self-test).</summary>
        public string TripCardBestSoFar { get; private set; }

        /// <summary>Whether the trip card's details fit their box (for the self-test).</summary>
        public bool TripDetailsFit => tripDetails.preferredHeight <= tripDetails.rectTransform.rect.height + 0.5f;

        /// <summary>The last note, before the family album.</summary>
        public void ShowEnding()
        {
            ShowOnly(story);
            ShowConversation("Grandma Rose", new[]
            {
                "Rosie fell asleep in the wagon on the way home, holding Mr. Buttons.",
                "Joe always said you were the best packer in the family. He was right.",
                "But the secret was never the packing, dear. It was who you were packing for.",
                "Now go and look at all those photos. I'll put the kettle on.",
            }, "OPEN THE FAMILY ALBUM", false, true, null);
            ((RectTransform)tripTitle.transform.parent.parent).gameObject.SetActive(false);
        }

        void ShowConversation(string sender, string[] messages, string buttonText, bool showBack, bool asNote = false, ChapterDef chapter = null, bool instant = false)
        {
            noteMode = asNote;
            phone.gameObject.SetActive(!asNote);
            note.gameObject.SetActive(asNote);
            noteHeader.text = sender == "Grandpa Joe" ? "A note from Grandpa Joe" : $"A note from {sender}";
            UiKit.Clear(noteList);
            phoneName.text = sender;
            phoneAvatarLetter.text = sender.Length > 0 ? sender.Split(' ').Last().Substring(0, 1).ToUpperInvariant() : "?";
            phoneAvatar.color = Color.HSVToRGB(Mathf.Abs(sender.GetHashCode() % 360) / 360f, 0.55f, 0.85f);
            startLabel.text = buttonText;
            backButton.gameObject.SetActive(showBack);
            startButton.transform.parent.gameObject.SetActive(false);
            skipMessages = instant;
            UiKit.Clear(messageList);
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = StartCoroutine(RevealMessages(messages ?? new string[0], chapter));
        }

        bool skipMessages;

        /// <summary>Story pacing honours the text-speed setting; a click or Space hurries it along.</summary>
        IEnumerator Pause(float seconds)
        {
            float total = seconds * GameSettings.TextDelayScale;
            for (float t = 0; t < total && !skipMessages; t += Time.deltaTime) yield return null;
        }

        IEnumerator RevealMessages(string[] messages, ChapterDef chapter)
        {
            if (chapter != null) yield return PlayChapterCard(chapter);
            yield return Pause(0.5f);
            foreach (var message in messages)
            {
                if (noteMode)
                {
                    var line = UiTheme.Label("Line", noteList, message, UiTheme.Hand, 34, new Color(0.18f, 0.22f, 0.42f), TextAnchor.UpperLeft);
                    line.lineSpacing = 1.05f;
                    var group = line.gameObject.AddComponent<CanvasGroup>();
                    Sfx.Instance?.Pencil();
                    float fade = skipMessages ? 0.15f : 0.9f * GameSettings.TextDelayScale;
                    for (float t = 0; t < fade; t += Time.deltaTime) { group.alpha = t / fade; yield return null; }
                    group.alpha = 1f;
                    yield return Pause(0.9f);
                    continue;
                }
                if (!skipMessages)
                {
                    var typing = Bubble("• • •", true);
                    float wait = Mathf.Clamp(message.Length * 0.018f, 0.5f, 1.1f) * GameSettings.TextDelayScale;
                    for (float t = 0; t < wait && !skipMessages; t += Time.deltaTime)
                    {
                        typing.GetComponentInChildren<Text>().color = new Color(0.5f, 0.5f, 0.55f, 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(t * 6f)));
                        yield return null;
                    }
                    Destroy(typing.gameObject);
                }
                var bubble = Bubble(message, false);
                Sfx.Instance?.Message();
                for (float t = 0; t < 0.22f; t += Time.deltaTime)
                {
                    float s = Ease.OutBack(t / 0.22f, 2f);
                    bubble.localScale = new Vector3(Mathf.LerpUnclamped(0.5f, 1f, s), Mathf.LerpUnclamped(0.5f, 1f, s), 1f);
                    yield return null;
                }
                bubble.localScale = Vector3.one;
                yield return Pause(0.5f);
            }
            skipMessages = false;
            startButton.transform.parent.gameObject.SetActive(true);
            messageRoutine = null;
        }

        RectTransform Bubble(string text, bool typing)
        {
            const float maxText = 330f;
            var holder = UiKit.Rect("Bubble", messageList);
            holder.pivot = new Vector2(0f, 1f);
            var img = UiKit.Image("Fill", holder, typing ? new Color(0.88f, 0.89f, 0.92f) : Color.white);
            img.pixelsPerUnitMultiplier = 0.9f;
            img.rectTransform.Fill();
            var t = UiTheme.Label("Text", holder, text, UiTheme.Body, 25, UiTheme.Ink, TextAnchor.MiddleLeft);
            float width = Mathf.Min(maxText, t.preferredWidth + 2f);
            t.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(20, 12), new Vector2(-20, -12));
            var settings = t.GetGenerationSettings(new Vector2(width, 0));
            float height = t.cachedTextGeneratorForLayout.GetPreferredHeight(text, settings) / t.pixelsPerUnit;
            var le = holder.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width + 40f;
            le.preferredHeight = height + 26f;
            holder.sizeDelta = new Vector2(width + 40f, height + 26f);
            return holder;
        }

        // ================================================================== HUD

        void BuildHud()
        {
            hud = UiKit.Rect("HUD", root).Fill();

            // "FRAGILE" stamps that float over fragile things on the blanket (behind every panel).
            fragileLayer = UiKit.Rect("Fragile Tags", hud).Fill();
            var fragileGroup = fragileLayer.gameObject.AddComponent<CanvasGroup>();
            fragileGroup.blocksRaycasts = false;
            fragileGroup.interactable = false;

            // Luggage tag with the trip name.
            var tag = UiTheme.Card("Trip Tag", hud, UiTheme.Kraft, -1.5f);
            ((RectTransform)tag.parent).Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -30), new Vector2(680, 178));
            UiMotion.Intro(tag.parent, new Vector2(0, 260), 0.05f, 1f, -8f, 0.65f);
            tag.GetComponent<Image>().raycastTarget = false;
            var hole = UiKit.Image("Hole", tag, new Color(0.35f, 0.27f, 0.2f), false);
            hole.sprite = UiTheme.Circle;
            hole.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34, 0), new Vector2(26, 26));
            var holeRing = UiKit.Image("Eyelet", tag, new Color(0.85f, 0.8f, 0.7f), false);
            holeRing.sprite = UiTheme.Ring;
            holeRing.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34, 0), new Vector2(40, 40));
            tagTrip = UiTheme.Label("Trip", tag, "", UiTheme.Body, 18, new Color(0.3f, 0.22f, 0.15f), TextAnchor.UpperLeft);
            tagTrip.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(70, -46), new Vector2(-20, -16));
            tagTitle = UiTheme.Label("Title", tag, "", UiTheme.Display, 48, UiTheme.Ink, TextAnchor.UpperLeft);
            tagTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(68, -104), new Vector2(-20, -40));
            tagBlurb = UiTheme.Label("Blurb", tag, "", UiTheme.Hand, 27, new Color(0.25f, 0.18f, 0.12f), TextAnchor.UpperLeft);
            tagBlurb.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(70, 10), new Vector2(-20, -104));

            var buttons = UiKit.Rect("Buttons", hud).Pin(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -36), new Vector2(546, 60));
            UiKit.Horizontal(buttons.gameObject, 12, TextAnchor.MiddleRight).childForceExpandWidth = true;
            hintButton = UiTheme.Pill("Hint", buttons, "HINT", UiTheme.Accent, 24, () => HintPressed?.Invoke());
            UiKit.Size(hintButton, 104, 56);
            UiKit.Size(UiTheme.Pill("Undo", buttons, "UNDO", UiTheme.Teal, 24, () => UndoPressed?.Invoke()), 130, 56);
            UiKit.Size(UiTheme.Pill("Restart", buttons, "RESTART", UiTheme.Night, 24, () => RestartPressed?.Invoke()), 150, 56);
            UiKit.Size(UiTheme.Pill("Pause", buttons, "MENU", UiTheme.Night, 24, () => PausePressed?.Invoke()), 110, 56);
            UiMotion.Intro(buttons, new Vector2(0, 140), 0.12f, 1f, 0f, 0.5f);

            // Packing checklist on a taped notepad.
            var list = UiTheme.Card("Packing List", hud, UiTheme.Paper, 1f);
            ((RectTransform)list.parent).Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-470, 40), new Vector2(-40, -118));
            UiMotion.Intro(list.parent, new Vector2(620, 0), 0.08f, 0.96f, 6f, 0.65f);
            UiTheme.Tape(list, new Vector2(0.5f, 1f), new Vector2(0, -2), 2f, 170f);
            var margin = UiKit.Image("Margin", list, new Color(0.95f, 0.45f, 0.45f, 0.45f), false);
            margin.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(64, 12), new Vector2(66, -12));
            margin.raycastTarget = false;
            var header = UiTheme.Label("Header", list, "PACKING LIST", UiTheme.Display, 34, UiTheme.Ink, TextAnchor.MiddleLeft);
            header.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(82, -78), new Vector2(-20, -26));
            // Beside the heading: the stars closing the trunk right now would earn.
            meterRect = UiKit.Rect("Star Meter", list).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-124, -76), new Vector2(-24, -32));
            UiKit.Horizontal(meterRect.gameObject, 3, TextAnchor.MiddleRight);
            meterStars = new Image[3];
            for (int i = 0; i < 3; i++) meterStars[i] = UiKit.StarImage(meterRect, false, 30);
            listFrom = UiTheme.Label("From", list, "", UiTheme.Hand, 24, UiTheme.AccentInk, TextAnchor.MiddleLeft);
            listFrom.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(84, -108), new Vector2(-26, -76));

            // The rows scroll (wheel over the list) when a big trip doesn't fit at a readable size.
            listViewport = UiKit.Rect("Items", list).Place(Vector2.zero, Vector2.one, new Vector2(18, 214), new Vector2(-18, -114));
            listViewport.gameObject.AddComponent<RectMask2D>();
            var listHit = listViewport.gameObject.AddComponent<Image>();
            listHit.color = new Color(1f, 1f, 1f, 0f);
            itemList = UiKit.Rect("Rows", listViewport);
            itemList.anchorMin = new Vector2(0f, 1f);
            itemList.anchorMax = new Vector2(1f, 1f);
            itemList.pivot = new Vector2(0.5f, 1f);
            itemList.anchoredPosition = Vector2.zero;
            itemList.sizeDelta = Vector2.zero;
            UiKit.Vertical(itemList.gameObject, 2);
            itemList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            listScroll = listViewport.gameObject.AddComponent<ScrollRect>();
            listScroll.content = itemList;
            listScroll.viewport = listViewport;
            listScroll.horizontal = false;
            listScroll.movementType = ScrollRect.MovementType.Clamped;
            listScroll.inertia = false;
            listScroll.scrollSensitivity = 28f;
            moreBelow = UiTheme.Label("More", list, "more below  ▾", UiTheme.Hand, 21, UiTheme.AccentInk, TextAnchor.MiddleRight);
            moreBelow.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(30, 194), new Vector2(-28, 216));
            moreBelow.raycastTarget = false;
            moreBelow.gameObject.SetActive(false);
            moreAbove = UiTheme.Label("More Above", list, "▴  more above", UiTheme.Hand, 21, UiTheme.AccentInk, TextAnchor.MiddleRight);
            moreAbove.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -108), new Vector2(-28, -80));
            moreAbove.raycastTarget = false;
            moreAbove.gameObject.SetActive(false);

            countsText = UiTheme.Label("Counts", list, "", UiTheme.Body, 21, UiTheme.Ink, TextAnchor.MiddleLeft);
            countsText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(30, 168), new Vector2(-24, 204));
            var spaceBg = UiKit.Image("Space", list, UiTheme.PaperShade);
            spaceBg.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(30, 128), new Vector2(-24, 152));
            spaceBg.raycastTarget = false;
            spaceFill = UiKit.Image("Fill", spaceBg.transform, UiTheme.Teal).rectTransform;
            spaceFill.Place(Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            spaceFill.GetComponent<Image>().raycastTarget = false;
            spaceText = UiTheme.Label("SpaceText", spaceBg.transform, "", UiTheme.Hand, 21, UiTheme.Ink, TextAnchor.MiddleCenter);
            spaceText.rectTransform.Fill();

            closeButton = UiTheme.Pill("Close", list, "CLOSE THE TRUNK", UiTheme.Accent, 30, () => ClosePressed?.Invoke(), out closeLabel);
            closePulse = (RectTransform)closeButton.transform.GetChild(1);
            closeFace = closePulse.GetComponent<Image>();
            ((RectTransform)closeButton.transform).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 26), new Vector2(-20, 108));

            // Card for whatever's in your hands.
            var held = UiTheme.Card("Held", hud, UiTheme.Paper, -1f);
            heldPanel = (RectTransform)held.parent;
            heldPanel.Pin(new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 84), new Vector2(620, 206));
            UiMotion.Intro(heldPanel, new Vector2(-700, -40), 0f, 0.9f, -10f, 0.42f);
            held.GetComponent<Image>().raycastTarget = false;
            UiTheme.Tape(held, new Vector2(0, 1), new Vector2(30, -8), -30f, 110f);
            heldName = UiTheme.Label("Name", held, "", UiTheme.Display, 38, UiTheme.Ink, TextAnchor.UpperLeft);
            heldName.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, -68), new Vector2(-150, -18));
            heldStamps = UiKit.Rect("Stamps", held).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, -112), new Vector2(-20, -72));
            UiKit.Horizontal(heldStamps.gameObject, 12, TextAnchor.MiddleLeft);
            heldDesc = UiTheme.Label("Desc", held, "", UiTheme.Hand, 28, UiTheme.InkSoft, TextAnchor.UpperLeft);
            heldDesc.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(28, 12), new Vector2(-24, -118));
            var putBack = UiTheme.Pill("Put Back", held, "PUT BACK", UiTheme.Night, 20, () => PutBackPressed?.Invoke());
            ((RectTransform)putBack.transform).Pin(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -18), new Vector2(130, 46));

            hoverText = UiTheme.Label("Hover", hud, "", UiTheme.Display, 30, Color.white, TextAnchor.MiddleLeft);
            hoverText.rectTransform.Pin(new Vector2(0, 0), new Vector2(0, 0), new Vector2(46, 104), new Vector2(1000, 44));
            var ho = hoverText.gameObject.AddComponent<Outline>();
            ho.effectColor = new Color(0.1f, 0.08f, 0.15f, 0.85f);
            ho.effectDistance = new Vector2(2, -2);

            var toastCard = UiTheme.Card("Toast", hud, UiTheme.Night, 0f);
            toastHolder = (RectTransform)toastCard.parent;
            toastHolder.Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-200, 300), new Vector2(860, 74));
            toastCard.GetComponent<Image>().raycastTarget = false;
            toastGroup = toastHolder.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toast = UiTheme.Label("Text", toastCard, "", UiTheme.Body, 27, Color.white, TextAnchor.MiddleCenter);
            toast.rectTransform.Fill(10);
            BuildTip();

            var keys = keyHints = UiKit.Rect("Keys", hud).Pin(new Vector2(0, 0), new Vector2(0, 0), new Vector2(44, 22), new Vector2(1380, 44));
            UiMotion.Intro(keys, new Vector2(0, -90), 0.25f, 1f, 0f, 0.5f, false);
            // Two sets of hints in the same strip: keyboard, or controller once a gamepad is in use.
            var kbHints = UiKit.Rect("Keyboard", keys).Fill();
            var kh = UiKit.Horizontal(kbHints.gameObject, 22, TextAnchor.MiddleLeft);
            kh.childControlWidth = true;
            keyboardHintsRow = kbHints;
            BuildKeyboardHints();
            Bindings.Changed += BuildKeyboardHints;
            var padHints = UiKit.Rect("Gamepad", keys).Fill();
            var ph = UiKit.Horizontal(padHints.gameObject, 18, TextAnchor.MiddleLeft);
            ph.childControlWidth = true;
            Hint(padHints, "A", "grab / drop", 0);
            Hint(padHints, "X", "turn", 1);
            Hint(padHints, "Y", "tip", 2);
            Hint(padHints, "RB", "roll", 3);
            Hint(padHints, "D-PAD", "shelf", 6);
            Hint(padHints, "B", "put back", 9);
            Hint(padHints, "VIEW", "undo", 5);
            Hint(padHints, "R-STICK", "look", 8);
            padXRayHint = UiTheme.KeyHint(padHints, "L3", "x-ray");
            hintRank[padXRayHint] = 7;
            Hint(padHints, "D-PAD >", "close", 4);
            padHintsRow = padHints;
            void ShowPadHints()
            {
                kbHints.gameObject.SetActive(!GamepadCursor.Active);
                padHints.gameObject.SetActive(GamepadCursor.Active);
                hudLayoutWidth = -1f;
            }
            ShowPadHints();
            GamepadCursor.ActiveChanged += ShowPadHints;
        }

        public void ShowHud(LevelDef level, IReadOnlyList<PackItem> items)
        {
            ShowOnly(hud);
            HideHudForCutscene(false);
            tagTrip.text = $"TRIP {level.Index + 1}  ·  {(level.Trip ?? "").ToUpperInvariant()}  ·  {level.Vehicle.ToUpperInvariant()}";
            tagTitle.text = level.Title;
            tagBlurb.text = level.Blurb;
            listFrom.text = string.IsNullOrEmpty(level.PackFor) ? "" : "for " + level.PackFor;
            heldPanel.gameObject.SetActive(false);
            hoverText.text = "";
            toastGroup.alpha = 0f;
            toastTimer = 0f;
            totalSpace = Mathf.Max(1, level.FreeCells);
            spaceShown = spaceTarget = 0f;
            meterShown = 0;
            foreach (var star in meterStars)
            {
                star.color = MeterUnlit;
                star.transform.localScale = Vector3.one;
            }
            keyHints.gameObject.SetActive(GameSettings.KeyHints);
            BuildFragileTags(items);
            BuildListRows(items);
            ShowResultsPanel(false);
        }

        IReadOnlyList<PackItem> listItems;
        float listBuiltFor = -1f;
        (PackItem Held, bool CanClose, bool AllPacked, int FreeCells) lastRefresh;

        /// <summary>
        /// One row per item, sized to the list's actual height (which depends on the screen shape and
        /// the interface size), so even the 25-item trips fit without rows running into each other.
        /// </summary>
        void BuildListRows(IReadOnlyList<PackItem> items)
        {
            listItems = items;
            rows.Clear();
            UiKit.Clear(itemList);
            float available = listViewport.rect.height > 50f ? listViewport.rect.height : 1080f - 118f - 40f - 214f - 114f;
            listBuiltFor = available;
            // Never below a readable 20 units (or 12 screen pixels of text); if that doesn't fit, the list
            // scrolls instead.
            int minFont = Mathf.Max(14, MinReadableFont);
            float rowHeight = Mathf.Clamp(available / Mathf.Max(1, items.Count) - 2f, Mathf.Max(20f, minFont + 6f), 46f);
            listScrolls = items.Count * (rowHeight + 2f) > available + 1f;
            listScroll.enabled = listScrolls;
            itemList.anchoredPosition = Vector2.zero;
            // Glyphs are about 1.1x the font size tall: keep them inside the row.
            int fontSize = Mathf.Max(minFont, Mathf.Min(rowHeight < 30f ? 22 : rowHeight < 38f ? 25 : 28, Mathf.Clamp(Mathf.FloorToInt((rowHeight + 1f) / 1.18f), 13, 28)));
            foreach (var item in items)
            {
                var captured = item;
                var rowRt = UiKit.Rect(item.Def.Name, itemList);
                UiKit.Size(rowRt.gameObject.AddComponent<LayoutElement>(), -1, rowHeight);
                var hit = rowRt.gameObject.AddComponent<Image>();
                hit.color = new Color(1, 1, 1, 0);
                var button = rowRt.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.onClick.AddListener(() => ItemRowClicked?.Invoke(captured));

                var marker = UiKit.Image("Marker", rowRt, UiTheme.Marker, false);
                marker.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(50, 2), new Vector2(-6, -2));
                marker.raycastTarget = false;
                var line = UiKit.Image("Rule", rowRt, new Color(0.55f, 0.7f, 0.9f, 0.35f), false);
                line.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 2));
                line.raycastTarget = false;

                float box = Mathf.Clamp(rowHeight - 6f, 9f, 26f);
                var boxOuter = UiKit.Image("Box", rowRt, UiTheme.InkSoft, true);
                boxOuter.pixelsPerUnitMultiplier = 4f;
                boxOuter.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26, 0), new Vector2(box, box));
                boxOuter.raycastTarget = false;
                var boxInner = UiKit.Image("Inner", boxOuter.transform, UiTheme.Paper, true);
                boxInner.pixelsPerUnitMultiplier = 4f;
                boxInner.rectTransform.Fill(2.5f);
                boxInner.raycastTarget = false;
                var check = UiKit.Image("Check", rowRt, UiTheme.Good, false);
                check.sprite = UiTheme.Check;
                check.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(31, 5), new Vector2(box * 1.5f, box * 1.5f));
                check.raycastTarget = false;

                var swatchRing = UiKit.Image("SwatchRing", rowRt, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.45f), false);
                swatchRing.sprite = UiTheme.Dot;
                swatchRing.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(82, 0), new Vector2(17, 17));
                swatchRing.raycastTarget = false;
                var swatch = UiKit.Image("Swatch", rowRt, item.Def.Colors[0], false);
                swatch.sprite = UiTheme.Dot;
                swatch.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(82, 0), new Vector2(14, 14));
                swatch.raycastTarget = false;
                int stampCount = (item.Def.Fragile ? 1 : 0) + (item.IsBonus ? 1 : 0);
                var name = UiTheme.Label("Name", rowRt, item.Def.Name, UiTheme.Hand, fontSize, UiTheme.Ink, TextAnchor.MiddleLeft);
                name.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(96, 0), new Vector2(-8 - stampCount * 74, 0));
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                // Shrink to stay on one line beside the stamps.
                float room = 430f - 36f - 96f - 8f - stampCount * 74f;
                while (name.fontSize > minFont && name.preferredWidth > room) name.fontSize--;
                var strike = UiKit.Image("Strike", rowRt, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.55f), false);
                strike.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(92, -1), new Vector2(Mathf.Min(name.preferredWidth, 300f - stampCount * 74f) + 10, 3));
                strike.raycastTarget = false;

                var stamps = UiKit.Rect("Stamps", rowRt).Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-170, 0), new Vector2(-4, 0));
                UiKit.Horizontal(stamps.gameObject, 6, TextAnchor.MiddleRight);
                // Stamps are 28 units tall; on short rows they get a thinner border rather than smaller
                // letters, and only shrink if even that doesn't fit.
                int stampFont = Mathf.Max(14, minFont);
                float stampPad = Mathf.Clamp(rowHeight - stampFont, 4f, 14f);
                if (item.Def.Fragile) UiTheme.StampLabel(stamps, "FRAGILE", UiTheme.Stamp, stampFont, -5f, stampPad);
                if (item.IsBonus) UiTheme.StampLabel(stamps, "EXTRA", new Color(0.85f, 0.6f, 0.1f), stampFont, 4f, stampPad);
                stamps.localScale = Vector3.one * Mathf.Clamp01((rowHeight + 2f) / (stampFont + stampPad + 2f));

                rows[item] = new ItemRow { Marker = marker, Check = check, Strike = strike, Name = name, Stamps = stamps, StrikeWidth = strike.rectTransform.sizeDelta.x };
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(itemList);
        }

        /// <summary>Scroll the packing list so this item's row is fully in view.</summary>
        void ScrollToRow(PackItem item)
        {
            if (!listScrolls || item == null || !rows.TryGetValue(item, out var row)) return;
            var rt = (RectTransform)row.Name.transform.parent;
            float top = -(rt.localPosition.y + rt.rect.yMax);
            float bottom = top + rt.rect.height;
            float view = listViewport.rect.height;
            float offset = itemList.anchoredPosition.y;
            if (top < offset) offset = top;
            else if (bottom > offset + view) offset = bottom - view;
            offset = Mathf.Clamp(offset, 0f, Mathf.Max(0f, itemList.rect.height - view));
            itemList.anchoredPosition = new Vector2(0f, offset);
        }

        /// <summary>Is this item's row entirely inside the visible part of the list? (self-test)</summary>
        public bool RowVisible(PackItem item)
        {
            if (!rows.TryGetValue(item, out var row)) return false;
            var rt = (RectTransform)row.Name.transform.parent;
            float top = -(rt.localPosition.y + rt.rect.yMax) - itemList.anchoredPosition.y;
            return top >= -1f && top + rt.rect.height <= listViewport.rect.height + 1f;
        }

        public bool ListScrolls => listScrolls;

        public void RefreshHud(IReadOnlyList<PackItem> items, PackItem held, bool canClose, bool allPacked, int freeCells)
        {
            lastRefresh = (held, canClose, allPacked, freeCells);
            int req = 0, reqDone = 0, bonus = 0, bonusDone = 0;
            foreach (var item in items)
            {
                bool packed = item.State == ItemState.Packed || item.State == ItemState.Dropping;
                if (item.IsBonus) { bonus++; if (packed) bonusDone++; }
                else { req++; if (packed) reqDone++; }

                if (!rows.TryGetValue(item, out var row)) continue;
                if (packed && !row.Packed) AnimateCheck(row);
                row.Packed = packed;
                row.Check.enabled = packed;
                row.Strike.enabled = packed;
                row.Marker.enabled = item == held;
                row.Name.color = packed ? new Color(UiTheme.InkSoft.r, UiTheme.InkSoft.g, UiTheme.InkSoft.b, 0.75f) : UiTheme.Ink;
                row.Stamps.gameObject.SetActive(!packed);
            }

            countsReq = req;
            countsBonus = bonus;
            countsText.text = CountsLine(reqDone, req, bonusDone, bonus);
            SetMeter(GameController.StarRating(reqDone == req, bonusDone, bonus));
            spaceTarget = Mathf.Clamp01(1f - freeCells / (float)totalSpace);
            spaceText.text = freeCells == 0 ? "not a single inch to spare!" : $"{freeCells} space{(freeCells == 1 ? "" : "s")} left in the trunk";

            closeButton.interactable = canClose;
            closeLabel.text = canClose ? (allPacked ? "EVERYTHING FITS!" : "CLOSE THE TRUNK") : "PACK THE ESSENTIALS";
            closeFace.color = canClose ? (allPacked ? UiTheme.Good : UiTheme.Accent) : new Color(0.7f, 0.66f, 0.6f);
            pulseClose = canClose;
        }

        static readonly string CountsGood = UiKit.Hex(UiTheme.Good), CountsInk = UiKit.Hex(UiTheme.Ink), CountsGold = UiKit.Hex(new Color(0.8f, 0.55f, 0.05f));

        static string CountsLine(int reqDone, int req, int bonusDone, int bonus) =>
            $"Essentials <b><color={(reqDone == req ? CountsGood : CountsInk)}>{reqDone}/{req}</color></b>      " +
            $"Extras <b><color={CountsGold}>{bonusDone}/{bonus}</color></b>";

        /// <summary>How far the widest counts line this trip can show runs past its one line, in canvas units (layout self-test).</summary>
        public float CountsOverflow()
        {
            string saved = countsText.text;
            countsText.text = CountsLine(countsReq, countsReq, countsBonus, countsBonus);
            float over = countsText.preferredWidth - countsText.rectTransform.rect.width;
            countsText.text = saved;
            return over;
        }

        /// <summary>Space between the end of the PACKING LIST heading and the star meter, in canvas units (layout self-test).</summary>
        public float MeterGapToHeading()
        {
            var header = meterRect.parent.Find("Header") as RectTransform;
            var text = header.GetComponent<Text>();
            float headingRight = header.offsetMin.x + text.preferredWidth;
            float meterLeft = ((RectTransform)meterRect.parent).rect.width + meterRect.offsetMin.x;
            return meterLeft - headingRight;
        }

        /// <summary>Stars the meter shows: what closing the trunk now would earn.</summary>
        public int MeterStars => meterShown;

        void SetMeter(int stars)
        {
            if (stars == meterShown) return;
            for (int i = 0; i < meterStars.Length; i++)
            {
                meterStars[i].color = i < stars ? UiTheme.Gold : MeterUnlit;
                if (i < meterShown || i >= stars) continue;
                // A newly earned star pops (and chimes when it's the one a drop just earned).
                var rt = meterStars[i].rectTransform;
                if (stars == meterShown + 1) Sfx.Instance?.Star(i);
                UiMotion.Run(this, 0.4f, t =>
                {
                    if (rt == null) return;
                    float k = Ease.OutBack(t, 3f);
                    rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, k);
                    rt.localRotation = Quaternion.Euler(0, 0, (1f - t) * 50f);
                });
            }
            meterShown = stars;
        }

        void AnimateCheck(ItemRow row)
        {
            Sfx.Instance?.Check();
            var check = row.Check.rectTransform;
            var strike = row.Strike.rectTransform;
            float width = row.StrikeWidth;
            UiMotion.Run(this, 0.35f, t =>
            {
                if (check == null || strike == null) return;
                float s = Ease.OutBack(t, 2.2f);
                check.localScale = new Vector3(s, s, 1f);
                check.localRotation = Quaternion.Euler(0, 0, (1f - t) * -25f);
                strike.sizeDelta = new Vector2(width * Ease.OutCubic(Mathf.Clamp01(t * 1.6f)), strike.sizeDelta.y);
            });
        }

        void UpdateHudMotion()
        {
            if (spaceFill == null) return;
            float dt = UiTime.Delta;
            spaceShown = Mathf.Lerp(spaceShown, spaceTarget, 1f - Mathf.Exp(-dt * 7f));
            spaceFill.anchorMax = new Vector2(spaceShown, 1f);
            float want = toastTimer > 0f ? 1f : 0f;
            toastShown = Mathf.MoveTowards(toastShown, want, dt * (want > 0f ? 7f : 3f));
            toastHolder.anchoredPosition = new Vector2(-200, 300 - (1f - Ease.OutBack(toastShown, 1.6f)) * 50f);
            toastHolder.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, Ease.OutCubic(toastShown));
        }

        public void ShowHeld(PackItem item)
        {
            heldPanel.gameObject.SetActive(item != null);
            hoverText.gameObject.SetActive(item == null);
            if (item == null) return;
            ScrollToRow(item);
            heldName.text = item.Def.Name;
            UiKit.Clear(heldStamps);
            UiTheme.StampLabel(heldStamps, item.IsBonus ? "EXTRA" : "ESSENTIAL", item.IsBonus ? new Color(0.85f, 0.6f, 0.1f) : UiTheme.Teal, 18, -3f);
            if (item.Def.Fragile) UiTheme.StampLabel(heldStamps, "FRAGILE · NOTHING ON TOP", UiTheme.Stamp, 18, 2f);
            UiTheme.StampLabel(heldStamps, $"{item.Def.Volume} SPACE{(item.Def.Volume == 1 ? "" : "S")}", UiTheme.InkSoft, 18, -1f);
            heldDesc.text = "“" + item.Def.Description + "”";
        }

        public void SetHover(string text) => hoverText.text = text ?? "";

        // ------------------------------------------------------------------ Grandpa's tips

        RectTransform tipHolder;
        CanvasGroup tipGroup;
        Text tipText;
        bool tipShown;

        void BuildTip()
        {
            var card = UiTheme.Card("Tip", hud, UiTheme.Paper, 1.2f);
            tipHolder = (RectTransform)card.parent;
            // Top, in the gap between the trip tag and the packing list (sized to fit in ShowTip).
            tipHolder.Pin(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(TipLeft, TipTop), new Vector2(690, 136));
            tipGroup = tipHolder.gameObject.AddComponent<CanvasGroup>();
            tipGroup.blocksRaycasts = false;
            tipGroup.interactable = false;
            tipGroup.alpha = 0f;
            UiTheme.Tape(card, new Vector2(0.5f, 1f), new Vector2(0, -4), -3f, 120f);
            var header = UiTheme.Label("Header", card, "GRANDPA'S TIP", UiTheme.Display, 20, UiTheme.AccentInk, TextAnchor.UpperLeft);
            header.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -50), new Vector2(-24, -20));
            tipText = UiTheme.Label("Text", card, "", UiTheme.Hand, 30, UiTheme.Ink, TextAnchor.UpperLeft);
            tipText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(30, 10), new Vector2(-24, -48));
            tipHolder.gameObject.SetActive(false);
        }

        public bool TipVisible => tipShown;

        const float TipLeft = 744f, TipTop = -128f, TipRightMargin = 490f;

        Vector2 tipHome = new Vector2(TipLeft, TipTop);

        /// <summary>
        /// Between the trip tag and the packing list when there's room (wrapping taller as it narrows);
        /// on narrow canvases (4:3, big interface sizes) under the trip tag instead.
        /// </summary>
        void PlaceTip()
        {
            float gap = root.rect.width - TipLeft - TipRightMargin;
            float width;
            if (gap >= 480f)
            {
                width = Mathf.Min(690f, gap);
                tipHome = new Vector2(TipLeft, TipTop);
            }
            else
            {
                width = Mathf.Min(660f, root.rect.width - ListReserve - 2f * HudMargin - 30f);
                tipHome = new Vector2(HudMargin, -250f);
            }
            tipHolder.sizeDelta = new Vector2(width, width < 600f ? 176f : 136f);
        }

        public void ShowTip(string text)
        {
            PlaceTip();
            tipText.text = text;
            tipShown = true;
            tipHolder.gameObject.SetActive(true);
        }

        public void HideTip() => tipShown = false;

        void UpdateTip()
        {
            if (tipHolder == null || !tipHolder.gameObject.activeSelf) return;
            tipGroup.alpha = Mathf.MoveTowards(tipGroup.alpha, tipShown ? 1f : 0f, UiTime.Delta * (tipShown ? 5f : 3.5f));
            // Drops in from above as it fades in, lifts away as it fades out.
            tipHolder.anchoredPosition = tipHome + new Vector2(0f, 40f * (1f - Ease.OutCubic(tipGroup.alpha)));
            if (!tipShown && tipGroup.alpha <= 0f) tipHolder.gameObject.SetActive(false);
        }

        /// <summary>Is a toast containing <paramref name="fragment"/> on screen right now?</summary>
        public bool ToastShowing(string fragment) =>
            toastGroup != null && toastGroup.alpha > 0.5f && toast.text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;

        public void Toast(string message, float seconds = 2.2f)
        {
            toast.text = message;
            if (toastHolder != null && root != null) PlaceToast();
            toastTimer = seconds;
            toastGroup.alpha = 1f;
        }

        RectTransform fragileLayer;
        readonly List<(PackItem Item, RectTransform Tag)> fragileTags = new List<(PackItem, RectTransform)>();

        void BuildFragileTags(IReadOnlyList<PackItem> items)
        {
            fragileTags.Clear();
            UiKit.Clear(fragileLayer);
            foreach (var item in items)
            {
                if (!item.Def.Fragile) continue;
                var tag = UiTheme.StampLabel(fragileLayer, "FRAGILE", UiTheme.Stamp, 18, -5f);
                tag.gameObject.SetActive(false);
                fragileTags.Add((item, tag));
            }
        }

        /// <summary>Keep each fragile item's stamp just above it while it waits on the blanket.</summary>
        public void UpdateFragileTags(bool show)
        {
            var cam = Camera.main;
            foreach (var (item, tag) in fragileTags)
            {
                bool on = show && cam != null && item != null && item.State == ItemState.Pile && !item.IsFalling;
                if (on)
                {
                    var c = item.Shape.Center;
                    var top = item.transform.position + new Vector3(c.x, item.Shape.Size.y + 0.25f, c.z);
                    var screen = cam.WorldToScreenPoint(top);
                    on = screen.z > 0f;
                    if (on && RectTransformUtility.ScreenPointToLocalPointInRectangle(fragileLayer, screen, null, out var local))
                        tag.anchoredPosition = local;
                }
                if (tag.gameObject.activeSelf != on) tag.gameObject.SetActive(on);
            }
        }

        public void HideHudForCutscene(bool hide)
        {
            hudHidden = hide;
            foreach (Transform child in hud) child.gameObject.SetActive(!hide);
            if (!hide)
            {
                heldPanel.gameObject.SetActive(false);
                keyHints.gameObject.SetActive(GameSettings.KeyHints);
            }
        }

        // ================================================================== ALBUM

        /// <summary>The family album: one polaroid per trip, photographed as each trunk closed.</summary>
        void BuildAlbum()
        {
            album = UiKit.Rect("Album", root).Fill();
            var bg = UiKit.Image("Bg", album, new Color(0.24f, 0.19f, 0.16f, 0.97f), false);
            bg.rectTransform.Fill();
            var page = UiTheme.Card("Page", album, new Color(0.96f, 0.92f, 0.84f), 0f, false);
            ((RectTransform)page.parent).Place(Vector2.zero, Vector2.one, new Vector2(40, 30), new Vector2(-40, -30));
            UiMotion.Intro(page.parent, new Vector2(0, -260), 0f, 0.96f, 0f, 0.6f);
            albumTitle = UiTheme.Label("Title", page, "", UiTheme.Display, 54, UiTheme.Ink, TextAnchor.MiddleCenter);
            albumTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -100), new Vector2(0, -26));
            albumGrid = UiKit.Rect("Grid", page).Place(Vector2.zero, Vector2.one, new Vector2(30, 150), new Vector2(-30, -115));
            var grid = albumGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(152, 214);
            grid.spacing = new Vector2(8, 22);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 11;
            grid.childAlignment = TextAnchor.UpperCenter;
            albumThanks = UiTheme.Label("Thanks", page, "", UiTheme.Hand, 40, UiTheme.Ink, TextAnchor.MiddleCenter);
            albumThanks.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 60), new Vector2(-40, 130));
            albumThanks.supportRichText = true;
            albumDone = UiTheme.Pill("Album Done", page, "CONTINUE", UiTheme.Accent, 26, () => AlbumClosed?.Invoke(), out albumBackLabel);
            ((RectTransform)albumDone.transform).Pin(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 30), new Vector2(300, 64));
            BuildAlbumZoom();
            album.gameObject.SetActive(false);
        }

        public void ShowAlbum(IReadOnlyList<LevelDef> levels, Func<int, int> starsFor, Func<string, Texture2D> photoFor, Func<string, Texture2D> fullPhotoFor, bool finale)
        {
            albumFullPhotoFor = fullPhotoFor;
            ShowOnly(album);
            albumFinale = finale;
            albumBackLabel.text = finale ? "CONTINUE" : "BACK";
            albumDone.GetComponent<PillHover>().IsBack = !finale;
            album.SetAsLastSibling();
            int first = levels.Count > 0 ? levels[0].Year : 0, last = levels.Count > 0 ? levels[levels.Count - 1].Year : 0;
            albumTitle.text = $"The Family Album  ·  {first} to {last}";
            albumThanks.text = "";
            albumDone.gameObject.SetActive(false);
            CloseAlbumZoom();
            albumPhotos.Clear();
            albumStarsFor = starsFor;
            UiKit.Clear(albumGrid);
            StartCoroutine(FillAlbum(levels, starsFor, photoFor));
        }

        IEnumerator FillAlbum(IReadOnlyList<LevelDef> levels, Func<int, int> starsFor, Func<string, Texture2D> photoFor)
        {
            yield return null;
            // Polaroids as big as the page allows, up to the 152x214 design, eleven to a row; the text
            // on them never drops below 12 screen pixels (a long title wraps onto a second line instead).
            const int Columns = 11;
            int rowsNeeded = Mathf.Max(1, Mathf.CeilToInt(levels.Count / (float)Columns));
            var grid = albumGrid.GetComponent<GridLayoutGroup>();
            var area = albumGrid.rect;
            float w = Mathf.Min(152f, (area.width - (Columns - 1) * grid.spacing.x) / Columns,
                (area.height - (rowsNeeded - 1) * grid.spacing.y) / rowsNeeded * 152f / 214f);
            float h = w * 214f / 152f, k = w / 152f;
            grid.cellSize = new Vector2(w, h);
            int minFont = Mathf.Max(13, MinReadableFont);
            int yearFont = Mathf.Max(Mathf.RoundToInt(14f * k), minFont);
            float yearTop = 4f * k + yearFont * 1.25f;

            var rng = new System.Random(3);
            foreach (var level in levels)
            {
                var slot = UiKit.Rect("Slot", albumGrid);
                var polaroid = UiTheme.Card("Polaroid", slot, Color.white, (float)(rng.NextDouble() * 10 - 5));
                var holder = (RectTransform)polaroid.parent;
                holder.Fill();

                var cap = UiTheme.Label("Caption", polaroid, level.Title, UiTheme.Hand, Mathf.Max(Mathf.RoundToInt(20f * k), minFont), UiTheme.Ink, TextAnchor.MiddleCenter);
                cap.horizontalOverflow = HorizontalWrapMode.Wrap;
                cap.verticalOverflow = VerticalWrapMode.Overflow;
                cap.lineSpacing = 0.85f;
                float lineWidth = w - 8f;
                while (cap.fontSize > minFont && cap.preferredWidth > lineWidth) cap.fontSize--;
                int lines = cap.preferredWidth > lineWidth ? 2 : 1;
                float capHeight = Mathf.Max(38f * k, lines * cap.fontSize * 1.05f + 4f);
                cap.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(4, yearTop - 2f), new Vector2(-4, yearTop - 2f + capHeight));
                var year = UiTheme.Label("Year", polaroid, level.Year > 0 ? level.Year.ToString() : "", UiTheme.Body, yearFont, UiTheme.InkSoft, TextAnchor.LowerCenter);
                year.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(4, 4f * k), new Vector2(-4, yearTop));

                // The photo fills what's left above the caption.
                float photoBottom = yearTop - 2f + capHeight + 2f * k;
                var photoArea = new Vector2(w - 20f * k, h - 10f * k - photoBottom);
                var photo = photoFor(level.Id);
                if (photo != null)
                {
                    var raw = UiKit.Rect("Photo", polaroid).gameObject.AddComponent<RawImage>();
                    raw.texture = photo;
                    raw.raycastTarget = false;
                    raw.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(10f * k, photoBottom), new Vector2(-10f * k, -10f * k));
                    // Crop the photo to the frame instead of stretching it.
                    float aspect = photo.width / (float)photo.height, frame = photoArea.x / Mathf.Max(1f, photoArea.y);
                    raw.uvRect = frame < aspect ? new Rect((1f - frame / aspect) * 0.5f, 0f, frame / aspect, 1f)
                                                : new Rect(0f, (1f - aspect / frame) * 0.5f, 1f, aspect / frame);
                }
                else
                {
                    var swatch = UiKit.Image("Photo", polaroid, Color.Lerp(level.BodyColor, Color.white, 0.25f), false);
                    swatch.raycastTarget = false;
                    swatch.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(10f * k, photoBottom), new Vector2(-10f * k, -10f * k));
                    var v = UiTheme.Label("Vehicle", swatch.transform, level.Vehicle, UiTheme.Display, Mathf.Max(Mathf.RoundToInt(18f * k), minFont), Color.white, TextAnchor.MiddleCenter);
                    v.rectTransform.Fill(6);
                }
                if (SealedFor != null && SealedFor(level.Index))
                    UiTheme.Seal(polaroid, 40f * k).Pin(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-12f * k, -12f * k), new Vector2(40f * k, 40f * k));
                if (starsFor(level.Index) == 0)
                {
                    var g = holder.gameObject.AddComponent<CanvasGroup>();
                    g.alpha = 0.45f;
                }
                Sfx.Instance?.Play("bubble", 0.25f, 0.9f + level.Index * 0.012f, 0f, 0.02f, 0f);
                for (float t = 0; t < 0.16f; t += Time.deltaTime)
                {
                    float e = t / 0.16f;
                    holder.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, e * e);
                    yield return null;
                }
                holder.localScale = Vector3.one;
                if (photo != null) MakePolaroidClickable(holder, polaroid.GetComponent<Image>(), level, photo);
                yield return new WaitForSeconds(0.09f);
            }
            yield return new WaitForSeconds(0.8f);
            int sealedCount = SealCount(levels);
            albumThanks.text = albumFinale ? "Thank you for packing with us." : "Every trip you pack adds a photo." +
                (sealedCount > 0 ? $"\n<size=28>Grandpa's seal on {sealedCount} of {levels.Count}.</size>" : "");
            albumDone.gameObject.SetActive(true);
        }

        // ================================================================== NOW PLAYING

        /// <summary>A little cassette that slides in from the top when the track changes.</summary>
        void BuildNowPlaying()
        {
            var tape = UiTheme.Card("Now Playing", root, new Color(0.2f, 0.21f, 0.27f), 0f);
            nowPlaying = (RectTransform)tape.parent;
            nowPlaying.Pin(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(110, 200), new Vector2(430, 92));
            tape.GetComponent<Image>().raycastTarget = false;
            var label = UiKit.Image("Label", tape, new Color(1f, 0.93f, 0.6f), true);
            label.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -12));
            label.raycastTarget = false;
            reels = new RectTransform[2];
            for (int i = 0; i < 2; i++)
            {
                var reel = UiKit.Image("Reel", label.transform, new Color(0.2f, 0.21f, 0.27f), false);
                reel.sprite = UiTheme.Ring;
                reel.raycastTarget = false;
                reel.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34 + i * 46, 0), new Vector2(38, 38));
                for (int k = 0; k < 3; k++)
                {
                    var spoke = UiKit.Image("Spoke", reel.transform, new Color(0.2f, 0.21f, 0.27f), false);
                    spoke.raycastTarget = false;
                    spoke.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4, 16));
                    spoke.rectTransform.localRotation = Quaternion.Euler(0, 0, k * 120f);
                }
                reels[i] = reel.rectTransform;
            }
            var caption = UiTheme.Label("Caption", label.transform, "NOW PLAYING", UiTheme.Body, 18, UiTheme.InkSoft, TextAnchor.UpperLeft);
            caption.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(112, -28), new Vector2(-10, -6));
            nowPlayingTitle = UiTheme.Label("Title", label.transform, "", UiTheme.Hand, 30, UiTheme.Ink, TextAnchor.UpperLeft);
            nowPlayingTitle.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(112, 2), new Vector2(-10, -24));
            nowPlaying.gameObject.SetActive(false);
        }

        public void ShowNowPlaying(string title, string artist)
        {
            nowPlayingTitle.text = $"{title}  <size=20><color={UiKit.Hex(UiTheme.InkSoft)}>· {artist}</color></size>";
            nowPlayingTimer = 4.5f;
            // No room beside Grandpa's tip: the music is heard anyway, so skip the cassette this time.
            if (!PlaceNowPlaying()) { RetractNowPlaying(); return; }
            nowPlaying.gameObject.SetActive(true);
            nowPlaying.SetAsLastSibling();
        }

        float nowPlayingX = -140f, nowPlayingY = -112f;
        bool nowPlayingFromLeft;

        /// <summary>
        /// Centred over the empty sky on menus. While packing it goes in the gap between the trip tag and
        /// the buttons, or, when that gap is too narrow (big interface sizes, narrow screens), it slides in
        /// from the left under the trip tag (under Grandpa's tip if that's there), never over a button.
        /// </summary>
        bool PlaceNowPlaying()
        {
            const float Width = 430f, TagRight = 40f + 680f, ButtonsWidth = 40f + 546f;
            float w = root.rect.width;
            nowPlayingFromLeft = false;
            nowPlayingY = -112f;
            if (!hud.gameObject.activeSelf) { nowPlayingX = -140f; return true; }
            float gap = w - TagRight - ButtonsWidth;
            if (gap >= Width + 24f)
            {
                nowPlayingX = (TagRight - ButtonsWidth) * 0.5f;
                return true;
            }
            nowPlayingFromLeft = true;
            nowPlayingX = HudMargin + Width * 0.5f - w * 0.5f;
            nowPlayingY = -322f;
            return !TipUnderTag;
        }

        bool TipUnderTag => tipShown && tipHome.y < -200f;

        /// <summary>Slide a showing cassette away now.</summary>
        void RetractNowPlaying()
        {
            if (nowPlaying.gameObject.activeSelf && nowPlayingTimer > 0.45f) nowPlayingTimer = 0.45f;
        }

        void UpdateNowPlaying()
        {
            if (nowPlaying == null || !nowPlaying.gameObject.activeSelf) return;
            // Grandpa's tip took the spot under the trip tag: give it the room.
            if (nowPlayingFromLeft && TipUnderTag) RetractNowPlaying();
            nowPlayingTimer -= UiTime.Delta;
            float slide = Mathf.Clamp01(Mathf.Min(4.5f - nowPlayingTimer, nowPlayingTimer) / 0.45f);
            float e = 1f - (1f - slide) * (1f - slide);
            nowPlaying.anchoredPosition = nowPlayingFromLeft
                ? new Vector2(Mathf.Lerp(nowPlayingX - 520f, nowPlayingX, e), nowPlayingY)
                : new Vector2(nowPlayingX, Mathf.Lerp(20f, nowPlayingY, e));
            foreach (var reel in reels) reel.Rotate(0, 0, -160f * UiTime.Delta);
            if (nowPlayingTimer <= 0f) nowPlaying.gameObject.SetActive(false);
        }

        // ================================================================== RESULTS

        void BuildResults()
        {
            results = UiKit.Rect("Results", root).Fill();
            var shade = UiKit.Image("Shade", results, new Color(0.06f, 0.05f, 0.1f, 0.55f), false);
            shade.rectTransform.Fill();

            var card = UiTheme.Card("Postcard", results, UiTheme.Paper, -2f);
            ((RectTransform)card.parent).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1060, 620));
            UiMotion.Intro(card.parent, new Vector2(0, -900), 0.05f, 0.75f, 16f, 0.75f);
            UiTheme.Tape(card, new Vector2(0, 1), new Vector2(60, -10), -30f);
            UiTheme.Tape(card, new Vector2(1, 0), new Vector2(-60, 10), -30f);

            greetings = UiTheme.Label("Greetings", card, "Greetings from", UiTheme.Hand, 36, UiTheme.InkSoft, TextAnchor.UpperLeft);
            greetings.rectTransform.Place(new Vector2(0, 1), new Vector2(0.58f, 1), new Vector2(50, -96), new Vector2(0, -40));
            placeText = UiTheme.Label("Place", card, "", UiTheme.Display, 76, UiTheme.AccentInk, TextAnchor.UpperLeft);
            placeText.rectTransform.Place(new Vector2(0, 1), new Vector2(0.58f, 1), new Vector2(46, -196), new Vector2(0, -88));
            placeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var po = placeText.gameObject.AddComponent<Shadow>();
            po.effectColor = new Color(0.3f, 0.15f, 0.05f, 0.35f);
            po.effectDistance = new Vector2(4, -4);
            epilogueText = UiTheme.Label("Epilogue", card, "", UiTheme.Hand, 32, UiTheme.Ink, TextAnchor.UpperLeft);
            epilogueText.rectTransform.Place(new Vector2(0, 0), new Vector2(0.58f, 1), new Vector2(50, 130), new Vector2(-10, -210));
            epilogueText.lineSpacing = 1.1f;
            leftBehindText = UiTheme.Label("Left Behind", card, "", UiTheme.Hand, 25, UiTheme.InkSoft, TextAnchor.UpperLeft);
            leftBehindText.rectTransform.Place(new Vector2(0, 0), new Vector2(0.58f, 0), new Vector2(50, 30), new Vector2(-10, 126));

            var divider = UiKit.Image("Divider", card, new Color(UiTheme.InkSoft.r, UiTheme.InkSoft.g, UiTheme.InkSoft.b, 0.3f), false);
            divider.rectTransform.Place(new Vector2(0.6f, 0), new Vector2(0.6f, 1), new Vector2(-1, 50), new Vector2(1, -50));

            var stampBox = UiKit.Image("Stamp Box", card, new Color(UiTheme.Stamp.r, UiTheme.Stamp.g, UiTheme.Stamp.b, 0.85f), true);
            stampBox.pixelsPerUnitMultiplier = 3f;
            stampBox.rectTransform.Place(new Vector2(0.64f, 1), new Vector2(1, 1), new Vector2(0, -300), new Vector2(-50, -50));
            UiMotion.Intro(stampBox.rectTransform, Vector2.zero, 0.6f, 2.6f, -24f, 0.28f, false);
            var stampInner = UiKit.Image("Inner", stampBox.transform, new Color(1f, 0.96f, 0.9f), true);
            stampInner.pixelsPerUnitMultiplier = 3f;
            stampInner.rectTransform.Fill(5);
            resultsTitle = UiTheme.Label("Title", stampInner.transform, "", UiTheme.Display, 40, UiTheme.Stamp, TextAnchor.UpperCenter);
            resultsTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -64), new Vector2(-10, -14));
            resultsStars = UiKit.Rect("Stars", stampInner.transform).Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 20), new Vector2(-10, -70));
            UiKit.Horizontal(resultsStars.gameObject, 10, TextAnchor.MiddleCenter);

            resultsCounts = UiTheme.Label("Counts", card, "", UiTheme.Body, 25, UiTheme.Ink, TextAnchor.UpperLeft);
            resultsCounts.rectTransform.Place(new Vector2(0.64f, 0), new Vector2(1, 1), new Vector2(0, 40), new Vector2(-40, -320));
            resultsCounts.lineSpacing = 1.4f;

            // Grandpa's seal, stamped after the stars (or a note on how to earn it).
            resultsSeal = UiKit.Rect("Seal Row", card).Place(new Vector2(0.64f, 0), new Vector2(1, 0), new Vector2(0, 34), new Vector2(-30, 134));
            UiKit.Horizontal(resultsSeal.gameObject, 14, TextAnchor.MiddleLeft);
            resultsSealMark = UiTheme.Seal(resultsSeal, 84);
            resultsSealText = UiTheme.Label("Seal Text", resultsSeal, "", UiTheme.Hand, 27, UiTheme.Ink, TextAnchor.MiddleLeft);
            UiKit.Size(resultsSealText, 250, 96);

            var buttons = UiKit.Rect("Buttons", results).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(900, 90));
            UiMotion.Intro(buttons, new Vector2(0, -220), 1.2f, 1f, 0f, 0.55f);
            UiKit.Horizontal(buttons.gameObject, 20, TextAnchor.MiddleCenter);
            UiKit.Size(UiTheme.Pill("Levels", buttons, "MAP", UiTheme.Night, 28, () => MenuPressed?.Invoke()), 180, 80);
            UiKit.Size(UiTheme.Pill("Retry", buttons, "TRY AGAIN", UiTheme.Teal, 28, () => RestartPressed?.Invoke()), 250, 80);
            var next = UiTheme.Pill("Next", buttons, "NEXT TRIP", UiTheme.Accent, 32, () => NextPressed?.Invoke(), out nextLabel);
            UiKit.Size(next, 300, 80);
            resultsButtons = buttons;
            var keys = resultsKeys = UiTheme.Label("Keys", results, "<b>SPACE</b>  next trip      <b>R</b>  try again      <b>ESC</b>  map", UiTheme.Body, 20,
                new Color(1f, 0.95f, 0.88f, 0.8f), TextAnchor.MiddleCenter);
            keys.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -400), new Vector2(900, 30));
            keys.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);
            UiMotion.Intro(keys.rectTransform, new Vector2(0, -20), 1.5f, 1f, 0f, 0.5f, false);
        }

        RectTransform resultsButtons, resultsSeal, resultsSealMark;
        Text resultsSealText;
        bool resultsSealEarned;
        Text resultsKeys;
        float resultsShownAt;

        /// <summary>The postcard's buttons have arrived and can take keyboard shortcuts.</summary>
        public bool ResultsReady => results.gameObject.activeSelf && UiTime.Now - resultsShownAt > 1.4f && !curtainBusy && !OverlayOpen;

        /// <summary>The story texts are done and "Let's pack!" is waiting.</summary>
        public bool StoryReady => story.gameObject.activeSelf && startButton.gameObject.activeInHierarchy && !curtainBusy && !OverlayOpen;

        public void PressStart()
        {
            Sfx.Instance?.Click();
            StartPressed?.Invoke();
        }

        public void PressNext()
        {
            Sfx.Instance?.Click();
            NextPressed?.Invoke();
        }

        public void PressRetry()
        {
            Sfx.Instance?.Click();
            RestartPressed?.Invoke();
        }

        public void PressMap()
        {
            Sfx.Instance?.Back();
            MenuPressed?.Invoke();
        }

        /// <summary>Is the postcard showing Grandpa's seal (self-test)?</summary>
        public bool ResultsShowSeal => resultsSeal.gameObject.activeSelf && resultsSealMark.gameObject.activeSelf;

        public void ShowResults(LevelDef level, int stars, int reqDone, int req, int bonusDone, int bonus, IEnumerable<string> leftBehind, bool hasNext,
            bool sealEarned, bool sealNudge)
        {
            resultsSealEarned = sealEarned;
            resultsSeal.gameObject.SetActive(sealEarned || sealNudge);
            resultsSealMark.gameObject.SetActive(sealEarned);
            resultsSealMark.localScale = sealEarned ? Vector3.zero : Vector3.one;
            resultsSealText.color = sealEarned ? UiTheme.Ink : UiTheme.InkSoft;
            resultsSealText.text = sealEarned ? "Grandpa's seal!\n<size=22>Packed without a single hint.</size>" : "<size=23>Three stars without a hint\nearns Grandpa's seal.</size>";
            UiKit.Size(resultsSealText, sealEarned ? 250 : 340, 96);
            ShowResultsPanel(true);
            resultsShownAt = UiTime.Now;
            // The postcard's buttons sit where the key hints are.
            keyHints.gameObject.SetActive(false);
            hoverText.text = "";
            string[] titles = { "Hmm.", "It Closed!", "Nicely Packed!", "Packing Legend!" };
            resultsTitle.text = titles[Mathf.Clamp(stars, 0, 3)];
            var trip = level.Trip ?? "";
            int dot = trip.IndexOf('·');
            placeText.text = dot >= 0 ? trip.Substring(dot + 1).Trim() : level.Title;
            placeText.fontSize = 76;
            while (placeText.fontSize > 36 && placeText.preferredWidth > 560f) placeText.fontSize--;
            epilogueText.text = level.Epilogues != null && level.Epilogues.Length >= stars && stars > 0 ? level.Epilogues[stars - 1] : "";
            var behind = leftBehind.ToList();
            bool gnome = level.Required.Concat(level.Bonus).Any(d => d.Id == "gnome");
            leftBehindText.text = behind.Count == 0 ? (gnome ? "Nothing left behind. Not even the gnome." : "Nothing left behind. Not one thing.") : "Left on the curb: " + string.Join(", ", behind) + ".";
            resultsCounts.text = $"Essentials  <b>{reqDone}/{req}</b>\nExtras  <b>{bonusDone}/{bonus}</b>\n<size=21>{(level.Title.Contains(level.Vehicle) ? "" : level.Vehicle + " · ")}{level.Title}{(level.Year > 0 ? " · " + level.Year : "")}</size>";
            nextLabel.text = hasNext ? "NEXT TRIP" : "THE END";
            resultsKeys.text = hasNext ? "<b>SPACE</b>  next trip      <b>R</b>  try again      <b>ESC</b>  map" : "<b>SPACE</b>  the end      <b>R</b>  try again      <b>ESC</b>  map";

            UiKit.Clear(resultsStars);
            var images = new List<Image>();
            for (int i = 0; i < 3; i++)
            {
                var star = UiKit.StarImage(resultsStars, false, 100);
                star.color = new Color(0.4f, 0.32f, 0.25f, 0.18f);
                images.Add(star);
            }
            StartCoroutine(PopStars(images, stars));
        }

        IEnumerator PopStars(List<Image> images, int stars)
        {
            yield return UiMotion.Wait(0.86f);
            Sfx.Instance?.Stamp();
            yield return UiMotion.Wait(0.3f);
            for (int i = 0; i < stars && i < images.Count; i++)
            {
                var img = images[i];
                img.color = UiTheme.Gold;
                Sfx.Instance?.Star(i);
                for (float t = 0; t < 0.3f; t += Time.deltaTime)
                {
                    float k = t / 0.3f;
                    float s = k < 0.6f ? Mathf.Lerp(0.2f, 1.3f, k / 0.6f) : Mathf.Lerp(1.3f, 1f, (k - 0.6f) / 0.4f);
                    img.transform.localScale = new Vector3(s, s, 1f);
                    img.transform.localRotation = Quaternion.Euler(0, 0, (1f - k) * 40f);
                    yield return null;
                }
                img.transform.localScale = Vector3.one;
                img.transform.localRotation = Quaternion.identity;
                yield return UiMotion.Wait(0.16f);
            }
            if (!resultsSealEarned) yield break;
            // Then Grandpa presses his seal into the corner.
            yield return UiMotion.Wait(0.25f);
            Sfx.Instance?.Stamp();
            var seal = resultsSealMark;
            for (float t = 0; t < 0.28f; t += Time.deltaTime)
            {
                float k = t / 0.28f;
                seal.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, k * k);
                seal.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-30f, -8f, k));
                yield return null;
            }
            seal.localScale = Vector3.one;
            seal.localRotation = Quaternion.Euler(0, 0, -8f);
        }

        void ShowResultsPanel(bool show)
        {
            results.gameObject.SetActive(show);
            hud.gameObject.SetActive(true);
        }
    }

    /// <summary>Gentle scale pulse for attention markers.</summary>
    public class Breathe : MonoBehaviour
    {
        void Update()
        {
            float s = GameSettings.ReduceMotion ? 1f : 1f + Mathf.Sin(UiTime.Now * 3f) * 0.08f;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
