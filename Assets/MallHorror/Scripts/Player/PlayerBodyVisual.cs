using UnityEngine;

namespace MallHorror.Player
{
    /// <summary>
    /// Temporary debug body: draws the exact collision shape (torso capsule + crawling legs) so you can
    /// see it with F5. Replaced by the real animated character model in Part 8/15.
    /// </summary>
    public class PlayerBodyVisual : MonoBehaviour
    {
        public PlayerMotor motor;
        public Transform torso;
        public Transform legs;

        void LateUpdate()
        {
            if (motor == null) return;
            float h = motor.CurrentHeight;
            float r = motor.CurrentRadius;

            if (torso != null)
            {
                torso.localPosition = new Vector3(0f, h * 0.5f, 0f);
                torso.localRotation = Quaternion.identity;
                torso.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);
            }

            if (legs != null)
            {
                bool on = motor.LegsActive;
                if (legs.gameObject.activeSelf != on) legs.gameObject.SetActive(on);
                if (on)
                {
                    Vector3 hip = motor.transform.position;
                    Vector3 dir = motor.LegsDirection;
                    float lr = motor.LegRadius;
                    Vector3 a = hip + dir * (r * 0.8f);
                    Vector3 b = hip + dir * motor.LegLength;
                    legs.position = (a + b) * 0.5f + Vector3.up * (lr + 0.05f);
                    legs.rotation = Quaternion.FromToRotation(Vector3.up, dir);
                    float length = Vector3.Distance(a, b) + lr * 2f;
                    legs.localScale = new Vector3(lr * 2f, length * 0.5f, lr * 2f);
                }
            }
        }
    }
}
