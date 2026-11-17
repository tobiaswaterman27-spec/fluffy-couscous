using System;
using UnityEngine;
using AfterHours.Audio;

namespace AfterHours.Net
{
    /// <summary>
    /// Proximity voice chat. Your microphone is read at 16 kHz, quiet parts are gated out, and 20 ms chunks are
    /// compressed (mu-law, 8 bits) and sent. Other players hear you from where your body is, muffled by walls and
    /// doors. Elliot hears you too: a whisper carries a couple of metres, talking ~9 m, shouting ~20 m, screaming
    /// much further. V mutes / unmutes your microphone.
    /// </summary>
    public class NetVoice
    {
        public const int Rate = 16000;
        public const int Chunk = 320; // 20 ms
        public bool Muted;
        public bool Talking { get; private set; }
        public float Level { get; private set; }

        AudioClip mic;
        string device;
        int readPos;
        readonly float[] samples = new float[Chunk];
        float gateHold;

        public bool Start()
        {
            if (Microphone.devices == null || Microphone.devices.Length == 0) return false;
            device = Microphone.devices[0];
            mic = Microphone.Start(device, true, 1, Rate);
            readPos = 0;
            return mic != null;
        }

        public void Stop()
        {
            if (mic != null && Microphone.IsRecording(device)) Microphone.End(device);
            mic = null;
        }

        /// <summary>Returns each finished 20 ms chunk (encoded) with its loudness; null when there is nothing to send.</summary>
        public void Poll(Action<byte[], float> send)
        {
            if (mic == null) return;
            int pos = Microphone.GetPosition(device);
            int available = pos - readPos;
            if (available < 0) available += mic.samples;
            while (available >= Chunk)
            {
                mic.GetData(samples, readPos);
                readPos = (readPos + Chunk) % mic.samples;
                available -= Chunk;
                float sum = 0f;
                for (int i = 0; i < Chunk; i++) sum += samples[i] * samples[i];
                float rms = Mathf.Sqrt(sum / Chunk);
                Level = Mathf.Lerp(Level, rms, 0.3f);
                // Noise gate (with a short hold so words aren't chopped).
                if (rms > 0.012f) gateHold = 0.25f; else gateHold -= 0.02f;
                Talking = gateHold > 0f && !Muted;
                if (!Talking) continue;
                send(Encode(samples), rms);
            }
        }

        /// <summary>How far the creatures can hear this loudness (metres).</summary>
        public static float NoiseRadius(float rms)
        {
            if (rms < 0.03f) return 2.5f;   // whisper
            if (rms < 0.12f) return 9f;     // talking
            if (rms < 0.3f) return 20f;     // shouting
            return 35f;                     // screaming
        }

        public static byte[] Encode(float[] pcm)
        {
            var b = new byte[pcm.Length];
            for (int i = 0; i < pcm.Length; i++) b[i] = MuLawEncode(pcm[i]);
            return b;
        }

        public static void Decode(byte[] data, float[] into)
        {
            for (int i = 0; i < data.Length && i < into.Length; i++) into[i] = MuLawDecode(data[i]);
        }

        static byte MuLawEncode(float f)
        {
            const int Bias = 0x84, Clip = 32635;
            int s = Mathf.Clamp((int)(f * 32767f), -32767, 32767);
            int sign = (s >> 8) & 0x80;
            if (sign != 0) s = -s;
            if (s > Clip) s = Clip;
            s += Bias;
            int exponent = 7;
            for (int mask = 0x4000; (s & mask) == 0 && exponent > 0; mask >>= 1) exponent--;
            int mantissa = (s >> (exponent + 3)) & 0x0F;
            return (byte)~(sign | (exponent << 4) | mantissa);
        }

        static float MuLawDecode(byte b)
        {
            int u = ~b & 0xFF;
            int sign = u & 0x80, exponent = (u >> 4) & 0x07, mantissa = u & 0x0F;
            int s = ((mantissa << 3) + 0x84) << exponent;
            s -= 0x84;
            return (sign != 0 ? -s : s) / 32768f;
        }
    }

    /// <summary>Plays another player's voice from their head, in 3D, through walls (occlusion).</summary>
    public class VoicePlayer : MonoBehaviour
    {
        /// <summary>How loud this player's voice is coming out right now (RMS of what's playing), for their mouth.</summary>
        public float Loudness { get; private set; }
        volatile float audioRms;
        AudioSource source;

        void Update() => Loudness = Mathf.Lerp(Loudness, audioRms, 1f - Mathf.Exp(-25f * Time.deltaTime));
        readonly float[] ring = new float[NetVoice.Rate * 2];
        int writePos, readPos, buffered;
        readonly object gate = new object();
        readonly float[] chunk = new float[NetVoice.Chunk];

        void Start()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1.2f;
            source.maxDistance = 35f;
            source.dopplerLevel = 0f;
            source.clip = AudioClip.Create("Voice", NetVoice.Rate, 1, NetVoice.Rate, true, OnRead);
            source.loop = true;
            source.Play();
            gameObject.AddComponent<AudioOcclusion>();
        }

        public void Feed(byte[] data)
        {
            NetVoice.Decode(data, chunk);
            lock (gate)
            {
                for (int i = 0; i < data.Length && i < chunk.Length; i++)
                {
                    ring[writePos] = chunk[i];
                    writePos = (writePos + 1) % ring.Length;
                }
                buffered = Mathf.Min(buffered + data.Length, ring.Length);
            }
        }

        void OnRead(float[] data)
        {
            try { ReadInto(data); }
            finally
            {
                float sum = 0f;
                for (int i = 0; i < data.Length; i++) sum += data[i] * data[i];
                audioRms = data.Length > 0 ? Mathf.Sqrt(sum / data.Length) : 0f;
            }
        }

        void ReadInto(float[] data)
        {
            lock (gate)
            {
                // Wait for ~60 ms of audio before starting, so small network hiccups don't crackle.
                for (int i = 0; i < data.Length; i++)
                {
                    if (buffered > 0 && (buffered > 960 || started))
                    {
                        started = true;
                        data[i] = ring[readPos];
                        readPos = (readPos + 1) % ring.Length;
                        buffered--;
                    }
                    else
                    {
                        started = false;
                        data[i] = 0f;
                    }
                }
            }
        }

        bool started;
    }
}
