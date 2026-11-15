using UnityEngine;
using UnityEngine.InputSystem;
using AfterHours.Creature;
using AfterHours.Player;

namespace AfterHours.DevTools
{
    /// <summary>
    /// Part 8 test only (replaced by the real AI in Part 9): Elliot follows you at a distance so you can watch
    /// every movement style. Number keys: 1 still, 2 idle, 3 slow walk, 4 walk, 5 fast walk, 6 run, 7 crawl.
    /// 8 scream, 9 bang (on whatever is ahead), 0 look around, minus = stare at you.
    /// </summary>
    public class ElliotTestDriver : MonoBehaviour
    {
        public ElliotMotor motor;
        public ElliotAnimator animator;
        public float keepDistance = 4f;
        ElliotGait gait = ElliotGait.Walk;
        Transform player;

        void Update()
        {
            if (player == null)
            {
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) player = pc.transform;
                if (player == null) return;
            }
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) gait = ElliotGait.Still;
                if (kb.digit2Key.wasPressedThisFrame) gait = ElliotGait.Idle;
                if (kb.digit3Key.wasPressedThisFrame) gait = ElliotGait.SlowWalk;
                if (kb.digit4Key.wasPressedThisFrame) gait = ElliotGait.Walk;
                if (kb.digit5Key.wasPressedThisFrame) gait = ElliotGait.FastWalk;
                if (kb.digit6Key.wasPressedThisFrame) gait = ElliotGait.Run;
                if (kb.digit7Key.wasPressedThisFrame) gait = ElliotGait.Crawl;
                Vector3 ahead = transform.position + transform.forward * 0.7f + Vector3.up * 1.4f;
                if (kb.digit8Key.wasPressedThisFrame) animator.PlayAction(ElliotAction.Scream, player.position + Vector3.up * 1.6f, 1.8f);
                if (kb.digit9Key.wasPressedThisFrame) animator.PlayAction(ElliotAction.BangDoor, ahead, 2.5f);
                if (kb.digit0Key.wasPressedThisFrame) animator.PlayAction(ElliotAction.LookAround, ahead, 3f);
                if (kb.minusKey.wasPressedThisFrame) animator.PlayAction(ElliotAction.Stare, player.position + Vector3.up * 1.6f, 4f);
            }

            Vector3 to = player.position - transform.position;
            to.y = 0f;
            bool move = to.magnitude > keepDistance && gait != ElliotGait.Still && gait != ElliotGait.Idle;
            motor.Drive(move ? to : Vector3.zero, gait);
            motor.Face(move ? (Vector3?)null : player.position);
            animator.LookTarget = player.position + Vector3.up * 1.6f;
        }
    }
}
