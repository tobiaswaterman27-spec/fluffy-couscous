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
        Floor,       // caught crawling: dragged back across the floor by the leg, then flipped over and pinned
        CeilingDrop  // it was on the ceiling right above you, and let go
    }

    /// <summary>
    /// The death sequence: Elliot takes you down - each kind of catch has its own way (a lunge, a yank by the arm, a
    /// hand over your face from behind, dragged out of a hiding place or a vent, dropped on from the ceiling) - and
    /// you end up on your back with it crouched over you, pinning your shoulders. It goes completely still, screams
    /// into your face, rears back, bites into your neck and thrashes; you go limp and it feeds as the screen goes
    /// black. Gore included. (Caught inside a duct has its own face-to-face version.)
    ///
    /// Everything is a pure function of (context, seed, time since start, and where everyone stood at the start).
    /// The victim's body and Elliot's pose are driven in the WORLD, so in co-op every other player sees exactly
    /// what the victim sees in first person (the host just sends Begin's arguments; Part 16).
    /// Screen effects (blood on the lens, red flash, fade) are drawn only for the victim.
    /// </summary>
    public class CatchSequence : MonoBehaviour
    {
        // Beats, in seconds after the context's opening. The pin (every catch but inside a duct):
        //   0 taken down, on your back by PinT - fighting him - QuietT dead still - YellT the scream - RearT rears back,
        //   jaw unhinging - BiteHitT into your neck - ThrashT tearing - DropT limp (ragdoll) - FeedT feeding - black.
        float GrabT = 0f, LiftT = 0.5f, StillT = 1.7f, ScreamT = 2.2f, RecoilT = 3.5f, BiteT = 3.8f, BiteHitT = 3.92f,
              ThrashT = 4.0f, DropT = 5.1f, LandT = 5.15f, FeedT = 5.3f, BlackT = 6.9f, ResetT = 7.7f, EndT = 8.9f;

        void DuctBeats()
        {
            // caught inside a duct: the older, tighter timing (it drags you to it, face to face)
            GrabT = 0f; LiftT = 0.35f; StillT = 1.0f; ScreamT = 1.3f; RecoilT = 2.9f; BiteT = 3.2f; BiteHitT = 3.36f;
            ThrashT = 3.4f; DropT = 4.7f; LandT = 5.0f; FeedT = 5.1f; BlackT = 6.6f; ResetT = 7.4f; EndT = 8.6f;
        }

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
                struggle = u < c.BiteHitT ? 1f : 1f - Ease(c.BiteHitT, c.ThrashT + 0.6f, u);
                upside = false;
                prone = c.ctx == CatchContext.VentInside;
                return true;
            }
            gripL = gripR = Vector3.zero; struggle = 0f; upside = false; prone = false;
            return false;
        }

        /// <summary>
        /// The victim's whole body while it's being taken down and pinned (every catch but inside a duct, until it
        /// goes limp): where the eyes are, which way the body is turned (standing, falling, flat on its back), where
        /// Elliot's hands are, how hard it's still fighting, and whether it's crouched (dragged out of a hiding place).
        /// </summary>
        public static bool VictimBody(PlayerController p, out Vector3 eye, out Quaternion body, out Vector3 gripL, out Vector3 gripR, out float struggle, out bool crouched, out float floorY)
        {
            foreach (CatchSequence c in active)
            {
                if (c.player != p || c.ctx == CatchContext.VentInside || c.ragdolled) continue;
                float u = c.t - c.intro;
                eye = c.curEye; body = c.curBody;
                bool held = u >= 0f || c.ctx == CatchContext.HidingPlace && u > -0.4f;
                gripL = held ? c.pose.handL : Vector3.zero; gripR = held ? c.pose.handR : Vector3.zero;
                struggle = u < c.BiteHitT ? (u < 0f ? 0.6f : 1f) : 1f - Ease(c.BiteHitT, c.ThrashT + 0.8f, u);
                struggle = Mathf.Max(struggle, u < c.DropT ? 0.3f : 0f);
                crouched = c.ctx == CatchContext.HidingPlace && u < -0.4f;
                floorY = c.floorY;
                return true;
            }
            eye = Vector3.zero; body = Quaternion.identity; gripL = gripR = Vector3.zero; struggle = 0f; crouched = false; floorY = 0f;
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
        // the pin: where you end up lying and where he ends up crouched
        bool headFirst;
        Vector3 headDir, lieHead, lieEye, pinRoot;
        Quaternion lieBody;
        float floorY;
        Vector3 fallEye;
        Quaternion fallBody;
        bool fallSet;
        float handOverEyes;
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
                case CatchContext.HidingPlace: intro = 1.1f; break;
                case CatchContext.Vent: intro = 1.5f; break;
                case CatchContext.Floor: intro = 1.0f; break;
                case CatchContext.VentInside: intro = 0f; DuctBeats(); break;
                case CatchContext.CeilingDrop: intro = 1.15f; break;
                default:
                    // in front: it lunges and takes you down. From the side: yanked round by the arm. From behind
                    // (or in the dark): a hand over your face, pulled backwards off your feet.
                    if (ctx == CatchContext.DarkRoom) approach = Approach.Behind;
                    intro = approach == Approach.Behind ? 0.6f : approach == Approach.Side ? 0.55f : 0.35f;
                    break;
            }
            upsideDown = false;

            // Where you'll end up: on your back. Taken from behind, your head lands at his feet (he looms over you
            // upside down); every other way your feet are towards him and he climbs over you to your face.
            floorY = startRoot.y;
            if (ctx == CatchContext.Vent || ctx == CatchContext.Floor || ctx == CatchContext.VentInside) floorY = ePos.y;
            if (Physics.Raycast(new Vector3(startRoot.x, startRoot.y + 0.5f, startRoot.z), Vector3.down, out RaycastHit fl, 2f, GameLayers.CharacterBlockingMask & ~PlayerMask, QueryTriggerInteraction.Ignore))
                floorY = fl.point.y;
            headFirst = approach == Approach.Behind && ctx != CatchContext.Vent && ctx != CatchContext.Floor && ctx != CatchContext.HidingPlace && ctx != CatchContext.CeilingDrop;
            headDir = headFirst ? -fwd : fwd;
            float headAt = headFirst ? 0.62f : ctx == CatchContext.Vent ? 0.8f : 1.85f;
            float rootAt = headFirst ? -0.15f : ctx == CatchContext.Vent ? -0.45f : 0.55f;
            if (!headFirst && ctx != CatchContext.Vent)
            {
                // a wall in the way: you land shorter, and he comes in less far
                if (Physics.Raycast(new Vector3(ePos.x, floorY + 0.3f, ePos.z), fwd, out RaycastHit wall, 2.3f, GameLayers.CharacterBlockingMask & ~PlayerMask, QueryTriggerInteraction.Ignore))
                {
                    float room = wall.distance - 0.3f;
                    if (room < headAt) { rootAt -= headAt - room; headAt = room; }
                }
            }
            lieHead = new Vector3(ePos.x, floorY + 0.11f, ePos.z) + fwd * headAt;
            lieBody = Quaternion.LookRotation(Vector3.up, headDir);
            lieEye = lieHead + Vector3.up * 0.08f + headDir * 0.05f;
            pinRoot = fwd * rootAt;
            holdEye = upsideDown
                ? ePos + fwd * 0.8f + Vector3.up * (headH - 1.0f)
                : ePos + fwd * (ctx == CatchContext.HidingPlace ? 0.8f : 0.95f) + Vector3.up * Mathf.Max(headH - 0.05f, 2.3f);   // lifted to his face at the end of his long arms, never inside him
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
            pose.root = Vector3.zero;
            if (ctx == CatchContext.CeilingDrop && b.motor != null) b.motor.CeilingOverride = 1f;
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
            // offsets for his head are measured from where his own pose puts it this frame (crawling, it's far lower)
            if (anim != null && anim.NeutralHead.HasValue) headStart = anim.NeutralHead.Value;

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
            pose.jaw = 14f;
            pose.lean = 0f;
            pose.headShake = 0f;
            pose.headPush = Vector3.zero;
            pose.root = Vector3.zero;
            Vector3 ahead = startEye + (startView * Vector3.forward) * 3f;

            switch (ctx)
            {
                case CatchContext.HidingPlace:
                {
                    // The door torn open, its face in the gap - then long hands take you by the collar and haul you out.
                    float outK = Ease(0.55f, intro, time);
                    Vector3 inGap = startEye - fwd * 0.38f + Vector3.up * 0.05f;
                    pose.headPush = (inGap - headStart) * Ease(0.1f, 0.45f, time) * (1f - outK);
                    pose.lean = 25f * (1f - outK);
                    pose.jaw = time < 0.5f ? 6f : 40f;
                    Vector3 pulledTo = new Vector3(ePos.x, floorY + 1.2f, ePos.z) + fwd * 0.95f;
                    eye = Vector3.Lerp(startEye, pulledTo, outK) + Vector3.up * (Mathf.Sin(outK * Mathf.PI) * 0.15f);
                    Vector3 collar = eye + Vector3.down * 0.25f - fwd * 0.08f;
                    pose.handL = Vector3.Lerp(pose.handL, collar - right * 0.12f, Ease(0.4f, 0.6f, time));
                    pose.handR = Vector3.Lerp(pose.handR, collar + right * 0.12f, Ease(0.4f, 0.6f, time));
                    sharp = 8f;
                    shake = time < 0.3f ? 0.6f : 0.3f + 0.4f * outK;
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
                    lookAt = ahead + offset;
                    sharp = 4f;
                    shake = 0.35f + 0.25f * drag;
                    break;
                }
                case CatchContext.CeilingDrop:
                {
                    // A hiss right above you. You look up: it's on the ceiling over your head. It lets go,
                    // turning over in the air, and comes down on you.
                    float upK = Ease(0.15f, 0.45f, time);
                    lookAt = Vector3.Lerp(ahead, Face, upK);
                    float drop = Ease(0.55f, intro, time);
                    if (brain.motor != null) brain.motor.CeilingOverride = 1f - drop;
                    pose.root = fwd * (0.5f * drop);
                    pose.jaw = time > 0.5f ? 50f : 10f;
                    sharp = 10f;
                    shake = time > 0.55f ? 0.3f : 0.05f;
                    break;
                }
                default:
                {
                    if (approach == Approach.Front)
                    {
                        // It closes the gap in two strides, arms out, and hits you.
                        float k = Ease(0f, intro, time);
                        pose.root = fwd * (0.55f * k);
                        pose.lean = 35f * k;
                        pose.jaw = 30f;
                        pose.handL = Vector3.Lerp(pose.handL, startEye - right * 0.25f + Vector3.down * 0.35f - fwd * 0.15f, k);
                        pose.handR = Vector3.Lerp(pose.handR, startEye + right * 0.25f + Vector3.down * 0.35f - fwd * 0.15f, k);
                        sharp = 14f;
                        shake = 0.4f * k;
                    }
                    else if (approach == Approach.Side)
                    {
                        // Your near arm, yanked round to face it.
                        float k = Ease(0.05f, intro, time);
                        float yawDelta = Mathf.DeltaAngle(startYaw, faceYaw);
                        if (Mathf.Sign(yawDelta) != turnSign && Mathf.Abs(yawDelta) > 1f) yawDelta += 360f * turnSign;
                        body = Quaternion.Euler(0f, startYaw + yawDelta * k, 0f);
                        Vector3 pullTo = ePos + fwd * 0.8f; pullTo.y = startEye.y - 0.1f;
                        eye = Vector3.Lerp(startEye, pullTo, k * 0.6f);
                        Vector3 arm = eye + Quaternion.Euler(0f, startYaw, 0f) * new Vector3(turnSign * 0.25f, -0.45f, 0f);
                        pose.handR = Vector3.Lerp(pose.handR, Vector3.Lerp(arm, ShoulderPoint(eye, body, 1f), k), Ease(0f, 0.12f, time));
                        pose.handL = Vector3.Lerp(pose.handL, ShoulderPoint(eye, body, -1f), Ease(0.2f, intro, time));
                        lookAt = Vector3.Lerp(ahead, Face, Ease(0.1f, intro, time));
                        sharp = 12f;
                        shake = 0.7f;
                    }
                    else
                    {
                        // From behind: you don't see it. A long hand comes over your face; you're dragged back and down.
                        float back = Ease(0.15f, intro, time);
                        eye = startEye + Vector3.down * (0.25f * back) - startFwd * (0.25f * back);
                        body = startBody * Quaternion.Euler(-35f * back, 0f, 0f);
                        pose.handR = Vector3.Lerp(pose.handR, eye + (body * Vector3.forward) * 0.09f + Vector3.down * 0.02f, Ease(0f, 0.15f, time));
                        pose.handL = Vector3.Lerp(pose.handL, ShoulderPoint(eye, body, -1f), Ease(0.1f, 0.35f, time));
                        pose.headPush = (eye + right * 0.22f + Vector3.up * 0.15f - startFwd * 0.25f - headStart) * Ease(0.1f, intro, time);
                        lookAt = ahead + Vector3.up * (1.2f * back);
                        handOverEyes = Ease(0.05f, 0.2f, time);
                        sharp = 9f;
                        shake = 0.6f;
                    }
                    break;
                }
            }
        }

        // ---------------- the pin ----------------

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
            if (brain.motor != null && brain.motor.CeilingOverride.HasValue)
            {
                brain.motor.CeilingOverride = 0f;
                if (brain.motor.Gait == ElliotGait.Crawl) brain.motor.Drive(Vector3.zero, ElliotGait.Idle);   // landed: off all fours, onto you
            }
            handOverEyes = Mathf.Max(0f, handOverEyes - Time.deltaTime / 0.3f);

            if (u >= DropT && Ragdoll(u, Vector3.down * 0.2f, out eye, out body, out lookAt))
            {
                // Limp now (a real ragdoll). It stays crouched over you and feeds.
                roll = 0f;
                shake = 0.25f;
                pose.headShake = 0f; pose.jaw = 20f;
                pose.root = pinRoot;
                if (u >= FeedT) Feed(u, ref lookAt, ref shake, ref sharp);
                else
                {
                    pose.lean = 80f;
                    pose.headPush = (lieEye + Vector3.up * 0.35f - headStart);
                }
                return;
            }

            // Down: from wherever the opening left you, onto your back on the floor.
            float down = Ease(0f, LiftT, u);
            eye = Vector3.Lerp(eyeAtMain, lieEye, down) + Vector3.up * (Mathf.Sin(down * Mathf.PI) * 0.12f);
            body = Quaternion.Slerp(bodyAtMain, lieBody, down);
            pose.root = pinRoot * Ease(0f, LiftT + 0.15f, u);
            pose.look = lieEye;
            pose.lean = 85f;
            pose.headShake = 0f;
            lookAt = Face;
            sharp = 16f;
            Vector3 Above(float h) => lieEye + Vector3.up * h + headDir * (headFirst ? -0.04f : 0.02f) - headStart;
            Vector3 neck = lieHead - headDir * 0.2f + right * 0.06f + Vector3.up * 0.1f;

            if (u < LiftT)
            {
                pose.headPush = Above(0.55f) * down;
                pose.jaw = 30f;
                shake = 0.9f * (1f - down) + 0.3f;
            }
            else if (u < StillT)
            {
                // You fight; it holds you down, breathing on you.
                float b = Mathf.Sin(u * 2.4f);
                pose.headPush = Above(0.45f + 0.03f * b);
                pose.jaw = 12f + 6f * Mathf.Max(0f, b);
                shake = 0.35f;
            }
            else if (u < ScreamT)
            {
                // Dead still. Silence. Its face comes closer, head tilted.
                float k = Ease(StillT, ScreamT, u);
                pose.headPush = Above(0.3f - 0.06f * k) + right * (0.07f * k);
                pose.jaw = 2f;
                shake = 0.03f;
                sharp = 20f;
            }
            else if (u < RecoilT)
            {
                // The scream, right into your face.
                float open = Ease(ScreamT, ScreamT + 0.12f, u);
                pose.headPush = Above(0.22f);
                pose.jaw = 54f * open + 6f * Mathf.Sin(u * 40f);
                pose.headShake = 9f;
                shake = 0.8f;
            }
            else if (u < BiteT)
            {
                // Rears back, the jaw opening far too wide.
                float k = Ease(RecoilT, BiteT, u);
                pose.headPush = Above(0.22f + 0.4f * k);
                pose.jaw = Mathf.Lerp(54f, 80f, k);
                shake = 0.25f;
            }
            else if (u < ThrashT)
            {
                // Into your neck.
                float k = Ease(BiteT, BiteHitT, u);
                pose.headPush = Vector3.Lerp(Above(0.62f), neck - headStart, k);
                pose.jaw = u < BiteHitT ? 80f : 8f;
                shake = u < BiteHitT ? 0.3f : 1.5f;
                sharp = 30f;
            }
            else
            {
                // Tearing, its teeth in you.
                pose.headPush = neck - headStart;
                pose.jaw = 10f;
                pose.headShake = 30f;
                Vector3 jerk = new Vector3(Noise(13f, 1f), Noise(11f, 2f) * 0.4f, Noise(12f, 3f)) * 0.1f;
                eye += jerk;
                roll = Noise(7f, 4f) * 30f;
                shake = 1.2f;
                lookAt = Face + jerk * 3f;
            }
            // its hands: down onto your shoulders, pinning you to the floor
            Vector3 pinL = ShoulderPoint(eye, body, -1f), pinR = ShoulderPoint(eye, body, 1f);
            pinL.y = floorY + 0.14f; pinR.y = floorY + 0.14f;
            float g = Ease(0.1f, LiftT, u);
            pose.handL = Vector3.Lerp(pose.handL, pinL, g);
            pose.handR = Vector3.Lerp(pose.handR, pinR, g);
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
            bool duct = ctx == CatchContext.VentInside;
            pose.lean = duct ? 65f * lower : 80f;
            pose.look = chest;
            float bite = Mathf.Abs(Mathf.Sin((u - FeedT) * 7.2f));
            Vector3 feedAt = chest + Vector3.up * (0.28f + 0.12f * bite) - headStart;
            pose.headPush = duct ? feedAt * lower : Vector3.Lerp(lieEye + Vector3.up * 0.35f - headStart, feedAt, lower);
            pose.jaw = 8f + 46f * bite;
            pose.headShake = 14f * lower;
            pose.handL = Vector3.Lerp(pose.handL, chest - right * 0.25f + Vector3.down * 0.05f, 0.2f);
            pose.handR = Vector3.Lerp(pose.handR, chest + right * 0.25f + fwd * 0.25f + Vector3.down * 0.05f, 0.2f);
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
            else if (u < ThrashT) { float k = Ease(BiteT, BiteHitT, u); pose.jaw = u < BiteHitT ? 80f : 8f; pose.headPush = Vector3.Lerp(faceAt(0.37f), eye + fwd * 0.15f + Vector3.up * 0.12f - headStart, k); shake = u < BiteHitT ? 0.3f : 1.4f; sharp = 30f; }   // over the back of your neck
            else if (u < DropT)
            {
                // thrashed from side to side against the duct walls
                pose.headPush = eye + fwd * 0.15f + Vector3.up * 0.12f - headStart;
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

        /// <summary>
        /// Where Elliot's hand on HIS side 'side' (-1 left, +1 right) grips you: the outside of whichever of your
        /// upper arms is on that side of him (so his arms never cross, and his hands close on you, not inside you).
        /// </summary>
        Vector3 ShoulderPoint(Vector3 eye, Quaternion body, float side)
        {
            Vector3 a = eye + body * new Vector3(0.27f, -0.33f, 0f), b = eye + body * new Vector3(-0.27f, -0.33f, 0f);
            bool aIsHisRight = Vector3.Dot(a - b, right) > 0f;
            return side > 0f ? (aIsHisRight ? a : b) : (aIsHisRight ? b : a);
        }
        Vector3 AnklePoint(Vector3 eye, Quaternion body, float side)
        {
            Vector3 a = eye + body * new Vector3(0.1f, -eyeH + 0.1f, 0f), b = eye + body * new Vector3(-0.1f, -eyeH + 0.1f, 0f);
            bool aIsHisRight = Vector3.Dot(a - b, right) > 0f;
            return side > 0f ? (aIsHisRight ? a : b) : (aIsHisRight ? b : a);
        }

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
            if (ctx == CatchContext.CeilingDrop)
            {
                if (Fire(21, -intro, u)) Voice("elliot_clicks", 1f);                        // above you
                if (Fire(24, -intro + 0.5f, u)) Voice("elliot_shriek", 1f);                // and it lets go
            }
            if (ctx == CatchContext.HidingPlace && Fire(25, -intro + 0.6f, u)) Sfx(ProceduralSounds.Sfx.Thud, eye, 0.9f);
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
                if (local) { AudioPool.Play2D(AudioLibrary.Get("sub_hit"), 1f); AudioMix.Kick(1f, EndT); AfterHours.Controls.Rumble.Pulse(1f, 0.8f); }
            }
            if (Fire(1, LiftT + 0.1f, u)) Voice("elliot_snarl", 0.9f);
            if (ctx != CatchContext.VentInside && Fire(26, LiftT * 0.8f, u))
            {
                // your back hits the floor
                Sfx(ProceduralSounds.Sfx.Thud, eye, 1f, 0.7f, 0.8f);
                if (local) { AudioPool.Play2D(AudioLibrary.Get("sub_hit"), 0.8f, 0.8f); AudioMix.Kick(0.8f, 0.6f); AfterHours.Controls.Rumble.Pulse(1f, 0.5f); }
            }
            if (Fire(15, ThrashT + 0.05f, u)) Voice("elliot_snarl", 1f);
            if (Fire(16, ThrashT + 0.7f, u)) Voice(rng.NextDouble() < 0.5 ? "elliot_roar" : "elliot_shriek", 1f);
            if (Fire(17, FeedT + 0.15f, u)) Voice("elliot_snarl", 0.9f);
            if (Fire(18, FeedT + 1.0f, u)) Voice(rng.NextDouble() < 0.6 ? "elliot_feed" : rng.NextDouble() < 0.5 ? "elliot_roar" : "elliot_snarl", 0.85f);
            if (Fire(19, FeedT + 1.6f, u)) Voice("elliot_scream", 0.7f);
            if (Fire(2, ScreamT, u))
            {
                if (anim != null && anim.audioOut != null) anim.audioOut.Scream();
                // ...and right in your face: the same scream, close and unfiltered.
                if (local) { AudioPool.Play2D(AudioLibrary.Get("elliot_scream"), 0.85f, UnityEngine.Random.Range(0.97f, 1.03f)); AfterHours.Controls.Rumble.Pulse(0.6f, 1f); }
            }
            if (Fire(3, BiteHitT, u))
            {
                Sfx(ProceduralSounds.Sfx.Crunch, eye, 1f);
                if (local) { AudioPool.Play2D(AudioLibrary.Get("sub_hit"), 1f, 0.85f); AfterHours.Controls.Rumble.Pulse(1f, 1f); }
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
            // he stays where the takedown left him (his body had come onto you), instead of snapping back
            if (ctx != CatchContext.VentInside && brain != null && brain.motor != null && pinRoot.sqrMagnitude > 1e-4f)
                brain.motor.Warp(brain.transform.position + Vector3.ProjectOnPlane(pinRoot, Vector3.up), brain.transform.eulerAngles.y);
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
            if (brain != null && brain.motor != null) brain.motor.CeilingOverride = null;
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
            if (handOverEyes > 0.01f && black < 0.75f)
            {
                // its hand over your face (from behind): long fingers, light only between them
                GUI.color = new Color(0f, 0f, 0f, 0.75f * handOverEyes);
                GUI.DrawTexture(full, Texture2D.whiteTexture);
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
