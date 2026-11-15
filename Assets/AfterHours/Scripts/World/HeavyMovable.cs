using System.Collections.Generic;
using UnityEngine;
using AfterHours.Audio;

namespace AfterHours.World
{
    /// <summary>
    /// A heavy object (vending machine, cabinet, crate stack) that players move by grabbing it (hold E).
    /// One person moves it slowly; each extra person makes it faster (co-op ready). It always moves
    /// physically through velocity, so it collides with walls and other objects and is never teleported.
    /// It scrapes loudly, and the scraping is a real noise event creatures can hear.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class HeavyMovable : MonoBehaviour, AfterHours.Puzzles.IResettable
    {
        Vector3 startPos;
        Quaternion startRot;

        /// <summary>Loading a save.</summary>
        public void PlaceAt(Vector3 p, Quaternion r)
        {
            grips.Clear();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = p;
            body.rotation = r;
            transform.SetPositionAndRotation(p, r);
        }

        public void ResetState()
        {
            grips.Clear();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = startPos;
            body.rotation = startRot;
            transform.SetPositionAndRotation(startPos, startRot);
        }

        [Header("Speed")]
        public float soloSpeed = 0.55f;
        public float extraSpeedPerHelper = 0.35f;
        public float maxSpeed = 1.4f;
        public float soloTurnRate = 35f;
        public float extraTurnPerHelper = 20f;
        public float acceleration = 3f;

        [Header("Sound")]
        public SurfaceType material = SurfaceType.Metal;
        public float scrapeNoiseRadius = 10f;

        struct Grip
        {
            public Vector3 position;
            public float yaw;
        }

        readonly Dictionary<object, Grip> grips = new Dictionary<object, Grip>();
        Rigidbody body;
        AudioSource scrape;
        float noiseTimer;

        public Rigidbody Body => body;
        public int GripCount => grips.Count;
        public float AllowedSpeed => Mathf.Min(maxSpeed, soloSpeed + extraSpeedPerHelper * Mathf.Max(0, grips.Count - 1));
        public float AllowedTurnRate => soloTurnRate + extraTurnPerHelper * Mathf.Max(0, grips.Count - 1);

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            startPos = transform.position;
            startRot = transform.rotation;
            // Elliot paths around it while it stands still.
            var obstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            scrape = gameObject.AddComponent<AudioSource>();
            scrape.clip = ProceduralSounds.ScrapeLoop(material);
            scrape.loop = true;
            scrape.spatialBlend = 1f;
            scrape.rolloffMode = AudioRolloffMode.Logarithmic;
            scrape.minDistance = 1.5f;
            scrape.maxDistance = 35f;
            scrape.volume = 0f;
            scrape.playOnAwake = false;
        }

        /// <summary>Called every frame by each player holding the object: where they want it to be.</summary>
        public void SetGrip(object holder, Vector3 targetPosition, float targetYaw)
        {
            grips[holder] = new Grip { position = targetPosition, yaw = targetYaw };
        }

        public void Release(object holder)
        {
            grips.Remove(holder);
        }

        /// <summary>How far the object is from where this holder wants it (used to lose grip when it jams).</summary>
        public float GripError(object holder)
        {
            if (!grips.TryGetValue(holder, out Grip g)) return 0f;
            Vector3 d = g.position - body.position;
            d.y = 0f;
            return d.magnitude;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 v = body.linearVelocity;
            Vector3 flat = new Vector3(v.x, 0f, v.z);

            if (grips.Count > 0)
            {
                Vector3 target = Vector3.zero;
                Vector2 yawVec = Vector2.zero;
                foreach (Grip g in grips.Values)
                {
                    target += g.position;
                    yawVec += new Vector2(Mathf.Sin(g.yaw * Mathf.Deg2Rad), Mathf.Cos(g.yaw * Mathf.Deg2Rad));
                }
                target /= grips.Count;
                float targetYaw = Mathf.Atan2(yawVec.x, yawVec.y) * Mathf.Rad2Deg;

                Vector3 error = target - body.position;
                error.y = 0f;
                Vector3 desired = Vector3.ClampMagnitude(error / 0.25f, AllowedSpeed);
                flat = Vector3.MoveTowards(flat, desired, acceleration * dt);
                body.linearVelocity = new Vector3(flat.x, v.y, flat.z);

                float yawError = Mathf.DeltaAngle(body.rotation.eulerAngles.y, targetYaw);
                float rate = Mathf.Clamp(yawError / 0.3f, -AllowedTurnRate, AllowedTurnRate);
                body.angularVelocity = new Vector3(0f, rate * Mathf.Deg2Rad, 0f);
            }

            UpdateScrape(flat.magnitude + Mathf.Abs(body.angularVelocity.y) * 0.3f, dt);
        }

        void UpdateScrape(float speed, float dt)
        {
            float target = Mathf.Clamp01(speed / Mathf.Max(0.1f, soloSpeed)) * 0.8f;
            scrape.volume = Mathf.MoveTowards(scrape.volume, target, dt * 4f);
            if (scrape.volume > 0.01f && !scrape.isPlaying) scrape.Play();
            else if (scrape.volume <= 0.01f && scrape.isPlaying) scrape.Stop();
            scrape.pitch = 0.85f + Mathf.Clamp01(speed) * 0.3f;

            if (speed > 0.08f)
            {
                noiseTimer -= dt;
                if (noiseTimer <= 0f)
                {
                    noiseTimer = 0.35f;
                    float radius = scrapeNoiseRadius * Mathf.Clamp(speed / soloSpeed, 0.5f, 1.6f);
                    NoiseBus.Emit(new NoiseEvent(body.position, radius, NoiseKind.Impact, gameObject));
                }
            }
        }
    }
}
