using System.Globalization;
using UnityEngine;
using AfterHours.Puzzles;

namespace AfterHours.World
{
    /// <summary>
    /// Something free-standing that goes over when you (or Elliot) blunder into it: wet-floor signs, bins, A-boards,
    /// stacked boxes, café chairs. Top-heavy on purpose, so a bump tips it rather than sliding it; the crash is a
    /// noise (PhysicsProp) that carries - running through a dark concourse is how you give yourself away.
    /// Goes back where it was when a run starts over; saved; and co-op players all see it where the host has it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Knockable : MonoBehaviour, IResettable, IStateful
    {
        [Tooltip("Centre of mass height as a fraction of the object's height (higher = tips over more easily).")]
        public float topHeavy = 0.7f;
        public float height = 1f;

        Rigidbody body;
        Vector3 homePos, netPos;
        Quaternion homeRot, netRot;
        bool hasNet;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            homePos = transform.position;
            homeRot = transform.rotation;
            body.centerOfMass = new Vector3(0f, height * topHeavy, 0f);
            body.sleepThreshold = 0.05f;
        }

        void Start() { if (body != null) body.Sleep(); }   // standing still until something hits it

        bool Moved => (transform.position - homePos).sqrMagnitude > 0.0004f || Quaternion.Angle(transform.rotation, homeRot) > 1f;

        public void ResetState()
        {
            if (body == null) return;
            body.isKinematic = AfterHours.Items.ItemPickup.NetDriven;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = homePos;
            body.rotation = homeRot;
            transform.SetPositionAndRotation(homePos, homeRot);
            hasNet = false;
            if (!body.isKinematic) body.Sleep();
        }

        public string CaptureState()
        {
            if (!Moved) return "h";
            Vector3 p = transform.position; Quaternion q = transform.rotation;
            return string.Join(" ", new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w }.Select4(Inv));
        }

        public void RestoreState(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            Vector3 p = homePos; Quaternion q = homeRot;
            if (data != "h")
            {
                string[] a = data.Split(' ');
                if (a.Length < 7) return;
                float F(int i) => float.Parse(a[i], Inv);
                p = new Vector3(F(0), F(1), F(2)); q = new Quaternion(F(3), F(4), F(5), F(6));
            }
            if (AfterHours.Items.ItemPickup.NetDriven)
            {
                // co-op client: the host simulates it; glide to where it is there
                body.isKinematic = true;
                netPos = p; netRot = q;
                if (!hasNet) transform.SetPositionAndRotation(p, q);
                hasNet = true;
                return;
            }
            body.position = p; body.rotation = q;
            transform.SetPositionAndRotation(p, q);
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        }

        void Update()
        {
            if (!hasNet) return;
            float k = 1f - Mathf.Exp(-12f * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, netPos, k), Quaternion.Slerp(transform.rotation, netRot, k));
        }
    }

    static class KnockableFormat
    {
        public static string[] Select4(this float[] v, CultureInfo inv)
        {
            var s = new string[v.Length];
            for (int i = 0; i < v.Length; i++) s[i] = v[i].ToString("0.###", inv);
            return s;
        }
    }
}
