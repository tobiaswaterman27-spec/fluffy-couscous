using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using AfterHours.Core;
using AfterHours.Interaction;
using AfterHours.Player;
using AfterHours.World.Layout;

namespace AfterHours.Creature
{
    public enum ElliotState { Wander, Investigate, Search, Hunt }

    /// <summary>
    /// Elliot's decision making. States are not a fixed sequence; it moves between them as things happen:
    ///
    /// Wander      - roams slowly and unpredictably. At the start it only knows the car park and stays there: it has
    ///               no reason to go upstairs until it hears or sees something. Later it also roams places it has been.
    /// Investigate - heads for where it THINKS a sound came from (maybe the wrong corridor), then looks around.
    /// Search      - believes someone is near: checks points around the area, looks into rooms and behind things,
    ///               sometimes doubles back. Spots behind closed doors seem less likely unless it heard you there.
    /// Hunt        - has seen you. Occasionally stares first; then builds up speed over ~1-2 s and runs. Losing sight
    ///               does not end it: it goes where you were heading, then searches.
    ///
    /// Doors: opens unlocked ones (slams them when hunting), bangs on locked ones, may force them, or gives up and
    /// goes another way. Rare mistakes: hesitating, picking a wrong point, misjudging where a sound came from.
    /// Runs on the host only in co-op (Part 16).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ElliotBrain : MonoBehaviour
    {
        public ElliotMotor motor;
        public ElliotAnimator animator;
        public ElliotSenses senses;
        public ElliotMemory memory;
        public ElliotAudio audioOut;

        [Header("Start area: the car park")]
        public Vector3 startAreaMin = new Vector3(-96f, -4.5f, -96f);
        public Vector3 startAreaMax = new Vector3(96f, -2f, 96f);
        public float catchDistance = 1.15f;

        public ElliotState State => state;
        public PlayerController Target => target;

        NavMeshAgent agent;
        ElliotState state = ElliotState.Wander;
        float stateTime;
        Vector3 goal;
        bool hasGoal;
        ElliotGait gait = ElliotGait.SlowWalk;
        float pauseUntil;
        PlayerController target;
        Vector3 lastKnown, lastKnownVel;
        float lastSeenTime;
        float huntRamp;
        bool leftStartArea;
        readonly List<Vector3> searchPoints = new List<Vector3>();
        int searchIndex;
        Vector3 searchCentre;
        float searchUntil;
        Door doorInWay;
        float doorTime;
        int bangs;
        float hesitateTimer = 8f;
        bool catching;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.radius = 0.35f;
            agent.height = 1.9f;
            agent.autoBraking = true;
        }

        void OnEnable()
        {
            if (senses != null) { senses.Spotted += OnSpotted; senses.Heard += OnHeard; }
        }

        void OnDisable()
        {
            if (senses != null) { senses.Spotted -= OnSpotted; senses.Heard -= OnHeard; }
        }

        void Start()
        {
            memory.RememberPlace(transform.position);
            Enter(ElliotState.Wander);
        }

        // =====================================================================
        // Stimuli
        // =====================================================================

        void OnSpotted(PlayerController p)
        {
            if (catching) return;
            if (state == ElliotState.Hunt && target != null && target != p && CanSeeTarget()) return; // keeps its current prey
            target = p;
            lastKnown = p.transform.position;
            lastSeenTime = Time.time;
            memory.RememberSighting(p, p.transform.position, p.Motor.Locomotion.worldVelocity);
            bool firstTime = state != ElliotState.Hunt;
            Enter(ElliotState.Hunt);
            if (firstTime && Random.value < 0.12f)
            {
                // Rarely: it just stares at you for a few seconds first.
                float stare = Random.Range(1.5f, 3f);
                animator.PlayAction(ElliotAction.Stare, p.EyePosition, stare);
                motor.Freeze(stare);
                huntRamp = -stare;
            }
        }

        void OnHeard(HeardSound s)
        {
            if (catching) return;
            memory.RememberNoise(s.estimatedPosition, s.strength);
            if (state == ElliotState.Hunt)
            {
                // Chasing someone nearby beats a distant noise; only switch if it has lost them and this is loud.
                if (CanSeeTarget() || Time.time - lastSeenTime < 4f || s.strength < 0.6f) return;
            }
            float threshold = state == ElliotState.Wander ? 0.08f : state == ElliotState.Search ? 0.2f : 0.3f;
            if (s.strength < threshold) return;
            Vector3 where = s.estimatedPosition;
            if (Random.value < 0.06f) where += Random.insideUnitSphere * 8f; // rare misjudgement
            where.y = s.estimatedPosition.y;
            goal = where;
            hasGoal = TrySetDestination(goal);
            gait = s.strength > 0.5f ? ElliotGait.FastWalk : ElliotGait.Walk;
            Enter(ElliotState.Investigate, false);
            animator.LookTarget = s.estimatedPosition + Vector3.up;
            leftStartArea |= !InStartArea(where);
        }

        // =====================================================================
        // Update loop
        // =====================================================================

        void Update()
        {
            if (catching) return;
            float dt = Time.deltaTime;
            stateTime += dt;
            agent.nextPosition = transform.position;

            if (state == ElliotState.Hunt) UpdateHunt(dt);
            else if (state == ElliotState.Investigate) UpdateInvestigate();
            else if (state == ElliotState.Search) UpdateSearch();
            else UpdateWander();

            HandleDoors();
            Steer();
            audioOut?.SetIntensity(state == ElliotState.Hunt ? 1f : state == ElliotState.Search ? 0.4f : 0f);
            memory.RememberPlace(transform.position);

            // Rare hesitation (never while hunting someone it can see).
            hesitateTimer -= dt;
            if (hesitateTimer <= 0f)
            {
                hesitateTimer = Random.Range(20f, 60f);
                if (state != ElliotState.Hunt || !CanSeeTarget()) Pause(Random.Range(0.6f, 1.5f));
            }
        }

        void Enter(ElliotState s, bool clearGoal = true)
        {
            state = s;
            stateTime = 0f;
            if (clearGoal) hasGoal = false;
            if (s == ElliotState.Hunt) huntRamp = Mathf.Min(huntRamp, 0f);
            if (s != ElliotState.Hunt) huntRamp = 0f;
        }

        void Pause(float seconds) => pauseUntil = Mathf.Max(pauseUntil, Time.time + seconds);

        bool CanSeeTarget() => target != null && senses.CanSee(target, out _);

        // ---------------- Wander ----------------

        void UpdateWander()
        {
            if (!hasGoal || Arrived())
            {
                if (hasGoal && Random.value < 0.35f)
                {
                    float wait = Random.Range(2f, 7f);
                    Pause(wait);
                    animator.PlayAction(Random.value < 0.6f ? ElliotAction.LookAround : ElliotAction.Stare,
                        transform.position + transform.forward * 5f + Vector3.up * 1.5f, wait);
                }
                hasGoal = TrySetDestination(PickWanderPoint());
                gait = Random.value < 0.6f ? ElliotGait.SlowWalk : ElliotGait.Walk;
            }
            animator.LookTarget = null;
        }

        Vector3 PickWanderPoint()
        {
            // Mostly the car park until it has a reason to go elsewhere; later, also places it has been.
            if (leftStartArea && memory.Territory.Count > 0 && Random.value < 0.4f)
            {
                Vector3 t = memory.Territory[Random.Range(0, memory.Territory.Count)];
                return t + new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
            }
            return new Vector3(Random.Range(startAreaMin.x, startAreaMax.x), MallLayout.CarParkY, Random.Range(startAreaMin.z, startAreaMax.z));
        }

        bool InStartArea(Vector3 p) => p.x > startAreaMin.x && p.x < startAreaMax.x && p.z > startAreaMin.z && p.z < startAreaMax.z && p.y < startAreaMax.y;

        // ---------------- Investigate ----------------

        void UpdateInvestigate()
        {
            if (!hasGoal || Arrived() || stateTime > 45f)
            {
                float look = Random.Range(2f, 4f);
                animator.PlayAction(ElliotAction.LookAround, goal + Vector3.up * 1.5f, look);
                Pause(look);
                BeginSearch(goal, 12f, Random.Range(18f, 35f));
            }
        }

        // ---------------- Search ----------------

        void BeginSearch(Vector3 centre, float radius, float duration)
        {
            Enter(ElliotState.Search);
            searchCentre = centre;
            searchUntil = Time.time + duration;
            searchPoints.Clear();
            searchIndex = 0;

            // Remembered hiding places first, then likely spots around the area.
            searchPoints.AddRange(memory.HidingSpotsNear(centre, radius * 1.5f));
            var candidates = new List<(Vector3 p, float w)>();
            for (int i = 0; i < 12; i++)
            {
                Vector3 c = centre + new Vector3(Random.Range(-radius, radius), 0f, Random.Range(-radius, radius));
                if (!NavMesh.SamplePosition(c, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                float w = 1f + memory.NoiseHeatAt(hit.position);
                // Behind a closed door? Less likely you went there (unless it heard you there).
                if (ClosedDoorBetween(centre, hit.position) && memory.NoiseHeatAt(hit.position) < 0.3f) w *= 0.3f;
                candidates.Add((hit.position, w));
            }
            candidates.Sort((a, b) => b.w.CompareTo(a.w));
            for (int i = 0; i < Mathf.Min(5, candidates.Count); i++) searchPoints.Add(candidates[i].p);
            // Sometimes it doubles back to where it started looking.
            if (Random.value < 0.3f) searchPoints.Add(centre);
            gait = ElliotGait.Walk;
        }

        void UpdateSearch()
        {
            if (Time.time > searchUntil || searchIndex >= searchPoints.Count)
            {
                Enter(ElliotState.Wander);
                return;
            }
            if (!hasGoal)
            {
                Vector3 p = searchPoints[searchIndex];
                if (Random.value < 0.05f) p += Random.insideUnitSphere * 10f; // rare wrong turn
                hasGoal = TrySetDestination(p);
                gait = Random.value < 0.5f ? ElliotGait.Walk : ElliotGait.FastWalk;
                if (!hasGoal) searchIndex++;
            }
            else if (Arrived())
            {
                float look = Random.Range(1.5f, 3.5f);
                animator.PlayAction(Random.value < 0.5f ? ElliotAction.LookAround : ElliotAction.InspectLow,
                    transform.position + transform.forward * 1.2f + Vector3.up * 0.5f, look);
                Pause(look);
                searchIndex++;
                hasGoal = false;
            }
        }

        bool ClosedDoorBetween(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            if (Physics.Raycast(a + Vector3.up * 1.2f, d.normalized, out RaycastHit hit, d.magnitude, 1 << Mathf.Max(0, GameLayers.InteractableLayer), QueryTriggerInteraction.Ignore))
            {
                Door door = hit.collider.GetComponentInParent<Door>();
                return door != null && !door.IsOpen;
            }
            return false;
        }

        // ---------------- Hunt ----------------

        void UpdateHunt(float dt)
        {
            if (target == null) { Enter(ElliotState.Wander); return; }
            huntRamp += dt;
            bool sees = CanSeeTarget();
            if (sees)
            {
                lastKnown = target.transform.position;
                lastKnownVel = target.Motor.Locomotion.worldVelocity;
                lastSeenTime = Time.time;
                if (Random.value < dt * 2f) memory.RememberSighting(target, lastKnown, lastKnownVel);
            }

            // Where to go: straight at them if visible; otherwise where they seemed to be heading.
            float lost = Time.time - lastSeenTime;
            Vector3 dest = sees ? lastKnown : lastKnown + Vector3.ClampMagnitude(lastKnownVel * Mathf.Min(lost, 1.5f), 6f);
            if (!hasGoal || (agent.destination - dest).sqrMagnitude > 1f) hasGoal = TrySetDestination(dest);

            // Gradual start: about 1-2 seconds to reach full speed.
            gait = ElliotGait.Run;
            animator.LookTarget = sees ? target.EyePosition : lastKnown + Vector3.up * 1.5f;

            float dist = Vector3.Distance(Flat(transform.position), Flat(target.transform.position));
            if (sees && dist < catchDistance && Mathf.Abs(target.transform.position.y - transform.position.y) < 1.4f)
            {
                Catch(target);
                return;
            }

            if (!sees && lost > 3f && (Arrived() || lost > 12f))
            {
                // Lost them: search around where they were last seen (with remembered hiding places).
                BeginSearch(lastKnown, 15f, Random.Range(30f, 50f));
                target = null;
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // =====================================================================
        // Movement along the path
        // =====================================================================

        bool TrySetDestination(Vector3 p)
        {
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit me, 3f, NavMesh.AllAreas)) agent.Warp(me.position);
                else return false;
            }
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return false;
            goal = hit.position;
            return agent.SetDestination(goal);
        }

        bool Arrived()
        {
            if (!hasGoal || agent.pathPending) return false;
            return agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 1.2f);
        }

        void Steer()
        {
            bool paused = Time.time < pauseUntil || doorInWay != null;
            if (paused || !hasGoal || agent.pathPending)
            {
                motor.Drive(Vector3.zero, paused ? ElliotGait.Idle : gait);
                return;
            }
            Vector3 dir = agent.steeringTarget - transform.position;
            dir.y = 0f;
            float factor = 1f;
            if (state == ElliotState.Hunt) factor = Mathf.Clamp01(0.25f + huntRamp / 1.6f);
            motor.Drive(dir, gait, factor);
            motor.Face(null);
        }

        // =====================================================================
        // Doors
        // =====================================================================

        void HandleDoors()
        {
            if (doorInWay == null)
            {
                if (!hasGoal || motor.Speed < 0.05f && Time.time < pauseUntil) return;
                Vector3 ahead = (agent.steeringTarget - transform.position);
                ahead.y = 0f;
                if (ahead.sqrMagnitude < 0.01f) return;
                if (!Physics.Raycast(transform.position + Vector3.up * 1.2f, ahead.normalized, out RaycastHit hit, 1.3f,
                        1 << Mathf.Max(0, GameLayers.InteractableLayer), QueryTriggerInteraction.Ignore)) return;
                Door door = hit.collider.GetComponentInParent<Door>();
                if (door == null || door.IsOpen) return;
                doorInWay = door;
                doorTime = 0f;
                bangs = 0;
                motor.Face(door.transform.position);
            }

            doorTime += Time.deltaTime;
            Door d = doorInWay;
            if (d == null || d.IsOpen && !d.IsMoving) { doorInWay = null; motor.Face(null); return; }
            if (d.IsMoving) return;

            if (!d.locked)
            {
                if (doorTime < 0.15f) animator.PlayAction(ElliotAction.ReachHandle, d.leaf != null ? d.leaf.position : d.transform.position, 0.7f);
                if (doorTime > 0.5f) d.Open(transform.position, state == ElliotState.Hunt ? 2.2f : 0.9f, gameObject);
                return;
            }

            // Locked: bang on it; when hunting it may force it; otherwise it soon gives up.
            int maxBangs = state == ElliotState.Hunt ? 4 : 1;
            if (doorTime > 1.2f * (bangs + 1))
            {
                bangs++;
                animator.PlayAction(ElliotAction.BangDoor, d.transform.position + Vector3.up * 1.4f, 1.1f);
                d.Bang(gameObject);
                if (state == ElliotState.Hunt && bangs >= 2 && d.forceable && Random.value < 0.4f)
                {
                    d.Force(transform.position, gameObject);
                    return;
                }
                if (bangs >= maxBangs)
                {
                    memory.MarkBlocked(d);
                    doorInWay = null;
                    motor.Face(null);
                    if (state == ElliotState.Hunt) BeginSearch(transform.position, 18f, 25f); // look for another way round
                    else hasGoal = false;
                }
            }
        }

        // =====================================================================
        // Catching (placeholder until Part 11's full grab / scream / eat sequence)
        // =====================================================================

        void Catch(PlayerController p)
        {
            catching = true;
            motor.Drive(Vector3.zero, ElliotGait.Idle);
            animator.PlayAction(ElliotAction.Grab, p.EyePosition, 1.2f);
            CatchHandler.Begin(this, p);
        }

        /// <summary>Back to the car park, unaware, memories wiped (single-player run reset).</summary>
        public void ResetToStart(Vector3 position, float yaw)
        {
            catching = false;
            target = null;
            doorInWay = null;
            leftStartArea = false;
            memory.Forget();
            motor.Warp(position, yaw);
            if (agent.isOnNavMesh) agent.Warp(position);
            animator.StopAction();
            Enter(ElliotState.Wander);
        }
    }
}
EOF
echo ok