using UnityEngine;

namespace AfterHours.Rendering
{
    /// <summary>
    /// Tiny specks of dust drifting in the air around you - you only really see them where there is light, like in
    /// a real building at night. One particle system that follows the camera; brightness follows the light level.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DustMotes : MonoBehaviour
    {
        public Material material;
        ParticleSystem ps;
        float timer;

        void Start()
        {
            if (material == null) { enabled = false; return; }
            var go = new GameObject("Dust motes");
            go.transform.SetParent(transform, false);
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 12f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.012f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            main.startColor = new Color(1f, 0.97f, 0.9f, 0.3f);
            var em = ps.emission;
            em.rateOverTime = 45f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(9f, 4f, 9f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.05f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
        }

        void Update()
        {
            if (ps == null) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.5f;
            float light = AfterHours.Creature.ElliotSenses.LightLevelAt(transform.position);
            var main = ps.main;
            main.startColor = new Color(1f, 0.97f, 0.9f, Mathf.Lerp(0.02f, 0.38f, light));
        }
    }
}
