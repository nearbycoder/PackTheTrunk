using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PackTheTrunk
{
    /// <summary>Title and main menu, pause menu, credits roll, confirm dialogs and screen transitions.</summary>
    public partial class GameUI
    {
        public event Action ContinuePressed, TripMapPressed, AlbumPressed, CreditsPressed, TitleBackPressed;
        public event Action PausePressed, ResumePressed, PauseRestartPressed, PauseMapPressed, PauseMainMenuPressed;
        public event Action TitleKeyPressed, CreditsFinished;

        // Title / main menu
        RectTransform title, titleMenu, titleLogo, titleTagline;
        CanvasGroup pressKeyGroup;
        Text pressKeyText, continueSub, mapEntrySub, albumEntrySub;
        bool waitingForKey;
        float titleShownAt;

        // Pause
        RectTransform pause;
        Text pauseTrip;

        // Credits
        RectTransform credits, creditsRoll;
        bool creditsRunning;

        // Confirm
        RectTransform confirm;
        Text confirmText;
        Text confirmYesLabel;
        Action confirmYes;

        // Curtain
        RectTransform curtain, curtainSheet, curtainCar;
        CanvasGroup curtainGroup;

        /// <summary>Where the paper-wipe sheet is and how opaque (for the self-test).</summary>
        public float CurtainSheetX => curtainSheet.anchoredPosition.x;
        public float CurtainAlpha => curtainGroup.alpha;
        RectTransform[] curtainWheels;
        bool curtainBusy;

        public bool IsTitleWaiting => waitingForKey && title != null && title.gameObject.activeSelf;
        public bool IsAlbumOpen => album != null && album.gameObject.activeSelf;
        public bool OverlayOpen => (settings != null && settings.gameObject.activeSelf) || (confirm != null && confirm.gameObject.activeSelf) ||
                                   (credits != null && credits.gameObject.activeSelf) || IsAlbumZoomOpen;
        public bool IsPaused => pause != null && pause.gameObject.activeSelf;

        // ================================================================== TITLE

        void BuildTitle()
        {
            title = UiKit.Rect("Title", root).Fill();
            var shade = UiKit.Image("Shade", title, new Color(0.09f, 0.06f, 0.15f, 0.7f), false);
            shade.sprite = UiTheme.Gradient;
            shade.raycastTarget = false;
            shade.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(1250, 0));
            var bottom = UiKit.Image("Bottom", title, new Color(0.09f, 0.06f, 0.15f, 0.45f), false);
            bottom.sprite = UiTheme.GradientUp;
            bottom.raycastTarget = false;
            bottom.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 260));

            titleLogo = BuildLogo(title, new Vector2(96, -64));
            var tag = UiTheme.Card("Tagline", title, new Color(1f, 0.93f, 0.6f), -2f);
            titleTagline = (RectTransform)tag.parent;
            titleTagline.Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(122, -372), new Vector2(660, 64));
            UiTheme.Label("Text", tag, "A lifetime of fitting way too much into way too little.", UiTheme.Hand, 30, UiTheme.Ink, TextAnchor.MiddleCenter).rectTransform.Fill(6);
            UiMotion.Intro(titleTagline, new Vector2(0, -40), 0.75f, 0.6f, -10f, 0.6f);

            titleMenu = UiKit.Rect("Main Menu", title).Pin(new Vector2(0, 0), new Vector2(0, 0), new Vector2(110, 96), new Vector2(760, 540));
            var v = UiKit.Vertical(titleMenu.gameObject, 4, null, TextAnchor.LowerLeft);
            // A soft shade behind the menu, so its small captions stay readable over bright grass or a parked car.
            // Solid over the same area as before (the menu plus 80 / 60 / 40 units), then easing to clear over
            // another 260 units, so it has no edge or corner to see.
            const float feather = 260f;
            var scrim = UiKit.Image("Menu Scrim", titleMenu, new Color(0f, 0f, 0f, 0.4f), false);
            scrim.sprite = UiTheme.ScrimSprite;
            scrim.type = Image.Type.Sliced;
            scrim.pixelsPerUnitMultiplier = 128f / feather;
            scrim.raycastTarget = false;
            scrim.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(-80 - feather, -60 - feather), new Vector2(60 + feather, 40 + feather));
            // Its faded top reaches the tagline and the logo: draw the menu before them, so they stay on top.
            titleMenu.SetSiblingIndex(bottom.transform.GetSiblingIndex() + 1);
            scrim.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            v.childForceExpandHeight = false;
            int n = 0;
            MenuEntry(titleMenu, "Continue", "CONTINUE", "", () => ContinuePressed?.Invoke(), n++, out continueSub, true);
            MenuEntry(titleMenu, "Trip Map", "TRIP MAP", "", () => TripMapPressed?.Invoke(), n++, out mapEntrySub);
            MenuEntry(titleMenu, "Album", "FAMILY ALBUM", "", () => AlbumPressed?.Invoke(), n++, out albumEntrySub);
            MenuEntry(titleMenu, "Settings", "SETTINGS", "Sound, display, graphics and controls", () => ShowSettings(), n++, out _);
            MenuEntry(titleMenu, "Credits", "CREDITS", "The people (and ducks) behind it", () => CreditsPressed?.Invoke(), n++, out _);
            // A browser tab can't close itself: there the tab is the way out.
            if (!Web.IsBrowser)
                MenuEntry(titleMenu, "Quit", "QUIT", "", () => Confirm("Leave the driveway?", "QUIT", () => QuitPressed?.Invoke()), n++, out _);

            pressKeyText = UiTheme.Label("Press", title, "PRESS ANY KEY", UiTheme.Display, 40, Color.white, TextAnchor.MiddleCenter);
            pressKeyText.rectTransform.Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(900, 60));
            pressKeyText.gameObject.AddComponent<Outline>().effectColor = new Color(0.1f, 0.07f, 0.16f, 0.8f);
            pressKeyGroup = pressKeyText.gameObject.AddComponent<CanvasGroup>();

            var version = UiTheme.Label("Version", title, $"v{Application.version}  ·  Made with Unity and Blender", UiTheme.Body, 18, new Color(1f, 1f, 1f, 0.85f), TextAnchor.LowerRight);
            version.rectTransform.Pin(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 26), new Vector2(700, 30));
            title.gameObject.SetActive(false);
        }

        /// <summary>The bouncy "PACK THE TRUNK" logo, one letter at a time.</summary>
        RectTransform BuildLogo(Transform parent, Vector2 position)
        {
            var logo = UiKit.Rect("Logo", parent).Pin(new Vector2(0, 1), new Vector2(0, 1), position, new Vector2(900, 300));
            logo.localRotation = Quaternion.Euler(0, 0, 3f);
            int index = 0;
            foreach (var (line, y, size) in new[] { ("PACK THE", -10f, 118), ("TRUNK", -128f, 150) })
            {
                float x = 0f;
                foreach (char ch in line)
                {
                    if (ch == ' ') { x += size * 0.3f; continue; }
                    var slot = UiKit.Rect("Letter", logo);
                    var t = UiTheme.Label("Glyph", slot, ch.ToString(), UiTheme.Display, size, Color.white, TextAnchor.UpperLeft);
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    float w = t.preferredWidth;
                    slot.Pin(new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(x + w / 2f, y - size * 0.6f), new Vector2(w, size + 30));
                    t.rectTransform.Fill();
                    var outline = t.gameObject.AddComponent<Outline>();
                    outline.effectColor = UiTheme.Ink;
                    outline.effectDistance = new Vector2(4, -4);
                    var drop = t.gameObject.AddComponent<Shadow>();
                    drop.effectColor = new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 1f);
                    drop.effectDistance = new Vector2(9, -10);
                    var bob = t.gameObject.AddComponent<Bob>();
                    bob.Phase = index * 0.55f;
                    bob.Amount = 3.5f;
                    bob.Tilt = 1.6f;
                    bob.Speed = 1.6f;
                    UiMotion.Intro(slot, new Vector2(0, 260), 0.05f + index * 0.045f, 0.4f, (index % 2 == 0 ? -1 : 1) * 25f, 0.7f);
                    x += w - 2f;
                    index++;
                }
            }
            return logo;
        }

        void MenuEntry(Transform parent, string name, string label, string sub, Action onClick, int order, out Text subText, bool primary = false)
        {
            var slot = UiKit.Rect(name, parent);
            UiKit.Size(slot.gameObject.AddComponent<LayoutElement>(), 720, primary ? 100 : 80);
            var hit = slot.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());

            var content = UiKit.Rect("Content", slot).Fill();
            UiMotion.Intro(content, new Vector2(-520, 0), 0.05f + order * 0.06f, 1f, 0f, 0.55f, false);
            var inner = UiKit.Rect("Inner", content).Fill();

            var marker = UiKit.Image("Marker", inner, UiTheme.Accent, true);
            marker.raycastTarget = false;
            marker.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-26, primary ? 8 : 0), new Vector2(12, primary ? 56 : 44));
            var text = UiTheme.Label("Label", inner, label, UiTheme.Display, primary ? 64 : 48, Color.white, TextAnchor.UpperLeft);
            text.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, primary ? -78 : -58), new Vector2(0, 0));
            var o = text.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.1f, 0.07f, 0.16f, 0.75f);
            o.effectDistance = new Vector2(3, -3);
            var sh = text.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.1f, 0.07f, 0.16f, 0.45f);
            sh.effectDistance = new Vector2(0, -6);
            subText = UiTheme.Label("Sub", inner, sub, UiTheme.Body, 20, new Color(1f, 0.92f, 0.78f), TextAnchor.UpperLeft);
            subText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(4, -2), new Vector2(0, 24));
            subText.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);

            var hover = slot.gameObject.AddComponent<MenuEntryHover>();
            hover.Inner = inner;
            hover.Marker = marker;
            hover.Label = text;
            hover.Sub = subText;
        }

        public void ShowTitle(bool waitForKey, string continueText, string mapText, string albumText)
        {
            ShowOnly(title);
            waitingForKey = waitForKey;
            titleShownAt = UiTime.Now;
            continueSub.text = continueText;
            mapEntrySub.text = mapText;
            albumEntrySub.text = albumText;
            titleMenu.gameObject.SetActive(!waitForKey);
            pressKeyText.gameObject.SetActive(waitForKey);
            titleLogo.gameObject.SetActive(false);
            titleLogo.gameObject.SetActive(true);
        }

        /// <summary>
        /// The menu stands on the bottom edge and the logo and tagline hang from the top; on a short canvas
        /// (a bigger interface size on a 16:9 screen) the menu shrinks to stay clear of the tagline.
        /// </summary>
        void FitTitleMenu()
        {
            // The tagline's bottom edge (372 + 64 down, plus about 24 more at its tilted end), a gap, and
            // the menu's 96-unit bottom margin.
            float s = Mathf.Clamp((root.rect.height - 372f - 64f - 24f - 12f - 96f) / 520f, 0.6f, 1f);
            if (!Mathf.Approximately(titleMenu.localScale.x, s)) titleMenu.localScale = new Vector3(s, s, 1f);
        }

        void UpdateTitle()
        {
            if (title == null || !title.gameObject.activeSelf) return;
            FitTitleMenu();
            if (!waitingForKey) return;
            string prompt = Web.TouchActive ? "TOUCH TO START" : "PRESS ANY KEY";
            if (pressKeyText.text != prompt) pressKeyText.text = prompt;
            float since = UiTime.Now - titleShownAt;
            pressKeyGroup.alpha = Mathf.Clamp01((since - 1.2f) / 0.6f) * (0.55f + 0.45f * Mathf.Sin(UiTime.Now * 3.2f));
            if (since < 0.8f) return;
            bool pressed = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                           (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) || Pad.AnyButton;
            if (!pressed) return;
            waitingForKey = false;
            escConsumedFrame = Time.frameCount;
            pressKeyText.gameObject.SetActive(false);
            titleMenu.gameObject.SetActive(true);
            TitleKeyPressed?.Invoke();
        }

        // ================================================================== PAUSE

        void BuildPause()
        {
            pause = UiKit.Rect("Pause", root).Fill();
            var dim = UiKit.Image("Dim", pause, new Color(0.07f, 0.05f, 0.12f, 0.45f), false);
            dim.rectTransform.Fill();
            var shade = UiKit.Image("Shade", pause, new Color(0.09f, 0.06f, 0.15f, 0.7f), false);
            shade.sprite = UiTheme.Gradient;
            shade.raycastTarget = false;
            shade.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(1100, 0));

            var header = UiTheme.Label("Header", pause, "PAUSED", UiTheme.Display, 120, Color.white, TextAnchor.UpperLeft);
            header.rectTransform.Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -90), new Vector2(800, 150));
            header.gameObject.AddComponent<Shadow>().effectColor = new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 1f);
            header.GetComponent<Shadow>().effectDistance = new Vector2(8, -9);
            header.gameObject.AddComponent<Outline>().effectColor = UiTheme.Ink;
            UiMotion.Intro(header.rectTransform, new Vector2(-300, 0), 0f, 1f, -6f, 0.5f);
            pauseTrip = UiTheme.Label("Trip", pause, "", UiTheme.Hand, 34, new Color(1f, 0.9f, 0.75f), TextAnchor.UpperLeft);
            pauseTrip.rectTransform.Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(116, -240), new Vector2(900, 50));
            UiMotion.Intro(pauseTrip.rectTransform, new Vector2(-300, 0), 0.05f, 1f, 0f, 0.5f, false);

            var list = UiKit.Rect("Pause Menu", pause).Pin(new Vector2(0, 0), new Vector2(0, 0), new Vector2(110, 120), new Vector2(760, 520));
            UiKit.Vertical(list.gameObject, 4, null, TextAnchor.LowerLeft).childForceExpandHeight = false;
            // The same soft scrim as the main menu's, so the captions hold their contrast over a bright, blurred
            // scene (a 21:9 window stretches the shade's gradient thin); drawn before the header so it passes under it.
            const float feather = 260f;
            var scrim = UiKit.Image("Menu Scrim", list, new Color(0f, 0f, 0f, 0.4f), false);
            scrim.sprite = UiTheme.ScrimSprite;
            scrim.type = Image.Type.Sliced;
            scrim.pixelsPerUnitMultiplier = 128f / feather;
            scrim.raycastTarget = false;
            scrim.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(-80 - feather, -60 - feather), new Vector2(60 + feather, 40 + feather));
            scrim.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            list.SetSiblingIndex(shade.transform.GetSiblingIndex() + 1);
            int n = 0;
            MenuEntry(list, "Resume", "RESUME", "Back to packing", () => ResumePressed?.Invoke(), n++, out _, true);
            MenuEntry(list, "Pause Restart", "RESTART TRIP", "Unpack everything (undo puts it back)", () => PauseRestartPressed?.Invoke(), n++, out _);
            MenuEntry(list, "Pause Settings", "SETTINGS", "Sound, display, graphics and controls", () => ShowSettings(), n++, out _);
            MenuEntry(list, "Pause Map", "TRIP MAP", "Pick another trip (this trunk will wait)", () => PauseMapPressed?.Invoke(), n++, out _);
            MenuEntry(list, "Pause Title", "MAIN MENU", "Everything you've packed is saved", () => PauseMainMenuPressed?.Invoke(), n++, out _);

            // How-to card on the right.
            var card = UiTheme.Card("How To", pause, UiTheme.Paper, 2f);
            var holder = (RectTransform)card.parent;
            holder.Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-120, 0), new Vector2(640, 780));
            UiTheme.Tape(card, new Vector2(0.5f, 1f), new Vector2(0, -4), -2f, 170f);
            UiMotion.Intro(holder, new Vector2(500, 0), 0.1f, 0.9f, 8f, 0.6f);
            var h = UiTheme.Label("Header", card, "HOW TO PACK", UiTheme.Display, 44, UiTheme.Ink, TextAnchor.UpperLeft);
            h.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(46, -100), new Vector2(-30, -40));
            var rules = UiTheme.Label("Rules", card,
                "Pack every <b>essential</b>, then close the trunk.\nEach <b>extra</b> you squeeze in earns a star.\nNothing can float, and nothing goes on top of <b>fragile</b> things.",
                UiTheme.Hand, 27, UiTheme.InkSoft, TextAnchor.UpperLeft);
            rules.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(46, -250), new Vector2(-40, -104));
            var keys = UiKit.Rect("Keys", card).Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(46, 40), new Vector2(-30, -262));
            UiKit.Vertical(keys.gameObject, 8).childControlHeight = false;
            pauseKeys = keys;
            pauseCard = card;
            var padKeys = UiKit.Rect("Pad Keys", card).Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(46, 40), new Vector2(-30, -262));
            UiKit.Vertical(padKeys.gameObject, 8).childControlHeight = false;
            var touchKeys = UiKit.Rect("Touch Keys", card).Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(46, 40), new Vector2(-30, -262));
            UiKit.Vertical(touchKeys.gameObject, 8).childControlHeight = false;
            bool filledToggle = false;
            void FillKeys()
            {
                filledToggle = GameSettings.XRayToggle;
                UiKit.Clear(keys);
                foreach (var (k, what) in ControlsList) ControlRow(keys, k, what, 22);
                UiKit.Clear(padKeys);
                foreach (var (k, what) in PadControlsList) ControlRow(padKeys, k, what, 22);
                UiKit.Clear(touchKeys);
                foreach (var (k, what) in TouchControlsList) ControlRow(touchKeys, k, what, 22);
            }
            FillKeys();
            Bindings.Changed += FillKeys;
            PadBindings.Changed += FillKeys;
            // "(hold)" or "(press)" follows the X-ray setting.
            GameSettings.Changed += () => { if (GameSettings.XRayToggle != filledToggle) FillKeys(); };
            void ShowPadKeys()
            {
                keys.gameObject.SetActive(!GamepadCursor.Active && !Web.TouchActive);
                padKeys.gameObject.SetActive(GamepadCursor.Active && !Web.TouchActive);
                touchKeys.gameObject.SetActive(Web.TouchActive);
            }
            ShowPadKeys();
            GamepadCursor.ActiveChanged += ShowPadKeys;
            Web.TouchActiveChanged += ShowPadKeys;
            pause.gameObject.SetActive(false);
        }

        /// <summary>The keyboard controls, with whatever keys are bound right now.</summary>
        internal static (string, string)[] ControlsList
        {
            get
            {
                string L(Bindings.Action a) => Bindings.Label(a);
                return new[]
                {
                    ("CLICK", "pick up · drop (or drag it in)"),
                    ($"{L(Bindings.Action.Turn)}  {L(Bindings.Action.Tip)}  {L(Bindings.Action.Roll)}", "turn · tip · roll (hold SHIFT to reverse)"),
                    ($"WHEEL / {L(Bindings.Action.ShelfUp)} {L(Bindings.Action.ShelfDown)}", "choose a shelf when there's a gap"),
                    ($"{L(Bindings.Action.LookLeft)}  {L(Bindings.Action.LookRight)} / RIGHT-DRAG", "look around the car"),
                    ($"{L(Bindings.Action.XRay)} ({(GameSettings.XRayToggle ? "press" : "hold")})", "see through everything packed"),
                    ($"{L(Bindings.Action.Undo)}  ·  {L(Bindings.Action.Hint)}", "undo (SHIFT: redo) · ask Grandpa"),
                    (L(Bindings.Action.Close), "close the trunk"),
                    ("ARROWS  ·  ENTER", "pack without a mouse"),
                    ($"ESC  ·  {L(Bindings.Action.Music)}", "put back · pause · music on / off"),
                };
            }
        }

        /// <summary>The browser's on-screen touch controls (Assets/WebGLTemplates/PackTheTrunk/index.html).</summary>
        internal static (string, string)[] TouchControlsList => new[]
        {
            ("TOUCH", "pick up · drag it in · lift to drop"),
            ("TURN  TIP  ROLL", "turn it around (while it's in hand)"),
            ("UP  ·  DOWN", "choose a shelf when there's a gap"),
            ("TWO FINGERS", "drag to look around · pinch to zoom"),
            ($"X-RAY ({(GameSettings.XRayToggle ? "press" : "hold")})", "see through everything packed"),
            ("UNDO  REDO  HINT", "take a step back · ask Grandpa"),
            ("BACK", "put it back on the blanket"),
            ("CLOSE THE TRUNK", "when the essentials are in"),
        };

        internal static (string, string)[] PadControlsList
        {
            get
            {
                string P(PadBindings.Action a) => PadBindings.Label(a);
                return new[]
                {
                    ("L-STICK  ·  A", "point · pick up · drop · click"),
                    ($"{P(PadBindings.Action.Turn)}  {P(PadBindings.Action.Tip)}  {P(PadBindings.Action.Roll)}", "turn · tip · roll (hold LB to reverse)"),
                    (PadBindings.ShelfLabel(), "choose a shelf"),
                    ("R-STICK  ·  LT RT", "look around · zoom"),
                    ($"{P(PadBindings.Action.XRay)} ({(GameSettings.XRayToggle ? "press" : "hold")})", "see through everything packed"),
                    ($"{P(PadBindings.Action.Undo)}  ·  {P(PadBindings.Action.Hint)}", "undo (LB: redo) · ask Grandpa"),
                    (P(PadBindings.Action.Close), "close the trunk"),
                    ("B  ·  MENU", "put back / back · pause"),
                };
            }
        }

        void ControlRow(Transform parent, string key, string text, int size)
        {
            var r = UiKit.Rect("Line", parent);
            r.sizeDelta = new Vector2(560, size + 22);
            UiKit.Size(r.gameObject.AddComponent<LayoutElement>(), -1, size + 22);
            var row = UiKit.Horizontal(r.gameObject, 14, TextAnchor.MiddleLeft);
            row.childControlWidth = true;
            var cap = UiKit.Image("Cap", r, Color.white, false);
            cap.sprite = UiTheme.Keycap;
            cap.type = Image.Type.Sliced;
            cap.raycastTarget = false;
            var kt = UiTheme.Label("K", cap.transform, key, UiTheme.Display, size - 2, UiTheme.Ink, TextAnchor.MiddleCenter);
            kt.horizontalOverflow = HorizontalWrapMode.Overflow;
            kt.rectTransform.Fill(2);
            UiKit.Size(cap, Mathf.Max(46, kt.preferredWidth + 24), size + 18);
            FitToText.Attach(cap, kt, 24f, 46f);
            var lt = UiTheme.Label("T", r, text, UiTheme.Body, size, UiTheme.InkSoft, TextAnchor.MiddleLeft);
            lt.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Size(lt, lt.preferredWidth + 4, size + 18);
            FitToText.Attach(lt, lt, 4f);
        }

        RectTransform pauseKeys, pauseCard;

        /// <summary>
        /// The pause card's keyboard rows that don't fit (for the self-test): any row that sticks out of the card,
        /// or whose key and words together are wider than the card leaves them.
        /// </summary>
        public List<string> PauseKeysProblems()
        {
            var problems = new List<string>();
            if (pauseKeys == null || !pauseKeys.gameObject.activeInHierarchy) return problems;
            Canvas.ForceUpdateCanvases();
            var card = pauseCard.rect;
            float right = card.width - 30f;
            foreach (RectTransform row in pauseKeys)
            {
                var corners = new Vector3[4];
                row.GetWorldCorners(corners);
                var min = pauseCard.InverseTransformPoint(corners[0]);
                var max = pauseCard.InverseTransformPoint(corners[2]);
                float width = 0f;
                foreach (RectTransform part in row) width += part.rect.width;
                width += 14f * Mathf.Max(0, row.childCount - 1);
                string name = row.GetComponentInChildren<Text>()?.text ?? row.name;
                if (min.y < card.yMin || max.y > card.yMax) problems.Add($"{name} sticks out of the card");
                if (min.x - card.xMin + width > right) problems.Add($"{name} is {min.x - card.xMin + width - right:0} units too wide");
            }
            return problems;
        }

        /// <summary>The pause card's keyboard rows (key, words), for the self-test.</summary>
        public List<string> PauseKeyRows() => pauseKeys == null ? new List<string>()
            : pauseKeys.Cast<Transform>().Select(r => string.Join(" | ", r.GetComponentsInChildren<Text>().Select(t => t.text))).ToList();

        public void ShowPause(LevelDef level)
        {
            foreach (Transform child in hud) if (child != results) child.gameObject.SetActive(false);
            pause.gameObject.SetActive(true);
            pause.SetAsLastSibling();
            if (nowPlaying != null) nowPlaying.SetAsLastSibling();
            pauseTrip.text = level == null ? "" : level.IsFavour ? $"Favour {level.Favour}  ·  {level.Title} for {level.Sender}" : $"Trip {level.Index + 1}  ·  {level.Title}";
        }

        public void HidePause()
        {
            if (!pause.gameObject.activeSelf) return;
            pause.gameObject.SetActive(false);
            if (hud.gameObject.activeSelf && !hudHidden) HideHudForCutscene(false);
        }

        // ================================================================== CREDITS

        void BuildCredits()
        {
            credits = UiKit.Rect("Credits", root).Fill();
            var bg = UiKit.Image("Bg", credits, new Color(0.1f, 0.08f, 0.14f, 1f), false);
            bg.rectTransform.Fill();
            var skip = UiTheme.Label("Skip", credits, "ESC or click to return", UiTheme.Body, 20, new Color(1, 1, 1, 0.45f), TextAnchor.LowerRight);
            skip.rectTransform.Pin(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 26), new Vector2(600, 30));
            var button = bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => CloseCredits());

            creditsRoll = UiKit.Rect("Roll", credits).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(1200, 100));
            var v = UiKit.Vertical(creditsRoll.gameObject, 8, null, TextAnchor.UpperCenter);
            v.childControlHeight = true;
            var fitter = creditsRoll.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            void Line(string text, Font font, int size, Color color, float space = 0f)
            {
                if (space > 0f)
                {
                    var gap = UiKit.Rect("Gap", creditsRoll);
                    UiKit.Size(gap.gameObject.AddComponent<LayoutElement>(), -1, space);
                }
                var t = UiTheme.Label("Line", creditsRoll, text, font, size, color, TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            var cream = new Color(1f, 0.95f, 0.86f);
            var soft = new Color(0.8f, 0.76f, 0.88f);
            Line("PACK THE TRUNK", UiTheme.Display, 110, Color.white);
            Line("A family, thirty years and one very full trunk.", UiTheme.Hand, 38, new Color(1f, 0.85f, 0.6f));
            void Section(string head, params string[] lines)
            {
                Line(head, UiTheme.Body, 24, UiTheme.AccentInk, 70f);
                foreach (var l in lines) Line(l, UiTheme.Hand, 38, cream);
            }
            Section("THE FAMILY",
                "Grandpa Joe  ·  who taught everyone how to pack",
                "Grandma Rose  ·  who taught everyone why",
                "Mom and Dad  ·  Aunt Dee  ·  Uncle Rick  ·  Cousin Bonko",
                "Dev and Wes  ·  the friends who always need a hand",
                "Sam  ·  six hundred records and one cat",
                "Rosie  ·  the next packer in the family");
            Section("ALSO STARRING",
                "Mr. Buttons as himself",
                "Sir Quacksalot as himself",
                "Biscuit the cat",
                "The garden gnome, who was never left behind");
            Section("MUSIC", "\"lofi Compilation\" by TAD  ·  CC0");
            Section("SOUND",
                "Interface, impact and foley sounds by Kenney  ·  CC0",
                "Park ambience by Thimras  ·  CC0",
                "Stingers, horn and engine synthesized for this game");
            Section("TYPE",
                "Lilita One by Juan Montoreano  ·  OFL",
                "Varela Round by Joe Prince  ·  OFL",
                "Patrick Hand by Patrick Wagesreiter  ·  OFL");
            Section("MADE WITH", "Unity 6  ·  Universal Render Pipeline  ·  Blender");
            Line("Big things first. Fragile on top.", UiTheme.Hand, 46, Color.white, 160f);
            Line("And always leave room for one more thing.", UiTheme.Hand, 46, Color.white);
            Line("Thank you for packing with us.", UiTheme.Display, 54, new Color(1f, 0.85f, 0.6f), 120f);
            credits.gameObject.SetActive(false);
        }

        public void ShowCredits()
        {
            credits.gameObject.SetActive(true);
            credits.SetAsLastSibling();
            StartCoroutine(RollCredits());
        }

        IEnumerator RollCredits()
        {
            creditsRunning = true;
            creditsRoll.anchoredPosition = new Vector2(0, -60);
            yield return null;
            Canvas.ForceUpdateCanvases();
            float height = creditsRoll.rect.height;
            float y = -60f;
            float end = height + 1080f + 40f;
            while (creditsRunning && y < end)
            {
                y += UiTime.Delta * 72f;
                creditsRoll.anchoredPosition = new Vector2(0, y);
                yield return null;
            }
            if (creditsRunning) CloseCredits();
        }

        void CloseCredits()
        {
            if (!credits.gameObject.activeSelf) return;
            creditsRunning = false;
            credits.gameObject.SetActive(false);
            Sfx.Instance?.Back();
            CreditsFinished?.Invoke();
        }

        // ================================================================== CONFIRM

        void BuildConfirm()
        {
            confirm = UiKit.Rect("Confirm", root).Fill();
            var dim = UiKit.Image("Dim", confirm, new Color(0.05f, 0.04f, 0.09f, 0.6f), false);
            dim.rectTransform.Fill();
            var card = UiTheme.Card("Dialog", confirm, UiTheme.Paper, -1.5f);
            var holder = (RectTransform)card.parent;
            holder.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 340));
            UiTheme.Tape(card, new Vector2(0.5f, 1f), new Vector2(0, -4), 3f, 160f);
            UiMotion.Intro(holder, new Vector2(0, -80), 0f, 0.7f, -6f, 0.45f);
            confirmText = UiTheme.Label("Text", card, "", UiTheme.Display, 46, UiTheme.Ink, TextAnchor.MiddleCenter);
            confirmText.rectTransform.Place(new Vector2(0, 0.45f), Vector2.one, new Vector2(40, 0), new Vector2(-40, -40));
            var row = UiKit.Rect("Buttons", card).Place(new Vector2(0, 0), new Vector2(1, 0.45f), new Vector2(40, 40), new Vector2(-40, -10));
            UiKit.Horizontal(row.gameObject, 24, TextAnchor.MiddleCenter);
            var no = UiTheme.Pill("Confirm No", row, "CANCEL", UiTheme.Night, 28, () => CloseConfirm(false));
            no.GetComponent<PillHover>().IsBack = true;
            UiKit.Size(no, 230, 76);
            UiKit.Size(UiTheme.Pill("Confirm Yes", row, "YES", UiTheme.Accent, 30, () => CloseConfirm(true), out confirmYesLabel), 260, 76);
            confirm.gameObject.SetActive(false);
        }

        public void Confirm(string question, string yes, Action onYes)
        {
            confirmText.text = question;
            confirmYesLabel.text = yes;
            confirmYes = onYes;
            confirm.gameObject.SetActive(true);
            confirm.SetAsLastSibling();
            Sfx.Instance?.OpenPanel();
        }

        void CloseConfirm(bool yes)
        {
            confirm.gameObject.SetActive(false);
            var action = confirmYes;
            confirmYes = null;
            if (yes) action?.Invoke();
        }

        // ================================================================== CURTAIN

        void BuildCurtain()
        {
            curtain = UiKit.Rect("Curtain", root).Fill();
            // Swallows clicks for the whole transition so nothing can be pressed twice.
            UiKit.Image("Input Blocker", curtain, new Color(0, 0, 0, 0), false).rectTransform.Fill();
            curtainSheet = UiKit.Rect("Sheet", curtain).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2700, 1900));
            curtainSheet.localRotation = Quaternion.Euler(0, 0, 8f);
            curtainGroup = curtainSheet.gameObject.AddComponent<CanvasGroup>();
            var paper = UiKit.Image("Paper", curtainSheet, new Color(0.99f, 0.84f, 0.55f), false);
            paper.rectTransform.Fill();
            var edge = UiKit.Image("Edge", curtainSheet, UiTheme.Accent, false);
            edge.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(-26, 0), new Vector2(0, 0));
            var edge2 = UiKit.Image("Edge", curtainSheet, UiTheme.Accent, false);
            edge2.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(26, 0));
            for (int i = 0; i < 9; i++)
            {
                var dash = UiKit.Image("Road", curtainSheet, new Color(1f, 1f, 1f, 0.75f), true);
                dash.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-640 + i * 160, -64), new Vector2(90, 12));
            }

            // A little cartoon car that drives across while the sheet is up.
            curtainCar = UiKit.Rect("Car", curtainSheet).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(220, 110));
            curtainCar.localRotation = Quaternion.Euler(0, 0, -8f);
            var cabin = UiKit.Image("Cabin", curtainCar, new Color(1f, 0.97f, 0.9f), true);
            cabin.pixelsPerUnitMultiplier = 0.6f;
            cabin.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-12, 26), new Vector2(120, 60));
            var body = UiKit.Image("Body", curtainCar, UiTheme.Stamp, true);
            body.pixelsPerUnitMultiplier = 0.55f;
            body.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(210, 62));
            var bags = UiKit.Image("Luggage", curtainCar, UiTheme.Teal, true);
            bags.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-20, 66), new Vector2(90, 26));
            var bag2 = UiKit.Image("Luggage", curtainCar, UiTheme.Gold, true);
            bag2.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26, 70), new Vector2(46, 34));
            curtainWheels = new RectTransform[2];
            for (int i = 0; i < 2; i++)
            {
                var wheel = UiKit.Image("Wheel", curtainCar, UiTheme.Night, false);
                wheel.sprite = UiTheme.Circle;
                wheel.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-62 + i * 124, -42), new Vector2(54, 54));
                var hub = UiKit.Image("Hub", wheel.transform, new Color(0.85f, 0.85f, 0.88f), false);
                hub.sprite = UiTheme.Circle;
                hub.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));
                var spoke = UiKit.Image("Spoke", wheel.transform, UiTheme.Night, false);
                spoke.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 20));
                curtainWheels[i] = wheel.rectTransform;
            }
            curtain.gameObject.SetActive(false);
        }

        /// <summary>Sweep a paper sheet across the screen, run <paramref name="atCover"/> while hidden, then reveal.</summary>
        public void Transition(Action atCover)
        {
            if (curtainBusy)
            {
                // One already running: queue this one to run under the next cover.
                queuedTransition = atCover;
                return;
            }
            StartCoroutine(TransitionRoutine(atCover));
        }

        Action queuedTransition;

        /// <summary>True while the curtain is moving; game input waits for it.</summary>
        public bool InTransition => curtainBusy;

        IEnumerator TransitionRoutine(Action atCover)
        {
            curtainBusy = true;
            curtain.gameObject.SetActive(true);
            curtain.SetAsLastSibling();
            Sfx.Instance?.WhooshIn();
            const float inTime = 0.42f, outTime = 0.5f;
            for (float t = 0; t < inTime; t += UiTime.Delta)
            {
                PoseCurtain(Mathf.Lerp(2750f, 0f, Ease.InOutCubic(t / inTime)), t);
                yield return null;
            }
            PoseCurtain(0f, inTime);
            yield return null;
            try { atCover?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            curtain.SetAsLastSibling();
            // Let the new scene settle for a beat (and hide any first-frame hitch).
            for (float t = 0; t < 0.28f; t += UiTime.Delta)
            {
                PoseCurtain(0f, inTime + t);
                yield return null;
            }
            Sfx.Instance?.WhooshOut();
            for (float t = 0; t < outTime; t += UiTime.Delta)
            {
                PoseCurtain(Mathf.Lerp(0f, -2750f, Ease.InOutCubic(t / outTime)), inTime + 0.28f + t);
                yield return null;
            }
            curtain.gameObject.SetActive(false);
            curtainBusy = false;
            if (queuedTransition != null)
            {
                var next = queuedTransition;
                queuedTransition = null;
                Transition(next);
            }
        }

        void PoseCurtain(float x, float time)
        {
            if (GameSettings.ReduceMotion)
            {
                // Reduce motion: the sheet fades in and out where it is, and the little car stays parked.
                curtainGroup.alpha = 1f - Mathf.Clamp01(Mathf.Abs(x) / 2750f);
                curtainSheet.anchoredPosition = Vector2.zero;
                curtainCar.anchoredPosition = new Vector2(0f, -6f);
                return;
            }
            curtainGroup.alpha = 1f;
            curtainSheet.anchoredPosition = new Vector2(x, 0f);
            curtainCar.anchoredPosition = new Vector2(Mathf.Lerp(-260f, 260f, Mathf.Clamp01(time / 1.2f)), -6f + Mathf.Abs(Mathf.Sin(time * 22f)) * 5f);
            foreach (var w in curtainWheels) w.localRotation = Quaternion.Euler(0, 0, -time * 900f);
        }
    }

    /// <summary>Main-menu entry hover: slides right, grows the marker and brightens the subtitle.</summary>
    public class MenuEntryHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
        UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerClickHandler
    {
        public RectTransform Inner;
        public Image Marker;
        public Text Label, Sub;
        float k, press;
        bool over, down;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { over = true; Sfx.Instance?.Hover(); }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { over = false; down = false; }
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) => down = true;
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => down = false;
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) => Sfx.Instance?.Click();

        void OnDisable() { over = down = false; k = press = 0f; Pose(); }

        void Update()
        {
            float dt = UiTime.Delta;
            float wantK = over ? 1f : 0f, wantPress = down ? 1f : 0f;
            if (k == wantK && press == wantPress) return;
            k = Mathf.MoveTowards(k, wantK, dt * 6f);
            press = Mathf.MoveTowards(press, wantPress, dt * 12f);
            Pose();
        }

        void Pose()
        {
            if (Inner == null) return;
            float e = Ease.OutCubic(k);
            Inner.anchoredPosition = new Vector2(e * 26f, 0f);
            Inner.localScale = Vector3.one * (1f + e * 0.03f - press * 0.03f);
            var m = Marker.rectTransform;
            m.localScale = new Vector3(1f, Mathf.Lerp(0.0f, 1f, e), 1f);
            Label.color = Color.Lerp(Color.white, new Color(1f, 0.86f, 0.55f), e);
            var c = Sub.color;
            c.a = Mathf.Lerp(0.6f, 1f, e);
            Sub.color = c;
        }
    }
}
