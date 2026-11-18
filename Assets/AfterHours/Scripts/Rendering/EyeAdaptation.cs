using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AfterHours.Rendering
{
    /// <summary>
    /// Simple eye adaptation (URP has no auto-exposure): measures how much baked light reaches the camera
    /// (from light probes) and slowly adjusts exposure. Stepping from a bright concourse into a dark corridor,
    /// the corridor first looks pitch black, then your eyes adjust a little. Kept deliberately limited so
    /// darkness stays dark.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class EyeAdaptation : MonoBehaviour
    {
        public float targetLuminance = 0.22f;
        [Tooltip("How strongly exposure follows the light level (0 = off, 1 = full).")]
        public float strength = 0.55f;
        public float minExposure = -0.8f;
        public float maxExposure = 1.1f;
        [Tooltip("Adapting to darkness is slow...")]
        public float darkAdaptSpeed = 0.35f;
        [Tooltip("...adapting to brightness is fast.")]
        public float brightAdaptSpeed = 2.0f;

        ColorAdjustments colorAdjustments;
        float baseExposure;
        float current;

        void Start()
        {
            // The main global post-processing volume (not a power-cut area or the ending's daylight).
            Volume volume = null;
            foreach (Volume v in FindObjectsByType<Volume>())
            {
                if (!v.isGlobal || v.GetComponent<AfterHours.Power.PowerZone>() != null) continue;
                if (volume == null || v.priority < volume.priority) volume = v;
            }
            if (volume != null && volume.profile != null && volume.profile.TryGet(out colorAdjustments))
            {
                baseExposure = colorAdjustments.postExposure.value;
            }
        }

        void LateUpdate()
        {
            if (colorAdjustments == null) return;
            float target = 0f;
            LightProbes probes = LightmapSettings.lightProbes;
            if (probes != null && probes.count > 0)
            {
                LightProbes.GetInterpolatedProbe(transform.position, null, out SphericalHarmonicsL2 sh);
                float lum = Mathf.Max(0.003f, 0.2126f * sh[0, 0] + 0.7152f * sh[1, 0] + 0.0722f * sh[2, 0]);
                target = Mathf.Clamp(Mathf.Log(targetLuminance / lum, 2f) * strength, minExposure, maxExposure);
            }
            float speed = target > current ? darkAdaptSpeed : brightAdaptSpeed;
            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
            colorAdjustments.postExposure.value = baseExposure + current;
        }
    }
}
