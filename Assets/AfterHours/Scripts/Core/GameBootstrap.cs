using UnityEngine;
using AfterHours.Controls;
using AfterHours.Settings;
using AfterHours.UI;

namespace AfterHours.Core
{
    /// <summary>
    /// Runs automatically before any scene loads (you never add this to a scene).
    /// Creates one persistent object holding the global services: settings, input and the pause menu.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateServices()
        {
            if (Object.FindAnyObjectByType<GameServicesRoot>() != null) return;

            var go = new GameObject("[Game Services]");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<GameServicesRoot>();
            go.AddComponent<SettingsService>();
            go.AddComponent<InputService>();
            go.AddComponent<DevPauseMenu>();

            if (!GameLayers.AllLayersExist())
            {
                Debug.LogWarning("[AfterHours] Physics layers are missing. Run the menu 'After Hours > 1 - Set Up Project' once.");
            }
        }
    }
}
