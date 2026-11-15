using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Menu: After Hours > Get Latest Update.
    /// Runs "git pull" in the project folder, then tells Unity to reload the changed files.
    /// </summary>
    public static class UpdateFromGitHub
    {
        [MenuItem("After Hours/Get Latest Update", priority = 100)]
        public static void GetLatest()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("After Hours", "Stop Play mode first, then try again.", "OK");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            if (!Directory.Exists(Path.Combine(projectRoot, ".git")))
            {
                EditorUtility.DisplayDialog("After Hours",
                    "This project is not connected to GitHub yet.\nFollow the setup steps in README.md first.", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("After Hours", "Downloading the latest update...", 0.5f);
            string output;
            int exitCode;
            try
            {
                exitCode = RunGit(projectRoot, "pull --ff-only", out output);
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("After Hours", "Could not run git:\n" + e.Message + "\n\nIs git installed?", "OK");
                return;
            }
            EditorUtility.ClearProgressBar();

            if (exitCode != 0)
            {
                Debug.LogError("[AfterHours] git pull failed:\n" + output);
                EditorUtility.DisplayDialog("After Hours",
                    "The update failed. The details are in the Console (Window > General > Console) - copy them to Claude.", "OK");
                return;
            }

            AssetDatabase.Refresh();
            bool nothingNew = output.Contains("Already up to date") || output.Contains("Already up-to-date");
            Debug.Log("[AfterHours] Update:\n" + output);
            EditorUtility.DisplayDialog("After Hours",
                nothingNew ? "You already have the latest version." : "Update downloaded. Unity will now reload the changed files.",
                "OK");
        }

        static int RunGit(string workingDirectory, string arguments, out string output)
        {
            var info = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            // Never hang waiting for a password prompt inside Unity.
            info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

            using (Process p = Process.Start(info))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(120000))
                {
                    try { p.Kill(); } catch (Exception) { }
                    output = "git timed out.\n" + stdout + stderr;
                    return -1;
                }
                output = (stdout + "\n" + stderr).Trim();
                return p.ExitCode;
            }
        }
    }
}
