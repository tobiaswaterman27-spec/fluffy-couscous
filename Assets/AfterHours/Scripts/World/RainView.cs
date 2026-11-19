using UnityEngine;

namespace AfterHours.World
{
    /// <summary>
    /// Rain you can see: a sheet of falling streaks in a box (outside the entrance glass), or splashes hitting a pane
    /// (on top of the atrium skylight, seen from below as tiny flickers). Follows the storm's intensity, and every
    /// streak lights up in a lightning flash. Built at runtime so the scene file stays small.
    /// </summary>
    public class RainView : MonoBehaviour
    {
        public Material material;
        public Vector3 size = new Vector3(20f, 6f, 4f);
        [Tooltip("Splashes on a surface instead of falling streaks.")]
        public bool splashes;
        public float rate = 900f;
        public Color tint = new Color(0.6f, 0.68f, 0.8f, 0.22f);
        [Tooltip("Rain around whoever is watching, but only while their camera is inside this area (out on the roof).")]
        public bool followCamera;
        public Bounds followArea;

        ParticleSystem ps;
        ParticleSystemRenderer psr;
        Light lightning;
        float timer;
        bool wasInside, covered;
        float coverTimer;

        void Start()
        {
            if (material == null) { enabled = false; return; }
            Weather w = GetComponentInParent<Weather>();
            if (w == null) w = FindAnyObjectByType<Weather>();
            if (w != null) lightning = w.lightning;

            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = splashes ? 800 : 3000;
            main.startColor = tint;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            if (splashes)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.4f));
            }
            else
            {
                // falls the box height in about 0.5 s (rain is ~9 m/s), a little wind slant
                float fall = 9f;
                main.startLifetime = size.y / fall;
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                vel.y = new ParticleSystem.MinMaxCurve(-fall * 1.1f, -fall * 0.9f);
                vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            // streaks start along the top of the box; splashes anywhere on the pane
            sh.scale = splashes ? new Vector3(size.x, 0.02f, size.z) : new Vector3(size.x, 0.05f, size.z);
            sh.position = splashes ? Vector3.zero : new Vector3(0f, size.y * 0.5f, 0f);

            psr = GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = material;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            if (!splashes)
            {
                psr.renderMode = ParticleSystemRenderMode.Stretch;
                psr.velocityScale = 0.045f;      // a streak ~0.4 m long, like a drop under a camera's shutter
                psr.lengthScale = 1f;
            }
            Apply(Weather.Intensity, 0f);
            ps.Play();
        }

        void Update()
        {
            if (ps == null) return;
            if (followCamera)
            {
                Camera cam = Camera.main;
                bool inside = cam != null && followArea.Contains(cam.transform.position);
                // under a roof (the stair core's top room, a doorway): no rain on you
                if (inside)
                {
                    coverTimer -= Time.deltaTime;
                    if (coverTimer <= 0f)
                    {
                        coverTimer = 0.3f;
                        covered = Physics.Raycast(cam.transform.position, Vector3.up, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    }
                    inside = !covered;
                }
                if (inside) transform.position = cam.transform.position + Vector3.up * (size.y * 0.5f - 1.6f);
                if (inside != wasInside) { wasInside = inside; timer = 0f; }
                if (!inside) { if (ps.emission.rateOverTime.constant > 0f) { var em0 = ps.emission; em0.rateOverTime = 0f; } return; }
            }
            float flash = lightning != null && lightning.enabled ? Mathf.Clamp01(lightning.intensity / 2f) : 0f;
            timer -= Time.deltaTime;
            if (timer > 0f && flash <= 0f) return;
            timer = 0.25f;
            Apply(Weather.Intensity, flash);
        }

        void Apply(float intensity, float flash)
        {
            var em = ps.emission;
            em.rateOverTime = rate * intensity;
            var main = ps.main;
            Color c = tint;
            c.a = Mathf.Clamp01(tint.a * (0.6f + 0.6f * intensity) + 0.6f * flash);
            c.r += 0.3f * flash; c.g += 0.3f * flash; c.b += 0.3f * flash;
            main.startColor = c;
        }
    }
}
