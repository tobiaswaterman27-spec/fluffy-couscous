using System;
using UnityEngine;

namespace AfterHours.Audio
{
    public enum NoiseKind
    {
        Footstep,
        Landing,
        Impact,
        Door,
        Interaction,
        Voice,
        Distraction,
        Machinery,
        Creature
    }

    /// <summary>
    /// A gameplay sound "event" that creatures can hear. This is separate from the audio you hear:
    /// it describes where a noise happened and how far an alert listener could hear it in a quiet open space.
    /// Part 10 adds wall/door occlusion, echoes and background masking on top of this.
    /// </summary>
    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly NoiseKind Kind;
        public readonly GameObject Source;
        public readonly float Timestamp;

        public NoiseEvent(Vector3 position, float radius, NoiseKind kind, GameObject source)
        {
            Position = position;
            Radius = radius;
            Kind = kind;
            Source = source;
            Timestamp = Time.time;
        }
    }

    public static class NoiseBus
    {
        const int HistorySize = 64;
        static readonly NoiseEvent[] history = new NoiseEvent[HistorySize];
        static int head;
        static int count;

        public static event Action<NoiseEvent> Emitted;

        /// <summary>LAN co-op client: noises are also sent to the host, where the creatures live.</summary>
        public static Action<NoiseEvent> Forward;

        public static bool HasLast => count > 0;
        public static NoiseEvent Last => history[(head - 1 + HistorySize) % HistorySize];
        public static int RecentCount => count;

        /// <summary>0 = newest.</summary>
        public static NoiseEvent GetRecent(int index)
        {
            return history[(head - 1 - index + HistorySize * 2) % HistorySize];
        }

        public static void Emit(NoiseEvent e)
        {
            history[head] = e;
            head = (head + 1) % HistorySize;
            if (count < HistorySize) count++;
            Emitted?.Invoke(e);
            if (Forward != null && e.Kind != NoiseKind.Creature) Forward(e);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Emitted = null;
            Forward = null;
            head = 0;
            count = 0;
        }
    }
}
