using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AfterHours.Audio;
using AfterHours.Rendering;

namespace AfterHours.Power
{
    /// <summary>
    /// The lights of one circuit's area. The building's lighting is baked, so a power cut is shown the way your
    /// eyes see it: the area drops into near-darkness (a local exposure volume you walk into), the hum of the
    /// fittings stops, flickering tubes die, and the few battery emergency lights come on.
    /// Restoring power: a pause, the tubes strike one after another, then light floods back.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class PowerZone : MonoBehaviour
    {
        public string circuit = "north";
        [Tooltip("Battery emergency lights in this area (off while the mains is on).")]
        public Light[] emergencyLights;

        Volume volume;
        float target, weight;
        float transitionT = -1f;
        bool powered = true;
        System.Random rng;
        readonly List<AudioSource> hums = new List<AudioSource>();
        readonly List<FluorescentFlicker> flickers = new List<FluorescentFlicker>();
        readonly List<Light> flickerLights = new List<Light>();

        void Awake()
        {
            volume = GetComponent<Volume>();
            volume.isGlobal = false;
            volume.blendDistance = 4f;
            volume.priority = 50f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            ColorAdjustments ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(-3.6f);
            ca.saturation.Override(-45f);
            ca.contrast.Override(18f);
            volume.sharedProfile = profile;
            volume.weight = 0f;
            rng = new System.Random(circuit.GetHashCode());
            foreach (Collider c in GetComponents<Collider>()) c.isTrigger = true;
        }

        void Start()
        {
            // Hum of the fittings and flickering tubes inside this zone.
            foreach (AmbientSound a in AmbientSound.All)
            {
                if (a.kind != ProceduralSounds.Ambience.FluorescentHum && a.kind != ProceduralSounds.Ambience.Buzz) continue;
                if (Contains(a.transform.position)) hums.Add(a.GetComponent<AudioSource>());
            }
            foreach (FluorescentFlicker f in FindObjectsByType<FluorescentFlicker>(FindObjectsSortMode.None))
            {
                if (!Contains(f.transform.position)) continue;
                flickers.Add(f);
                if (f.lightSource != null) flickerLights.Add(f.lightSource);
            }
            SetEmergency(false);
            powered = PowerGrid.IsPowered(circuit);
            target = weight = powered ? 0f : 1f;
            Apply();
        }

        void OnEnable() => PowerGrid.Changed += OnChanged;
        void OnDisable() => PowerGrid.Changed -= OnChanged;

        public bool Contains(Vector3 p)
        {
            foreach (Collider c in GetComponents<Collider>())
                if (c.bounds.Contains(p) && (c.ClosestPoint(p) - p).sqrMagnitude < 1e-4f) return true;
            return false;
        }

        void OnChanged(string id)
        {
            bool now = PowerGrid.IsPowered(circuit);
            if (now == powered) return;
            powered = now;
            transitionT = 0f;
            Vector3 ear = Camera.main != null ? Camera.main.transform.position : transform.position;
            if (Contains(ear))
            {
                if (now)
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 p = ear + new Vector3(Random.Range(-8f, 8f), 2.5f, Random.Range(-8f, 8f));
                        AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.TubeStart), p, 0.5f, Random.Range(0.9f, 1.1f), 25f);
                    }
                else AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.BreakerOff), ear + Vector3.up * 2f, 0.6f, 0.6f, 30f);
            }
        }

        void Update()
        {
            if (transitionT >= 0f)
            {
                transitionT += Time.deltaTime;
                if (!powered)
                {
                    // Dying: a second of stutter, then dark.
                    weight = transitionT < 1.1f ? (rng.NextDouble() < 0.55 ? 1f : 0.2f) : 1f;
                    if (transitionT >= 1.1f) { transitionT = -1f; SetEmergency(true); }
                }
                else
                {
                    // Coming back: a dark pause, the tubes struggle to strike, then light.
                    SetEmergency(false);
                    if (transitionT < 0.7f) weight = 1f;
                    else if (transitionT < 2.0f) weight = rng.NextDouble() < 0.3 ? 0.15f : 1f;
                    else weight = Mathf.MoveTowards(weight, 0f, Time.deltaTime * 2f);
                    if (transitionT > 2.6f) { transitionT = -1f; weight = 0f; }
                }
                Apply();
            }
        }

        void Apply()
        {
            if (volume != null) volume.weight = weight;
            bool dark = weight > 0.5f;
            foreach (AudioSource s in hums) if (s != null) s.mute = dark;
            foreach (FluorescentFlicker f in flickers) if (f != null) f.enabled = !dark;
            if (dark) foreach (Light l in flickerLights) if (l != null) l.intensity = 0f;
        }

        void SetEmergency(bool on)
        {
            if (emergencyLights == null) return;
            foreach (Light l in emergencyLights) if (l != null) l.enabled = on;
        }
    }
}
