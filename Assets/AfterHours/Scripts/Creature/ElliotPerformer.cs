using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AfterHours.Creature
{
    /// <summary>What his voice is doing to his body right now (all roughly -1..1; ElliotAnimator applies the scales).</summary>
    public struct VoicePose
    {
        public float jaw, lip, nose, brow, throat, chest, tongue;
        public float headPitch, headYaw, headRoll, push, arch, shoulders, arms, curl, splay, shake;
    }

    /// <summary>
    /// Every take of every Elliot sound has its own movement track, made offline from the audio itself
    /// (tools/elliotgen/perf.py): the jaw follows the loudness, the head throws back on a shriek, the nostrils flare on
    /// each sniff, the shoulders shake with the 'laugh', the claws curl on a roar... This plays the track that belongs
    /// to the clip that just started, in step with it, and adds the breathing loop underneath.
    /// </summary>
    public class ElliotPerformer
    {
        class Track { public int frames; public short[] data; }
        static Dictionary<string, Track> tracks;
        static int fps = 30, channels = 17;

        Track current;
        float start, rate = 1f, length;
        Track breathTrack;
        string breathName;

        static void Load()
        {
            if (tracks != null) return;
            tracks = new Dictionary<string, Track>();
            TextAsset t = Resources.Load<TextAsset>("Creature/elliot_perf");
            if (t == null) return;
            using (var r = new BinaryReader(new MemoryStream(t.bytes)))
            {
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "EPF1") return;
                fps = r.ReadInt32();
                channels = r.ReadInt32();
                int n = r.ReadInt32();
                for (int c = 0; c < channels; c++) r.ReadBytes(r.ReadInt32());
                for (int e = 0; e < n; e++)
                {
                    string name = Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
                    int frames = r.ReadInt32();
                    var data = new short[frames * channels];
                    for (int i = 0; i < data.Length; i++) data[i] = r.ReadInt16();
                    tracks[name] = new Track { frames = frames, data = data };
                }
            }
        }

        /// <summary>A voice clip has just started on the creature.</summary>
        public void Play(AudioClip clip, float pitch)
        {
            Load();
            if (clip == null || !tracks.TryGetValue(clip.name, out Track tr)) return;
            current = tr;
            start = Time.time;
            rate = Mathf.Max(0.1f, pitch);
            length = tr.frames / (float)fps / rate;
        }

        public void Stop() => current = null;
        public bool Playing => current != null && Time.time - start < length;

        static void Add(ref VoicePose p, Track tr, float t, float weight)
        {
            float f = t * fps;
            if (tr == null || f < 0f || f >= tr.frames - 1) return;
            int i = (int)f;
            float a = f - i;
            int b0 = i * channels, b1 = (i + 1) * channels;
            float V(int c) => (tr.data[b0 + c] * (1f - a) + tr.data[b1 + c] * a) / 32000f * weight;
            p.jaw += V(0); p.lip += V(1); p.nose += V(2); p.brow += V(3); p.throat += V(4); p.chest += V(5); p.tongue += V(6);
            p.headPitch += V(7); p.headYaw += V(8); p.headRoll += V(9); p.push += V(10); p.arch += V(11);
            p.shoulders += V(12); p.arms += V(13); p.curl += V(14); p.splay += V(15); p.shake += V(16);
        }

        /// <summary>The pose for this frame. breathing: the looping breath source (its time keeps the chest in step).</summary>
        public VoicePose Sample(AudioSource breathing)
        {
            Load();
            var p = new VoicePose();
            if (current != null)
            {
                float t = (Time.time - start) * rate;
                if (t * fps >= current.frames - 1) current = null;
                else Add(ref p, current, t, 1f);
            }
            if (breathing != null && breathing.isPlaying && breathing.clip != null)
            {
                if (breathName != breathing.clip.name)
                {
                    breathName = breathing.clip.name;
                    tracks.TryGetValue(breathName, out breathTrack);
                }
                // breathing sits under the voice and gives way to it
                Add(ref p, breathTrack, breathing.time, current != null ? 0.35f : 1f);
            }
            return p;
        }
    }
}
