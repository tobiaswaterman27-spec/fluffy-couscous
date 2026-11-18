using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours.Puzzles
{
    /// <summary>Something a puzzle checks: a fitted part, a swiped card, a cut chain, a switch...</summary>
    public interface IPuzzleInput
    {
        bool Satisfied { get; }
        /// <summary>Who last changed it (co-op contribution tracking, Part 17).</summary>
        GameObject LastActor { get; }
        event Action Changed;
    }

    /// <summary>Extra state that saves and co-op carry as one line of text (generators, alarms...).</summary>
    public interface IStateful
    {
        string CaptureState();
        void RestoreState(string data);
    }

    /// <summary>Anything whose state must go back to the start when a run resets (death, Part 11/13).</summary>
    public interface IResettable
    {
        void ResetState();
    }

    /// <summary>
    /// Waits until all its inputs are satisfied, then runs its outputs once (unlock a door, raise a shutter,
    /// start a machine, open the way out). Physical feedback only: clunks, motors, humming - no pop-up messages.
    /// </summary>
    public class PuzzleGate : MonoBehaviour, IResettable
    {
        public string routeName = "";
        public List<MonoBehaviour> inputs = new List<MonoBehaviour>();
        public List<PuzzleOutput> outputs = new List<PuzzleOutput>();

        public bool Done { get; private set; }

        void OnEnable()
        {
            foreach (MonoBehaviour m in inputs) if (m is IPuzzleInput i) i.Changed += Evaluate;
        }

        void OnDisable()
        {
            foreach (MonoBehaviour m in inputs) if (m is IPuzzleInput i) i.Changed -= Evaluate;
        }

        void Evaluate()
        {
            if (Done) return;
            GameObject actor = null;
            foreach (MonoBehaviour m in inputs)
            {
                if (!(m is IPuzzleInput i) || !i.Satisfied) return;
                if (i.LastActor != null) actor = i.LastActor;
            }
            Done = true;
            foreach (PuzzleOutput o in outputs) if (o != null) o.Activate(actor);
        }

        public void ResetState()
        {
            Done = false;
            foreach (PuzzleOutput o in outputs) if (o != null) o.ResetOutput();
        }
    }

    /// <summary>What a gate does when solved.</summary>
    public abstract class PuzzleOutput : MonoBehaviour
    {
        public abstract void Activate(GameObject actor);
        public virtual void ResetOutput() { }
    }
}
