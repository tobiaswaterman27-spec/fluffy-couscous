using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using AfterHours.Core;
using AfterHours.Hiding;
using AfterHours.Interaction;
using AfterHours.Player;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.Creature
{
    public enum ElliotState { Wander, Investigate, Search, Hunt, CheckHidingSpot, CheckVent, CrawlVent }

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
    /// CheckHidingSpot - goes to a locker / cupboard. If it SAW you get in, it runs straight there and rips it open.
    ///               Otherwise it stands outside, completely still, listening... then may open it, or walk away
    ///               (and sometimes comes back a little later). Worse hiding places get checked more often.
    /// CheckVent   - peers into a vent opening, may pull the cover off and reach in (it can grab you if you're close).
    /// CrawlVent   - rarely, on the ground floor, it squeezes into the duct after you on all fours.
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
        bool stillPause;

        // hiding places & vents
        HidingSpot checkSpot;
        VentCover checkVent;
        VentDuct crawlDuct;
        bool checkCertain;
        int checkStep;
        float stepTime, listenTime, waitOutside;
        HidingSpot returnSpot;
        float returnAt;
        Vector3 crawlEntry, crawlGoal;
        float crawlUntil;
        readonly Dictionary<Object, float> recentlyChecked = new Dictionary<Object, float>();
        readonly List<Object> searchTargets = new List<Object>();

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
            HidingSpot.SomeoneHid += OnSomeoneHid;
        }

        void OnDisable()
        {
            if (senses != null) { senses.Spotted -= OnSpotted; senses.Heard -= OnHeard; }
            HidingSpot.SomeoneHid -= OnSomeoneHid;
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
            // Focused on a hiding place / vent: a sound from right there makes it certain; others are ignored unless loud.
            if (state == ElliotState.CheckHidingSpot && checkSpot != null)
            {
                if (Vector3.Distance(s.estimatedPosition, checkSpot.transform.position) < 3f) { checkCertain = true; return; }
                if (s.strength < 0.6f) return;
            }
            if ((state == ElliotState.CheckVent || state == ElliotState.CrawlVent) && VentDuct.Contains(s.estimatedPosition))
            {
                checkCertain = true;
                crawlGoal = s.estimatedPosition;
                return;
            }
            if (state == ElliotState.CrawlVent) return;
            if (state == ElliotState.Hunt)
            {
                // Chasing someone nearby beats a distant noise; only switch if it has lost them and this is loud.
                if (CanSeeTarget() || Time.time - lastSeenTime < 4f || s.strength < 0.6f) return;
            }
            float threshold = state == ElliotState.Wander ? 0.08f : state == ElliotState.Search ? 0.2f : 0.3f;
            if (s.strength < threshold) return;
            // A noise in the ducts: go and look into the nearest vent opening.
            if (VentDuct.Contains(s.estimatedPosition))
            {
                VentCover v = NearestVent(s.estimatedPosition, 12f);
                if (v != null && !RecentlyChecked(v, 20f))
                {
                    StartCheckVent(v, s.strength > 0.55f);
                    return;
                }
            }
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
            else if (state == ElliotState.CheckHidingSpot) UpdateCheckSpot(dt);
            else if (state == ElliotState.CheckVent) UpdateCheckVent(dt);
            else if (state == ElliotState.CrawlVent) UpdateCrawl(dt);
            else UpdateWander();
            if (catching) return;

            if (state != ElliotState.CrawlVent)
            {
                HandleDoors();
                Steer();
            }
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
            stillPause = false;
            if (clearGoal) hasGoal = false;
            if (s == ElliotState.Hunt) huntRamp = Mathf.Min(huntRamp, 0f);
            if (s != ElliotState.Hunt) huntRamp = 0f;
        }

        void Pause(float seconds) => pauseUntil = Mathf.Max(pauseUntil, Time.time + seconds);

        bool CanSeeTarget() => target != null && senses.CanSee(target, out _);

        // ---------------- Wander ----------------

        bool TryReturnToSpot()
        {
            if (returnSpot == null || Time.time < returnAt) return false;
            HidingSpot s = returnSpot;
            returnSpot = null;
            StartCheckSpot(s, false);
            return true;
        }

        void UpdateWander()
        {
            if (TryReturnToSpot()) return;
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
            searchTargets.Clear();
            searchIndex = 0;

            // Remembered hiding places first, then likely spots around the area.
            foreach (Vector3 remembered in memory.HidingSpotsNear(centre, radius * 1.5f))
            {
                HidingSpot hs = NearestSpot(remembered, 1.5f);
                searchPoints.Add(hs != null ? SpotStand(hs) : remembered);
                searchTargets.Add(hs);
            }
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
            for (int i = 0; i < Mathf.Min(5, candidates.Count); i++) { searchPoints.Add(candidates[i].p); searchTargets.Add(null); }

            // Hiding places and vents nearby, mixed in. Closed doors look less likely; bad hiding places more likely.
            foreach (HidingSpot hs in HidingSpot.All)
            {
                if (searchTargets.Contains(hs) || !hs.Closed || RecentlyChecked(hs, 60f)) continue;
                Vector3 hp = hs.transform.position;
                if (Mathf.Abs(hp.y - centre.y) > 2.5f || Vector3.Distance(hp, centre) > radius * 1.2f) continue;
                float chance = 0.15f + 0.35f * (1f - hs.quality) + Mathf.Min(0.3f, memory.NoiseHeatAt(hp) * 0.4f);
                if (Random.value < chance) InsertSearchTarget(SpotStand(hs), hs);
            }
            foreach (VentCover v in VentCover.All)
            {
                if (RecentlyChecked(v, 60f)) continue;
                Vector3 vp = v.SlotPosition;
                if (Mathf.Abs(vp.y - centre.y) > 4f || Vector3.Distance(vp, centre) > radius) continue;
                float chance = (v.IsOpen ? 0.2f : 0.08f) + Mathf.Min(0.3f, memory.NoiseHeatAt(vp) * 0.4f);
                if (Random.value < chance) InsertSearchTarget(VentStand(v), v);
            }
            // Sometimes it doubles back to where it started looking.
            if (Random.value < 0.3f) { searchPoints.Add(centre); searchTargets.Add(null); }
            gait = ElliotGait.Walk;
        }

        void InsertSearchTarget(Vector3 p, Object what)
        {
            int at = Random.Range(0, searchPoints.Count + 1);
            searchPoints.Insert(at, p);
            searchTargets.Insert(at, what);
        }

        void UpdateSearch()
        {
            if (TryReturnToSpot()) return;
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
                Object what = searchIndex < searchTargets.Count ? searchTargets[searchIndex] : null;
                searchIndex++;
                if (what is HidingSpot hs && hs != null && hs.Closed) { ResumeSearchAfterCheck(); StartCheckSpot(hs, false); return; }
                if (what is VentCover vc && vc != null) { ResumeSearchAfterCheck(); StartCheckVent(vc, false); return; }
                searchIndex--;
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

            if (!sees && lost > 0.6f && VentDuct.Contains(lastKnown + Vector3.up * 0.3f))
            {
                // Saw them disappear into the vents.
                VentCover v = NearestVent(lastKnown, 8f);
                if (v != null) { StartCheckVent(v, true); return; }
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
                motor.Drive(Vector3.zero, paused ? (stillPause ? ElliotGait.Still : ElliotGait.Idle) : gait);
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
        // Hiding places
        // =====================================================================

        void OnSomeoneHid(HidingSpot spot, PlayerController p)
        {
            if (catching || spot == null || p == null) return;
            bool saw = senses.CanSee(p, out _) || (state == ElliotState.Hunt && target == p && Time.time - lastSeenTime < 2.5f);
            if (!saw) return;
            memory.RememberHidingSpot(spot.transform.position);
            target = p;
            StartCheckSpot(spot, true);
        }

        static Vector3 SpotStand(HidingSpot s)
        {
            Vector3 o = s.outsidePoint - s.insidePoint;
            o.y = 0f;
            return s.outsidePoint + o.normalized * 0.45f;
        }

        HidingSpot NearestSpot(Vector3 p, float within)
        {
            HidingSpot best = null;
            float bd = within;
            foreach (HidingSpot s in HidingSpot.All)
            {
                float d = Vector3.Distance(s.transform.position, p);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        bool RecentlyChecked(Object o, float seconds) => recentlyChecked.TryGetValue(o, out float t) && Time.time - t < seconds;

        bool resumeSearch;
        void ResumeSearchAfterCheck() => resumeSearch = true;

        void StartCheckSpot(HidingSpot spot, bool certain)
        {
            Enter(ElliotState.CheckHidingSpot);
            checkSpot = spot;
            checkVent = null;
            checkCertain = certain;
            checkStep = 0;
            stepTime = 0f;
            recentlyChecked[spot] = Time.time;
            hasGoal = TrySetDestination(SpotStand(spot));
            gait = certain ? ElliotGait.Run : (Random.value < 0.5f ? ElliotGait.Walk : ElliotGait.FastWalk);
            if (certain) huntRamp = 1f;
            if (!hasGoal) EndCheck();
        }

        float OpenChance(HidingSpot s)
        {
            float c = 0.3f + 0.35f * (1f - s.quality) + Mathf.Min(0.35f, memory.NoiseHeatAt(s.transform.position) * 0.5f);
            if (memory.HidingSpotsNear(s.transform.position, 1.5f).Count > 0) c += 0.3f;
            return Mathf.Clamp01(c);
        }

        void UpdateCheckSpot(float dt)
        {
            if (checkSpot == null) { EndCheck(); return; }
            stepTime += dt;
            Vector3 front = checkSpot.insidePoint + Vector3.up * 1.3f;
            animator.LookTarget = front;
            switch (checkStep)
            {
                case 0: // going there
                    if (Arrived() || Vector3.Distance(Flat(transform.position), Flat(SpotStand(checkSpot))) < 0.7f)
                    {
                        checkStep = 1;
                        stepTime = 0f;
                        hasGoal = false;
                        motor.Face(front);
                        listenTime = checkCertain ? 0.25f : Random.Range(1.5f, 4.5f);
                        stillPause = !checkCertain;
                        Pause(listenTime);
                        if (!checkCertain) animator.PlayAction(ElliotAction.Stare, front, listenTime);
                    }
                    else if (stepTime > 25f) EndCheck();
                    break;
                case 1: // standing outside, listening
                    if (stepTime < listenTime) break;
                    stillPause = false;
                    if (!checkSpot.Closed)
                    {
                        // Door open: it can see inside.
                        animator.PlayAction(ElliotAction.InspectLow, front, 1.2f);
                        Pause(1.2f);
                        if (checkSpot.Occupant != null) { Catch(checkSpot.Occupant); return; }
                        checkStep = 3;
                        stepTime = 0f;
                        waitOutside = 0f;
                        break;
                    }
                    if (checkCertain || Random.value < OpenChance(checkSpot))
                    {
                        animator.PlayAction(ElliotAction.PullOpen, checkSpot.outsidePoint + Vector3.up * 1.1f, 0.9f);
                        Pause(0.9f);
                        checkStep = 2;
                        stepTime = 0f;
                    }
                    else
                    {
                        // Walks away... and sometimes comes back.
                        if (Random.value < 0.3f) { returnSpot = checkSpot; returnAt = Time.time + Random.Range(8f, 25f); }
                        EndCheck();
                    }
                    break;
                case 2: // yanks it open
                    if (stepTime < 0.3f) break;
                    PlayerController occ = checkSpot.Occupant;
                    if (occ != null) { Catch(occ); return; }
                    checkSpot.RipOpen(gameObject);
                    animator.PlayAction(ElliotAction.InspectLow, front, 1.5f);
                    Pause(1.5f);
                    checkStep = 3;
                    stepTime = 0f;
                    waitOutside = Random.value < 0.25f ? Random.Range(5f, 10f) : 0f;
                    break;
                case 3: // empty. Sometimes it just stands there a while, facing away.
                    if (stepTime < 1.5f + waitOutside)
                    {
                        if (waitOutside > 0f && stepTime > 1.5f)
                        {
                            stillPause = true;
                            Pause(0.2f);
                            motor.Face(transform.position - (front - transform.position));
                        }
                        break;
                    }
                    EndCheck();
                    break;
            }
        }

        void EndCheck()
        {
            Vector3 c = checkSpot != null ? checkSpot.transform.position : checkVent != null ? checkVent.SlotPosition : transform.position;
            checkSpot = null;
            checkVent = null;
            stillPause = false;
            motor.Face(null);
            if (resumeSearch && Time.time < searchUntil && searchIndex < searchPoints.Count)
            {
                resumeSearch = false;
                Enter(ElliotState.Search);
                return;
            }
            resumeSearch = false;
            BeginSearch(c, 10f, Random.Range(12f, 25f));
        }

        // =====================================================================
        // Vents
        // =====================================================================

        static Vector3 VentStand(VentCover v)
        {
            Vector3 p = v.SlotPosition + v.Outward * 0.9f;
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 3.5f, NavMesh.AllAreas)) return hit.position;
            return p;
        }

        VentCover NearestVent(Vector3 p, float within)
        {
            VentCover best = null;
            float bd = within;
            foreach (VentCover v in VentCover.All)
            {
                float d = Vector3.Distance(v.SlotPosition, p);
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }

        void StartCheckVent(VentCover v, bool certain)
        {
            Enter(ElliotState.CheckVent);
            checkVent = v;
            checkSpot = null;
            checkCertain = certain;
            checkStep = 0;
            stepTime = 0f;
            crawlGoal = v.SlotPosition - v.Outward * 1.5f;
            recentlyChecked[v] = Time.time;
            hasGoal = TrySetDestination(VentStand(v));
            gait = certain ? ElliotGait.Run : ElliotGait.Walk;
            if (!hasGoal) EndCheck();
        }

        PlayerController PlayerInDuctNear(Vector3 p, float within)
        {
            foreach (PlayerController pc in PlayerController.All)
            {
                if (pc == null || CatchSequence.IsBeingCaught(pc)) continue;
                Vector3 at = pc.transform.position + Vector3.up * 0.3f;
                if (VentDuct.Contains(at) && Vector3.Distance(at, p) < within) return pc;
            }
            return null;
        }

        void UpdateCheckVent(float dt)
        {
            if (checkVent == null) { EndCheck(); return; }
            stepTime += dt;
            Vector3 slot = checkVent.SlotPosition;
            animator.LookTarget = slot;
            switch (checkStep)
            {
                case 0:
                    if (Arrived() || Vector3.Distance(Flat(transform.position), Flat(VentStand(checkVent))) < 0.7f)
                    {
                        checkStep = 1;
                        stepTime = 0f;
                        hasGoal = false;
                        motor.Face(slot);
                        listenTime = checkCertain ? 0.4f : Random.Range(2f, 5f);
                        stillPause = true;
                        Pause(listenTime);
                        animator.PlayAction(ElliotAction.InspectLow, slot, listenTime);
                    }
                    else if (stepTime > 25f) EndCheck();
                    break;
                case 1:
                    if (stepTime < listenTime) break;
                    stillPause = false;
                    if (!checkVent.IsOpen)
                    {
                        if (checkCertain || Random.value < 0.25f + Mathf.Min(0.4f, memory.NoiseHeatAt(slot) * 0.5f))
                        {
                            animator.PlayAction(ElliotAction.PullOpen, slot, 0.8f);
                            checkVent.Remove(transform.position, gameObject);
                            Pause(0.8f);
                        }
                        else { EndCheck(); break; }
                    }
                    checkStep = 2;
                    stepTime = 0f;
                    break;
                case 2:
                    if (stepTime < 0.7f) break;
                    {
                        // Long arms reach into the duct.
                        animator.PlayAction(ElliotAction.InspectLow, slot - checkVent.Outward * 0.6f, 1.6f);
                        PlayerController p = PlayerInDuctNear(slot, 2.4f);
                        if (p != null) { Catch(p); return; }
                        VentDuct duct = VentDuct.Find(slot - checkVent.Outward * 0.7f);
                        bool heard = checkCertain || memory.NoiseHeatAt(slot) > 0.4f;
                        if (duct != null && duct.creatureSized && heard && Random.value < 0.3f)
                        {
                            StartCrawl(duct);
                            return;
                        }
                        Pause(Random.Range(1.6f, 3.5f));
                        stillPause = true;
                        checkStep = 3;
                        stepTime = 0f;
                    }
                    break;
                case 3:
                    if (stepTime < 2f) break;
                    EndCheck();
                    break;
            }
        }

        // Crawling inside a duct: no navmesh in there, it just follows the duct on all fours.
        int crawlStep;

        void StartCrawl(VentDuct duct)
        {
            Enter(ElliotState.CrawlVent);
            crawlDuct = duct;
            crawlEntry = transform.position;
            crawlUntil = Time.time + Random.Range(10f, 18f);
            crawlStep = 0;
            stepTime = 0f;
            hasGoal = false;
        }

        void UpdateCrawl(float dt)
        {
            stepTime += dt;
            if (crawlDuct == null || checkVent == null) { crawlStep = 2; }
            Vector3 slot = checkVent != null ? checkVent.SlotPosition : crawlEntry;
            Vector3 inward = checkVent != null ? -checkVent.Outward : transform.forward;
            Vector3 aim;
            switch (crawlStep)
            {
                case 0: // into the opening
                    aim = slot + inward * 0.9f;
                    if (crawlDuct.ContainsPoint(transform.position + Vector3.up * 0.3f)) { crawlStep = 1; stepTime = 0f; }
                    else if (stepTime > 5f) { crawlStep = 2; stepTime = 0f; } // can't get in
                    break;
                case 1: // along the duct towards the last sound
                {
                    Vector3 axis = crawlDuct.transform.forward;
                    Vector3 c = crawlDuct.transform.position;
                    float along = Vector3.Dot(crawlGoal - c, axis);
                    aim = c + axis * along;
                    aim.y = transform.position.y;
                    PlayerController p = PlayerInDuctNear(transform.position, catchDistance + 0.3f);
                    if (p != null) { Catch(p); return; }
                    if (Time.time > crawlUntil || Vector3.Distance(Flat(aim), Flat(transform.position)) < 0.5f && stepTime > 3f)
                    {
                        crawlStep = 2;
                        stepTime = 0f;
                    }
                    break;
                }
                default: // back out
                    aim = crawlDuct != null && crawlDuct.ContainsPoint(transform.position + Vector3.up * 0.3f) ? slot : crawlEntry;
                    if (!VentDuct.Contains(transform.position + Vector3.up * 0.3f) && Vector3.Distance(Flat(transform.position), Flat(crawlEntry)) < 0.8f || stepTime > 25f)
                    {
                        crawlDuct = null;
                        EndCheck();
                        return;
                    }
                    break;
            }
            Vector3 dir = aim - transform.position;
            dir.y = 0f;
            motor.Drive(dir.sqrMagnitude > 0.04f ? dir : Vector3.zero, ElliotGait.Crawl);
            motor.Face(null);
            animator.LookTarget = transform.position + transform.forward * 3f + Vector3.up * 0.4f;
        }

        // =====================================================================
        // Catching: the grab / scream / eat sequence (CatchSequence)
        // =====================================================================

        void Catch(PlayerController p)
        {
            if (CatchSequence.IsBeingCaught(p)) return;
            catching = true;
            hasGoal = false;
            motor.Drive(Vector3.zero, ElliotGait.Idle);
            motor.Face(null);
            CatchContext ctx = CatchSequence.Classify(this, p);
            CatchSequence.Begin(this, p, ctx, Random.Range(1, 1 << 30));
        }

        /// <summary>LAN co-op: a player was caught and respawned elsewhere; carry on hunting the others.</summary>
        public void EndCatchCoop()
        {
            catching = false;
            target = null;
            animator.Puppet = null;
            BeginSearch(transform.position, 20f, Random.Range(20f, 35f));
        }

        /// <summary>Back to the car park, unaware, memories wiped (single-player run reset).</summary>
        public void ResetToStart(Vector3 position, float yaw)
        {
            catching = false;
            target = null;
            doorInWay = null;
            checkSpot = null;
            checkVent = null;
            crawlDuct = null;
            returnSpot = null;
            resumeSearch = false;
            recentlyChecked.Clear();
            stillPause = false;
            motor.Face(null);
            leftStartArea = false;
            memory.Forget();
            motor.Warp(position, yaw);
            if (agent.isOnNavMesh) agent.Warp(position);
            animator.StopAction();
            Enter(ElliotState.Wander);
        }
    }
}
