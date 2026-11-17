using UnityEngine;

namespace AfterHours.World
{
    /// <summary>
    /// A few moths batting round a light that still works: tiny dark flecks circling, darting, bumping the
    /// fitting. They vanish when its power goes. Only simulated when someone is close enough to see them.
    /// </summary>
    public class Moths : MonoBehaviour
    {
        public int count = 6;
        public float radius = 0.35f;
        ParticleSystem ps;
        static Material mat;
        bool running;

        void Start()
        {
            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 9999f; main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
            main.startColor = new Color(0.12f, 0.11f, 0.1f, 0.9f);
            main.maxParticles = count; main.gravityModifier = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius;
            // erratic flight, held round the light by a pull to the centre and a slow orbit
            var n = ps.noise; n.enabled = true; n.strength = 1.6f; n.frequency = 2.2f; n.scrollSpeed = 1.3f; n.quality = ParticleSystemNoiseQuality.Medium;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = -0.9f; vel.orbitalY = 1.4f; vel.orbitalX = 0.3f;
            var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 1.4f; lim.dampen = 0.3f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-20f, 20f);
            var r = GetComponent<ParticleSystemRenderer>();
            if (mat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
                mat = new Material(s);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            if (ps == null) return;
            Camera c = Camera.main;
            bool near = c != null && (c.transform.position - transform.position).sqrMagnitude < 20f * 20f;
            bool lit = AfterHours.Power.PowerGrid.LightFactorAt(transform.position) > 0.5f;
            bool want = near && lit;
            if (want == running) return;
            running = want;
            if (want) ps.Play(); else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
