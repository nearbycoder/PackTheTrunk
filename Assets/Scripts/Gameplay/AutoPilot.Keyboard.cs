using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PackTheTrunk
{
    public partial class AutoPilot
    {
        /// <summary>Move the real mouse a little: that hands pointing back to it from the keyboard cursor.</summary>
        IEnumerator NudgeMouse(Mouse mouse)
        {
            var p = mouse.position.ReadValue();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = p + new Vector2(12f, 0f), delta = new Vector2(12f, 0f) });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = p + new Vector2(12f, 0f) });
            yield return null;
            yield return Wait(0.1f);
        }

        /// <summary>
        /// Menus with the keyboard: the arrow keys move a cursor between buttons, sliders and switches, and
        /// Enter clicks what it's on, instead of the screen's own Enter / Space job. The mouse takes over again
        /// as soon as it moves.
        /// </summary>
        IEnumerator KeyMenuChecks()
        {
            var mouse = Mouse.current;
            var visited = new List<string>();
            IEnumerator Nav(Key key)
            {
                yield return Press(key);
                yield return Wait(0.25f);
                visited.Add(GamepadCursor.LastNavigation);
            }
            IEnumerator NavTo(string name, params Key[] keys)
            {
                foreach (var key in keys)
                    for (int i = 0; i < 8 && GamepadCursor.LastNavigation != name; i++) yield return Nav(key);
            }

            PerfProbe.Begin("menus");
            game.AutoShowMainMenu();
            yield return Wait(2f);
            yield return Nav(Key.DownArrow);
            bool shown = GamepadCursor.KeysActive && !GamepadCursor.Active && visited[0] != "";
            visited.Clear();
            yield return NavTo("Settings", Key.DownArrow, Key.UpArrow);
            bool onSettings = GamepadCursor.LastNavigation == "Settings";
            yield return Shot("keyboard-menu");
            yield return Press(Key.Enter);
            yield return Wait(1.2f);
            Check(shown && onSettings && Visible("Settings Back"),
                $"keyboard menus: an arrow key shows the cursor (keyboard hints stay), the arrows walk the main menu ({string.Join(" > ", visited)}) and Enter on SETTINGS opens it");

            visited.Clear();
            yield return NavTo("Tab AUDIO", Key.UpArrow, Key.LeftArrow);
            yield return Press(Key.Enter);
            yield return Wait(0.8f);
            float master = GameSettings.Master;
            yield return Nav(Key.DownArrow);
            bool onSlider = GamepadCursor.LastNavigation == "Slider";
            yield return Nav(Key.RightArrow);
            float raised = GameSettings.Master;
            yield return Nav(Key.LeftArrow);
            float lowered = GameSettings.Master;
            Check(onSlider && raised > master + 0.01f && Mathf.Abs(lowered - master) < 0.011f,
                $"keyboard menus: on the master volume slider → raises it ({master:0.00} > {raised:0.00}) and ← lowers it ({lowered:0.00}); {string.Join(" > ", visited)}");
            GameSettings.Master = master;
            Uncap();
            yield return Press(Key.Escape);
            yield return Wait(0.8f);

            // The pause menu: ↓ / ↑ to RESUME, Enter resumes.
            PerfProbe.Begin("playing");
            game.AutoStartLevel(LevelIndex("weekend"));
            yield return Wait(2.5f);
            yield return Press(Key.Escape);
            yield return Wait(1f);
            bool paused = game.IsPaused;
            visited.Clear();
            yield return Nav(Key.DownArrow);
            yield return NavTo("Resume", Key.UpArrow, Key.DownArrow);
            yield return Press(Key.Enter);
            yield return Wait(0.6f);
            Check(paused && GamepadCursor.LastNavigation == "Resume" && game.IsPlaying && !game.IsPaused,
                $"keyboard menus: the arrows walk the pause menu ({string.Join(" > ", visited)}) and Enter on RESUME resumes");

            // The postcard: Enter on TRY AGAIN tries again (Enter alone would have gone on to the next trip).
            var wagon = LevelIndex("wagon");
            game.AutoStartLevel(wagon);
            yield return Wait(2.5f);
            foreach (var (id, cells) in LoadSolutions()["wagon"])
            {
                var it = game.Items.First(i => i.Def.Id == id && i.State == ItemState.Pile);
                game.AutoPlace(it, FindOrientation(it.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                yield return Wait(0.05f);
            }
            yield return Wait(0.5f);
            game.AutoClose();
            for (float t = 0f; t < 9f && !(game.IsShowingResults && game.Ui.ResultsReady); t += Time.unscaledDeltaTime) yield return null;
            yield return Wait(1f);
            visited.Clear();
            yield return Nav(Key.DownArrow);
            yield return NavTo("Retry", Key.LeftArrow, Key.RightArrow);
            bool onRetry = GamepadCursor.LastNavigation == "Retry";
            yield return Shot("keyboard-postcard");
            yield return Press(Key.Enter);
            for (float t = 0f; t < 6f && !game.IsPlaying; t += Time.unscaledDeltaTime) yield return null;
            yield return Wait(2f);
            Check(onRetry && game.IsPlaying && game.CurrentLevelIndex == wagon,
                $"keyboard menus: on the postcard the arrows reach TRY AGAIN ({string.Join(" > ", visited)}) and Enter tries the wagon again instead of going on");

            // Moving the mouse hands straight back.
            yield return NudgeMouse(mouse);
            Check(!GamepadCursor.KeysActive && Mouse.current == mouse && Cursor.visible, "keyboard menus: moving the mouse hides the keyboard cursor and hands back to the mouse");
            GameController.AutoForgetTrunk("weekend");
            GameController.AutoForgetTrunk("wagon");
        }
    }
}
