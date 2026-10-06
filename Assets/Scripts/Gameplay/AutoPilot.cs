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
    public class AutoPilot : MonoBehaviour
    {
        string outDir;
        string solutionsPath;
        GameController game;
        int shot;
        bool quick, layoutOnly;

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
                Log("done");
                Application.Quit();
                yield break;
            }
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
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
            foreach (var tab in new[] { "DISPLAY", "GRAPHICS", "GAMEPLAY", "CONTROLS" })
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
            yield return TipChecks(solutions);
            yield return HintChecks();
            yield return RestartChecks(solutions);
            yield return StarMeterChecks(solutions);
            yield return SeeThroughChecks();
            yield return RebindChecks();
            yield return LayoutChecks();
            // Last: once the gamepad has been used, Mouse.current is its virtual cursor.
            yield return GamepadChecks();

            // The finale: Grandma's note, then the family album.
            PerfProbe.Begin("ending + album");
            game.AutoShowEnding();
            yield return Wait(7f);
            yield return Shot("ending-note");
            game.AutoShowAlbum();
            yield return Wait(12f);
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
            var photo = Path.Combine(Prefs.AlbumDir, "wagon.png");
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
        }

        /// <summary>
        /// The HUD at this screen shape, at 80%, 100% and 120% interface size, on the biggest trip with
        /// everything showing (held card, a tip, a toast): no two pieces overlap, nothing leaves the
        /// screen, and every packing-list row fits its area without overlapping the next.
        /// </summary>
        IEnumerator LayoutChecks()
        {
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
                Check(problems.Count == 0, $"layout {size} at {scale * 100:0}%: " + (problems.Count == 0 ? $"{names.Count} HUD pieces, {rows.Count} list rows of {smallest:0} units{(game.Ui.ListScrolls ? " (scrolling)" : "")}, counts line {-countsOver:0} units spare, star meter {meterGap:0} clear of the heading, all clear" : string.Join("; ", problems)));
                yield return Shot($"layout-{size}-{scale * 100:0}");
            }
            GameSettings.UiScale = 1f;
            Uncap();
            game.Ui.HideTip();
            game.AutoPutBack();
            yield return Wait(0.5f);
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
            yield return PadPress(pad, GamepadButton.DpadLeft);
            yield return Wait(0.4f);
            Check(game.HintItem != null, "gamepad: D-pad left asks Grandpa");

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

        IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
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
