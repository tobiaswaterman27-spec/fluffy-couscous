using System.Collections.Generic;
using UnityEngine;
using AfterHours.Audio;

namespace AfterHours.World
{
    /// <summary>
    /// Sparks, dust and splinters, made in code (no particle assets to set up). Bursts come from small pools of
    /// world-space particle systems, so a door being smashed or a breaker arcing costs almost nothing.
    ///   Sparks: white-hot streaks that fall, bounce off the floor and fade to orange, with a flash of light.
    ///   Dust: soft grey puffs that drift down slowly (door frames shaking, ceiling tiles).
    ///   Splinters: small wood chips flung out, tumbling and bouncing.
    /// </summary>
    public static class WorldFX
    {
        enum Kind { Spark, Dust, Splinter, Breath }
        static readonly Dictionary<Kind, ParticleSystem> systems = new Dictionary<Kind, ParticleSystem>();
        static Transform root;
        static Texture2D dot, puff;

        static Texture2D Dot(bool soft)
        {
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = soft ? Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f) * (0.7f + 0.3f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f)) : Mathf.Clamp01(1f - r * r);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        static Material Mat(Texture2D tex, bool additive)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
            var m = new Material(sh);
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            m.SetFloat("_Surface", 1f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            return m;
        }

        static ParticleSystem Get(Kind k)
        {
            if (systems.TryGetValue(k, out ParticleSystem ps) && ps != null) return ps;
            if (root == null) { root = new GameObject("[World FX]").transform; Object.DontDestroyOnLoad(root.gameObject); }
            if (dot == null) { dot = Dot(false); puff = Dot(true); }
            var go = new GameObject(k.ToString());
            go.transform.SetParent(root, false);
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 600;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            switch (k)
            {
                case Kind.Spark:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
                    main.gravityModifier = 1f;
                    main.startColor = new Color(2.6f, 2.0f, 1.2f, 1f);    // HDR: blooms
                    r.renderMode = ParticleSystemRenderMode.Stretch;
                    r.velocityScale = 0.035f; r.lengthScale = 1f;
                    r.sharedMaterial = Mat(dot, true);
                    g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.35f), new GradientColorKey(new Color(0.7f, 0.15f, 0.02f), 1f) },
                              new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
                    var c = ps.collision; c.enabled = true; c.type = ParticleSystemCollisionType.World; c.bounce = 0.35f; c.dampen = 0.4f; c.lifetimeLoss = 0.25f;
                    c.quality = ParticleSystemCollisionQuality.Low; c.radiusScale = 0.5f;
                    break;
                case Kind.Dust:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
                    main.gravityModifier = 0.03f;
                    main.startColor = new Color(0.55f, 0.52f, 0.48f, 0.35f);
                    main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    r.renderMode = ParticleSystemRenderMode.Billboard;
                    r.sharedMaterial = Mat(puff, false);
                    g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                              new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.15f), new GradientAlphaKey(0f, 1f) });
                    var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
                    var dr = ps.limitVelocityOverLifetime; dr.enabled = true; dr.drag = 2.5f;
                    break;
                case Kind.Breath:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.0f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                    main.gravityModifier = -0.012f;                                  // warm air rises a touch
                    main.startColor = new Color(0.85f, 0.87f, 0.9f, 0.16f);
                    main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    r.renderMode = ParticleSystemRenderMode.Billboard;
                    r.sharedMaterial = Mat(puff, false);
                    g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                              new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.08f), new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0f, 1f) });
                    var bs = ps.sizeOverLifetime; bs.enabled = true; bs.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 3.2f));
                    var bd = ps.limitVelocityOverLifetime; bd.enabled = true; bd.drag = 3.5f;
                    var bn = ps.noise; bn.enabled = true; bn.strength = 0.08f; bn.frequency = 0.8f; bn.scrollSpeed = 0.3f;
                    break;
                case Kind.Splinter:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.5f);
                    main.startSize3D = true;
                    main.startSizeX = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
                    main.startSizeY = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
                    main.startSizeZ = new ParticleSystem.MinMaxCurve(0.004f, 0.008f);
                    main.startRotation3D = true;
                    main.gravityModifier = 1f;
                    main.startColor = new Color(0.45f, 0.33f, 0.22f, 1f);
                    r.renderMode = ParticleSystemRenderMode.Mesh;
                    r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                    Shader lit = Shader.Find("Universal Render Pipeline/Particles/Lit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    r.sharedMaterial = new Material(lit);
                    var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
                    rot.x = new ParticleSystem.MinMaxCurve(-12f, 12f); rot.y = new ParticleSystem.MinMaxCurve(-12f, 12f); rot.z = new ParticleSystem.MinMaxCurve(-12f, 12f);
                    var c2 = ps.collision; c2.enabled = true; c2.type = ParticleSystemCollisionType.World; c2.bounce = 0.25f; c2.dampen = 0.55f; c2.quality = ParticleSystemCollisionQuality.Low;
                    g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                              new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
                    break;
            }
            col.color = g;
            ps.Play();
            systems[k] = ps;
            return ps;
        }

        static void Emit(Kind k, Vector3 at, Vector3 dir, int count, float speed, float spread)
        {
            ParticleSystem ps = Get(k);
            var p = new ParticleSystem.EmitParams();
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
            for (int i = 0; i < count; i++)
            {
                p.position = at + Random.insideUnitSphere * (k == Kind.Dust ? 0.12f : 0.02f);
                p.velocity = (dir + Random.insideUnitSphere * spread).normalized * speed * Random.Range(0.35f, 1.1f);
                p.applyShapeToPosition = false;
                ps.Emit(p, 1);
            }
        }

        /// <summary>A shower of sparks with a flash of light and a crackle.</summary>
        public static void Sparks(Vector3 at, Vector3 dir, int count = 40, float loudness = 0.7f)
        {
            Emit(Kind.Spark, at, dir, count, 3.2f, 0.9f);
            var go = new GameObject("Spark flash");
            go.transform.position = at;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.75f, 0.45f); l.range = 4f; l.intensity = 4f;
            l.shadows = LightShadows.None;
            go.AddComponent<FlashFade>().life = 0.18f;
            AudioClip c = AudioLibrary.Get("sfx_spark");
            if (c != null) AudioPool.PlayAt(c, at, loudness, Random.Range(0.9f, 1.15f), 18f);
            NoiseBus.Emit(new NoiseEvent(at, 5f, NoiseKind.Machinery, null));
        }

        /// <summary>Dust and grit shaken loose (e.g. from a door frame being hammered).</summary>
        public static void Dust(Vector3 at, Vector3 dir, int count = 14) => Emit(Kind.Dust, at, dir, count, 0.4f, 1.2f);

        /// <summary>
        /// A breath of fog in the cold (the heating went off when the mall closed). 'strength' 0..1: a calm breath
        /// is a faint wisp, a panting one a proper cloud. Skipped when nobody's close enough to see it.
        /// </summary>
        public static void Breath(Vector3 mouth, Vector3 dir, float strength = 0.5f)
        {
            Camera c = Camera.main;
            if (c == null || (c.transform.position - mouth).sqrMagnitude > 18f * 18f) return;
            int count = Mathf.RoundToInt(Mathf.Lerp(3f, 9f, strength));
            ParticleSystem ps = Get(Kind.Breath);
            var p = new ParticleSystem.EmitParams();
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            for (int i = 0; i < count; i++)
            {
                p.position = mouth + Random.insideUnitSphere * 0.015f;
                p.velocity = (dir + Random.insideUnitSphere * 0.35f + Vector3.down * 0.25f).normalized * Mathf.Lerp(0.35f, 0.9f, strength) * Random.Range(0.6f, 1.1f);
                p.applyShapeToPosition = false;
                ps.Emit(p, 1);
            }
        }

        /// <summary>Wood splinters flung from a breaking door, plus dust.</summary>
        public static void Splinters(Vector3 at, Vector3 dir, int count = 60)
        {
            Emit(Kind.Splinter, at, dir, count, 4f, 0.8f);
            Emit(Kind.Dust, at, dir, 24, 0.8f, 1.4f);
        }

        class FlashFade : MonoBehaviour
        {
            public float life = 0.2f;
            float t;
            Light l;
            float start;
            void Start() { l = GetComponent<Light>(); start = l != null ? l.intensity : 0f; }
            void Update()
            {
                t += Time.deltaTime;
                if (l != null) l.intensity = start * Mathf.Clamp01(1f - t / life) * (Random.value < 0.3f ? 0.4f : 1f);
                if (t >= life) Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// A live wire (hanging out of a broken ceiling, or behind a failing light) that spits sparks now and then
    /// while the power is on in its area.
    /// </summary>
    public class SparkingWire : MonoBehaviour
    {
        public float minInterval = 4f, maxInterval = 16f;
        public int seed;
        float next;
        System.Random rng;

        void Start()
        {
            rng = new System.Random(seed);
            next = Time.time + (float)rng.NextDouble() * maxInterval;
        }

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + minInterval + (float)rng.NextDouble() * (maxInterval - minInterval);
            if (AfterHours.Power.PowerGrid.LightFactorAt(transform.position) < 0.5f) return;   // dead wire when the power's off
            if (Camera.main != null && (Camera.main.transform.position - transform.position).sqrMagnitude > 45f * 45f) return;
            WorldFX.Sparks(transform.position, Vector3.down + Random.insideUnitSphere * 0.3f, 15 + (int)(rng.NextDouble() * 35), 0.5f);
            // sometimes it stutters: two or three quick bursts
            if (rng.NextDouble() < 0.35) next = Time.time + 0.12f + (float)rng.NextDouble() * 0.25f;
        }
    }
}
