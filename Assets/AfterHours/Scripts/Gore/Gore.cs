using System.Collections.Generic;
using UnityEngine;
using AfterHours.Puzzles;

namespace AfterHours.Gore
{
    /// <summary>Materials and textures for blood (set up by the editor when Elliot is built).</summary>
    public class GoreKit : MonoBehaviour
    {
        public Material poolMaterial;
        public Material spatterMaterial;
        public Material smearMaterial;
        public Material dropMaterial;
        public Texture2D lensTexture;

        static GoreKit instance;
        public static GoreKit Instance
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<GoreKit>();
                return instance;
            }
        }
    }

    /// <summary>
    /// A blood decal on a surface (a thin quad hugging the floor or wall). It spreads out over a few seconds.
    /// Cleared when the run resets.
    /// </summary>
    public class GoreDecal : MonoBehaviour, IResettable
    {
        float targetSize, growTime, t;

        public static GoreDecal Spawn(Material m, Vector3 point, Vector3 normal, float size, float growSeconds, float spin)
        {
            if (m == null) return null;
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Blood";
            Collider c = q.GetComponent<Collider>();
            if (c != null) Destroy(c);
            q.GetComponent<Renderer>().sharedMaterial = m;
            q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            q.transform.SetPositionAndRotation(point + normal * (0.004f + Random.value * 0.003f),
                Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, spin));
            GoreDecal d = q.AddComponent<GoreDecal>();
            d.targetSize = size;
            d.growTime = Mathf.Max(0.01f, growSeconds);
            q.transform.localScale = Vector3.one * size * (growSeconds > 0.05f ? 0.15f : 1f);
            return d;
        }

        /// <summary>Puts a decal on whatever surface a ray hits (walls, floors, furniture).</summary>
        public static GoreDecal SpawnOnSurface(Material m, Vector3 from, Vector3 dir, float maxDist, float size, float grow, int seed)
        {
            int mask = AfterHours.Core.GameLayers.CharacterBlockingMask;
            foreach (int layer in new[] { AfterHours.Core.GameLayers.PlayerLayer, AfterHours.Core.GameLayers.CreatureLayer,
                                          AfterHours.Core.GameLayers.PropLayer, AfterHours.Core.GameLayers.InteractableLayer })
                if (layer >= 0) mask &= ~(1 << layer);
            if (!Physics.Raycast(from, dir, out RaycastHit hit, maxDist, mask, QueryTriggerInteraction.Ignore)) return null;
            if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) return null;
            return Spawn(m, hit.point, hit.normal, size, grow, (seed * 47) % 360);
        }

        void Update()
        {
            if (t >= growTime) return;
            t += Time.deltaTime;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / growTime), 3f);
            transform.localScale = Vector3.one * targetSize * Mathf.Lerp(0.15f, 1f, k);
        }

        public void ResetState() => Destroy(gameObject);
    }

    /// <summary>Blood droplets: a shared particle system that sprays, falls and splashes on the floor.</summary>
    public static class BloodSpray
    {
        static ParticleSystem system;

        static ParticleSystem Get()
        {
            if (system != null) return system;
            var go = new GameObject("[Blood spray]");
            system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.035f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.02f, 0.03f), new Color(0.25f, 0.01f, 0.01f));
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;
            ParticleSystem.EmissionModule em = system.emission;
            em.enabled = false;
            ParticleSystem.ShapeModule sh = system.shape;
            sh.enabled = false;
            ParticleSystem.CollisionModule col = system.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.dampen = 0.9f;
            col.bounce = 0.05f;
            col.lifetimeLoss = 0.35f;
            col.quality = ParticleSystemCollisionQuality.Medium;
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 1.2f;
            GoreKit kit = GoreKit.Instance;
            if (kit != null && kit.dropMaterial != null) r.sharedMaterial = kit.dropMaterial;
            system.Play();
            return system;
        }

        /// <summary>Sprays 'count' droplets from a point in a cone around 'direction'.</summary>
        public static void Burst(Vector3 position, Vector3 direction, int count, float speed, float spread, int seed)
        {
            ParticleSystem ps = Get();
            var rng = new System.Random(seed);
            var p = new ParticleSystem.EmitParams();
            Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.up;
            for (int i = 0; i < count; i++)
            {
                Vector3 jitter = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 2f * spread;
                p.position = position + jitter * 0.05f;
                p.velocity = (dir + jitter).normalized * speed * (0.35f + (float)rng.NextDouble());
                p.startSize = 0.006f + (float)rng.NextDouble() * 0.03f;
                ps.Emit(p, 1);
            }
        }
    }
}
