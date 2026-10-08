using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PackTheTrunk
{
    /// <summary>
    /// Scripted self-test, only active when the player is launched with
    /// <c>-pttAutopilot &lt;screenshotDir&gt; [-pttSolutions &lt;file&gt;]</c>.
    /// Drives the game through queued mouse/keyboard events, packs levels from solver output
    /// and saves screenshots, then quits. Results are logged with a [AutoPilot] prefix.
    /// </summary>
    public partial class AutoPilot : MonoBehaviour
    {
        string outDir;
        string solutionsPath;
        GameController game;
        int shot;
        bool quick, layoutOnly;
        string resumePhase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-pttAutopilot");
            if (i < 0 || i + 1 >= args.Length) return;
            var pilot = new GameObject("AutoPilot").AddComponent<AutoPilot>();
            pilot.outDir = args[i + 1];
            pilot.quick = Array.IndexOf(args, "-pttQuick") >= 0;
            pilot.layoutOnly = Array.IndexOf(args, "-pttLayoutOnly") >= 0;
            int r = Array.IndexOf(args, "-pttResumeTest");
            if (r >= 0 && r + 1 < args.Length) pilot.resumePhase = args[r + 1];
            int s = Array.IndexOf(args, "-pttSolutions");
            if (s >= 0 && s + 1 < args.Length) pilot.solutionsPath = args[s + 1];
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            if (layoutOnly)
            {
                // Just the HUD layout pass, at whatever window size the player was launched with.
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                yield return null;
                Uncap();
                game = FindAnyObjectByType<GameController>();
                yield return Wait(3f);
                yield return LayoutChecks();
                yield return MenuLayoutChecks();
                yield return LegibilityChecks(LoadSolutions());
                Log("done");
                Application.Quit();
                yield break;
            }
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            if (resumePhase != null)
            {
                yield return null;
                game = FindAnyObjectByType<GameController>();
                yield return ResumeCrashTest(resumePhase);
                Log("done");
                Application.Quit();
                yield break;
            }
            PerfProbe.Attach();
            yield return null;
            Uncap();
            game = FindAnyObjectByType<GameController>();
            Log("started, levels: " + GameDatabase.Levels.Count);

            // Menus: title, main menu, every settings tab, credits, trip map and album.
            PerfProbe.Begin("title");
            yield return Wait(4f);
            yield return Shot("title");
            yield return Press(Key.Space);
            yield return Wait(1.6f);
            Check(Visible("Continue"), "a key press on the title opens the main menu");
            PerfProbe.Begin("menus");
            yield return Shot("main-menu");
            yield return ClickUi("Settings");
            yield return Wait(1.2f);
            Check(Visible("Settings Back"), "Settings opens");
            yield return Shot("settings-audio");
            foreach (var tab in new[] { "DISPLAY", "GRAPHICS", "GAMEPLAY", "ACCESSIBILITY", "CONTROLS" })
            {
                yield return ClickUi("Tab " + tab);
                yield return Wait(0.9f);
                yield return Shot("settings-" + tab.ToLowerInvariant());
            }
            yield return Press(Key.Escape);
            yield return Wait(0.6f);
            Check(!Visible("Settings Back"), "Escape closes Settings");
            yield return ClickUi("Credits");
            yield return Wait(5f);
            yield return Shot("credits");
            yield return Press(Key.Escape);
            yield return Wait(0.6f);
            yield return ClickUi("Trip Map");
            yield return Wait(1.6f);
            Check(Visible("Map Back") && Visible("Next Page"), "Trip Map opens");
            yield return Shot("menu");
            yield return Press(Key.Escape);
            yield return Wait(1f);
            Check(Visible("Continue"), "Escape on the map goes back to the main menu");

            var solutions = LoadSolutions();
            PerfProbe.Begin("story + arrival");
            game.AutoBeginTrip(0);
            yield return Wait(5f);
            yield return Shot("story");
            PerfProbe.Begin("playing");
            game.AutoStartLevel(0);
            yield return Wait(2f);
            yield return Shot("level1-start");

            // Real input: hover and click the first pile item, aim at the trunk floor.
            var first = game.Items[0];
            yield return MoveMouse(first.transform.position + (Vector3)first.Shape.Center);
            yield return Wait(0.3f);
            yield return Shot("level1-hover");
            yield return Click();
            yield return Wait(0.3f);
            Check(game.Held == first, "clicking a pile item picks it up");

            yield return MoveMouse(game.CurrentVehicle.transform.TransformPoint(new Vector3(1.5f, 0f, 1.5f)));
            yield return Wait(0.5f);
            Check(game.HasValidTarget, "aiming at the empty trunk floor gives a valid target");

            // Carry it around over the trunk for a few seconds (targeting, ghost, lean and sway).
            PerfProbe.Begin("holding");
            var trunk = game.CurrentVehicle.transform;
            for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
            {
                var local = new Vector3(1.5f + Mathf.Sin(t * 2.1f) * 1.4f, 0f, 1.5f + Mathf.Cos(t * 1.7f) * 1.2f);
                var screen = game.Camera.WorldToScreenPoint(trunk.TransformPoint(local));
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(screen.x, screen.y) });
                yield return null;
            }
            yield return MoveMouse(trunk.TransformPoint(new Vector3(1.5f, 0f, 1.5f)));
            yield return Wait(0.3f);
            PerfProbe.Begin("playing");
            yield return Shot("level1-aim");
            yield return Press(Key.R);
            yield return Wait(0.4f);
            yield return Shot("level1-turned");
            yield return Click();
            yield return Wait(0.8f);
            Check(first.State == ItemState.Packed, "clicking drops the item into the trunk");
            yield return Shot("level1-dropped");
            yield return GhostChecks(first);
            yield return Press(Key.Z);
            yield return Wait(0.6f);
            Check(first.State == ItemState.Pile, "Z undoes the placement");

            yield return Press(Key.Escape);
            yield return Wait(1.2f);
            Check(game.IsPaused && Time.timeScale == 0f, "Escape pauses the game");
            yield return Shot("pause");
            yield return Press(Key.Escape);
            yield return Wait(0.6f);
            Check(!game.IsPaused && Time.timeScale == 1f, "Escape again resumes");

            // Play every trip in story order: pack it completely, close the trunk, check the postcard.
            for (int level = 0; level < GameDatabase.Levels.Count; level++)
            {
                var def = GameDatabase.Levels[level];
                if (quick && level != 0 && level != 5 && level != GameDatabase.Levels.Count - 1) continue;
                PerfProbe.Begin("packing");
                if (level != 0) game.AutoStartLevel(level);
                yield return Wait(level == 0 ? 0.2f : 1.2f);
                if (def.Id == "grandma") yield return Wait(2f);
                yield return Shot(def.Id + "-start");

                if (!solutions.TryGetValue(def.Id, out var placements))
                {
                    Check(false, $"{def.Id}: no solver solution");
                    continue;
                }
                int ok = 0;
                foreach (var (itemId, cells) in placements)
                {
                    var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                    if (item == null) { Check(false, $"{def.Id}: no free {itemId} in pile"); continue; }
                    var min = cells.Aggregate(Vector3Int.Min);
                    var orientation = FindOrientation(item.Def.Shape, cells);
                    if (game.AutoPlace(item, orientation, min)) ok++;
                    else Check(false, $"{def.Id}: could not place {itemId} at {min}");
                    yield return Wait(0.03f);
                }
                yield return Wait(0.8f);
                Check(ok == placements.Count, $"{def.Id}: packed {ok}/{placements.Count}");
                yield return Shot(def.Id + "-packed");

                int meterAtClose = game.Ui.MeterStars;
                PerfProbe.Begin("close + drive-off");
                yield return Press(Key.Space);
                for (float t = 0f; t < 9f && !game.IsShowingResults; t += Time.unscaledDeltaTime) yield return null;
                PerfProbe.Begin("results");
                yield return Wait(1.2f);
                Check(game.IsShowingResults, $"{def.Id}: closing the trunk shows the postcard");
                tripsClosed++;
                if (meterAtClose != game.LastStars) meterMismatches.Add($"{def.Id} meter {meterAtClose} postcard {game.LastStars}");
                if (level == 0 || level == GameDatabase.Levels.Count - 1 || level % 8 == 4) yield return Shot(def.Id + "-results");
                if (level == 0) Check(!AnyText("gnome"), "the wagon's perfect postcard doesn't mention a gnome (there isn't one)");
                if (level == 0) Check(game.Ui.ResultsShowSeal && Prefs.GetInt("ptt.seal.wagon") == 1, "seal: the wagon, packed without a hint, gets Grandpa's seal on the postcard");

                if (level == 0)
                {
                    // Keyboard flow: Space on the postcard, then Space through the next story.
                    yield return Wait(0.6f);
                    yield return Press(Key.Space);
                    yield return Wait(2f);
                    Check(game.IsInStory, "Space on the postcard goes on to the next trip");
                    for (float t = 0f; t < 20f && !game.IsPlaying; t += 0.5f)
                    {
                        yield return Press(Key.Space);
                        yield return Wait(0.5f);
                    }
                    Check(game.IsPlaying, "Space hurries the story along and starts packing");
                }
            }

            Check(meterMismatches.Count == 0, $"stars: the meter matched the postcard on all {tripsClosed} trips" + (meterMismatches.Count > 0 ? " (" + string.Join(", ", meterMismatches) + ")" : ""));
            yield return EarlyCloseChecks(solutions);
            Check(Prefs.GetInt("ptt.seal.wagon") == 1, "seal: the 1-star replay doesn't take the wagon's seal away");
            yield return SealChecks(solutions);
            yield return PhotoChecks();
            yield return AlbumChecks();
            yield return TipChecks(solutions);
            yield return HintChecks();
            yield return RestartChecks(solutions);
            yield return ResumeChecks(solutions);
            yield return ResumeHistoryChecks(solutions);
            yield return WaitingTrunkChecks(solutions);
            yield return DragChecks(solutions);
            yield return RedoChecks(solutions);
            yield return StarMeterChecks(solutions);
            yield return BestSoFarChecks(solutions);
            yield return SeeThroughChecks();
            yield return MouseOnlyChecks(solutions);
            yield return MouseRedoChecks(solutions);
            yield return KeyMenuChecks();
            yield return KeyboardOnlyChecks(solutions);
            yield return FavourChecks();
            yield return RebindChecks();
            yield return LayoutChecks();
            yield return MenuLayoutChecks();
            yield return LegibilityChecks(solutions);
            yield return ReduceMotionChecks(solutions);
            yield return InputReportChecks();
            // Last: once the gamepad has been used, Mouse.current is its virtual cursor.
            yield return GamepadChecks();

            // The finale: Grandma's note, then the family album.
            PerfProbe.Begin("ending + album");
            game.AutoShowEnding();
            yield return Wait(7f);
            yield return Shot("ending-note");
            game.AutoShowAlbum();
            yield return WaitForAlbum(12f);
            yield return Shot("album");

            PerfProbe.Report();
            Log("done");
            Application.Quit();
        }

        /// <summary>
        /// Replay the (already 3-star) wagon with only the essentials: one Space must not close while
        /// extras still fit, a second must, and the 1-star close must keep the 3-star album photo.
        /// </summary>
        IEnumerator EarlyCloseChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            var photo = GameController.PhotoPath("wagon");
            string before = File.Exists(photo) ? Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(photo))) : null;
            Check(before != null && Prefs.GetInt("ptt.stars.wagon") == 3, "the wagon has a 3-star photo before the replay");

            PerfProbe.Begin("packing");
            game.AutoStartLevel(0);
            yield return Wait(1.2f);
            foreach (var (itemId, cells) in solutions["wagon"])
            {
                var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && !it.IsBonus && it.State == ItemState.Pile);
                if (item != null) game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                yield return Wait(0.03f);
            }
            yield return Wait(0.8f);
            int meter = game.Ui.MeterStars;
            yield return Press(Key.Space);
            yield return Wait(0.6f);
            Check(game.IsPlaying && !game.IsShowingResults, "one Space with extras that still fit doesn't close the trunk");
            yield return Shot("early-close-prompt");
            yield return Press(Key.Space);
            for (float t = 0f; t < 9f && !game.IsShowingResults; t += Time.unscaledDeltaTime) yield return null;
            Check(game.IsShowingResults, "a second Space closes it anyway");
            Check(meter == 1 && game.LastStars == 1, $"stars: essentials only, the meter showed {meter} and the postcard {game.LastStars}");
            yield return Wait(2f);
            string after = File.Exists(photo) ? Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(photo))) : null;
            Check(before != null && after == before && Prefs.GetInt("ptt.stars.wagon") == 3, "a 1-star replay keeps the 3-star photo and stars");
        }

        /// <summary>
        /// Hold a second item over the (1-tall) wagon: over the packed item it can't fit, over an empty
        /// cell it can. Screenshot both in both placement palettes for the colour-blind check.
        /// </summary>
        IEnumerator GhostChecks(PackItem packed)
        {
            var second = game.Items.First(i => i != packed && i.State == ItemState.Pile);
            yield return MoveMouse(second.transform.position + (Vector3)second.Shape.Center);
            yield return Click();
            yield return Wait(0.3f);
            if (game.Held != second) { Check(false, "picking up a second item for the ghost check"); yield break; }
            var trunk = game.CurrentVehicle.transform;
            for (int palette = 0; palette < GameSettings.PlacementPalettes.Length; palette++)
            {
                GameSettings.PlacementPalette = palette;
                Uncap();
                yield return MoveMouse(packed.transform.position + (Vector3)packed.Shape.Center + Vector3.up * 0.5f);
                yield return Wait(0.4f);
                if (palette == 0) Check(game.Held == second && !game.HasValidTarget, "over the packed item the ghost says it won't fit");
                yield return Shot($"ghost-bad-{GameSettings.PlacementPalettes[palette].Replace(" / ", "-").ToLowerInvariant()}");
                bool found = false;
                for (int x = 0; x < game.TrunkSize.x && !found; x++)
                for (int z = 0; z < game.TrunkSize.z && !found; z++)
                {
                    yield return MoveMouse(trunk.TransformPoint(new Vector3(x + 0.5f, 0f, z + 0.5f)));
                    yield return Wait(0.15f);
                    found = game.HasValidTarget;
                }
                yield return Wait(0.3f);
                if (palette == 0) Check(found, "over an empty spot the ghost says it fits");
                yield return Shot($"ghost-ok-{GameSettings.PlacementPalettes[palette].Replace(" / ", "-").ToLowerInvariant()}");
            }
            GameSettings.PlacementPalette = 0;
            Uncap();
            yield return Press(Key.Escape);
            yield return Wait(0.5f);
            Check(game.Held == null && second.State == ItemState.Pile, "Escape puts the held item back");
        }

        IEnumerator WaitForTip(string tip, float timeout)
        {
            for (float t = 0f; t < timeout && game.ActiveTip != tip; t += Time.unscaledDeltaTime) yield return null;
        }

        /// <summary>
        /// From a clean slate, trigger each of Grandpa's tips the way a player would and check it shows
        /// (and goes away when the player does the thing); then check none of them ever repeats.
        /// </summary>
        IEnumerator TipChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            game.AutoResetTips();
            var trunk = (Func<Transform>)(() => game.CurrentVehicle.transform);

            // Trip 1, first look: pick something up.
            PerfProbe.Begin("playing");
            game.AutoStartLevel(0);
            yield return WaitForTip("pickup", 4f);
            Check(game.ActiveTip == "pickup", "tip: a new trip says to pick something up");
            yield return Wait(0.6f);
            yield return Shot("tip-pickup");

            // Let everything land on the blanket, then click (retrying: a loaded machine can drop a click).
            for (float t = 0f; t < 6f && game.Items.Any(i => i.IsFalling); t += Time.unscaledDeltaTime) yield return null;
            var first = game.Items.First(i => !i.Def.Fragile && i.State == ItemState.Pile);
            for (int attempt = 0; attempt < 3 && game.Held == null; attempt++)
            {
                yield return MoveMouse(first.transform.position + (Vector3)first.Shape.Center);
                yield return Wait(0.2f);
                yield return Click();
                yield return Wait(0.2f);
            }
            // Whatever the click landed on (things overlap a little on the blanket).
            var basket = game.Held;
            if (basket == null) { Check(false, "tip: picking something up on the wagon"); yield break; }
            yield return WaitForTip("aim", 2f);
            Check(game.ActiveTip == "aim", "tip: holding something explains the ghost");
            bool found = false;
            for (int x = 0; x < game.TrunkSize.x && !found; x++)
            for (int z = 0; z < game.TrunkSize.z && !found; z++)
            {
                yield return MoveMouse(trunk().TransformPoint(new Vector3(x + 0.5f, 0f, z + 0.5f)));
                yield return Wait(0.15f);
                found = game.HasValidTarget;
            }
            yield return Wait(0.6f);
            yield return Shot("tip-aim");
            yield return Click();
            yield return Wait(1f);
            Check(game.ActiveTip != "aim", "tip: dropping it in retires the aiming tip");

            // A fragile thing, then holding it where it can't go.
            var jug = game.Items.FirstOrDefault(i => i.Def.Fragile && i.State == ItemState.Pile);
            if (jug != null)
            {
                yield return MoveMouse(jug.transform.position + (Vector3)jug.Shape.Center);
                yield return Click();
                if (game.Held != jug) game.AutoHold(jug);
                yield return WaitForTip("fragile", 2f);
                Check(game.ActiveTip == "fragile", "tip: picking up something fragile explains fragile");
                yield return Wait(0.6f);
                yield return Shot("tip-fragile");
                yield return MoveMouse(basket.transform.position + (Vector3)basket.Shape.Center + Vector3.up * 0.5f);
                yield return WaitForTip("turn", 10f);
                Check(game.ActiveTip == "turn", "tip: hovering where it won't fit suggests turning it");
                yield return Wait(0.6f);
                yield return Shot("tip-turn");
                yield return Press(Key.R);
                yield return Wait(0.6f);
                Check(game.ActiveTip != "turn", "tip: turning it retires the turning tip");
                yield return Press(Key.Escape);
                yield return Wait(0.4f);
            }
            else Check(false, "tip: the wagon has a fragile item");

            // A few placements in, mention undo.
            foreach (var (itemId, cells) in solutions["wagon"])
            {
                var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                if (item != null) game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                yield return Wait(0.05f);
            }
            yield return WaitForTip("undo", 10f);
            Check(game.ActiveTip == "undo", "tip: after a few drops it mentions undo");
            yield return Wait(0.6f);
            yield return Shot("tip-undo");

            // The Garage Sale pickup: the third thing has a shelf over a gap (as in the trailer).
            var garage = solutions["garage"];
            game.AutoStartLevel(LevelIndex("garage"));
            yield return Wait(2.5f);
            for (int i = 0; i < 2; i++)
            {
                var (itemId, cells) = garage[i];
                var item = game.Items.First(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
            }
            yield return Wait(0.6f);
            {
                var (itemId, cells) = garage[2];
                var item = game.Items.First(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                game.AutoHold(item);
                item.SetOrientation(FindOrientation(item.Def.Shape, cells));
                var min = cells.Aggregate(Vector3Int.Min);
                var heights = game.HeldRestingHeights(min.x, min.z);
                Check(heights.Count >= 2, "tip: the garage's third item has a shelf to choose");
                // Sweep the trunk at a few heights until the aim lands on a column with a shelf.
                var size = game.TrunkSize;
                for (int y = size.y; y >= 0 && game.ActiveTip != "shelf"; y--)
                for (int x = 0; x < size.x && game.ActiveTip != "shelf"; x++)
                for (int z = 0; z < size.z && game.ActiveTip != "shelf"; z++)
                {
                    yield return MoveMouse(trunk().TransformPoint(new Vector3(x + 0.5f, y, z + 0.5f)));
                    yield return Wait(0.12f);
                }
                if (game.ActiveTip != "shelf") yield return WaitForTip("shelf", 1f);
                Check(game.ActiveTip == "shelf", "tip: a column with a gap underneath explains shelves");
                yield return Wait(0.6f);
                yield return Shot("tip-shelf");
                yield return Press(Key.W);
                yield return Wait(0.6f);
                Check(game.ActiveTip != "shelf", "tip: W retires the shelf tip");
                yield return Press(Key.Escape);
            }
            yield return WaitForTip("orbit", 20f);
            Check(game.ActiveTip == "orbit", "tip: when nothing else is pending, it mentions the camera");
            yield return Wait(0.6f);
            yield return Shot("tip-orbit");

            // Seen once, never again: a restart and a new trip show nothing.
            game.AutoStartLevel(LevelIndex("garage"));
            yield return Wait(3f);
            game.AutoStartLevel(1);
            yield return Wait(3f);
            Check(game.ActiveTip == null, "tip: nothing repeats after a restart or on the next trip");
            var counts = game.TipCounts;
            var expected = new[] { "pickup", "aim", "turn", "shelf", "fragile", "undo", "orbit" };
            Check(expected.All(t => counts.TryGetValue(t, out var n) && n == 1) && counts.Count == expected.Length,
                "tip: each of the seven tips showed exactly once (" + string.Join(", ", counts.Select(kv => kv.Key + " " + kv.Value)) + ")");
        }

        /// <summary>
        /// Grandpa's hints: press H for real (ghost + toast), check the hinted item picks up turned the
        /// right way, then pack trips using nothing but hints, from empty and from a deliberately wrong start.
        /// </summary>
        IEnumerator HintChecks()
        {
            PerfProbe.Begin("playing");
            game.AutoStartLevel(LevelIndex("weekend"));
            yield return Wait(2.5f);
            Check(Visible("Hint"), "hint: the HINT button is there");
            yield return Press(Key.H);
            yield return Wait(0.8f);
            var hinted = game.HintItem;
            Check(hinted != null && hinted.State == ItemState.Pile, "hint: H points at something on the blanket");
            yield return Shot("hint-ghost");
            if (hinted != null)
            {
                var expected = game.AutoFindHint();
                yield return MoveMouse(hinted.transform.position + (Vector3)hinted.Shape.Center);
                yield return Click();
                yield return Wait(0.3f);
                Check(game.Held == hinted && Quaternion.Angle(hinted.Orientation, expected.Rotation) < 1f,
                    "hint: picking up the hinted item turns it like the ghost");
                yield return Press(Key.Escape);
                yield return Wait(0.3f);
            }

            // Hints alone, from an empty trunk, for every trip in this run.
            for (int level = 0; level < GameDatabase.Levels.Count; level++)
            {
                if (quick && level != 0 && level != 5 && level != GameDatabase.Levels.Count - 1 && GameDatabase.Levels[level].Id != "weekend") continue;
                game.AutoStartLevel(level);
                yield return Wait(0.3f);
                yield return FollowHints();
                var id = GameDatabase.Levels[level].Id;
                Check(game.Items.All(i => i.State == ItemState.Packed || i.State == ItemState.Dropping),
                    $"hint: following only hints packs {id} 100% ({followSteps} hints{(followStuck != null ? ", stuck: " + followStuck : "")})");
                yield return Wait(0.2f);
            }

            // A wrong start: the big suitcase lying across the front of the sedan, where no solution has it.
            game.AutoStartLevel(LevelIndex("weekend"));
            yield return Wait(0.5f);
            var suitcase = game.Items.First(i => i.Def.Id == "suitcase_big");
            bool misplaced = false;
            foreach (var (rotation, shape) in suitcase.Def.Shape.Orientations())
            {
                for (int x = 0; x + shape.Size.x <= game.TrunkSize.x && !misplaced; x++)
                for (int z = 0; z + shape.Size.z <= game.TrunkSize.z && !misplaced; z++)
                {
                    if (!game.AutoPlace(suitcase, rotation, new Vector3Int(x, 0, z))) continue;
                    var h = game.AutoFindHint();
                    if (h.Move || h.Item == null) misplaced = true;
                    else { game.AutoPutBackToPile(suitcase); }
                }
                if (misplaced) break;
            }
            Check(misplaced, "hint: a suitcase in a spot no solution uses gets a 'move it' or 'undo' hint");
            yield return Wait(0.6f);
            yield return Press(Key.H);
            yield return Wait(0.8f);
            yield return Shot("hint-move");
            yield return FollowHints();
            Check(game.Items.All(i => i.State == ItemState.Packed || i.State == ItemState.Dropping),
                $"hint: from the wrong start, hints still lead to 100% ({followSteps} hints{(followStuck != null ? ", stuck: " + followStuck : "")})");
        }

        /// <summary>Pack a trip completely from the solver's solution (no hints).</summary>
        IEnumerator PackAll(string id, List<(string, List<Vector3Int>)> placements)
        {
            foreach (var (itemId, cells) in placements)
            {
                var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                if (item != null) game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                yield return Wait(0.03f);
            }
            yield return Wait(0.8f);
        }

        /// <summary>
        /// The trip card's "best so far" on Grocery Run (8 essentials, 4 extras): closed with an extra left out,
        /// starting it again names the stars and what stayed on the curb; a worse replay doesn't change that;
        /// three stars after a hint asks for the seal instead. Grocery Run's own results are put back afterwards.
        /// </summary>
        IEnumerator BestSoFarChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            const string id = "groceries";
            if (!solutions.TryGetValue(id, out var placements)) { Check(false, "best so far: groceries has a solution"); yield break; }
            int index = LevelIndex(id);
            string[] keys = { "ptt.stars." + id, "ptt.seal." + id, "ptt.bestleft." + id };
            var saved = keys.Select(k => (k, has: Prefs.HasKey(k), value: Prefs.GetString(k), number: Prefs.GetInt(k))).ToList();
            foreach (var k in keys) Prefs.DeleteKey(k);

            IEnumerator Play(int skipExtras)
            {
                game.AutoStartLevel(index);
                yield return Wait(1.5f);
                // Leave out only extras that nothing else rests on, so everything else still packs.
                var taken = new HashSet<Vector3Int>(placements.SelectMany(p => p.Item2));
                int skipped = 0;
                foreach (var (itemId, cells) in placements)
                {
                    var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                    if (item == null) continue;
                    bool bare = cells.All(c => cells.Contains(c + Vector3Int.up) || !taken.Contains(c + Vector3Int.up));
                    if (item.IsBonus && bare && skipped < skipExtras) { skipped++; continue; }
                    game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                    yield return Wait(0.03f);
                }
                yield return Wait(0.8f);
                yield return Press(Key.Space);
                yield return Wait(0.4f);
                if (!game.IsShowingResults) yield return Press(Key.Space);
                for (float t = 0f; t < 9f && !game.IsShowingResults; t += Time.unscaledDeltaTime) yield return null;
                yield return Wait(1.2f);
            }

            game.AutoBeginTrip(index);
            yield return Wait(1f);
            Check(game.Ui.TripCardBestSoFar == null, "best so far: a trip never closed has no best-so-far line");

            yield return Play(1);
            bool closed = game.IsShowingResults;
            int stars = game.LastStars;
            var curb = game.Items.Where(i => i.State != ItemState.Packed).Select(i => i.Def.Name).Distinct().ToList();
            int curbCount = game.Items.Count(i => i.State != ItemState.Packed);
            game.AutoBeginTrip(index);
            yield return Wait(2.5f);
            string line = game.Ui.TripCardBestSoFar ?? "";
            Check(closed && stars < 3 && curb.Count > 0 && line.Contains($"{stars} star") && line.Contains(curb[0]) && game.Ui.TripDetailsFit,
                $"best so far: after closing with {curb.Count} left out, the trip card reads \"{line}\" (fits: {game.Ui.TripDetailsFit})");
            yield return Shot("best-so-far-card");

            yield return Play(4);
            bool closedAgain = game.IsShowingResults;
            int curbAgain = game.Items.Count(i => i.State != ItemState.Packed);
            game.AutoBeginTrip(index);
            yield return Wait(1f);
            Check(closedAgain && game.LastStars <= stars && curbAgain > curbCount && game.Ui.TripCardBestSoFar == line,
                $"best so far: a worse replay ({game.LastStars} stars, more left out) leaves the line as it was");

            game.AutoStartLevel(index);
            yield return Wait(1.5f);
            game.AutoAskGrandpa();
            yield return Wait(0.3f);
            game.AutoClearHint();
            yield return PackAll(id, placements);
            yield return CloseAndWait();
            game.AutoBeginTrip(index);
            yield return Wait(1f);
            string seal = game.Ui.TripCardBestSoFar ?? "";
            Check(game.LastStars == 3 && seal.Contains("seal"), $"best so far: three stars after a hint asks for the seal (\"{seal}\")");
            Prefs.SetInt("ptt.seal." + id, 1);
            game.AutoBeginTrip(index);
            yield return Wait(1f);
            Check(game.Ui.TripCardBestSoFar == null, "best so far: with three stars and the seal there's no extra line");

            foreach (var k in keys) Prefs.DeleteKey(k);
            foreach (var (k, has, value, number) in saved)
            {
                if (!has) continue;
                if (k.Contains("bestleft")) Prefs.SetString(k, value);
                else Prefs.SetInt(k, number);
            }
            game.AutoShowMainMenu();
            yield return Wait(1f);
        }

        /// <summary>The album deals out a polaroid a quarter of a second at a time; wait until it's done (at least as long as before).</summary>
        IEnumerator WaitForAlbum(float atLeast = 11f)
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < 25f && !(game.Ui.AlbumRevealed && Time.unscaledTime - start >= atLeast)) yield return null;
            yield return Wait(0.3f);
        }

        static string LoadAverage()
        {
            try { return File.ReadAllText("/proc/loadavg").Split(' ')[0]; }
            catch { return "?"; }
        }

        IEnumerator CloseAndWait()
        {
            yield return Press(Key.Space);
            for (float t = 0f; t < 9f && !game.IsShowingResults; t += Time.unscaledDeltaTime) yield return null;
            yield return Wait(1.2f);
        }

        /// <summary>
        /// Grandpa's seal on Weekend Getaway: three stars after a hint gets no seal (and says how to earn
        /// one); RESTART starts a fresh attempt, undoing it brings the hint back; a hint-free three stars
        /// earns it; a later hinted replay keeps it; the map and the album show it.
        /// </summary>
        IEnumerator SealChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            const string id = "weekend";
            string key = "ptt.seal." + id;
            var placements = solutions[id];
            Prefs.DeleteKey(key);
            PerfProbe.Begin("packing");
            game.AutoStartLevel(LevelIndex(id));
            yield return Wait(2.5f);
            yield return Press(Key.H);
            yield return Wait(0.5f);
            Check(game.HintedThisTry, "seal: asking Grandpa marks this attempt as hinted");
            yield return PackAll(id, placements);
            yield return CloseAndWait();
            yield return Wait(2f);
            Check(game.LastStars == 3 && !game.Ui.ResultsShowSeal && Prefs.GetInt(key) == 0 && AnyText("earns Grandpa's seal"),
                "seal: three stars after a hint, no seal, and the postcard says how to earn one");
            yield return Shot("seal-nudge");

            // TRY AGAIN is a fresh attempt; RESTART is too, and undoing it brings the hint back.
            yield return Press(Key.R);
            yield return Wait(2.5f);
            Check(game.IsPlaying && !game.HintedThisTry, "seal: TRY AGAIN starts a fresh attempt");
            yield return Press(Key.H);
            yield return Wait(0.5f);
            yield return PackAll(id, placements.Take(2).ToList());
            yield return ClickUi("Restart");
            yield return Wait(0.6f);
            bool cleared = !game.HintedThisTry;
            yield return Press(Key.Z);
            yield return Wait(0.6f);
            bool back = game.HintedThisTry;
            yield return ClickUi("Restart");
            yield return Wait(0.6f);
            Check(cleared && back && !game.HintedThisTry, $"seal: RESTART clears the hint ({cleared}), undoing it brings it back ({back}), RESTART clears it again");
            yield return PackAll(id, placements);
            yield return CloseAndWait();
            yield return Wait(3f);
            Check(game.LastStars == 3 && game.Ui.ResultsShowSeal && Prefs.GetInt(key) == 1, "seal: three stars without a hint earns Grandpa's seal");
            yield return Shot("seal-postcard");

            // A hinted replay doesn't take it away (and doesn't nag).
            game.AutoStartLevel(LevelIndex(id));
            yield return Wait(1.2f);
            yield return Press(Key.H);
            yield return Wait(0.3f);
            yield return PackAll(id, placements);
            yield return CloseAndWait();
            Check(Prefs.GetInt(key) == 1 && !game.Ui.ResultsShowSeal && !AnyText("earns Grandpa's seal"), "seal: a hinted replay keeps the seal, without the how-to note");

            game.AutoShowMenu();
            yield return Wait(2f);
            Check(FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude).Any(r => r.name.StartsWith("Seal ") && r.name.Length > 5 && char.IsDigit(r.name[5])) && AnyText("Grandpa's seal on"), "seal: the Trip Map shows the seals and how many there are");
            yield return Shot("seal-map");
            game.AutoShowMenuAlbum();
            yield return WaitForAlbum();
            int polaroidSeals = FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude).Count(r => r.name == "Seal" && r.parent != null && r.parent.parent != null && r.parent.parent.name.StartsWith("Polaroid"));
            int sealedCount = GameDatabase.Levels.Count(l => Prefs.GetInt("ptt.seal." + l.Id) == 1);
            Check(polaroidSeals == sealedCount && AnyText($"Grandpa's seal on {sealedCount} of"), $"seal: the album puts a seal on each of the {sealedCount} sealed polaroids ({polaroidSeals}) and counts them");
            yield return Shot("seal-album");
        }

        /// <summary>
        /// The album's close-up: a polaroid with a photo opens it big, the arrows and A / D flip to the
        /// neighbouring photos, Escape and a click outside close it (the album stays open), and a polaroid
        /// without a photo doesn't open.
        /// </summary>
        /// <summary>
        /// Album photos: a freshly closed trunk is saved as a 960×720 JPEG plus a 320×240 thumbnail; the polaroid
        /// uses the thumbnail (with mipmaps) and the close-up the full photo; an old 480×360 PNG from before round 7
        /// still loads for both. Decode times are logged.
        /// </summary>
        IEnumerator PhotoChecks()
        {
            const string id = "wagon";
            yield return Wait(1f);
            string path = GameController.PhotoPath(id), thumbPath = GameController.ThumbPath(id);
            var bytes = path != null ? File.ReadAllBytes(path) : new byte[0];
            var thumbBytes = thumbPath != null ? File.ReadAllBytes(thumbPath) : new byte[0];
            var probe = new Texture2D(2, 2);
            var thumbProbe = new Texture2D(2, 2);
            bool decoded = bytes.Length > 0 && probe.LoadImage(bytes) && thumbBytes.Length > 0 && thumbProbe.LoadImage(thumbBytes);
            Check(path != null && path.EndsWith(".jpg") && thumbPath.EndsWith(".thumb.jpg") && decoded &&
                  probe.width == GameController.PhotoWidth && probe.height == GameController.PhotoHeight && thumbProbe.width == GameController.ThumbWidth,
                $"photo: the wagon's trunk is saved as {Path.GetFileName(path ?? "nothing")} {probe.width}x{probe.height} ({bytes.Length / 1024} KB) " +
                $"and {Path.GetFileName(thumbPath ?? "nothing")} {thumbProbe.width}x{thumbProbe.height} ({thumbBytes.Length / 1024} KB)");
            Destroy(thumbProbe);

            game.AutoForgetPhoto(id);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var thumb = game.AutoPhoto(id);
            double thumbMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var full = game.AutoFullPhoto(id);
            double fullMs = watch.Elapsed.TotalMilliseconds;
            Check(thumb != null && thumb.width == GameController.ThumbWidth && thumb.mipmapCount > 1 && full != null && full.width == GameController.PhotoWidth,
                $"photo: read back, the polaroid's copy is {thumb?.width}x{thumb?.height} with {thumb?.mipmapCount} mip levels ({thumbMs:0.0} ms) " +
                $"and the close-up's {full?.width}x{full?.height} ({fullMs:0.0} ms), load {LoadAverage()}");

            // An album from before round 7: a 480x360 PNG (made from the wagon's photo) still shows.
            var legacyId = GameDatabase.Levels[GameDatabase.Levels.Count - 1].Id;
            var small = RenderTexture.GetTemporary(480, 360, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(probe, small);
            var prev = RenderTexture.active;
            RenderTexture.active = small;
            var png = new Texture2D(480, 360, TextureFormat.RGB24, false);
            png.ReadPixels(new Rect(0, 0, 480, 360), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(small);
            foreach (var stale in new[] { legacyId + ".jpg", legacyId + ".thumb.jpg" })
                if (File.Exists(Path.Combine(Prefs.AlbumDir, stale))) File.Delete(Path.Combine(Prefs.AlbumDir, stale));
            Directory.CreateDirectory(Prefs.AlbumDir);
            File.WriteAllBytes(Path.Combine(Prefs.AlbumDir, legacyId + ".png"), png.EncodeToPNG());
            Destroy(png);
            Destroy(probe);
            game.AutoForgetPhoto(legacyId);
            var old = game.AutoPhoto(legacyId);
            var oldFull = game.AutoFullPhoto(legacyId);
            Check(old != null && old.width == 480 && oldFull != null && oldFull.width == 480 && GameController.PhotoPath(legacyId).EndsWith(".png"),
                $"photo: an old 480x360 PNG ({legacyId}) still loads for the polaroid and the close-up ({old?.width}x{old?.height})");
        }

        IEnumerator AlbumChecks()
        {
            if (!game.Ui.IsAlbumOpen)
            {
                game.AutoShowMenuAlbum();
                yield return WaitForAlbum();
            }
            var withPhoto = GameDatabase.Levels.Where(l => FindButton("Polaroid " + l.Id) != null).ToList();
            if (withPhoto.Count < 2) { Check(false, $"album: at least two polaroids have photos ({withPhoto.Count})"); yield break; }
            var first = withPhoto[0];
            var polaroid = (RectTransform)FindButton("Polaroid " + first.Id).transform;
            var corners = new Vector3[4];
            polaroid.GetWorldCorners(corners);
            float polaroidWidth = Vector3.Distance(corners[0], corners[3]);
            yield return ClickUi("Polaroid " + first.Id);
            yield return Wait(0.6f);
            Check(game.Ui.AlbumZoomTrip == first.Id && game.Ui.AlbumZoomPhotoWidth >= 3f * polaroidWidth && AnyText(first.Title) && AnyText($"photo 1 of {withPhoto.Count}"),
                $"album: clicking {first.Title}'s polaroid opens its photo {game.Ui.AlbumZoomPhotoWidth / polaroidWidth:0.0}x as wide as the polaroid, with its title and count");
            Check(game.Ui.AlbumZoomTextureSize.x == GameController.PhotoWidth, $"album: the close-up shows the full {game.Ui.AlbumZoomTextureSize.x}x{game.Ui.AlbumZoomTextureSize.y} photo, not the polaroid's copy");
            yield return Shot("album-zoom");
            yield return Press(Key.RightArrow);
            yield return Wait(0.3f);
            bool nextOk = game.Ui.AlbumZoomTrip == withPhoto[1].Id;
            yield return Press(Key.A);
            yield return Wait(0.3f);
            bool backOk = game.Ui.AlbumZoomTrip == first.Id;
            yield return Press(Key.LeftArrow);
            yield return Wait(0.3f);
            Check(nextOk && backOk && game.Ui.AlbumZoomTrip == withPhoto[withPhoto.Count - 1].Id,
                $"album: right goes to the next photo ({withPhoto[1].Title}), A back, and left from the first wraps to the last");
            yield return Press(Key.Escape);
            yield return Wait(0.5f);
            Check(!game.Ui.IsAlbumZoomOpen && game.Ui.IsAlbumOpen, "album: Escape closes the close-up and the album stays open");
            yield return ClickUi("Polaroid " + first.Id);
            yield return Wait(0.5f);
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(12, 12) });
            yield return null;
            yield return Click();
            yield return Wait(0.5f);
            Check(!game.Ui.IsAlbumZoomOpen && game.Ui.IsAlbumOpen, "album: a click outside the photo closes the close-up");
            var bare = FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude).FirstOrDefault(r => r.name == "Polaroid" && r.parent != null && r.parent.name == "Slot");
            if (bare != null)
            {
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = (Vector2)bare.TransformPoint(bare.rect.center) });
                yield return null;
                yield return Click();
                yield return Wait(0.5f);
                Check(!game.Ui.IsAlbumZoomOpen, "album: a polaroid without a photo doesn't open");
            }
            yield return Press(Key.Escape);
            yield return Wait(1f);
            Check(!game.Ui.IsAlbumOpen, "album: with the close-up shut, Escape leaves the album as before");
        }

        readonly List<string> meterMismatches = new List<string>();
        int tripsClosed;

        /// <summary>
        /// The star meter on Grandma's Big Move (11 essentials, 5 extras, so "half" rounds up to 3): empty
        /// until the essentials are in, then it follows the extras, and the drop that earns a star says so.
        /// </summary>
        IEnumerator StarMeterChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            PerfProbe.Begin("playing");
            game.AutoStartLevel(LevelIndex("grandma"));
            yield return Wait(2.5f);
            Check(game.Ui.MeterStars == 0, "stars: the meter starts empty");
            var placed = new List<(PackItem Item, Quaternion Rot, Vector3Int Pos)>();
            foreach (var (itemId, cells) in solutions["grandma"])
            {
                var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                if (item == null) continue;
                var rot = FindOrientation(item.Def.Shape, cells);
                var min = cells.Aggregate(Vector3Int.Min);
                if (game.AutoPlace(item, rot, min)) placed.Add((item, rot, min));
                yield return Wait(0.03f);
            }
            yield return Wait(0.9f);
            Check(game.Ui.MeterStars == 3 && AnyText("Three stars"), "stars: packing everything fills the meter and says three stars");

            var extras = placed.Where(p => p.Item.IsBonus).ToList();
            var readings = new List<int>();
            foreach (var p in Enumerable.Reverse(extras))
            {
                game.AutoPutBackToPile(p.Item);
                readings.Add(game.Ui.MeterStars);
            }
            yield return Wait(0.6f);
            string read = string.Join(",", readings);
            Check(extras.Count == 5 && read == "2,2,1,1,1", $"stars: taking the 5 extras out one by one reads {read} (3 of 5 still makes two)");

            for (int k = 0; k < 3; k++)
            {
                game.AutoPlace(extras[k].Item, extras[k].Rot, extras[k].Pos);
                yield return Wait(k < 2 ? 0.4f : 0.9f);
            }
            Check(game.Ui.MeterStars == 2 && AnyText("Two stars if you close now"), "stars: the third extra back in earns two stars, and the toast says what makes three");
            yield return Shot("star-meter-two");

            var essential = placed.Last(p => !p.Item.IsBonus).Item;
            game.AutoPutBackToPile(essential);
            yield return Wait(0.3f);
            Check(game.Ui.MeterStars == 0, $"stars: taking out an essential ({essential.Def.Name}) empties the meter");
        }

        /// <summary>
        /// RESTART (the HUD button and the pause menu's) unpacks in place as one undo step: everything goes
        /// back on the blanket, and Z puts every item back in the same cell, turned the same way.
        /// </summary>
        IEnumerator RestartChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            PerfProbe.Begin("playing");
            int index = LevelIndex("grandma");
            game.AutoStartLevel(index);
            yield return Wait(2.5f);
            int emptyFree = game.FreeCells;
            int undoBefore = game.UndoDepth;
            yield return ClickUi("Restart");
            yield return Wait(0.5f);
            Check(game.IsPlaying && game.UndoDepth == undoBefore && AnyText("already empty"),
                "restart: with nothing packed, RESTART just says so (no undo step)");

            if (!solutions.TryGetValue("grandma", out var placements)) { Check(false, "restart: grandma has a solution"); yield break; }
            foreach (var (itemId, cells) in placements.Take(placements.Count / 2 + 1))
            {
                var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                if (item != null) game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                yield return Wait(0.03f);
            }
            yield return Wait(0.8f);
            var packedNow = game.Items.Where(i => i.State == ItemState.Packed).ToList();
            var layout = packedNow.ToDictionary(i => i, i => (i.GridPos, i.Orientation));
            int packedFree = game.FreeCells;

            foreach (var via in new[] { "HUD", "pause" })
            {
                if (via == "HUD") yield return ClickUi("Restart");
                else
                {
                    yield return Press(Key.Escape);
                    yield return Wait(1f);
                    yield return ClickUi("Pause Restart");
                }
                yield return Wait(0.8f);
                if (via == "HUD") yield return Shot("restart-unpacked");
                Check(game.IsPlaying && !game.IsPaused && game.Items.All(i => i.State == ItemState.Pile) && game.FreeCells == emptyFree,
                    $"restart ({via}): {packedNow.Count} packed items all go back on the blanket and the trunk is empty");
                yield return Press(Key.Z);
                yield return Wait(0.8f);
                bool same = packedNow.All(i => i.State == ItemState.Packed && i.GridPos == layout[i].GridPos && Quaternion.Angle(i.Orientation, layout[i].Orientation) < 1f);
                Check(same && game.FreeCells == packedFree && game.Items.Count(i => i.State == ItemState.Packed) == packedNow.Count,
                    $"restart ({via}): one Z puts all {packedNow.Count} back exactly where they were");
            }
        }

        /// <summary>
        /// Tools/resume_test.sh, on a sandboxed save file (-pttPrefsFile). "pack": half-pack Grandma's Big
        /// Move after a hint, write down the layout and wait to be killed (SIGKILL, like a crash). "check":
        /// in a new player on the same save, start the trip and compare.
        /// </summary>
        IEnumerator ResumeCrashTest(string phase)
        {
            int index = LevelIndex("grandma");
            var expected = Path.Combine(outDir, "resume-expected.txt");
            yield return Wait(3f);
            if (phase == "pack")
            {
                game.AutoStartLevel(index);
                yield return Wait(2.5f);
                game.AutoAskGrandpa();
                yield return Wait(0.3f);
                var placements = LoadSolutions()["grandma"];
                int lastPlaced = -1;
                foreach (var (itemId, cells) in placements.Take(placements.Count / 2 + 1))
                {
                    var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                    if (item != null && game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min)))
                        lastPlaced = game.Items.ToList().IndexOf(item);
                    yield return Wait(0.03f);
                }
                yield return Wait(0.8f);
                File.WriteAllText(Path.Combine(outDir, "resume-undo.txt"), $"{game.UndoDepth} {lastPlaced}");
                var lines = new List<string>();
                for (int i = 0; i < game.Items.Count; i++)
                {
                    var it = game.Items[i];
                    if (it.State != ItemState.Packed) continue;
                    var e = it.Orientation.eulerAngles;
                    lines.Add($"{i} {it.GridPos.x} {it.GridPos.y} {it.GridPos.z} {e.x:0} {e.y:0} {e.z:0}");
                }
                File.WriteAllLines(expected, lines);
                Log($"resume-crash: packed {lines.Count} (hinted {game.HintedThisTry}), ready to be killed");
                yield return Wait(600f);
                yield break;
            }

            var want = File.Exists(expected) ? File.ReadAllLines(expected) : new string[0];
            Check(want.Length > 0, $"resume-crash: the first run left a layout to compare ({want.Length} items)");
            game.AutoTransitionTrip(index);
            yield return Wait(2.5f);
            yield return StoryToPacking();
            yield return Wait(1.5f);
            int matched = 0;
            foreach (var line in want)
            {
                var f = line.Split(' ').Select(int.Parse).ToArray();
                var it = game.Items[f[0]];
                if (it.State == ItemState.Packed && it.GridPos == new Vector3Int(f[1], f[2], f[3])
                    && Quaternion.Angle(it.Orientation, Quaternion.Euler(f[4], f[5], f[6])) < 1f) matched++;
            }
            Check(game.IsPlaying && matched == want.Length && game.Items.Count(i => i.State == ItemState.Packed) == want.Length,
                $"resume-crash: after a SIGKILL, starting the trip again puts back {matched}/{want.Length} in the same cells and orientations");
            Check(game.HintedThisTry, "resume-crash: the hint mark survives the crash too");
            yield return Shot("resume-after-crash");
            var undoFile = Path.Combine(outDir, "resume-undo.txt");
            var u = File.Exists(undoFile) ? File.ReadAllText(undoFile).Split(' ').Select(int.Parse).ToArray() : new[] { -1, -1 };
            int depth = game.UndoDepth;
            yield return Press(Key.Z);
            yield return Wait(0.8f);
            Check(u[0] > 0 && depth == u[0] && u[1] >= 0 && game.Items[u[1]].State == ItemState.Pile
                && game.Items.Count(i => i.State == ItemState.Packed) == want.Length - 1,
                $"resume-crash: the undo history survives too ({depth}/{u[0]} steps), and one Z takes out the last thing packed");
        }

        /// <summary>A screen point over the trunk where the held item would land validly (or not), and the cell it would land in.</summary>
        bool FindAim(bool wantValid, out Vector2 screen, out Vector3Int pos)
        {
            var size = game.TrunkSize;
            var trunk = game.CurrentVehicle.transform;
            for (int y = 0; y <= size.y; y++)
            for (int z = 0; z < size.z; z++)
            for (int x = 0; x < size.x; x++)
            {
                var s = game.Camera.WorldToScreenPoint(trunk.TransformPoint(new Vector3(x + 0.5f, y, z + 0.5f)));
                screen = new Vector2(s.x, s.y);
                if (game.PreviewTarget(screen, out pos, out bool valid) && valid == wantValid) return true;
            }
            screen = default;
            pos = default;
            return false;
        }

        IEnumerator MouseDown(Vector3 world)
        {
            var s = game.Camera.WorldToScreenPoint(world);
            var p = new Vector2(s.x, s.y);
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = p });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = p }.WithButton(MouseButton.Left, true));
            yield return null;
            yield return null;
        }

        /// <summary>Move the mouse with the left button held, a little each frame, like a hand would.</summary>
        IEnumerator DragTo(Vector2 to, bool release)
        {
            var from = Mouse.current.position.ReadValue();
            for (int i = 1; i <= 15; i++)
            {
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = Vector2.Lerp(from, to, i / 15f) }.WithButton(MouseButton.Left, true));
                yield return null;
            }
            yield return Wait(0.25f);
            if (!release) yield break;
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = to });
            yield return null;
            yield return null;
        }

        /// <summary>
        /// Drag to pack, with real mouse events on Grocery Run: press on something, drag it over the trunk and
        /// let go to drop it at the ghost. A plain click still only picks up. Letting go where it won't fit
        /// keeps it in hand and says why; letting go off the trunk puts it back. Things in the trunk drag too.
        /// </summary>
        IEnumerator DragChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            PerfProbe.Begin("playing");
            game.AutoStartLevel(LevelIndex("groceries"));
            yield return Wait(3f);
            // A fragile thing in the trunk gives a spot where nothing else may go.
            if (!solutions.TryGetValue("groceries", out var placements)) { Check(false, "drag: groceries has a solution"); yield break; }
            foreach (var (itemId, cells) in placements)
            {
                var f = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.Def.Fragile && it.State == ItemState.Pile);
                if (f == null) continue;
                game.AutoPlace(f, FindOrientation(f.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                break;
            }
            yield return Wait(0.8f);
            var pile = game.Items.Where(i => i.State == ItemState.Pile && !i.Def.Fragile).OrderBy(i => i.Def.Volume).ToList();
            var a = pile[0];
            var b = pile[1];
            Vector3 Centre(PackItem i) => i.transform.position + (Vector3)i.Shape.Center;

            // 1. Press on the blanket, drag into the trunk, let go.
            yield return MouseDown(Centre(a));
            Check(game.Held == a, "drag: pressing on something picks it up");
            if (!FindAim(true, out var good, out _)) { Check(false, "drag: found a valid spot to aim at"); yield break; }
            yield return DragTo(good, false);
            game.CurrentTarget(out var ghostAt, out bool ghostOk);
            yield return Shot("drag-holding");
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = good });
            yield return null;
            yield return null;
            yield return Wait(0.6f);
            Check(ghostOk && game.Held == null && a.State == ItemState.Packed && a.GridPos == ghostAt,
                $"drag: letting go over the trunk drops it where the ghost was ({ghostAt})");

            // 2. A plain click (press and let go in place) still just picks up.
            yield return MoveMouse(Centre(b));
            yield return Click();
            yield return Wait(0.4f);
            Check(game.Held == b, "drag: a plain click still only picks up (letting go doesn't drop)");
            yield return Press(Key.Escape);
            yield return Wait(0.6f);

            // 3. Let go where it won't fit: it stays in hand and the game says why.
            yield return MouseDown(Centre(b));
            if (!FindAim(false, out var bad, out _)) { Check(false, "drag: found a blocked spot to aim at"); yield break; }
            game.PreviewTarget(bad, out _, out _, out string problem);
            yield return DragTo(bad, true);
            yield return Wait(0.2f);
            Check(game.Held == b && b.State == ItemState.Held && problem != null && game.Ui.ToastShowing(problem.Substring(0, Math.Min(14, problem.Length))),
                "drag: letting go where it won't fit keeps it in hand and says why (" + problem + ")");
            yield return Press(Key.Escape);
            yield return Wait(0.6f);

            // 4. Drag off the trunk onto the driveway and let go: back on the blanket.
            yield return MouseDown(Centre(b));
            var off = game.Camera.WorldToScreenPoint(game.CurrentVehicle.transform.TransformPoint(new Vector3(-5f, 0f, -4f)));
            yield return DragTo(new Vector2(off.x, off.y), true);
            yield return Wait(0.6f);
            Check(game.Held == null && b.State == ItemState.Pile, "drag: letting go off the trunk puts it back on the blanket");

            // 4b. Drag onto the HUD (the packing list) and let go: back on the blanket too.
            var list = FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude).FirstOrDefault(r => r.name == "Packing List");
            if (list == null) { Check(false, "drag: the packing list is on screen"); yield break; }
            var corners = new Vector3[4];
            list.GetWorldCorners(corners);
            var overList = (Vector2)(corners[0] + corners[2]) * 0.5f;
            yield return MouseDown(Centre(b));
            bool heldOverList = game.Held == b;
            yield return DragTo(overList, false);
            heldOverList &= game.Held == b;
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = overList });
            yield return null;
            yield return null;
            yield return Wait(0.6f);
            Check(heldOverList && game.Held == null && b.State == ItemState.Pile, "drag: letting go over the HUD (the packing list) puts it back on the blanket");

            // 5. Things already in the trunk drag too.
            var from = a.GridPos;
            yield return MouseDown(Centre(a));
            Check(game.Held == a, "drag: pressing on something packed lifts it out");
            Vector2 moveTo = default;
            Vector3Int moveCell = default;
            bool foundMove = false;
            var size = game.TrunkSize;
            var trunk = game.CurrentVehicle.transform;
            for (int z = size.z - 1; z >= 0 && !foundMove; z--)
            for (int x = size.x - 1; x >= 0 && !foundMove; x--)
            {
                var s = game.Camera.WorldToScreenPoint(trunk.TransformPoint(new Vector3(x + 0.5f, 0f, z + 0.5f)));
                if (game.PreviewTarget(new Vector2(s.x, s.y), out moveCell, out bool v) && v && moveCell != from)
                {
                    moveTo = new Vector2(s.x, s.y);
                    foundMove = true;
                }
            }
            if (!foundMove) { Check(false, "drag: found another spot for the packed item"); yield break; }
            yield return DragTo(moveTo, false);
            game.CurrentTarget(out var moveGhost, out _);
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = moveTo });
            yield return null;
            yield return null;
            yield return Wait(0.6f);
            Check(a.State == ItemState.Packed && a.GridPos == moveGhost && a.GridPos != from, $"drag: a packed thing drags to a new spot ({from} -> {a.GridPos})");
        }

        /// <summary>Hold Shift, then press the key with it.</summary>
        IEnumerator PressShift(Key key)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.LeftShift));
            yield return null;
            yield return null;
            yield return Press(key, Key.LeftShift);
        }

        /// <summary>
        /// Redo (Shift + undo): three drops, three Z and three Shift+Z give back the same trunk; anything new
        /// clears what could be redone; redoing a RESTART empties the trunk again and starts a fresh attempt
        /// for the seal, as RESTART does.
        /// </summary>
        IEnumerator RedoChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            PerfProbe.Begin("playing");
            game.AutoStartLevel(LevelIndex("grandma"));
            yield return Wait(2.5f);
            if (!solutions.TryGetValue("grandma", out var placements)) { Check(false, "redo: grandma has a solution"); yield break; }
            void Pack(int count)
            {
                foreach (var (itemId, cells) in placements)
                {
                    if (count == 0) return;
                    var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                    if (item == null || game.Items.Any(it => it.State == ItemState.Packed && it.Def.Id == itemId && it.GridPos == cells.Aggregate(Vector3Int.Min))) continue;
                    if (game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min))) count--;
                }
            }
            string Layout() => string.Join(";", game.Items.Select(i => i.State == ItemState.Packed ? $"{i.GridPos}{i.Orientation.eulerAngles}" : "-"));

            Pack(3);
            yield return Wait(0.8f);
            string three = Layout();
            for (int i = 0; i < 3; i++) { yield return Press(Key.Z); yield return Wait(0.3f); }
            bool emptied = game.Items.All(i => i.State == ItemState.Pile);
            for (int i = 0; i < 3; i++) { yield return PressShift(Key.Z); yield return Wait(0.3f); }
            yield return Wait(0.5f);
            Check(emptied && Layout() == three && game.UndoDepth == 3, "redo: three Z then three Shift+Z gives back the same trunk");

            yield return Press(Key.Z);
            yield return Wait(0.3f);
            Pack(1);
            yield return Wait(0.6f);
            string afterNew = Layout();
            yield return PressShift(Key.Z);
            yield return Wait(0.3f);
            Check(Layout() == afterNew && game.Ui.ToastShowing("Nothing to redo"), "redo: a new drop clears what could be redone (\"Nothing to redo\")");

            game.AutoAskGrandpa();
            yield return Wait(0.3f);
            game.AutoClearHint();
            string beforeRestart = Layout();
            yield return ClickUi("Restart");
            yield return Wait(0.6f);
            bool restartFresh = !game.HintedThisTry;
            yield return Press(Key.Z);
            yield return Wait(0.6f);
            bool undoneHinted = game.HintedThisTry && Layout() == beforeRestart;
            yield return PressShift(Key.Z);
            yield return Wait(0.6f);
            Check(restartFresh && undoneHinted && game.Items.All(i => i.State == ItemState.Pile) && !game.HintedThisTry,
                "redo: redoing an undone RESTART empties the trunk again and is a fresh attempt for the seal");
            yield return Press(Key.Z);
            yield return Wait(0.6f);
            Check(Layout() == beforeRestart && game.HintedThisTry, "redo: and Z brings that trunk (and its hint mark) back once more");
        }

        IEnumerator StoryToPacking()
        {
            for (float t = 0f; t < 20f && !game.IsPlaying; t += 0.5f)
            {
                yield return Press(Key.Space);
                yield return Wait(0.5f);
            }
        }

        /// <summary>
        /// Your trunk waits for you: half-pack Grandma's Big Move after asking for a hint, leave through the
        /// pause menu's TRIP MAP holding something lifted out of the trunk, start the trip again, and every
        /// item is back in the same cell, turned the same way, still marked as hinted. On the last trip, the
        /// title's Continue line says the trunk is waiting and CONTINUE brings it back. Closing the trunk
        /// clears it, and a saved trunk that doesn't fit the level any more is dropped.
        /// </summary>
        IEnumerator ResumeChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            PerfProbe.Begin("playing");
            int index = LevelIndex("grandma");
            game.AutoStartLevel(index);
            yield return Wait(2.5f);
            Check(GameController.SavedTrunk("grandma") == "", "resume: a fresh trip has no saved trunk");
            game.AutoAskGrandpa();
            yield return Wait(0.3f);
            game.AutoClearHint();
            if (!solutions.TryGetValue("grandma", out var placements)) { Check(false, "resume: grandma has a solution"); yield break; }
            foreach (var (itemId, cells) in placements.Take(placements.Count / 2 + 1))
            {
                var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                if (item != null) game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                yield return Wait(0.03f);
            }
            yield return Wait(0.8f);
            var layout = new Dictionary<int, (Vector3Int, Quaternion)>();
            for (int i = 0; i < game.Items.Count; i++)
                if (game.Items[i].State == ItemState.Packed) layout[i] = (game.Items[i].GridPos, game.Items[i].Orientation);
            int free = game.FreeCells;
            bool hinted = game.HintedThisTry;
            // Leave with something lifted out of the trunk: it belongs where it was picked up from.
            var lifted = game.Items.Last(i => i.State == ItemState.Packed);
            game.AutoHold(lifted);
            yield return Wait(0.3f);
            Check(hinted && game.Held == lifted && GameController.SavedTrunkItems("grandma") == layout.Count,
                $"resume: the saved trunk has all {layout.Count} packed items (the one in hand where it came from) and the hint mark");

            game.AutoPause();
            yield return Wait(0.8f);
            yield return ClickUi("Pause Map");
            yield return Wait(2.5f);
            Check(Visible("Map Back"), "resume: the pause menu's TRIP MAP leaves the trip");
            game.AutoTransitionTrip(index);
            yield return Wait(2.5f);
            yield return StoryToPacking();
            yield return Wait(1f);
            bool same = layout.All(kv => game.Items[kv.Key].State == ItemState.Packed && game.Items[kv.Key].GridPos == kv.Value.Item1
                && Quaternion.Angle(game.Items[kv.Key].Orientation, kv.Value.Item2) < 1f);
            Check(game.IsPlaying && same && game.FreeCells == free && game.Items.Count(i => i.State == ItemState.Packed) == layout.Count,
                $"resume: starting the trip again puts all {layout.Count} back in the same cells, turned the same way");
            Check(game.HintedThisTry && game.Ui.ToastShowing("just how you left it"), "resume: the hint mark comes back too (no seal), and a toast on screen says the trunk was kept");
            yield return Shot("resume-restored");

            // The trip CONTINUE goes to, through the title: it says the trunk is waiting and brings it back.
            int last = game.NextTrip;
            string lastId = GameDatabase.Levels[last].Id;
            game.AutoStartLevel(last);
            yield return Wait(2.5f);
            if (!solutions.TryGetValue(lastId, out var finale)) { Check(false, "resume: the next trip has a solution"); yield break; }
            foreach (var (itemId, cells) in finale.Take(2))
            {
                var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                if (item != null) game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                yield return Wait(0.03f);
            }
            yield return Wait(0.8f);
            game.AutoPause();
            yield return Wait(0.8f);
            yield return ClickUi("Pause Title");
            yield return Wait(2.5f);
            Check(Visible("Continue") && AnyText("2 packed, waiting for you"), "resume: the title's Continue line says 2 things are packed and waiting");
            yield return Shot("resume-continue");
            yield return ClickUi("Continue");
            yield return Wait(2.5f);
            yield return StoryToPacking();
            yield return Wait(1.5f);
            Check(game.IsPlaying && game.Items.Count(i => i.State == ItemState.Packed) == 2, "resume: CONTINUE brings the waiting trunk back");

            foreach (var (itemId, cells) in finale.Skip(2))
            {
                var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                if (item != null) game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                yield return Wait(0.03f);
            }
            yield return Wait(0.8f);
            game.AutoClose();
            for (float t = 0f; t < 9f && !game.IsShowingResults; t += Time.unscaledDeltaTime) yield return null;
            Check(game.IsShowingResults && GameController.SavedTrunk(lastId) == "", "resume: closing the trunk clears the saved trunk");
            game.AutoTransitionTrip(last);
            yield return Wait(2.5f);
            yield return StoryToPacking();
            yield return Wait(1f);
            Check(game.IsPlaying && game.Items.All(i => i.State == ItemState.Pile), "resume: after a close, the trip starts with an empty trunk");

            // A saved trunk that no longer fits this version of the trip is dropped, not half-loaded.
            Prefs.SetString("ptt.trunk." + lastId, "1|0|0:" + game.Items[0].Def.Id + ":99:0:0:0:0:0");
            game.AutoTransitionTrip(last);
            yield return Wait(2.5f);
            yield return StoryToPacking();
            yield return Wait(1f);
            Check(game.IsPlaying && game.Items.All(i => i.State == ItemState.Pile) && GameController.SavedTrunk(lastId) == "",
                "resume: a saved trunk that doesn't fit the trip is dropped and the trip starts fresh");
        }

        /// <summary>A trunk layout to compare: each item packed (cell + orientation) or not.</summary>
        List<(bool Packed, Vector3Int Pos, Quaternion Rot)> Snapshot() =>
            game.Items.Select(i => (i.State == ItemState.Packed, i.GridPos, i.Orientation)).ToList();

        bool SameLayout(List<(bool Packed, Vector3Int Pos, Quaternion Rot)> want)
        {
            var now = Snapshot();
            return now.Count == want.Count && now.Zip(want, (a, b) => a.Packed == b.Packed && (!a.Packed || (a.Pos == b.Pos && Quaternion.Angle(a.Rot, b.Rot) < 1f))).All(x => x);
        }

        /// <summary>
        /// Undo and redo survive leaving: pack five things one by one, leave through the pause menu, come
        /// back, and Z walks back through every step, then Shift+Z forward again. A RESTART survives leaving
        /// too: RESTART, leave, come back to an empty trunk, and Z brings the trunk and its hint mark back.
        /// The saved size of a fully packed 25-item minivan (with its history) is logged.
        /// </summary>
        IEnumerator ResumeHistoryChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            PerfProbe.Begin("playing");
            int index = LevelIndex("grandma");
            game.AutoStartLevel(index);
            yield return Wait(2.5f);
            if (!solutions.TryGetValue("grandma", out var placements)) { Check(false, "history: grandma has a solution"); yield break; }
            var steps = new List<List<(bool, Vector3Int, Quaternion)>> { Snapshot() };
            foreach (var (itemId, cells) in placements.Take(5))
            {
                var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                if (item != null) game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                yield return Wait(0.6f);
                steps.Add(Snapshot());
            }

            IEnumerator LeaveAndComeBack()
            {
                game.AutoPause();
                yield return Wait(0.8f);
                yield return ClickUi("Pause Map");
                yield return Wait(2.5f);
                game.AutoTransitionTrip(index);
                yield return Wait(2.5f);
                yield return StoryToPacking();
                yield return Wait(1f);
            }

            yield return LeaveAndComeBack();
            Check(game.IsPlaying && SameLayout(steps[5]) && game.UndoDepth == 5, $"history: after leaving, the trunk and all 5 undo steps come back (undo depth {game.UndoDepth})");
            int walked = 0;
            for (int i = 4; i >= 0; i--)
            {
                yield return Press(Key.Z);
                yield return Wait(0.5f);
                if (SameLayout(steps[i])) walked++;
            }
            Check(walked == 5 && game.Items.All(i => i.State == ItemState.Pile), $"history: Z walks back through each step from before leaving ({walked}/5)");
            for (int i = 0; i < 5; i++) { yield return PressShift(Key.Z); yield return Wait(0.4f); }
            yield return Wait(0.4f);
            Check(SameLayout(steps[5]), "history: Shift+Z redoes all five again");

            // Undo back two steps, leave, and the redo list waits too.
            yield return Press(Key.Z);
            yield return Wait(0.4f);
            yield return Press(Key.Z);
            yield return Wait(0.6f);
            yield return LeaveAndComeBack();
            bool atThree = SameLayout(steps[3]);
            yield return PressShift(Key.Z);
            yield return Wait(0.4f);
            yield return PressShift(Key.Z);
            yield return Wait(0.6f);
            Check(atThree && SameLayout(steps[5]), "history: the redo list survives leaving too (two Shift+Z after coming back)");

            // RESTART, leave: an empty trunk that Z can still fill, with the hint mark.
            game.AutoAskGrandpa();
            yield return Wait(0.3f);
            game.AutoClearHint();
            yield return ClickUi("Restart");
            yield return Wait(0.8f);
            bool restartFresh = !game.HintedThisTry && game.Items.All(i => i.State == ItemState.Pile);
            yield return LeaveAndComeBack();
            Check(restartFresh && game.IsPlaying && game.Items.All(i => i.State == ItemState.Pile) && !game.HintedThisTry && game.Ui.ToastShowing("still brings back"),
                "history: after RESTART and leaving, the trip opens empty and a toast says Z brings it back");
            yield return Shot("history-empty-trunk-toast");
            yield return Press(Key.Z);
            yield return Wait(0.8f);
            Check(SameLayout(steps[5]) && game.HintedThisTry, "history: Z after coming back undoes the RESTART, hint mark and all");

            // The biggest trunk, packed one thing at a time: how big the save gets.
            int biggest = Enumerable.Range(0, GameDatabase.Levels.Count)
                .OrderByDescending(i => GameDatabase.Levels[i].Required.Count + GameDatabase.Levels[i].Bonus.Count).First();
            string bigId = GameDatabase.Levels[biggest].Id;
            game.AutoStartLevel(biggest);
            yield return Wait(2.5f);
            if (solutions.TryGetValue(bigId, out var big))
                foreach (var (itemId, cells) in big)
                {
                    var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                    if (item != null) game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                    yield return Wait(0.03f);
                }
            yield return Wait(0.8f);
            int size = GameController.SavedTrunk(bigId).Length;
            Log($"history: {bigId} fully packed ({game.UndoDepth} undo steps) saves {size} characters");
            Check(game.UndoDepth == big.Count && size > 0 && size < 64 * 1024, $"history: {bigId}'s saved trunk with {game.UndoDepth} steps stays small ({size / 1024f:0.0} KB)");
            game.AutoStartLevel(biggest);
            yield return Wait(1f);
        }

        /// <summary>
        /// A waiting trunk you can see: leave the next trip half-packed and the title's parked car has those
        /// things in its trunk (not on the blanket), and the Trip Map's label says how many are waiting. On a
        /// trip that opens a chapter, coming back skips the chapter card and the typing: BACK TO PACKING is
        /// there at once, where a fresh start is still on the chapter card.
        /// </summary>
        IEnumerator WaitingTrunkChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            PerfProbe.Begin("playing");
            int next = game.NextTrip;
            string nextId = GameDatabase.Levels[next].Id;
            game.AutoStartLevel(next);
            yield return Wait(2.5f);
            if (!solutions.TryGetValue(nextId, out var placements)) { Check(false, "waiting: the next trip has a solution"); yield break; }
            foreach (var (itemId, cells) in placements.Take(3))
            {
                var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                if (item != null) game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                yield return Wait(0.03f);
            }
            yield return Wait(0.8f);
            var left = Snapshot();
            game.AutoPause();
            yield return Wait(0.8f);
            yield return ClickUi("Pause Title");
            yield return Wait(3f);
            Check(Visible("Continue") && game.Items.Count(i => i.State == ItemState.Packed) == 3 && SameLayout(left),
                $"waiting: the title's parked car has the 3 waiting things in its trunk, where they were left ({GameDatabase.Levels[next].Title})");
            yield return Shot("waiting-title");
            yield return ClickUi("Trip Map");
            yield return Wait(2.5f);
            Check(Visible("Map Back") && AnyText("3 PACKED, WAITING"), "waiting: the Trip Map's label says 3 packed, waiting");
            yield return Shot("waiting-map");
            yield return ClickUi("Map Back");
            yield return Wait(1f);
            game.AutoStartLevel(next);
            yield return Wait(1f);

            // A trip that opens a chapter: fresh, the chapter card plays first; with a waiting trunk, straight back.
            int chapterStart = Enumerable.Range(1, GameDatabase.Levels.Count - 1).First(i => GameDatabase.Levels[i].IsFirstInChapter);
            string chapterId = GameDatabase.Levels[chapterStart].Id;
            GameController.AutoForgetTrunk(chapterId);
            game.AutoTransitionTrip(chapterStart);
            yield return Wait(2.3f);
            bool freshWaits = game.IsInStory && !game.Ui.StoryReady;
            yield return StoryToPacking();
            yield return Wait(1f);
            if (!solutions.TryGetValue(chapterId, out var chapterPlacements)) { Check(false, "waiting: the chapter's first trip has a solution"); yield break; }
            foreach (var (itemId, cells) in chapterPlacements.Take(2))
            {
                var item = game.Items.FirstOrDefault(it => it.Def.Id == itemId && it.State == ItemState.Pile);
                if (item != null) game.AutoPlace(item, FindOrientation(item.Def.Shape, cells), cells.Aggregate(Vector3Int.Min));
                yield return Wait(0.03f);
            }
            yield return Wait(0.8f);
            game.AutoPause();
            yield return Wait(0.8f);
            yield return ClickUi("Pause Map");
            yield return Wait(2.5f);
            game.AutoTransitionTrip(chapterStart);
            yield return Wait(2.3f);
            Check(freshWaits && game.IsInStory && game.Ui.StoryReady && AnyText("BACK TO PACKING") && AnyText("Your trunk is waiting: 2 packed"),
                $"waiting: {GameDatabase.Levels[chapterStart].Title} with a waiting trunk skips the chapter card and the typing (fresh: still on the chapter card)");
            yield return Shot("waiting-story");
            yield return StoryToPacking();
            yield return Wait(1f);
            Check(game.IsPlaying && game.Items.Count(i => i.State == ItemState.Packed) == 2, "waiting: BACK TO PACKING puts the 2 back in");
            game.AutoStartLevel(chapterStart);
            yield return Wait(1f);
        }

        /// <summary>
        /// Seeing into the trunk, on the Garage Sale pickup where the third item has a shelf over a gap:
        /// tucked into the gap, whatever hides the ghost goes see-through (and comes back when the ghost
        /// moves up on top); holding Tab turns everything packed see-through and aims through it.
        /// </summary>
        IEnumerator SeeThroughChecks()
        {
            // Build a covered gap on First Snow (a 3-tall SUV): a small thing on the floor one row in, the
            // skis resting on it and sticking out over the floor, another small thing in front of the gap,
            // then hold a third small thing.
            PerfProbe.Begin("playing");
            game.AutoStartLevel(LevelIndex("snow"));
            yield return Wait(2.5f);
            var smalls = game.Items.Where(i => i.Def.Shape.Voxels.Length == 1 && !i.Def.Fragile).ToList();
            PackItem plank = null;
            Quaternion plankTurn = Quaternion.identity;
            foreach (var candidate in game.Items.Where(i => i.Def.Shape.Voxels.Length >= 2 && !i.Def.Fragile))
            {
                var flat = candidate.Def.Shape.Orientations().FirstOrDefault(o => o.Shape.Size.y == 1 && o.Shape.Size.z == 1 && o.Shape.Size.x >= 2);
                if (flat.Shape == null) continue;
                plank = candidate;
                plankTurn = flat.Rotation;
                break;
            }
            if (smalls.Count < 3 || plank == null) { Check(false, "see-through: First Snow has the pieces for an overhang"); yield break; }
            // Find a spot clear of the wheel wells: support at (x, 0, z), skis from (x, 1, z) along +x,
            // the gap at (x + 1, 0, z) and the blocker in front of it at (x + 1, 0, z - 1).
            bool built = false;
            var gap = Vector3Int.zero;
            for (int z = 1; z < game.TrunkSize.z && !built; z++)
            for (int x = 0; x + 1 < game.TrunkSize.x && !built; x++)
            {
                if (!game.AutoPlace(smalls[0], Quaternion.identity, new Vector3Int(x, 0, z))) continue;
                if (game.AutoPlace(plank, plankTurn, new Vector3Int(x, 1, z)))
                {
                    if (game.AutoPlace(smalls[1], Quaternion.identity, new Vector3Int(x + 1, 0, z - 1)))
                    {
                        built = true;
                        gap = new Vector3Int(x + 1, 0, z);
                        break;
                    }
                    game.AutoPutBackToPile(plank);
                }
                game.AutoPutBackToPile(smalls[0]);
            }
            Check(built, $"see-through: built a covered gap at {gap} ({plank.Def.Name} over it, {smalls[1].Def.Name} in front)");
            if (!built) yield break;
            yield return Wait(0.8f);
            var item = smalls[2];
            game.AutoHold(item);
            var trunk = game.CurrentVehicle.transform;

            // Aim at the top of the plank over the gap, then step down into the gap.
            yield return MoveMouse(trunk.TransformPoint(new Vector3(gap.x + 0.5f, 2f, gap.z + 0.5f)));
            yield return Wait(0.3f);
            game.CurrentTarget(out var pos, out _);
            for (int i = 0; i < 4 && pos.y > 0; i++)
            {
                yield return Press(Key.S);
                yield return Wait(0.2f);
                game.CurrentTarget(out pos, out _);
            }
            yield return Wait(0.3f);
            Check(pos == gap && plank.SeeThrough && game.Items.Where(i => i.SeeThrough).All(i => i.State == ItemState.Packed),
                $"see-through: tucked under the {plank.Def.Name} ({pos}), it goes see-through");
            yield return Shot("see-through-gap");
            yield return Press(Key.W);
            yield return Press(Key.W);
            yield return Wait(0.3f);
            game.CurrentTarget(out pos, out _);
            Check(pos.y > 1 && !plank.SeeThrough, $"see-through: back on top ({pos}), the {plank.Def.Name} comes back");

            // X-ray: everything packed is see-through, and aiming at the floor passes through the plank.
            var floor = trunk.TransformPoint(new Vector3(gap.x + 0.5f, 0.02f, gap.z + 0.5f));
            yield return MoveMouse(floor);
            yield return Wait(0.3f);
            game.CurrentTarget(out var without, out _);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Tab));
            yield return Wait(0.4f);
            yield return MoveMouse(floor);
            yield return Wait(0.3f);
            game.CurrentTarget(out var with, out _);
            Check(game.XRayActive && game.Items.Where(i => i.State == ItemState.Packed).All(i => i.SeeThrough),
                "see-through: holding Tab shows every packed item see-through");
            Check(with == gap && without != gap, $"see-through: with Tab the aim passes through to the gap ({without} -> {with})");
            yield return Shot("see-through-xray");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return Wait(0.3f);
            yield return Press(Key.Escape);
            yield return Wait(0.4f);
            Check(!game.XRayActive && game.SeeThroughCount == 0 && game.Items.All(i => !i.SeeThrough),
                "see-through: letting go of Tab and putting the item back restores everything");

            // Toggle mode (Settings → Accessibility): one press turns X-ray on and it stays on after the
            // key is let go; the next press turns it off. Aiming still passes through to the gap.
            GameSettings.XRayToggle = true;
            Uncap();
            game.AutoHold(item);
            yield return MoveMouse(floor);
            yield return Wait(0.3f);
            yield return Press(Key.Tab);
            yield return Wait(0.5f);
            yield return MoveMouse(floor);
            yield return Wait(0.3f);
            game.CurrentTarget(out var toggled, out _);
            Check(game.XRayActive && game.Items.Where(i => i.State == ItemState.Packed).All(i => i.SeeThrough) && toggled == gap
                && game.Ui.XRayOnShown && game.Ui.ToastShowing("X-ray on") && game.Ui.KeyboardHintCaptions().Contains("x-ray on"),
                $"x-ray toggle: one Tab press turns X-ray on and it stays on after letting go (aim {toggled}, hint reads \"x-ray on\")");
            yield return Shot("xray-toggle-on");
            yield return Press(Key.Tab);
            yield return Wait(0.4f);
            Check(!game.XRayActive && !game.Ui.XRayOnShown && game.Ui.KeyboardHintCaptions().Contains("x-ray"),
                "x-ray toggle: a second Tab press turns it off");
            yield return Press(Key.Escape);
            yield return Wait(0.4f);

            // Grandpa's hint ghost in the same gap, with empty hands: whatever hides it goes see-through
            // too (round 2 only checked this by eye), and comes back once the hint is gone.
            game.AutoShowHint(item, gap, Quaternion.identity);
            yield return Wait(0.4f);
            var faded = game.Items.Where(i => i.SeeThrough).ToList();
            Check(game.Held == null && game.HintItem == item && plank.SeeThrough && faded.All(i => i.State == ItemState.Packed),
                $"see-through: Grandpa's ghost in the gap makes what hides it see-through ({string.Join(", ", faded.Select(i => i.Def.Name))})");
            yield return Shot("see-through-hint");
            game.AutoClearHint();
            yield return Wait(0.3f);
            Check(game.SeeThroughCount == 0 && game.Items.All(i => !i.SeeThrough), "see-through: clearing the hint restores everything");

            // Toggled X-ray doesn't outlive the trip: on, then leave through the pause menu.
            yield return Press(Key.Tab);
            yield return Wait(0.3f);
            bool wasOn = game.XRayActive;
            game.AutoPause();
            yield return Wait(0.8f);
            Check(AnyText($"{Bindings.Label(Bindings.Action.XRay)} (press)"), "x-ray toggle: the pause card says press, not hold");
            yield return ClickUi("Pause Map");
            yield return Wait(2.5f);
            Check(wasOn && !game.XRayActive && !game.Ui.XRayOnShown && game.SeeThroughCount == 0, "x-ray toggle: leaving the trip turns it off");
            GameSettings.XRayToggle = false;
            Uncap();
        }

        /// <summary>
        /// The HUD at this screen shape, at 80%, 100% and 120% interface size, on the biggest trip with
        /// everything showing (held card, a tip, a toast): no two pieces overlap, nothing leaves the
        /// screen, and every packing-list row fits its area without overlapping the next.
        /// </summary>
        IEnumerator LayoutChecks()
        {
            // The layout check places its own tip; the game's real tips (a fresh save in layout-only runs)
            // would come and go mid-check.
            bool tips = GameSettings.Tips;
            GameSettings.Tips = false;
            Uncap();
            PerfProbe.Begin("playing");
            game.AutoStartLevel(LevelIndex("reunion2"));
            yield return Wait(2.5f);
            // The last row: if the list scrolls, holding it has to bring it into view.
            var last = game.Items.Last(i => i.State == ItemState.Pile);
            game.AutoHold(last);
            string size = $"{Screen.width}x{Screen.height}";
            foreach (var scale in new[] { 0.8f, 1f, 1.2f })
            {
                GameSettings.UiScale = scale;
                Uncap();
                // REDO widens the button row while there's something to redo: measure it at its widest.
                game.Ui.SetRedoAvailable(true);
                game.Ui.ShowTip("Grandpa's tip for the layout check: a sentence about as long as the longest real tip is.");
                game.Ui.Toast("A toast for the layout check, as long as the longest real one is.", 30f);
                yield return Wait(1.2f);
                var rects = game.Ui.HudRects();
                var screen = new Rect(0, 0, Screen.width, Screen.height);
                var problems = new List<string>();
                var names = rects.Keys.ToList();
                for (int i = 0; i < names.Count; i++)
                {
                    var a = rects[names[i]];
                    if (a.xMin < -1 || a.yMin < -1 || a.xMax > screen.xMax + 1 || a.yMax > screen.yMax + 1) problems.Add($"{names[i]} off screen");
                    for (int j = i + 1; j < names.Count; j++)
                    {
                        var b = rects[names[j]];
                        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                        if (w > 2f && h > 2f) problems.Add($"{names[i]} overlaps {names[j]}");
                    }
                }
                var (area, rows) = game.Ui.ListRects();
                // Rows scrolled out of view are fine when the list scrolls; otherwise none may spill.
                int outside = game.Ui.ListScrolls ? 0 : rows.Count(r => r.yMin < area.yMin - 1 || r.yMax > area.yMax + 1);
                if (!game.Ui.RowVisible(last)) problems.Add($"the held {last.Def.Name}'s row is scrolled out of view");
                float smallest = rows.Count > 0 ? rows.Min(r => r.height) : 0f;
                float overflow = game.Ui.ListTextOverflow();
                int stacked = 0;
                var sorted = rows.OrderByDescending(r => r.yMax).ToList();
                for (int i = 1; i < sorted.Count; i++) if (sorted[i].yMax > sorted[i - 1].yMin + 1f) stacked++;
                if (outside > 0) problems.Add($"{outside} of {rows.Count} list rows spill out of the list");
                if (stacked > 0) problems.Add($"{stacked} list rows overlap the one above");
                if (overflow > 1f) problems.Add($"list text is {overflow:0} units taller than its {smallest:0}-unit rows");
                if (smallest < 19.5f) problems.Add($"list rows squashed to {smallest:0} units");
                float countsOver = game.Ui.CountsOverflow(), meterGap = game.Ui.MeterGapToHeading();
                if (countsOver > 0f) problems.Add($"the counts line is {countsOver:0} units too wide for one line");
                if (meterGap < 4f) problems.Add($"the star meter is {-meterGap:0} units into the heading");
                problems.AddRange(game.Ui.ListCrowdedRows().Take(4));
                var wrapped = game.Ui.WrappedKeyHints();
                if (wrapped.Count > 0) problems.Add("key hints wrap onto two lines: " + string.Join(", ", wrapped));
                Check(problems.Count == 0, $"layout {size} at {scale * 100:0}%: " + (problems.Count == 0 ? $"{names.Count} HUD pieces, {rows.Count} list rows of {smallest:0} units{(game.Ui.ListScrolls ? " (scrolling)" : "")}, counts line {-countsOver:0} units spare, star meter {meterGap:0} clear of the heading, all clear" : string.Join("; ", problems)));
                yield return Shot($"layout-{size}-{scale * 100:0}");

                // The "now playing" cassette (shown for a few seconds when a track starts) never covers a
                // button or the trip tag, with or without Grandpa's tip.
                game.Ui.ShowNowPlaying("A song title for the layout check", "TAD");
                yield return Wait(1f);
                var withTip = game.Ui.HudRects();
                game.Ui.HideTip();
                game.Ui.Toast("", 0.01f);
                yield return Wait(0.8f);
                game.Ui.ShowNowPlaying("A song title for the layout check", "TAD");
                yield return Wait(1f);
                var withoutTip = game.Ui.HudRects();
                var cassetteProblems = new List<string>();
                foreach (var (label, set) in new[] { ("with the tip", withTip), ("without it", withoutTip) })
                {
                    if (!set.TryGetValue("now playing", out var np)) continue;
                    if (np.xMin < -1 || np.yMin < -1 || np.xMax > screen.xMax + 1 || np.yMax > screen.yMax + 1) cassetteProblems.Add($"{label}: off screen");
                    foreach (var kv in set)
                    {
                        if (kv.Key == "now playing") continue;
                        float w = Mathf.Min(np.xMax, kv.Value.xMax) - Mathf.Max(np.xMin, kv.Value.xMin), h = Mathf.Min(np.yMax, kv.Value.yMax) - Mathf.Max(np.yMin, kv.Value.yMin);
                        if (w > 2f && h > 2f) cassetteProblems.Add($"{label}: it overlaps the {kv.Key}");
                    }
                }
                Check(withoutTip.ContainsKey("now playing") && cassetteProblems.Count == 0,
                    $"layout {size} at {scale * 100:0}%: the now-playing cassette " + (cassetteProblems.Count == 0
                        ? $"is clear of everything ({(withTip.ContainsKey("now playing") ? "shown beside the tip" : "skipped while the tip has its spot")})"
                        : string.Join("; ", cassetteProblems)));
                yield return Shot($"layout-{size}-{scale * 100:0}-cassette");

                // The pause card's HOW TO PACK list names the keyboard-only controls, and every row fits the card.
                game.AutoPause();
                yield return Wait(1.2f);
                var pauseRows = game.Ui.PauseKeyRows();
                var pauseProblems = game.Ui.PauseKeysProblems();
                bool arrowsRow = pauseRows.Any(r => r.StartsWith("ARROWS"));
                Check(game.IsPaused && arrowsRow && pauseProblems.Count == 0,
                    $"layout {size} at {scale * 100:0}%: the pause card lists {pauseRows.Count} keyboard rows" + (arrowsRow ? " (with ARROWS · ENTER)" : ", none for the arrow keys")
                    + (pauseProblems.Count == 0 ? ", all inside the card" : ": " + string.Join("; ", pauseProblems)));
                if (Mathf.Approximately(scale, 1f)) yield return Shot($"pause-keys-{size}");
                game.AutoResume();
                yield return Wait(0.5f);
            }
            GameSettings.ResetUiScale();
            GameSettings.Tips = tips;
            Uncap();
            game.Ui.HideTip();
            game.Ui.SetRedoAvailable(false);
            game.AutoPutBack();
            yield return Wait(0.5f);
        }

        /// <summary>
        /// The main menu and the trip map at this screen shape, at 80%, 100% and 120% interface size: the
        /// menu stays clear of the tagline, and the map's page clear of the note and the heading.
        /// </summary>
        IEnumerator MenuLayoutChecks()
        {
            string size = $"{Screen.width}x{Screen.height}";
            PerfProbe.Begin("menus");
            foreach (var scale in new[] { 0.8f, 1f, 1.2f })
            {
                GameSettings.UiScale = scale;
                Uncap();
                game.AutoShowMainMenu();
                yield return Wait(1.5f);
                var problems = game.Ui.MenuLayoutProblems();
                if (scale > 1.1f) yield return Shot($"menu-layout-{size}-{scale * 100:0}-title");
                game.AutoShowMenu();
                yield return Wait(1.8f);
                problems.AddRange(game.Ui.MenuLayoutProblems());
                if (scale > 1.1f) yield return Shot($"menu-layout-{size}-{scale * 100:0}-map");
                Check(problems.Count == 0, $"menu layout {size} at {scale * 100:0}%: " + (problems.Count == 0 ? "the main menu and the trip map are clear" : string.Join("; ", problems)));
            }
            GameSettings.ResetUiScale();
            Uncap();
            game.AutoShowMainMenu();
            yield return Wait(1f);
        }

        /// <summary>
        /// Readable text at this window size with default settings: on every screen (title, menus, each
        /// settings tab, the map, story, packing with a held item, tip and toast, pause, postcard, album and
        /// its close-up) the smallest text showing is at least 12 screen pixels, Valve's recommended
        /// minimum at the Steam Deck's 1280x800.
        /// </summary>
        IEnumerator LegibilityChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            const float MinPx = 12f;
            string size = $"{Screen.width}x{Screen.height}";
            GameSettings.ResetUiScale();
            Uncap();
            Log($"legibility {size}: interface size {GameSettings.UiScale * 100f:0}% by default");
            static string Short(string text) => text.Length > 28 ? text.Substring(0, 28) + "…" : text;
            IEnumerator Measure(string screen)
            {
                var sizes = game.Ui.VisibleTextSizes();
                if (sizes.Count == 0) { Check(false, $"legibility {size} {screen}: no text found"); yield break; }
                var (name, text, px) = sizes[0];
                var small = sizes.Where(x => x.Px < MinPx - 0.05f).Select(x => $"{x.Name} {x.Px:0.0}").Distinct().Take(8).ToList();
                Check(small.Count == 0, $"legibility {size} {screen}: smallest text {px:0.0} px (\"{Short(text.Replace("\n", " "))}\" in {name}), {sizes.Count} texts" +
                    (small.Count > 0 ? "; under 12 px: " + string.Join(", ", small) : ""));
                var wraps = game.Ui.WrapProblems().Distinct().ToList();
                Check(wraps.Count == 0, $"wrapping {size} {screen}: " + (wraps.Count == 0 ? "no text breaks a word or spills out of its box" : string.Join("; ", wraps.Take(6))));
                yield return ContrastCheck($"{size} {screen}");
            }

            PerfProbe.Begin("menus");
            game.AutoShowTitle();
            yield return Wait(3f);
            yield return Measure("title");
            yield return Press(Key.Space);
            yield return Wait(1.6f);
            yield return Measure("main menu");
            yield return Shot($"legibility-{size}-menu");
            yield return ClickUi("Settings");
            yield return Wait(1f);
            foreach (var tab in new[] { "AUDIO", "DISPLAY", "GRAPHICS", "GAMEPLAY", "ACCESSIBILITY", "CONTROLS" })
            {
                yield return ClickUi("Tab " + tab);
                yield return Wait(0.8f);
                yield return Measure("settings " + tab.ToLowerInvariant());
            }
            yield return Shot($"legibility-{size}-settings");
            yield return Press(Key.Escape);
            yield return Wait(0.6f);
            yield return ClickUi("Trip Map");
            yield return Wait(1.6f);
            yield return Measure("trip map");
            yield return Shot($"legibility-{size}-map");
            // Every chapter's page: each pin wears its car's colour.
            for (int i = 0; i < GameDatabase.Chapters.Count; i++)
            {
                yield return ClickUi("Prev Page");
                yield return Wait(0.3f);
            }
            for (int page = 1; page <= GameDatabase.Chapters.Count; page++)
            {
                yield return ClickUi("Next Page");
                // The page's last label finishes fading in about 1.1 s after the flip.
                yield return Wait(1.5f);
                yield return Measure(page < GameDatabase.Chapters.Count ? $"trip map page {page + 1}" : $"trip map neighbours ({(game.FavoursUnlocked ? "open" : "locked")})");
            }
            yield return Press(Key.Escape);
            yield return Wait(1f);

            game.AutoBeginTrip(LevelIndex("grandma"));
            yield return Wait(7f);
            yield return Measure("story");
            yield return Shot($"legibility-{size}-story");

            PerfProbe.Begin("playing");
            game.AutoStartLevel(LevelIndex("reunion2"));
            yield return Wait(2.5f);
            game.AutoHold(game.Items.Last(i => i.State == ItemState.Pile));
            game.Ui.ShowTip("Grandpa's tip for the legibility check, about as long as a real one.");
            game.Ui.Toast("A toast for the legibility check.", 30f);
            yield return Wait(1.2f);
            yield return Measure("packing");
            yield return Shot($"legibility-{size}-packing");
            game.Ui.HideTip();
            game.AutoPutBack();
            game.AutoPause();
            yield return Wait(1f);
            yield return Measure("pause");
            game.AutoResume();
            yield return Wait(0.5f);

            if (solutions.TryGetValue("wagon", out var wagon))
            {
                game.AutoStartLevel(LevelIndex("wagon"));
                yield return Wait(2f);
                yield return PackAll("wagon", wagon);
                yield return CloseAndWait();
                yield return Wait(3f);
                yield return Measure("postcard");
                yield return Shot($"legibility-{size}-postcard");
            }
            // A favour for the neighbours: its page (open), texts, packing and postcard.
            {
                var savedStars = GameDatabase.Levels.ToDictionary(l => l.Id, l => Prefs.GetInt("ptt.stars." + l.Id, 0));
                bool hadFavour = GameController.SavedFavour != "";
                for (int i = 0; i <= LevelIndex(Favours.UnlockedBy); i++)
                    if (savedStars[GameDatabase.Levels[i].Id] == 0) Prefs.SetInt("ptt.stars." + GameDatabase.Levels[i].Id, 1);
                PerfProbe.Begin("menus");
                game.AutoShowMenu();
                yield return Wait(1.8f);
                yield return FlipToNeighbours();
                yield return Measure("trip map neighbours (open)");
                yield return Shot($"legibility-{size}-neighbours");
                NeighboursPageLayoutCheck(size);
                var favour = game.AutoCurrentFavour();
                game.AutoBeginFavour(favour);
                // Every text in, and LET'S PACK settled (a favour has four texts, so it comes later than a fixed wait).
                for (float t = 0f; t < 15f && !Visible("Start"); t += Time.unscaledDeltaTime) yield return null;
                yield return Wait(1.5f);
                yield return Measure("favour story");
                PerfProbe.Begin("playing");
                yield return StoryToPacking();
                yield return Wait(2f);
                game.AutoHold(game.Items.Last(i => i.State == ItemState.Pile));
                yield return Wait(1f);
                yield return Measure("favour packing");
                yield return Shot($"legibility-{size}-favour-packing");
                game.AutoPutBack();
                yield return PackAll(favour.Id, favour.Packing);
                yield return CloseAndWait();
                yield return Wait(3f);
                yield return Measure("favour postcard");
                yield return Shot($"legibility-{size}-favour-postcard");
                foreach (var kv in savedStars) Prefs.SetInt("ptt.stars." + kv.Key, kv.Value);
                if (!hadFavour) game.AutoResetFavours();
            }

            game.AutoShowMenuAlbum();
            yield return WaitForAlbum();
            yield return Measure("album");
            if (Visible("Polaroid wagon"))
            {
                yield return ClickUi("Polaroid wagon");
                yield return Wait(0.6f);
                yield return Measure("album close-up");
                yield return Shot($"legibility-{size}-album-zoom");
                yield return Press(Key.Escape);
                yield return Wait(0.5f);
            }
            yield return Press(Key.Escape);
            yield return Wait(1f);
        }

        static readonly float[] SrgbToLinear = Enumerable.Range(0, 256).Select(v =>
        {
            float c = v / 255f;
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }).ToArray();

        /// <summary>
        /// WCAG contrast of every visible text against what's actually behind it. The background is read from the
        /// screen: the median luminance of a thin band just outside the glyphs. The text is its own colour
        /// (blended by its alpha over that background), as WCAG defines it; when the rendered glyphs measure
        /// higher (99.5th-percentile pixel), or a solid outline (which WCAG counts as part of a letter) stands out
        /// from the background more, the best of those counts.
        /// Body text needs 4.5:1; large text (24 px, or 18.7 px bold) and single symbols 3:1. Text that's covered
        /// by an overlay, faded below 60% (a locked trip's polaroid) or part of a control that can't be used (a locked
        /// trip's pin) is skipped, like WCAG's inactive parts. A stamp's
        /// text is checked against its own fill (<see cref="TextBacking"/>), since its letters nearly touch the border.
        /// </summary>
        IEnumerator ContrastCheck(string screen)
        {
            yield return new WaitForEndOfFrame();
            int w = Screen.width, h = Screen.height;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply(false);
            var pixels = tex.GetPixels32();
            Destroy(tex);
            PerfProbe.Ignore();
            float Lum(int x, int y)
            {
                var c = pixels[y * w + x];
                return 0.2126f * SrgbToLinear[c.r] + 0.7152f * SrgbToLinear[c.g] + 0.0722f * SrgbToLinear[c.b];
            }
            static float LumOf(Color c) =>
                0.2126f * SrgbToLinear[Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255)] + 0.7152f * SrgbToLinear[Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255)] +
                0.0722f * SrgbToLinear[Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255)];
            static float Ratio(float a, float b) => (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
            var band = new List<float>();
            var inner = new List<float>();
            var low = new List<string>();
            float worst = float.MaxValue;
            string worstWhat = "";
            int measured = 0, covered = 0, faded = 0;
            foreach (var t in game.Ui.VisibleTextSamples())
            {
                if (t.Covered) { covered++; continue; }
                if (t.Alpha < 0.6f || t.Inactive) { faded++; continue; }
                var r = t.Glyphs;
                int x0 = Mathf.FloorToInt(r.xMin), y0 = Mathf.FloorToInt(r.yMin), x1 = Mathf.CeilToInt(r.xMax), y1 = Mathf.CeilToInt(r.yMax);
                int m = Mathf.Max(3, Mathf.RoundToInt(t.Px * 0.12f));
                band.Clear();
                inner.Clear();
                // Classify each pixel in the text's own space, so a tilted card's band follows the tilt
                // (screen-aligned, the band's corners land off a tilted card and read the scene behind it).
                var lr = t.LocalGlyphs;
                float unit = 1f / Mathf.Max(0.0001f, t.PixelsPerUnit);
                float near = unit, far = m * unit;
                for (int y = Mathf.Max(0, y0 - m); y < Mathf.Min(h, y1 + m); y++)
                for (int x = Mathf.Max(0, x0 - m); x < Mathf.Min(w, x1 + m); x++)
                {
                    Vector2 p = t.ScreenToLocal.MultiplyPoint3x4(new Vector3(x + 0.5f, y + 0.5f, 0f));
                    bool inGlyphs = p.x >= lr.xMin && p.x < lr.xMax && p.y >= lr.yMin && p.y < lr.yMax;
                    bool nearGlyphs = p.x >= lr.xMin - near && p.x < lr.xMax + near && p.y >= lr.yMin - near && p.y < lr.yMax + near;
                    bool inBand = p.x >= lr.xMin - far && p.x < lr.xMax + far && p.y >= lr.yMin - far && p.y < lr.yMax + far;
                    if (inGlyphs) inner.Add(Lum(x, y));
                    else if (!nearGlyphs && inBand) band.Add(Lum(x, y));
                }
                if (band.Count < 16 || inner.Count < 16) continue;
                band.Sort();
                float bg = band[band.Count / 2];
                inner.Sort((a, b) => Mathf.Abs(a - bg).CompareTo(Mathf.Abs(b - bg)));
                float rendered = Ratio(bg, inner[Mathf.Min(inner.Count - 1, Mathf.FloorToInt(inner.Count * 0.995f))]);
                float own = LumOf(t.Color);
                float specified = Ratio(bg, Mathf.Lerp(bg, own, Mathf.Clamp01(t.Alpha)));
                float halo = t.Halo.HasValue ? Ratio(bg, LumOf(t.Halo.Value)) : 0f;
                // A stamp's letters nearly touch its border, so the band reads the border; its fill is known.
                if (t.Backing.HasValue)
                {
                    float fill = LumOf(t.Backing.Value);
                    specified = Ratio(fill, Mathf.Lerp(fill, own, Mathf.Clamp01(t.Alpha)));
                }
                float ratio = Mathf.Max(specified, rendered, halo);
                float need = t.Px >= 24f || (t.Bold && t.Px >= 18.66f) || t.Text.Trim().Length <= 1 ? 3f : 4.5f;
                measured++;
                string what = $"{t.Name} \"{(t.Text.Length > 24 ? t.Text.Substring(0, 24) + "…" : t.Text).Replace("\n", " ")}\" {ratio:0.0}:1";
                if (ratio / need < worst) { worst = ratio / need; worstWhat = what + $" (needs {need:0.#})"; }
                if (ratio < need)
                {
                    low.Add(what + $" (needs {need:0.#})");
                    Log($"contrast detail {screen} | {t.Name} | {x0},{y0},{x1},{y1} | bg {bg:0.000} specified {specified:0.0} rendered {rendered:0.0} halo {halo:0.0} | {ratio:0.0}");
                }
            }
            var shown = low.Distinct().Take(10).ToList();
            Check(low.Count == 0, $"contrast {screen}: {measured} texts (skipped {covered} covered, {faded} faded or inactive), lowest against its target {worstWhat}" +
                (low.Count > 0 ? $"; {low.Count} below: " + string.Join("; ", shown) : ""));
        }

        /// <summary>
        /// Reduce motion, compared with it off: the title letters stop bobbing, the settings card fades in
        /// without sliding or scaling, the paper-wipe sheet stays put and fades, the camera starts a trip
        /// already in place, CLOSE THE TRUNK doesn't pulse, and the slam doesn't shake the camera.
        /// </summary>
        IEnumerator ReduceMotionChecks(Dictionary<string, List<(string, List<Vector3Int>)>> solutions)
        {
            if (!solutions.TryGetValue("wagon", out var wagon)) { Check(false, "motion: the wagon has a solution"); yield break; }
            var results = new Dictionary<bool, (bool Bob, bool Intro, bool Sheet, bool Sweep, bool Pulse, bool Shake)>();
            foreach (bool reduce in new[] { false, true })
            {
                GameSettings.ReduceMotion = reduce;
                Uncap();
                PerfProbe.Begin("menus");
                game.AutoShowMainMenu();
                yield return Wait(2f);
                var bobs = FindObjectsByType<Bob>(FindObjectsInactive.Exclude).Select(b => (RectTransform)b.transform).ToList();
                var before = bobs.Select(b => b.anchoredPosition).ToList();
                yield return Wait(0.4f);
                bool bobMoved = bobs.Where((b, i) => (b.anchoredPosition - before[i]).magnitude > 0.5f).Any();

                yield return ClickUi("Settings");
                yield return null;
                var panel = FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude).First(r => r.name == "Panel" && r.parent != null && r.parent.name == "Settings");
                var group = panel.GetComponent<CanvasGroup>();
                Vector2 early = panel.anchoredPosition;
                float earlyScale = panel.localScale.x, earlyAlpha = group != null ? group.alpha : 1f;
                yield return Wait(1f);
                bool introMoved = (panel.anchoredPosition - early).magnitude > 1f || Mathf.Abs(panel.localScale.x - earlyScale) > 0.01f;
                bool introFaded = earlyAlpha < 0.99f;
                yield return Press(Key.Escape);
                yield return Wait(0.6f);

                game.AutoTransitionTrip(LevelIndex("wagon"));
                float minX = 0f, maxX = 0f, minAlpha = 1f;
                for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime)
                {
                    if (game.Ui.InTransition)
                    {
                        minX = Mathf.Min(minX, game.Ui.CurtainSheetX);
                        maxX = Mathf.Max(maxX, game.Ui.CurtainSheetX);
                        minAlpha = Mathf.Min(minAlpha, game.Ui.CurtainAlpha);
                    }
                    yield return null;
                }
                bool sheetMoved = maxX - minX > 1f;

                PerfProbe.Begin("playing");
                game.AutoStartLevel(LevelIndex("wagon"));
                yield return null;
                yield return null;
                bool swept = !game.Rig.Settled;
                yield return Wait(1.5f);
                yield return PackAll("wagon", wagon);
                yield return Wait(0.3f);
                float lo = 1f, hi = 1f;
                for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
                {
                    lo = Mathf.Min(lo, game.Ui.ClosePulseScale);
                    hi = Mathf.Max(hi, game.Ui.ClosePulseScale);
                    yield return null;
                }
                bool pulsed = hi - lo > 0.01f;
                yield return Press(Key.Space);
                bool shook = false;
                for (float t = 0f; t < 6f && !game.IsShowingResults; t += Time.unscaledDeltaTime)
                {
                    shook |= game.Rig.IsShaking;
                    yield return null;
                }
                yield return Wait(1.2f);
                results[reduce] = (bobMoved, introMoved, sheetMoved, swept, pulsed, shook);
                if (reduce) Check(introFaded && minAlpha < 0.5f, $"motion: with reduce motion the settings card and the scene change still fade (card alpha {earlyAlpha:0.00} at first, sheet alpha down to {minAlpha:0.00})");
            }
            var off = results[false];
            var on = results[true];
            Check(off.Bob && off.Intro && off.Sheet && off.Sweep && off.Pulse && off.Shake,
                $"motion: normally the letters bob ({off.Bob}), the card slides in ({off.Intro}), the wipe moves ({off.Sheet}), the camera sweeps in ({off.Sweep}), CLOSE pulses ({off.Pulse}) and the slam shakes ({off.Shake})");
            Check(!on.Bob && !on.Intro && !on.Sheet && !on.Sweep && !on.Pulse && !on.Shake,
                $"motion: with reduce motion none of that moves (bob {on.Bob}, slide {on.Intro}, wipe {on.Sheet}, sweep {on.Sweep}, pulse {on.Pulse}, shake {on.Shake})");
            GameSettings.ReduceMotion = false;
            Uncap();
        }

        IEnumerator OpenControls()
        {
            yield return Press(Key.Escape);
            yield return Wait(1f);
            yield return ClickUi("Pause Settings");
            yield return Wait(1f);
            yield return ClickUi("Tab CONTROLS");
            yield return Wait(0.8f);
        }

        IEnumerator CloseControlsAndResume()
        {
            yield return Press(Key.Escape);
            yield return Wait(0.6f);
            yield return Press(Key.Escape);
            yield return Wait(0.8f);
        }

        /// <summary>
        /// Remap keys through Settings → Controls like a player: turn → G, Escape cancels a rebind,
        /// G then turns (and R doesn't), binding G to undo swaps the two, and Defaults restores all.
        /// </summary>
        IEnumerator RebindChecks()
        {
            PerfProbe.Begin("menus");
            game.AutoStartLevel(LevelIndex("weekend"));
            yield return Wait(2.5f);
            yield return OpenControls();
            Check(Visible("Bind Turn"), "remap: Settings → Controls lists the keys");
            yield return ClickUi("Bind Turn");
            yield return Wait(0.3f);
            Check(game.Ui.IsRebinding, "remap: clicking a key waits for a new one");
            yield return Press(Key.G);
            yield return Wait(0.3f);
            Check(Bindings.KeyFor(Bindings.Action.Turn) == Key.G && !game.Ui.IsRebinding, "remap: pressing G binds turn to G");
            yield return Shot("controls-rebound");
            yield return ClickUi("Bind Undo");
            yield return Wait(0.3f);
            yield return Press(Key.Escape);
            yield return Wait(0.5f);
            Check(!game.Ui.IsRebinding && Bindings.KeyFor(Bindings.Action.Undo) == Key.Z && Visible("Settings Back"),
                "remap: Escape cancels a rebind and leaves Settings open");
            yield return CloseControlsAndResume();
            Check(game.IsPlaying && !game.IsPaused, "remap: back to packing");

            var item = game.Items.First(i => i.State == ItemState.Pile && i.Def.Shape.Orientations().Count > 1);
            game.AutoHold(item);
            yield return Wait(0.3f);
            var before = item.Orientation;
            yield return Press(Key.R);
            yield return Wait(0.3f);
            bool rIgnored = item.Orientation == before;
            yield return Press(Key.G);
            yield return Wait(0.3f);
            Check(rIgnored && item.Orientation != before, "remap: G turns the held item and R no longer does");
            var hintKeys = game.Ui.KeyboardHintKeys();
            Check(hintKeys.Contains("G") && !hintKeys.Contains("R"), "remap: the key hints show G (" + string.Join(" ", hintKeys) + ")");
            yield return Press(Key.Escape);
            yield return Wait(0.4f);

            yield return OpenControls();
            yield return ClickUi("Bind Undo");
            yield return Wait(0.3f);
            yield return Press(Key.G);
            yield return Wait(0.3f);
            Check(Bindings.KeyFor(Bindings.Action.Undo) == Key.G && Bindings.KeyFor(Bindings.Action.Turn) == Key.Z,
                "remap: binding G to undo swaps it with turn (turn is now Z)");
            yield return ClickUi("Settings Defaults");
            yield return Wait(0.6f);
            yield return ClickUi("Confirm Yes");
            yield return Wait(0.8f);
            Check(Bindings.All.All(a => Bindings.KeyFor(a) == DefaultKey(a)), "remap: Defaults restores every key");
            yield return CloseControlsAndResume();
        }

        static Key DefaultKey(Bindings.Action a) => a switch
        {
            Bindings.Action.Turn => Key.R, Bindings.Action.Tip => Key.T, Bindings.Action.Roll => Key.F,
            Bindings.Action.ShelfUp => Key.W, Bindings.Action.ShelfDown => Key.S, Bindings.Action.Undo => Key.Z,
            Bindings.Action.Hint => Key.H, Bindings.Action.XRay => Key.Tab, Bindings.Action.LookLeft => Key.Q,
            Bindings.Action.LookRight => Key.E, Bindings.Action.Close => Key.Space, _ => Key.M,
        };

        /// <summary>Do exactly what Grandpa says (undoing when he says so) until everything is packed.</summary>
        int followSteps;
        string followStuck;

        // Paced like the main packing loop, so the landing sounds don't all pile into one frame.
        IEnumerator FollowHints()
        {
            followStuck = null;
            for (followSteps = 0; followSteps < 120 && !game.Items.All(i => i.State == ItemState.Packed || i.State == ItemState.Dropping); followSteps++)
            {
                var hint = game.AutoFindHint();
                if (hint.Item == null)
                {
                    if (!game.AutoUndo()) { followStuck = hint.Message; yield break; }
                }
                else if (!game.AutoPlace(hint.Item, hint.Rotation, hint.Pos))
                {
                    followStuck = $"could not place {hint.Item.Def.Id} at {hint.Pos}";
                    yield break;
                }
                yield return Wait(0.03f);
            }
        }

        /// <summary>
        /// Uncapped, so the frame times show real headroom rather than the display's refresh. Changing
        /// any setting re-applies V-Sync, so call this again after touching GameSettings: with V-Sync on,
        /// a covered window on Wayland gets throttled to ~11 fps and skews every later frame time.
        /// </summary>
        static void Uncap()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        /// <summary>
        /// Play with a (simulated) gamepad: point with the stick, A to pick up and drop, X / Y / RB to
        /// turn, View to undo, D-pad left for a hint, Menu to pause, A on a menu button, B to resume,
        /// and the key hints switch to controller buttons and back when the mouse moves again.
        /// There's no physical controller on the test machine, so this is the only gamepad test.
        /// </summary>
        IEnumerator GamepadChecks()
        {
            var realMouse = Mouse.current;
            var pad = InputSystem.AddDevice<Gamepad>("AutoPilot Pad");
            PerfProbe.Begin("playing");
            game.AutoStartLevel(LevelIndex("weekend"));
            yield return Wait(2.5f);

            var item = game.Items.First(i => i.Def.Id == "duffel");
            yield return PadStickTo(pad, game.Camera.WorldToScreenPoint(item.transform.position + (Vector3)item.Shape.Center));
            Check(GamepadCursor.Active && !Cursor.visible, "gamepad: the stick brings up the gamepad cursor");
            Check(game.Ui.PadHintsShown, "gamepad: the key hints switch to controller buttons");
            yield return PadPress(pad, GamepadButton.South);
            Check(game.Held == item, "gamepad: A picks up what the cursor is on");
            var o = item.Orientation;
            yield return PadPress(pad, GamepadButton.West);
            bool turned = item.Orientation != o;
            o = item.Orientation;
            yield return PadPress(pad, GamepadButton.North);
            bool tipped = item.Orientation != o;
            o = item.Orientation;
            yield return PadPress(pad, GamepadButton.RightShoulder);
            Check(turned && tipped && item.Orientation != o, "gamepad: X, Y and RB turn, tip and roll it");
            yield return Shot("gamepad-holding");

            var trunk = game.CurrentVehicle.transform;
            bool found = false;
            for (int x = 0; x < game.TrunkSize.x && !found; x++)
            for (int z = 0; z < game.TrunkSize.z && !found; z++)
            {
                yield return PadStickTo(pad, game.Camera.WorldToScreenPoint(trunk.TransformPoint(new Vector3(x + 0.5f, 0f, z + 0.5f))));
                yield return Wait(0.1f);
                found = game.HasValidTarget;
            }
            yield return PadPress(pad, GamepadButton.South);
            yield return Wait(0.8f);
            Check(found && item.State == ItemState.Packed, "gamepad: A drops it into the trunk");
            yield return PadPress(pad, GamepadButton.Select);
            yield return Wait(0.6f);
            Check(item.State == ItemState.Pile, "gamepad: View undoes");
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder).WithButton(GamepadButton.Select));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            yield return Wait(0.8f);
            Check(item.State == ItemState.Packed, "gamepad: LB + View redoes");
            yield return PadPress(pad, GamepadButton.Select);
            yield return Wait(0.6f);
            yield return PadPress(pad, GamepadButton.DpadLeft);
            yield return Wait(0.4f);
            Check(game.HintItem != null, "gamepad: D-pad left asks Grandpa");

            // Toggled X-ray: one click of the left stick turns it on (no holding the stick in while
            // pointing with it), the next turns it off.
            GameSettings.XRayToggle = true;
            Uncap();
            yield return PadPress(pad, GamepadButton.LeftStick);
            yield return Wait(0.4f);
            bool padOn = game.XRayActive && game.Ui.XRayOnShown && game.Ui.PadHintCaptions().Contains("x-ray on");
            yield return PadPress(pad, GamepadButton.LeftStick);
            yield return Wait(0.4f);
            Check(padOn && !game.XRayActive && !game.Ui.XRayOnShown, "gamepad: in toggle mode one L3 click turns X-ray on (the hint says so) and the next turns it off");
            GameSettings.XRayToggle = false;
            Uncap();

            yield return PadPress(pad, GamepadButton.Start);
            yield return Wait(1f);
            Check(game.IsPaused, "gamepad: Menu pauses");
            yield return Shot("gamepad-pause");
            var resume = FindButton("Resume");
            if (resume != null)
            {
                var rt = (RectTransform)resume.transform;
                yield return PadStickTo(pad, rt.TransformPoint(rt.rect.center));
                yield return PadPress(pad, GamepadButton.South);
                yield return Wait(0.6f);
            }
            Check(!game.IsPaused, "gamepad: A on RESUME (a menu button) resumes");
            yield return PadPress(pad, GamepadButton.Start);
            yield return Wait(0.8f);
            yield return PadPress(pad, GamepadButton.East);
            yield return Wait(0.6f);
            Check(!game.IsPaused, "gamepad: B backs out of the pause menu");
            yield return PadMenuChecks(pad);
            yield return PadRebindChecks(pad);

            // Touch the real mouse again: control and the hints go back to mouse and keyboard.
            var p = realMouse.position.ReadValue();
            InputSystem.QueueStateEvent(realMouse, new MouseState { position = p + new Vector2(40, 0), delta = new Vector2(40, 0) });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(realMouse, new MouseState { position = p + new Vector2(40, 0) });
            yield return Wait(0.3f);
            Check(!GamepadCursor.Active && Cursor.visible && !game.Ui.PadHintsShown && Mouse.current == realMouse,
                "gamepad: moving the mouse hands control back");
            InputSystem.RemoveDevice(pad);
        }

        /// <summary>
        /// D-pad menu navigation: while packing the D-pad keeps its jobs and the cursor stays put; in the pause
        /// menu, the main menu, Settings and the album it jumps the cursor from button to button and A clicks;
        /// on a settings slider left / right change the value. Then the album close-up's D-pad and bumper
        /// flipping (owed since round 6).
        /// </summary>
        IEnumerator PadMenuChecks(Gamepad pad)
        {
            var visited = new List<string>();
            IEnumerator Nav(GamepadButton direction)
            {
                yield return PadPress(pad, direction);
                yield return Wait(0.25f);
                visited.Add(GamepadCursor.LastNavigation);
            }
            // Keep pressing one way, then the next, until the cursor lands on that control.
            IEnumerator NavTo(string name, params GamepadButton[] directions)
            {
                foreach (var direction in directions)
                    for (int i = 0; i < 8 && GamepadCursor.LastNavigation != name; i++) yield return Nav(direction);
            }
            bool CursorOn(string name)
            {
                var b = FindButton(name);
                if (b == null) return false;
                var rt = (RectTransform)b.transform;
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                var p = GamepadCursor.Position;
                return p.x >= corners[0].x && p.x <= corners[2].x && p.y >= corners[0].y && p.y <= corners[2].y;
            }

            var before = GamepadCursor.Position;
            yield return PadPress(pad, GamepadButton.DpadUp);
            yield return Wait(0.3f);
            Check(game.IsPlaying && !game.IsPaused && (GamepadCursor.Position - before).magnitude < 1f,
                "gamepad menus: while packing, the D-pad doesn't move the cursor");

            yield return PadPress(pad, GamepadButton.Start);
            yield return Wait(1f);
            visited.Clear();
            yield return Nav(GamepadButton.DpadDown);
            yield return Nav(GamepadButton.DpadDown);
            yield return NavTo("Resume", GamepadButton.DpadUp, GamepadButton.DpadDown);
            bool onResume = CursorOn("Resume");
            yield return PadPress(pad, GamepadButton.South);
            yield return Wait(0.6f);
            Check(onResume && !game.IsPaused, $"gamepad menus: the D-pad walks the pause menu ({string.Join(" > ", visited)}) and A on RESUME resumes");

            game.AutoShowMainMenu();
            yield return Wait(2f);
            visited.Clear();
            yield return NavTo("Settings", GamepadButton.DpadDown, GamepadButton.DpadUp);
            bool onSettings = CursorOn("Settings");
            yield return Shot("gamepad-dpad-menu");
            yield return PadPress(pad, GamepadButton.South);
            yield return Wait(1.2f);
            Check(onSettings && Visible("Settings Back"), $"gamepad menus: D-pad down walks the main menu ({string.Join(" > ", visited)}) and A on SETTINGS opens it");

            visited.Clear();
            yield return NavTo("Tab AUDIO", GamepadButton.DpadUp, GamepadButton.DpadLeft);
            yield return PadPress(pad, GamepadButton.South);
            yield return Wait(0.8f);
            float master = GameSettings.Master;
            yield return Nav(GamepadButton.DpadDown);
            bool onSlider = GamepadCursor.LastNavigation == "Slider";
            yield return Nav(GamepadButton.DpadRight);
            float raised = GameSettings.Master;
            yield return Nav(GamepadButton.DpadLeft);
            float lowered = GameSettings.Master;
            Check(onSlider && raised > master + 0.01f && Mathf.Abs(lowered - master) < 0.011f,
                $"gamepad menus: on the master volume slider D-pad right raises it ({master:0.00} > {raised:0.00}) and left lowers it ({lowered:0.00}); {string.Join(" > ", visited)}");
            GameSettings.Master = master;
            Uncap();
            yield return PadPress(pad, GamepadButton.East);
            yield return Wait(0.8f);
            Check(!Visible("Settings Back"), "gamepad menus: B closes Settings");

            game.AutoShowMenuAlbum();
            yield return WaitForAlbum();
            var withPhoto = GameDatabase.Levels.Where(l => FindButton("Polaroid " + l.Id) != null).ToList();
            if (withPhoto.Count < 2) { Check(false, $"gamepad album: at least two polaroids have photos ({withPhoto.Count})"); yield break; }
            visited.Clear();
            foreach (var direction in new[] { GamepadButton.DpadUp, GamepadButton.DpadLeft, GamepadButton.DpadDown })
                for (int i = 0; i < 8 && !GamepadCursor.LastNavigation.StartsWith("Polaroid "); i++) yield return Nav(direction);
            string landed = GamepadCursor.LastNavigation;
            yield return PadPress(pad, GamepadButton.South);
            yield return Wait(0.6f);
            int at = withPhoto.FindIndex(l => l.Id == game.Ui.AlbumZoomTrip);
            Check(landed.StartsWith("Polaroid ") && at >= 0 && "Polaroid " + game.Ui.AlbumZoomTrip == landed,
                $"gamepad album: the D-pad reaches a polaroid ({landed}) and A opens its close-up");
            if (at < 0) yield break;
            string Step(int k) => withPhoto[(at + k + withPhoto.Count) % withPhoto.Count].Id;
            yield return PadPress(pad, GamepadButton.DpadRight);
            yield return Wait(0.3f);
            bool right = game.Ui.AlbumZoomTrip == Step(1);
            yield return PadPress(pad, GamepadButton.RightShoulder);
            yield return Wait(0.3f);
            bool rb = game.Ui.AlbumZoomTrip == Step(2);
            yield return PadPress(pad, GamepadButton.LeftShoulder);
            yield return Wait(0.3f);
            bool lb = game.Ui.AlbumZoomTrip == Step(1);
            yield return PadPress(pad, GamepadButton.DpadLeft);
            yield return Wait(0.3f);
            bool left = game.Ui.AlbumZoomTrip == Step(0);
            Check(right && rb && lb && left, $"gamepad album: in the close-up D-pad right / RB go forward and LB / D-pad left go back (right {right}, RB {rb}, LB {lb}, left {left})");
            yield return PadPress(pad, GamepadButton.East);
            yield return Wait(0.5f);
            Check(!game.Ui.IsAlbumZoomOpen && game.Ui.IsAlbumOpen, "gamepad album: B closes the close-up, the album stays open");
            yield return PadPress(pad, GamepadButton.East);
            yield return Wait(1f);
        }

        /// <summary>
        /// The [Input] log lines a real controller test relies on: a gamepad coming and going is logged, and
        /// a device Unity only knows as a generic joystick is called out as unsupported (with a toast).
        /// </summary>
        IEnumerator InputReportChecks()
        {
            // The toast needs a trip in progress (the section before may end on a postcard or the title).
            PerfProbe.Begin("playing");
            game.AutoStartLevel(LevelIndex("weekend"));
            yield return Wait(2.5f);
            var pad = InputSystem.AddDevice<Gamepad>("Report Test Pad");
            yield return null;
            string added = InputReport.LastLine;
            InputSystem.RemoveDevice(pad);
            yield return null;
            string removed = InputReport.LastLine;
            Check(added.StartsWith("[Input] added Gamepad") && removed.StartsWith("[Input] removed Gamepad"), $"input: a gamepad coming and going is logged ({added})");
            var stick = InputSystem.AddDevice<Joystick>("Report Test Stick");
            yield return Wait(0.4f);
            Check(game.IsPlaying && InputReport.LastLine.Contains("generic joystick") && AnyText("isn't a gamepad the game understands"),
                "input: a generic joystick is logged as unsupported, and a toast says what to try");
            yield return Shot("input-unsupported-joystick");
            InputSystem.RemoveDevice(stick);
            yield return Wait(0.2f);
        }

        IEnumerator PadPress(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            yield return Wait(0.1f);
        }

        /// <summary>Steer the gamepad cursor onto a screen point with the left stick.</summary>
        IEnumerator PadStickTo(Gamepad pad, Vector3 screen)
        {
            var target = new Vector2(screen.x, screen.y);
            for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
            {
                var d = target - GamepadCursor.Position;
                if (d.magnitude < 10f && GamepadCursor.Active) break;
                var stick = d.normalized * Mathf.Clamp(d.magnitude / 300f, 0.3f, 1f);
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = stick });
                yield return null;
            }
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            yield return null;
        }

        static int LevelIndex(string id)
        {
            for (int i = 0; i < GameDatabase.Levels.Count; i++)
                if (GameDatabase.Levels[i].Id == id) return i;
            return -1;
        }

        static bool AnyText(string fragment) =>
            FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Exclude).Any(t => t.text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Find the orientation of the item's authored shape that produces the solver's cells.</summary>
        internal static Quaternion FindOrientation(VoxelShape baseShape, List<Vector3Int> cells)
        {
            var min = cells.Aggregate(Vector3Int.Min);
            var want = new HashSet<Vector3Int>(cells.Select(c => c - min));
            var turns = new[] { Quaternion.AngleAxis(90, Vector3.up), Quaternion.AngleAxis(90, Vector3.right), Quaternion.AngleAxis(90, Vector3.forward) };
            var queue = new Queue<Quaternion>();
            var seen = new HashSet<string>();
            queue.Enqueue(Quaternion.identity);
            while (queue.Count > 0)
            {
                var q = queue.Dequeue();
                var s = baseShape.Rotated(q);
                string key = string.Join(";", s.Voxels.Select(v => v.Pos.ToString()).OrderBy(x => x));
                if (!seen.Add(key)) continue;
                if (s.Voxels.Length == want.Count && s.Voxels.All(v => want.Contains(v.Pos))) return q;
                foreach (var t in turns) queue.Enqueue(t * q);
            }
            return Quaternion.identity;
        }

        Dictionary<string, List<(string, List<Vector3Int>)>> LoadSolutions() => LoadSolutions(solutionsPath);

        internal static Dictionary<string, List<(string, List<Vector3Int>)>> LoadSolutions(string solutionsPath) =>
            string.IsNullOrEmpty(solutionsPath) || !File.Exists(solutionsPath)
                ? new Dictionary<string, List<(string, List<Vector3Int>)>>()
                : Solutions.Parse(File.ReadAllText(solutionsPath));

        /// <summary>The neighbours page: ASK SOMEONE ELSE inside the page and clear of the pin, its card and the note (in the page's own space).</summary>
        void NeighboursPageLayoutCheck(string size)
        {
            var swap = FindButton("Favour Swap");
            var area = swap != null ? swap.transform.parent as RectTransform : null;
            Rect Local(Transform t)
            {
                var corners = new Vector3[4];
                ((RectTransform)t).GetWorldCorners(corners);
                var a = area.InverseTransformPoint(corners[0]);
                var b = area.InverseTransformPoint(corners[2]);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }
            var problems = new List<string>();
            if (area == null) problems.Add("no ASK SOMEONE ELSE button");
            else
            {
                var r = Local(swap.transform);
                var page = area.rect;
                if (r.xMin < page.xMin - 2f || r.xMax > page.xMax + 2f || r.yMin < page.yMin - 2f || r.yMax > page.yMax + 2f) problems.Add("it leaves the page");
                foreach (var name in new[] { "Favour Slot", "Label", "Favours Note" })
                {
                    var other = area.Find(name);
                    if (other == null) { problems.Add("no " + name); continue; }
                    var o = Local(other);
                    float w = Mathf.Min(r.xMax, o.xMax) - Mathf.Max(r.xMin, o.xMin), h = Mathf.Min(r.yMax, o.yMax) - Mathf.Max(r.yMin, o.yMin);
                    if (w > 2f && h > 2f) problems.Add($"it overlaps {name} by {w:0}x{h:0}");
                }
            }
            Check(problems.Count == 0, $"neighbours page at {size}, {GameSettings.UiScale * 100f:0}%: ASK SOMEONE ELSE sits inside the page, clear of the pin, its card and the note" +
                (problems.Count > 0 ? ": " + string.Join("; ", problems) : ""));
        }

        static UnityEngine.UI.Button FindButton(string name) =>
            FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude).FirstOrDefault(b => b.name == name);

        static bool Visible(string name) => FindButton(name) != null;

        IEnumerator ClickUi(string name)
        {
            var button = FindButton(name);
            if (button == null)
            {
                Check(false, "button " + name + " is on screen");
                yield break;
            }
            var rt = (RectTransform)button.transform;
            Vector2 screen = rt.TransformPoint(rt.rect.center);
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = screen });
            yield return null;
            yield return null;
            yield return Click();
        }

        IEnumerator MoveMouse(Vector3 world)
        {
            var screen = game.Camera.WorldToScreenPoint(world);
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(screen.x, screen.y) });
            yield return null;
            yield return null;
        }

        IEnumerator Click()
        {
            var pos = Mouse.current.position.ReadValue();
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = pos }.WithButton(MouseButton.Left, true));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = pos });
            yield return null;
        }

        IEnumerator Press(Key key, params Key[] held)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(held.Append(key).ToArray()));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
        }

        static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        IEnumerator Shot(string name)
        {
            var path = Path.Combine(outDir, $"{++shot:00}-{name}.png");
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            PerfProbe.Ignore();
            Log("screenshot " + path);
        }

        static void Check(bool ok, string what) => Log((ok ? "PASS " : "FAIL ") + what);

        static void Log(string message) => Debug.Log("[AutoPilot] " + message);
    }
}
