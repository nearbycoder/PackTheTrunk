using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PackTheTrunk
{
    /// <summary>The settings screen: tabbed rows of sliders, switches and choosers, all applied live.</summary>
    public partial class GameUI
    {
        public event Action ResetProgressPressed, SettingsClosed;

        RectTransform settings, settingsBody;
        Text settingsHint;
        int settingsTab;
        readonly List<Action> settingsRefresh = new List<Action>();
        readonly List<(Image face, Text label)> settingsTabs = new List<(Image, Text)>();
        static readonly string[] TabNames = { "AUDIO", "DISPLAY", "GRAPHICS", "GAMEPLAY", "CONTROLS" };
        float lastTick;
        int rowCount;

        void BuildSettings()
        {
            settings = UiKit.Rect("Settings", root).Fill();
            var dim = UiKit.Image("Dim", settings, new Color(0.06f, 0.04f, 0.1f, 0.55f), false);
            dim.rectTransform.Fill();

            var card = UiTheme.Card("Panel", settings, UiTheme.Paper, 0f);
            var holder = (RectTransform)card.parent;
            holder.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1500, 920));
            UiTheme.Tape(card, new Vector2(0, 1), new Vector2(70, -12), -28f);
            UiTheme.Tape(card, new Vector2(1, 1), new Vector2(-70, -12), 28f);
            UiMotion.Intro(holder, new Vector2(0, -140), 0f, 0.92f, -3f, 0.5f);

            var header = UiTheme.Label("Header", card, "SETTINGS", UiTheme.Display, 66, UiTheme.Ink, TextAnchor.UpperLeft);
            header.rectTransform.Place(new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(64, -118), new Vector2(0, -36));

            var tabs = UiKit.Rect("Tabs", card).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -206), new Vector2(-60, -136));
            UiKit.Horizontal(tabs.gameObject, 12, TextAnchor.MiddleLeft);
            for (int i = 0; i < TabNames.Length; i++)
            {
                int tab = i;
                var b = UiTheme.Pill("Tab " + TabNames[i], tabs, TabNames[i], UiTheme.Night, 26, () => SelectTab(tab, true), out var label);
                UiKit.Size(b, 236, 60);
                settingsTabs.Add((b.transform.GetChild(1).GetComponent<Image>(), label));
            }
            var rule = UiKit.Image("Rule", card, new Color(UiTheme.InkSoft.r, UiTheme.InkSoft.g, UiTheme.InkSoft.b, 0.25f), false);
            rule.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -226), new Vector2(-60, -223));

            settingsBody = UiKit.Rect("Rows", card).Place(Vector2.zero, Vector2.one, new Vector2(60, 140), new Vector2(-60, -240));
            var v = UiKit.Vertical(settingsBody.gameObject, 4);
            v.childControlHeight = true;

            settingsHint = UiTheme.Label("Hint", card, "", UiTheme.Hand, 28, UiTheme.InkSoft, TextAnchor.MiddleLeft);
            settingsHint.rectTransform.Place(new Vector2(0, 0), new Vector2(0.62f, 0), new Vector2(66, 40), new Vector2(0, 110));

            var footer = UiKit.Rect("Footer", card).Pin(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-56, 40), new Vector2(560, 70));
            UiKit.Horizontal(footer.gameObject, 16, TextAnchor.MiddleRight);
            UiKit.Size(UiTheme.Pill("Settings Defaults", footer, "DEFAULTS", UiTheme.Night, 24, () =>
                Confirm("Reset every setting?", "RESET", () => { GameSettings.ResetToDefaults(); SelectTab(settingsTab, false); })), 210, 66);
            var back = UiTheme.Pill("Settings Back", footer, "BACK", UiTheme.Accent, 30, HideSettings);
            back.GetComponent<PillHover>().IsBack = true;
            UiKit.Size(back, 210, 66);

            GameSettings.Changed += () =>
            {
                foreach (var r in settingsRefresh) r();
                ApplyUiSettings();
            };
            settings.gameObject.SetActive(false);
        }

        float blurBeforeSettings;

        public void ShowSettings()
        {
            if (!settings.gameObject.activeSelf && GameSettings.Atmosphere != null)
            {
                blurBeforeSettings = GameSettings.Atmosphere.BlurTarget;
                GameSettings.Atmosphere.SetBlur(1f);
            }
            settings.gameObject.SetActive(true);
            settings.SetAsLastSibling();
            Sfx.Instance?.OpenPanel();
            SelectTab(settingsTab, false);
        }

        public void HideSettings()
        {
            if (!settings.gameObject.activeSelf) return;
            settings.gameObject.SetActive(false);
            GameSettings.Save();
            GameSettings.Atmosphere?.SetBlur(blurBeforeSettings);
            SettingsClosed?.Invoke();
        }

        void SelectTab(int tab, bool sound)
        {
            settingsTab = tab;
            if (sound) Sfx.Instance?.Page();
            for (int i = 0; i < settingsTabs.Count; i++)
            {
                settingsTabs[i].face.color = i == tab ? UiTheme.Accent : UiTheme.Night;
            }
            settingsRefresh.Clear();
            UiKit.Clear(settingsBody);
            rowCount = 0;
            settingsHint.text = "";
            switch (tab)
            {
                case 0: BuildAudioTab(); break;
                case 1: BuildDisplayTab(); break;
                case 2: BuildGraphicsTab(); break;
                case 3: BuildGameplayTab(); break;
                default: BuildControlsTab(); break;
            }
        }

        void BuildAudioTab()
        {
            SliderRow("Master volume", "Everything at once.", () => GameSettings.Master, v => GameSettings.Master = v);
            SliderRow("Music", "TAD's lo-fi mixtape. Press M any time to pause it.", () => GameSettings.Music, v => GameSettings.Music = v);
            SliderRow("Sound effects", "Thuds, zips, slams, honks and every little click.", () => GameSettings.Effects, v => GameSettings.Effects = v);
            SliderRow("Ambience", "Birdsong and a summer breeze in the driveway.", () => GameSettings.Ambience, v => GameSettings.Ambience = v);
            ToggleRow("Mute in background", "Go quiet when the game window isn't focused.", () => GameSettings.MuteInBackground, v => GameSettings.MuteInBackground = v);
        }

        void BuildDisplayTab()
        {
            ChoiceRow("Window mode", "Borderless is the smoothest way to play full screen.",
                () => GameSettings.WindowModes[Mathf.Clamp(GameSettings.WindowMode, 0, 2)],
                d => GameSettings.WindowMode = (GameSettings.WindowMode + d + 3) % 3);
            ChoiceRow("Resolution", "Screen size in pixels.",
                () => GameSettings.ResolutionLabel(GameSettings.CurrentResolutionIndex()),
                d =>
                {
                    int count = GameSettings.Resolutions.Count;
                    GameSettings.ResolutionIndex = (GameSettings.CurrentResolutionIndex() + d + count) % count;
                });
            ToggleRow("V-Sync", "Match the monitor's refresh rate to avoid tearing.", () => GameSettings.VSync, v => GameSettings.VSync = v);
            ChoiceRow("Frame rate limit", "Only used when V-Sync is off.",
                () => { int c = GameSettings.FrameCaps[Mathf.Clamp(GameSettings.FrameCap, 0, GameSettings.FrameCaps.Length - 1)]; return c < 0 ? "Unlimited" : c + " FPS"; },
                d => GameSettings.FrameCap = (GameSettings.FrameCap + d + GameSettings.FrameCaps.Length) % GameSettings.FrameCaps.Length);
            SliderRow("Field of view", "How wide the camera sees.", () => GameSettings.FieldOfView, v => GameSettings.FieldOfView = Mathf.Round(v),
                32f, 55f, v => $"{Mathf.RoundToInt(v)}°");
            SliderRow("Interface size", "Scale every menu, card and button.", () => GameSettings.UiScale, v => GameSettings.UiScale = Mathf.Round(v * 20f) / 20f,
                0.8f, 1.2f, v => $"{Mathf.RoundToInt(v * 100)}%");
        }

        void BuildGraphicsTab()
        {
            ChoiceRow("Quality preset", "Sets everything below in one go. Changing any of them switches to Custom.",
                () => GameSettings.Presets[Mathf.Clamp(GameSettings.Preset, 0, 4)],
                d => GameSettings.Preset = (Mathf.Min(GameSettings.Preset, 3) + d + 4) % 4);
            SliderRow("Render resolution", "Below 100% is faster, above is sharper.", () => GameSettings.RenderScale, v => GameSettings.RenderScale = Mathf.Round(v * 20f) / 20f,
                0.5f, 1.5f, v => $"{Mathf.RoundToInt(v * 100)}%");
            ChoiceRow("Anti-aliasing", "Smooths jagged edges. MSAA + SMAA looks best.",
                () => GameSettings.AntiAliasingModes[Mathf.Clamp(GameSettings.AntiAliasing, 0, 3)],
                d => GameSettings.AntiAliasing = (GameSettings.AntiAliasing + d + 4) % 4);
            ChoiceRow("Shadows", "Soft sun shadows from the late-afternoon light.",
                () => GameSettings.ShadowModes[Mathf.Clamp(GameSettings.Shadows, 0, 4)],
                d => GameSettings.Shadows = (GameSettings.Shadows + d + 5) % 5);
            ToggleRow("Ambient occlusion", "Soft contact shadows in corners and between packed things.", () => GameSettings.AmbientOcclusion, v => GameSettings.AmbientOcclusion = v);
            ToggleRow("Ink outlines", "The hand-drawn outline around everything.", () => GameSettings.Outlines, v => GameSettings.Outlines = v);
            ToggleRow("Depth of field", "Gently blurs the far-away street.", () => GameSettings.DepthOfField, v => GameSettings.DepthOfField = v);
            ToggleRow("Bloom", "A warm glow around bright highlights.", () => GameSettings.Bloom, v => GameSettings.Bloom = v);
        }

        void BuildGameplayTab()
        {
            SliderRow("Camera speed", "How fast Q / E and right-drag swing the camera.", () => GameSettings.OrbitSpeed, v => GameSettings.OrbitSpeed = Mathf.Round(v * 10f) / 10f,
                0.4f, 2f, v => $"{v:0.0}×");
            ToggleRow("Invert camera tilt", "Flip up and down when dragging the camera.", () => GameSettings.InvertOrbit, v => GameSettings.InvertOrbit = v);
            ToggleRow("Screen shake", "A little bump when the trunk slams shut.", () => GameSettings.ScreenShake, v => GameSettings.ScreenShake = v);
            ToggleRow("Key hints", "Show the controls along the bottom while packing.", () => GameSettings.KeyHints, v => GameSettings.KeyHints = v);
            ToggleRow("Grandpa's tips", "A short note the first time each move matters. Switching them on shows them all again.",
                () => GameSettings.Tips, v => { GameSettings.Tips = v; if (v) GameController.ResetTips(); });
            ChoiceRow("Placement colours", "Blue / orange is easier to tell apart with red-green colour blindness.",
                () => GameSettings.PlacementPalettes[Mathf.Clamp(GameSettings.PlacementPalette, 0, 1)],
                d => GameSettings.PlacementPalette = (GameSettings.PlacementPalette + d + 2) % 2);
            ChoiceRow("Story text speed", "How quickly texts and notes appear.",
                () => GameSettings.TextSpeeds[Mathf.Clamp(GameSettings.TextSpeed, 0, 2)],
                d => GameSettings.TextSpeed = (GameSettings.TextSpeed + d + 3) % 3);
            ButtonRow("Erase progress", "Forget every trip, star and album photo. This can't be undone.", "ERASE", UiTheme.Stamp,
                () => Confirm("Erase all trips, stars and photos?", "ERASE", () => ResetProgressPressed?.Invoke()));
        }

        void BuildControlsTab()
        {
            var grid = UiKit.Rect("Controls", settingsBody);
            UiKit.Size(grid.gameObject.AddComponent<LayoutElement>(), -1, 460);
            var left = UiKit.Rect("Keys", grid).Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 0), new Vector2(0, -10));
            UiKit.Vertical(left.gameObject, 10).childControlHeight = false;
            foreach (var (k, what) in ControlsList) ControlRow(left, k, what, 26);
            settingsHint.text = "Hold SHIFT with R, T or F to turn the other way.";
        }

        // ------------------------------------------------------------------ rows

        RectTransform Row(string label, string hint, out RectTransform control)
        {
            var slot = UiKit.Rect(label, settingsBody);
            UiKit.Size(slot.gameObject.AddComponent<LayoutElement>(), -1, 64);
            // Layout drives the slot; the entrance animation moves the row inside it.
            var row = UiKit.Rect("Row", slot).Fill();
            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = UiKit.Rounded;
            bg.type = Image.Type.Sliced;
            float zebra = rowCount++ % 2 == 0 ? 0.35f : 0.12f;
            bg.color = new Color(UiTheme.PaperShade.r, UiTheme.PaperShade.g, UiTheme.PaperShade.b, zebra);
            var hover = row.gameObject.AddComponent<SettingsRowHover>();
            hover.Background = bg;
            hover.BaseAlpha = zebra;
            hover.OnEnter = () => settingsHint.text = hint;
            var text = UiTheme.Label("Label", row, label, UiTheme.Body, 28, UiTheme.Ink, TextAnchor.MiddleLeft);
            text.rectTransform.Place(Vector2.zero, new Vector2(0.55f, 1), new Vector2(28, 0), Vector2.zero);
            control = UiKit.Rect("Control", row).Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(600, 52));
            UiMotion.Intro(row, new Vector2(60, 0), rowCount * 0.03f, 1f, 0f, 0.35f, false);
            return slot;
        }

        void SliderRow(string label, string hint, Func<float> get, Action<float> set, float min = 0f, float max = 1f, Func<float, string> format = null)
        {
            format ??= v => $"{Mathf.RoundToInt(v * 100)}%";
            Row(label, hint, out var control);
            var area = UiKit.Rect("Slider", control).Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 0), new Vector2(-130, 0));
            var track = UiKit.Image("Track", area, new Color(UiTheme.InkSoft.r, UiTheme.InkSoft.g, UiTheme.InkSoft.b, 0.25f), true);
            track.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -7), new Vector2(0, 7));
            var fillArea = UiKit.Rect("Fill Area", area).Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -7), new Vector2(0, 7));
            var fill = UiKit.Image("Fill", fillArea, UiTheme.Accent, true);
            fill.rectTransform.Place(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(10, 0));
            var handleArea = UiKit.Rect("Handle Area", area).Place(Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-14, 0));
            var handle = UiKit.Image("Handle", handleArea, Color.white, false);
            handle.sprite = UiTheme.Circle;
            handle.rectTransform.sizeDelta = new Vector2(36, 36);
            var ring = UiKit.Image("Ring", handle.transform, UiTheme.Accent, false);
            ring.sprite = UiTheme.Ring;
            ring.raycastTarget = false;
            ring.rectTransform.Fill();
            var shadow = UiKit.Image("Shadow", handle.transform, new Color(0, 0, 0, 0.18f), false);
            shadow.sprite = UiTheme.Circle;
            shadow.raycastTarget = false;
            shadow.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(2, -4), new Vector2(2, -4));
            shadow.transform.SetAsFirstSibling();
            var slider = area.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.transition = Selectable.Transition.None;
            area.gameObject.AddComponent<PillHover>().HoverScale = 1f;
            var value = UiTheme.Label("Value", control, "", UiTheme.Display, 30, UiTheme.Ink, TextAnchor.MiddleRight);
            value.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-110, 0), Vector2.zero);

            void Refresh()
            {
                slider.SetValueWithoutNotify(get());
                value.text = format(get());
            }
            slider.onValueChanged.AddListener(v =>
            {
                set(v);
                value.text = format(get());
                if (UiTime.Now - lastTick > 0.05f)
                {
                    lastTick = UiTime.Now;
                    Sfx.Instance?.Tick();
                }
            });
            settingsRefresh.Add(Refresh);
            Refresh();
        }

        void ToggleRow(string label, string hint, Func<bool> get, Action<bool> set)
        {
            Row(label, hint, out var control);
            var sw = UiKit.Rect("Switch", control).Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(104, 50));
            var track = UiKit.Image("Track", sw, UiTheme.Teal, true);
            track.pixelsPerUnitMultiplier = 0.55f;
            track.rectTransform.Fill();
            var knob = UiKit.Image("Knob", sw, Color.white, false);
            knob.sprite = UiTheme.Circle;
            knob.raycastTarget = false;
            knob.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26, 0), new Vector2(40, 40));
            var state = UiTheme.Label("State", control, "", UiTheme.Display, 26, UiTheme.InkSoft, TextAnchor.MiddleRight);
            state.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, new Vector2(-122, 0));
            var button = sw.gameObject.AddComponent<Button>();
            button.targetGraphic = track;
            button.transition = Selectable.Transition.None;
            var anim = sw.gameObject.AddComponent<SwitchAnim>();
            anim.Track = track;
            anim.Knob = knob.rectTransform;
            var hover = sw.gameObject.AddComponent<PillHover>();
            hover.Silent = true;
            button.onClick.AddListener(() =>
            {
                bool v = !get();
                set(v);
                Sfx.Instance?.Toggle(v);
            });

            void Refresh()
            {
                bool on = get();
                anim.On = on;
                state.text = on ? "ON" : "OFF";
                state.color = on ? UiTheme.Teal : UiTheme.InkSoft;
            }
            settingsRefresh.Add(Refresh);
            Refresh();
            anim.Snap();
        }

        void ChoiceRow(string label, string hint, Func<string> get, Action<int> step)
        {
            Row(label, hint, out var control);
            var left = UiTheme.Pill("Prev", control, "<", UiTheme.Night, 28, () => { step(-1); Sfx.Instance?.Tick(); });
            ((RectTransform)left.transform).Pin(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 0), new Vector2(58, 50));
            left.GetComponent<PillHover>().Silent = true;
            var right = UiTheme.Pill("Next", control, ">", UiTheme.Night, 28, () => { step(1); Sfx.Instance?.Tick(); });
            ((RectTransform)right.transform).Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(58, 50));
            right.GetComponent<PillHover>().Silent = true;
            // Don't collide with the results "Next" button the automation looks for.
            left.name = "Choice Prev";
            right.name = "Choice Next";
            var value = UiTheme.Label("Value", control, "", UiTheme.Display, 29, UiTheme.Ink, TextAnchor.MiddleCenter);
            value.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(216, 0), new Vector2(-66, 0));
            void Refresh() => value.text = get();
            settingsRefresh.Add(Refresh);
            Refresh();
        }

        void ButtonRow(string label, string hint, string text, Color color, Action onClick)
        {
            Row(label, hint, out var control);
            var b = UiTheme.Pill("Row Button", control, text, color, 24, () => onClick());
            ((RectTransform)b.transform).Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(200, 52));
        }

        void ApplyUiSettings()
        {
            if (scaler != null) scaler.referenceResolution = new Vector2(1920, 1080) / Mathf.Clamp(GameSettings.UiScale, 0.7f, 1.3f);
            if (keyHints != null && hud != null && hud.gameObject.activeSelf && !hudHidden && !results.gameObject.activeSelf) keyHints.gameObject.SetActive(GameSettings.KeyHints);
        }
    }

    /// <summary>Highlights a settings row and shows its description while hovered.</summary>
    public class SettingsRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image Background;
        public float BaseAlpha;
        public Action OnEnter;
        bool over;
        float k;

        public void OnPointerEnter(PointerEventData e) { over = true; OnEnter?.Invoke(); }
        public void OnPointerExit(PointerEventData e) => over = false;

        void Update()
        {
            float want = over ? 1f : 0f;
            if (k == want) return;
            k = Mathf.MoveTowards(k, want, UiTime.Delta * 8f);
            var c = Background.color;
            c.a = Mathf.Lerp(BaseAlpha, 0.85f, k);
            Background.color = c;
        }
    }

    /// <summary>Animated on/off switch.</summary>
    public class SwitchAnim : MonoBehaviour
    {
        public Image Track;
        public RectTransform Knob;
        public bool On;
        float k;
        static readonly Color OffColor = new Color(0.72f, 0.69f, 0.66f);

        public void Snap()
        {
            k = On ? 1f : 0f;
            Pose();
        }

        void Update()
        {
            float want = On ? 1f : 0f;
            if (k == want) return;
            k = Mathf.MoveTowards(k, want, UiTime.Delta * 7f);
            Pose();
        }

        void Pose()
        {
            float e = Ease.OutBack(k, 1.4f);
            Knob.anchoredPosition = new Vector2(Mathf.LerpUnclamped(26f, 78f, On ? e : k), 0f);
            Track.color = Color.Lerp(OffColor, UiTheme.Teal, k);
        }
    }
}
