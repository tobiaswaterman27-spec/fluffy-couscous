using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using AfterHours.Audio;
using AfterHours.Controls;
using AfterHours.Core;
using AfterHours.Creature;
using AfterHours.Machines;
using AfterHours.Net;
using AfterHours.Player;
using AfterHours.Power;
using AfterHours.Rendering;
using AfterHours.World.Layout;

namespace AfterHours.Flow
{
    public enum FlowState { Menu, Opening, Playing, Ending }

    /// <summary>
    /// The whole single-player game from start to finish:
    ///   Main menu   - the camera drifts slowly through the dark atrium. New / Continue / Settings / Host / Join / Quit.
    ///   Opening     - 11:30 PM. You come to on the atrium floor: ears ringing, eyes struggling to stay open,
    ///                 blurred vision; you push yourself up and stand. Somewhere far off a shutter slams down.
    ///   Playing     - autosaves every minute when it's safe; dying resets the run (and the save).
    ///   Ending      - you get out into grey morning daylight. Birds. An empty car park... and behind the glass
    ///                 of the entrance doors, something is standing very still, watching you go.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        [Header("Menu")]
        public Camera menuCamera;
        public Vector3 menuOrbitCentre = new Vector3(0f, 3f, 0f);

        [Header("Ending (built inactive by the mall builder)")]
        public GameObject endingSet;
        public Transform endingSpawn;
        public Transform[] figurePoints;
        public Transform glassPoint;
        public Light endingSun;
        public Light lobbyLight;
        public Volume daylightVolume;

        public FlowState State { get; private set; } = FlowState.Menu;
        public float PlayTime => playTime;
        /// <summary>Times caught this run (single player: each one sends you back to the atrium at 11:30).</summary>
        public int Deaths { get; private set; }

        NightDirector night;

        NightDirector Night()
        {
            if (night == null)
            {
                night = GetComponent<NightDirector>();
                if (night == null) night = gameObject.AddComponent<NightDirector>();
            }
            return night;
        }

        /// <summary>A brand-new layout of the important items (host / single player only).</summary>
        void NewLayout()
        {
            AfterHours.Items.ItemShuffle.Apply(AfterHours.Items.ItemShuffle.NewSeed());
            Night().ResetRun();
        }

        void LoadLayout(SaveData data)
        {
            AfterHours.Items.ItemShuffle.Apply(data.runSeed);
            Night().ResetRun();
            Night().NightTime = data.nightTime;
            Deaths = data.deaths;
        }

        PlayerController player;
        ElliotBrain elliot;
        float playTime, autosaveTimer, t, orbit;
        GUIStyle titleStyle, buttonStyle, smallStyle, textStyle, smallCentered, fieldStyle;
        bool hasSave;
        string saveInfo = "";
        int hover = -1;

        void Awake()
        {
            Instance = this;
            PowerGrid.RestoreAll();
            if (endingSet != null) endingSet.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GameFlowFlags.InMainMenu = false;
            GameFlowFlags.QuitToMenu = null;
            RunReset.Done -= OnRunReset;
        }

        void Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            elliot = FindAnyObjectByType<ElliotBrain>();
            if (GetComponent<SaveSystem>() == null) gameObject.AddComponent<SaveSystem>();
            Night();
            GameFlowFlags.ClockText = () => State == FlowState.Playing && night != null ? NightDirector.ClockText(night.NightTime) : null;
            RunReset.Done += OnRunReset;
            AudioLibrary.Has("preload"); // load the sound library now, not at the first sound
            EnterMenu();
        }

        // =====================================================================
        // Menu
        // =====================================================================

        void EnterMenu()
        {
            SaveSystem.CoopSlot = false;
            State = FlowState.Menu;
            GameFlowFlags.InMainMenu = true;
            GameFlowFlags.QuitToMenu = null;
            InputService.Instance?.SetGameplayEnabled(false);
            if (player != null) player.SetControlLock(true, true);
            SetPlayerCamera(false);
            SetWorldRunning(false);
            hasSave = SaveSystem.TryLoad(out SaveData d);
            saveInfo = hasSave ? $"saved {d.savedAt}  ·  {FormatTime(d.playTime)} inside" : "";
        }

        void SetPlayerCamera(bool on)
        {
            if (menuCamera != null)
            {
                menuCamera.enabled = !on;
                AudioListener ml = menuCamera.GetComponent<AudioListener>();
                if (ml != null) ml.enabled = !on;
            }
            if (player != null && player.Look != null && player.Look.playerCamera != null)
            {
                player.Look.playerCamera.enabled = on;
                AudioListener pl = player.Look.playerCamera.GetComponent<AudioListener>();
                if (pl != null) pl.enabled = on;
            }
        }

        /// <summary>Elliot and the malfunctions only run while you are actually in the building.</summary>
        void SetWorldRunning(bool on)
        {
            bool simulate = on && !NetSession.IsClient; // co-op clients only show the host's creatures and faults
            foreach (ElliotBrain b in FindObjectsByType<ElliotBrain>()) b.enabled = simulate;
            foreach (MalfunctionDirector m in FindObjectsByType<MalfunctionDirector>()) m.enabled = simulate;
        }

        // =====================================================================
        // LAN co-op (menu screens, start, endings)
        // =====================================================================

        enum MenuScreen { Main, Host, Join }
        MenuScreen screen = MenuScreen.Main;
        string addressText = "";
        string sessionMessage = "";
        NetSession net;

        NetSession Net()
        {
            if (net != null) return net;
            net = gameObject.AddComponent<NetSession>();
            net.GameStarted += OnCoopStarted;
            net.EscapeDecided += OnCoopEscape;
            net.SessionEnded += msg =>
            {
                sessionMessage = msg;
                if (State == FlowState.Ending) return; // the game is over anyway; let the credits finish
                if (State != FlowState.Menu) ReloadScene();
                else { net.Shutdown(); screen = MenuScreen.Main; }
            };
            return net;
        }

        bool continueCoop;

        void OnCoopStarted(int index)
        {
            playTime = 0f;
            coopIndex = index;
            if (NetSession.IsHost && continueCoop && SaveSystem.TryLoad(out SaveData data))
            {
                // The host carries on from the co-op save; everyone else gets the world as it was.
                playTime = data.playTime;
                LeaveMenu();
                if (player != null) player.SetControlLock(false, false);
                LoadLayout(data);
                if (SaveSystem.Instance != null) SaveSystem.Instance.Apply(data, player);
                BeginPlaying();
                return;
            }
            if (NetSession.IsHost) { SaveSystem.DeleteSave(); NewLayout(); }
            Deaths = 0;
            BeginOpening();
        }

        int coopIndex;
        bool leftBehind;

        void OnCoopEscape(EscapeRouteId r, bool escaped)
        {
            if (net != null) net.FreezeGame();
            if (escaped) BeginEnding(r);
            else
            {
                leftBehind = true;
                BeginEnding(r);
            }
        }

        void NewGame()
        {
            SaveSystem.DeleteSave();
            playTime = 0f;
            Deaths = 0;
            NewLayout();
            BeginOpening();
        }

        void ContinueGame()
        {
            if (!SaveSystem.TryLoad(out SaveData data)) { NewGame(); return; }
            playTime = data.playTime;
            LeaveMenu();
            if (player != null) player.SetControlLock(false, false);
            if (data.runSeed == 0) NewLayout(); // a save from before the shuffle: keep the items where they were saved
            else LoadLayout(data);
            if (SaveSystem.Instance != null) SaveSystem.Instance.Apply(data, player);
            BeginPlaying();
        }

        void LeaveMenu()
        {
            GameFlowFlags.InMainMenu = false;
            SetPlayerCamera(true);
            GamePause.SetCursorLocked(true);
        }

        // =====================================================================
        // Opening
        // =====================================================================

        Vector3 wakeEye;
        float wakeYaw;
        AudioSource ringing;

        void BeginOpening()
        {
            LeaveMenu();
            State = FlowState.Opening;
            t = 0f;
            DevLocation start = MallLayout.DevLocations[0];
            if (player != null)
            {
                player.Teleport(start.position + Quaternion.Euler(0f, start.yaw, 0f) * Vector3.right * (coopIndex * 1.3f), start.yaw);
                player.SetControlLock(true, true);
                if (player.Look != null) player.Look.ExternallyDriven = true;
            }
            wakeYaw = start.yaw;
            wakeEye = start.position + Quaternion.Euler(0f, start.yaw, 0f) * Vector3.right * (coopIndex * 1.3f) + Vector3.up * 0.16f;
            var go = new GameObject("Ringing");
            go.transform.SetParent(transform, false);
            ringing = go.AddComponent<AudioSource>();
            ringing.clip = MakeRinging();
            ringing.loop = true;
            ringing.spatialBlend = 0f;
            ringing.volume = 0.2f;
            ringing.Play();
        }

        void UpdateOpening(float dt)
        {
            t += dt;
            if (player == null) { BeginPlaying(); return; }
            PlayerLook look = player.Look;
            float eyeH = look != null && look.cameraRig != null ? 1.62f : 1.6f;

            // Lying on your side -> sitting up -> standing.
            float sit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(9f, 12f, t));
            float stand = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(12.2f, 14.6f, t));
            float height = Mathf.Lerp(Mathf.Lerp(0.16f, 0.9f, sit), eyeH, stand);
            float roll = Mathf.Lerp(Mathf.Lerp(78f, 9f, sit), 0f, stand);
            float pitch = Mathf.Lerp(Mathf.Lerp(4f, 22f, sit), 0f, stand) + Mathf.Sin(t * 1.3f) * 1.2f * (1f - stand);
            float yaw = wakeYaw + Mathf.Lerp(-25f, 0f, sit) + Mathf.Sin(t * 0.7f) * 2f * (1f - stand);
            Vector3 eye = new Vector3(wakeEye.x, wakeEye.y - 0.16f + height + Mathf.Sin(t * 1.9f) * 0.008f, wakeEye.z);

            Quaternion body = Quaternion.Euler(0f, yaw, roll);
            Vector3 rigLocal = new Vector3(0f, eyeH, 0f);
            player.MoveBodyTo(eye - body * rigLocal, body);
            if (look != null && look.cameraRig != null)
            {
                look.cameraRig.localPosition = rigLocal;
                look.cameraRig.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                if (look.playerCamera != null)
                {
                    look.playerCamera.transform.localPosition = Vector3.zero;
                    look.playerCamera.transform.localRotation = Quaternion.identity;
                }
            }

            if (ringing != null) ringing.volume = Mathf.Lerp(0.22f, 0f, Mathf.InverseLerp(6f, 14f, t));
            if (Fire(1, 6.4f)) AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.ShutterStop), new Vector3(0f, 3f, -70f), 1f, 0.8f, 120f);

            if (t >= 15f)
            {
                if (ringing != null) Destroy(ringing.gameObject);
                if (look != null) look.ExternallyDriven = false;
                player.SetControlLock(false, false);
                player.Teleport(new Vector3(wakeEye.x, wakeEye.y - 0.16f, wakeEye.z), wakeYaw);
                BeginPlaying();
                captionT = 0f;
            }
        }

        int fired;
        bool Fire(int bit, float at)
        {
            if (t < at || (fired & (1 << bit)) != 0) return false;
            fired |= 1 << bit;
            return true;
        }

        /// <summary>Eyes: black with blinks while waking. 1 = closed.</summary>
        float EyesClosed()
        {
            if (rewakeT >= 0f)
            {
                // After being caught: black, then two slow blinks.
                if (rewakeT < 1.2f) return 1f;
                if (rewakeT < 2.2f) return Mathf.Lerp(1f, 0.2f, (rewakeT - 1.2f) / 1f);
                if (rewakeT < 2.6f) return Mathf.Lerp(0.2f, 0.9f, (rewakeT - 2.2f) / 0.4f);
                if (rewakeT < 3.6f) return Mathf.Lerp(0.9f, 0f, (rewakeT - 2.6f) / 1f);
                return 0f;
            }
            if (State != FlowState.Opening) return 0f;
            if (t < 3f) return 1f;
            if (t < 4.6f) return Mathf.Lerp(1f, 0.35f, (t - 3f) / 1.6f);
            if (t < 5.1f) return Mathf.Lerp(0.35f, 0.95f, (t - 4.6f) / 0.5f);
            if (t < 6.8f) return Mathf.Lerp(0.95f, 0.12f, (t - 5.1f) / 1.7f);
            if (t < 7.6f) return Mathf.Lerp(0.12f, 0.8f, (t - 6.8f) / 0.8f);
            if (t < 8.8f) return Mathf.Lerp(0.8f, 0f, (t - 7.6f) / 1.2f);
            return 0f;
        }

        static AudioClip MakeRinging()
        {
            const int rate = 44100;
            int n = rate * 2;
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float tt = i / (float)rate;
                d[i] = 0.3f * Mathf.Sin(2f * Mathf.PI * 3700f * tt) * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.5f * tt));
            }
            AudioClip c = AudioClip.Create("Ear ringing", n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }

        // =====================================================================
        // Playing
        // =====================================================================

        float captionT = -1f;

        void BeginPlaying()
        {
            State = FlowState.Playing;
            GameFlowFlags.InMainMenu = false;
            GameFlowFlags.QuitToMenu = QuitToMenu;
            InputService.Instance?.SetGameplayEnabled(true);
            SetWorldRunning(true);
            autosaveTimer = 60f;
        }

        void UpdatePlaying(float dt)
        {
            if (!GamePause.IsPaused) playTime += dt;
            autosaveTimer -= dt;
            if (autosaveTimer <= 0f && SafeToSave())
            {
                autosaveTimer = 60f;
                SaveNow();
            }
        }

        bool SafeToSave()
        {
            if (player == null || NetSession.IsClient || CatchSequence.AnyActive || AfterHours.Puzzles.EscapeDirector.Escaping) return false;
            if (elliot != null && elliot.State == ElliotState.Hunt) return false;
            return !player.ControlLocked || player.IsHidden;
        }

        public void SaveNow()
        {
            if (State == FlowState.Playing && SaveSystem.Instance != null) SaveSystem.Instance.Save(player, playTime);
        }

        float rewakeT = -1f;

        void OnRunReset()
        {
            if (NetSession.Active) return; // co-op never resets the whole run
            // Caught: the run starts over, so the save goes too. You come to again on the atrium floor... at 11:30.
            SaveSystem.DeleteSave();
            Deaths++;
            NewLayout(); // everything moves: what you learned about where things were no longer holds
            rewakeT = 0f;
            captionT = 0.5f;
            autosaveTimer = 60f;
        }

        void QuitToMenu()
        {
            if (SafeToSave()) SaveNow();
            ReloadScene();
        }

        void OnApplicationQuit()
        {
            if (State == FlowState.Playing && SafeToSave()) SaveNow();
        }

        static void ReloadScene()
        {
            GamePause.SetPaused(false);
            Scene s = SceneManager.GetActiveScene();
            SceneManager.LoadScene(s.buildIndex >= 0 ? s.buildIndex : 0);
        }

        // =====================================================================
        // Ending
        // =====================================================================

        AfterHours.World.Layout.EscapeRouteId route;
        int figureStep;
        float lookAwayTime, lastStep, stareTime, finaleT = -1f;
        bool transitioned, credits;
        AudioSource birds;

        public void BeginEnding(EscapeRouteId r)
        {
            if (State == FlowState.Ending) return;
            route = r;
            State = FlowState.Ending;
            t = 0f;
            SaveSystem.DeleteSave();
            InputService.Instance?.SetGameplayEnabled(false);
            if (player != null) player.SetControlLock(true, true);
        }

        void UpdateEnding(float dt)
        {
            t += dt;
            if (leftBehind)
            {
                // The others got out. The doors are shut. Something is behind you.
                if (Fire(10, 1.2f) && player != null)
                    AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.Slam), player.transform.position + Vector3.up * 2f, 1f, 0.6f, 60f);
                if (Fire(11, 3.4f) && player != null && elliot != null && elliot.animator != null && elliot.animator.audioOut != null)
                    AudioPool.PlayAt(AudioLibrary.Get("elliot_scream"), player.EyePosition + player.transform.forward * -2f, 1f, 1f, 30f);
                if (t > 5.5f) StartCredits();
                return;
            }
            if (!transitioned && t >= 2.6f) TransitionToDaylight();
            if (!transitioned || credits) return;
            if (t > 4.2f && player != null && player.ControlLocked)
            {
                player.SetControlLock(false, false);
                InputService.Instance?.SetGameplayEnabled(true);
            }
            UpdateFigure(dt);
        }

        void TransitionToDaylight()
        {
            transitioned = true;
            SetWorldRunning(false);
            if (endingSet != null) endingSet.SetActive(true);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.8f, 0.83f, 0.86f);
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 320f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.68f);
            var sky = new UnityEngine.Rendering.SphericalHarmonicsL2();
            sky.AddAmbientLight(new Color(0.62f, 0.64f, 0.68f));
            RenderSettings.ambientProbe = sky;
            if (daylightVolume != null) daylightVolume.weight = 1f;
            if (player != null && player.Look != null && player.Look.playerCamera != null)
            {
                Camera cam = player.Look.playerCamera;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.78f, 0.82f, 0.87f);
                EyeAdaptation ea = cam.GetComponent<EyeAdaptation>();
                if (ea != null) ea.enabled = false;
            }
            if (player != null && endingSpawn != null) player.Teleport(endingSpawn.position, endingSpawn.eulerAngles.y);

            // It is standing just inside, behind the glass.
            if (elliot != null && figurePoints != null && figurePoints.Length > 0)
            {
                elliot.enabled = false;
                var agent = elliot.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.enabled = false;
                if (elliot.senses != null) elliot.senses.enabled = false;
                PlaceFigure(0);
            }

            var go = new GameObject("Dawn chorus");
            go.transform.SetParent(transform, false);
            birds = go.AddComponent<AudioSource>();
            birds.clip = AudioLibrary.Get("amb_dawn") ?? MakeBirds();
            birds.loop = true;
            birds.spatialBlend = 0f;
            birds.volume = 0.35f;
            birds.Play();
        }

        void PlaceFigure(int i)
        {
            figureStep = i;
            lastStep = t;
            Transform p = figurePoints[Mathf.Clamp(i, 0, figurePoints.Length - 1)];
            elliot.motor.Warp(p.position, p.eulerAngles.y);
            elliot.motor.Drive(Vector3.zero, ElliotGait.Still);
        }

        void UpdateFigure(float dt)
        {
            if (elliot == null || !elliot.gameObject.activeSelf || player == null || figurePoints == null || figurePoints.Length == 0)
            {
                if (finaleT >= 0f) { finaleT += dt; if (lobbyLight != null) lobbyLight.enabled = true; if (finaleT > 5f) StartCredits(); }
                else if (t > 30f) StartCredits();
                return;
            }
            Vector3 eye = player.EyePosition;
            Vector3 head = elliot.animator.head != null ? elliot.animator.head.position : elliot.transform.position + Vector3.up * 2.3f;
            elliot.motor.Drive(Vector3.zero, ElliotGait.Still);
            elliot.motor.Face(eye);
            if (elliot.animator.CurrentAction == ElliotAction.None && finaleT < 0f) elliot.animator.PlayAction(ElliotAction.Stare, eye, 30f);
            elliot.animator.LookTarget = eye;

            Vector3 view = player.Look != null && player.Look.playerCamera != null ? player.Look.playerCamera.transform.forward : player.transform.forward;
            bool looking = Vector3.Angle(view, head - eye) < 35f;
            lookAwayTime = looking ? 0f : lookAwayTime + dt;
            if (looking) stareTime += dt;

            // It only moves when you aren't looking.
            if (finaleT < 0f && figureStep < figurePoints.Length - 1 && lookAwayTime > 1.4f && t - lastStep > 4f)
            {
                PlaceFigure(figureStep + 1);
                stareTime = 0f;
            }
            if (finaleT < 0f && figureStep >= figurePoints.Length - 1 && stareTime > 2.2f)
            {
                finaleT = 0f;
                Vector3 hand = glassPoint != null ? glassPoint.position : elliot.transform.position + elliot.transform.forward * 0.6f + Vector3.up * 1.7f;
                elliot.animator.PlayAction(ElliotAction.ReachHandle, hand, 6f);
                AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.Bump), hand, 0.5f, 0.6f, 20f);
            }
            if (finaleT >= 0f)
            {
                finaleT += dt;
                if (lobbyLight != null) lobbyLight.enabled = finaleT < 2.5f || finaleT > 3.1f ? (Random.value > 0.15f) : false;
                if (finaleT > 2.8f && elliot.gameObject.activeSelf) elliot.gameObject.SetActive(false); // gone
                if (finaleT > 5f) StartCredits();
            }
            if (t > 75f) StartCredits();
        }

        float creditsT;

        void StartCredits()
        {
            if (credits) return;
            credits = true;
            creditsT = 0f;
            if (player != null) player.SetControlLock(true, true);
            InputService.Instance?.SetGameplayEnabled(false);
            GamePause.SetCursorLocked(false);
        }

        static string RouteName(EscapeRouteId r)
        {
            switch (r)
            {
                case EscapeRouteId.MainEntrance: return "through the main entrance";
                case EscapeRouteId.CarParkExit: return "up the car park exit ramp";
                case EscapeRouteId.LoadingBay: return "out through the loading bay";
                case EscapeRouteId.EmergencyExit: return "through the emergency exit";
                case EscapeRouteId.Rooftop: return "from the roof";
                case EscapeRouteId.ServiceTunnel: return "along the service tunnel";
                case EscapeRouteId.RestrictedStaffExit: return "through the staff exit";
                default: return "by a way nobody was meant to find";
            }
        }

        /// <summary>One line about how it ended, different for every way out.</summary>
        static string RouteEpilogue(EscapeRouteId r)
        {
            switch (r)
            {
                case EscapeRouteId.MainEntrance: return "The doors are still standing open. Nobody has come to close them.";
                case EscapeRouteId.CarParkExit: return "The shutter never came back down. Something walked up the ramp after you, and stopped at the line of daylight.";
                case EscapeRouteId.LoadingBay: return "A delivery driver found the dock open at six. He did not go inside.";
                case EscapeRouteId.EmergencyExit: return "The alarm company logged a fire door at the west end. The engineer's report says it was opened from the inside, twice.";
                case EscapeRouteId.Rooftop: return "The ladder ends three metres above the service road. You'll feel that drop for weeks.";
                case EscapeRouteId.ServiceTunnel: return "The tunnel comes up under the bus station. The pressure door is shut again now, and nobody admits to shutting it.";
                case EscapeRouteId.RestrictedStaffExit: return "Your name is in the staff exit log at 05:58. So is one other, an hour later. It isn't a name.";
                default: return "Nobody knows that hatch is there. Nobody but you, and whatever made the scratches on the inside of it.";
            }
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return s >= 3600 ? $"{s / 3600}:{(s / 60) % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }

        static AudioClip MakeBirds()
        {
            const int rate = 44100;
            int n = rate * 12;
            float[] d = new float[n];
            var rng = new System.Random(612);
            // Scattered chirps and trills, near and far, over a soft wind.
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.01f * (white - lp);
                d[i] = lp * 0.6f;
            }
            for (int k = 0; k < 70; k++)
            {
                int start = rng.Next(n - rate);
                float f0 = 2200f + (float)rng.NextDouble() * 2600f;
                float len = 0.05f + (float)rng.NextDouble() * 0.18f;
                int notes = 1 + rng.Next(6);
                float gain = 0.08f + (float)rng.NextDouble() * 0.25f;
                for (int nn = 0; nn < notes; nn++)
                {
                    int s0 = start + (int)(nn * len * 1.4f * rate);
                    float ph = 0f;
                    for (int j = 0; j < (int)(len * rate) && s0 + j < n; j++)
                    {
                        float tt = j / (float)rate;
                        float f = f0 * (1f + 0.35f * Mathf.Sin(tt / len * Mathf.PI)) * (1f + 0.1f * nn);
                        ph += f / rate;
                        d[s0 + j] += gain * Mathf.Sin(2f * Mathf.PI * ph) * Mathf.Sin(Mathf.PI * tt / len);
                    }
                }
            }
            AudioClip c = AudioClip.Create("Dawn chorus", n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }

        // =====================================================================
        // Update and screen
        // =====================================================================

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            TensionMusic.MenuMode = State == FlowState.Menu;
            TensionMusic.Silent = State == FlowState.Ending || State == FlowState.Opening;
            switch (State)
            {
                case FlowState.Menu:
                    GamePause.SetCursorLocked(false);
                    orbit += dt * 2.2f;
                    if (menuCamera != null)
                    {
                        Vector3 p = menuOrbitCentre + Quaternion.Euler(0f, orbit, 0f) * new Vector3(0f, 7f, -13f);
                        menuCamera.transform.SetPositionAndRotation(p, Quaternion.LookRotation(menuOrbitCentre + Vector3.down * 1.5f - p));
                    }
                    break;
                case FlowState.Opening: UpdateOpening(Time.deltaTime); break;
                case FlowState.Playing: UpdatePlaying(Time.deltaTime); break;
                case FlowState.Ending: UpdateEnding(dt); break;
            }
            if (captionT >= 0f) captionT += dt;
            if (rewakeT >= 0f) { rewakeT += dt; if (rewakeT > 4f) rewakeT = -1f; }
            if (credits)
            {
                creditsT += dt;
                bool key = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.anyKey.wasPressedThisFrame;
                bool click = UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
                if (creditsT > 6f && (key || click)) ReloadScene();
            }
        }

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 92, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            titleStyle.normal.textColor = new Color(0.86f, 0.85f, 0.8f);
            buttonStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleLeft };
            buttonStyle.normal.textColor = new Color(0.7f, 0.69f, 0.65f);
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            smallStyle.normal.textColor = new Color(0.55f, 0.54f, 0.5f);
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter };
            textStyle.normal.textColor = new Color(0.2f, 0.2f, 0.22f);
            smallCentered = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
            fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 24 };
        }

        void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width, h = Screen.height;
            Rect full = new Rect(0f, 0f, w, h);
            float scale = h / 1080f;

            if (State == FlowState.Menu && !GamePause.IsPaused)
            {
                if (screen == MenuScreen.Main) DrawMenu(scale);
                else DrawLobby(scale);
            }

            // Waking up: eyelids.
            float closed = EyesClosed();
            if (closed > 0.001f)
            {
                GUI.color = new Color(0f, 0f, 0f, closed);
                GUI.DrawTexture(full, Texture2D.whiteTexture);
                // Lids close from top and bottom.
                float lid = h * 0.5f * Mathf.Clamp01(closed * 1.3f);
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0f, 0f, w, lid), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, h - lid, w, lid), Texture2D.whiteTexture);
            }
            if (State == FlowState.Opening && t > 8f)
            {
                // Vision still swimming: a soft dark edge.
                GUI.color = new Color(0f, 0f, 0f, 0.35f * Mathf.Clamp01(1f - (t - 8f) / 6f));
                GUI.DrawTexture(full, Texture2D.whiteTexture);
            }

            // "11:30 PM" after waking.
            if (captionT >= 0f && captionT < 7f)
            {
                float a = Mathf.Clamp01(captionT / 1.5f) * Mathf.Clamp01((7f - captionT) / 1.5f);
                GUI.color = new Color(1f, 1f, 1f, a * 0.85f);
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
                GUI.Label(new Rect(80f, 1080f - 150f, 600f, 50f), "11:30 PM", buttonStyle);
                GUI.matrix = Matrix4x4.identity;
            }

            // The ending: white out, daylight, white out, words.
            if (State == FlowState.Ending)
            {
                float white = !transitioned ? Mathf.Clamp01(t / 2.6f) : Mathf.Clamp01(1f - (t - 2.6f) / 2f);
                if (credits) white = Mathf.Clamp01(creditsT / 3f);
                if (leftBehind) white = Mathf.Clamp01(t / 1.2f);
                if (white > 0.001f)
                {
                    GUI.color = leftBehind ? new Color(0f, 0f, 0f, white) : new Color(1f, 1f, 1f, white);
                    GUI.DrawTexture(full, Texture2D.whiteTexture);
                }
                if (credits && creditsT > 3f)
                {
                    float a = Mathf.Clamp01((creditsT - 3f) / 2f);
                    GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
                    float cx = w / scale;
                    GUI.color = new Color(1f, 1f, 1f, a);
                    titleStyle.alignment = TextAnchor.MiddleCenter;
                    Color old = titleStyle.normal.textColor;
                    titleStyle.normal.textColor = new Color(0.15f, 0.15f, 0.17f);
                    GUI.Label(new Rect(0f, 330f, cx, 120f), "AFTER HOURS", titleStyle);
                    titleStyle.normal.textColor = old;
                    titleStyle.alignment = TextAnchor.MiddleLeft;
                    Color tc = textStyle.normal.textColor;
                    if (leftBehind) textStyle.normal.textColor = new Color(0.75f, 0.73f, 0.7f);
                    GUI.Label(new Rect(0f, 480f, cx, 50f), leftBehind ? "They got out. You didn't." : "You got out " + RouteName(route) + ".", textStyle);
                    if (!leftBehind) GUI.Label(new Rect(0f, 520f, cx, 40f), RouteEpilogue(route), smallCentered);
                    GUI.Label(new Rect(0f, 565f, cx, 50f), "Time inside: " + FormatTime(playTime) + (Deaths > 0 ? "   ·   caught " + Deaths + (Deaths == 1 ? " time" : " times") : "   ·   never caught"), textStyle);
                    if (!leftBehind) GUI.Label(new Rect(0f, 615f, cx, 50f), "6:12 AM.", textStyle);
                    if (net != null && net.Players.Count > 1)
                    {
                        float y = 660f;
                        foreach (NetSession.NetPlayer p in net.Players)
                        {
                            string line = $"{p.name}   {(p.escaped ? "got out" : "left behind")}   ·   solved {p.solved}   ·   caught {p.caught}";
                            GUI.Label(new Rect(0f, y, cx, 36f), line, smallCentered);
                            y += 36f;
                        }
                    }
                    textStyle.normal.textColor = tc;
                    if (creditsT > 6f)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.5f + 0.3f * Mathf.Sin(creditsT * 2f));
                        GUI.Label(new Rect(0f, 760f, cx, 40f), "press any key", textStyle);
                    }
                    GUI.matrix = Matrix4x4.identity;
                }
            }
            GUI.color = Color.white;
        }

        void DrawMenu(float scale)
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(0f, 0f, 760f, 1080f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // The title flickers very slightly, like a dying tube.
            float flick = Random.value < 0.015f ? 0.45f : 1f;
            GUI.color = new Color(1f, 1f, 1f, flick);
            GUI.Label(new Rect(110f, 170f, 900f, 130f), "AFTER HOURS", titleStyle);
            GUI.color = Color.white;

            // who you are: click to change
            {
                var settings = AfterHours.Settings.SettingsService.CurrentOrDefault;
                AfterHours.Player.PlayerCharacters.Entry me = AfterHours.Player.PlayerCharacters.Get(settings.character);
                Rect cr = new Rect(120f, 300f, 600f, 80f);
                bool overC = cr.Contains(Event.current.mousePosition);
                GUI.color = overC ? Color.white : new Color(0.75f, 0.73f, 0.7f);
                GUI.Label(new Rect(122f, 300f, 600f, 40f), "You are " + (me != null ? me.name : settings.character) + "   \u25B8", smallStyle);
                GUI.color = Color.white;
                if (overC && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    Event.current.Use();
                    var all = AfterHours.Player.PlayerCharacters.All;
                    if (all.Length > 0)
                    {
                        AfterHours.Settings.SettingsService service = AfterHours.Settings.SettingsService.Instance;
                        string next = all[(AfterHours.Player.PlayerCharacters.IndexOf(settings.character) + 1) % all.Length].id;
                        if (service != null) { service.Current.character = next; service.Save(); }
                    }
                }
            }

            string[] items = { "New game", "Continue", "Settings", "Host LAN game", "Join LAN game", "Quit" };
            bool[] enabled = { true, hasSave, true, true, true, true };
            Vector2 mouse = Event.current.mousePosition;
            for (int i = 0; i < items.Length; i++)
            {
                Rect r = new Rect(120f, 400f + i * 62f, 520f, 52f);
                bool over = r.Contains(mouse) && enabled[i];
                if (Event.current.type == EventType.Repaint && over) hover = i;
                GUIStyle st = buttonStyle;
                Color c = st.normal.textColor;
                st.normal.textColor = !enabled[i] ? new Color(0.35f, 0.34f, 0.32f) : over ? new Color(0.95f, 0.93f, 0.88f) : c;
                GUI.Label(r, (over ? "—  " : "") + items[i], st);
                st.normal.textColor = c;
                if (i == 1 && hasSave) GUI.Label(new Rect(360f, 400f + i * 62f, 400f, 52f), saveInfo, smallStyle);
                if (i == 3) GUI.Label(new Rect(420f, 400f + i * 62f, 400f, 52f), "up to 4 players, same network", smallStyle);
                if (enabled[i] && Event.current.type == EventType.MouseDown && Event.current.button == 0 && r.Contains(mouse))
                {
                    Event.current.Use();
                    AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.Click), menuCamera != null ? menuCamera.transform.position : Vector3.zero, 0.4f, 1.3f, 5f);
                    switch (i)
                    {
                        case 0: NewGame(); break;
                        case 1: ContinueGame(); break;
                        case 2: AfterHours.UI.DevPauseMenu.OpenFromMenu(); break;
                        case 3: sessionMessage = ""; SaveSystem.CoopSlot = true; continueCoop = false; Net().Host(); screen = MenuScreen.Host; break;
                        case 4: sessionMessage = ""; SaveSystem.CoopSlot = false; Net().Browse(); screen = MenuScreen.Join; break;
                        case 5:
#if UNITY_EDITOR
                            UnityEditor.EditorApplication.isPlaying = false;
#else
                            Application.Quit();
#endif
                            break;
                    }
                }
            }
            GUI.Label(new Rect(120f, 1080f - 110f, 700f, 40f), "Headphones recommended. Play in the dark.", smallStyle);
            if (!string.IsNullOrEmpty(sessionMessage)) GUI.Label(new Rect(120f, 1080f - 150f, 900f, 40f), sessionMessage, smallStyle);
            GUI.matrix = Matrix4x4.identity;
        }

        bool LobbyButton(Rect r, string text, bool enabled = true)
        {
            Vector2 mouse = Event.current.mousePosition;
            bool over = enabled && r.Contains(mouse);
            Color c = buttonStyle.normal.textColor;
            buttonStyle.normal.textColor = !enabled ? new Color(0.35f, 0.34f, 0.32f) : over ? new Color(0.95f, 0.93f, 0.88f) : c;
            GUI.Label(r, (over ? "—  " : "") + text, buttonStyle);
            buttonStyle.normal.textColor = c;
            if (enabled && Event.current.type == EventType.MouseDown && Event.current.button == 0 && over)
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        void DrawLobby(float scale)
        {
            NetSession n = Net();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0f, 0f, 900f, 1080f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(110f, 140f, 900f, 110f), screen == MenuScreen.Host ? "HOST" : "JOIN", titleStyle);

            GUI.Label(new Rect(120f, 270f, 200f, 40f), "Your name", smallStyle);
            bool canRename = n.phase == NetSession.Phase.Lobby && !NetSession.Active;
            GUI.enabled = canRename;
            n.LocalName = GUI.TextField(new Rect(300f, 268f, 360f, 40f), n.LocalName, 20, fieldStyle);
            GUI.enabled = true;
            GUI.Label(new Rect(120f, 320f, 760f, 40f), n.Status, smallStyle);

            float y = 380f;
            if (screen == MenuScreen.Host || NetSession.IsClient)
            {
                GUI.Label(new Rect(120f, y, 600f, 40f), "Players", smallStyle);
                y += 44f;
                foreach (NetSession.NetPlayer p in n.Players)
                {
                    GUI.color = p.colour;
                    GUI.DrawTexture(new Rect(124f, y + 12f, 18f, 18f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(160f, y, 600f, 44f), p.name + (p.local ? "  (you)" : p.id == 0 ? "  (host)" : ""), buttonStyle);
                    y += 46f;
                }
                if (screen == MenuScreen.Host)
                {
                    int creatures = NetCreatures.CountFor(n.Players.Count);
                    GUI.Label(new Rect(120f, y + 10f, 700f, 40f), $"{creatures} creature{(creatures > 1 ? "s" : "")} will be hunting you.", smallStyle);
                    if (SaveSystem.HasSave)
                    {
                        if (LobbyButton(new Rect(120f, y + 60f, 700f, 48f), (continueCoop ? "[x] " : "[ ] ") + "Carry on the saved co-op game")) continueCoop = !continueCoop;
                        y += 50f;
                    }
                    if (LobbyButton(new Rect(120f, y + 70f, 500f, 52f), "Start", n.Players.Count >= 1)) n.HostStartGame();
                    GUI.Label(new Rect(120f, y + 130f, 760f, 40f), "Friends can also join after you've started.", smallStyle);
                }
                else GUI.Label(new Rect(120f, y + 20f, 700f, 40f), "Waiting for the host to start...", smallStyle);
            }
            else
            {
                GUI.Label(new Rect(120f, y, 700f, 40f), "Games on this network", smallStyle);
                y += 44f;
                if (n.Discovery.Games.Count == 0) { GUI.Label(new Rect(120f, y, 700f, 44f), "Looking...", smallStyle); y += 46f; }
                foreach (LanDiscovery.Found g in n.Discovery.Games)
                {
                    if (LobbyButton(new Rect(120f, y, 700f, 50f), $"{g.name}   ({g.players}/4)")) n.Join(g.endPoint);
                    y += 52f;
                }
                y += 20f;
                GUI.Label(new Rect(120f, y, 700f, 40f), "Or type the host's address (e.g. 192.168.1.20)", smallStyle);
                addressText = GUI.TextField(new Rect(120f, y + 44f, 380f, 40f), addressText, 40, fieldStyle);
                bool valid = NetSession.TryParseAddress(addressText, out System.Net.IPEndPoint ep);
                if (LobbyButton(new Rect(520f, y + 38f, 200f, 52f), "Join", valid)) n.Join(ep);
            }

            if (LobbyButton(new Rect(120f, 1080f - 170f, 400f, 52f), "Back"))
            {
                SaveSystem.CoopSlot = false;
                n.Shutdown();
                screen = MenuScreen.Main;
            }
            GUI.matrix = Matrix4x4.identity;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;
    }
}
