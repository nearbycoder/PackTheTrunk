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
        }

        int preset = -1;

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
