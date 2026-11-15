using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using AfterHours.Core;
using AfterHours.World;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Builds the walkable-area map Elliot uses to find paths through the mall (stairs, ramps, doorways, car park).
    /// Doors, props and items are left out of the map on purpose: Elliot deals with those physically (opens,
    /// bangs, forces, pushes). Runs automatically at the end of "3 - Build Mall"; can also be run on its own.
    /// </summary>
    public static class NavigationBaker
    {
        public const string DataPath = EditorBuild.Root + "/Scenes/MallNavMesh.asset";

        [MenuItem("After Hours/Rebuild AI Navigation", priority = 51)]
        public static void BakeMenu()
        {
            if (EditorApplication.isPlaying) return;
            Bake(new Bounds(new Vector3(-30f, 6f, 0f), new Vector3(340f, 34f, 270f)));
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        public static NavMeshData Bake(Bounds bounds)
        {
            EditorUtility.DisplayProgressBar("After Hours", "Building Elliot's navigation map (this can take a few minutes)...", 0.5f);
            try
            {
                NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
                settings.agentRadius = 0.35f;
                settings.agentHeight = 1.9f;        // hunched height: it ducks through 2.1 m doorways
                settings.agentClimb = 0.45f;
                settings.agentSlope = 46f;
                settings.overrideVoxelSize = true;
                settings.voxelSize = 0.12f;
                settings.overrideTileSize = true;
                settings.tileSize = 128;

                int mask = Physics.DefaultRaycastLayers;
                foreach (int layer in new[] { GameLayers.InteractableLayer, GameLayers.PropLayer, GameLayers.PlayerLayer,
                                               GameLayers.CreatureLayer, GameLayers.TriggerZoneLayer, GameLayers.LocalBodyLayer })
                {
                    if (layer >= 0) mask &= ~(1 << layer);
                }

                var sources = new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(bounds, mask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
                NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                if (data == null)
                {
                    Debug.LogError("[AfterHours] Navigation build failed.");
                    return null;
                }
                data.name = "MallNavMesh";
                AssetDatabase.DeleteAsset(DataPath);
                AssetDatabase.CreateAsset(data, DataPath);
                AssetDatabase.SaveAssets();

                NavMeshDataLoader loader = Object.FindAnyObjectByType<NavMeshDataLoader>();
                if (loader == null) loader = new GameObject("Navigation").AddComponent<NavMeshDataLoader>();
                loader.data = data;
                loader.enabled = false;
                loader.enabled = true;
                Debug.Log("[AfterHours] Navigation map built from " + sources.Count + " colliders.");
                return data;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
