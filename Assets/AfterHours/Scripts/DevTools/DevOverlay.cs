using UnityEngine;
using AfterHours.Audio;
using AfterHours.Controls;
using AfterHours.Core;
using AfterHours.Player;

namespace AfterHours.DevTools
{
    /// <summary>
    /// Developer-only info panel (F3) and controls help. Not part of the final game UI.
    /// F5 toggles a third-person view that shows your real collision body.
    /// </summary>
    public class DevOverlay : MonoBehaviour
    {
        public PlayerMotor motor;
        public PlayerLook look;
        public bool showStats = true;
        public float helpSeconds = 20f;

        float helpUntil;
        float smoothedDelta = 0.016f;
        GUIStyle style;

        void Start()
        {
            helpUntil = Time.realtimeSinceStartup + helpSeconds;
        }

        void Update()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.05f);
            InputService input = InputService.Instance;
            if (input == null) return;
            if (input.ToggleStats.WasPressedThisFrame())
            {
                showStats = !showStats;
                helpUntil = showStats ? Time.realtimeSinceStartup + helpSeconds : 0f;
            }
            if (input.ToggleDebugView.WasPressedThisFrame() && look != null) look.ToggleThirdPersonDebug();
            if (input.Screenshot.WasPressedThisFrame()) TakeScreenshot();
            if (input.ToggleFullbright.WasPressedThisFrame()) ToggleFullbright();
        }

        /// <summary>F12: saves a screenshot to the ReviewShots folder in the project (to share with Claude).</summary>
        void TakeScreenshot()
        {
            string folder = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "ReviewShots");
            System.IO.Directory.CreateDirectory(folder);
            string file = System.IO.Path.Combine(folder, "play_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(file);
            lastScreenshot = file;
            lastScreenshotTime = Time.realtimeSinceStartup;
        }

        bool fullbright;
        Color savedAmbient;
        UnityEngine.Rendering.AmbientMode savedMode;
        readonly System.Collections.Generic.List<UnityEngine.Rendering.Volume> disabledVolumes = new System.Collections.Generic.List<UnityEngine.Rendering.Volume>();

        /// <summary>F6: developer full-bright view (bright flat light, no post effects) to inspect geometry in the dark.</summary>
        void ToggleFullbright()
        {
            fullbright = !fullbright;
            if (fullbright)
            {
                savedAmbient = RenderSettings.ambientLight;
                savedMode = RenderSettings.ambientMode;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.75f, 0.75f, 0.75f);
                disabledVolumes.Clear();
                foreach (var v in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
                {
                    if (v.enabled) { v.enabled = false; disabledVolumes.Add(v); }
                }
            }
            else
            {
                RenderSettings.ambientMode = savedMode;
                RenderSettings.ambientLight = savedAmbient;
                foreach (var v in disabledVolumes) if (v != null) v.enabled = true;
                disabledVolumes.Clear();
            }
        }

        string lastScreenshot;
        float lastScreenshotTime = -100f;

        void OnGUI()
        {
            if (motor == null || GamePause.IsPaused) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                style.normal.textColor = new Color(0.85f, 0.95f, 0.85f);
            }

            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            if (showStats)
            {
                LocomotionState l = motor.Locomotion;
                string stance = motor.CurrentStance.ToString();
                if (motor.DesiredStance != motor.CurrentStance) stance += "  (wants " + motor.DesiredStance + ")";
                if (motor.StanceBlocked) stance += "  <color=#ff6060>NO ROOM</color>";
                if (motor.CrawlBlocked) stance += "  <color=#ff6060>NO ROOM FOR LEGS</color>";

                string noise = "-";
                if (NoiseBus.HasLast)
                {
                    NoiseEvent e = NoiseBus.Last;
                    noise = $"{e.Kind} {e.Radius:0.0} m  ({Time.time - e.Timestamp:0.0}s ago)";
                }

                string text =
                    $"<b>AFTER HOURS - DEV</b>   FPS {1f / Mathf.Max(0.0001f, smoothedDelta):0}\n" +
                    $"Stance: {stance}\n" +
                    $"Body height {motor.CurrentHeight:0.00} m   radius {motor.CurrentRadius:0.00} m\n" +
                    $"Mode: {l.mode}   Speed {l.planarSpeed:0.00} m/s   {(l.backward ? "BACKWARDS" : "")}\n" +
                    $"Local vel: side {l.localPlanarVelocity.x:0.00}  fwd {l.localPlanarVelocity.y:0.00}\n" +
                    $"Grounded: {l.grounded}   Surface: {motor.GroundSurface}   Slope {motor.GroundSlope:0}°\n" +
                    $"Hip bend {l.hipBendDegrees:0}°   Head yaw {(look != null ? look.HeadYaw : 0f):0}°\n" +
                    $"Last noise: {noise}";
                GUI.Box(new Rect(10, 10, 470, 175), GUIContent.none);
                GUI.Label(new Rect(20, 16, 460, 170), text, style);
            }

            if (Time.realtimeSinceStartup < helpUntil)
            {
                string help =
                    "<b>CONTROLS</b>\n" +
                    "WASD move (S = backwards)   Mouse look\n" +
                    "Double-tap + hold a move key = sprint (also backwards)\n" +
                    "Hold Shift = crouch   Hold Ctrl = crawl\n" +
                    "Hold E on a heavy object = push / pull it\n" +
                    "Esc pause/settings/teleport  F3 info  F5 body  F6 full-bright  F12 shot";
                GUI.Box(new Rect(10, 195, 470, 125), GUIContent.none);
                GUI.Label(new Rect(20, 200, 460, 120), help, style);
            }

            if (Time.realtimeSinceStartup - lastScreenshotTime < 3f)
            {
                GUI.Label(new Rect(20, 1040, 1200, 30), "Screenshot saved: " + lastScreenshot, style);
            }

            GUI.matrix = Matrix4x4.identity;
        }
    }
}
