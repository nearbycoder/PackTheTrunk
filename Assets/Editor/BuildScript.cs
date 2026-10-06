using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PackTheTrunk.EditorTools
{
    /// <summary>Menu items and batch-mode entry points for building players.</summary>
    public static class BuildScript
    {
        static readonly string[] Scenes = { "Assets/Scenes/Main.unity" };

        const string BundleId = "com.nearbycoder.packthetrunk";

        [MenuItem("Pack The Trunk/Build Linux Player")]
        public static void BuildLinux() =>
            Build(BuildTarget.StandaloneLinux64, "Builds/Linux/PackTheTrunk.x86_64");

        /// <summary>
        /// A universal (Apple Silicon + Intel) Mono .app. Unsigned and not notarized: macOS will
        /// quarantine it until the player right-clicks Open (or clears the quarantine attribute).
        /// </summary>
        [MenuItem("Pack The Trunk/Build macOS Player")]
        public static void BuildMac()
        {
            EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX), "Architecture", "x64ARM64");
            Build(BuildTarget.StandaloneOSX, "Builds/macOS/PackTheTrunk.app");
        }

        /// <summary>Needs Unity's Windows Build Support (Mono) module, which isn't installed on the development machine.</summary>
        [MenuItem("Pack The Trunk/Build Windows Player")]
        public static void BuildWindows() =>
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/PackTheTrunk.exe");

        [MenuItem("Pack The Trunk/Build WebGL Player")]
        public static void BuildWebGL() =>
            Build(BuildTarget.WebGL, "Builds/WebGL");

        static void Build(BuildTarget target, string path)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                Debug.LogError($"[PackTheTrunk] {target} build support isn't installed in this Unity editor (add the module in Unity Hub).");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (group == BuildTargetGroup.Standalone)
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);

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
