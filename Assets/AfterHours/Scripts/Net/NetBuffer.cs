using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace AfterHours.Net
{
    /// <summary>Writes a message into bytes (little-endian, compact).</summary>
    public class NetWriter
    {
        readonly MemoryStream ms = new MemoryStream(256);
        readonly BinaryWriter w;

        public NetWriter() { w = new BinaryWriter(ms, Encoding.UTF8); }

        public NetWriter Begin(MsgType type)
        {
            ms.SetLength(0);
            w.Write((byte)type);
            return this;
        }

        public int Length => (int)ms.Length;
        public byte[] ToArray() => ms.ToArray();

        public void Byte(byte v) => w.Write(v);
        public void Bool(bool v) => w.Write(v);
        public void UShort(ushort v) => w.Write(v);
        public void Int(int v) => w.Write(v);
        public void Float(float v) => w.Write(v);
        public void Str(string s) => w.Write(s ?? "");
        public void Vec(Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        public void Rot(Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
        public void Bytes(byte[] b, int count) { w.Write((ushort)count); w.Write(b, 0, count); }
        /// <summary>An angle in 2 bytes (0.0055 degree steps).</summary>
        public void Angle(float deg) => w.Write((ushort)(Mathf.Repeat(deg, 360f) / 360f * 65535f));
    }

    /// <summary>Reads a message written by NetWriter. Throws on malformed data (the caller drops the packet).</summary>
    public class NetReader
    {
        readonly BinaryReader r;
        public NetReader(byte[] data, int offset, int count)
        {
            r = new BinaryReader(new MemoryStream(data, offset, count, false), Encoding.UTF8);
        }

        public bool More => r.BaseStream.Position < r.BaseStream.Length;
        public byte Byte() => r.ReadByte();
        public bool Bool() => r.ReadBoolean();
        public ushort UShort() => r.ReadUInt16();
        public int Int() => r.ReadInt32();
        public float Float() => r.ReadSingle();
        public string Str() => r.ReadString();
        public Vector3 Vec() => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        public Quaternion Rot() => new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        public byte[] Bytes() { int n = r.ReadUInt16(); return r.ReadBytes(n); }
        public float Angle() => r.ReadUInt16() / 65535f * 360f;
    }

    public enum MsgType : byte
    {
        Hello = 1, Welcome = 2, PlayerJoined = 3, PlayerLeft = 4, StartGame = 5, HostLeaving = 6, Ping = 7,
        PlayerState = 10, Voice = 11, Noise = 12,
        Creatures = 20, WorldDelta = 21, Sfx = 22, Interact = 23, HoldTick = 24, CatchStart = 25,
        EscapeCountdown = 26, EscapeNow = 27, Grip = 28, ItemDrop = 29, Respawned = 30, Stats = 31
    }
}
