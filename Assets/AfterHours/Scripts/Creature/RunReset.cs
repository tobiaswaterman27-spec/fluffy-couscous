using UnityEngine;
using AfterHours.Player;
using AfterHours.Puzzles;
using AfterHours.World.Layout;

namespace AfterHours.Creature
{
    /// <summary>
    /// Single-player run reset after being caught: everything goes back to how it started - doors, shutters,
    /// items, vent covers, hiding places, puzzles, blood - the player returns to the start and Elliot goes back to
    /// the car park, unaware, its memories wiped.
    /// </summary>
    public static class RunReset
    {
        public static Vector3 ElliotStart = new Vector3(30f, MallLayout.CarParkY + 0.05f, 70f);
        public static float ElliotStartYaw = 180f;

        public static event System.Action Done;

        public static void Execute()
        {
            DevLocation start = MallLayout.DevLocations[0];
            int i = 0;
            foreach (PlayerController p in PlayerController.All)
            {
                if (p == null) continue;
                p.SetControlLock(false, false);
                if (p.Look != null) p.Look.ExternallyDriven = false;
                p.IsHidden = false;
                p.CurrentHidingSpot = null;
                // Co-op players start side by side.
                p.Teleport(start.position + Quaternion.Euler(0f, start.yaw, 0f) * new Vector3((i - 0.5f * (PlayerController.All.Count - 1)) * 1.1f, 0f, 0f), start.yaw);
                i++;
            }
            foreach (MonoBehaviour m in Object.FindObjectsByType<MonoBehaviour>())
            {
                if (m is IResettable r) r.ResetState();
            }
            foreach (ElliotBrain b in Object.FindObjectsByType<ElliotBrain>())
            {
                b.ResetToStart(ElliotStart, ElliotStartYaw);
            }
            Done?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Done = null;
    }
}
