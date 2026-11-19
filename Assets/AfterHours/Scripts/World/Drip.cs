using UnityEngine;
using AfterHours.Audio;

namespace AfterHours.World
{
    /// <summary>
    /// A leak: every few seconds a drop forms, falls and plinks into the puddle below - you hear it land at the
    /// right moment, from the right place. (Drips also hide quieter sounds very slightly nearby.)
    /// </summary>
    public class Drip : MonoBehaviour
    {
        public Material dropMaterial;
        public float height = 3f;
        public float minInterval = 1.2f, maxInterval = 4f;
        [Tooltip("Comes through the roof: drips faster when it's raining harder (Weather.Intensity).")]
        public bool rainFed;
        ParticleSystem ps;
        float timer, fall;
        float landAt = -1f;

        void Start()
        {
            timer = Random.Range(0f, maxInterval);
            fall = Mathf.Sqrt(2f * height / 9.81f);
            var go = new GameObject("Drops");
            go.transform.SetParent(transform, false);
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = fall;
            main.startSpeed = 0f;
            main.startSize = 0.012f;
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.8f, 0.85f, 0.9f, 0.6f);
            var em = ps.emission;
            em.enabled = false;
            var sh = ps.shape;
            sh.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.02f;
            r.lengthScale = 2f;
            if (dropMaterial != null) r.sharedMaterial = dropMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = Random.Range(minInterval, maxInterval);
                if (rainFed) timer /= Mathf.Lerp(0.6f, 2.2f, Weather.Intensity);
                if (ps != null) ps.Emit(1);
                landAt = Time.time + fall;
            }
            if (landAt > 0f && Time.time >= landAt)
            {
                landAt = -1f;
                AudioClip c = AudioLibrary.Get("drip");
                if (c != null) AudioPool.PlayAt(c, transform.position + Vector3.down * height, Random.Range(0.25f, 0.45f), Random.Range(0.85f, 1.2f), 18f);
            }
        }
    }
}
