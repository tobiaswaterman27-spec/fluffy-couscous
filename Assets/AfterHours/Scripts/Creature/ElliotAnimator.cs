using UnityEngine;
using AfterHours.Core;
using AfterHours.World;

namespace AfterHours.Creature
{
    /// <summary>
    /// Procedural animation for Elliot's skeleton (no animation files needed).
    ///
    /// - Feet really plant on the ground (they never slide) and step to new footholds found by ray casts, so it
    ///   walks properly up stairs and over uneven floors. Each footstep sound fires the moment a foot lands.
    /// - Legs and crawling arms use two-bone IK (hip/knee/ankle, shoulder/elbow/hand).
    /// - Gaits blend smoothly: still, idle breathing, slow walk, walk, fast walk, a hunched forward-leaning sprint,
    ///   and crawling on all fours.
    /// - It is human-like but wrong: overlong limbs, a head that turns much too far, sudden twitches, and walking
    ///   that occasionally hitches and lurches.
    /// - Actions (reach for a handle, bang on a door, inspect, pull open, grab, scream, stare) blend over the gait.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ElliotAnimator : MonoBehaviour
    {
        [Header("Skeleton (set by the factory)")]
        public Transform pelvis, spine, neck, head, jaw;
        public Transform upperArmL, foreArmL, handL, upperArmR, foreArmR, handR;
        public Transform thighL, shinL, footL, thighR, shinR, footR;
        public ElliotMotor motor;
        public ElliotAudio audioOut;

        // Detail joints from the sculpted body (ElliotBody): driven by his voice. Found by name when the body exists.
        Transform chestJ, throatJ, lipJ, noseL, noseR, browL, browR, tongueJ;
        readonly Transform[,] fingerJ = new Transform[2, 15];   // per side: 4 fingers x 3 joints, then the thumb's 2
        readonly Quaternion[,] fingerRest = new Quaternion[2, 15];
        Vector3 lipRest, noseLRest, noseRRest, browLRest, browRRest, throatRest, tongueRest;
        Quaternion tongueRot;
        bool detailReady;
        VoicePose vp;
        /// <summary>What his voice is doing to his body this frame (for other systems, e.g. network or debug).</summary>
        public VoicePose Voice => vp;

        [Header("Proportions (metres)")]
        public float hipHeight = 1.32f;
        public float thigh = 0.68f, shin = 0.66f, ankle = 0.06f;
        public float spineLength = 0.62f, neckLength = 0.2f;
        public float shoulderHalfWidth = 0.21f, hipHalfWidth = 0.12f;
        public float upperArm = 0.56f, foreArm = 0.56f;

        public Vector3? LookTarget { get; set; }

        /// <summary>
        /// Full-body override used by the catch sequence (a scripted, deterministic pose so every player sees the
        /// same thing). Hands go to these world points, the head pushes / shakes, the jaw is set directly.
        /// </summary>
        public class PuppetPose
        {
            public float weight;
            public Vector3 handL, handR;
            public Vector3 look;
            public float jaw = -1f;        // degrees; < 0 = automatic
            public float lean;             // extra forward bend (degrees)
            public Vector3 headPush;       // world offset (lunging bite)
            public float headShake;        // degrees of violent side-to-side thrash
        }
        public PuppetPose Puppet { get; set; }

        /// <summary>Roughly where the mouth is (for bites, blood, camera framing).</summary>
        public Vector3 MouthPosition => head != null ? head.position + head.up * 0.075f + head.forward * 0.1f : transform.position + Vector3.up * 2.2f;
        public ElliotAction CurrentAction => action;
        public Vector3 ActionTarget => actionTarget;
        public float ActionRemaining => Mathf.Max(0f, actionDuration - actionTime);

        // gait state
        float phase;
        float runW, crawlW, moveW, stillW;
        readonly Vector3[] footPos = new Vector3[2];
        readonly Vector3[] footFrom = new Vector3[2];
        readonly Vector3[] footTo = new Vector3[2];
        readonly bool[] swinging = new bool[2];
        readonly Vector3[] handPlant = new Vector3[2];
        readonly Vector3[] handFrom = new Vector3[2];
        readonly Vector3[] handTo = new Vector3[2];
        readonly bool[] handSwinging = new bool[2];
        bool initialised;

        // head & quirks
        float headYaw, headPitch, headRoll;
        float twitchTimer = 3f, twitchT = -1f;
        Vector3 twitchOffset;
        float hitchTimer = 6f, hitchT = -1f;
        float jawOpen;

        // actions
        ElliotAction action;
        Vector3 actionTarget;
        float actionTime, actionDuration, actionW;

        int groundMask;

        void Start()
        {
            int mask = GameLayers.CharacterBlockingMask;
            if (GameLayers.CreatureLayer >= 0) mask &= ~(1 << GameLayers.CreatureLayer);
            if (GameLayers.PlayerLayer >= 0) mask &= ~(1 << GameLayers.PlayerLayer);
            if (GameLayers.PropLayer >= 0) mask &= ~(1 << GameLayers.PropLayer);
            groundMask = mask;
        }

        /// <summary>Plays a timed action. target: where it reaches / looks.</summary>
        public void PlayAction(ElliotAction a, Vector3 target, float duration)
        {
            action = a;
            actionTarget = target;
            actionTime = 0f;
            actionDuration = Mathf.Max(0.2f, duration);
            if (a == ElliotAction.Scream && audioOut != null) audioOut.Scream();
        }

        public void StopAction() => action = ElliotAction.None;

        void Init()
        {
            Vector3 right = transform.right;
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                footPos[i] = Ground(transform.position + right * (hipHalfWidth * s));
                handPlant[i] = Ground(transform.position + transform.forward * 0.9f + right * (shoulderHalfWidth * s));
            }
            initialised = true;
        }

        Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out RaycastHit hit, 3f, groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return new Vector3(p.x, transform.position.y, p.z);
        }

        SurfaceType SurfaceAt(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 0.8f, groundMask, QueryTriggerInteraction.Ignore))
            {
                return SurfaceLookup.Get(hit.collider);
            }
            return SurfaceType.Concrete;
        }

        void LateUpdate()
        {
            if (motor == null || pelvis == null) return;
            if (!initialised) Init();
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            SampleVoice(dt);
            ElliotGait gait = motor.Gait;
            float speed = motor.Speed;
            Vector3 fwd = transform.forward, right = transform.right, up = Vector3.up;
            Vector3 root = transform.position;

            // ---------------- blend weights ----------------
            float k = 1f - Mathf.Exp(-5f * dt);
            runW = Mathf.Lerp(runW, Mathf.InverseLerp(motor.fastWalkSpeed, motor.runSpeed, speed), k);
            crawlW = Mathf.Lerp(crawlW, gait == ElliotGait.Crawl ? 1f : 0f, 1f - Mathf.Exp(-3f * dt));
            moveW = Mathf.Lerp(moveW, Mathf.Clamp01(speed / 0.4f), k);
            stillW = Mathf.Lerp(stillW, gait == ElliotGait.Still ? 1f : 0f, k);
            actionTime += dt;
            if (action != ElliotAction.None && actionTime > actionDuration) action = ElliotAction.None;
            actionW = Mathf.MoveTowards(actionW, action != ElliotAction.None ? 1f : 0f, dt / 0.25f);

            // ---------------- gait phase (distance travelled) ----------------
            float stride = Mathf.Lerp(Mathf.Lerp(1.0f, 2.7f, runW), Mathf.Lerp(0.7f, 1.05f, Mathf.Clamp01(speed / 3.4f)), crawlW);
            float phaseSpeed = speed / stride;
            // Occasional hitch: it stalls mid-step, then lurches to catch up (jerky, unnatural).
            hitchTimer -= dt;
            if (hitchTimer <= 0f && speed > 0.3f && runW < 0.5f) { hitchT = 0f; hitchTimer = Random.Range(5f, 14f); }
            if (hitchT >= 0f)
            {
                hitchT += dt;
                phaseSpeed *= hitchT < 0.25f ? 0.1f : 2.2f;
                if (hitchT > 0.5f) hitchT = -1f;
            }
            // When standing, keep stepping until both feet are back under the body.
            if (speed < 0.1f)
            {
                bool settled = true;
                for (int i = 0; i < 2; i++)
                {
                    Vector3 rest = root + right * (hipHalfWidth * (i == 0 ? -1f : 1f));
                    if (Vector3.Distance(Flat(footPos[i]), Flat(rest)) > 0.22f || swinging[i]) settled = false;
                }
                if (!settled) phaseSpeed = 1.6f;
            }
            float prevPhase = phase;
            phase = Mathf.Repeat(phase + phaseSpeed * dt, 1f);

            // ---------------- feet ----------------
            float swingFrac = Mathf.Lerp(0.38f, 0.55f, runW);
            float lift = Mathf.Lerp(Mathf.Lerp(0.1f, 0.32f, runW), 0.2f, crawlW);   // spider-like high steps when crawling
            Vector3 vel = new Vector3(motor.Velocity.x, 0f, motor.Velocity.z);
            for (int i = 0; i < 2; i++)
            {
                float lp = Mathf.Repeat(phase + i * 0.5f, 1f);
                float lpPrev = Mathf.Repeat(prevPhase + i * 0.5f, 1f);
                bool inSwing = lp < swingFrac;
                float side = i == 0 ? -1f : 1f;
                if (inSwing && (!swinging[i] || lp < lpPrev))
                {
                    swinging[i] = true;
                    footFrom[i] = footPos[i];
                    Vector3 lead = speed > 0.1f ? vel.normalized * (stride * 0.5f) : Vector3.zero;
                    footTo[i] = Ground(root + right * (hipHalfWidth * side * Mathf.Lerp(1f, 2.7f, crawlW)) + lead - fwd * (crawlW * 0.3f));
                }
                if (swinging[i])
                {
                    float t = Mathf.Clamp01(lp / swingFrac);
                    footPos[i] = Vector3.Lerp(footFrom[i], footTo[i], Mathf.SmoothStep(0f, 1f, t)) + up * Mathf.Sin(t * Mathf.PI) * lift;
                    if (!inSwing || t >= 1f)
                    {
                        swinging[i] = false;
                        footPos[i] = footTo[i];
                        if (audioOut != null) audioOut.Footstep(footPos[i], SurfaceAt(footPos[i]), Mathf.Lerp(0.55f, 1f, runW) * (1f - crawlW * 0.6f));
                    }
                }
            }

            // ---------------- pelvis ----------------
            float bob = Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2f)) * Mathf.Lerp(0.04f, 0.1f, runW) * moveW;
            float breathe = Mathf.Sin(Time.time * 1.6f) * 0.012f * (1f - stillW);
            float hunch = motor.Hunch;
            float hipH = Mathf.Lerp(hipHeight - Mathf.Lerp(0f, 0.14f, runW) - 0.22f * hunch, 0.4f, crawlW) - bob * (1f - 0.6f * crawlW) + breathe;
            float legReach = thigh + shin + ankle - 0.02f;
            float footY = Mathf.Min(footPos[0].y, footPos[1].y);
            hipH = Mathf.Min(hipH, footY - root.y + legReach);
            float lean = Mathf.Lerp(Mathf.Lerp(4f, 26f, runW) + 30f * hunch, 86f, crawlW);
            float twist = Mathf.Sin(phase * Mathf.PI * 2f) * (Mathf.Lerp(4f, 9f, runW) + 7f * crawlW) * moveW;
            // scuttling: a fast, insect-like shiver through the body
            float skitter = crawlW * moveW * (Mathf.Sin(Time.time * 37f) * 1.5f + Mathf.Sin(Time.time * 23f) * 1f);
            Quaternion yaw = Quaternion.LookRotation(fwd);
            Vector3 pelvisPos = root + up * hipH;

            // ---------------- the kill: the whole body goes for the victim, not just the neck ----------------
            float actLean = 0f;
            if (action == ElliotAction.InspectLow) actLean = 45f;
            else if (action == ElliotAction.Scream) actLean = 12f;
            PuppetPose pp = Puppet != null && Puppet.weight > 0.001f ? Puppet : null;
            float bend = actLean * actionW;
            if (pp != null) bend = Mathf.Lerp(bend, pp.lean, pp.weight);
            bend += vp.arch * 30f * (1f - crawlW);
            Vector3 reachT = Vector3.zero;
            if (pp != null)
            {
                // where the head has to get to decides the rest: the hips step in when it's out of reach,
                // the knees bend when it's low, and the spine curls over to get there
                Quaternion sr0 = yaw * Quaternion.Euler(lean + bend, 0f, 0f);
                Vector3 h0 = pelvisPos + sr0 * up * spineLength + (sr0 * Quaternion.Euler(-lean * 0.7f, 0f, 0f)) * up * neckLength;
                reachT = h0 + pp.headPush * pp.weight;
                float span = spineLength + neckLength;
                pelvisPos.y -= Mathf.Clamp(pelvisPos.y - root.y - (reachT.y - root.y + 0.15f), 0f, 0.6f) * pp.weight;
                Vector3 flat = reachT - pelvisPos; flat.y = 0f;
                float dist = Vector3.Distance(pelvisPos, reachT);
                if (dist > span * 0.96f && flat.sqrMagnitude > 1e-4f) pelvisPos += flat.normalized * (Mathf.Min(dist - span * 0.96f, 0.8f) * pp.weight);
            }
            Quaternion pelvisRot = yaw * Quaternion.Euler(lean * Mathf.Lerp(0.35f, 0.85f, crawlW), twist, skitter);
            pelvis.SetPositionAndRotation(pelvisPos, pelvisRot);

            // ---------------- legs (IK) ----------------
            Vector3 knee = fwd + up * 0.2f;
            // crawling, the knees point out and up like a spider's (the body hangs low between them)
            Vector3 kneeL = Vector3.Lerp(knee, -right + up * 1.3f - fwd * 0.2f, crawlW);
            Vector3 kneeR = Vector3.Lerp(knee, right + up * 1.3f - fwd * 0.2f, crawlW);
            Leg(thighL, shinL, footL, pelvisPos - right * hipHalfWidth, footPos[0] + up * ankle, kneeL, yaw);
            Leg(thighR, shinR, footR, pelvisPos + right * hipHalfWidth, footPos[1] + up * ankle, kneeR, yaw);

            // ---------------- spine, neck ----------------
            Quaternion spineRot = yaw * Quaternion.Euler(lean + bend, -twist * 1.4f, -skitter * 1.5f);
            Quaternion neckRot = spineRot * Quaternion.Euler(-Mathf.Lerp(lean * 0.7f, 38f, crawlW) - vp.push * 25f, 0f, 0f);
            if (pp != null)
            {
                // spine + neck as a two-bone chain from the hips to where the head must be; the back hunches up as it bends
                Vector3 mid = SolveIK(pelvisPos, reachT, spineLength, neckLength, up - fwd * 0.4f, out Vector3 end);
                spineRot = Quaternion.Slerp(spineRot, Aim(mid - pelvisPos, fwd), pp.weight);
                neckRot = Quaternion.Slerp(neckRot, Aim(end - mid, fwd), pp.weight);
            }
            spine.SetPositionAndRotation(pelvisPos, spineRot);
            Vector3 chest = pelvisPos + spineRot * up * spineLength;
            neck.SetPositionAndRotation(chest, neckRot);
            Vector3 headBase = chest + neckRot * up * neckLength + fwd * (vp.push * 0.15f * (pp != null ? 1f - pp.weight : 1f));

            // ---------------- head (turns far too far; twitches) ----------------
            float wantYaw = 0f, wantPitch = 0f;
            Vector3? look = pp != null ? pp.look : action != ElliotAction.None ? actionTarget : LookTarget;
            if (look.HasValue)
            {
                Vector3 d = look.Value - headBase;
                Vector3 local = Quaternion.Inverse(yaw) * d;
                wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -150f, 150f);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -50f, 60f);
            }
            if (action == ElliotAction.LookAround) wantYaw = Mathf.Sin(actionTime * 1.7f) * 130f;
            float headK = 1f - Mathf.Exp(-(pp != null ? 18f : action == ElliotAction.Stare ? 2f : 7f) * dt);
            headYaw = Mathf.LerpAngle(headYaw, wantYaw, headK);
            headPitch = Mathf.Lerp(headPitch, wantPitch, headK);
            twitchTimer -= dt;
            if (twitchTimer <= 0f && stillW < 0.5f)
            {
                twitchT = 0f;
                twitchTimer = Random.Range(3f, 9f);
                twitchOffset = new Vector3(Random.Range(-14f, 14f), Random.Range(-30f, 30f), Random.Range(-28f, 28f));
                if (audioOut != null && Random.value < 0.5f) audioOut.Twitch();
            }
            Vector3 tw = Vector3.zero;
            if (twitchT >= 0f)
            {
                twitchT += dt;
                float a = twitchT < 0.06f ? twitchT / 0.06f : Mathf.Clamp01(1f - (twitchT - 0.06f) / 0.5f);
                tw = twitchOffset * a;
                if (twitchT > 0.6f) twitchT = -1f;
            }
            headRoll = Mathf.Lerp(headRoll, action == ElliotAction.Stare ? 18f : 0f, 1f - Mathf.Exp(-2f * dt));
            if (pp != null && pp.headShake > 0f)
            {
                float sh = pp.headShake * pp.weight;
                tw += new Vector3(Mathf.Sin(Time.time * 31f) * sh * 0.3f, Mathf.Sin(Time.time * 23f) * sh, Mathf.Sin(Time.time * 37f + 1f) * sh * 0.6f);
            }
            // his voice: the head throws back on a shriek, drops to feed, jerks with each snarl, trembles when he screams
            float sk = vp.shake, tt = Time.time;
            Vector3 vh = new Vector3(vp.headPitch * 40f + Mathf.Sin(tt * 31f) * 6f * sk, vp.headYaw * 40f + Mathf.Sin(tt * 23f) * 9f * sk, vp.headRoll * 40f + Mathf.Sin(tt * 37f + 1f) * 6f * sk);
            Quaternion headRot = yaw * Quaternion.Euler(headPitch + tw.x - crawlW * 10f + vh.x, headYaw + tw.y + vh.y, headRoll + tw.z + vh.z);
            head.SetPositionAndRotation(headBase, headRot);

            float jawWant = action == ElliotAction.Scream ? 38f : action == ElliotAction.Grab ? 20f : (Mathf.Sin(Time.time * 1.6f) * 0.5f + 0.5f) * 3f;
            if (pp != null && pp.jaw >= 0f) jawWant = Mathf.Lerp(jawWant, pp.jaw, pp.weight);
            jawWant += vp.jaw * 42f * (pp != null ? 1f - 0.5f * pp.weight : 1f);
            jawOpen = Mathf.Lerp(jawOpen, jawWant, 1f - Mathf.Exp(-(pp != null ? 30f : 12f) * dt));
            if (jaw != null) jaw.localRotation = Quaternion.Euler(jawOpen, 0f, 0f);

            // ---------------- arms ----------------
            Vector3 shoulderL = chest + spineRot * (Vector3.left * shoulderHalfWidth) - spineRot * up * 0.04f + up * (vp.shoulders * 0.07f);
            Vector3 shoulderR = chest + spineRot * (Vector3.right * shoulderHalfWidth) - spineRot * up * 0.04f + up * (vp.shoulders * 0.07f);
            float armReach = upperArm + foreArm;

            // Upright: long arms hanging and swinging against the legs (wide and loose when running).
            float swing = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Lerp(22f, 55f, runW) * moveW;
            Vector3 hangL = shoulderL + (Quaternion.AngleAxis(-swing, right) * (Quaternion.AngleAxis(-8f - 10f * runW, fwd) * Vector3.down)) * armReach * 0.97f;
            Vector3 hangR = shoulderR + (Quaternion.AngleAxis(swing, right) * (Quaternion.AngleAxis(8f + 10f * runW, fwd) * Vector3.down)) * armReach * 0.97f;
            hangL += fwd * (0.12f * runW);
            hangR += fwd * (0.12f * runW);

            // Crawling: hands become front feet, planted and stepped like the legs (offset by a quarter cycle).
            for (int i = 0; i < 2; i++)
            {
                float lp = Mathf.Repeat(phase + 0.25f + i * 0.5f, 1f);
                bool inSwing = lp < 0.4f;
                float side = i == 0 ? -1f : 1f;
                if (inSwing && !handSwinging[i])
                {
                    handSwinging[i] = true;
                    handFrom[i] = handPlant[i];
                    handTo[i] = Ground(chest + right * (shoulderHalfWidth * 2.4f * side) + fwd * 0.5f + (speed > 0.1f ? vel.normalized * stride * 0.5f : Vector3.zero));
                }
                if (handSwinging[i])
                {
                    float t = Mathf.Clamp01(lp / 0.4f);
                    handPlant[i] = Vector3.Lerp(handFrom[i], handTo[i], Mathf.SmoothStep(0f, 1f, t)) + up * Mathf.Sin(t * Mathf.PI) * 0.2f;
                    if (!inSwing || t >= 1f) { handSwinging[i] = false; handPlant[i] = handTo[i]; }
                }
                if (crawlW < 0.05f) handPlant[i] = Ground(chest + fwd * 0.5f + right * (shoulderHalfWidth * 2.4f * side));
            }
            Vector3 handTargetL = Vector3.Lerp(hangL, handPlant[0] + up * 0.03f, crawlW);
            Vector3 handTargetR = Vector3.Lerp(hangR, handPlant[1] + up * 0.03f, crawlW);

            // A scream flings the arms out, claws spread (not while crawling - the hands are feet then).
            float fling = Mathf.Clamp01(vp.arms) * (1f - crawlW);
            if (fling > 0.001f)
            {
                handTargetL = Vector3.Lerp(handTargetL, shoulderL + (yaw * new Vector3(-0.82f, -0.32f, 0.5f)).normalized * armReach * 0.93f, fling);
                handTargetR = Vector3.Lerp(handTargetR, shoulderR + (yaw * new Vector3(0.82f, -0.32f, 0.5f)).normalized * armReach * 0.93f, fling);
            }
            if (sk > 0.001f)
            {
                handTargetL += (right * Mathf.Sin(tt * 29f) + up * Mathf.Sin(tt * 21f)) * (0.012f * sk);
                handTargetR += (right * Mathf.Sin(tt * 29f + 1f) + up * Mathf.Sin(tt * 21f + 1f)) * (0.012f * sk);
            }

            // Actions take over the arms.
            if (actionW > 0.001f)
            {
                Vector3 aL = handTargetL, aR = handTargetR;
                float t = actionTime;
                switch (action)
                {
                    case ElliotAction.ReachHandle:
                        aR = actionTarget;
                        break;
                    case ElliotAction.PullOpen:
                        aR = actionTarget - fwd * (Mathf.Clamp01(t / 0.4f) * 0.5f);
                        aL = actionTarget + right * -0.3f - fwd * (Mathf.Clamp01(t / 0.4f) * 0.4f);
                        break;
                    case ElliotAction.BangDoor:
                        float hammer = Mathf.Abs(Mathf.Sin(t * 7f));
                        aL = actionTarget - right * 0.25f - fwd * (0.35f * hammer) + up * 0.3f;
                        aR = actionTarget + right * 0.25f - fwd * (0.35f * Mathf.Abs(Mathf.Sin(t * 7f + 1.4f))) + up * 0.3f;
                        break;
                    case ElliotAction.Grab:
                        aL = actionTarget - right * 0.25f;
                        aR = actionTarget + right * 0.25f;
                        break;
                    case ElliotAction.Scream:
                        aL = shoulderL - fwd * 0.3f - right * 0.5f + Vector3.down * 0.6f;
                        aR = shoulderR - fwd * 0.3f + right * 0.5f + Vector3.down * 0.6f;
                        break;
                    case ElliotAction.InspectLow:
                        aR = actionTarget;
                        break;
                }
                handTargetL = Vector3.Lerp(handTargetL, aL, actionW);
                handTargetR = Vector3.Lerp(handTargetR, aR, actionW);
            }
            if (pp != null)
            {
                handTargetL = Vector3.Lerp(handTargetL, pp.handL, pp.weight);
                handTargetR = Vector3.Lerp(handTargetR, pp.handR, pp.weight);
            }

            Vector3 elbowPole = -fwd + Vector3.down * 0.4f;
            Vector3 poleL = Vector3.Lerp(elbowPole - right * 0.4f, -right + up * 1.1f - fwd * 0.3f, crawlW);
            Vector3 poleR = Vector3.Lerp(elbowPole + right * 0.4f, right + up * 1.1f - fwd * 0.3f, crawlW);
            Arm(upperArmL, foreArmL, handL, shoulderL, handTargetL, poleL, yaw);
            Arm(upperArmR, foreArmR, handR, shoulderR, handTargetR, poleR, yaw);

            float grip = action == ElliotAction.Grab || action == ElliotAction.BangDoor || action == ElliotAction.PullOpen ? 0.8f * actionW : 0f;
            if (pp != null) grip = Mathf.Max(grip, 0.9f * pp.weight);
            DriveDetails(grip + 0.35f * crawlW);   // crawling: claws dug in
        }

        // ------------------------------------------------------------------ voice-driven detail
        static float Ease(float a, float b, float k) => a + (b - a) * k;

        void SampleVoice(float dt)
        {
            VoicePose want = default;
            if (audioOut != null) want = audioOut.Performer.Sample(audioOut.breathing);
            else want.chest = Mathf.Sin(Time.time * 1.6f) * 0.35f;
            float k = 1f - Mathf.Exp(-40f * dt);
            vp.jaw = Ease(vp.jaw, want.jaw, k); vp.lip = Ease(vp.lip, want.lip, k); vp.nose = Ease(vp.nose, want.nose, k);
            vp.brow = Ease(vp.brow, want.brow, k); vp.throat = Ease(vp.throat, want.throat, k); vp.chest = Ease(vp.chest, want.chest, k);
            vp.tongue = Ease(vp.tongue, want.tongue, k); vp.headPitch = Ease(vp.headPitch, want.headPitch, k); vp.headYaw = Ease(vp.headYaw, want.headYaw, k);
            vp.headRoll = Ease(vp.headRoll, want.headRoll, k); vp.push = Ease(vp.push, want.push, k); vp.arch = Ease(vp.arch, want.arch, k);
            vp.shoulders = Ease(vp.shoulders, want.shoulders, k); vp.arms = Ease(vp.arms, want.arms, k); vp.curl = Ease(vp.curl, want.curl, k);
            vp.splay = Ease(vp.splay, want.splay, k); vp.shake = Ease(vp.shake, want.shake, k);

            // his breath fogs in the cold too: a wisp at the top of each breath, a cloud with every roar
            float dc = vp.chest - lastChest;
            lastChest = vp.chest;
            fogCool -= dt;
            if (head != null && fogCool <= 0f)
            {
                bool loud = vp.jaw > 0.55f;
                if (loud || (lastChestDelta > 0f && dc <= 0f && vp.chest > 0.1f))
                {
                    AfterHours.World.WorldFX.Breath(MouthPosition, head.forward, loud ? 1f : 0.55f);
                    fogCool = loud ? 0.3f : 1f;
                }
            }
            lastChestDelta = dc;
        }

        float lastChest, lastChestDelta, fogCool;

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform f = FindDeep(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        void FindDetails()
        {
            if (detailReady) return;
            chestJ = FindDeep(transform, "Chest");
            if (chestJ == null) return;   // primitive body: nothing to drive
            throatJ = FindDeep(transform, "Throat"); lipJ = FindDeep(transform, "LipUpper"); tongueJ = FindDeep(transform, "Tongue");
            noseL = FindDeep(transform, "NostrilL"); noseR = FindDeep(transform, "NostrilR");
            browL = FindDeep(transform, "BrowL"); browR = FindDeep(transform, "BrowR");
            if (throatJ == null || lipJ == null || tongueJ == null || noseL == null || noseR == null || browL == null || browR == null) return;
            throatRest = throatJ.localPosition; lipRest = lipJ.localPosition; tongueRest = tongueJ.localPosition; tongueRot = tongueJ.localRotation;
            noseLRest = noseL.localPosition; noseRRest = noseR.localPosition; browLRest = browL.localPosition; browRRest = browR.localPosition;
            for (int s = 0; s < 2; s++)
            {
                string side = s == 0 ? "L" : "R";
                for (int f = 0; f < 4; f++)
                    for (int j = 0; j < 3; j++)
                        fingerJ[s, f * 3 + j] = FindDeep(transform, "Finger" + side + f + "_" + (j + 1));
                fingerJ[s, 12] = FindDeep(transform, "Thumb" + side + "_1");
                fingerJ[s, 13] = FindDeep(transform, "Thumb" + side + "_2");
                for (int i = 0; i < 14; i++) if (fingerJ[s, i] != null) fingerRest[s, i] = fingerJ[s, i].localRotation;
            }
            detailReady = true;
        }

        /// <summary>Lips, nostrils, brows, throat, chest, tongue and fingers. grip: extra finger curl from what the hands are doing.</summary>
        void DriveDetails(float grip)
        {
            FindDetails();
            if (!detailReady) return;
            lipJ.localPosition = lipRest + new Vector3(0f, vp.lip * 0.0065f, -vp.lip * 0.0015f);
            float n = Mathf.Clamp01(vp.nose);
            Vector3 ns = new Vector3(1f + 0.7f * n, 1f + 0.3f * n, 1f + 0.2f * n);
            noseL.localScale = ns; noseR.localScale = ns;
            noseL.localPosition = noseLRest + new Vector3(-0.0013f * n, 0f, 0f);
            noseR.localPosition = noseRRest + new Vector3(0.0013f * n, 0f, 0f);
            browL.localPosition = browLRest + new Vector3(0.002f * vp.brow, -0.0045f * vp.brow, 0.001f * Mathf.Max(0f, vp.brow));
            browR.localPosition = browRRest + new Vector3(-0.002f * vp.brow, -0.0045f * vp.brow, 0.001f * Mathf.Max(0f, vp.brow));
            throatJ.localPosition = throatRest + new Vector3(0f, vp.throat * 0.003f, vp.throat * 0.005f);
            chestJ.localScale = new Vector3(1f + 0.03f * vp.chest, 1f + 0.012f * vp.chest, 1f + 0.06f * vp.chest);
            tongueJ.localPosition = tongueRest + new Vector3(0f, 0f, vp.tongue * 0.045f);
            tongueJ.localRotation = tongueRot * Quaternion.Euler(-8f * vp.tongue, 0f, 0f);
            float curl = Mathf.Clamp(0.15f + vp.curl - vp.splay * 0.5f + grip, -0.3f, 1f);
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                for (int f = 0; f < 4; f++)
                    for (int j = 0; j < 3; j++)
                    {
                        Transform t = fingerJ[s, f * 3 + j];
                        if (t == null) continue;
                        float amt = (j == 0 ? 55f : j == 1 ? 70f : 50f) * curl;
                        float spread = j == 0 ? (f - 1.5f) * vp.splay * 9f * -sign : 0f;
                        t.localRotation = fingerRest[s, f * 3 + j] * Quaternion.Euler(-amt, 0f, spread);
                    }
                for (int j = 0; j < 2; j++)
                {
                    Transform t = fingerJ[s, 12 + j];
                    if (t != null) t.localRotation = fingerRest[s, 12 + j] * Quaternion.Euler(-30f * curl, 0f, 0f);
                }
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>A rotation whose local +Y points along dir, facing forward as far as it can.</summary>
        static Quaternion Aim(Vector3 dir, Vector3 forward)
        {
            dir.Normalize();
            Vector3 f = Vector3.ProjectOnPlane(forward, dir);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.ProjectOnPlane(Vector3.down, dir);
            return Quaternion.LookRotation(f.normalized, dir);
        }

        void Leg(Transform upper, Transform lower, Transform foot, Vector3 hip, Vector3 ankleTarget, Vector3 pole, Quaternion yaw)
        {
            Vector3 kneePos = SolveIK(hip, ankleTarget, thigh, shin, pole, out Vector3 anklePos);
            Place(upper, hip, kneePos, pole);
            Place(lower, kneePos, anklePos, pole);
            if (foot != null) foot.SetPositionAndRotation(anklePos, yaw);
        }

        void Arm(Transform upper, Transform lower, Transform hand, Vector3 shoulder, Vector3 target, Vector3 pole, Quaternion yaw)
        {
            Vector3 elbow = SolveIK(shoulder, target, upperArm, foreArm, pole, out Vector3 wrist);
            Place(upper, shoulder, elbow, pole);
            Place(lower, elbow, wrist, pole);
            if (hand != null)
            {
                Vector3 dir = (wrist - elbow).normalized;
                hand.SetPositionAndRotation(wrist, Quaternion.LookRotation(Vector3.ProjectOnPlane(pole, dir).sqrMagnitude > 1e-6f ? Vector3.ProjectOnPlane(pole, dir) : Vector3.forward, -dir));
            }
        }

        /// <summary>Two-bone IK: returns the middle joint; end is the reachable end point.</summary>
        static Vector3 SolveIK(Vector3 root, Vector3 target, float a, float b, Vector3 pole, out Vector3 end)
        {
            Vector3 toTarget = target - root;
            float d = Mathf.Clamp(toTarget.magnitude, 0.05f, a + b - 0.001f);
            Vector3 dir = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : Vector3.down;
            end = root + dir * d;
            float x = (a * a - b * b + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
            bend = bend.sqrMagnitude > 1e-6f ? bend.normalized : Vector3.Cross(dir, Vector3.right).normalized;
            return root + dir * x + bend * h;
        }

        /// <summary>Points a limb segment (whose mesh hangs along local -Y) from 'from' to 'to'.</summary>
        static void Place(Transform seg, Vector3 from, Vector3 to, Vector3 pole)
        {
            if (seg == null) return;
            Vector3 dir = to - from;
            if (dir.sqrMagnitude < 1e-8f) return;
            dir.Normalize();
            Vector3 fwd = Vector3.ProjectOnPlane(pole, dir);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.forward, dir);
            seg.SetPositionAndRotation(from, Quaternion.LookRotation(fwd.normalized, -dir));
        }
    }
}
