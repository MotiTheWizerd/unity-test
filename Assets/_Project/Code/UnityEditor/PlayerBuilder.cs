using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Forge.UnityEditor
{
    /// <summary>
    /// Dev tool: builds Forge as a Windows player (Builds/Windows/Forge.exe, git-ignored) from the scenes enabled in
    /// the build settings, and runs it: Forge as a player sees it, fullscreen at the screen's resolution, with no Unity
    /// around it. The player reads the same base plugins (StreamingAssets) and the same user plugin
    /// (persistent data, by company and product name) as Play mode.
    /// </summary>
    static class PlayerBuilder
    {
        const string Folder = "Builds/Windows";
        const string Executable = "Forge.exe";

        [MenuItem("Forge/Build and Run (Windows)")]
        static void BuildAndRun() => Build(BuildOptions.AutoRunPlayer);

        [MenuItem("Forge/Build (Windows)")]
        static void BuildOnly() => Build(BuildOptions.None);

        static void Build(BuildOptions options)
        {
            // A running player locks its files, and the build would stall writing over them.
            if (System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Executable)).Length > 0)
            {
                EditorUtility.DisplayDialog("Forge is running", $"Close {Executable} first, then build again.", "OK");
                return;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Forge] No scene is enabled in the build settings; nothing to build.");
                return;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(Folder, Executable),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = options,
            });

            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"[Forge] Built {summary.outputPath} ({summary.totalSize / (1024.0 * 1024.0):0} MB) in {summary.totalTime.TotalSeconds:0} s.");
            else
                Debug.LogError($"[Forge] Build {summary.result}: {summary.totalErrors} error(s); see the console above.");
        }
    }
}
