using UnityEngine;
using AfterHours.Core;
using AfterHours.Settings;

namespace AfterHours.Player
{
    /// <summary>
    /// First-person camera: mouse look, head turning beyond the body while crawling,
    /// head bob locked to real footsteps, landing dip, sprint FOV and crawl sway.
    /// Hierarchy: Player (yaw) > CameraRig (eye height, pitch + head yaw) > PlayerCamera (bob offset).
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        public Transform cameraRig;
        public Camera playerCamera;

        [Header("Look limits")]
        public float minPitch = -85f;
        public float maxPitch = 85f;
        public float crawlMinPitch = -40f;
        public float crawlMaxPitch = 65f;
        [Tooltip("How far the head can turn relative to the body (only matters when crawling).")]
        public float maxHeadYaw = 75f;

        [Header("Head bob (metres)")]
        public float walkBob = 0.035f;
        public float sprintBob = 0.065f;
        public float crouchBob = 0.022f;
        public float crawlBob = 0.014f;
        public float crawlRoll = 0.9f;
        public float sprintFovBoost = 6f;

        [Header("Debug third-person view (F5)")]
        public Vector3 debugCameraOffset = new Vector3(0f, 0.4f, -2.4f);

        float pitch;
        float lookYaw;
        float headYaw;
        float bobAmp;
        float rollAmp;
        float fovBoost;
        float landOffset;
        float landVelocity;
        bool thirdPerson;
        int firstPersonMask = ~0;

        /// <summary>While true, something else (the catch sequence) positions and aims the camera.</summary>
        public bool ExternallyDriven { get; set; }

        public float Pitch => pitch;
        public float HeadYaw => headYaw;
        public bool ThirdPersonDebug => thirdPerson;

        public void Initialize(float yaw)
        {
            lookYaw = yaw;
            pitch = 0f;
            headYaw = 0f;
            if (playerCamera != null) firstPersonMask = playerCamera.cullingMask;
        }

        public void ApplyLook(Vector2 delta, PlayerMotor motor, float dt)
        {
            lookYaw += delta.x;

            float crawl = motor.CrawlBlend;
            float lo = Mathf.Lerp(minPitch, crawlMinPitch, crawl);
            float hi = Mathf.Lerp(maxPitch, crawlMaxPitch, crawl);
            pitch = Mathf.Clamp(pitch - delta.y, lo, hi);

            float body = motor.TrySetBodyYaw(lookYaw, dt);
            headYaw = Mathf.DeltaAngle(body, lookYaw);
            if (Mathf.Abs(headYaw) > maxHeadYaw) headYaw = Mathf.Sign(headYaw) * maxHeadYaw;
            lookYaw = body + headYaw;
        }

        /// <summary>Looking around while the body can't turn (hiding): yaw limited around a centre direction.</summary>
        public void ApplyLimitedLook(Vector2 delta, float centreYaw, float maxYaw)
        {
            lookYaw += delta.x;
            float rel = Mathf.Clamp(Mathf.DeltaAngle(centreYaw, lookYaw), -maxYaw, maxYaw);
            lookYaw = centreYaw + rel;
            pitch = Mathf.Clamp(pitch - delta.y, -60f, 60f);
            headYaw = rel;
            transform.rotation = Quaternion.Euler(0f, centreYaw, 0f);
        }

        /// <summary>Points the view at a world position (used while being caught).</summary>
        public void ForceLookAt(Vector3 worldPoint, float sharpness, float dt)
        {
            if (cameraRig == null) return;
            Vector3 d = worldPoint - cameraRig.position;
            if (d.sqrMagnitude < 1e-4f) return;
            float wantYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float wantPitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            float k = 1f - Mathf.Exp(-sharpness * dt);
            lookYaw = Mathf.LerpAngle(lookYaw, wantYaw, k);
            pitch = Mathf.Lerp(pitch, wantPitch, k);
            headYaw = 0f;
            transform.rotation = Quaternion.Euler(0f, lookYaw, 0f);
        }

        public void OnLanded(float fallSpeed)
        {
            landVelocity -= Mathf.Clamp(fallSpeed * 0.05f, 0f, 0.6f);
        }

        public void ToggleThirdPersonDebug()
        {
            thirdPerson = !thirdPerson;
            if (playerCamera == null) return;
            int bodyLayer = GameLayers.LocalBodyLayer;
            playerCamera.cullingMask = thirdPerson && bodyLayer >= 0 ? firstPersonMask | (1 << bodyLayer) : firstPersonMask;
        }

        public void UpdateCamera(PlayerMotor motor, float dt)
        {
            if (cameraRig == null || playerCamera == null || ExternallyDriven) return;
            GameSettings s = SettingsService.CurrentOrDefault;
            LocomotionState loco = motor.Locomotion;

            float targetAmp = 0f;
            float targetRoll = 0f;
            switch (loco.mode)
            {
                case MoveMode.Walk: targetAmp = walkBob * Mathf.Clamp01(loco.planarSpeed / Mathf.Max(0.1f, motor.walkSpeed)); break;
                case MoveMode.Sprint: targetAmp = sprintBob; break;
                case MoveMode.CrouchWalk: targetAmp = crouchBob; break;
                case MoveMode.Crawl: targetAmp = crawlBob; targetRoll = crawlRoll; break;
            }
            if (!s.headBob)
            {
                targetAmp *= 0.2f;
                targetRoll *= 0.2f;
            }

            float k = 1f - Mathf.Exp(-8f * dt);
            bobAmp = Mathf.Lerp(bobAmp, targetAmp, k);
            rollAmp = Mathf.Lerp(rollAmp, targetRoll, k);
            fovBoost = Mathf.Lerp(fovBoost, loco.sprinting ? sprintFovBoost : 0f, 1f - Mathf.Exp(-4f * dt));

            // One dip per footstep; sideways sway alternates feet.
            float phase = loco.stridePhase * Mathf.PI;
            float vertical = -bobAmp * Mathf.Abs(Mathf.Sin(phase));
            float lateral = bobAmp * 0.55f * Mathf.Sin(phase);
            float roll = rollAmp * Mathf.Sin(phase);

            // Damped spring for landing impacts.
            landVelocity += (-landOffset * 120f - landVelocity * 18f) * dt;
            landOffset += landVelocity * dt;

            cameraRig.localPosition = new Vector3(0f, motor.EyeHeight, 0f);
            cameraRig.localRotation = Quaternion.Euler(pitch, headYaw, 0f);

            playerCamera.transform.localPosition = thirdPerson
                ? debugCameraOffset
                : new Vector3(lateral, vertical + landOffset, 0f);
            playerCamera.transform.localRotation = Quaternion.Euler(0f, 0f, thirdPerson ? 0f : roll);
            playerCamera.fieldOfView = s.fieldOfView + fovBoost;
        }
    }
}
