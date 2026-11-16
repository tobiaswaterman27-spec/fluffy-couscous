using UnityEngine;
using AfterHours.Controls;
using AfterHours.Core;
using AfterHours.Creature;
using AfterHours.Player;
using AfterHours.World;

namespace AfterHours.Audio
{
    /// <summary>
    /// Your own body: breathing that follows how hard you've been running and how scared you are, a heartbeat
    /// that climbs when something is close, and holding your breath while hidden (hold Crouch). Heavy breathing
    /// is a sound too: in a locker with Elliot outside, panting can give you away. Hold your breath too long and
    /// you gasp.
    /// </summary>
    public class PlayerAudio : MonoBehaviour
    {
        PlayerController player;
        AudioSource calm, heavy, panic, heart, oneShots, duct;
        float exertion, fear, heartTimer, breathNoiseTimer;
        float heldFor;
        bool holding;
        public float Fear => fear;
        public bool HoldingBreath => holding;
        public float BreathLeft => Mathf.Clamp01(1f - heldFor / MaxHold);
        const float MaxHold = 14f;

        void Start()
        {
            player = GetComponent<PlayerController>();
            if (player == null || !player.isLocallyControlled) { Destroy(this); return; }
            // (No breathing or voice sounds for you: just your heart and the duct around you.)
            duct = Loop("amb_duct", 0f);
            heart = Make();
            oneShots = Make();
        }

        AudioSource Make()
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.spatialBlend = 0f;
            s.playOnAwake = false;
            return s;
        }

        AudioSource Loop(string key, float vol)
        {
            AudioSource s = Make();
            s.clip = AudioLibrary.Get(key);
            s.loop = true;
            s.volume = vol;
            if (s.clip != null) { s.timeSamples = Random.Range(0, s.clip.samples); s.Play(); }
            return s;
        }

        void Update()
        {
            if (player == null || !player.isLocallyControlled) return;
            float dt = Time.deltaTime;
            LocomotionState l = player.Motor.Locomotion;

            // Exertion builds while sprinting, fades slowly.
            exertion = l.sprinting ? Mathf.Min(1f, exertion + dt / 6f) : Mathf.Max(0f, exertion - dt / 14f);

            // Fear: the nearest creature, how close, and whether it's coming.
            float threat = 0f;
            foreach (ElliotBrain b in FindCreatures())
            {
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(b.transform.position, transform.position);
                float near = Mathf.Clamp01(1f - d / 25f);
                bool chasing = b.motor != null && b.motor.Gait == ElliotGait.Run;
                threat = Mathf.Max(threat, near * (chasing ? 1f : 0.6f) + (chasing && d < 40f ? 0.35f : 0f));
            }
            if (CatchSequence.IsBeingCaught(player)) threat = 1f;
            fear = Mathf.MoveTowards(fear, Mathf.Clamp01(threat), dt * (threat > fear ? 0.8f : 0.12f));

            // Holding your breath (only while hidden).
            InputService input = InputService.Instance;
            bool wantHold = player.IsHidden && input != null && input.Crouch != null && input.Crouch.IsPressed();
            if (wantHold && !holding && heldFor < MaxHold * 0.3f) holding = true;
            if (holding)
            {
                heldFor += dt;
                if (!wantHold || !player.IsHidden || heldFor >= MaxHold)
                {
                    holding = false;
                    bool forced = heldFor >= MaxHold;
                    // (released silently; a forced release is still heard by the creatures)
                    if (forced) NoiseBus.Emit(new NoiseEvent(player.EyePosition, 7f, NoiseKind.Voice, gameObject));
                    exertion = Mathf.Max(exertion, heldFor / MaxHold * 0.8f);
                }
            }
            else heldFor = Mathf.Max(0f, heldFor - dt * 0.7f);

            float load = Mathf.Max(exertion, fear * 0.9f);
            float mute = holding ? 0f : 1f;
            Fade(calm, (1f - Mathf.Clamp01(load * 1.6f)) * 0.22f * mute, dt);
            Fade(heavy, Mathf.Clamp01(load * 1.5f) * (1f - fear) * 0.45f * mute, dt);
            Fade(panic, Mathf.Clamp01((fear - 0.4f) * 1.7f) * 0.55f * mute, dt);

            // In the vents: the hollow sound of the duct around you.
            Fade(duct, VentDuct.Contains(transform.position + Vector3.up * 0.3f) ? 0.4f : 0f, dt);

            // Heartbeat: felt when afraid (or out of breath from holding it).
            float bpm = Mathf.Lerp(62f, 150f, Mathf.Max(fear, heldFor / MaxHold));
            float heartVol = Mathf.Clamp01(Mathf.Max(fear - 0.15f, heldFor / MaxHold - 0.3f) * 1.4f);
            heartTimer -= dt;
            if (heartTimer <= 0f)
            {
                heartTimer = 60f / bpm;
                if (heartVol > 0.02f) { AudioClip h = AudioLibrary.Get("heartbeat"); if (h != null) heart.PlayOneShot(h, heartVol * 0.9f); }
            }

            // Panting is audible (the host's creatures hear it through the network noise forward).
            breathNoiseTimer -= dt;
            if (breathNoiseTimer <= 0f)
            {
                breathNoiseTimer = 1.5f;
                if (!holding && load > 0.45f) NoiseBus.Emit(new NoiseEvent(player.EyePosition, Mathf.Lerp(1.2f, 3.5f, load), NoiseKind.Voice, gameObject));
            }
        }

        static ElliotBrain[] cached;
        static float cacheTime = -10f;
        static ElliotBrain[] FindCreatures()
        {
            if (Time.time - cacheTime > 2f || cached == null) { cached = FindObjectsByType<ElliotBrain>(FindObjectsSortMode.None); cacheTime = Time.time; }
            return cached;
        }

        static void Fade(AudioSource s, float target, float dt)
        {
            if (s == null || s.clip == null) return;
            s.volume = Mathf.MoveTowards(s.volume, target, dt * 0.6f);
        }

        void Play(string key, float vol)
        {
            AudioClip c = AudioLibrary.Get(key);
            if (c != null && oneShots != null) oneShots.PlayOneShot(c, vol);
        }

        void OnGUI()
        {
            if (player == null || !player.IsHidden || GamePause.IsPaused) return;
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            var st = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0f, 1080f - 120f, w, 30f), holding ? "holding your breath" : "hold Crouch to hold your breath", st);
            if (holding)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.DrawTexture(new Rect(w * 0.5f - 60f, 1080f - 88f, 120f * BreathLeft, 3f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.identity;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cached = null; cacheTime = -10f; }
    }

    /// <summary>
    /// The score: a barely-audible dread bed; a sting when something starts running at you; the chase music while it
    /// does; a long exhausted fall when it gives up. The main menu has its own drifting music box.
    /// </summary>
    public class TensionMusic : MonoBehaviour
    {
        AudioSource dread, chase, stings, near;
        float closeness;
        bool chased;
        float chaseHold;
        public static bool MenuMode;
        public static bool Silent;
        AudioSource menu;

        void Start()
        {
            PlayerController pc = GetComponent<PlayerController>();
            if (pc != null && !pc.isLocallyControlled) { Destroy(this); return; }
            dread = Loop("music_dread", 0f);
            chase = Loop("music_chase", 0f);
            near = Loop("music_near", 0f);
            menu = Loop("music_menu", 0f);
            stings = gameObject.AddComponent<AudioSource>();
            stings.spatialBlend = 0f;
            stings.playOnAwake = false;
        }

        AudioSource Loop(string key, float vol)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.spatialBlend = 0f;
            s.clip = AudioLibrary.Get(key);
            s.loop = true;
            s.volume = vol;
            s.ignoreListenerPause = true;
            if (s.clip != null) s.Play();
            return s;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            AudioMix.Tick(dt);
            float musicVol = AfterHours.Settings.SettingsService.CurrentOrDefault.musicVolume;
            bool hunted = false;
            float nearest = 999f;
            foreach (ElliotBrain b in FindObjectsByType<ElliotBrain>(FindObjectsSortMode.None))
            {
                if (b == null || b.motor == null || !b.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(b.transform.position, transform.position);
                nearest = Mathf.Min(nearest, d);
                if (b.motor.Gait == ElliotGait.Run && d < 45f) hunted = true;
            }
            // How close the nearest one is: 0 far away (40 m+), 1 right on top of you (4 m).
            closeness = Mathf.MoveTowards(closeness, Mathf.Clamp01(Mathf.InverseLerp(40f, 4f, nearest)), dt * 0.5f);
            if (hunted) chaseHold = 4f; else chaseHold -= dt;
            bool now = chaseHold > 0f && !MenuMode;
            if (now && !chased)
            {
                Sting("music_sting", 0.9f);
                AudioPool.Play2D(AudioLibrary.Get("sub_hit"), 0.85f);
                AudioMix.Kick(1f, 1.5f);
            }
            if (!now && chased) Sting("music_lost", 0.5f);
            chased = now;
            if (Silent) musicVol = 0f;
            Fade(menu, MenuMode ? 0.5f * musicVol : 0f, dt, 0.3f);
            Fade(dread, MenuMode ? 0f : (chased ? 0.05f : 0.16f * (1f - 0.6f * closeness)) * musicVol * (1f - AudioMix.Duck), dt, 0.3f);
            // The chase music swells as it closes in; the thin, trembling layer creeps in whenever it's near.
            Fade(chase, chased ? Mathf.Lerp(0.3f, 0.75f, closeness) * musicVol : 0f, dt, chased ? 0.6f : 0.15f);
            Fade(near, MenuMode ? 0f : closeness * (chased ? 0.25f : 0.45f) * musicVol, dt, 0.2f);
        }

        void Sting(string key, float vol)
        {
            AudioClip c = AudioLibrary.Get(key);
            if (c != null) stings.PlayOneShot(c, vol * AfterHours.Settings.SettingsService.CurrentOrDefault.musicVolume);
        }

        static void Fade(AudioSource s, float target, float dt, float speed)
        {
            if (s == null || s.clip == null) return;
            s.volume = Mathf.MoveTowards(s.volume, target, dt * speed);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { MenuMode = false; Silent = false; }
    }

    /// <summary>
    /// The building is never quite silent: somewhere a pipe ticks, a beam groans as it cools, water drips, a distant
    /// thud, a tray of something rattling, a crackle in the wiring. Random, positioned around you at a distance,
    /// muffled by walls.
    /// </summary>
    public class AmbientEvents : MonoBehaviour
    {
        float timer = 6f;

        void Start()
        {
            PlayerController pc = GetComponent<PlayerController>();
            if (pc != null && !pc.isLocallyControlled) Destroy(this);
        }
        float NearestCreature()
        {
            float best = 9999f;
            foreach (ElliotBrain b in FindObjectsByType<ElliotBrain>(FindObjectsSortMode.None))
                if (b != null && b.gameObject.activeInHierarchy) best = Mathf.Min(best, Vector3.Distance(b.transform.position, transform.position));
            return best;
        }

        System.Collections.IEnumerator FalseAlarm()
        {
            Vector2 d = Random.insideUnitCircle.normalized * Random.Range(18f, 30f);
            Vector3 p = transform.position + new Vector3(d.x, 0f, d.y);
            if (Random.value < 0.6f)
            {
                int steps = Random.Range(3, 6);
                for (int i = 0; i < steps; i++)
                {
                    AudioClip s = AudioLibrary.Get("elliot_step_hard");
                    if (s != null) AudioPool.PlayAt(s, p, 0.6f, Random.Range(0.92f, 1.02f), 60f);
                    p += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                    yield return new WaitForSeconds(Random.Range(0.55f, 0.75f));
                }
            }
            else
            {
                AudioClip g = AudioLibrary.Get(Random.value < 0.4f ? "elliot_growl" : Random.value < 0.5f ? "elliot_moan" : Random.value < 0.5f ? "elliot_call" : "elliot_wail");
                if (g != null) AudioPool.PlayAt(g, p + Vector3.up * 2f, 0.45f, Random.Range(0.85f, 0.95f), 70f);
            }
        }

        static readonly string[] Inside = { "groan", "pipe_tick", "distant_thud", "distant_clatter", "crackle", "drip" };
        static readonly string[] CarPark = { "drip", "drip", "groan", "distant_thud", "pipe_tick" };

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Random.Range(7f, 22f);
            // Now and then, when nothing is actually near: footsteps somewhere that stop, or a groan far away.
            if (Random.value < 0.12f && NearestCreature() > 50f)
            {
                StartCoroutine(FalseAlarm());
                return;
            }
            bool carPark = transform.position.y < -0.6f;
            string[] set = carPark ? CarPark : Inside;
            AudioClip c = AudioLibrary.Get(set[Random.Range(0, set.Length)]);
            if (c == null) return;
            Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(10f, 35f);
            Vector3 p = transform.position + new Vector3(dir.x, Random.Range(0.5f, 4f), dir.y);
            AudioPool.PlayAt(c, p, Random.Range(0.35f, 0.75f), Random.Range(0.9f, 1.1f), 60f);
        }
    }
}
