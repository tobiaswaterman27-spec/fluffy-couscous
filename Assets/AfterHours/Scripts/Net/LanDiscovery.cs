using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace AfterHours.Net
{
    /// <summary>
    /// Finding games on the same network: a host shouts "AFTERHOURS|name|players|port" once a second on the LAN
    /// broadcast address; anyone on the Join screen listens and lists what it hears.
    /// </summary>
    public class LanDiscovery : IDisposable
    {
        public const int Port = 47799;

        public class Found
        {
            public IPEndPoint endPoint;
            public string name;
            public int players;
            public float seen;
        }

        public readonly List<Found> Games = new List<Found>();
        Socket socket;
        bool hosting;
        float timer;
        string hostName;
        int gamePort;
        public int HostPlayers = 1;
        readonly byte[] buf = new byte[512];

        public void StartAdvertising(string name, int port)
        {
            Dispose();
            hosting = true;
            hostName = name;
            gamePort = port;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { EnableBroadcast = true, Blocking = false };
        }

        public void StartListening()
        {
            Dispose();
            hosting = false;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { Blocking = false };
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            try { socket.Bind(new IPEndPoint(IPAddress.Any, Port)); }
            catch (SocketException e) { Debug.LogWarning("[AfterHours] LAN discovery could not listen: " + e.Message); }
        }

        public void Poll()
        {
            if (socket == null) return;
            if (hosting)
            {
                timer -= Time.unscaledDeltaTime;
                if (timer > 0f) return;
                timer = 1f;
                byte[] msg = Encoding.UTF8.GetBytes($"AFTERHOURS|{hostName}|{HostPlayers}|{gamePort}");
                try { socket.SendTo(msg, new IPEndPoint(IPAddress.Broadcast, Port)); } catch (SocketException) { }
                return;
            }
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            for (int i = 0; i < 32; i++)
            {
                int n;
                try
                {
                    if (socket.Available <= 0) break;
                    n = socket.ReceiveFrom(buf, ref from);
                }
                catch (SocketException) { break; }
                string[] parts = Encoding.UTF8.GetString(buf, 0, n).Split('|');
                if (parts.Length < 4 || parts[0] != "AFTERHOURS" || !int.TryParse(parts[3], out int port)) continue;
                var ep = new IPEndPoint(((IPEndPoint)from).Address, port);
                Found f = Games.Find(g => g.endPoint.Equals(ep));
                if (f == null) Games.Add(f = new Found { endPoint = ep });
                f.name = parts[1];
                int.TryParse(parts[2], out f.players);
                f.seen = Time.unscaledTime;
            }
            Games.RemoveAll(g => Time.unscaledTime - g.seen > 4f);
        }

        public void Dispose()
        {
            if (socket == null) return;
            try { socket.Close(); } catch (Exception) { }
            socket = null;
        }
    }
}
