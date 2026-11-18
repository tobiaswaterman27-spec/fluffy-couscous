using UnityEditor;
using UnityEngine;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Menu: After Hours > ★ Update and Build Everything.
    /// One click does it all: downloads the latest update, waits for Unity to reload the scripts, sets up the
    /// project, builds the whole mall (with Elliot, the players, items, escape routes, sound and lighting setup),
    /// then checks the mall and the escape-route progression - and says at the end how it went.
    /// It carries on by itself after Unity reloads the scripts.
    /// </summary>
    [InitializeOnLoad]
    public static class OneClickBuild
    {
        const string Key = "AfterHours.OneClick.Pending";

        /// <summary>While true, the individual steps don't show their own "done" boxes.</summary>
        public static bool Quiet;

        static OneClickBuild()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.delayCall += Continue;
        }

        [MenuItem("After Hours/★ Update and Build Everything", priority = 0)]
        public static void Start()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("After Hours", "Stop Play mode first (press the Play button again), then try again.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("After Hours",
                    "This downloads the latest update and rebuilds the whole game (mall, Elliot, players, everything).\n\n" +
                    "It takes several minutes and Unity may look frozen - the progress bar shows what it is doing.\n" +
                    "When it asks about saving the current scene, click Don't Save.", "Go", "Cancel"))
                return;

            EditorUtility.DisplayProgressBar("After Hours", "Downloading the latest update...", 0.1f);
            string err = UpdateFromGitHub.Pull(out bool nothingNew);
            EditorUtility.ClearProgressBar();
            if (err != null)
            {
                Debug.LogError("[AfterHours] " + err);
                if (!EditorUtility.DisplayDialog("After Hours", err + "\n\nBuild with what you have anyway?", "Build anyway", "Stop")) return;
            }
            SessionState.SetBool(Key, true);
            AssetDatabase.Refresh();
            // If scripts changed, Unity reloads them now and the static constructor above carries on afterwards.
            EditorApplication.delayCall += Continue;
        }

        static void Continue()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Continue;
                return;
            }
            SessionState.SetBool(Key, false);
            if (EditorUtility.scriptCompilationFailed)
            {
                EditorUtility.DisplayDialog("After Hours",
                    "The update has script errors, so nothing was built.\n\nOpen Window > General > Console, copy the red lines and send them to Claude.", "OK");
                return;
            }

            Quiet = true;
            bool built = false;
            try
            {
                ProjectSetup.EnsureLayers();
                if (PlayerSettings.productName != "After Hours") PlayerSettings.productName = "After Hours";
                MallBuilder.Build();
                built = UnityEngine.Object.FindAnyObjectByType<AfterHours.Flow.GameFlow>() != null;
                if (built)
                {
                    MallCheck.Run();
                    ProgressionCheck.Run();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                Quiet = false;
                EditorUtility.ClearProgressBar();
            }

            if (!built)
            {
                EditorUtility.DisplayDialog("After Hours",
                    "The build didn't finish. If there are red lines in Window > General > Console, copy them to Claude.", "OK");
                return;
            }
            bool clean = MallCheck.LastProblems == 0 && ProgressionCheck.LastOk;
            EditorUtility.DisplayDialog("After Hours",
                "Everything is updated and built.\n\n" +
                (clean ? "Checks: all fine." : "Checks found something - the details are in the Console (Window > General > Console). Send them to Claude.") +
                "\n\nPress Play to start.\n(For the final lighting, run After Hours > 5 - Bake Lighting when you have time - it's slow.)", "OK");
        }
    }
}
