using UnityEditor;
using UnityEngine;
using MallHorror.Core;

namespace MallHorror.EditorTools
{
    /// <summary>Menu: Mall Horror > 1 - Set Up Project. Safe to run any number of times.</summary>
    public static class ProjectSetup
    {
        [MenuItem("Mall Horror/1 - Set Up Project", priority = 1)]
        public static void SetUpFromMenu()
        {
            bool changed = EnsureLayers();
            EditorUtility.DisplayDialog("Mall Horror",
                changed ? "Project set up: physics layers created.\n\nNext: Mall Horror > 2 - Build Movement Test Scene."
                        : "Project was already set up.\n\nNext: Mall Horror > 2 - Build Movement Test Scene.",
                "OK");
        }

        /// <summary>Adds the game's physics layers to Project Settings > Tags and Layers.</summary>
        public static bool EnsureLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[MallHorror] Could not open ProjectSettings/TagManager.asset");
                return false;
            }

            var so = new SerializedObject(assets[0]);
            SerializedProperty layers = so.FindProperty("layers");
            bool changed = false;

            foreach (string name in GameLayers.AllNames)
            {
                if (HasLayer(layers, name)) continue;
                int slot = -1;
                for (int i = 8; i < layers.arraySize; i++)
                {
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    {
                        slot = i;
                        break;
                    }
                }
                if (slot < 0)
                {
                    Debug.LogError("[MallHorror] No free layer slot for " + name);
                    continue;
                }
                layers.GetArrayElementAtIndex(slot).stringValue = name;
                changed = true;
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
            return changed;
        }

        static bool HasLayer(SerializedProperty layers, string name)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == name) return true;
            }
            return false;
        }
    }
}
