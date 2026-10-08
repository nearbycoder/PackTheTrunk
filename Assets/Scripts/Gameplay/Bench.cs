using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Repeatable performance benchmark: <c>-pttBench [-pttBenchPreset n]</c> holds each screen of
    /// the game for a few seconds with an uncapped frame rate and logs per-screen frame times
    /// through <see cref="PerfProbe"/>, then quits.
    /// </summary>
    public class Bench : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-pttBench") < 0) return;
            var bench = new GameObject("Bench").AddComponent<Bench>();
            int p = Array.IndexOf(args, "-pttBenchPreset");
            if (p >= 0 && p + 1 < args.Length && int.TryParse(args[p + 1], out var preset)) bench.preset = preset;
            int f = Array.IndexOf(args, "-pttFidelity");
            if (f >= 0 && f + 1 < args.Length) bench.fidelityDir = args[f + 1];
        }

        int preset = -1;
        string fidelityDir;

        IEnumerator Start()
        {
            PerfProbe.Attach();
            yield return null;
            var game = FindAnyObjectByType<GameController>();
            int savedPreset = GameSettings.Preset;
            if (preset >= 0) GameSettings.Preset = preset;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Debug.Log($"[Bench] {SystemInfo.graphicsDeviceType} {Screen.width}x{Screen.height} preset {GameSettings.Presets[GameSettings.Preset]}");
            Debug.Log($"[Bench] renderer: {GameSettings.RenderDescription()}");
            if (fidelityDir != null)
            {
                yield return Fidelity(game, savedPreset);
                yield break;
            }

            yield return Hold("warm-up", 3f);
            yield return Hold("title", 4f);
            game.Ui.ShowTitle(false, "", "", "");
            yield return Hold("main menu", 4f);
            game.Ui.ShowSettings();
            yield return Hold("settings", 4f);
            game.Ui.HideSettings();
            game.AutoShowMenu();
            yield return Hold("trip map", 4f);
            game.AutoBeginTrip(0);
            yield return Hold("story", 4f);
            game.AutoStartLevel(0);
            yield return Hold("packing (wagon)", 4f);
            game.AutoPause();
            yield return Hold("pause", 4f);
            game.AutoResume();
            int biggest = Enumerable.Range(0, GameDatabase.Levels.Count)
                .OrderByDescending(i => GameDatabase.Levels[i].Required.Count + GameDatabase.Levels[i].Bonus.Count).First();
            game.AutoStartLevel(biggest);
            yield return Hold($"packing ({GameDatabase.Levels[biggest].Id})", 4f);
            // Half-packed (from the shipped solution) and holding the next thing over the trunk: the
            // ghost, the see-through check and the fragile stamps all run every frame.
            var solution = Solutions.For(GameDatabase.Levels[biggest]);
            if (solution.Count > 0)
            {
                var spots = solution[0];
                for (int i = 0; i < spots.Count / 2; i++)
                {
                    var spot = spots[i];
                    var piece = game.Items.FirstOrDefault(it => it.Def.Id == spot.Def.Id && it.State == ItemState.Pile);
                    if (piece != null) game.AutoPlace(piece, spot.Rotation, spot.Min);
                }
                var next = spots[spots.Count / 2];
                var held = game.Items.FirstOrDefault(it => it.Def.Id == next.Def.Id && it.State == ItemState.Pile);
                if (held != null)
                {
                    game.AutoHold(held);
                    var aim = game.Camera.WorldToScreenPoint(game.CurrentVehicle.transform.TransformPoint(next.Min + new Vector3(0.5f, 0f, 0.5f)));
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current,
                        new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(aim.x, aim.y) });
                }
                yield return Hold($"holding ({GameDatabase.Levels[biggest].Id}, half packed)", 4f);
                Debug.Log($"[Bench] holding {held?.Def.Id}: target {(game.CurrentTarget(out var at, out var ok) ? at + (ok ? " (fits)" : " (won't fit)") : "none")}");
                game.AutoPutBack();
            }
            game.AutoShowMenuAlbum();
            yield return Hold("album", 4f);
            game.Ui.ShowCredits();
            yield return Hold("credits", 4f);
            PerfProbe.Report();
            if (preset >= 0 && savedPreset < 4) GameSettings.Preset = savedPreset;
            GameSettings.Save();
            Debug.Log("[Bench] done");
            Application.Quit();
        }

        /// <summary>
        /// <c>-pttBench -pttFidelity dir</c>: the title and the biggest trip (half packed, holding the next
        /// thing over the trunk) at each Graphics fidelity step in one process, so the steps share the
        /// machine's load. Each step is measured for a few seconds, then all four are photographed back to
        /// back with motion reduced, so the screenshots show the same moment (after measuring, so the
        /// read-back stall never lands in the numbers).
        /// </summary>
        IEnumerator Fidelity(GameController game, int savedPreset)
        {
            System.IO.Directory.CreateDirectory(fidelityDir);
            yield return Hold("warm-up", 3f);
            yield return Steps("title");

            int biggest = Enumerable.Range(0, GameDatabase.Levels.Count)
                .OrderByDescending(i => GameDatabase.Levels[i].Required.Count + GameDatabase.Levels[i].Bonus.Count).First();
            var level = GameDatabase.Levels[biggest];
            game.AutoStartLevel(biggest);
            yield return new WaitForSecondsRealtime(2f);
            var spots = Solutions.For(level)[0];
            for (int i = 0; i < spots.Count / 2; i++)
            {
                var piece = game.Items.FirstOrDefault(it => it.Def.Id == spots[i].Def.Id && it.State == ItemState.Pile);
                if (piece != null) game.AutoPlace(piece, spots[i].Rotation, spots[i].Min);
            }
            var next = spots[spots.Count / 2];
            var held = game.Items.FirstOrDefault(it => it.Def.Id == next.Def.Id && it.State == ItemState.Pile);
            if (held != null)
            {
                game.AutoHold(held);
                var aim = game.Camera.WorldToScreenPoint(game.CurrentVehicle.transform.TransformPoint(next.Min + new Vector3(0.5f, 0f, 0.5f)));
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current,
                    new UnityEngine.InputSystem.LowLevel.MouseState { position = new Vector2(aim.x, aim.y) });
            }
            yield return Steps($"holding ({level.Id})");

            PerfProbe.Report();
            GameSettings.Preset = savedPreset < 4 ? savedPreset : 2;
            GameSettings.Save();
            Debug.Log("[Bench] done");
            Application.Quit();
        }

        IEnumerator Steps(string scene)
        {
            for (int step = 0; step < GameSettings.FidelitySteps.Length; step++)
            {
                SetStep(step);
                Debug.Log($"[Bench] {scene} @ {GameSettings.FidelitySteps[step]}: {GameSettings.RenderDescription()}");
                yield return Hold($"{scene} @ {GameSettings.FidelitySteps[step]}", 5f);
            }
            bool reduce = GameSettings.ReduceMotion;
            GameSettings.ReduceMotion = true;
            for (int step = 0; step < GameSettings.FidelitySteps.Length; step++)
            {
                SetStep(step);
                PerfProbe.Begin("(screenshots)");
                for (int k = 0; k < 4; k++) yield return null;
                yield return Shot($"{scene.Split(' ')[0]}-{step}-{GameSettings.FidelitySteps[step].ToLowerInvariant()}");
            }
            GameSettings.ReduceMotion = reduce;
            SetStep(2);
        }

        static void SetStep(int step)
        {
            GameSettings.Fidelity = step;
            // A settings change re-applies V-Sync; measure uncapped.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        IEnumerator Shot(string name)
        {
            var path = System.IO.Path.Combine(fidelityDir, name + ".png");
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            PerfProbe.Ignore();
            Debug.Log("[Bench] screenshot " + path);
        }

        static IEnumerator Hold(string phase, float seconds)
        {
            // Let transitions, intros and level builds settle before measuring.
            PerfProbe.Begin("(settle)");
            yield return new WaitForSecondsRealtime(1f);
            PerfProbe.Begin(phase);
            yield return new WaitForSecondsRealtime(seconds);
        }
    }
}
