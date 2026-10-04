using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PackTheTrunk.EditorTools
{
    /// <summary>Menu items and batch-mode entry points for building players.</summary>
    public static class BuildScript
    {
        static readonly string[] Scenes = { "Assets/Scenes/Main.unity" };

        [MenuItem("Pack The Trunk/Build Linux Player")]
        public static void BuildLinux() =>
            Build(BuildTarget.StandaloneLinux64, "Builds/Linux/PackTheTrunk.x86_64");

        [MenuItem("Pack The Trunk/Build WebGL Player")]
        public static void BuildWebGL() =>
            Build(BuildTarget.WebGL, "Builds/WebGL");

        static void Build(BuildTarget target, string path)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[PackTheTrunk] {target} build {summary.result}: {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors -> {path}");
            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
