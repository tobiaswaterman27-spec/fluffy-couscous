using System;
using System.Collections.Generic;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Controls;
using AfterHours.Core;
using AfterHours.Gore;
using AfterHours.Hiding;
using AfterHours.Interaction;
using AfterHours.Items;
using AfterHours.Player;
using AfterHours.Settings;
using AfterHours.World;

namespace AfterHours.Creature
{
    /// <summary>Where / how a player was caught. Each one has its own opening before the shared ending.</summary>
    public enum CatchContext
    {
        Chase,       // grabbed in the open
        HidingPlace, // the locker / cupboard is ripped open, Elliot's face is right there, then you're dragged out
        Vent,        // grabbed by the ankles from outside a vent and dragged backwards out of it, held upside down
        VentInside,  // it crawled in after you: face to face in the duct, nowhere to go
        Doorway,     // it slams the door shut behind you first
        DarkRoom,    // grabbed from behind in the dark and spun round to face it
        Floor        // caught crawling: dragged back across the floor by the leg, then lifted upside down
    }

    /// <summary>
    /// The death sequence: Elliot grabs you, lifts you to its face, goes completely still, screams into your face,
    /// then bites and tears and drops you, and keeps feeding as the screen goes black. Gore included.
    ///
    /// Everything is a pure function of (context, seed, time since start, and where everyone stood at the start).
    /// The victim's body and Elliot's pose are driven in the WORLD, so in co-op every other player sees exactly
    /// what the victim sees in first person (the host just sends Begin's arguments; Part 16).
    /// Screen effects (blood on the lens, red flash, fade) are drawn only for the victim.
    /// </summary>
    public class CatchSequence : MonoBehaviour
    {
        // Shared beats, in seconds after the context's opening.
        const float GrabT = 0f, LiftT = 0.35f, StillT = 1.0f, ScreamT = 1.3f, RecoilT = 2.9f, BiteT = 3.2f, BiteHitT = 3.36f,
                    ThrashT = 3.4f, DropT = 4.7f, LandT = 5.0f, FeedT = 5.1f, BlackT = 6.6f, ResetT = 7.4f, EndT = 8.6f;

        static readonly List<CatchSequence> active = new List<CatchSequence>();
        public static bool AnyActive => active.Count > 0;
        public static bool IsBeingCaught(PlayerController p)
        {
            foreach (CatchSequence c in active) if (c.player == p) return true;
            return false;
        }

        /// <summary>
        /// How the victim's body should hold itself right now: where Elliot's hands are gripping them, how hard
        /// they're still struggling (1 = fighting, 0 = limp), and whether they're held upside down.
        /// </summary>
        public static bool VictimPose(PlayerController p, out Vector3 gripL, out Vector3 gripR, out float struggle, out bool upside)
            => VictimPose(p, out gripL, out gripR, out struggle, out upside, out _);

        public static bool VictimPose(PlayerController p, out Vector3 gripL, out Vector3 gripR, out float struggle, out bool upside, out bool prone)
        {
            foreach (CatchSequence c in active)
            {
                if (c.player != p) continue;
                float u = c.t - c.intro;
                gripL = c.pose.handL; gripR = c.pose.handR;
                struggle = u < BiteHitT ? 1f : 1f - Ease(BiteHitT, ThrashT + 0.6f, u);
                upside = c.upsideDown && u >= LiftT;
                prone = c.ctx == CatchContext.VentInside;
                return true;
            }
            gripL = gripR = Vector3.zero; struggle = 0f; upside = false; prone = false;
            return false;
        }

        /// <summary>Raised when a catch begins (networking sends this to everyone; stats; achievements).</summary>
        public static event Action<ElliotBrain, PlayerController, CatchContext, int> Started;

        /// <summary>LAN co-op: called instead of the full run reset (only the caught player starts over).</summary>
        public static Action<ElliotBrain, PlayerController> CoopRespawn;

        public static bool IsCreatureBusy(ElliotBrain b)
        {
            foreach (CatchSequence c in active) if (c.brain == b) return true;
            return false;
        }

        ElliotBrain brain;
        ElliotAnimator anim;
        PlayerController player;
        PlayerLook look;
        CatchContext ctx;
        int seed;
        System.Random rng;
        float t, intro;
        bool local, released;
        int fired;

        Vector3 ePos, fwd, right, headStart;
        float headH, eyeH = 1.6f;
        Vector3 startEye, startRoot, startFwd;
        Quaternion startBody, startView;
        Vector3 holdEye, floorEye, eyeAtMain, dragOffset;
        Quaternion bodyAtMain;
        bool upsideDown;
        Vector3 grabPoint;
        HidingSpot spot;
        Door nearDoor;
        Quaternion view;
        Vector3 curEye;
        Quaternion curBody;

        readonly ElliotAnimator.PuppetPose pose = new ElliotAnimator.PuppetPose();

        /// <summary>Which way you were facing when it got you (front, side, behind) and which way it turns you.</summary>
        enum Approach { Front, Side, Behind }
        Approach approach;
        float turnSign, startYaw, faceYaw;
        bool ragdolled;
        float flash, lens, black, listener = 1f;
        AudioLowPassFilter ears;

        // =====================================================================

        public static CatchSequence Begin(ElliotBrain brain, PlayerController victim, CatchContext ctx, int seed)
        {
            if (brain == null || victim == null || IsBeingCaught(victim)) return null;
            var go = new GameObject("[Caught] " + victim.name);
            CatchSequence c = go.AddComponent<CatchSequence>();
            c.Setup(brain, victim, ctx, seed);
            active.Add(c);
            Started?.Invoke(brain, victim, ctx, seed);
            return c;
        }

        /// <summary>Works out which kind of catch fits the situation.</summary>
        public static CatchContext Classify(ElliotBrain brain, PlayerController p)
        {
            if (p.IsHidden || p.CurrentHidingSpot != null) return CatchContext.HidingPlace;
            if (VentDuct.Contains(p.transform.position + Vector3.up * 0.3f))
                return VentDuct.Contains(brain.transform.position + Vector3.up * 0.3f) ? CatchContext.VentInside : CatchContext.Vent;
            if (p.Motor.CurrentStance == Stance.Crawling) return CatchContext.Floor;
            Vector3 toE = brain.transform.position - p.transform.position;
            toE.y = 0f;
            bool behind = Vector3.Angle(p.transform.forward, toE) > 115f;
            if (behind && ElliotSenses.LightLevelAt(p.EyePosition) < 0.3f) return CatchContext.DarkRoom;
            foreach (Collider c in Physics.OverlapSphere(p.transform.position + Vector3.up, 1.6f, 1 << Mathf.Max(0, GameLayers.InteractableLayer), QueryTriggerInteraction.Ignore))
            {
                Door d = c.GetComponentInParent<Door>();
                if (d != null && d.IsOpen && d.kind != DoorKind.Storage) return CatchContext.Doorway;
            }
            return CatchContext.Chase;
        }

        void Setup(ElliotBrain b, PlayerController p, CatchContext c, int s)
        {
            brain = b;
            anim = b.animator;
            player = p;
            look = p.Look;
            ctx = c;
            seed = s;
            rng = new System.Random(s);
            local = p.isLocallyControlled;

            ePos = b.transform.position;
            Vector3 d = p.transform.position - ePos;
            d.y = 0f;
            fwd = d.sqrMagnitude > 1e-4f ? d.normalized : b.transform.forward;
            right = Vector3.Cross(Vector3.up, fwd);
            b.transform.rotation = Quaternion.LookRotation(fwd);
            headStart = anim != null && anim.head != null ? anim.head.position : ePos + Vector3.up * 2.25f;
            headH = headStart.y - ePos.y;

            eyeH = look != null && look.cameraRig != null ? look.cameraRig.localPosition.y : 1.6f;
            startEye = p.EyePosition;
            startRoot = p.transform.position;
            startBody = p.transform.rotation;
            startFwd = p.transform.forward;
            startView = look != null && look.playerCamera != null ? look.playerCamera.transform.rotation : startBody;
            view = startView;
            curEye = startEye;
            curBody = startBody;

            spot = p.CurrentHidingSpot;
            // where it came from: straight ahead of you, from the side, or from behind
            Vector3 pf = startFwd; pf.y = 0f; pf.Normalize();
            float rel = Vector3.SignedAngle(pf, -fwd, Vector3.up);       // -fwd: from you towards it
            approach = Mathf.Abs(rel) < 50f ? Approach.Front : Mathf.Abs(rel) < 130f ? Approach.Side : Approach.Behind;
            turnSign = rel >= 0f ? 1f : -1f;
            startYaw = startBody.eulerAngles.y;
            faceYaw = Mathf.Atan2(-fwd.x, -fwd.z) * Mathf.Rad2Deg;
            switch (ctx)
            {
                case CatchContext.HidingPlace: intro = 1.0f; break;
                case CatchContext.Vent: intro = 1.6f; break;
                case CatchContext.Floor: intro = 1.0f; break;
                case CatchContext.VentInside: intro = 0f; break;
                default:
                    // grabbed from the side: yanked round by the arm. From behind: hauled backwards and spun to face it.
                    intro = approach == Approach.Behind ? 0.7f : approach == Approach.Side ? 0.45f : 0f;
                    break;
            }
            upsideDown = ctx == CatchContext.Vent || ctx == CatchContext.Floor;
            holdEye = upsideDown
                ? ePos + fwd * 0.5f + Vector3.up * (headH - 1.0f)
                : ePos + fwd * (ctx == CatchContext.HidingPlace ? 0.5f : 0.6f) + Vector3.up * (headH - 0.02f);
            if (ctx == CatchContext.VentInside) holdEye = startEye;
            floorEye = ePos + fwd * 1.9f + Vector3.up * 0.2f;
            if (Physics.Raycast(floorEye + Vector3.up, Vector3.down, out RaycastHit fh, 3f, GameLayers.CharacterBlockingMask & ~PlayerMask, QueryTriggerInteraction.Ignore))
                floorEye.y = fh.point.y + 0.2f;

            // Grabbed by the end of the body nearest Elliot (vent / floor drags).
            Vector3 feet = startRoot - startFwd * 0.9f;
            grabPoint = Vector3.Distance(feet, ePos) < Vector3.Distance(startEye, ePos) ? feet : startEye;
            Vector3 mouth = ePos + fwd * 0.75f;
            dragOffset = new Vector3(mouth.x - grabPoint.x, 0f, mouth.z - grabPoint.z);
            if (ctx == CatchContext.Floor) dragOffset = Vector3.ClampMagnitude(dragOffset, 1.3f);

            if (ctx == CatchContext.Doorway)
            {
                float best = 3f;
                foreach (Collider col in Physics.OverlapSphere(startRoot + Vector3.up, 2f, 1 << Mathf.Max(0, GameLayers.InteractableLayer), QueryTriggerInteraction.Ignore))
                {
                    Door door = col.GetComponentInParent<Door>();
                    if (door == null || door.kind == DoorKind.Storage) continue;
                    float dist = Vector3.Distance(door.transform.position, startRoot);
                    if (dist < best) { best = dist; nearDoor = door; }
                }
            }

            // Take over the victim.
            PlayerInventory inv = p.GetComponent<PlayerInventory>();
            if (inv != null) inv.DropAtFeet();
            p.SetControlLock(true, true);
            if (look != null) look.ExternallyDriven = true;
            if (local) InputService.Instance?.SetGameplayEnabled(false);
            if (b.motor != null) b.motor.Freeze(EndT + intro);
            if (anim != null)
            {
                anim.StopAction();
                anim.Puppet = pose;
            }
            pose.weight = 0f;
            pose.handL = anim != null && anim.handL != null ? anim.handL.position : ePos;
            pose.handR = anim != null && anim.handR != null ? anim.handR.position : ePos;
        }

        static int PlayerMask => GameLayers.PlayerLayer >= 0 ? 1 << GameLayers.PlayerLayer : 0;

        Vector3 Face => anim != null && anim.head != null ? anim.head.position + anim.head.forward * 0.1f : ePos + Vector3.up * headH;
        Vector3 Mouth => anim != null ? anim.MouthPosition : ePos + Vector3.up * headH;

        bool Fire(int bit, float at, float now)
        {
            if (now < at || (fired & (1 << bit)) != 0) return false;
            fired |= 1 << bit;
            return true;
        }

        float Noise(float speed, float offset) => Mathf.PerlinNoise(seed * 0.013f + offset, t * speed) * 2f - 1f;

        static float Ease(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / Mathf.Max(0.0001f, b - a)));

        void Sfx(ProceduralSounds.Sfx s, Vector3 at, float vol, float pitchLo = 0.85f, float pitchHi = 1.1f)
        {
            float pitch = pitchLo + (float)rng.NextDouble() * (pitchHi - pitchLo);
            AudioPool.PlayAt(ProceduralSounds.Effect(s), at, vol, pitch, 35f);
        }

        // =====================================================================

        void Update()
        {
            if (player == null || brain == null) { Finish(); return; }
            float dt = Time.deltaTime;
            t += dt;
            float u = t - intro;

            if (!released)
            {
                Vector3 eye;
                Quaternion body;
                Vector3 lookAt;
                float roll = 0f, shake = 0f, sharp = 14f;
                if (u < 0f) Intro(t, out eye, out body, out lookAt, ref shake, ref sharp);
                else Main(u, out eye, out body, out lookAt, ref roll, ref shake, ref sharp);
                Apply(eye, body, lookAt, roll, shake, sharp, dt);
            }
            Events(u);

            // Screen effects.
            flash = Mathf.MoveTowards(flash, 0f, dt * 1.6f);
            if (u > LandT && u < ResetT) black = Mathf.Max(black, Ease(LandT + 0.4f, BlackT, u));
            if (u > ResetT + 0.2f) black = 1f - Ease(ResetT + 0.2f, EndT, u);
            float wantListener = u >= StillT && u < ScreamT ? 0.2f : u > BlackT ? 0.3f : 1f;
            listener = Mathf.MoveTowards(listener, wantListener, dt * 4f);
            if (local)
            {
                AudioListener.volume = listener * SettingsService.CurrentOrDefault.masterVolume;
                // Your hearing: muffled in the dead-still moment, wide open for the scream and the bite,
                // then going under as you're dropped.
                if (ears == null && look != null && look.playerCamera != null)
                {
                    ears = look.playerCamera.GetComponent<AudioLowPassFilter>();
                    if (ears == null) ears = look.playerCamera.gameObject.AddComponent<AudioLowPassFilter>();
                }
                if (ears != null)
                {
                    ears.enabled = true;
                    float want = u >= StillT && u < ScreamT ? 700f : u > LandT ? Mathf.Lerp(22000f, 450f, Mathf.InverseLerp(LandT, BlackT, u)) : 22000f;
                    if (u > ResetT) want = 22000f;
                    ears.cutoffFrequency = Mathf.Lerp(ears.cutoffFrequency, want, 1f - Mathf.Exp(-12f * dt));
                }
            }

            if (u >= EndT) Finish();
        }

        // ---------------- context openings ----------------

        void Intro(float time, out Vector3 eye, out Quaternion body, out Vector3 lookAt, ref float shake, ref float sharp)
        {
            eye = startEye;
            body = startBody;
            lookAt = Face;
            pose.weight = Mathf.Clamp01(time / 0.15f);
            pose.look = startEye;
            pose.jaw = 12f;
            pose.lean = 0f;
            pose.headShake = 0f;
            pose.headPush = Vector3.zero;
            Vector3 sL = ShoulderPoint(startEye, startBody, -1f), sR = ShoulderPoint(startEye, startBody, 1f);

            switch (ctx)
            {
                case CatchContext.HidingPlace:
                {
                    // Door ripped open; its face pushes right into the opening; long hands reach in.
                    Vector3 doorFront = spot != null ? spot.outsidePoint + Vector3.up * 1.2f - fwd * 0.2f : startEye - fwd * 0.6f;
                    pose.handL = doorFront - right * 0.35f;
                    pose.handR = doorFront + right * 0.35f;
                    Vector3 want = startEye - fwd * 0.38f + Vector3.up * 0.05f;
                    pose.headPush = (want - headStart) * Ease(0.15f, 0.55f, time);
                    pose.lean = 25f * Ease(0.15f, 0.55f, time);
                    pose.jaw = 6f;
                    float reach = Ease(0.55f, 0.95f, time);
                    pose.handL = Vector3.Lerp(pose.handL, sL, reach);
                    pose.handR = Vector3.Lerp(pose.handR, sR, reach);
                    sharp = 7f;
                    shake = time < 0.25f ? 0.6f : 0.15f;
                    break;
                }
                case CatchContext.Vent:
                case CatchContext.Floor:
                {
                    // Grabbed by the ankles and dragged backwards, still facing the way you were going.
                    float reach = Ease(0f, ctx == CatchContext.Vent ? 0.5f : 0.3f, time);
                    float drag = Mathf.Pow(Ease(ctx == CatchContext.Vent ? 0.5f : 0.3f, intro, time), 1.3f);
                    Vector3 offset = dragOffset * drag;
                    eye = startEye + offset + Vector3.up * (Noise(9f, 3f) * 0.025f);
                    Vector3 grip = grabPoint + offset;
                    pose.handR = Vector3.Lerp(pose.handR, grip + right * 0.06f, reach);
                    pose.handL = Vector3.Lerp(pose.handL, grip - right * 0.06f, reach * 0.8f);
                    pose.lean = 40f;
                    pose.look = grip;
                    lookAt = startEye + startView * Vector3.forward * 3f + offset;
                    sharp = 4f;
                    shake = 0.35f + 0.25f * drag;
                    break;
                }
                default:
                {
                    // Turned to face it. From the side it grabs your near arm and yanks you round; from behind it
                    // grabs both shoulders, hauls you back off your feet and spins you. The view whips round with you.
                    float k = Ease(0.05f, intro, time);
                    float yawDelta = Mathf.DeltaAngle(startYaw, faceYaw);
                    // turn the way it pulls (the side it's on), even if that's the long way round
                    if (Mathf.Sign(yawDelta) != turnSign && Mathf.Abs(yawDelta) > 1f) yawDelta += 360f * turnSign;
                    float yaw = startYaw + yawDelta * k;
                    body = Quaternion.Euler(0f, yaw, 0f);
                    Vector3 pullTo = ePos + fwd * 0.75f;
                    pullTo.y = startEye.y;
                    float stumble = Mathf.Sin(k * Mathf.PI) * (approach == Approach.Behind ? 0.16f : 0.08f);
                    eye = Vector3.Lerp(startEye, pullTo, Ease(0f, intro, time) * 0.6f) + Vector3.down * stumble;
                    Vector3 behind = eye - (body * Vector3.forward) * 0.25f;
                    if (approach == Approach.Behind)
                    {
                        pose.handL = Vector3.Lerp(pose.handL, Vector3.Lerp(behind - right * 0.2f, sL, k), Ease(0f, 0.15f, time));
                        pose.handR = Vector3.Lerp(pose.handR, Vector3.Lerp(behind + right * 0.2f, sR, k), Ease(0f, 0.15f, time));
                    }
                    else
                    {
                        // the near arm first, then the other shoulder
                        Vector3 arm = eye + (Quaternion.Euler(0f, startYaw, 0f) * new Vector3(turnSign * 0.25f, -0.45f, 0f));
                        pose.handR = Vector3.Lerp(pose.handR, Vector3.Lerp(arm, sR, k), Ease(0f, 0.12f, time));
                        pose.handL = Vector3.Lerp(pose.handL, sL, Ease(0.2f, intro, time));
                    }
                    lookAt = Vector3.Lerp(startEye + (startView * Vector3.forward) * 3f, Face, Ease(0.1f, intro, time));
                    sharp = approach == Approach.Behind ? 9f : 12f;
                    shake = 0.7f;
                    break;
                }
            }
        }

        // ---------------- shared sequence ----------------

        void Main(float u, out Vector3 eye, out Quaternion body, out Vector3 lookAt, ref float roll, ref float shake, ref float sharp)
        {
            if ((fired & (1 << 20)) == 0)
            {
                fired |= 1 << 20;
                eyeAtMain = curEye;
                bodyAtMain = curBody;
                if (spot != null) spot.Release();
                player.IsHidden = false;
                player.CurrentHidingSpot = null;
            }

            pose.weight = Mathf.MoveTowards(pose.weight, 1f, Time.deltaTime * 6f);
            if (ctx == CatchContext.VentInside) { VentMain(u, out eye, out body, out lookAt, ref roll, ref shake, ref sharp); return; }
            Quaternion held = Quaternion.LookRotation(-fwd) * Quaternion.Euler(0f, 0f, upsideDown ? 180f * Ease(LiftT, StillT, u) : 0f);
            float lift = Ease(LiftT, StillT, u);
            eye = ctx == CatchContext.VentInside ? startEye : Vector3.Lerp(eyeAtMain, holdEye, lift);
            body = Quaternion.Slerp(bodyAtMain, held, Ease(GrabT, StillT, u));
            lookAt = Face;
            pose.look = eye;
            pose.jaw = 14f;
            pose.lean = upsideDown ? 28f : 6f;
            pose.headShake = 0f;
            pose.headPush = upsideDown ? Vector3.down * 0.45f * lift : Vector3.zero;
            sharp = 16f;

            if (u < LiftT)
            {
                shake = 0.9f * (1f - u / LiftT); // the jolt of being grabbed
            }
            else if (u < StillT)
            {
                shake = 0.25f;
                eye += Vector3.up * Mathf.Sin(u * 30f) * 0.01f;
            }
            else if (u < ScreamT)
            {
                // Dead still. Silence. It just looks at you, head tilted.
                pose.jaw = 2f;
                pose.headPush += right * 0.06f * Ease(StillT, ScreamT, u);
                shake = 0.02f;
                sharp = 20f;
            }
            else if (u < RecoilT)
            {
                // The scream: jaw unhinged, head shaking, right in your face.
                float open = Ease(ScreamT, ScreamT + 0.12f, u);
                pose.jaw = 52f * open + 6f * Mathf.Sin(u * 40f);
                pose.headShake = 9f;
                pose.headPush += fwd * 0.14f * open;
                shake = 0.75f;
                eye += -fwd * 0.04f * open;
            }
            else if (u < BiteT)
            {
                // Draws back, jaw opening far too wide.
                float k = Ease(RecoilT, BiteT, u);
                pose.jaw = Mathf.Lerp(52f, 78f, k);
                pose.headPush += -fwd * 0.18f * k + Vector3.up * 0.05f * k;
                shake = 0.2f;
            }
            else if (u < ThrashT)
            {
                // The bite: lunges at your neck.
                float k = Ease(BiteT, BiteHitT, u);
                Vector3 neck = eye - (held * Vector3.up) * 0.2f;
                Vector3 want = neck - fwd * 0.08f;
                pose.headPush = Vector3.Lerp(-fwd * 0.18f, want - headStart, k);
                pose.jaw = u < BiteHitT ? 80f : 8f;
                shake = u < BiteHitT ? 0.3f : 1.4f;
                sharp = 30f;
            }
            else if (u < DropT)
            {
                // Thrashing with its teeth in you.
                Vector3 neck = eye - (held * Vector3.up) * 0.2f;
                pose.headPush = neck - fwd * 0.06f - headStart;
                pose.jaw = 10f;
                pose.headShake = 30f;
                Vector3 jerk = new Vector3(Noise(13f, 1f), Noise(11f, 2f), Noise(12f, 3f)) * 0.12f;
                eye += jerk;
                roll = Noise(7f, 4f) * 35f;
                shake = 1.2f;
                lookAt = Face + jerk * 3f;
            }
            else if (Ragdoll(u, fwd * 0.9f + Vector3.down * 0.6f, out eye, out body, out lookAt))
            {
                // Dropped, limp (a real ragdoll now). It crouches over the body and feeds.
                roll = 0f;
                shake = u < LandT + 0.1f ? 0.8f : 0.25f;
                pose.headShake = 0f; pose.jaw = 20f; pose.headPush = Vector3.zero;
                pose.handL = Vector3.Lerp(pose.handL, pose.handL + Vector3.down * 0.3f, Ease(DropT, DropT + 0.2f, u));
                if (u >= FeedT) Feed(u, ref lookAt, ref shake, ref sharp);
                return;
            }
            else
            {
                // Dropped. Then it crouches over you and feeds.
                float fall = Mathf.Pow(Mathf.Clamp01((u - DropT) / (LandT - DropT)), 2f);
                Quaternion lying = Quaternion.LookRotation(-fwd) * Quaternion.Euler(-90f, 0f, 14f);
                Vector3 from = ctx == CatchContext.VentInside ? startEye : holdEye;
                Vector3 to = ctx == CatchContext.VentInside ? startEye - Vector3.up * 0.15f : floorEye;
                eye = Vector3.Lerp(from, to, fall);
                body = ctx == CatchContext.VentInside ? held : Quaternion.Slerp(held, lying, fall);
                roll = u < LandT ? Noise(9f, 5f) * 40f : 25f;
                shake = u < LandT + 0.1f ? 1f : 0.3f;
                pose.headShake = 0f;
                pose.jaw = 20f;
                pose.headPush = Vector3.zero;

                if (u >= FeedT)
                {
                    Vector3 chest = eye - (body * Vector3.up) * 0.45f;
                    float lower = Ease(FeedT, FeedT + 0.5f, u);
                    pose.lean = 65f * lower;
                    pose.look = chest;
                    float bite = Mathf.Abs(Mathf.Sin((u - FeedT) * 7.2f));
                    pose.headPush = (chest + Vector3.up * (0.25f + 0.12f * bite) - headStart) * lower;
                    pose.jaw = 8f + 46f * bite;
                    pose.headShake = 14f * lower;
                    pose.handL = chest - right * 0.25f;
                    pose.handR = chest + right * 0.25f + fwd * 0.3f;
                    shake = 0.25f + 0.6f * (1f - bite);
                    lookAt = Face;
                    sharp = 6f;
                    return;
                }
            }

            // Hands: on the shoulders, or round the ankles when held upside down.
            Vector3 gL = upsideDown && u >= LiftT ? AnklePoint(eye, body, -1f) : ShoulderPoint(eye, body, -1f);
            Vector3 gR = upsideDown && u >= LiftT ? AnklePoint(eye, body, 1f) : ShoulderPoint(eye, body, 1f);
            float grip = u < DropT ? Ease(GrabT, GrabT + 0.2f, u) : 1f - Ease(DropT, DropT + 0.15f, u);
            pose.handL = Vector3.Lerp(pose.handL, gL, grip);
            pose.handR = Vector3.Lerp(pose.handR, gR, grip);
            if (ctx == CatchContext.DarkRoom && u < LiftT) sharp = 11f;
        }

        // ---------------- the limp body ----------------

        PlayerBody victimBody;

        /// <summary>
        /// From the drop on, the victim is a physics ragdoll and the view rides on its head (tumbling, then lying
        /// where it fell, still turned up towards Elliot). Returns false if there's no body to ragdoll.
        /// </summary>
        bool Ragdoll(float u, Vector3 throwVel, out Vector3 eye, out Quaternion body, out Vector3 lookAt)
        {
            eye = curEye; body = curBody; lookAt = Face;
            if (victimBody == null) victimBody = player.GetComponent<PlayerBody>();
            if (victimBody == null || !victimBody.Ready) return false;
            if (!ragdolled)
            {
                ragdolled = true;
                var r = new System.Random(seed + 77);
                PlayerRagdoll.Begin(victimBody, throwVel, new Vector3((float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f) * 3f);
            }
            Transform head = victimBody.Bone("Head");
            if (head == null) return false;
            eye = head.position + head.up * 0.1f + head.forward * 0.08f;
            body = head.rotation;
            // your eyes stay on it (as much as a limp head allows)
            Vector3 toFace = (Face - eye).normalized;
            Vector3 dir = Vector3.Slerp(head.forward, toFace, Ease(DropT + 0.4f, FeedT + 0.6f, u) * 0.75f);
            lookAt = eye + dir * 2f;
            return true;
        }

        /// <summary>Crouched over the body, tearing at it; each bite jerks the limp body.</summary>
        void Feed(float u, ref Vector3 lookAt, ref float shake, ref float sharp)
        {
            Transform chestB = victimBody != null ? victimBody.Bone("Spine") : null;
            Vector3 chest = chestB != null ? chestB.position + Vector3.up * 0.1f : curEye - Vector3.up * 0.4f;
            float lower = Ease(FeedT, FeedT + 0.5f, u);
            pose.lean = 65f * lower;
            pose.look = chest;
            float bite = Mathf.Abs(Mathf.Sin((u - FeedT) * 7.2f));
            pose.headPush = (chest + Vector3.up * (0.25f + 0.12f * bite) - headStart) * lower;
            pose.jaw = 8f + 46f * bite;
            pose.headShake = 14f * lower;
            pose.handL = chest - right * 0.25f;
            pose.handR = chest + right * 0.25f + fwd * 0.3f;
            shake = 0.25f + 0.6f * (1f - bite);
            lookAt = Vector3.Lerp(lookAt, Face, 0.5f);
            sharp = 6f;
        }

        // ---------------- caught inside a duct ----------------

        /// <summary>
        /// Face to face in the duct: it grabs you by the shoulders and drags you along the metal to it, stops
        /// dead with its face a hand's width from yours, screams, bites, thrashes you against the walls of the
        /// duct (the whole duct booms), then you go limp where you lie and it feeds.
        /// </summary>
        void VentMain(float u, out Vector3 eye, out Quaternion body, out Vector3 lookAt, ref float roll, ref float shake, ref float sharp)
        {
            body = Quaternion.LookRotation(-fwd);
            float drag = Ease(LiftT, StillT, u);
            Vector3 dragged = startEye + fwd * -0.55f * drag;
            eye = dragged;
            lookAt = Face;
            pose.look = eye;
            pose.lean = 0f;
            pose.headShake = 0f;
            Vector3 eyeNow = eye;
            Vector3 faceAt(float dist) => eyeNow - fwd * dist + Vector3.up * 0.02f - headStart;
            sharp = 16f;
            if (u < LiftT) { pose.jaw = 14f; pose.headPush = faceAt(0.55f) * Ease(GrabT, LiftT, u); shake = 0.9f * (1f - u / LiftT); }
            else if (u < StillT) { pose.jaw = 10f; pose.headPush = faceAt(0.45f); shake = 0.4f; eye += Vector3.up * Mathf.Sin(u * 25f) * 0.01f; }
            else if (u < ScreamT) { pose.jaw = 2f; pose.headPush = faceAt(0.32f) + right * 0.04f * Ease(StillT, ScreamT, u); shake = 0.02f; sharp = 20f; }
            else if (u < RecoilT)
            {
                float open = Ease(ScreamT, ScreamT + 0.12f, u);
                pose.jaw = 52f * open + 6f * Mathf.Sin(u * 40f);
                pose.headShake = 9f;
                pose.headPush = faceAt(0.22f);
                shake = 0.75f;
            }
            else if (u < BiteT) { float k = Ease(RecoilT, BiteT, u); pose.jaw = Mathf.Lerp(52f, 78f, k); pose.headPush = faceAt(0.22f + 0.15f * k); shake = 0.2f; }
            else if (u < ThrashT) { float k = Ease(BiteT, BiteHitT, u); pose.jaw = u < BiteHitT ? 80f : 8f; pose.headPush = Vector3.Lerp(faceAt(0.37f), eye - Vector3.up * 0.14f - headStart, k); shake = u < BiteHitT ? 0.3f : 1.4f; sharp = 30f; }
            else if (u < DropT)
            {
                // thrashed from side to side against the duct walls
                pose.headPush = eye - Vector3.up * 0.14f - headStart;
                pose.jaw = 10f;
                pose.headShake = 30f;
                Vector3 jerk = right * (Noise(9f, 1f) * 0.22f) + Vector3.up * (Noise(11f, 2f) * 0.05f);
                eye += jerk;
                roll = Noise(7f, 4f) * 40f;
                shake = 1.3f;
                lookAt = Face + jerk * 2f;
            }
            else if (Ragdoll(u, Vector3.down * 0.4f + right * (Noise(3f, 9f) * 0.5f), out eye, out body, out lookAt))
            {
                shake = 0.3f;
                pose.headShake = 0f; pose.jaw = 20f; pose.headPush = Vector3.zero;
                if (u >= FeedT) Feed(u, ref lookAt, ref shake, ref sharp);
                return;
            }
            // hands on your shoulders
            float grip = u < DropT ? Ease(GrabT, GrabT + 0.2f, u) : 1f;
            pose.handL = Vector3.Lerp(pose.handL, ShoulderPoint(eye, body, -1f) + Vector3.up * 0.05f, grip);
            pose.handR = Vector3.Lerp(pose.handR, ShoulderPoint(eye, body, 1f) + Vector3.up * 0.05f, grip);
        }

        Vector3 ShoulderPoint(Vector3 eye, Quaternion body, float side) => eye + body * new Vector3(0.19f * side, -0.27f, 0.02f);
        Vector3 AnklePoint(Vector3 eye, Quaternion body, float side) => eye + body * new Vector3(0.09f * side, -eyeH + 0.08f, 0f);

        void Apply(Vector3 eye, Quaternion body, Vector3 lookAt, float roll, float shake, float sharp, float dt)
        {
            curEye = eye;
            curBody = body;
            Vector3 rigLocal = new Vector3(0f, eyeH, 0f);
            if (!ragdolled) player.MoveBodyTo(eye - body * rigLocal, body);
            if (look == null || look.cameraRig == null) return;
            if (ragdolled) look.cameraRig.position = eye;
            else look.cameraRig.localPosition = rigLocal;
            if (look.playerCamera != null)
            {
                look.playerCamera.transform.localPosition = Vector3.zero;
                look.playerCamera.transform.localRotation = Quaternion.identity;
            }
            Vector3 dir = lookAt - eye;
            if (dir.sqrMagnitude < 1e-4f) dir = body * Vector3.forward;
            Quaternion want = Quaternion.LookRotation(dir, body * Vector3.up);
            float s = shake * (SettingsService.CurrentOrDefault.cameraShake ? 1f : 0.3f);
            want *= Quaternion.Euler(Noise(23f, 10f) * 4f * s, Noise(21f, 11f) * 4f * s, roll + Noise(19f, 12f) * 6f * s);
            view = Quaternion.Slerp(view, want, 1f - Mathf.Exp(-sharp * dt));
            look.cameraRig.rotation = view;
        }

        // ---------------- sounds, blood, world events ----------------

        void Events(float u)
        {
            Vector3 eye = curEye;
            bool goreOn = SettingsService.CurrentOrDefault.gore;
            GoreKit kit = goreOn ? GoreKit.Instance : null;
            ElliotFootprints.Resoak();

            // Openings.
            if (Fire(12, -intro, u))
            {
                if (ctx == CatchContext.HidingPlace && spot != null) spot.RipOpen(brain.gameObject);
                if (intro > 0f && (ctx == CatchContext.DarkRoom || ctx == CatchContext.Chase || ctx == CatchContext.Doorway))
                {
                    Sfx(ProceduralSounds.Sfx.Thud, eye, 0.8f);
                    Sfx(ProceduralSounds.Sfx.Gasp, eye, 0.7f, 0.95f, 1.1f);
                }
                if (ctx == CatchContext.VentInside) Sfx(ProceduralSounds.Sfx.DuctBuckle, eye, 1f, 0.8f, 1f);
            }
            if ((ctx == CatchContext.Vent || ctx == CatchContext.Floor) && Fire(13, -intro + 0.35f, u))
            {
                AudioPool.PlayAt(ProceduralSounds.ScrapeLoop(ctx == CatchContext.Vent ? SurfaceType.Metal : SurfaceType.Concrete), eye, 0.9f, 0.8f, 25f);
                if (kit != null)
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 along = startEye + dragOffset * (0.25f + i * 0.3f);
                        GoreDecal.SpawnOnSurface(kit.smearMaterial, along + Vector3.up * 0.2f, Vector3.down, 2.5f, 0.7f, 1.2f, seed + i);
                    }
            }
            if (ctx == CatchContext.VentInside)
            {
                if (Fire(22, LiftT, u)) AudioPool.PlayAt(ProceduralSounds.ScrapeLoop(SurfaceType.Metal), eye, 0.9f, 0.9f, 25f);
                for (int i = 0; i < 4; i++)
                    if (Fire(23 + i, ThrashT + 0.1f + i * 0.3f, u)) Sfx(ProceduralSounds.Sfx.DuctBuckle, eye + right * (i % 2 == 0 ? 0.4f : -0.4f), 1f, 0.75f, 1.05f);
            }
            if (ctx == CatchContext.Doorway && Fire(14, 0.2f, u) && nearDoor != null)
            {
                nearDoor.Close(2.6f, brain.gameObject);
            }

            // Shared.
            if (Fire(0, GrabT, u))
            {
                Sfx(ProceduralSounds.Sfx.Thud, eye, 1f);
                if (local) { AudioPool.Play2D(AudioLibrary.Get("sub_hit"), 1f); AudioMix.Kick(1f, EndT); }
            }
            if (Fire(1, LiftT + 0.1f, u)) Voice("elliot_snarl", 0.9f);
            if (Fire(15, ThrashT + 0.05f, u)) Voice("elliot_snarl", 1f);
            if (Fire(16, ThrashT + 0.7f, u)) Voice(rng.NextDouble() < 0.5 ? "elliot_roar" : "elliot_shriek", 1f);
            if (Fire(17, FeedT + 0.15f, u)) Voice("elliot_snarl", 0.9f);
            if (Fire(18, FeedT + 1.0f, u)) Voice(rng.NextDouble() < 0.6 ? "elliot_feed" : rng.NextDouble() < 0.5 ? "elliot_roar" : "elliot_snarl", 0.85f);
            if (Fire(19, FeedT + 1.6f, u)) Voice("elliot_scream", 0.7f);
            if (Fire(2, ScreamT, u))
            {
                if (anim != null && anim.audioOut != null) anim.audioOut.Scream();
                // ...and right in your face: the same scream, close and unfiltered.
                if (local) AudioPool.Play2D(AudioLibrary.Get("elliot_scream"), 0.85f, UnityEngine.Random.Range(0.97f, 1.03f));
            }
            if (Fire(3, BiteHitT, u))
            {
                Sfx(ProceduralSounds.Sfx.Crunch, eye, 1f);
                if (local) AudioPool.Play2D(AudioLibrary.Get("sub_hit"), 1f, 0.85f);
                Sfx(ProceduralSounds.Sfx.Tear, eye, 0.9f);
                Sfx(ProceduralSounds.Sfx.Splatter, eye, 1f);
                if (goreOn) BloodSpray.Burst(Mouth, -fwd + Vector3.up * 0.4f, 160, 3.5f, 0.6f, seed);
                if (goreOn) BloodSpray.Burst(Mouth, fwd + Vector3.up * 0.2f, 80, 2.5f, 0.8f, seed + 1);
                if (kit != null)
                {
                    GoreDecal.SpawnOnSurface(kit.spatterMaterial, eye, (fwd + Vector3.up * 0.15f).normalized, 5f, 1.6f, 0.15f, seed);
                    GoreDecal.SpawnOnSurface(kit.spatterMaterial, eye, Vector3.down, 4f, 1.1f, 0.2f, seed + 2);
                }
                flash = goreOn ? 1f : 0.5f;
                lens = goreOn ? 0.85f : 0f;
            }
            if (Fire(4, ThrashT + 0.45f, u))
            {
                Sfx(ProceduralSounds.Sfx.Tear, eye, 1f, 0.7f, 0.9f);
                if (goreOn) BloodSpray.Burst(Mouth, right + Vector3.up * 0.5f, 90, 3f, 0.7f, seed + 4);
                flash = Mathf.Max(flash, 0.5f);
            }
            if (Fire(5, ThrashT + 0.9f, u))
            {
                Sfx(ProceduralSounds.Sfx.Crunch, eye, 1f, 0.75f, 0.95f);
                if (goreOn) BloodSpray.Burst(Mouth, -right + Vector3.up * 0.3f, 110, 3.2f, 0.7f, seed + 5);
                if (kit != null) GoreDecal.SpawnOnSurface(kit.spatterMaterial, eye, (right * 0.7f + fwd * 0.3f).normalized, 4f, 1.2f, 0.15f, seed + 5);
                flash = Mathf.Max(flash, 0.6f);
                if (goreOn) lens = 1f;
            }
            if (Fire(6, LandT, u))
            {
                Sfx(ProceduralSounds.Sfx.Thud, eye, 1f, 0.7f, 0.8f);
                Sfx(ProceduralSounds.Sfx.Splatter, eye, 0.8f);
                if (kit != null) GoreDecal.SpawnOnSurface(kit.poolMaterial, eye + Vector3.up * 0.3f - (curBody * Vector3.up) * 0.5f, Vector3.down, 2f, 2.1f, 6f, seed + 6);
            }
            for (int i = 0; i < 3; i++)
            {
                if (Fire(7 + i, FeedT + 0.35f + i * 0.44f, u))
                {
                    Sfx(i == 1 ? ProceduralSounds.Sfx.Tear : ProceduralSounds.Sfx.Crunch, Mouth, 1f, 0.7f, 1f);
                    // the body jerks as it's torn at
                    if (victimBody != null)
                    {
                        PlayerRagdoll.Push(victimBody, "Spine", (Vector3.up * 0.6f - fwd * 0.4f + right * (i - 1) * 0.5f) * 38f);
                        PlayerRagdoll.Push(victimBody, i == 1 ? "UpperArmL" : "UpperArmR", Vector3.up * 6f + right * (i - 1) * 4f);
                    }
                    if (goreOn) BloodSpray.Burst(Mouth, Vector3.up + right * (i - 1), 70, 2.2f, 0.8f, seed + 7 + i);
                    flash = Mathf.Max(flash, 0.35f);
                }
            }
            if (Fire(10, ResetT, u)) ResetRun();
        }

        /// <summary>Elliot's voice during the kill: from its mouth (everyone hears it there), varied takes.</summary>
        void Voice(string key, float vol)
        {
            AudioClip c = AudioLibrary.Get(key);
            if (c == null) return;
            if (anim != null && anim.audioOut != null && anim.audioOut.voice != null) anim.audioOut.PlayVoice(c, vol);
            else AudioPool.PlayAt(c, Mouth, vol, 1f, 40f);
        }

        void ResetRun()
        {
            flash = 0f;
            lens = 0f;
            ReleasePlayer();
            if (anim != null) { anim.Puppet = null; }
            // Co-op: only the caught player starts over (the others carry on). Single player: everything resets.
            if (CoopRespawn != null) { CoopRespawn(brain, player); return; }
            RunReset.Execute();
        }

        void ReleasePlayer()
        {
            if (released) return;
            released = true;
            if (player != null)
            {
                PlayerRagdoll.End(player.GetComponent<PlayerBody>());
                player.SetControlLock(false, false);
                if (look != null) look.ExternallyDriven = false;
                Vector3 e = player.transform.eulerAngles;
                player.MoveBodyTo(player.transform.position, e.y);
            }
            if (local) InputService.Instance?.SetGameplayEnabled(true);
        }

        void Finish()
        {
            if (!released) ResetRun();
            if (local) AudioListener.volume = SettingsService.CurrentOrDefault.masterVolume;
            if (ears != null) { ears.cutoffFrequency = 22000f; ears.enabled = false; }
            if (anim != null && anim.Puppet == pose) anim.Puppet = null;
            active.Remove(this);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            active.Remove(this);
        }

        void OnGUI()
        {
            if (!local) return;
            float w = Screen.width, h = Screen.height;
            Rect full = new Rect(0f, 0f, w, h);
            if (flash > 0.01f)
            {
                GUI.color = new Color(0.55f, 0f, 0f, flash * 0.65f);
                GUI.DrawTexture(full, Texture2D.whiteTexture);
            }
            GoreKit kit = GoreKit.Instance;
            if (lens > 0.01f && kit != null && kit.lensTexture != null && black < 0.99f)
            {
                GUI.color = new Color(1f, 1f, 1f, lens * (1f - black));
                GUI.DrawTexture(full, kit.lensTexture, ScaleMode.StretchToFill, true);
            }
            if (black > 0.01f)
            {
                GUI.color = new Color(0f, 0f, 0f, black);
                GUI.DrawTexture(full, Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();
            Started = null;
        }
    }
}
