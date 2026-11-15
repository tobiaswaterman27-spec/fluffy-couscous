using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace AfterHours.Net
{
    /// <summary>One connection (the host has one per client; a client has one: the host).</summary>
    public class NetPeer
    {
        public IPEndPoint endPoint;
        public byte playerId;
        public string name = "";
        public float lastHeard;
        internal ushort nextSendSeq;
        internal ushort nextRecvSeq;
        internal readonly Dictionary<ushort, (byte[] data, float sent, int tries)> unacked = new Dictionary<ushort, (byte[], float, int)>();
        internal readonly Dictionary<ushort, byte[]> early = new Dictionary<ushort, byte[]>();
        public float Rtt { get; internal set; } = 0.02f;
    }

    /// <summary>
    /// A small UDP transport for LAN play. Two channels: unreliable (positions, voice - newest wins) and
    /// reliable-ordered (everything that must arrive: interactions, world changes, catches). Polled on the main
    /// thread, no background threads. Peers that go quiet for 8 seconds are dropped.
    /// Packet: [kind][...]. kind 0 = unreliable payload, 1 = reliable [seq][payload], 2 = ack [seq],
    /// 3 = connect [protocol][name], 4 = accept [your id], 5 = goodbye.
    /// </summary>
    public class NetTransport : IDisposable
    {
        public const int DefaultPort = 47800;
        public const ushort Protocol = 1;
        const float ResendAfter = 0.15f, Timeout = 8f;

        public bool IsHost { get; private set; }
        public bool Running => socket != null;
        public readonly List<NetPeer> Peers = new List<NetPeer>();

        public event Action<NetPeer> Connected;
        public event Action<NetPeer> Disconnected;
        public event Action<NetPeer, byte[], int, int> Received; // peer, data, offset, count

        Socket socket;
        readonly byte[] recvBuf = new byte[65536];
        EndPoint anyEp = new IPEndPoint(IPAddress.Any, 0);
        byte nextPlayerId = 1;
        string myName;
        NetPeer pendingHost;
        float connectTimer;
        public byte MyId { get; private set; }
        public int MaxPlayers = 4;

        public void StartHost(int port, string name)
        {
            IsHost = true;
            myName = name;
            MyId = 0;
            Open(port);
        }

        public void StartClient(IPEndPoint host, string name)
        {
            IsHost = false;
            myName = name;
            Open(0);
            pendingHost = new NetPeer { endPoint = host, lastHeard = Time.unscaledTime };
            connectTimer = 0f;
        }

        void Open(int port)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Blocking = false;
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
        }

        public void Dispose()
        {
            if (socket == null) return;
            foreach (NetPeer p in Peers) SendRaw(p.endPoint, new byte[] { 5 });
            try { socket.Close(); } catch (Exception) { }
            socket = null;
            Peers.Clear();
            pendingHost = null;
        }

        // ---------------- sending ----------------

        public void SendUnreliable(NetPeer p, byte[] payload) => SendRaw(p.endPoint, Wrap(0, payload));

        public void SendReliable(NetPeer p, byte[] payload)
        {
            ushort seq = p.nextSendSeq++;
            byte[] packet = new byte[payload.Length + 3];
            packet[0] = 1;
            packet[1] = (byte)(seq & 0xff);
            packet[2] = (byte)(seq >> 8);
            Buffer.BlockCopy(payload, 0, packet, 3, payload.Length);
            p.unacked[seq] = (packet, Time.unscaledTime, 1);
            SendRaw(p.endPoint, packet);
        }

        public void BroadcastUnreliable(byte[] payload, NetPeer except = null)
        {
            foreach (NetPeer p in Peers) if (p != except) SendUnreliable(p, payload);
        }

        public void BroadcastReliable(byte[] payload, NetPeer except = null)
        {
            foreach (NetPeer p in Peers) if (p != except) SendReliable(p, payload);
        }

        static byte[] Wrap(byte kind, byte[] payload)
        {
            byte[] b = new byte[payload.Length + 1];
            b[0] = kind;
            Buffer.BlockCopy(payload, 0, b, 1, payload.Length);
            return b;
        }

        void SendRaw(IPEndPoint ep, byte[] data)
        {
            if (socket == null || ep == null) return;
            try { socket.SendTo(data, ep); }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }

        // ---------------- receiving ----------------

        public void Poll()
        {
            if (socket == null) return;
            float now = Time.unscaledTime;

            if (pendingHost != null)
            {
                // Keep knocking until the host answers (or give up after 6 s).
                connectTimer -= Time.unscaledDeltaTime;
                if (connectTimer <= 0f)
                {
                    connectTimer = 0.5f;
                    var w = new System.IO.MemoryStream();
                    var bw = new System.IO.BinaryWriter(w);
                    bw.Write((byte)3); bw.Write(Protocol); bw.Write(myName ?? "");
                    SendRaw(pendingHost.endPoint, w.ToArray());
                }
                if (now - pendingHost.lastHeard > 6f) { NetPeer failed = pendingHost; pendingHost = null; Disconnected?.Invoke(failed); }
            }

            for (int guard = 0; guard < 512; guard++)
            {
                int n;
                try
                {
                    if (socket.Available <= 0) break;
                    n = socket.ReceiveFrom(recvBuf, ref anyEp);
                }
                catch (SocketException) { break; }
                catch (ObjectDisposedException) { return; }
                if (n <= 0) continue;
                Handle((IPEndPoint)anyEp, recvBuf, n, now);
                if (socket == null) return;
            }

            // Resend unacknowledged reliable packets; drop silent peers.
            for (int i = Peers.Count - 1; i >= 0; i--)
            {
                NetPeer p = Peers[i];
                if (now - p.lastHeard > Timeout) { Peers.RemoveAt(i); Disconnected?.Invoke(p); continue; }
                if (p.unacked.Count == 0) continue;
                var resend = new List<ushort>();
                foreach (var kv in p.unacked) if (now - kv.Value.sent > Mathf.Max(ResendAfter, p.Rtt * 2.5f)) resend.Add(kv.Key);
                foreach (ushort s in resend)
                {
                    var u = p.unacked[s];
                    p.unacked[s] = (u.data, now, u.tries + 1);
                    SendRaw(p.endPoint, u.data);
                }
            }
        }

        NetPeer Find(IPEndPoint ep)
        {
            foreach (NetPeer p in Peers) if (p.endPoint.Equals(ep)) return p;
            return null;
        }

        void Handle(IPEndPoint from, byte[] buf, int n, float now)
        {
            byte kind = buf[0];
            NetPeer peer = Find(from);

            if (kind == 3 && IsHost)
            {
                // A client knocking.
                if (peer == null)
                {
                    ushort proto = n >= 3 ? (ushort)(buf[1] | (buf[2] << 8)) : (ushort)0;
                    if (proto != Protocol || Peers.Count >= MaxPlayers - 1) return;
                    string name = "";
                    try { name = new System.IO.BinaryReader(new System.IO.MemoryStream(buf, 3, n - 3)).ReadString(); } catch (Exception) { }
                    peer = new NetPeer { endPoint = from, playerId = nextPlayerId++, name = name, lastHeard = now };
                    Peers.Add(peer);
                    SendRaw(from, new byte[] { 4, peer.playerId });
                    Connected?.Invoke(peer);
                }
                else SendRaw(from, new byte[] { 4, peer.playerId });
                return;
            }
            if (kind == 4 && !IsHost && pendingHost != null && from.Equals(pendingHost.endPoint))
            {
                MyId = buf[1];
                NetPeer host = pendingHost;
                host.lastHeard = now;
                pendingHost = null;
                Peers.Add(host);
                Connected?.Invoke(host);
                return;
            }
            if (peer == null) return;
            peer.lastHeard = now;

            switch (kind)
            {
                case 0:
                    Received?.Invoke(peer, buf, 1, n - 1);
                    break;
                case 1:
                {
                    if (n < 3) return;
                    ushort seq = (ushort)(buf[1] | (buf[2] << 8));
                    SendRaw(from, new byte[] { 2, buf[1], buf[2] });
                    short diff = (short)(seq - peer.nextRecvSeq);
                    if (diff < 0) return; // already had it
                    byte[] payload = new byte[n - 3];
                    Buffer.BlockCopy(buf, 3, payload, 0, payload.Length);
                    if (diff > 0) { peer.early[seq] = payload; return; }
                    Received?.Invoke(peer, payload, 0, payload.Length);
                    peer.nextRecvSeq++;
                    while (peer.early.TryGetValue(peer.nextRecvSeq, out byte[] next))
                    {
                        peer.early.Remove(peer.nextRecvSeq);
                        Received?.Invoke(peer, next, 0, next.Length);
                        peer.nextRecvSeq++;
                    }
                    break;
                }
                case 2:
                {
                    ushort seq = (ushort)(buf[1] | (buf[2] << 8));
                    if (peer.unacked.TryGetValue(seq, out var u))
                    {
                        if (u.tries == 1) peer.Rtt = Mathf.Lerp(peer.Rtt, now - u.sent, 0.2f);
                        peer.unacked.Remove(seq);
                    }
                    break;
                }
                case 5:
                    Peers.Remove(peer);
                    Disconnected?.Invoke(peer);
                    break;
            }
        }
    }
}
