using System;
using System.IO;
using UnityEngine;
using MallHorror.Core;

namespace MallHorror.Settings
{
    /// <summary>Loads, applies and safely saves <see cref="GameSettings"/>.</summary>
    public class SettingsService : MonoBehaviour
    {
        public static SettingsService Instance { get; private set; }

        static GameSettings fallback;

        /// <summary>Always returns usable settings, even if the service has not started yet.</summary>
        public static GameSettings CurrentOrDefault
        {
            get
            {
                if (Instance != null && Instance.Current != null) return Instance.Current;
                return fallback ??= new GameSettings();
            }
        }

        public GameSettings Current { get; private set; }
        public event Action<GameSettings> Changed;

        public string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Load();
            ApplyRuntime();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnApplicationQuit()
        {
            Save();
        }

        public void Load()
        {
            GameSettings loaded = null;
            if (SafeFile.TryReadAllText(FilePath, out string json)) loaded = TryParse(json);
            if (loaded == null && SafeFile.TryReadAllText(FilePath + ".bak", out string backup)) loaded = TryParse(backup);
            Current = loaded ?? new GameSettings();
            Current.Sanitize();
        }

        public void Save()
        {
            if (Current == null) return;
            try
            {
                SafeFile.WriteAllTextAtomic(FilePath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e)
            {
                Debug.LogError("[MallHorror] Could not save settings: " + e.Message);
            }
        }

        public void ApplyRuntime()
        {
            Current.Sanitize();
            QualitySettings.vSyncCount = Current.vSync ? 1 : 0;
            Application.targetFrameRate = Current.targetFrameRate > 0 ? Current.targetFrameRate : -1;
            AudioListener.volume = Current.masterVolume;
            Changed?.Invoke(Current);
        }

        static GameSettings TryParse(string json)
        {
            try
            {
                return JsonUtility.FromJson<GameSettings>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MallHorror] Settings file unreadable, using defaults: " + e.Message);
                return null;
            }
        }
    }
}
