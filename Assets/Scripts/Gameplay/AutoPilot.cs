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
        bool quick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-pttAutopilot");
            if (i < 0 || i + 1 >= args.Length) return;
            var pilot = new GameObject("AutoPilot").AddComponent<AutoPilot>();
            pilot.outDir = args[i + 1];
            pilot.quick = Array.IndexOf(args, "-pttQuick") >= 0;
            int s = Array.IndexOf(args, "-pttSolutions");
            if (s >= 0 && s + 1 < args.Length) pilot.solutionsPath = args[s + 1];
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            PerfProbe.Attach();
            yield return null;
            // Uncapped, so the frame times show real headroom rather than the display's refresh.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
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

                PerfProbe.Begin("close + drive-off");
                yield return Press(Key.Space);
                for (float t = 0f; t < 9f && !game.IsShowingResults; t += Time.unscaledDeltaTime) yield return null;
                PerfProbe.Begin("results");
                yield return Wait(1.2f);
                Check(game.IsShowingResults, $"{def.Id}: closing the trunk shows the postcard");
                if (level == 0 || level == GameDatabase.Levels.Count - 1 || level % 8 == 4) yield return Shot(def.Id + "-results");

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

        internal static Dictionary<string, List<(string, List<Vector3Int>)>> LoadSolutions(string solutionsPath)
        {
            var result = new Dictionary<string, List<(string, List<Vector3Int>)>>();
            if (string.IsNullOrEmpty(solutionsPath) || !File.Exists(solutionsPath)) return result;
            foreach (var line in File.ReadAllLines(solutionsPath))
            {
                var parts = line.Split('\t');
                if (parts.Length != 3) continue;
                var cells = parts[2].Split(';').Select(c =>
                {
                    var n = c.Split(',').Select(int.Parse).ToArray();
                    return new Vector3Int(n[0], n[1], n[2]);
                }).ToList();
                if (!result.TryGetValue(parts[0], out var list)) result[parts[0]] = list = new List<(string, List<Vector3Int>)>();
                list.Add((parts[1], cells));
            }
            return result;
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
