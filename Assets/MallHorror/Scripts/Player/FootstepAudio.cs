using UnityEngine;
using MallHorror.Audio;
using MallHorror.World;

namespace MallHorror.Player
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
            bool crawl = info.mode == MoveMode.Crawl;
            AudioClip clip = ProceduralSounds.Footstep(info.surface, crawl);
            source.pitch = Random.Range(0.92f, 1.08f) * (info.mode == MoveMode.Sprint ? 1.05f : 1f);
            float volume = Mathf.Clamp01(info.intensity * SurfaceAcoustics.FootstepVolume(info.surface));
            source.PlayOneShot(clip, volume);
        }
    }
}
