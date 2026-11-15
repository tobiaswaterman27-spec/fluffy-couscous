using UnityEngine;

namespace AfterHours.Player
{
    /// <summary>
    /// Temporary debug body: draws the exact collision shape (torso capsule + crawling legs) so you can
    /// see it with F5. Replaced by the real animated character model in Part 8/15.
    /// </summary>
    public class PlayerBodyVisual : MonoBehaviour
    {
        public PlayerMotor motor;
        public Transform torso;
        public Transform thigh;
        public Transform shin;

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

            bool on = motor.LegsActive;
            Vector3 hip = motor.transform.position + motor.LegsDirection * (r * 0.8f);
            Place(thigh, on, hip, motor.KneePosition);
            Place(shin, on, motor.KneePosition, motor.FeetPosition);
        }

        void Place(Transform part, bool on, Vector3 a, Vector3 b)
        {
            if (part == null) return;
            if (part.gameObject.activeSelf != on) part.gameObject.SetActive(on);
            if (!on) return;
            float lr = motor.LegRadius;
            Vector3 up = Vector3.up * (lr + 0.05f);
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 1e-6f) dir = -motor.transform.forward * 0.01f;
            part.position = (a + b) * 0.5f + up;
            part.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            float length = dir.magnitude + lr * 2f;
            part.localScale = new Vector3(lr * 2f, length * 0.5f, lr * 2f);
        }
    }
}
