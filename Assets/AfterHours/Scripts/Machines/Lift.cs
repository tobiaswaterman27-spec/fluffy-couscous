using System.Collections.Generic;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Core;
using AfterHours.Interaction;
using AfterHours.Player;
using AfterHours.Power;
using AfterHours.Puzzles;
using AfterHours.World;

namespace AfterHours.Machines
{
    public enum LiftState { Idle, Opening, Open, Closing, Moving, Stuck, Falling, Crashed }

    /// <summary>A pair of sliding door leaves that part from the middle.</summary>
    public class SlidingDoorPair : MonoBehaviour
    {
        public Transform left, right;
        [Tooltip("How far each leaf slides (local x).")]
        public float travel = 0.55f;
        Vector3 leftClosed, rightClosed;
        bool init;

        public float Amount { get; private set; }

        void Awake() => Init();

        void Init()
        {
            if (init) return;
            init = true;
            if (left != null) leftClosed = left.localPosition;
            if (right != null) rightClosed = right.localPosition;
        }

        public void Set(float amount)
        {
            Init();
            Amount = Mathf.Clamp01(amount);
            if (left != null) left.localPosition = leftClosed + Vector3.left * travel * Amount;
            if (right != null) right.localPosition = rightClosed + Vector3.right * travel * Amount;
        }

        /// <summary>Is a person or creature standing in the doorway?</summary>
        public bool Obstructed()
        {
            int mask = 0;
            if (GameLayers.PlayerLayer >= 0) mask |= 1 << GameLayers.PlayerLayer;
            if (GameLayers.CreatureLayer >= 0) mask |= 1 << GameLayers.CreatureLayer;
            Vector3 c = transform.position + Vector3.up * 1.05f;
            return Physics.CheckBox(c, new Vector3(travel, 1f, 0.25f), transform.rotation, mask, QueryTriggerInteraction.Ignore);
        }
    }

    /// <summary>
    /// A lift: a real car that travels up and down its shaft carrying whoever is inside, with sliding car and
    /// landing doors that re-open if someone is in the way, call buttons on every floor, a floor panel, an
    /// indicator, a chime (which Elliot can hear), and an alarm button.
    /// No power: it stops dead between floors with only the emergency light, until the power comes back.
    /// Very rarely the cables give: a jolt, the brakes scream, and it falls to the car park - and Elliot hears.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class LiftController : MonoBehaviour, IResettable
    {
        public MovingPlatform car;
        public float[] levelY;
        public string[] levelNames;
        public SlidingDoorPair carDoors;
        public SlidingDoorPair[] landingDoors;
        public TextMesh[] indicators;
        public Light carLight;
        public AudioSource motorAudio;
        public string circuit = "lifts";
        public int startLevel = 1;
        public float speed = 1.4f;
        public float acceleration = 0.8f;
        [Range(0f, 1f)] public float dropChancePerTrip = 0.03f;
        public int seed = 1;

        public LiftState State => state;
        public int CurrentLevel => current;

        LiftState state = LiftState.Idle;
        int current, target;
        float carY, velocity, openAmt, timer, jam;
        float dropAtY = float.NaN;
        float lightBase;
        readonly List<int> queue = new List<int>();
        System.Random rng;
        readonly Collider[] riderBuf = new Collider[8];

        void Awake()
        {
            rng = new System.Random(seed);
            current = Mathf.Clamp(startLevel, 0, levelY.Length - 1);
            carY = car != null ? car.transform.position.y : levelY[current];
            netY = carY;
            if (car != null) car.manual = true;
            if (carLight != null) lightBase = carLight.intensity;
        }

        void Start() => UpdateIndicators();

        // ---------------- requests ----------------

        public bool Broken => state == LiftState.Crashed || state == LiftState.Falling;

        public void Request(int level, GameObject actor)
        {
            if (Broken || level < 0 || level >= levelY.Length) return;
            if (!PowerGrid.IsPowered(circuit)) return;
            if (level == current && (state == LiftState.Idle || state == LiftState.Closing))
            {
                state = LiftState.Opening;
                return;
            }
            if (level == current && state == LiftState.Open) { timer = Mathf.Max(timer, 2f); return; }
            if (!queue.Contains(level)) queue.Add(level);
            if (state == LiftState.Open) timer = Mathf.Min(timer, 1f);
        }

        public void OpenDoorsButton(GameObject actor)
        {
            if (Broken) return;
            if (state == LiftState.Idle || state == LiftState.Closing) { if (PowerGrid.IsPowered(circuit)) state = LiftState.Opening; }
            else if (state == LiftState.Open) timer = Mathf.Max(timer, 3f);
            else if (state == LiftState.Stuck && Mathf.Abs(carY - levelY[NearestLevel()]) < 0.5f)
            {
                // Hauling the doors apart by hand.
                current = NearestLevel();
                openAmt = Mathf.Max(openAmt, 0.55f);
                ApplyDoors();
                InteractionFx.Play(ProceduralSounds.Sfx.DrawerSlide, carDoors.transform.position, 0.8f, 7f, actor, NoiseKind.Machinery);
            }
        }

        public void Alarm(GameObject actor)
        {
            // An old bell: very loud, and it carries.
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.Bell), car.transform.position + Vector3.up * 2f, 1f, 1f, 60f);
            NoiseBus.Emit(new NoiseEvent(car.transform.position, 35f, NoiseKind.Machinery, actor));
        }

        int NearestLevel()
        {
            int best = 0;
            for (int i = 1; i < levelY.Length; i++) if (Mathf.Abs(levelY[i] - carY) < Mathf.Abs(levelY[best] - carY)) best = i;
            return best;
        }

        // ---------------- update ----------------

        // ---------------- LAN co-op: clients show the host's lift ----------------
        public bool remote;
        float netY;

        public string NetCapture() => (int)state + " " + carY.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " "
            + openAmt.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " " + current;

        public void NetApply(string[] a)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            remote = true;
            LiftState s = (LiftState)int.Parse(a[0], inv);
            if (s == LiftState.Crashed && state != LiftState.Crashed) Crash();
            if (s == LiftState.Falling && state != LiftState.Falling) BeginFall();
            if (s == LiftState.Moving && state != LiftState.Moving) { } // motor sound follows from velocity below
            state = s;
            netY = float.Parse(a[1], inv);
            openAmt = float.Parse(a[2], inv);
            current = int.Parse(a[3], inv);
            if (s == LiftState.Opening && state != LiftState.Opening) { }
        }

        void UpdateRemote(float dt)
        {
            float delta = Mathf.MoveTowards(carY, netY, Mathf.Max(speed * 2f, 15f) * dt) - carY;
            velocity = delta / Mathf.Max(dt, 1e-4f);
            carY += delta;
            if (car != null) car.ManualMove(Vector3.up * delta);
            ApplyDoors();
            UpdateLight();
            if (motorAudio != null)
            {
                bool running = Mathf.Abs(velocity) > 0.05f;
                motorAudio.volume = Mathf.MoveTowards(motorAudio.volume, running ? 0.35f : 0f, dt);
            }
            if (delta != 0f) UpdateIndicators();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (remote) { UpdateRemote(dt); return; }
            bool powered = PowerGrid.IsPowered(circuit);
            float delta = 0f;

            switch (state)
            {
                case LiftState.Idle:
                    if (powered && queue.Count > 0)
                    {
                        target = queue[0];
                        queue.RemoveAt(0);
                        if (target == current) state = LiftState.Opening;
                        else BeginMove();
                    }
                    break;

                case LiftState.Opening:
                    if (!powered) { state = LiftState.Idle; break; }
                    openAmt = Mathf.MoveTowards(openAmt, 1f, dt / 1.1f);
                    if (openAmt >= 1f) { state = LiftState.Open; timer = 5f; }
                    break;

                case LiftState.Open:
                    timer -= dt;
                    if (timer <= 0f && powered) state = LiftState.Closing;
                    break;

                case LiftState.Closing:
                    if (!powered) break; // doors stay where they are
                    if (openAmt >= 0.999f) AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.LiftDoors), car.transform.position + Vector3.up, 0.5f, 0.95f, 15f);
                    if (Obstructed())
                    {
                        state = LiftState.Opening;
                        InteractionFx.Play(ProceduralSounds.Sfx.Bump, carDoors.transform.position, 0.4f, 3f, gameObject, NoiseKind.Machinery);
                        break;
                    }
                    openAmt = Mathf.MoveTowards(openAmt, 0f, dt / 1.4f);
                    if (openAmt <= 0f) state = LiftState.Idle;
                    break;

                case LiftState.Moving:
                    if (!powered)
                    {
                        state = LiftState.Stuck;
                        velocity = 0f;
                        InteractionFx.Play(ProceduralSounds.Sfx.Thud, car.transform.position, 0.9f, 12f, gameObject, NoiseKind.Machinery);
                        break;
                    }
                    delta = MoveStep(dt);
                    if (!float.IsNaN(dropAtY) && (velocity < 0f ? carY <= dropAtY : carY >= dropAtY)) { BeginFall(); break; }
                    if (Mathf.Abs(carY + delta - levelY[target]) < 0.002f && Mathf.Abs(velocity) < 0.05f)
                    {
                        delta = levelY[target] - carY;
                        velocity = 0f;
                        current = target;
                        Arrive();
                    }
                    break;

                case LiftState.Stuck:
                    if (powered) state = LiftState.Moving;
                    break;

                case LiftState.Falling:
                    timer += dt;
                    if (timer < 0.5f) break; // the jolt: a moment of nothing
                    velocity = Mathf.Max(velocity - 9.81f * 0.75f * dt, -14f);
                    delta = velocity * dt;
                    if (carY + delta <= levelY[0])
                    {
                        delta = levelY[0] - carY;
                        Crash();
                    }
                    break;
            }

            if (state == LiftState.Crashed) openAmt = jam;
            if (car != null)
            {
                if (delta != 0f && car.WouldHit(Vector3.up * delta) && state != LiftState.Falling) { delta = 0f; velocity = 0f; }
                carY += delta;
                car.ManualMove(Vector3.up * delta);
            }
            ApplyDoors();
            UpdateLight();
            if (motorAudio != null)
            {
                bool running = state == LiftState.Moving || state == LiftState.Falling;
                motorAudio.volume = Mathf.MoveTowards(motorAudio.volume, running ? 0.35f : 0f, dt);
                motorAudio.pitch = state == LiftState.Falling ? 1.8f : 0.8f + Mathf.Abs(velocity) * 0.1f;
            }
            if (Mathf.Abs(delta) > 0f) UpdateIndicators();
        }

        void BeginMove()
        {
            state = LiftState.Moving;
            dropAtY = float.NaN;
            // The rare malfunction: only with someone inside, never from the car park itself.
            if (current > 0 && Riders().Count > 0 && rng.NextDouble() < dropChancePerTrip)
            {
                float a = Mathf.Min(levelY[current], levelY[target]), b = Mathf.Max(levelY[current], levelY[target]);
                dropAtY = Mathf.Lerp(a, b, 0.3f + 0.4f * (float)rng.NextDouble());
            }
        }

        float MoveStep(float dt)
        {
            float remaining = levelY[target] - carY;
            float dir = Mathf.Sign(remaining);
            float maxV = Mathf.Min(speed, Mathf.Sqrt(2f * acceleration * Mathf.Abs(remaining)) + 0.02f);
            velocity = Mathf.MoveTowards(velocity, dir * maxV, acceleration * dt);
            float step = velocity * dt;
            if (Mathf.Abs(step) > Mathf.Abs(remaining)) step = remaining;
            return step;
        }

        void Arrive()
        {
            state = LiftState.Opening;
            UpdateIndicators();
            Vector3 at = car.transform.position + Vector3.up * 2.3f;
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.Chime), at, 0.8f, 1f, 30f); // ding-dong
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.LiftDoors), at + Vector3.down * 1.5f, 0.6f, 1f, 15f);
            NoiseBus.Emit(new NoiseEvent(at, 16f, NoiseKind.Machinery, gameObject));
        }

        void BeginFall()
        {
            state = LiftState.Falling;
            timer = 0f;
            velocity = 0f;
            queue.Clear();
            Vector3 at = car.transform.position + Vector3.up * 2.6f;
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.MetalClank), at, 1f, 0.6f, 60f);
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.Crunch), at, 1f, 0.5f, 60f);
            AudioPool.PlayAt(ProceduralSounds.ScrapeLoop(SurfaceType.Metal), at, 1f, 1.6f, 60f);
            NoiseBus.Emit(new NoiseEvent(at, 30f, NoiseKind.Machinery, gameObject));
            foreach (PlayerController p in Riders()) if (p.Look != null) p.Look.OnLanded(8f);
        }

        void Crash()
        {
            state = LiftState.Crashed;
            velocity = 0f;
            current = 0;
            jam = 0.62f; // jammed part-open: just wide enough to squeeze out
            Vector3 at = car.transform.position + Vector3.up;
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.Slam), at, 1f, 0.5f, 80f);
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.Thud), at, 1f, 0.6f, 80f);
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.MetalClank), at, 1f, 0.7f, 80f);
            // The car park is Elliot's territory. It hears this.
            NoiseBus.Emit(new NoiseEvent(at, 60f, NoiseKind.Machinery, gameObject));
            foreach (PlayerController p in Riders()) if (p.Look != null) p.Look.OnLanded(30f);
            UpdateIndicators();
        }

        bool Obstructed()
        {
            if (carDoors != null && carDoors.Obstructed()) return true;
            SlidingDoorPair landing = LandingAtCar();
            return landing != null && landing.Obstructed();
        }

        SlidingDoorPair LandingAtCar()
        {
            if (landingDoors == null) return null;
            int n = NearestLevel();
            if (n >= landingDoors.Length || Mathf.Abs(levelY[n] - carY) > 0.6f) return null;
            return landingDoors[n];
        }

        void ApplyDoors()
        {
            if (carDoors != null) carDoors.Set(openAmt);
            if (landingDoors == null) return;
            SlidingDoorPair here = LandingAtCar();
            foreach (SlidingDoorPair d in landingDoors) if (d != null) d.Set(d == here ? openAmt : 0f);
        }

        void UpdateLight()
        {
            if (carLight == null) return;
            bool powered = PowerGrid.IsPowered(circuit);
            if (state == LiftState.Crashed || state == LiftState.Falling)
            {
                carLight.intensity = rng.NextDouble() < 0.12 ? lightBase * 0.8f : lightBase * 0.05f;
                carLight.color = new Color(1f, 0.85f, 0.7f);
            }
            else if (!powered)
            {
                carLight.intensity = lightBase * 0.25f;
                carLight.color = new Color(1f, 0.55f, 0.4f); // battery emergency light
            }
            else
            {
                carLight.intensity = lightBase;
                carLight.color = Color.white;
            }
        }

        void UpdateIndicators()
        {
            if (indicators == null) return;
            string text = Broken ? "--" : levelNames[NearestLevel()];
            foreach (TextMesh t in indicators) if (t != null) t.text = text;
        }

        public List<PlayerController> Riders()
        {
            var list = new List<PlayerController>();
            if (car == null) return list;
            Collider col = car.GetComponent<Collider>();
            Bounds b = col != null ? col.bounds : new Bounds(car.transform.position, Vector3.one * 2f);
            Vector3 c = b.center + Vector3.up * 1.2f;
            int mask = GameLayers.PlayerLayer >= 0 ? 1 << GameLayers.PlayerLayer : ~0;
            int n = Physics.OverlapBoxNonAlloc(c, new Vector3(b.extents.x, 1.1f, b.extents.z), riderBuf, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                PlayerController p = riderBuf[i].GetComponentInParent<PlayerController>();
                if (p != null && !list.Contains(p)) list.Add(p);
            }
            return list;
        }

        public void ResetState()
        {
            queue.Clear();
            state = LiftState.Idle;
            velocity = 0f;
            openAmt = 0f;
            jam = 0f;
            dropAtY = float.NaN;
            current = Mathf.Clamp(startLevel, 0, levelY.Length - 1);
            if (car != null)
            {
                float d = levelY[current] - carY;
                car.manual = true;
                car.transform.position += Vector3.up * d;
                Physics.SyncTransforms();
            }
            carY = levelY[current];
            ApplyDoors();
            UpdateIndicators();
        }
    }

    /// <summary>A lift button: a landing call button, a floor button in the car, "open doors", or the alarm.</summary>
    public class LiftButton : Interactable
    {
        public const int OpenDoors = -1, AlarmBell = -2;
        public LiftController lift;
        public int level;
        public bool isCall;
        public Renderer lamp;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        float litUntil;

        public override float HoldTime => 0.1f;

        public override string GetPrompt(IInteractor who)
        {
            if (lift == null) return null;
            if (lift.Broken) return level == AlarmBell ? "Alarm" : "Out of order";
            bool powered = PowerGrid.IsPowered(lift.circuit);
            string text = level == OpenDoors ? (lift.State == LiftState.Stuck ? "Force the doors" : "Open doors")
                        : level == AlarmBell ? "Alarm"
                        : isCall ? "Call lift" : "Floor " + lift.levelNames[level];
            return powered || level == AlarmBell || (level == OpenDoors && lift.State == LiftState.Stuck) ? text : text + " (no power)";
        }

        public override void Interact(IInteractor who)
        {
            if (lift == null) return;
            InteractionFx.Play(ProceduralSounds.Sfx.Click, transform.position, 0.35f, 1.5f, who.Actor);
            if (level == OpenDoors) lift.OpenDoorsButton(who.Actor);
            else if (level == AlarmBell) lift.Alarm(who.Actor);
            else lift.Request(level, who.Actor);
            litUntil = Time.time + 2.5f;
        }

        void Update()
        {
            if (lamp == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            bool lit = Time.time < litUntil && lift != null && PowerGrid.IsPowered(lift.circuit);
            block.SetColor(EmissionId, lit ? new Color(1.6f, 1.2f, 0.4f) : Color.black);
            lamp.SetPropertyBlock(block);
        }
    }
}
