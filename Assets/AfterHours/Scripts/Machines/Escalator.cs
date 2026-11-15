using UnityEngine;
using AfterHours.Audio;
using AfterHours.Interaction;
using AfterHours.Power;
using AfterHours.Puzzles;
using AfterHours.World;

namespace AfterHours.Machines
{
    public enum EscalatorState { Running, Stopped, Faulty }

    /// <summary>
    /// A working escalator: the steps really move and carry you (and anything standing on them), with the
    /// rumble and clack of the mechanism - which also hides your footsteps from Elliot nearby.
    /// Running / stopped (power cut or emergency stop) / faulty (lurches, grinds, jerks to a halt, restarts).
    /// The red emergency stop at the bottom works; restarting needs the key switch beside it... or a power cycle.
    /// </summary>
    public class Escalator : MonoBehaviour, IResettable
    {
        public ConveyorSurface belt;
        public Transform[] steps;
        public Vector3 bottom, top;
        public bool goesUp = true;
        public float speed = 0.5f;
        public EscalatorState state = EscalatorState.Running;
        public string circuit = "escalators";
        public AudioSource motor;
        public int seed = 1;

        EscalatorState startState;
        float factor;           // current belt speed (0..1.6 of nominal)
        float phase;
        float faultTimer;
        bool faultStopped;
        System.Random rng;
        AmbientSound ambience;

        public bool Running => state != EscalatorState.Stopped && PowerGrid.IsPowered(circuit);

        /// <summary>LAN co-op: clients copy the host's state (including when a faulty one is stuck).</summary>
        public bool remote;
        public string NetCapture() => (int)state + " " + (faultStopped ? "1" : "0");
        public void NetApply(string[] a)
        {
            remote = true;
            state = (EscalatorState)int.Parse(a[0]);
            faultStopped = a[1] == "1";
        }

        void Awake()
        {
            startState = state;
            rng = new System.Random(seed);
            ambience = motor != null ? motor.GetComponent<AmbientSound>() : null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float want = Running ? 1f : 0f;
            if (state == EscalatorState.Faulty && Running && remote) want = faultStopped ? 0f : 1f;
            else if (state == EscalatorState.Faulty && Running)
            {
                // Lurch, grind, stop dead, sit there, then jerk back into motion.
                faultTimer -= dt;
                if (faultTimer <= 0f)
                {
                    faultStopped = !faultStopped;
                    faultTimer = faultStopped ? Range(1.5f, 6f) : Range(4f, 14f);
                    InteractionFx.Play(faultStopped ? ProceduralSounds.Sfx.MetalClank : ProceduralSounds.Sfx.Thud,
                        (bottom + top) * 0.5f, 0.8f, 16f, gameObject, NoiseKind.Machinery);
                }
                want = faultStopped ? 0f : (faultTimer < 0.6f ? 1.6f : 1f);
            }
            // Real escalators stop within a second, and start over a couple of seconds.
            factor = Mathf.MoveTowards(factor, want, dt * (want < factor ? 1.4f : 0.5f));

            Vector3 dir = (top - bottom).normalized * (goesUp ? 1f : -1f);
            if (belt != null)
            {
                belt.localVelocity = belt.transform.InverseTransformDirection(dir * speed * factor);
                belt.running = factor > 0.01f;
            }

            // Steps cycle along the incline.
            if (steps != null && steps.Length > 0)
            {
                float length = Vector3.Distance(bottom, top);
                phase = Mathf.Repeat(phase + speed * factor * dt * (goesUp ? 1f : -1f), length);
                float spacing = length / steps.Length;
                for (int i = 0; i < steps.Length; i++)
                {
                    if (steps[i] == null) continue;
                    float s = Mathf.Repeat(phase + i * spacing, length);
                    steps[i].position = Vector3.Lerp(bottom, top, s / length);
                }
            }

            if (motor != null)
            {
                motor.volume = Mathf.Lerp(0f, 0.45f, Mathf.Clamp01(factor));
                motor.pitch = 0.75f + 0.3f * factor;
            }
            if (ambience != null) ambience.maskingLevel = 0.55f * Mathf.Clamp01(factor);
        }

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public void EmergencyStop(GameObject actor)
        {
            if (state == EscalatorState.Stopped) return;
            state = EscalatorState.Stopped;
            InteractionFx.Play(ProceduralSounds.Sfx.MetalClank, bottom, 0.8f, 12f, actor, NoiseKind.Machinery);
        }

        public void Restart(GameObject actor)
        {
            state = startState == EscalatorState.Faulty ? EscalatorState.Faulty : EscalatorState.Running;
            InteractionFx.Play(ProceduralSounds.Sfx.Click, bottom, 0.6f, 6f, actor, NoiseKind.Machinery);
        }

        /// <summary>A malfunction: this escalator develops a fault.</summary>
        public void Break() => state = EscalatorState.Faulty;

        public void ResetState()
        {
            state = startState;
            faultStopped = false;
            faultTimer = 0f;
        }
    }

    /// <summary>The red mushroom button (and key switch) at the foot of an escalator.</summary>
    public class EscalatorControl : Interactable
    {
        public Escalator escalator;

        public override float HoldTime => escalator != null && escalator.state == EscalatorState.Stopped ? 1.2f : 0.2f;

        public override string GetPrompt(IInteractor who)
        {
            if (escalator == null) return null;
            if (!PowerGrid.IsPowered(escalator.circuit)) return "Emergency stop (no power)";
            return escalator.state == EscalatorState.Stopped ? "Restart escalator" : "Emergency stop";
        }

        public override void Interact(IInteractor who)
        {
            if (escalator == null) return;
            if (escalator.state == EscalatorState.Stopped) escalator.Restart(who.Actor);
            else escalator.EmergencyStop(who.Actor);
        }
    }
}
