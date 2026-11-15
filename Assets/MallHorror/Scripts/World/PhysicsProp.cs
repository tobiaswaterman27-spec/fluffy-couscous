using UnityEngine;
using MallHorror.Audio;

namespace MallHorror.World
{
    /// <summary>
    /// A movable physical object. Sets sensible physics defaults (interpolation, anti-tunnelling)
    /// and turns real collisions into audible impact sounds AND creature-audible noise events.
    /// Dropping metal is loud; a cardboard box sliding gently is not.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicsProp : MonoBehaviour
    {
        public SurfaceType material = SurfaceType.Wood;
        [Tooltip("Multiplier on how far creatures can hear this object's impacts.")]
        public float noiseScale = 1f;
        [Tooltip("Collision impulse (N*s) below which no sound is made.")]
        public float minImpulseForSound = 1.2f;

        Rigidbody body;
        float lastSoundTime = -10f;

        public Rigidbody Body => body;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // Small light objects can move fast enough to pass through thin walls; use full continuous detection for them.
            body.collisionDetectionMode = body.mass < 4f
                ? CollisionDetectionMode.ContinuousDynamic
                : CollisionDetectionMode.ContinuousSpeculative;
        }

        void OnCollisionEnter(Collision collision)
        {
            float impulse = collision.impulse.magnitude;
            if (impulse < minImpulseForSound) return;
            if (Time.time - lastSoundTime < 0.08f) return;
            lastSoundTime = Time.time;

            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            SurfaceType other = SurfaceLookup.Get(collision.collider);
            float loudness = Mathf.Max(SurfaceAcoustics.NoiseMultiplier(material), SurfaceAcoustics.NoiseMultiplier(other));
            float radius = Mathf.Clamp(impulse * 0.9f * noiseScale * loudness, 1f, 40f);

            NoiseBus.Emit(new NoiseEvent(point, radius, NoiseKind.Impact, gameObject));
            AudioPool.PlayAt(ProceduralSounds.Impact(material), point, Mathf.Clamp01(impulse / 15f), Random.Range(0.9f, 1.1f));
        }
    }
}
