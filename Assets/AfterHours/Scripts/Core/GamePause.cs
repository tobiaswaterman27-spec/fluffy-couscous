using UnityEngine;

namespace AfterHours.Core
{
    /// <summary>
    /// Single-player pause state. (In LAN co-op the world cannot freeze; the pause menu will
    /// become an overlay that does not stop time. That is handled in Part 14.)
    /// </summary>
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }
        /// <summary>LAN co-op: the world keeps running while the menu is open.</summary>
        public static bool CannotFreeze;
        public static event System.Action<bool> Changed;

        public static void SetPaused(bool paused)
        {
            if (paused == IsPaused) return;
            IsPaused = paused;
            Time.timeScale = paused && !CannotFreeze ? 0f : 1f;
            SetCursorLocked(!paused);
            Changed?.Invoke(paused);
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // Resets static state when entering Play Mode with "domain reload" disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsPaused = false;
            CannotFreeze = false;
            Changed = null;
            Time.timeScale = 1f;
        }
    }
}
