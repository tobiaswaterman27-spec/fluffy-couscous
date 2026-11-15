using UnityEngine;

namespace AfterHours.World
{
    /// <summary>A moving walkway surface: anyone standing on it is carried along at this velocity.</summary>
    public class ConveyorSurface : MonoBehaviour
    {
        [Tooltip("Belt velocity in this object's local space (m/s).")]
        public Vector3 localVelocity = new Vector3(0f, 0f, 0.8f);
        public bool running = true;

        public Vector3 WorldVelocity => running ? transform.TransformDirection(localVelocity) : Vector3.zero;
    }
}
