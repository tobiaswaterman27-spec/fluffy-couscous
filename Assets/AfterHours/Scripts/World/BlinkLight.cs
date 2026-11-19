using UnityEngine;

namespace AfterHours.World
{
    /// <summary>
    /// A car's hazard lights left on: an amber light and the lamp lenses blinking on their own battery, power cut
    /// or not, slowly dimming as the battery dies over the night.
    /// </summary>
    public class BlinkLight : MonoBehaviour
    {
        public Light glow;
        public Renderer[] lenses;
        [ColorUsage(false, true)] public Color emission = new Color(4f, 2.2f, 0.4f);
        public float period = 0.8f;
        [Tooltip("Seconds until the battery is nearly flat.")]
        public float batteryLife = 3600f;

        float baseIntensity, started;
        MaterialPropertyBlock block;
        bool lastOn;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Start()
        {
            baseIntensity = glow != null ? glow.intensity : 1f;
            started = Time.time;
            block = new MaterialPropertyBlock();
            Set(false, 1f);
        }

        void Update()
        {
            bool on = Mathf.Repeat(Time.time, period) < period * 0.5f;
            if (on == lastOn) return;
            lastOn = on;
            float charge = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01((Time.time - started) / batteryLife));
            Set(on, charge);
        }

        void Set(bool on, float charge)
        {
            if (glow != null) { glow.enabled = on; glow.intensity = baseIntensity * charge; }
            if (lenses == null) return;
            block.SetColor(EmissionId, on ? emission * charge : Color.black);
            foreach (Renderer r in lenses) if (r != null) r.SetPropertyBlock(block);
        }
    }
}
