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
            if (crawl > 0.5f && !caught)
            {
                // ---------------- crawling: flat on the belly ----------------
                Vector3 legsDir = motor.LegsActive ? motor.LegsDirection : -fwd;
                pelvisPos = root + legsDir * (motor.CurrentRadius * 0.8f + 0.25f) + up * 0.14f;
                Vector3 mid = SolveIK(pelvisPos, headPos, Spine, Neck, up * 1f + fwd * 0.2f, out Vector3 end);
                spineRot = Aim(mid - pelvisPos, -up);
                neckRot = Aim(end - mid, -up);
                SetBody(pelvisPos, Aim(-legsDir, -up), spineRot, neckRot, headRot, up);
                // legs: hips to the knees and feet the movement code drags along behind
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? -1f : 1f;
                    Vector3 hip = pelvisPos + right * (HipW * side) - up * 0.02f;
                    Vector3 kick = Mathf.Sin((phase + s * 0.5f) * Mathf.PI * 2f) * 0.06f * moveW * up;
                    Vector3 ft = (motor.LegsActive ? motor.FeetPosition : hip + legsDir * (Thigh + Shin)) + right * (0.07f * side) + up * 0.1f + kick;
                    LegIK(s, hip, ft, -up + right * (0.3f * side), Quaternion.LookRotation(-up * 0.6f + legsDir * 0.4f, legsDir));
                }
                // arms: elbows out, hands working forward along the floor in turn
                Vector3 chest = pelvisPos + spineRot * Vector3.up * Spine;
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? -1f : 1f;
                    Vector3 shoulder = chest + spineRot * new Vector3(ShW * side, ShoulderOff.y, ShoulderOff.z);
                    float c = Mathf.Sin((phase + s * 0.5f) * Mathf.PI * 2f) * moveW;
                    Vector3 target = shoulder + fwd * (0.22f + 0.14f * c) + right * (0.14f * side) - up * (Vector3.Dot(shoulder - root, up) - 0.03f - Mathf.Max(0f, c) * 0.05f);
                    ArmIK(s, shoulder, target, -up * 0.3f + right * side - fwd * 0.4f, -up);
                }
                Fingers(0.2f);
                return;
            }

            // ---------------- upright: standing, walking, running, crouching ----------------
            float hipY = Mathf.Lerp(HipY, HipY * 0.62f, crouch) - Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2f)) * Mathf.Lerp(0.02f, 0.05f, runW) * moveW + breath * 0.004f;
            float dy = Vector3.Dot(headPos - root, up) - hipY;
            float span = Spine + Neck;
            float back = dy < span ? Mathf.Sqrt(Mathf.Max(0f, span * span - dy * dy)) * 0.92f : 0f;   // hips go back so the head can come forward
            Vector3 headFlat = headPos - up * Vector3.Dot(headPos - root, up);
            pelvisPos = headFlat - fwd * (back + 0.02f + 0.06f * runW) + up * hipY;
            if (caught) pelvisPos = headPos - (headRot * Vector3.up) * (span * 0.98f);
            Vector3 m2 = SolveIK(pelvisPos, headPos, Spine, Neck, (caught ? headRot * Vector3.back : -fwd) + up * 0.2f, out Vector3 e2);
            spineRot = Aim(m2 - pelvisPos, fwd);
            neckRot = Aim(e2 - m2, fwd);
            float twist = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Lerp(4f, 9f, runW) * moveW;
            spineRot = spineRot * Quaternion.Euler(0f, -twist, 0f);
            Quaternion pelvisRot = (caught ? headRot : rot) * Quaternion.Euler(Mathf.Lerp(4f, 20f, crouch), twist, 0f);
            SetBody(pelvisPos, pelvisRot, spineRot, neckRot, headRot, up);

            // legs: step in time with the footsteps, onto the real ground
            float lift = Mathf.Lerp(0.07f, 0.2f, runW) * moveW;
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1f : 1f;
                Vector3 hip = pelvisPos + pelvisRot * new Vector3(HipW * side, HipOff.y, HipOff.z);
                Vector3 ft;
                if (caught)
                {
                    ft = hip - (headRot * Vector3.up) * (Thigh + Shin - 0.05f) + new Vector3(Mathf.Sin(t * 11f + s * 2f), Mathf.Sin(t * 13f + s), Mathf.Sin(t * 9f + s * 3f)) * 0.15f;
                }
                else
                {
                    float ph = (phase + s * 0.5f) * Mathf.PI * 2f;
                    Vector3 basePos = root + right * (0.1f * side) + moveDir * (Mathf.Sin(ph) * stepLen * 0.5f);
                    float raise = Mathf.Max(0f, Mathf.Cos(ph)) * lift;
                    float groundY = Vector3.Dot(root, up);
                    if (Physics.Raycast(basePos + up * 0.5f, -up, out RaycastHit hit, 1.0f, groundMask, QueryTriggerInteraction.Ignore)) groundY = Vector3.Dot(hit.point, up);
                    if (!l.grounded) raise += 0.15f;
                    ft = basePos - up * (Vector3.Dot(basePos, up) - groundY) + up * (AnkleH + raise);
                }
                LegIK(s, hip, ft, fwd + up * 0.2f, caught ? headRot * Quaternion.Euler(-60f, 0f, 0f) : rot * Quaternion.Euler(0f, side * 6f, 0f));
            }

            // arms: swing against the legs; out in front when carrying; flailing when caught
            Vector3 chestPos = pelvisPos + spineRot * Vector3.up * Spine;
            bool carrying = controller.GetComponent<PlayerGrab>() is PlayerGrab g && g.IsHolding;
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1f : 1f;
                Vector3 shoulder = chestPos + spineRot * new Vector3(ShW * side, ShoulderOff.y, ShoulderOff.z);
                float swing = Mathf.Sin((phase + s * 0.5f + 0.5f) * Mathf.PI * 2f) * Mathf.Lerp(0.12f, 0.35f, runW) * moveW;
                Vector3 target = shoulder + (-up * 0.52f + right * (0.07f * side) + fwd * (swing + 0.04f + 0.1f * crouch)).normalized * ((Upper + Fore) * 0.96f);
                if (runW > 0.3f) target += up * (0.12f * runW) + fwd * 0.05f;
                if (carrying) target = shoulder + fwd * 0.42f - up * 0.12f - right * (0.05f * side);
                if (caught) target = shoulder + (headRot * new Vector3(side * 0.4f, -0.1f, 0.3f)) + new Vector3(Mathf.Sin(t * 14f + s), Mathf.Sin(t * 17f + s * 2f), Mathf.Sin(t * 12f + s * 3f)) * 0.22f;
                ArmIK(s, shoulder, target, -fwd + right * (0.3f * side) - up * 0.3f, -right * side - fwd * 0.3f);
            }
            Fingers(caught ? 0.8f : carrying ? 0.6f : 0.25f + 0.2f * runW);
            if (jaw != null) jaw.localRotation = Quaternion.Euler(caught ? 18f + Mathf.Sin(t * 20f) * 6f : 1f + exertion * 6f * Mathf.Max(0f, breath), 0f, 0f);
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

        AfterHours.Audio.PlayerAudio ownAudio;
        bool ownAudioLooked;
        bool HoldingBreath()
        {
            if (!ownAudioLooked) { ownAudio = GetComponentInParent<AfterHours.Audio.PlayerAudio>(); ownAudioLooked = true; }
            return ownAudio != null && ownAudio.HoldingBreath;
        }
    }
}
