using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Makes a playable copy of the game (no Unity needed to play it) for Windows, macOS and Linux.
    /// Each platform needs its "Build Support" module installed in Unity Hub (Installs > ... > Add modules).
    /// Builds go into the project's "Builds" folder.
    /// </summary>
    public static class GameBuilds
    {
        [MenuItem("After Hours/8 - Make Game Builds/Linux", priority = 80)]
        public static void Linux() => Build(BuildTarget.StandaloneLinux64, "Linux/AfterHours.x86_64");

        [MenuItem("After Hours/8 - Make Game Builds/Windows", priority = 81)]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Windows/AfterHours.exe");

        [MenuItem("After Hours/8 - Make Game Builds/macOS", priority = 82)]
        public static void Mac() => Build(BuildTarget.StandaloneOSX, "macOS/AfterHours.app");

        [MenuItem("After Hours/8 - Make Game Builds/All three", priority = 83)]
        public static void All()
        {
            Linux();
            Windows();
            Mac();
        }

        static void Build(BuildTarget target, string relative)
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("After Hours", "Stop Play mode first.", "OK"); return; }
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
            {
                EditorUtility.DisplayDialog("After Hours",
                    $"Unity can't make {target} builds yet.\n\nOpen Unity Hub > Installs > the gear next to your Unity version > Add modules, " +
                    $"tick the {Friendly(target)} Build Support module, install it, restart Unity and try again.", "OK");
                return;
            }
            if (EditorBuildSettings.scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("After Hours", "Build the mall first (After Hours > 3 - Build Mall).", "OK");
                return;
            }
            PlayerSettings.productName = "AFTER HOURS";
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var scenes = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = path,
                target = target,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AfterHours] {target} build finished: {path} ({report.summary.totalSize / (1024 * 1024)} MB)");
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                EditorUtility.DisplayDialog("After Hours", $"The {Friendly(target)} build failed. The red lines in the Console say why.", "OK");
            }
        }

        static string Friendly(BuildTarget t) => t == BuildTarget.StandaloneOSX ? "Mac" : t == BuildTarget.StandaloneWindows64 ? "Windows" : "Linux";
    }
}
