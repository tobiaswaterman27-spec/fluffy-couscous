using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours.Puzzles
{
    /// <summary>
    /// A step with more than one way to do it: the chain can be cut OR the padlock opened with its key; the fire door's
    /// magnetic lock can be waited out (alarm) OR its power cable cut. Satisfied when any of its options is.
    /// </summary>
    public class AnyOfInput : MonoBehaviour, IPuzzleInput
    {
        public List<MonoBehaviour> options = new List<MonoBehaviour>();

        public event Action Changed;

        public bool Satisfied
        {
            get
            {
                foreach (MonoBehaviour m in options) if (m is IPuzzleInput i && i.Satisfied) return true;
                return false;
            }
        }

        /// <summary>Where it was done (the satisfied option), for anything that wants a place.</summary>
        public Vector3 Where
        {
            get
            {
                foreach (MonoBehaviour m in options) if (m is IPuzzleInput i && i.Satisfied) return m.transform.position;
                return transform.position;
            }
        }

        public GameObject LastActor
        {
            get
            {
                foreach (MonoBehaviour m in options) if (m is IPuzzleInput i && i.Satisfied) return i.LastActor;
                return null;
            }
        }

        void Awake()
        {
            // Subscribe here, before any gate's OnEnable listens to us.
            foreach (MonoBehaviour m in options) if (m is IPuzzleInput i) i.Changed += Forward;
        }

        void OnDestroy()
        {
            foreach (MonoBehaviour m in options) if (m is IPuzzleInput i) i.Changed -= Forward;
        }

        void Forward() => Changed?.Invoke();
    }
}
