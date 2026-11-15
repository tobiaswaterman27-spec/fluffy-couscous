using System;
using System.Collections.Generic;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Core;
using AfterHours.Player;
using AfterHours.World.Layout;

namespace AfterHours.Creature
{
    /// <summary>A sound Elliot believes it heard: where it THINKS it came from, and how strongly.</summary>
    public struct HeardSound
    {
        public Vector3 estimatedPosition;
        public float strength;     // 0..1
        public NoiseKind kind;
        public float time;
        public GameObject source;
    }

    /// <summary>
    /// What Elliot can perceive. It never knows where a player is unless it actually sees or hears them.
    ///
    /// Sight: a wide cone from its head, blocked by walls, closed doors and big objects. Awareness builds up over
    /// time (faster when close, moving, standing, lit; slower at the edge of vision, crouched or crawling).
    /// Darkness only slightly reduces it: it sees well in the dark. Very close = noticed almost at once.
    ///
    /// Hearing: every noise event, weakened by distance, walls and doors between, and masked by loud background
    /// (machinery, ventilation). It only gets an approximate position - worse through walls and in echoing spaces
    /// (atrium, car park) - so it can go to the wrong corridor.
    /// </summary>
    public class ElliotSenses : MonoBehaviour
    {
        public ElliotAnimator animator;
        public float sightRange = 38f;
        public float fieldOfView = 150f;
        public float nearSense = 1.8f;
        public float hearingScale = 1f;

        public event Action<PlayerController> Spotted;
        public event Action<HeardSound> Heard;

        readonly Dictionary<PlayerController, float> awareness = new Dictionary<PlayerController, float>();
        readonly RaycastHit[] hits = new RaycastHit[8];
        int visionMask;
        float tick;

        public float AwarenessOf(PlayerController p) => awareness.TryGetValue(p, out float a) ? a : 0f;

        void OnEnable() => NoiseBus.Emitted += OnNoise;
        void OnDisable() => NoiseBus.Emitted -= OnNoise;

        void Start()
        {
            visionMask = GameLayers.CharacterBlockingMask;
            foreach (int l in new[] { GameLayers.CreatureLayer, GameLayers.PlayerLayer })
            {
                if (l >= 0) visionMask &= ~(1 << l);
            }
        }

        public Vector3 EyePosition => animator != null && animator.head != null ? animator.head.position + Vector3.up * 0.2f : transform.position + Vector3.up * 2.3f;
        public Vector3 LookDirection => animator != null && animator.head != null ? animator.head.forward : transform.forward;

        void Update()
        {
            tick -= Time.deltaTime;
            if (tick > 0f) return;
            float dt = 0.1f + Mathf.Max(0f, -tick);
            tick = 0.1f;

            foreach (PlayerController p in PlayerController.All)
            {
                float a = AwarenessOf(p);
                if (CanSee(p, out float rate)) a += rate * dt;
                else a -= 0.25f * dt;
                a = Mathf.Clamp(a, 0f, 1.2f);
                bool was = AwarenessOf(p) >= 1f;
                awareness[p] = a;
                if (a >= 1f && !was) Spotted?.Invoke(p);
            }
        }

        /// <summary>Is this player visible right now, and how fast does awareness of them build?</summary>
        public bool CanSee(PlayerController p, out float rate)
        {
            rate = 0f;
            if (p == null || p.IsHidden) return false;
            Vector3 eye = EyePosition;
            Vector3 target = p.EyePosition;
            Vector3 chest = p.transform.position + Vector3.up * Mathf.Max(0.3f, p.Motor.CurrentHeight * 0.55f);
            float dist = Vector3.Distance(eye, chest);

            float light = LightLevelAt(chest);
            float range = sightRange * Mathf.Lerp(0.82f, 1f, light);
            if (dist > range) return false;

            Vector3 dir = (chest - eye).normalized;
            float angle = Vector3.Angle(Flat(LookDirection), Flat(dir));
            bool close = dist < nearSense;
            if (!close && angle > fieldOfView * 0.5f) return false;
            if (!LineOfSight(eye, target) && !LineOfSight(eye, chest)) return false;

            float distF = Mathf.Clamp(7f / (dist + 1f), 0.2f, 5f);
            float stanceF = p.Motor.CurrentStance == Stance.Crawling ? 0.45f : p.Motor.CurrentStance == Stance.Crouching ? 0.6f : 1f;
            float speed = p.Motor.Locomotion.planarSpeed;
            float motionF = speed < 0.2f ? 0.6f : speed > 4f ? 1.4f : 1f;
            float peripheral = angle > 55f ? 0.4f : 1f;
            float lightF = Mathf.Lerp(0.75f, 1.25f, light);
            rate = close ? 6f : distF * stanceF * motionF * peripheral * lightF;
            return true;
        }

        bool LineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            return !Physics.Raycast(from, d.normalized, d.magnitude - 0.1f, visionMask, QueryTriggerInteraction.Ignore);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>0 = dark, 1 = brightly lit (from baked light probes; mid-grey before baking).</summary>
        public static float LightLevelAt(Vector3 p)
        {
            LightProbes probes = LightmapSettings.lightProbes;
            if (probes == null || probes.count == 0) return 0.5f;
            LightProbes.GetInterpolatedProbe(p, null, out UnityEngine.Rendering.SphericalHarmonicsL2 sh);
            float lum = 0.2126f * sh[0, 0] + 0.7152f * sh[1, 0] + 0.0722f * sh[2, 0];
            return Mathf.Clamp01(lum / 0.35f);
        }

        void OnNoise(NoiseEvent e)
        {
            if (e.Source == gameObject || e.Kind == NoiseKind.Creature) return;
            Vector3 ear = EyePosition;
            float dist = Vector3.Distance(ear, e.Position);
            if (dist > e.Radius * hearingScale * 1.2f) return;

            // Walls and closed doors between muffle the sound.
            Vector3 dir = e.Position - ear;
            int n = Physics.RaycastNonAlloc(ear, dir.normalized, hits, dir.magnitude, visionMask, QueryTriggerInteraction.Ignore);
            int barriers = Mathf.Min(n, 4);
            float effective = e.Radius * hearingScale * Mathf.Pow(0.55f, barriers);

            // Loud surroundings hide quiet sounds.
            float masking = AmbientSound.MaskingAt(e.Position);
            if (e.Radius < 10f) effective *= 1f - 0.65f * masking;
            if (dist > effective) return;

            // It only knows roughly where: worse with distance, walls, and echoing spaces.
            ZoneKind zone = MallZones.Classify(e.Position).kind;
            float echo = zone == ZoneKind.Atrium || zone == ZoneKind.CarPark ? 0.12f : 0f;
            float error = dist * (0.06f + 0.1f * barriers + echo);
            Vector2 r = UnityEngine.Random.insideUnitCircle * error;
            Heard?.Invoke(new HeardSound
            {
                estimatedPosition = e.Position + new Vector3(r.x, 0f, r.y),
                strength = Mathf.Clamp01(1f - dist / effective) * Mathf.Clamp01(e.Radius / 15f + 0.3f),
                kind = e.Kind,
                time = Time.time,
                source = e.Source
            });
        }
    }
}
