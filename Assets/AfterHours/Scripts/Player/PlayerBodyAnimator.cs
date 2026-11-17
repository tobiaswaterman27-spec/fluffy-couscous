using UnityEngine;
using AfterHours.Core;
using AfterHours.Creature;
using AfterHours.Items;
using AfterHours.World;

namespace AfterHours.Player
{
    /// <summary>
    /// Moves the player's body, procedurally, from what the movement code is actually doing (so it's identical for
    /// you and for co-op players, whose movement arrives over the network):
    ///
    /// - The head is always exactly where the camera is, looking where you look; the spine bends (hips back,
    ///   back rounded) to put it there, so crouching and leaning look natural and first person lines up.
    /// - Walking / running / sneaking: feet step in time with the footstep sounds (stride phase), in the direction
    ///   you're moving (forwards, backwards, sideways), and land on the real ground (stairs, escalators).
    ///   Arms swing against the legs, wider when sprinting.
    /// - Crawling: flat on the belly, legs trailing where the movement code puts them, elbows working forward.
    /// - Carrying something heavy: both hands out in front. Breathing in the chest, harder after running.
    /// - Caught by Elliot: arms and legs flail.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class PlayerBodyAnimator : MonoBehaviour
    {
        public PlayerController controller;
        public PlayerBody body;

        // this person's proportions (read from their skeleton; every character is built differently)
        float Spine = 0.52f, Neck = 0.075f, Thigh = 0.45f, Shin = 0.43f, AnkleH = 0.08f, HipW = 0.09f, ShW = 0.185f, Upper = 0.29f, Fore = 0.26f;
        float HipY = 0.95f;
        Vector3 EyeInHead = new Vector3(0f, 0.104f, 0.074f), ShoulderOff = new Vector3(0.185f, -0.045f, -0.01f), HipOff = new Vector3(0.09f, -0.03f, 0f);
        int builtVersion = -1;

        Transform pelvis, spine, chestJ, neck, head, jaw;
        readonly Transform[] thigh = new Transform[2], shin = new Transform[2], foot = new Transform[2], upper = new Transform[2], fore = new Transform[2], hand = new Transform[2];
        readonly Transform[,] fingers = new Transform[2, 14];
        readonly Quaternion[,] fingerRest = new Quaternion[2, 14];
        bool ready;
        float lastStride, stepRate, exertion, breathT;
        int groundMask;

        void Start()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (body == null) body = GetComponent<PlayerBody>();
            int m = GameLayers.CharacterBlockingMask;
            if (GameLayers.PlayerLayer >= 0) m &= ~(1 << GameLayers.PlayerLayer);
            if (GameLayers.CreatureLayer >= 0) m &= ~(1 << GameLayers.CreatureLayer);
            if (GameLayers.PropLayer >= 0) m &= ~(1 << GameLayers.PropLayer);
            groundMask = m;
        }

        bool Find()
        {
            if (body == null || !body.Ready) return false;
            if (ready && builtVersion == body.Version) return true;
            ready = false;
            builtVersion = body.Version;
            var R = body.Rest;
            float D(string a, string b) => R.ContainsKey(a) && R.ContainsKey(b) ? Vector3.Distance(R[a].pos, R[b].pos) : 0f;
            if (R.ContainsKey("Neck") && R.ContainsKey("ThighR") && R.ContainsKey("UpperArmR"))
            {
                Spine = D("Spine", "Neck"); Neck = D("Neck", "Head");
                Thigh = D("ThighR", "ShinR"); Shin = D("ShinR", "FootR"); AnkleH = R["FootR"].pos.y;
                Upper = D("UpperArmR", "ForeArmR"); Fore = D("ForeArmR", "HandR");
                Vector3 so = R["UpperArmR"].pos - R["Neck"].pos; ShoulderOff = so; ShW = so.x;
                Vector3 ho = R["ThighR"].pos - R["Pelvis"].pos; HipOff = ho; HipW = ho.x;
                HipY = R["Pelvis"].pos.y - 0.015f;   // standing: legs nearly straight
                if (R.ContainsKey("EyeL") && R.ContainsKey("EyeR")) EyeInHead = (R["EyeL"].pos + R["EyeR"].pos) * 0.5f - R["Head"].pos;
            }
            pelvis = body.Bone("Pelvis"); spine = body.Bone("Spine"); chestJ = body.Bone("Chest"); neck = body.Bone("Neck"); head = body.Bone("Head"); jaw = body.Bone("Jaw");
            string[] sides = { "L", "R" };
            for (int s = 0; s < 2; s++)
            {
                thigh[s] = body.Bone("Thigh" + sides[s]); shin[s] = body.Bone("Shin" + sides[s]); foot[s] = body.Bone("Foot" + sides[s]);
                upper[s] = body.Bone("UpperArm" + sides[s]); fore[s] = body.Bone("ForeArm" + sides[s]); hand[s] = body.Bone("Hand" + sides[s]);
                for (int f = 0; f < 4; f++) for (int j = 0; j < 3; j++) fingers[s, f * 3 + j] = body.Bone("Finger" + sides[s] + f + "_" + (j + 1));
                fingers[s, 12] = body.Bone("Thumb" + sides[s] + "_1"); fingers[s, 13] = body.Bone("Thumb" + sides[s] + "_2");
                for (int i = 0; i < 14; i++) if (fingers[s, i] != null) fingerRest[s, i] = fingers[s, i].localRotation;
            }
            ready = pelvis != null && spine != null && neck != null && head != null && thigh[1] != null && hand[1] != null;
            return ready;
        }

        void LateUpdate()
        {
            if (controller == null || controller.Motor == null || !Find()) return;
            if (PlayerRagdoll.IsActive(body)) return;          // limp: physics has the body now
            PlayerMotor motor = controller.Motor;
            LocomotionState l = motor.Locomotion;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f), t = Time.time;
            Transform tr = transform;
            Quaternion rot = tr.rotation;
            Vector3 up = rot * Vector3.up, fwd = rot * Vector3.forward, right = rot * Vector3.right, root = tr.position;
            bool caught = CatchSequence.IsBeingCaught(controller);

            float h = motor.CurrentHeight;
            float crawl = Mathf.Clamp01(motor.CrawlBlend);
            float crouch = Mathf.Clamp01(Mathf.InverseLerp(motor.standHeight, motor.crouchHeight, h));
            float speed = l.planarSpeed;
            float moveW = Mathf.Clamp01(speed / 0.3f);
            float runW = Mathf.Clamp01(Mathf.InverseLerp(3f, 5.5f, speed));
            exertion = Mathf.MoveTowards(exertion, runW, dt * (runW > exertion ? 0.15f : 0.05f));
            float prevBreathT = breathT;
            breathT += dt * Mathf.Lerp(1.4f, 3.2f, exertion);
            float breath = Mathf.Sin(breathT * Mathf.PI);
            // each exhale (sin turns negative) fogs in the cold air
            if (head != null && Mathf.FloorToInt(breathT) != Mathf.FloorToInt(prevBreathT) && Mathf.FloorToInt(breathT) % 2 == 1 && !HoldingBreath())
                WorldFX.Breath(head.position + transform.forward * 0.11f + Vector3.down * 0.05f, transform.forward + Vector3.down * 0.3f, 0.35f + exertion * 0.65f);

            // stride: one stridePhase unit per footstep; a full leg cycle is two
            float ds = l.stridePhase - lastStride;
            lastStride = l.stridePhase;
            if (ds >= 0f && ds < 1f) stepRate = Mathf.Lerp(stepRate, ds / dt, 1f - Mathf.Exp(-6f * dt));
            float stepLen = stepRate > 0.2f ? Mathf.Clamp(speed / stepRate, 0f, 1.4f) : 0f;
            stepLen *= Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(Mathf.InverseLerp(motor.standHeight, motor.crouchHeight, motor.CurrentHeight)));   // short steps crouched
            float phase = l.stridePhase * 0.5f;
            Vector2 lv = l.localPlanarVelocity;
            Vector3 moveDir = lv.sqrMagnitude > 0.01f ? (right * lv.x + fwd * lv.y).normalized : fwd;

            // the head goes exactly where the camera is
            float pitch = controller.Look != null ? controller.Look.Pitch : 0f;
            Quaternion headRot = rot * Quaternion.Euler(pitch * 0.9f, 0f, 0f);
            Vector3 eye = controller.Look != null && controller.Look.cameraRig != null ? controller.Look.cameraRig.position : root + up * motor.EyeHeight;
            if (caught && controller.Look != null && controller.Look.cameraRig != null) headRot = controller.Look.cameraRig.rotation;
            Vector3 headPos = eye - headRot * EyeInHead;

            Vector3 pelvisPos;
            Quaternion spineRot, neckRot;
            bool proneCaught = false;
            if (caught) CatchSequence.VictimPose(controller, out _, out _, out _, out _, out proneCaught);
            if (crawl > 0.5f && (!caught || proneCaught))
            {
                // ---------------- crawling: a belly crawl ----------------
                // hips and belly on the floor, chest raised on the forearms, head up to see ahead
                Vector3 legsDir = motor.LegsActive ? motor.LegsDirection : -fwd;
                pelvisPos = root + legsDir * 0.66f + up * 0.12f;
                Vector3 mid = SolveIK(pelvisPos, headPos, Spine, Neck, up - fwd * 0.4f, out Vector3 end);
                spineRot = Aim(mid - pelvisPos, -up);
                neckRot = Aim(end - mid, -up);
                SetBody(pelvisPos, Aim(-legsDir, -up), spineRot, neckRot, headRot, up);
                Vector3 legsRight = Vector3.Cross(up, -legsDir).normalized;
                // legs: frog kick - each knee in turn draws up along the floor to the side, then pushes back straight
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? -1f : 1f;
                    float cyc = (phase + s * 0.5f) * Mathf.PI * 2f;
                    Vector3 hip = pelvisPos + legsRight * (HipW * side) - up * 0.02f;
                    float k = Mathf.Max(0f, Mathf.Sin(cyc + Mathf.PI)) * moveW;
                    Vector3 straight = hip + legsDir * (Thigh + Shin - 0.03f) + legsRight * (0.06f * side);
                    Vector3 drawn = hip + legsDir * 0.38f + legsRight * (0.36f * side);
                    Vector3 ft = Vector3.Lerp(straight, drawn, k * 0.9f);
                    ft = ft - up * Vector3.Dot(ft - root, up) + up * 0.05f;
                    Vector3 toes = Vector3.Lerp(legsDir, -legsRight * side, k * 0.5f).normalized;
                    LegIK(s, hip, ft, legsRight * side - up * 0.6f, Quaternion.LookRotation(toes, up));   // tops of the feet on the floor
                }
                // arms: forearms flat on the floor, elbows out; each forearm in turn reaches forward, plants and drags the body up
                Vector3 chest = pelvisPos + spineRot * Vector3.up * Spine;
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? -1f : 1f;
                    float cyc = (phase + s * 0.5f) * Mathf.PI * 2f;
                    Vector3 shoulder = chest + spineRot * new Vector3(ShW * side, ShoulderOff.y, ShoulderOff.z);
                    float c = Mathf.Sin(cyc) * moveW, swing = Mathf.Max(0f, Mathf.Cos(cyc)) * moveW;
                    float sh = Vector3.Dot(shoulder - root, up) - 0.035f;
                    float armLift = swing * 0.07f;
                    float flat = Mathf.Sqrt(Mathf.Max(0.0025f, Upper * Upper - (sh - armLift) * (sh - armLift)));
                    Vector3 outDir = (fwd * (0.55f + 0.4f * c) + right * (0.8f * side)).normalized;
                    Vector3 shoulderFloor = shoulder - up * Vector3.Dot(shoulder - root, up);
                    Vector3 elbow = shoulderFloor + up * (0.035f + armLift) + outDir * flat;
                    Vector3 handDir = (fwd * 0.92f - right * (0.4f * side)).normalized;
                    Vector3 handP = elbow + handDir * Fore;
                    handP = handP - up * Vector3.Dot(handP - root, up) + up * (0.03f + armLift);
                    Place(upper[s], shoulder, elbow, -up);
                    Place(fore[s], elbow, handP, -up);
                    Vector3 hd = (handP - elbow).normalized;
                    if (hand[s] != null) hand[s].SetPositionAndRotation(handP, Quaternion.LookRotation(Vector3.ProjectOnPlane(-up, hd).normalized, -hd));
                }
                Fingers(0.35f);
                return;
            }

            // ---------------- upright: standing, walking, running, crouching ----------------
            // the torso hangs from the head at a natural lean: barely forward when walking, more when running,
            // well forward when crouched (the legs then bend to reach the floor). The hips dip on each step.
            float span = Spine + Neck;
            float leanDeg = Mathf.Lerp(3f, 13f, runW * moveW) + 14f * crouch + 3f * moveW;
            Vector3 torsoDir = up * Mathf.Cos(leanDeg * Mathf.Deg2Rad) + fwd * Mathf.Sin(leanDeg * Mathf.Deg2Rad);
            float hipBob = (0.5f + 0.5f * Mathf.Cos(phase * Mathf.PI * 4f)) * Mathf.Lerp(0.025f, 0.05f, runW) * moveW + 0.02f * moveW;   // lowest at each heel strike
            pelvisPos = headPos - torsoDir * (span * 0.995f) - fwd * (0.06f * crouch) - up * (hipBob - breath * 0.004f + 0.07f * runW * moveW);   // runners sit lower: knees stay bent
            // never higher than the legs can reach
            float maxHip = Vector3.Dot(root, up) + HipY;
            float overHip = Vector3.Dot(pelvisPos, up) - maxHip;
            if (overHip > 0f) pelvisPos -= up * overHip;
            if (!caught) pelvisPos -= right * (Mathf.Sin(phase * Mathf.PI * 2f) * 0.025f * moveW * (1f - runW));   // weight over the planted foot
            // held: the torso hangs from where it's gripped, under gravity (from the shoulders, or by the ankles)
            Vector3 gripL = Vector3.zero, gripR = Vector3.zero; float struggle = 0f; bool upside = false;
            if (caught) CatchSequence.VictimPose(controller, out gripL, out gripR, out struggle, out upside);
            if (caught) pelvisPos = headPos + (upside ? Vector3.up : Vector3.down) * (span * 0.97f) + (headRot * Vector3.back) * 0.05f;
            Vector3 m2 = SolveIK(pelvisPos, headPos, Spine, Neck, (caught ? headRot * Vector3.back : -fwd * 0.3f) + up * 0.2f, out Vector3 e2);
            spineRot = Aim(m2 - pelvisPos, fwd);
            neckRot = Aim(e2 - m2, fwd);
            float twist = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Lerp(4f, 9f, runW) * moveW;
            spineRot = spineRot * Quaternion.Euler(0f, -twist, 0f);
            Quaternion pelvisRot = (caught ? headRot : rot) * Quaternion.Euler(Mathf.Lerp(4f, 20f, crouch), twist, -Mathf.Sin(phase * Mathf.PI * 2f) * 4f * moveW * (1f - runW));
            SetBody(pelvisPos, pelvisRot, spineRot, neckRot, headRot, up);

            // legs: step in time with the footsteps, onto the real ground
            // gait: each foot is planted (moving back under the body at walking speed, so it never slides) for ~60% of
            // the cycle walking / ~36% running, then swings: heel peels up, knee folds, foot clears and reaches heel-first
            float duty = Mathf.Lerp(0.62f, 0.36f, runW);
            float reach = Mathf.Clamp(duty * 2f * stepLen, 0f, Mathf.Lerp(0.68f, 1.3f, runW));
            float clear = Mathf.Lerp(0.1f, 0.34f, runW) * moveW;
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1f : 1f;
                Vector3 hip = pelvisPos + pelvisRot * new Vector3(HipW * side, HipOff.y, HipOff.z);
                Vector3 ft;
                float footPitch = 0f;
                if (caught)
                {
                    if (upside) ft = (s == 0 ? gripL : gripR);                  // held by the ankles
                    else
                    {
                        // dangling, kicking: alternate legs drive forward and back, weaker as they give up
                        Vector3 bodyFwd = Vector3.ProjectOnPlane(headRot * Vector3.forward, Vector3.up).normalized;
                        float kick = Mathf.Sin(t * 6.5f + s * Mathf.PI) * 0.26f * struggle;
                        float bend = Mathf.Max(0f, Mathf.Sin(t * 6.5f + s * Mathf.PI + 1.2f)) * 0.12f * struggle;
                        ft = hip + Vector3.down * (Thigh + Shin - 0.04f - bend) + bodyFwd * kick
                             + new Vector3(Mathf.Sin(t * 1.3f + s), 0f, Mathf.Cos(t * 1.1f + s)) * 0.03f;
                    }
                }
                else
                {
                    float u = Mathf.Repeat(phase + s * 0.5f, 1f);
                    GaitFoot(u, duty, reach, clear, moveW, out float off, out float raise, out footPitch);
                    footPitch -= 22f * crouch * (1f - moveW);                   // a deep squat rises onto the balls of the feet
                    raise += 0.035f * crouch * (1f - moveW);
                    float behind = Mathf.Max(0f, -Vector3.Dot(pelvisPos - headPos, fwd));
                    Vector3 basePos = root - fwd * (behind * 0.95f * crouch - 0.05f * crouch) + right * ((0.1f + 0.07f * crouch) * side) + moveDir * off;
                    float groundY = Vector3.Dot(root, up);
                    if (Physics.Raycast(basePos + up * 0.5f, -up, out RaycastHit hit, 1.0f, groundMask, QueryTriggerInteraction.Ignore)) groundY = Vector3.Dot(hit.point, up);
                    if (!l.grounded) raise += 0.15f;
                    ft = basePos - up * (Vector3.Dot(basePos, up) - groundY) + up * (AnkleH + raise);
                }
                Vector3 hangF = Vector3.ProjectOnPlane(headRot * Vector3.forward, Vector3.up);
                Quaternion dangle = (hangF.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(hangF.normalized) : rot) * Quaternion.Euler(upside ? -40f : 45f, 0f, 0f);
                LegIK(s, hip, ft, fwd + up * 0.2f + right * (0.55f * side * crouch), caught ? dangle : rot * Quaternion.Euler(-footPitch, side * (6f + 14f * crouch), 0f));
            }

            // arms: swing against the legs; out in front when carrying; flailing when caught
            Vector3 chestPos = pelvisPos + spineRot * Vector3.up * Spine;
            bool carrying = controller.GetComponent<PlayerGrab>() is PlayerGrab g && g.IsHolding;
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1f : 1f;
                // idle breathing: the shoulders rise a little on each breath in (more when out of breath)
                Vector3 shoulder = chestPos + spineRot * new Vector3(ShW * side, ShoulderOff.y, ShoulderOff.z)
                                   + up * (Mathf.Max(0f, breath) * Mathf.Lerp(0.006f, 0.02f, exertion) * (1f - moveW * 0.5f));
                // arms swing against the legs; the elbow bends more as the hand comes forward, and a lot when running
                float swing = Mathf.Sin((phase + s * 0.5f + 0.5f) * Mathf.PI * 2f) * Mathf.Lerp(0.28f, 0.42f, runW) * moveW;
                float reachLen = (Upper + Fore) * (0.95f - 0.12f * Mathf.Max(0f, swing) - 0.2f * runW);
                Vector3 target = shoulder + (-up * 0.55f + right * (0.05f * side) + fwd * (swing + 0.05f + 0.12f * crouch)).normalized * reachLen;
                if (runW > 0.3f) target += up * (0.1f * runW) - right * (0.04f * side * runW);
                if (crouch > 0.01f)
                {
                    // crouched: forearms forward over the knees, hands loose in front of them
                    Vector3 rest = shoulder + fwd * (0.3f + swing * 0.3f) - up * 0.36f + right * (0.02f * side);
                    target = Vector3.Lerp(target, rest, crouch);
                }
                if (carrying) target = shoulder + fwd * 0.42f - up * 0.12f - right * (0.05f * side);
                if (caught)
                {
                    float armLen = (Upper + Fore) * 0.96f;
                    Vector3 hang = shoulder + Vector3.down * armLen + new Vector3(Mathf.Sin(t * 0.9f + s), 0f, Mathf.Cos(t * 0.8f + s)) * 0.04f;
                    Vector3 fight;
                    if (upside)
                        // upside down: arms hang to the floor, clawing at it
                        fight = shoulder + Vector3.down * armLen * 0.9f + new Vector3(Mathf.Sin(t * 5f + s * 2f), 0f, Mathf.Cos(t * 4.3f + s)) * 0.18f;
                    else
                    {
                        // clutching at the wrists of the hands that hold them, tugging
                        Vector3 grip = s == 0 ? gripL : gripR;
                        Vector3 toward = headRot * Vector3.forward;
                        fight = grip + toward * 0.1f - Vector3.up * 0.05f + toward * (Mathf.Sin(t * 7f + s * 1.7f) * 0.04f);
                        if ((fight - shoulder).magnitude > armLen) fight = shoulder + (fight - shoulder).normalized * armLen;
                    }
                    target = Vector3.Lerp(hang, fight, struggle);
                }
                Vector3 palm = Vector3.Lerp(-right * (0.4f * side) - fwd, -right * (0.4f * side) - up - fwd * 0.3f, crouch);   // relaxed: palms face back and a little in
                if (!caught && GestureArm(s, side, shoulder, headPos, fwd, up, right, ref target, ref palm)) handGrip[s] = true;
                else handGrip[s] = false;
                ArmIK(s, shoulder, target, -fwd * (1f - 0.6f * crouch) + right * ((0.3f + 0.7f * crouch) * side) - up * 0.3f, palm);
            }
            Fingers(caught ? 0.8f : carrying ? 0.6f : (handGrip[0] || handGrip[1]) ? 0.7f : 0.25f + 0.2f * runW);
            // the mouth: open with fright when caught, panting when out of breath, and moving with your voice
            if (mouth == null) mouth = GetComponent<VoiceMouth>() ?? gameObject.AddComponent<VoiceMouth>();
            float talk = mouth.Open;
            if (jaw != null) jaw.localRotation = Quaternion.Euler(caught ? 8f + 14f * struggle + Mathf.Sin(t * 20f) * 4f * struggle : 1f + exertion * 6f * Mathf.Max(0f, breath) + talk * 15f, 0f, 0f);
            Face(t, dt, caught, talk);

        }

        void SetBody(Vector3 pelvisPos, Quaternion pelvisRot, Quaternion spineRot, Quaternion neckRot, Quaternion headRot, Vector3 up)
        {
            pelvis.SetPositionAndRotation(pelvisPos, pelvisRot);
            spine.SetPositionAndRotation(pelvisPos, spineRot);
            Vector3 chest = pelvisPos + spineRot * Vector3.up * Spine;
            neck.SetPositionAndRotation(chest, neckRot);
            head.SetPositionAndRotation(chest + neckRot * Vector3.up * Neck, headRot);
            if (chestJ != null) chestJ.localScale = new Vector3(1f + 0.015f * Mathf.Sin(breathT * Mathf.PI), 1f, 1f + 0.03f * Mathf.Sin(breathT * Mathf.PI));
        }

        void LegIK(int s, Vector3 hip, Vector3 ankle, Vector3 pole, Quaternion footRot)
        {
            Vector3 knee = SolveIK(hip, ankle, Thigh, Shin, pole, out Vector3 end);
            Place(thigh[s], hip, knee, pole);
            Place(shin[s], knee, end, pole);
            if (foot[s] != null) foot[s].SetPositionAndRotation(end, footRot);
        }

        void ArmIK(int s, Vector3 shoulder, Vector3 target, Vector3 pole, Vector3 palm)
        {
            Vector3 elbow = SolveIK(shoulder, target, Upper, Fore, pole, out Vector3 wrist);
            Place(upper[s], shoulder, elbow, pole);
            Place(fore[s], elbow, wrist, pole);
            Vector3 dir = (wrist - elbow).normalized;
            Vector3 pf = Vector3.ProjectOnPlane(palm, dir);   // the palm faces this way
            if (hand[s] != null) hand[s].SetPositionAndRotation(wrist, Quaternion.LookRotation(pf.sqrMagnitude > 1e-6f ? pf.normalized : Vector3.forward, -dir));
        }

        void Fingers(float curl)
        {
            for (int s = 0; s < 2; s++)
            {
                for (int f = 0; f < 4; f++)
                    for (int j = 0; j < 3; j++)
                    {
                        Transform t = fingers[s, f * 3 + j];
                        if (t != null) t.localRotation = fingerRest[s, f * 3 + j] * Quaternion.Euler(-(j == 0 ? 50f : j == 1 ? 65f : 45f) * curl, 0f, 0f);
                    }
                for (int j = 0; j < 2; j++)
                {
                    Transform t = fingers[s, 12 + j];
                    if (t != null) t.localRotation = fingerRest[s, 12 + j] * Quaternion.Euler(-25f * curl, 0f, 0f);
                }
            }
        }

        static float Sm01(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / Mathf.Max(1e-4f, b - a)));

        /// <summary>One foot through the gait cycle (u 0..1): offset along the movement, height, toe pitch (deg, + = toe up).</summary>
        static void GaitFoot(float u, float duty, float reach, float clear, float moveW, out float off, out float raise, out float pitch)
        {
            if (u < duty)
            {
                float k = u / duty, heel = Sm01(0.6f, 1f, k);
                off = (0.5f - k) * reach;
                raise = heel * 0.07f * moveW;
                pitch = (14f * (1f - Sm01(0f, 0.18f, k)) - 38f * heel) * moveW;
                return;
            }
            float q = (u - duty) / (1f - duty), e = q * q * (3f - 2f * q);
            off = (-0.5f + e) * reach;
            raise = Mathf.Sin(Mathf.PI * Mathf.Pow(q, 0.65f)) * clear + 0.07f * moveW * (1f - Sm01(0f, 0.3f, q));
            pitch = (-38f * (1f - Sm01(0f, 0.35f, q)) + 14f * Sm01(0.7f, 1f, q)) * moveW;
        }

        static Quaternion Aim(Vector3 dir, Vector3 forward)
        {
            dir.Normalize();
            Vector3 f = Vector3.ProjectOnPlane(forward, dir);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.ProjectOnPlane(Vector3.forward, dir);
            return Quaternion.LookRotation(f.normalized, dir);
        }

        static Vector3 SolveIK(Vector3 root, Vector3 target, float a, float b, Vector3 pole, out Vector3 end)
        {
            Vector3 to = target - root;
            float d = Mathf.Clamp(to.magnitude, 0.05f, a + b - 0.001f);
            Vector3 dir = to.sqrMagnitude > 1e-8f ? to.normalized : Vector3.down;
            end = root + dir * d;
            float x = (a * a - b * b + d * d) / (2f * d);
            float hh = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
            bend = bend.sqrMagnitude > 1e-6f ? bend.normalized : Vector3.Cross(dir, Vector3.right).normalized;
            return root + dir * x + bend * hh;
        }

        static void Place(Transform seg, Vector3 from, Vector3 to, Vector3 pole)
        {
            if (seg == null) return;
            Vector3 dir = to - from;
            if (dir.sqrMagnitude < 1e-8f) return;
            dir.Normalize();
            Vector3 f = Vector3.ProjectOnPlane(pole, dir);
            if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(Vector3.forward, dir);
            seg.SetPositionAndRotation(from, Quaternion.LookRotation(f.normalized, -dir));
        }

        VoiceMouth mouth;
        Transform lidL, lidR;
        Quaternion lidRestL, lidRestR;
        float blinkT = -1f, nextBlink;
        int lidVersion = -1;

        /// <summary>
        /// Blinking: every 2-6 s (much more often when scared), a fast close-hold-open, sometimes twice; held shut
        /// once you go limp. Lids sit a few degrees lowered at rest so the eyes aren't staring wide.
        /// </summary>
        void Face(float t, float dt, bool caught, float talk)
        {
            if (lidVersion != builtVersion)
            {
                lidVersion = builtVersion;
                lidL = body.Bone("LidL"); lidR = body.Bone("LidR");
                if (lidL != null) lidRestL = lidL.localRotation;
                if (lidR != null) lidRestR = lidR.localRotation;
                nextBlink = t + Random.Range(1f, 4f);
            }
            if (lidL == null || lidR == null) return;
            if (blinkT < 0f && t >= nextBlink)
            {
                blinkT = 0f;
                nextBlink = t + (caught ? Random.Range(0.5f, 1.2f) : Random.Range(2f, 6f));
                if (Random.value < 0.15f) nextBlink = t + 0.32f;        // a double blink
            }
            float k = 0f;
            if (blinkT >= 0f)
            {
                blinkT += dt;
                float u = blinkT;
                k = u < 0.07f ? u / 0.07f : u < 0.1f ? 1f : u < 0.22f ? 1f - (u - 0.1f) / 0.12f : 0f;
                if (u >= 0.22f) blinkT = -1f;
            }
            k = Mathf.SmoothStep(0f, 1f, k);
            float wide = caught ? -4f : 0f;                              // terror: eyes wide between blinks
            float ang = 4f + wide + 46f * k + talk * 2f;
            lidL.localRotation = lidRestL * Quaternion.Euler(ang, 0f, 0f);
            lidR.localRotation = lidRestR * Quaternion.Euler(ang, 0f, 0f);
        }

        readonly bool[] handGrip = new bool[2];
        PlayerActions actions;
        bool actionsLooked;

        static float Sm(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / Mathf.Max(1e-4f, b - a)));

        /// <summary>
        /// Hands doing something (PlayerActions): reaching for a handle and pressing it, jiggling a locked one,
        /// shoving a door, bracing to barge through one, flinching from an impact, pulling a grille off,
        /// gripping a sill to climb. Blends over the normal arm swing; returns false when this arm is free.
        /// </summary>
        bool GestureArm(int s, float side, Vector3 shoulder, Vector3 headPos, Vector3 fwd, Vector3 up, Vector3 right, ref Vector3 target, ref Vector3 palm)
        {
            if (!actionsLooked) { actions = GetComponent<PlayerActions>(); actionsLooked = true; }
            if (actions == null || actions.Current == Gesture.None) return false;
            float p = actions.Progress;
            Vector3 g = actions.GestureTarget, n = actions.GestureNormal, along = actions.GestureAlong;
            float env = Sm(0f, 0.18f, p) * (1f - Sm(0.82f, 1f, p));
            Vector3 want;
            switch (actions.Current)
            {
                case Gesture.Handle:
                case Gesture.LockedTry:
                case Gesture.Rattle:
                {
                    if (s != 1) return false;                                   // right hand on the handle
                    float press;
                    Vector3 shove = Vector3.zero;
                    if (actions.Current == Gesture.Handle)
                    {
                        press = Sm(0.25f, 0.4f, p);
                        shove = -n * 0.22f * Sm(0.45f, 0.95f, p);                // through the doorway with the door
                    }
                    else if (actions.Current == Gesture.LockedTry) press = Sm(0.25f, 0.4f, p) * (1f - Sm(0.6f, 0.75f, p));
                    else
                    {
                        press = Mathf.Abs(Mathf.Sin(p * Mathf.PI * 6f));
                        shove = -n * (Mathf.Sin(p * Mathf.PI * 10f) * 0.035f);   // yanking it back and forth
                    }
                    want = g - up * (0.035f * press) + shove;
                    palm = -up * 0.6f - n * 0.4f;
                    break;
                }
                case Gesture.Push:
                case Gesture.Slam:
                {
                    if (actions.Current == Gesture.Push && s != 1) return false;
                    float reach = actions.Current == Gesture.Slam ? Sm(0.05f, 0.4f, p) : Sm(0.1f, 0.5f, p);
                    want = shoulder + (Vector3.ProjectOnPlane(g - shoulder, up).normalized * 0.55f + up * 0.05f) * (0.6f + 0.4f * reach) + right * (0.08f * side);
                    palm = -n;
                    break;
                }
                case Gesture.Barge:
                    env = Sm(0f, 0.08f, p) * (1f - Sm(0.7f, 1f, p));
                    want = shoulder + fwd * 0.42f + up * 0.06f - right * (0.1f * side) + (s == 0 ? fwd * 0.06f : Vector3.zero);
                    palm = fwd;
                    break;
                case Gesture.Flinch:
                    env = Sm(0f, 0.06f, p) * (1f - Sm(0.6f, 1f, p));
                    want = headPos + fwd * 0.22f - up * 0.08f + right * (0.1f * side);
                    palm = fwd;
                    break;
                case Gesture.Grille:
                {
                    float pull = Sm(0.35f, 0.75f, p);
                    want = g + along * (0.33f * side) + n * (0.04f + 0.35f * pull) - up * 0.05f;
                    palm = -n;
                    break;
                }
                case Gesture.Climb:
                {
                    env = Sm(0f, 0.15f, p) * (1f - Sm(0.78f, 0.95f, p));
                    Vector3 sill = g - up * 0.42f + n * 0.04f;                  // the bottom edge of the opening
                    want = sill + along * (0.28f * side);
                    palm = -up;
                    break;
                }
                case Gesture.Reach:
                case Gesture.PickUp:
                {
                    if (s != 1) return false;
                    // out to it, a press (or a grab: fingers close), and back
                    float reach = Sm(0f, 0.35f, p) * (1f - Sm(0.65f, 1f, p));
                    env = reach;
                    want = g + n * (0.03f - 0.02f * Sm(0.3f, 0.45f, p));
                    if ((want - shoulder).magnitude > (Upper + Fore) * 0.98f) want = shoulder + (want - shoulder).normalized * (Upper + Fore) * 0.98f;
                    palm = actions.Current == Gesture.PickUp ? -up : -n;
                    break;
                }
                case Gesture.Strike:
                {
                    if (s != 1) return false;
                    // wind up over the shoulder, then bring it down hard
                    Vector3 wind = shoulder + up * 0.35f - fwd * 0.1f + right * 0.1f;
                    want = p < 0.35f ? Vector3.Lerp(shoulder + fwd * 0.3f, wind, Sm(0f, 0.35f, p)) : Vector3.Lerp(wind, g, Sm(0.35f, 0.5f, p));
                    env = Sm(0f, 0.1f, p) * (1f - Sm(0.8f, 1f, p));
                    palm = -right;
                    break;
                }
                case Gesture.Drop:
                    env = Sm(0.1f, 0.3f, p) * (1f - Sm(0.7f, 0.9f, p));
                    want = headPos + fwd * 0.45f - up * 0.55f + right * (0.15f * side);
                    palm = -up;
                    break;
                default:
                    return false;
            }
            target = Vector3.Lerp(target, want, env);
            return env > 0.5f;
        }

        AfterHours.Audio.PlayerAudio ownAudio;
        bool ownAudioLooked;
        bool HoldingBreath()
        {
            if (!ownAudioLooked) { ownAudio = GetComponentInParent<AfterHours.Audio.PlayerAudio>(); ownAudioLooked = true; }
            return ownAudio != null && ownAudio.HoldingBreath;
        }
    }
}
