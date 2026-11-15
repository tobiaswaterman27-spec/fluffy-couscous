using System.Collections.Generic;
using UnityEngine;

namespace MallHorror.Player
{
    /// <summary>
    /// Feeds the movement state into an Animator. The parameters are already defined so that the
    /// character model and animation clips added in Part 8/15 (walk, run, crouch-walk and crawl,
    /// each forwards, backwards and sideways, plus turning and stance transitions) plug straight in.
    ///
    /// Animator parameters (all optional; missing ones are skipped):
    ///   MoveX (float)  strafe speed m/s, right positive
    ///   MoveZ (float)  forward speed m/s, NEGATIVE = backwards (drives backward walk/run/crawl clips)
    ///   Speed (float)  planar speed m/s
    ///   Stance (float) 0 stand, 1 crouch, 2 crawl (blended smoothly)
    ///   HipBend (float) crawling torso twist, degrees
    ///   TurnRate (float) degrees per second (turn-in-place clips)
    ///   StridePhase (float) 0..1 within the current step (foot sync)
    ///   Grounded (bool), Sprinting (bool), Backward (bool)
    /// </summary>
    public class PlayerAnimationDriver : MonoBehaviour
    {
        public PlayerMotor motor;
        public Animator animator;
        public float damping = 0.1f;

        static readonly int MoveX = Animator.StringToHash("MoveX");
        static readonly int MoveZ = Animator.StringToHash("MoveZ");
        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int StanceParam = Animator.StringToHash("Stance");
        static readonly int HipBend = Animator.StringToHash("HipBend");
        static readonly int TurnRate = Animator.StringToHash("TurnRate");
        static readonly int StridePhase = Animator.StringToHash("StridePhase");
        static readonly int Grounded = Animator.StringToHash("Grounded");
        static readonly int Sprinting = Animator.StringToHash("Sprinting");
        static readonly int Backward = Animator.StringToHash("Backward");

        readonly HashSet<int> available = new HashSet<int>();
        Animator cachedFor;

        void LateUpdate()
        {
            if (motor == null || animator == null || animator.runtimeAnimatorController == null) return;
            if (cachedFor != animator) CacheParameters();

            LocomotionState l = motor.Locomotion;
            float dt = Time.deltaTime;
            float stanceValue = l.height >= motor.crouchHeight
                ? Mathf.InverseLerp(motor.standHeight, motor.crouchHeight, l.height)
                : 1f + Mathf.InverseLerp(motor.crouchHeight, motor.crawlHeight, l.height);

            SetFloat(MoveX, l.localPlanarVelocity.x, dt);
            SetFloat(MoveZ, l.localPlanarVelocity.y, dt);
            SetFloat(Speed, l.planarSpeed, dt);
            SetFloat(StanceParam, stanceValue, dt);
            SetFloat(HipBend, l.hipBendDegrees, dt);
            SetFloat(TurnRate, l.yawRate, dt);
            if (available.Contains(StridePhase)) animator.SetFloat(StridePhase, l.stridePhase - Mathf.Floor(l.stridePhase));
            SetBool(Grounded, l.grounded);
            SetBool(Sprinting, l.sprinting);
            SetBool(Backward, l.backward);
        }

        void CacheParameters()
        {
            available.Clear();
            foreach (AnimatorControllerParameter p in animator.parameters) available.Add(p.nameHash);
            cachedFor = animator;
        }

        void SetFloat(int id, float value, float dt)
        {
            if (available.Contains(id)) animator.SetFloat(id, value, damping, dt);
        }

        void SetBool(int id, bool value)
        {
            if (available.Contains(id)) animator.SetBool(id, value);
        }
    }
}
