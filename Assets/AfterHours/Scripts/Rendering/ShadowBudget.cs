using System.Collections.Generic;
using UnityEngine;

namespace AfterHours.Rendering
{
    /// <summary>
    /// Real-time shadows from the mall's own lights, where they matter: a few times a second the lights nearest
    /// the camera (and in front of it) get soft shadows and the rest none. So Elliot, other players and anything
    /// knocked over throw proper shadows from the fitting above them, and those shadows jump when a tube flickers,
    /// without paying for shadows from hundreds of lights at once. (Fully baked lights already have theirs.)
    /// </summary>
    public class ShadowBudget : MonoBehaviour
    {
        [Tooltip("How many point lights may cast real-time shadows at once (each costs six shadow maps).")]
        public int pointLights = 4;
        [Tooltip("How many spot lights may cast real-time shadows at once.")]
        public int spotLights = 6;
        public float maxDistance = 28f;

        readonly List<Light> lights = new List<Light>();
        readonly List<(float score, Light l)> scored = new List<(float, Light)>();
        float rescan, tick;
        Camera cam;

        void Update()
        {
            if (cam == null) cam = GetComponent<Camera>() ?? Camera.main;
            if (cam == null) return;
            rescan -= Time.unscaledDeltaTime;
            if (rescan <= 0f)
            {
                rescan = 5f;
                lights.Clear();
                foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l.type != LightType.Point && l.type != LightType.Spot) continue;
                    if (l.name == "Spark flash") continue;
                    if (l.bakingOutput.isBaked && l.bakingOutput.lightmapBakeType == LightmapBakeType.Baked) continue;
                    lights.Add(l);
                }
            }
            tick -= Time.unscaledDeltaTime;
            if (tick > 0f) return;
            tick = 0.2f;
            Vector3 eye = cam.transform.position, fwd = cam.transform.forward;
            scored.Clear();
            foreach (Light l in lights)
            {
                if (l == null) continue;
                if (!l.isActiveAndEnabled || l.intensity < 0.05f) { if (l.shadows != LightShadows.None) l.shadows = LightShadows.None; continue; }
                Vector3 d = l.transform.position - eye;
                float dist = d.magnitude;
                if (dist > maxDistance + l.range) { if (l.shadows != LightShadows.None) l.shadows = LightShadows.None; continue; }
                float facing = dist < l.range ? 1f : Mathf.Clamp01(0.3f + 0.7f * Vector3.Dot(fwd, d / Mathf.Max(dist, 0.01f)));
                scored.Add((l.intensity * l.range / (1f + dist * dist * 0.05f) * facing, l));
            }
            scored.Sort((a, b) => b.score.CompareTo(a.score));
            int points = 0, spots = 0;
            foreach ((float score, Light l) in scored)
            {
                bool want = l.type == LightType.Point ? points < pointLights : spots < spotLights;
                if (want) { if (l.type == LightType.Point) points++; else spots++; }
                LightShadows s = want ? LightShadows.Soft : LightShadows.None;
                if (l.shadows != s)
                {
                    l.shadows = s;
                    if (want) { l.shadowStrength = 0.85f; l.shadowBias = 0.04f; l.shadowNormalBias = 0.3f; l.shadowNearPlane = 0.2f; }
                }
            }
        }
    }
}
