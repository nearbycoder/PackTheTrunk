using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace PackTheTrunk
{
    public partial class AutoPilot
    {
        static Slider FindSlider(string name) =>
            FindObjectsByType<Slider>(FindObjectsInactive.Exclude).FirstOrDefault(s => s.name == name);

        /// <summary>The value text beside the Graphics fidelity slider.</summary>
        static string FidelityLabel(Slider slider) => slider != null ? slider.transform.parent.Find("Value")?.GetComponent<Text>()?.text ?? "" : "";

        /// <summary>A settings row's switch (every switch is called "Switch"; its row is named after its label).</summary>
        static Button RowSwitch(string row) =>
            FindObjectsByType<Button>(FindObjectsInactive.Exclude).FirstOrDefault(b => b.name == "Switch" && b.GetComponentsInParent<Transform>().Any(t => t.name == row));

        /// <summary>A point along the slider's travel (0 = the left end, 1 = the right end), in screen pixels.</summary>
        static Vector2 SliderPoint(Slider slider, float t)
        {
            var area = (RectTransform)slider.handleRect.parent;
            var corners = new Vector3[4];
            area.GetWorldCorners(corners);
            return new Vector2(Mathf.Lerp(corners[0].x, corners[2].x, t), (corners[0].y + corners[2].y) * 0.5f);
        }

        IEnumerator PressAt(Vector2 screen)
        {
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = screen });
            yield return null;
            yield return null;
            yield return Click();
            yield return Wait(0.3f);
            Uncap();
        }

        /// <summary>What each step must have applied, read back from the renderer.</summary>
        static string FidelityProblems(int step, bool saved = true)
        {
            var problems = new List<string>();
            float[] scale = { 0.75f, 1f, 1f, 1.5f };
            int[] msaa = { 1, 1, 4, 4 };
            int[] shadowMap = { 1024, 2048, 4096, 8192 };
            float[] particles = { 0.5f, 0.75f, 1f, 1.6f };
            string[] ao = { "off", "Medium samples, half res", "Medium samples, full res", "High samples, full res" };
            if (GameSettings.Fidelity != step) problems.Add($"step {GameSettings.Fidelity}");
            if (GameSettings.FidelityCustom) problems.Add("reads Custom");
            if (!Mathf.Approximately(GameSettings.AppliedRenderScale, scale[step])) problems.Add($"render scale {GameSettings.AppliedRenderScale}");
            if (GameSettings.AppliedMsaa != msaa[step]) problems.Add($"MSAA {GameSettings.AppliedMsaa}");
            if (GameSettings.ShadowMapResolution != shadowMap[step]) problems.Add($"shadow map {GameSettings.ShadowMapResolution}");
            if (!GameSettings.OcclusionDescription().StartsWith(ao[step])) problems.Add($"AO {GameSettings.OcclusionDescription()}");
            if (!Mathf.Approximately(GameSettings.ParticleDensity, particles[step])) problems.Add($"particles {GameSettings.ParticleDensity}");
            if (saved && (Prefs.GetInt("ptt.set.fidelity", -1) != step || Prefs.GetInt("ptt.set.preset", -1) != step)) problems.Add("not saved");
            return problems.Count == 0 ? "" : ": " + string.Join(", ", problems);
        }

        /// <summary>
        /// The Graphics fidelity slider through the real settings screen: click and drag it with the mouse,
        /// step it with the arrow keys, check what each step applied to the renderer, that changing a row
        /// below reads Custom (and picking the step again sets the rows back), that a save from before the
        /// slider keeps its choice, and that DEFAULTS puts it back on High. The D-pad is checked with the pad.
        /// </summary>
        IEnumerator FidelityChecks()
        {
            var mouse = Mouse.current;
            PerfProbe.Begin("menus");
            game.AutoShowMainMenu();
            yield return Wait(2f);
            yield return ClickUi("Settings");
            yield return Wait(1.2f);
            yield return ClickUi("Tab GRAPHICS");
            yield return Wait(0.9f);
            var slider = FindSlider("Fidelity Slider");
            Check(slider != null && slider.wholeNumbers && slider.maxValue == 3 && FidelityLabel(slider) == "High" && FidelityProblems(2, false) == "",
                $"fidelity: the Graphics tab has the slider (4 steps), on High by default{FidelityProblems(2, false)} [{GameSettings.RenderDescription()}]");
            if (slider == null) yield break;

            yield return PressAt(SliderPoint(slider, 1f));
            Check(FidelityLabel(slider) == "Ultra" && FidelityProblems(3) == "",
                $"fidelity: clicking the slider's right end picks Ultra{FidelityProblems(3)} [{GameSettings.RenderDescription()}]");
            yield return Wait(0.6f);
            yield return Shot("fidelity-ultra");

            yield return PressAt(SliderPoint(slider, 0f));
            Check(FidelityLabel(slider) == "Low" && FidelityProblems(0) == "",
                $"fidelity: clicking the left end picks Low{FidelityProblems(0)} [{GameSettings.RenderDescription()}]");

            // Drag from Low to a third of the way: Medium.
            InputSystem.QueueStateEvent(mouse, new MouseState { position = SliderPoint(slider, 0f) }.WithButton(MouseButton.Left, true));
            yield return null;
            yield return null;
            for (int i = 1; i <= 8; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = SliderPoint(slider, i / 24f) }.WithButton(MouseButton.Left, true));
                yield return null;
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = SliderPoint(slider, 1f / 3f) });
            yield return null;
            yield return Wait(0.3f);
            Uncap();
            Check(FidelityLabel(slider) == "Medium" && FidelityProblems(1) == "",
                $"fidelity: dragging it a third of the way picks Medium{FidelityProblems(1)} [{GameSettings.RenderDescription()}]");

            // A fine-tune row: Custom, and the step's own detail stays.
            var bloom = RowSwitch("Bloom");
            if (bloom != null)
            {
                var rt = (RectTransform)bloom.transform;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = rt.TransformPoint(rt.rect.center) });
                yield return null;
                yield return null;
                yield return Click();
                yield return Wait(0.3f);
                Uncap();
            }
            bool custom = GameSettings.FidelityCustom && FidelityLabel(slider) == "Custom" && !GameSettings.Bloom && GameSettings.Fidelity == 1
                          && Mathf.Approximately(GameSettings.ParticleDensity, 0.75f);
            yield return Shot("fidelity-custom");
            yield return PressAt(SliderPoint(slider, 1f / 3f));
            Check(bloom != null && custom && GameSettings.Bloom && FidelityLabel(slider) == "Medium" && FidelityProblems(1) == "",
                $"fidelity: switching Bloom off reads Custom (Medium's detail stays), and clicking Medium again sets the rows back{FidelityProblems(1)}");

            // The arrow keys: walk to the slider, → and ←.
            var visited = new List<string>();
            for (int i = 0; i < 10 && GamepadCursor.LastNavigation != "Fidelity Slider"; i++)
            {
                yield return Press(i < 5 ? Key.UpArrow : Key.DownArrow);
                yield return Wait(0.25f);
                visited.Add(GamepadCursor.LastNavigation);
            }
            bool onSlider = GamepadCursor.LastNavigation == "Fidelity Slider";
            yield return Press(Key.RightArrow);
            yield return Wait(0.25f);
            Uncap();
            int up = GameSettings.Fidelity;
            yield return Press(Key.RightArrow);
            yield return Wait(0.25f);
            Uncap();
            int up2 = GameSettings.Fidelity;
            yield return Press(Key.RightArrow);
            yield return Wait(0.25f);
            Uncap();
            int capped = GameSettings.Fidelity;
            yield return Press(Key.LeftArrow);
            yield return Wait(0.25f);
            Uncap();
            Check(onSlider && up == 2 && up2 == 3 && capped == 3 && FidelityProblems(2) == "" && FidelityLabel(slider) == "High",
                $"fidelity: the arrow keys reach the slider ({string.Join(" > ", visited)}), → steps Medium > {up} > {up2} and stops at Ultra ({capped}), ← steps back to High{FidelityProblems(2)}");
            yield return NudgeMouse(mouse);

            // A save from before the slider: its preset becomes the step, Custom keeps its rows on High.
            Prefs.DeleteKey("ptt.set.fidelity");
            Prefs.SetInt("ptt.set.preset", 3);
            GameSettings.Tips = GameSettings.Tips; // any setting re-reads the cache
            int legacyUltra = GameSettings.Fidelity;
            Prefs.DeleteKey("ptt.set.fidelity");
            Prefs.SetInt("ptt.set.preset", 4);
            GameSettings.Tips = GameSettings.Tips;
            int legacyCustom = GameSettings.Fidelity;
            bool legacyCustomReads = GameSettings.FidelityCustom;
            Check(legacyUltra == 3 && legacyCustom == 2 && legacyCustomReads,
                $"fidelity: a round-11 save keeps its choice (preset Ultra reads step {legacyUltra}; Custom reads step {legacyCustom}, Custom {legacyCustomReads})");
            Uncap();

            yield return ClickUi("Settings Defaults");
            yield return Wait(0.6f);
            yield return ClickUi("Confirm Yes");
            yield return Wait(0.8f);
            Uncap();
            slider = FindSlider("Fidelity Slider");
            bool defaultsHigh = GameSettings.Fidelity == 2 && !GameSettings.FidelityCustom && FidelityLabel(slider) == "High"
                                && Mathf.Approximately(GameSettings.AppliedRenderScale, 1f) && GameSettings.ShadowMapResolution == 4096;
            Check(defaultsHigh && FidelityProblems(2, false) == "", $"fidelity: DEFAULTS puts it back on High{FidelityProblems(2, false)} [{GameSettings.RenderDescription()}]");
            yield return Press(Key.Escape);
            yield return Wait(0.8f);
            Uncap();
        }
    }
}
