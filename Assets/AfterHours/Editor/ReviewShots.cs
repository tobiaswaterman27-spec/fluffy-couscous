using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Menu: After Hours > 4 - Capture Review Shots.
    /// Renders the mall from fixed camera positions into the "ReviewShots" folder in the project, so the
    /// images can be sent to Claude to check layout and visuals. (In Play mode, F12 saves a screenshot too.)
    /// </summary>
    public static class ReviewShots
    {
        const int Width = 1600, Height = 900;

        [MenuItem("After Hours/4 - Capture Review Shots", priority = 4)]
        public static void Capture()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("After Hours", "Stop Play mode first (or press F12 in-game for a single screenshot).", "OK");
                return;
            }
            if (EditorSceneManager.GetActiveScene().path != MallBuilder.ScenePath)
            {
                if (!File.Exists(MallBuilder.ScenePath))
                {
                    EditorUtility.DisplayDialog("After Hours", "Build the mall first: After Hours > 3 - Build Mall.", "OK");
                    return;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(MallBuilder.ScenePath);
            }

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ReviewShots");
            Directory.CreateDirectory(folder);

            var camGo = new GameObject("Review Camera") { hideFlags = HideFlags.HideAndDontSave };
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 400f;
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            cam.targetTexture = rt;

            try
            {
                ReviewViewpoint[] views = MallLayout.ReviewViewpoints;
                for (int i = 0; i < views.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("After Hours", "Capturing " + views[i].name, i / (float)views.Length);
                    camGo.transform.position = views[i].position;
                    camGo.transform.LookAt(views[i].lookAt);
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(folder, views[i].name + ".png"), tex.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(camGo);
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.RevealInFinder(folder);
            EditorUtility.DisplayDialog("After Hours", "Saved " + MallLayout.ReviewViewpoints.Length + " images to:\n" + folder, "OK");
        }
    }
}
