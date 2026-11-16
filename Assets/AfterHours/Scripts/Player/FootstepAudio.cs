using UnityEngine;
using AfterHours.Audio;
using AfterHours.World;

namespace AfterHours.Player
{
    /// <summary>Plays the local player's own footsteps, crawling scuffs and landings.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class FootstepAudio : MonoBehaviour
    {
        public PlayerMotor motor;
        AudioSource source;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // your own feet: not positioned
        }

        void Start()
        {
            // Someone else's feet (co-op): heard from where they are, through walls.
            PlayerController pc = GetComponentInParent<PlayerController>();
            if (pc != null && !pc.isLocallyControlled)
            {
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 1.5f;
                source.maxDistance = 30f;
                if (GetComponent<AudioOcclusion>() == null) gameObject.AddComponent<AudioOcclusion>();
            }
        }

        void OnEnable()
        {
            if (motor != null) motor.Footstep += OnFootstep;
        }

        void OnDisable()
        {
            if (motor != null) motor.Footstep -= OnFootstep;
        }

        void OnFootstep(FootstepInfo info)
        {
            AudioClip clip = ProceduralSounds.Footstep(info.surface, info.mode);
            source.pitch = Random.Range(0.95f, 1.05f);
            float volume = Mathf.Clamp01(info.intensity * SurfaceAcoustics.FootstepVolume(info.surface));
            source.PlayOneShot(clip, volume);
        }
    }
}
