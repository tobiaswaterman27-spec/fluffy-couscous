namespace AfterHours.Core
{
    /// <summary>Small shared flags so low-level systems (the pause menu) can follow the game flow.</summary>
    public static class GameFlowFlags
    {
        /// <summary>True while the main menu is showing.</summary>
        public static bool InMainMenu;
        /// <summary>Set while playing: saves and returns to the main menu.</summary>
        public static System.Action QuitToMenu;
        /// <summary>The time of night while playing ("2:14 AM"), or null.</summary>
        public static System.Func<string> ClockText;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            InMainMenu = false;
            QuitToMenu = null;
            ClockText = null;
        }
    }
}
