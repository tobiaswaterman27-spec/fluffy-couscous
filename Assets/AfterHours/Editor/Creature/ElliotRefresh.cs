using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AfterHours.Creature;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Menu: After Hours > Update Elliot. Swaps every Elliot in the open scene for a freshly built one (new body, new
    /// rig, same place), so a new model can be tried without rebuilding the whole mall.
    /// </summary>
    public static class ElliotRefresh
    {
        [MenuItem("After Hours/Update Elliot", priority = 4)]
        public static void Refresh()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("After Hours", "Stop Play mode first, then try again.", "OK");
                return;
            }
            ElliotBrain[] all = Object.FindObjectsByType<ElliotBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all.Length == 0)
            {
                EditorUtility.DisplayDialog("After Hours", "There is no Elliot in this scene. Open the mall scene first (or run 3 - Build Mall).", "OK");
                return;
            }
            foreach (ElliotBrain b in all)
            {
                Transform t = b.transform;
                Transform parent = t.parent;
                Vector3 pos = t.position;
                float yaw = t.eulerAngles.y;
                string name = b.gameObject.name;
                int sibling = t.GetSiblingIndex();
                Object.DestroyImmediate(b.gameObject);
                GameObject fresh = ElliotFactory.CreateWithAI(parent, pos, yaw, name);
                fresh.transform.SetSiblingIndex(sibling);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            EditorUtility.DisplayDialog("After Hours", "Elliot has been updated and the scene saved.", "OK");
        }
    }
}
