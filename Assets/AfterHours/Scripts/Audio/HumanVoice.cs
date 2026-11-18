using UnityEngine;
using AfterHours.Creature;
using AfterHours.Player;

namespace AfterHours.Audio
{
    /// <summary>
    /// The sounds a person makes without talking (tools/sfxgen/gen_human.py), for every player - you hear your own
    /// quietly and close, and other co-op players from where they are, muffled through walls:
    ///   breathing that follows what the body is doing - slow through the nose at rest, heavy when recovering,
    ///   panting hard while sprinting, shaky and uneven when it's near;
    ///   a gasp when it suddenly appears; effort as you shove, barge, climb, pull or swing; winded when you hit
    ///   something; whimpers you can't keep in while hiding with it outside; the long shaky release after holding
    ///   your breath; and screaming, struggling and choking when it gets you.
    /// Each person has a male or female voice and their own pitch. The loud ones are noises Elliot can hear.
    /// </summary>
    public class HumanVoice : MonoBehaviour
    {
        static readonly string[] Female = { "priya", "ruth", "leah", "sofia", "jade" };

        PlayerController pc;
        PlayerBody body;
        PlayerActions actions;
        PlayerGrab grab;
        PlayerAudio own;
        AudioSource breathSrc, fxSrc;
        bool local;
        string sex = "m";
        float pitch = 1f;
        string builtFor;

        float exertion, fear, nextBreath, nextWhimper, nextStruggle, gaspCooldown, fearSlow;
        Gesture lastGesture;
        bool wasCaught, wasLimp, wasHolding, wasHoldingBreath;
        float heldFor;

        /// <summary>How open the mouth is from these sounds right now (the jaw follows it).</summary>
        public float MouthOpen { get; private set; }

        void Start()
        {
            pc = GetComponent<PlayerController>();
            body = GetComponent<PlayerBody>();
            actions = GetComponent<PlayerActions>();
            grab = GetComponent<PlayerGrab>();
            local = pc != null && pc.isLocallyControlled;
            breathSrc = MakeSource("Voice (breath)");
            fxSrc = MakeSource("Voice");
            nextBreath = Time.time + Random.Range(0f, 1.5f);
        }

        AudioSource MakeSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.6f, 0.1f);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.dopplerLevel = 0f;
            if (local)
            {
                s.spatialBlend = 0f;           // your own breath is inside your head
            }
            else
            {
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = 1.2f;
                s.maxDistance = 28f;
                go.AddComponent<AudioOcclusion>();
            }
            return s;
        }

        void Configure()
        {
            string id = body != null && !string.IsNullOrEmpty(body.characterId) ? body.characterId : PlayerCharacters.Default;
            if (id == builtFor) return;
            builtFor = id;
            sex = System.Array.IndexOf(Female, id) >= 0 ? "f" : "m";
            // everyone sounds a little different: a fixed pitch per person
            int h = 0;
            foreach (char c in id) h = h * 31 + c;
            pitch = 0.94f + (Mathf.Abs(h) % 1000) / 1000f * 0.12f;
            if (id == "arthur") pitch *= 0.93f;
        }

        string Key(string what) => "hv_" + sex + "_" + what;

        void Play(AudioSource s, string what, float volume, float noiseRadius = 0f)
        {
            AudioClip c = AudioLibrary.Get(Key(what));
            if (c == null || s == null) return;
            s.pitch = pitch * Random.Range(0.97f, 1.03f);
            s.clip = c;
            s.volume = volume * (local ? 0.55f : 1f);
            s.Play();
            // only the machine that owns this player tells the creatures (co-op forwards it to the host)
            if (local && noiseRadius > 0.1f) NoiseBus.Emit(new NoiseEvent(transform.position + Vector3.up * 1.5f, noiseRadius, NoiseKind.Voice, gameObject));
        }

        void Update()
        {
            if (pc == null || breathSrc == null) return;
            Configure();
            float dt = Time.deltaTime, t = Time.time;
            PlayerMotor motor = pc.Motor;
            LocomotionState l = motor != null ? motor.Locomotion : default;

            bool caught = CatchSequence.IsBeingCaught(pc);
            bool limp = PlayerRagdoll.IsActive(body);
            if (own == null && local) own = GetComponent<PlayerAudio>();
            bool holdingBreath = own != null && own.HoldingBreath;

            exertion = l.sprinting ? Mathf.Min(1f, exertion + dt / 5f) : Mathf.Max(0f, exertion - dt / 16f);
            float want = own != null ? own.Fear : NearFear();
            fear = Mathf.MoveTowards(fear, want, dt * (want > fear ? 1.2f : 0.15f));
            fearSlow = Mathf.Lerp(fearSlow, fear, 1f - Mathf.Exp(-dt / 1.5f));

            // ---- being caught: screaming, struggling, then a last choke when it's over
            if (caught && !wasCaught) { Play(fxSrc, "scream", 1f, 35f); nextStruggle = t + 1.6f; }
            if (caught && !limp && t >= nextStruggle && !fxSrc.isPlaying) { Play(fxSrc, "struggle", 0.9f, 18f); nextStruggle = t + Random.Range(0.3f, 0.9f); }
            if (limp && !wasLimp && wasCaught) Play(fxSrc, "choke", 0.8f);
            wasCaught = caught;
            wasLimp = limp;
            if (caught || limp) { breathSrc.Stop(); MouthOpen = fxSrc.isPlaying ? 0.8f : 0f; return; }

            // ---- effort with the hands
            if (actions != null && actions.Current != lastGesture)
            {
                switch (actions.Current)
                {
                    case Gesture.Barge: Play(fxSrc, "effortbig", 0.85f, 6f); break;
                    case Gesture.Strike: Play(fxSrc, "effortbig", 0.8f, 6f); break;
                    case Gesture.Climb: Play(fxSrc, "strain", 0.7f, 3f); break;
                    case Gesture.Grille: Play(fxSrc, "effort", 0.6f, 3f); break;
                    case Gesture.Flinch: Play(fxSrc, "oof", 0.85f, 6f); break;
                    case Gesture.Drop: Play(fxSrc, "oof", 0.8f, 6f); break;
                    case Gesture.Rattle: if (Random.value < 0.5f) Play(fxSrc, "effort", 0.5f, 3f); break;
                }
                lastGesture = actions.Current;
            }
            bool holding = grab != null && grab.IsHolding;
            if (holding && !wasHolding) Play(fxSrc, "effort", 0.55f, 3f);
            wasHolding = holding;

            // ---- a sudden scare
            gaspCooldown -= dt;
            if (fear - fearSlow > 0.3f && fear > 0.45f && gaspCooldown <= 0f && !holdingBreath)
            {
                Play(fxSrc, "gasp", 0.8f, 5f);
                gaspCooldown = 10f;
                nextBreath = t + 1.2f;
            }

            // ---- holding your breath, and letting it go
            if (holdingBreath) { heldFor += dt; breathSrc.Stop(); }
            else if (wasHoldingBreath && heldFor > 2.5f) { Play(fxSrc, "relief", Mathf.Lerp(0.4f, 0.75f, heldFor / 12f), 4f); nextBreath = t + 2.2f; heldFor = 0f; }
            else heldFor = 0f;
            wasHoldingBreath = holdingBreath;

            // ---- hiding with it right outside: you can't quite keep it in
            if (pc.IsHidden && fear > 0.6f && !holdingBreath && t >= nextWhimper)
            {
                if (nextWhimper > 0f && Random.value < 0.5f) Play(fxSrc, "whimper", 0.45f, 3.5f);
                nextWhimper = t + Random.Range(5f, 11f);
            }

            // ---- breathing
            if (!holdingBreath && t >= nextBreath && !breathSrc.isPlaying && !fxSrc.isPlaying)
            {
                string kind;
                float vol, gap;
                if (l.sprinting || exertion > 0.6f) { kind = "pant"; vol = 0.65f; gap = 0f; }
                else if (exertion > 0.22f) { kind = "heavy"; vol = Mathf.Lerp(0.3f, 0.5f, exertion); gap = Random.Range(0.05f, 0.25f); }
                else if (fear > 0.4f) { kind = "scared"; vol = Mathf.Lerp(0.25f, 0.45f, fear); gap = Random.Range(0.1f, 0.6f); }
                else { kind = "calm"; vol = local ? 0.12f : 0.18f; gap = Random.Range(0.4f, 1.2f); }
                if (pc.IsHidden && kind != "pant") vol *= 0.8f;
                Play(breathSrc, kind, vol);
                nextBreath = t + (breathSrc.clip != null ? breathSrc.clip.length / breathSrc.pitch : 2f) + gap;
            }

            float m = 0f;
            if (fxSrc.isPlaying) m = 0.7f;
            else if (breathSrc.isPlaying && breathSrc.clip != null && !breathSrc.clip.name.Contains("calm"))
                m = Mathf.Clamp01(exertion * 0.6f + 0.15f);
            MouthOpen = Mathf.Lerp(MouthOpen, m, 1f - Mathf.Exp(-dt * 12f));
        }

        /// <summary>Other players: how close the nearest creature is (their own machine knows more; this is close enough).</summary>
        float NearFear()
        {
            float f = 0f;
            foreach (ElliotBrain b in ElliotBrain.All)
            {
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(b.transform.position, transform.position);
                f = Mathf.Max(f, Mathf.Clamp01(1f - (d - 5f) / 20f));
            }
            return f;
        }
    }
}
