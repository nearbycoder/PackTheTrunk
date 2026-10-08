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
        static readonly Key[] ArrowKeys = { Key.LeftArrow, Key.RightArrow, Key.UpArrow, Key.DownArrow };

        static Key KeyFor(Bindings.Action action) => action == Bindings.Action.Turn ? Key.R : action == Bindings.Action.Tip ? Key.T : Key.F;

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

        /// <summary>
        /// Packing with the keyboard alone: the arrows walk the packing list and Enter picks a thing up; holding
        /// it, the arrows step the landing spot a cell at a time (relative to the camera) and Enter drops it.
        /// Weekend Getaway is packed and closed with keyboard events only.
        /// </summary>
        IEnumerator KeyboardOnlyChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            PerfProbe.Begin("playing");
            var mouse = Mouse.current;
            var mouseAt = mouse.position.ReadValue();
            bool tipsWere = GameSettings.Tips;
            GameSettings.Tips = false;
            Uncap();
            int weekend = LevelIndex("weekend");
            game.AutoStartLevel(weekend);
            yield return Wait(2.5f);

            // Walk the list to an item's row with ↓ (then ↑), checking the row under the cursor.
            IEnumerator WalkTo(PackItem target)
            {
                foreach (var key in new[] { Key.DownArrow, Key.UpArrow })
                    for (int i = 0; i < game.Items.Count + 6 && game.Ui.RowAt(GamepadCursor.Position) != target; i++)
                    {
                        yield return Press(key);
                        yield return Wait(0.2f);
                    }
            }
            // The trunk-space step each arrow gives, the way the game works it out from the camera.
            Vector2Int StepFor(Key key)
            {
                var rig = game.Rig;
                var world = key == Key.RightArrow ? rig.SnappedRight() : key == Key.LeftArrow ? -rig.SnappedRight() : key == Key.UpArrow ? rig.SnappedForward() : -rig.SnappedForward();
                var local = game.CurrentVehicle.transform.InverseTransformDirection(world);
                return new Vector2Int(Mathf.RoundToInt(local.x), Mathf.RoundToInt(local.z));
            }

            int placed = 0, steps = 0, exact = 0, liftChecks = 0;
            bool hintsFollow = false;
            string hintsSeen = "";
            bool lifted = false;
            string failure = null;
            var placements = solutions["weekend"];
            foreach (var (itemId, cells) in placements)
            {
                var next = game.Items.FirstOrDefault(i => i.Def.Id == itemId && i.State == ItemState.Pile);
                if (next == null) { failure = $"no {itemId} on the blanket"; break; }
                yield return WalkTo(next);
                if (game.Ui.RowAt(GamepadCursor.Position) != next) { failure = $"the arrows never reached the {next.Def.Name}'s row"; break; }
                yield return Press(Key.Enter);
                yield return Wait(0.3f);
                if (game.Held != next || !game.KeyAiming) { failure = $"Enter on the {next.Def.Name}'s row picked up {(game.Held != null ? game.Held.Def.Name : "nothing")} (aiming {game.KeyAiming})"; break; }

                var min = cells.Aggregate(Vector3Int.Min);
                string want = ShapeKey(next.Def.Shape.Rotated(FindOrientation(next.Def.Shape, cells)));
                var path = ClicksToOrient(next, want);
                if (path == null) { failure = $"no keys turn the {next.Def.Name} the solver's way"; break; }
                foreach (var action in path)
                {
                    yield return Press(KeyFor(action));
                    yield return Wait(0.12f);
                }
                yield return Wait(0.1f);

                // Step towards the solver's column, one arrow press at a time, checking each step.
                for (int tries = 0; tries < 24; tries++)
                {
                    if (!game.CurrentTarget(out var at, out _)) { failure = $"no ghost while aiming the {next.Def.Name}"; break; }
                    var need = new Vector2Int(min.x - at.x, min.z - at.z);
                    if (need == Vector2Int.zero) break;
                    var key = ArrowKeys.OrderByDescending(k => Vector2.Dot(StepFor(k), need)).First();
                    var expect = StepFor(key);
                    yield return Press(key);
                    yield return Wait(0.12f);
                    game.CurrentTarget(out var moved, out _);
                    steps++;
                    if (moved.x - at.x == expect.x && moved.z - at.z == expect.y) exact++;
                }
                if (failure != null) break;
                for (int tries = 0; tries < 6 && game.CurrentTarget(out var p, out _) && p.y != min.y; tries++)
                {
                    yield return Press(p.y < min.y ? Key.W : Key.S);
                    yield return Wait(0.12f);
                }
                game.CurrentTarget(out var target, out bool valid);
                if (target != min || !valid) { failure = $"the {next.Def.Name}'s ghost is at {target} ({(valid ? "fits" : "won't fit")}), not {min}"; break; }
                if (placed == 2)
                {
                    var captions = game.Ui.KeyboardHintCaptions();
                    var keys = game.Ui.KeyboardHintKeys();
                    var wrapped = game.Ui.WrappedKeyHints();
                    hintsFollow = captions.Contains("move") && keys.Contains("ARROWS") && keys.Contains("ENTER") && !keys.Contains("CLICK") && wrapped.Count == 0;
                    hintsSeen = string.Join(", ", keys.Zip(captions, (k, c) => k + " " + c));
                    yield return Shot("keyboard-aiming");
                }
                yield return Press(Key.Enter);
                yield return Wait(0.45f);
                if (next.GridPos != min || (next.State != ItemState.Packed && next.State != ItemState.Dropping)) { failure = $"Enter didn't drop the {next.Def.Name} at {min}"; break; }
                placed++;

                // Once: Enter on a packed row lifts it back out of the trunk, and Escape puts it back where it was.
                if (placed == 1)
                {
                    yield return Wait(0.3f);
                    yield return WalkTo(next);
                    yield return Press(Key.Enter);
                    yield return Wait(0.3f);
                    lifted = game.Held == next;
                    yield return Press(Key.Escape);
                    yield return Wait(0.4f);
                    lifted &= game.Held == null && next.State == ItemState.Packed && next.GridPos == min;
                    liftChecks++;
                }
            }
            yield return Wait(0.6f);
            yield return Shot("keyboard-packed");
            if (failure == null)
            {
                yield return Press(Key.Space);
                for (float t = 0f; t < 9f && !game.IsShowingResults; t += Time.unscaledDeltaTime) yield return null;
                yield return Wait(1.2f);
            }
            bool mouseUntouched = mouse.position.ReadValue() == mouseAt;
            Check(liftChecks == 1 && lifted, "keyboard only: Enter on a packed thing's row lifts it back out of the trunk, and Escape puts it back where it was");
            Check(steps > 0 && exact == steps, $"keyboard only: every arrow press moved the landing spot exactly one cell the way the camera faces ({exact} of {steps})");
            Check(failure == null && placed == placements.Count && game.IsShowingResults && game.LastStars == 3 && mouseUntouched,
                $"keyboard only: Weekend Getaway packed {placed}/{placements.Count} and closed for {game.LastStars} stars with keyboard events only ({steps} arrow steps)"
                + (failure != null ? $" (stopped: {failure})" : ""));
            Check(hintsFollow, $"keyboard only: while the arrows aim, the key hints say how ({hintsSeen}) and fit on one line");
            yield return NudgeMouse(mouse);
            Check(!GamepadCursor.KeysActive && game.Ui.KeyboardHintKeys().Contains("CLICK"), "keyboard only: moving the mouse hands back, and the key hints go back to CLICK and WHEEL");
            GameController.AutoForgetTrunk("weekend");
            GameSettings.Tips = tipsWere;
            Uncap();
        }
    }
}
