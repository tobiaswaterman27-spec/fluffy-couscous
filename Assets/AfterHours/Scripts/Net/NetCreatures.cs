using System.Collections.Generic;
using UnityEngine;
using AfterHours.Creature;
using AfterHours.World.Layout;

namespace AfterHours.Net
{
    /// <summary>
    /// The creatures in co-op: 1 for one player, 2 for two or three, 3 for four. Each has its own mind and memory
    /// (it remembers which of you it saw, and where). The host runs them; everyone else sees them move, look,
    /// reach and scream exactly as on the host.
    /// </summary>
    public class NetCreatures
    {
        public readonly List<ElliotBrain> All = new List<ElliotBrain>();

        public static int CountFor(int players) => players <= 1 ? 1 : players <= 3 ? 2 : 3;

        static readonly Vector3[] ExtraStarts =
        {
            new Vector3(-40f, MallLayout.CarParkY + 0.05f, -60f),
            new Vector3(70f, MallLayout.CarParkY + 0.05f, 10f),
        };

        public void Setup(int count, bool host)
        {
            All.Clear();
            ElliotBrain first = Object.FindAnyObjectByType<ElliotBrain>();
            if (first == null) return;
            All.Add(first);
            int extra = Mathf.Clamp(count, 1, 3) - 1;
            for (int i = 0; i < extra; i++)
            {
                GameObject copy = Object.Instantiate(first.gameObject, ExtraStarts[i], Quaternion.Euler(0f, 90f * i, 0f));
                copy.name = "Elliot " + (i + 2);
                All.Add(copy.GetComponent<ElliotBrain>());
            }
            if (!host)
            {
                foreach (ElliotBrain b in All)
                {
                    b.enabled = false;
                    if (b.senses != null) b.senses.enabled = false;
                    var agent = b.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent != null) agent.enabled = false;
                    b.motor.remote = true;
                }
            }
        }

        public void Write(NetWriter w)
        {
            w.Byte((byte)All.Count);
            foreach (ElliotBrain b in All)
            {
                ElliotAnimator a = b.animator;
                w.Vec(b.transform.position);
                w.Angle(b.transform.eulerAngles.y);
                w.Vec(b.motor.Velocity);
                w.Byte((byte)b.motor.Gait);
                w.Float(b.motor.Hunch);
                bool look = a.LookTarget.HasValue;
                w.Bool(look);
                if (look) w.Vec(a.LookTarget.Value);
                w.Byte((byte)a.CurrentAction);
                if (a.CurrentAction != ElliotAction.None) { w.Vec(a.ActionTarget); w.Float(a.ActionRemaining); }
                w.Bool(b.gameObject.activeSelf);
            }
        }

        public void Read(NetReader r)
        {
            int n = r.Byte();
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = r.Vec();
                float yaw = r.Angle();
                Vector3 vel = r.Vec();
                var gait = (ElliotGait)r.Byte();
                float hunch = r.Float();
                bool hasLook = r.Bool();
                Vector3 look = hasLook ? r.Vec() : Vector3.zero;
                var action = (ElliotAction)r.Byte();
                Vector3 target = Vector3.zero;
                float remaining = 0f;
                if (action != ElliotAction.None) { target = r.Vec(); remaining = r.Float(); }
                bool active = r.Bool();
                if (i >= All.Count || All[i] == null) continue;
                ElliotBrain b = All[i];
                if (b.gameObject.activeSelf != active) b.gameObject.SetActive(active);
                if (CatchSequence.IsCreatureBusy(b)) continue; // the catch sequence is posing it
                b.motor.ApplyRemote(pos, yaw, vel, gait, hunch);
                ElliotAnimator a = b.animator;
                a.LookTarget = hasLook ? look : (Vector3?)null;
                if (action != a.CurrentAction || (action != ElliotAction.None && (a.ActionTarget - target).sqrMagnitude > 0.25f))
                {
                    if (action == ElliotAction.None) a.StopAction();
                    else a.PlayAction(action, target, Mathf.Max(0.2f, remaining));
                }
            }
        }

        public int IndexOf(ElliotBrain b) => All.IndexOf(b);
    }
}
