using System;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Interaction;

namespace AfterHours.Puzzles
{
    /// <summary>
    /// The standby generator in the car park plant room. At night the door releases, the car park exit shutter motor,
    /// the dock controller and the staff exit mechanism are all on the emergency circuit it feeds - so those four ways
    /// out need it running.
    /// Pour in petrol (jerry can), then pull the starter cord: it coughs and dies a few times before it catches (each
    /// pull is loud). Once running it is VERY loud and keeps thudding through the floor; Elliot hears it from far away,
    /// less and less as it settles into the background. It can be switched off again (anything it already powered
    /// stays released).
    /// </summary>
    public class Generator : Interactable, IPuzzleInput, IResettable, IStateful
    {
        public ItemSocket fuel;
        [Tooltip("The control circuit fuse: without it the cord turns the engine but there's no spark.")]
        public ItemSocket controlFuse;
        public Light[] emergencyLights;
        public float startNoise = 42f;
        [Tooltip("Noise pulse radius when it has just started; it fades to settleNoise over settleSeconds.")]
        public float runNoise = 36f;
        public float settleNoise = 16f;
        public float settleSeconds = 180f;
        public float pulseEvery = 6f;

        public bool Running { get; private set; }
        public bool Satisfied => Running;
        public GameObject LastActor { get; private set; }
        public event Action Changed;

        int pulls;
        float runTime, pulseTimer;
        AudioSource loop;

        public override float HoldTime => 0.8f;

        public override string GetPrompt(IInteractor who)
        {
            return Running ? "Switch off generator" : "Pull the starter cord";
        }

        public override void Interact(IInteractor who)
        {
            LastActor = who.Actor;
            if (Running)
            {
                SetRunning(false);
                InteractionFx.Play(ProceduralSounds.Sfx.BreakerOff, transform.position, 0.9f, 6f, who.Actor, NoiseKind.Machinery);
                return;
            }
            Player.PlayerActions a = Player.PlayerActions.Of(who.Actor);
            if (a != null) a.Play(Player.Gesture.Reach, transform.position + Vector3.up * 0.6f, (who.Position - transform.position).normalized, Vector3.up, 0.8f);

            bool fuelled = (fuel == null || fuel.Satisfied) && (controlFuse == null || controlFuse.Satisfied);
            pulls++;
            // A cold engine: the first pulls only cough. Without petrol it never catches.
            float chance = fuelled ? Mathf.Clamp01(-0.15f + 0.22f * pulls) : 0f;
            bool starts = UnityEngine.Random.value < chance;
            AudioClip pull = AudioLibrary.Get(starts ? "generator_start" : fuelled ? "generator_cough" : "generator_pull");
            if (pull != null)
            {
                AudioPool.PlayAt(pull, transform.position, 1f, UnityEngine.Random.Range(0.95f, 1.05f), 40f);
                NoiseBus.Emit(new NoiseEvent(transform.position, starts ? startNoise : fuelled ? 18f : 10f, NoiseKind.Machinery, who.Actor));
            }
            else InteractionFx.Play(ProceduralSounds.Sfx.Rattle, transform.position, 1f, fuelled ? 18f : 10f, who.Actor, NoiseKind.Machinery);
            if (starts) { SetRunning(true); if (IsLocalPlayer(who.Actor)) AfterHours.Flow.Stats.Event("generator"); }
        }

        static bool IsLocalPlayer(GameObject a)
        {
            var pc = a != null ? a.GetComponent<AfterHours.Player.PlayerController>() : null;
            return pc != null && pc.isLocallyControlled;
        }

        void SetRunning(bool on)
        {
            if (Running == on) return;
            Running = on;
            runTime = 0f;
            pulseTimer = 2f;
            if (!on) pulls = 0;
            foreach (Light l in emergencyLights) if (l != null) l.enabled = on;
            if (on)
            {
                if (loop == null)
                {
                    loop = gameObject.AddComponent<AudioSource>();
                    loop.loop = true;
                    loop.spatialBlend = 1f;
                    loop.rolloffMode = AudioRolloffMode.Linear;
                    loop.minDistance = 2f;
                    loop.maxDistance = 70f;
                    loop.dopplerLevel = 0f;
                    loop.clip = AudioLibrary.Get("generator_run") ?? ProceduralSounds.Effect(ProceduralSounds.Sfx.ShutterRoll);
                }
                loop.volume = 0f;
                loop.Play();
            }
            else if (loop != null) loop.Stop();
            Changed?.Invoke();
        }

        void Update()
        {
            if (!Running) return;
            runTime += Time.deltaTime;
            if (loop != null) loop.volume = Mathf.MoveTowards(loop.volume, 0.9f, Time.deltaTime * 0.6f);
            pulseTimer -= Time.deltaTime;
            if (pulseTimer <= 0f)
            {
                pulseTimer = pulseEvery;
                float r = Mathf.Lerp(runNoise, settleNoise, Mathf.Clamp01(runTime / settleSeconds));
                NoiseBus.Emit(new NoiseEvent(transform.position, r, NoiseKind.Machinery, gameObject));
            }
        }

        public string CaptureState() => Running ? "1" : "0";

        public void RestoreState(string data)
        {
            bool on = data != null && data.StartsWith("1");
            if (on != Running) SetRunning(on);
            runTime = settleSeconds; // a loaded / joined game: it has been running a while
        }

        public void ResetState()
        {
            SetRunning(false);
            pulls = 0;
            LastActor = null;
        }
    }
}
