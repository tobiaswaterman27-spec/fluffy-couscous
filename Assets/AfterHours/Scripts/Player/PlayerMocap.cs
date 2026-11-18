using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AfterHours.Player
{
    /// <summary>
    /// Real human motion: motion capture from the CMU Graphics Lab Motion Capture Database (mocap.cs.cmu.edu,
    /// free for any use), converted by tools/mocap/build_mocap.py into Resources/Player/mocap.bytes.
    ///
    /// Every pose is stored proportion-free (directions and rotations in the character's own space, distances in
    /// leg lengths), so it can be rebuilt on anyone's bones. Walking / running / crouch-walking clips are single
    /// gait cycles that loop exactly (phase 0 = left heel strike); idles are long loops.
    /// </summary>
    public sealed class MocapClip
    {
        public string name;
        public bool cycle;
        public float rate;      // cycles per second as captured
        public float speed;     // leg lengths per second (negative: walking backwards)
        public float step;      // step length in leg lengths
        public int frames;
        public float[] data;    // frames * Mocap.CH
        public float[] mean;    // the average pose over the clip
    }

    public static class Mocap
    {
        // channel layout (see build_mocap.py)
        public const int P = 0, PQ = 3, HQ = 7, CQ = 11, FQ = 15, SEG = 23, SH = 107, ANK = 113, CON = 119, WR = 121;
        public const int Spine = 0, NeckS = 1, UpperL = 2, ForeL = 3, HandL = 4, UpperR = 5, ForeR = 6, HandR = 7,
                         ThighL = 8, ShinL = 9, FootL = 10, ThighR = 11, ShinR = 12, FootR = 13;
        public static int CH { get; private set; }

        static Dictionary<string, MocapClip> clips;
        static bool tried;

        public static bool Ready { get { Load(); return clips != null && clips.Count > 0; } }

        public static MocapClip Get(string name)
        {
            Load();
            return clips != null && clips.TryGetValue(name, out MocapClip c) ? c : null;
        }

        static string Str(BinaryReader r) => Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));

        static void Load()
        {
            if (tried) return;
            tried = true;
            TextAsset t = Resources.Load<TextAsset>("Player/mocap");
            if (t == null) { Debug.LogWarning("[AfterHours] Resources/Player/mocap.bytes is missing: players fall back to procedural animation."); return; }
            clips = new Dictionary<string, MocapClip>();
            using (var r = new BinaryReader(new MemoryStream(t.bytes)))
            {
                r.ReadBytes(4);
                int n = r.ReadInt32();
                CH = r.ReadInt32();
                int segs = r.ReadInt32();
                for (int i = 0; i < segs; i++) Str(r);
                for (int i = 0; i < n; i++)
                {
                    var c = new MocapClip { name = Str(r) };
                    c.cycle = r.ReadInt32() == 0;
                    c.rate = r.ReadSingle(); c.speed = r.ReadSingle(); c.step = r.ReadSingle(); r.ReadSingle();
                    c.frames = r.ReadInt32();
                    c.data = new float[c.frames * CH];
                    byte[] raw = r.ReadBytes(c.data.Length * 4);
                    System.Buffer.BlockCopy(raw, 0, c.data, 0, raw.Length);
                    c.mean = new float[CH];
                    for (int f = 0; f < c.frames; f++) for (int k = 0; k < CH; k++) c.mean[k] += c.data[f * CH + k] / c.frames;
                    clips[c.name] = c;
                }
            }
        }

        static bool IsQuat(int c) => c >= PQ && c < SEG;

        /// <summary>The pose at this phase (0..1 wraps), linearly between captured frames.</summary>
        public static void Sample(MocapClip clip, float phase, float[] o)
        {
            int n = clip.frames;
            float f = Mathf.Repeat(phase, 1f) * n;
            int i0 = Mathf.FloorToInt(f) % n, i1 = (i0 + 1) % n;
            float w = f - Mathf.Floor(f);
            int a = i0 * CH, b = i1 * CH;
            float[] d = clip.data;
            for (int c = 0; c < CH; c++) o[c] = d[a + c] + (d[b + c] - d[a + c]) * w;
            for (int q = PQ; q < SEG; q += 4)
            {
                float dot = d[a + q] * d[b + q] + d[a + q + 1] * d[b + q + 1] + d[a + q + 2] * d[b + q + 2] + d[a + q + 3] * d[b + q + 3];
                if (dot < 0f) for (int k = 0; k < 4; k++) o[q + k] = d[a + q + k] - (d[b + q + k] + d[a + q + k]) * w;
            }
        }

        /// <summary>o = a..b by w (o may be a or b).</summary>
        public static void Blend(float[] a, float[] b, float w, float[] o)
        {
            for (int q = PQ; q < SEG; q += 4)
            {
                float dot = a[q] * b[q] + a[q + 1] * b[q + 1] + a[q + 2] * b[q + 2] + a[q + 3] * b[q + 3];
                float sgn = dot < 0f ? -1f : 1f;
                for (int k = 0; k < 4; k++) o[q + k] = a[q + k] + (sgn * b[q + k] - a[q + k]) * w;
            }
            for (int c = 0; c < CH; c++)
            {
                if (IsQuat(c)) continue;
                o[c] = a[c] + (b[c] - a[c]) * w;
            }
        }

        public static Vector3 V(float[] p, int i) => new Vector3(p[i], p[i + 1], p[i + 2]);

        public static Quaternion Q(float[] p, int i)
        {
            var q = new Quaternion(p[i], p[i + 1], p[i + 2], p[i + 3]);
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (m < 1e-6f) return Quaternion.identity;
            return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }

        public static Vector3 Dir(float[] p, int seg) => V(p, SEG + seg * 6).normalized;
        public static Vector3 Twist(float[] p, int seg) => V(p, SEG + seg * 6 + 3);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { clips = null; tried = false; }
    }

    /// <summary>One character's mocap state: which clips, how far through them, the blended pose.</summary>
    public sealed class MocapRunner
    {
        static readonly string[] Loco = { "walk_slow", "walk", "jog", "run" };
        float[] s1, s2, mv, cr, idl, idl2;
        public float[] Pose;
        float idleT = Random.Range(0f, 30f);
        public float CorrY;
        public bool HasCorr;

        void Ensure()
        {
            int ch = Mocap.CH;
            if (Pose != null && Pose.Length == ch) return;
            s1 = new float[ch]; s2 = new float[ch]; mv = new float[ch]; cr = new float[ch]; idl = new float[ch]; idl2 = new float[ch]; Pose = new float[ch];
        }

        public MocapClip CrawlClip;

        /// <summary>On hands and knees: the crawl cycle at this phase, its stride stretched to the real speed.</summary>
        public bool EvaluateCrawl(float speed, float phase, float stepLen, float legLen, bool back = false)
        {
            if (!Mocap.Ready) return false;
            MocapClip c = Mocap.Get("crawl");
            if (c == null) return false;
            Ensure();
            CrawlClip = c;
            Mocap.Sample(c, back ? 1f - Mathf.Repeat(phase, 1f) : phase, mv);   // backing up: the same crawl, in reverse
            float k = Mathf.Clamp(stepLen / Mathf.Max(0.03f, c.step * legLen), 0.45f, 2.2f);
            foreach (int a in new[] { Mocap.ANK, Mocap.ANK + 3, Mocap.WR, Mocap.WR + 3 })
                mv[a + 2] = c.mean[a + 2] + (mv[a + 2] - c.mean[a + 2]) * k;
            Mocap.Sample(c, 0.1f, idl);
            Mocap.Blend(idl, mv, Mathf.Clamp01(speed / 0.15f), Pose);
            return true;
        }

        /// <summary>
        /// Blend the clips for this movement: speed (m/s), backwards or not, crouch 0..1, gait phase (one cycle = two
        /// footsteps; 0 = left heel strike), the actual step length (for stride warping) and fear 0..1 (idle).
        /// </summary>
        public bool Evaluate(float speed, bool back, float crouch, float phase, float stepLen, float fear, float legLen, float dt)
        {
            if (!Mocap.Ready) return false;
            Ensure();
            float v = speed / Mathf.Max(0.3f, legLen);
            float ph = Mathf.Repeat(phase, 1f);

            MocapClip lo = Mocap.Get(Loco[0]), hi = lo;
            float w = 0f;
            for (int i = 0; i < Loco.Length - 1; i++)
            {
                MocapClip a = Mocap.Get(Loco[i]), b = Mocap.Get(Loco[i + 1]);
                if (a == null || b == null) continue;
                if (v >= a.speed) { lo = a; hi = b; w = Mathf.Min(1f, (v - a.speed) / (b.speed - a.speed)); }
            }
            if (lo == null) return false;
            float sp = back ? 1f - ph : ph;   // backing away: the walk, in reverse
            Mocap.Sample(lo, sp, s1);
            Mocap.Sample(hi, sp, s2);
            Mocap.Blend(s1, s2, w, mv);
            float clipStep = Mathf.Lerp(lo.step, hi.step, w);

            MocapClip cc = Mocap.Get(back ? "crouch_back" : "crouch") ?? Mocap.Get("crouch");
            if (cc != null && crouch > 0.001f)
            {
                Mocap.Sample(cc, ph, cr);
                Mocap.Blend(mv, cr, crouch, mv);
                clipStep = Mathf.Lerp(clipStep, cc.step, crouch);
            }
            // stride warp: the planted foot travels back exactly as fast as the body moves (no sliding)
            float k = Mathf.Clamp(stepLen / Mathf.Max(0.05f, clipStep * legLen), 0.45f, 1.8f);
            mv[Mocap.ANK + 2] *= k;
            mv[Mocap.ANK + 5] *= k;

            // standing still: an idle loop (a scared one when it's near); crouched still: feet together, low
            idleT += dt;
            MocapClip ia = Mocap.Get("idle"), ib = Mocap.Get("idle_scared");
            if (ia != null)
            {
                Mocap.Sample(ia, idleT * ia.rate, idl);
                if (ib != null && fear > 0.001f)
                {
                    Mocap.Sample(ib, idleT * ib.rate + 0.37f, s1);
                    Mocap.Blend(idl, s1, fear, idl);
                }
            }
            else System.Array.Copy(mv, idl, mv.Length);
            if (cc != null && crouch > 0.001f)
            {
                MocapClip c0 = Mocap.Get("crouch");
                Mocap.Sample(c0, 0f, s1);
                Mocap.Sample(c0, 0.5f, s2);
                Mocap.Blend(s1, s2, 0.5f, idl2);
                idl2[Mocap.ANK + 2] *= 0.25f;
                idl2[Mocap.ANK + 5] *= 0.25f;
                Mocap.Blend(idl, idl2, crouch, idl);
            }
            float moveW = Mathf.Clamp01(speed / 0.3f);
            Mocap.Blend(idl, mv, moveW, Pose);
            return true;
        }
    }
}
