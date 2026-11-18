using System;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Interaction;

namespace AfterHours.Puzzles
{
    /// <summary>
    /// A real "delayed egress" fire door: push the bar and the alarm goes off at once, but the magnetic lock only
    /// lets go after fifteen seconds. Fifteen seconds of bell, flashing beacon and Elliot coming. Or: find the
    /// lock's power cable first and cut it - maglocks are fail-safe, so the door releases silently.
    /// </summary>
    public class DelayedEgress : Interactable, IPuzzleInput, IResettable, IStateful
    {
        public float delay = 15f;
        public float alarmRadius = 70f;
        public float ringFor = 45f;
        [Tooltip("Cutting the maglock's power cable: the door releases at once, silently.")]
        public ToolBreakable powerCut;
        public Light beacon;

        public bool Satisfied { get; private set; }
        public GameObject LastActor { get; private set; }
        public event Action Changed;

        float alarmT = -1f;
        float bellTimer, noiseTimer;
        AudioSource bell;

        public bool Ringing => alarmT >= 0f && alarmT < ringFor + delay;

        public override float HoldTime => 0.35f;

        void OnEnable() { if (powerCut != null) powerCut.Changed += OnPowerCut; }
        void OnDisable() { if (powerCut != null) powerCut.Changed -= OnPowerCut; }

        public override string GetPrompt(IInteractor who)
        {
            if (Satisfied || alarmT >= 0f) return null;
            return "Push the bar";
        }

        public override void Interact(IInteractor who)
        {
            if (Satisfied || alarmT >= 0f) return;
            LastActor = who.Actor;
            Player.PlayerActions a = Player.PlayerActions.Of(who.Actor);
            if (a != null) a.Play(Player.Gesture.Push, transform.position, (who.Position - transform.position).normalized, transform.right, 0.6f);
            if (powerCut != null && powerCut.Satisfied)
            {
                Release();
                var pc = who.Actor != null ? who.Actor.GetComponent<AfterHours.Player.PlayerController>() : null;
                if (pc != null && pc.isLocallyControlled) AfterHours.Flow.Stats.Event("silent_fire_exit");
                return;
            }
            InteractionFx.Play(ProceduralSounds.Sfx.Latch, transform.position, 0.8f, 6f, who.Actor, NoiseKind.Door);
            alarmT = 0f;
            bellTimer = 0f;
            noiseTimer = 0f;
        }

        void OnPowerCut()
        {
            if (powerCut == null || !powerCut.Satisfied) return;
            // No power: the bell (on the same supply) dies and the lock lets go.
            if (alarmT >= 0f) alarmT = ringFor + delay;
            if (bell != null) bell.Stop();
            if (beacon != null) beacon.enabled = false;
        }

        void Release()
        {
            if (Satisfied) return;
            Satisfied = true;
            foreach (Collider c in GetComponents<Collider>()) if (c.isTrigger) c.enabled = false;
            InteractionFx.Play(ProceduralSounds.Sfx.Unlock, transform.position, 1f, 8f, LastActor, NoiseKind.Door);
            Changed?.Invoke();
        }

        void Update()
        {
            if (alarmT < 0f) return;
            alarmT += Time.deltaTime;
            bool powered = powerCut == null || !powerCut.Satisfied;
            if (!Satisfied && (alarmT >= delay || !powered)) Release();
            if (!Ringing || !powered)
            {
                if (bell != null && bell.isPlaying) bell.Stop();
                if (beacon != null) beacon.enabled = false;
                return;
            }
            if (beacon != null) beacon.enabled = Mathf.Repeat(alarmT, 0.6f) < 0.3f;
            AudioClip loopClip = AudioLibrary.Get("fire_alarm");
            if (loopClip != null)
            {
                if (bell == null)
                {
                    bell = gameObject.AddComponent<AudioSource>();
                    bell.clip = loopClip;
                    bell.loop = true;
                    bell.spatialBlend = 1f;
                    bell.rolloffMode = AudioRolloffMode.Linear;
                    bell.minDistance = 3f;
                    bell.maxDistance = 90f;
                    bell.dopplerLevel = 0f;
                }
                if (!bell.isPlaying) bell.Play();
            }
            else
            {
                bellTimer -= Time.deltaTime;
                if (bellTimer <= 0f)
                {
                    bellTimer = 0.9f;
                    AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.Bell), transform.position + Vector3.up * 0.8f, 1f, 1f, 90f);
                }
            }
            noiseTimer -= Time.deltaTime;
            if (noiseTimer <= 0f)
            {
                noiseTimer = 2f;
                NoiseBus.Emit(new NoiseEvent(transform.position, alarmRadius, NoiseKind.Machinery, LastActor));
            }
        }

        public string CaptureState() => Satisfied ? "1" : alarmT >= 0f ? "a " + alarmT.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "0";

        public void RestoreState(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            if (data[0] == '1')
            {
                if (Satisfied) return;
                alarmT = Mathf.Max(alarmT, ringFor + delay);
                Satisfied = true;
                foreach (Collider c in GetComponents<Collider>()) if (c.isTrigger) c.enabled = false;
                Changed?.Invoke();
                return;
            }
            if (data[0] == 'a' && alarmT < 0f)
            {
                float.TryParse(data.Substring(1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
                alarmT = v;
            }
        }

        public void ResetState()
        {
            alarmT = -1f;
            Satisfied = false;
            LastActor = null;
            foreach (Collider c in GetComponents<Collider>()) if (c.isTrigger) c.enabled = true;
            if (bell != null) bell.Stop();
            if (beacon != null) beacon.enabled = false;
            Changed?.Invoke();
        }
    }
}
