using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Core;
using AfterHours.Creature;
using AfterHours.Interaction;
using AfterHours.Items;
using AfterHours.Player;
using AfterHours.Puzzles;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.Net
{
    /// <summary>
    /// LAN co-op for up to 4 players. One player hosts (their game is the real one: the creatures, the world,
    /// the puzzles); the others join over the local network. Everyone moves their own body (smooth for others,
    /// instant for you), uses things through the host, talks by proximity voice, and is hunted by 1-3 creatures.
    ///
    /// Co-op rules:
    ///  - Caught: only you start over (back at the atrium; what you carried is dropped where you were taken).
    ///  - Getting out: when the first player reaches an open exit, everyone has five seconds. Whoever is in an exit
    ///    when time runs out gets out. Anyone else is left behind.
    ///  - Pausing doesn't stop the world.
    /// </summary>
    public class NetSession : MonoBehaviour
    {
        public static NetSession Instance { get; private set; }
        public static bool Active => Instance != null && Instance.transport != null && Instance.transport.Running;
        public static bool IsHost => Active && Instance.transport.IsHost;
        public static bool IsClient => Active && !Instance.transport.IsHost;
        public static bool InCoopGame => Active && Instance.phase == Phase.Playing;

        public enum Phase { Lobby, Playing }

        public class NetPlayer
        {
            public byte id;
            public string name;
            public Color colour;
            public string character = "";
            public PlayerController pc;
            public NetProxy proxy;
            public NetPeer peer;     // host only: the connection to this player
            public bool local;
            public bool atExit;
            public EscapeRouteId exitRoute;
            public int caught, solved;
            public bool escaped;
        }

        public readonly List<NetPlayer> Players = new List<NetPlayer>();
        public Phase phase = Phase.Lobby;
        public string Status = "";
        public string LocalName = "Player";
        public NetVoice Voice { get; private set; }
        public LanDiscovery Discovery { get; private set; }

        /// <summary>Raised on every machine when the host starts the game (player index for spawn spacing).</summary>
        public event Action<int> GameStarted;
        /// <summary>Raised on every machine when everyone's fate is decided at an exit.</summary>
        public event Action<EscapeRouteId, bool> EscapeDecided;
        /// <summary>Raised on clients when the host goes away.</summary>
        public event Action<string> SessionEnded;

        NetTransport transport;
        readonly NetWriter w = new NetWriter();
        readonly NetWorld world = new NetWorld();
        readonly NetCreatures creatures = new NetCreatures();
        float stateTimer, worldTimer, creatureTimer, gripTimer;
        bool receivingCatch;
        float countdownEnd = -1f;
        EscapeRouteId countdownRoute;
        readonly List<(string, string)> changes = new List<(string, string)>();


        public NetPlayer Local => Players.Find(p => p.local);
        public float CountdownLeft => countdownEnd < 0f ? -1f : Mathf.Max(0f, countdownEnd - Time.unscaledTime);
        public IReadOnlyList<ElliotBrain> Creatures => creatures.All;

        void Awake()
        {
            Instance = this;
            // in co-op you are the person the host agreed (no two players the same)
            PlayerBody.LocalCharacterOverride = () => Active && Local != null && !string.IsNullOrEmpty(Local.character) ? Local.character : null;
            Discovery = new LanDiscovery();
            Voice = new NetVoice();
            // your own mouth moves with your microphone (seen by the others, and by you in a mirror or third person)
            AfterHours.Player.VoiceMouth.LocalSource = () => Voice != null && Voice.Talking ? Voice.Level : 0f;
            LocalName = "Player " + UnityEngine.Random.Range(100, 999);
        }

        void OnDestroy()
        {
            Shutdown();
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        // Hosting / joining
        // =====================================================================

        public void Host()
        {
            Shutdown();
            transport = new NetTransport();
            try { transport.StartHost(NetTransport.DefaultPort, LocalName); }
            catch (Exception e) { Status = "Couldn't host: " + e.Message; transport = null; return; }
            Hook();
            Players.Clear();
            Players.Add(new NetPlayer { id = 0, name = LocalName, colour = Color.gray, character = AfterHours.Settings.SettingsService.CurrentOrDefault.character, local = true });
            Discovery.StartAdvertising(LocalName, NetTransport.DefaultPort);
            Status = "Hosting. Waiting for players...";
        }

        public void Browse() => Discovery.StartListening();

        public void Join(IPEndPoint host)
        {
            Shutdown();
            transport = new NetTransport();
            try { transport.StartClient(host, LocalName); }
            catch (Exception e) { Status = "Couldn't join: " + e.Message; transport = null; return; }
            Hook();
            Players.Clear();
            Status = "Joining " + host + "...";
        }

        public static bool TryParseAddress(string text, out IPEndPoint ep)
        {
            ep = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string[] parts = text.Trim().Split(':');
            if (!IPAddress.TryParse(parts[0], out IPAddress ip)) return false;
            int port = NetTransport.DefaultPort;
            if (parts.Length > 1 && !int.TryParse(parts[1], out port)) return false;
            ep = new IPEndPoint(ip, port);
            return true;
        }

        void Hook()
        {
            transport.Connected += OnConnected;
            transport.Disconnected += OnDisconnected;
            transport.Received += OnReceived;
        }

        public void Shutdown()
        {
            if (transport != null && transport.IsHost && transport.Running) SendAll(w.Begin(MsgType.HostLeaving), true);
            transport?.Dispose();
            transport = null;
            Discovery?.Dispose();
            Voice?.Stop();
            Unhook();
            foreach (NetPlayer p in Players) if (p.proxy != null) Destroy(p.proxy.gameObject);
            Players.Clear();
            phase = Phase.Lobby;
        }

        void Unhook()
        {
            NoiseBus.Forward = null;
            InteractionFx.Broadcast = null;
            PlayerInteractor.NetInteract = null;
            PlayerInteractor.NetHoldTick = null;
            PlayerInventory.NetDropRequest = null;
            HeavyMovable.NetGrip = null;
            HeavyMovable.NetRelease = null;
            HeavyMovable.NetDriven = false;
            ItemPickup.NetDriven = false;
            CatchSequence.Started -= OnCatchStarted;
            CatchSequence.CoopRespawn = null;
            EscapeTrigger.CoopReport = null;
            GamePause.CannotFreeze = false;
        }

        void OnConnected(NetPeer peer)
        {
            if (transport.IsHost)
            {
                var np = new NetPlayer { id = peer.playerId, name = peer.name, peer = peer };
                np.colour = Color.gray;
                np.character = FreeCharacter(PlayerCharacters.Default, np.id);
                Players.Add(np);
                // Tell the newcomer who's here, and everyone else about the newcomer.
                w.Begin(MsgType.Welcome);
                w.Byte(peer.playerId);
                w.Byte((byte)Players.Count);
                foreach (NetPlayer p in Players) { w.Byte(p.id); w.Str(p.name); }
                transport.SendReliable(peer, w.ToArray());
                w.Begin(MsgType.PlayerJoined);
                w.Byte(np.id);
                w.Str(np.name);
                transport.BroadcastReliable(w.ToArray(), peer);
                // everyone's colours to the newcomer, and the newcomer's (for now) to everyone
                foreach (NetPlayer p in Players) { w.Begin(MsgType.PlayerCharacter); w.Byte(p.id); w.Str(p.character); transport.SendReliable(peer, w.ToArray()); }
                w.Begin(MsgType.PlayerCharacter); w.Byte(np.id); w.Str(np.character); transport.BroadcastReliable(w.ToArray(), peer);
                Discovery.HostPlayers = Players.Count;
                Status = $"Hosting. {Players.Count} player(s).";
                if (phase == Phase.Playing)
                {
                    // Joining a game in progress: start them off, show them to everyone, send the whole world.
                    PlayerController local = FindLocalPlayer();
                    if (local != null) { np.proxy = NetProxy.Create(local, np.id, np.name, np.colour); np.pc = np.proxy.controller; SetCharacter(np, np.character); }
                    transport.SendReliable(peer, StartMessage());
                    world.ResendAll();
                }
            }
            else Status = "Connected. Waiting for the host to start...";
        }

        void OnDisconnected(NetPeer peer)
        {
            if (!transport.IsHost)
            {
                Status = "Lost connection to the host.";
                SessionEnded?.Invoke(Status);
                return;
            }
            NetPlayer np = Players.Find(p => p.peer == peer);
            if (np == null) return;
            RemovePlayer(np);
            w.Begin(MsgType.PlayerLeft);
            w.Byte(np.id);
            SendAll(w, true);
        }

        void RemovePlayer(NetPlayer np)
        {
            Players.Remove(np);
            if (np.proxy != null)
            {
                PlayerInventory inv = np.proxy.GetComponent<PlayerInventory>();
                if (inv != null) inv.DropAtFeet();
                Destroy(np.proxy.gameObject);
            }
            Discovery.HostPlayers = Players.Count;
        }

        // =====================================================================
        // Starting
        // =====================================================================

        int creatureCount;

        byte[] StartMessage()
        {
            w.Begin(MsgType.StartGame);
            w.Byte((byte)creatureCount);
            w.Byte((byte)Players.Count);
            foreach (NetPlayer p in Players) w.Byte(p.id);
            return w.ToArray();
        }

        public void HostStartGame()
        {
            if (!IsHost || phase != Phase.Lobby) return;
            creatureCount = NetCreatures.CountFor(Players.Count) + (AfterHours.Flow.NightDirector.Difficulty >= AfterHours.Flow.NightDirector.Nightmare ? 1 : 0);
            transport.BroadcastReliable(StartMessage());
            BeginGame();
        }

        void BeginGame()
        {
            phase = Phase.Playing;
            Discovery.Dispose();
            PlayerController local = FindLocalPlayer();
            NetPlayer me = Local;
            if (me != null) me.pc = local;
            int index = 0;
            for (int i = 0; i < Players.Count; i++)
            {
                NetPlayer p = Players[i];
                if (p.local) { index = i; continue; }
                if (local == null) continue;
                p.proxy = NetProxy.Create(local, p.id, p.name, p.colour); SetCharacter(p, p.character);
                p.pc = p.proxy.controller;
            }
            world.Build();
            creatures.Setup(creatureCount, transport.IsHost, transport.IsHost && AfterHours.Flow.NightDirector.Difficulty >= AfterHours.Flow.NightDirector.Nightmare);
            GamePause.CannotFreeze = Players.Count > 1;

            // Wire the game's hooks to the network.
            CatchSequence.CoopRespawn = OnCoopRespawn;
            EscapeTrigger.CoopReport = route => { if (transport.IsHost) StartCountdown(route); };
            if (transport.IsHost)
            {
                CatchSequence.Started += OnCatchStarted;
                InteractionFx.Broadcast = (sfx, pos, vol) =>
                {
                    w.Begin(MsgType.Sfx);
                    w.Byte((byte)sfx);
                    w.Vec(pos);
                    w.Float(vol);
                    SendAll(w, false);
                };
            }
            else
            {
                ItemPickup.NetDriven = true;
                HeavyMovable.NetDriven = true;
                NoiseBus.Forward = e =>
                {
                    w.Begin(MsgType.Noise);
                    w.Vec(e.Position);
                    w.Float(e.Radius);
                    w.Byte((byte)e.Kind);
                    SendAll(w, false);
                };
                PlayerInteractor.NetInteract = (i, who) =>
                {
                    string key = world.KeyOf(i);
                    if (key == null) return false;
                    w.Begin(MsgType.Interact);
                    w.Str(key);
                    SendAll(w, true);
                    return true;
                };
                PlayerInteractor.NetHoldTick = (i, who, dt) =>
                {
                    string key = world.KeyOf(i);
                    if (key == null || i is AfterHours.Hiding.HidingSpot) return false;
                    w.Begin(MsgType.HoldTick);
                    w.Str(key);
                    w.Float(dt);
                    SendAll(w, true);
                    return true;
                };
                PlayerInventory.NetDropRequest = (item, pos, vel) =>
                {
                    w.Begin(MsgType.ItemDrop);
                    w.Str(world.KeyOf(item) ?? "");
                    w.Vec(pos);
                    w.Vec(vel);
                    SendAll(w, true);
                };
                HeavyMovable.NetGrip = (h, pos, yaw) =>
                {
                    if (Time.unscaledTime < gripTimer) return;
                    gripTimer = Time.unscaledTime + 0.05f;
                    w.Begin(MsgType.Grip);
                    w.Str(world.KeyOf(h) ?? "");
                    w.Bool(true);
                    w.Vec(pos);
                    w.Float(yaw);
                    SendAll(w, false);
                };
                HeavyMovable.NetRelease = h =>
                {
                    w.Begin(MsgType.Grip);
                    w.Str(world.KeyOf(h) ?? "");
                    w.Bool(false);
                    SendAll(w, true);
                };
            }
            Voice.Start();
            GameStarted?.Invoke(index);
        }

        PlayerController FindLocalPlayer()
        {
            foreach (PlayerController p in PlayerController.All) if (p != null && p.isLocallyControlled) return p;
            return null;
        }

        NetPlayer ById(int id) => Players.Find(p => p.id == id);
        NetPlayer ByController(PlayerController pc) => Players.Find(p => p.pc == pc);

        int PlayerIdOf(GameObject go)
        {
            if (go == null) return -1;
            PlayerController pc = go.GetComponent<PlayerController>();
            NetPlayer np = pc != null ? ByController(pc) : null;
            return np != null ? np.id : -1;
        }

        // =====================================================================
        // Update: sending
        // =====================================================================

        void Update()
        {
            Discovery?.Poll();
            if (transport == null) return;
            transport.Poll();
            if (transport == null || phase != Phase.Playing) return;
            float now = Time.unscaledTime;

            // My body, 20 times a second.
            if (now >= stateTimer)
            {
                stateTimer = now + 0.05f;
                NetPlayer me = Local;
                if (me != null && me.pc != null)
                {
                    WriteState(me);
                    SendAll(w, false);
                }
            }

            // Voice.
            Voice.Muted ^= UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.vKey.wasPressedThisFrame;
            Voice.Poll((data, rms) =>
            {
                NetPlayer me = Local;
                if (me == null) return;
                w.Begin(MsgType.Voice);
                w.Byte(me.id);
                w.Float(rms);
                w.Bytes(data, data.Length);
                SendAll(w, false);
                if (transport.IsHost && me.pc != null) NoiseBus.Emit(new NoiseEvent(me.pc.EyePosition, NetVoice.NoiseRadius(rms), NoiseKind.Voice, me.pc.gameObject));
            });

            if (!transport.IsHost)
            {
                world.Smooth(Time.deltaTime);
                return;
            }

            // Host: creatures 15 times a second, the world 10 times a second.
            if (now >= creatureTimer)
            {
                creatureTimer = now + 0.066f;
                w.Begin(MsgType.Creatures);
                creatures.Write(w);
                SendAll(w, false);
            }
            if (now >= worldTimer)
            {
                worldTimer = now + 0.1f;
                changes.Clear();
                world.CollectChanges(changes, PlayerIdOf);
                SendWorldChanges();
            }

            // The five-second rule.
            NetPlayer host = Local;
            if (host != null) { host.atExit = EscapeTrigger.LocalIsAtExit; host.exitRoute = EscapeTrigger.LocalAtExit; }
            if (countdownEnd > 0f && now >= countdownEnd) FinishCountdown();
        }

        void WriteState(NetPlayer me)
        {
            PlayerController pc = me.pc;
            PlayerMotor m = pc.Motor;
            LocomotionState l = m.Locomotion;
            w.Begin(MsgType.PlayerState);
            w.Byte(me.id);
            w.Float(Time.unscaledTime);
            w.Vec(pc.transform.position);
            w.Angle(pc.transform.eulerAngles.y);
            w.Float(pc.Look != null ? pc.Look.Pitch : 0f);
            w.Byte((byte)m.CurrentStance);
            w.Float(m.CurrentHeight);
            w.Vec(l.worldVelocity);
            w.Byte((byte)l.mode);
            w.Float(l.stridePhase);
            byte flags = 0;
            if (pc.IsHidden) flags |= 1;
            if (l.backward) flags |= 2;
            if (m.LegsActive) flags |= 4;
            if (EscapeTrigger.LocalIsAtExit) flags |= 8;
            w.Byte(flags);
            if (m.LegsActive) { w.Vec(m.KneePosition); w.Vec(m.FeetPosition); }
            w.Byte((byte)m.GroundSurface);
            w.Byte((byte)EscapeTrigger.LocalAtExit);
        }

        void SendWorldChanges()
        {
            if (changes.Count == 0) return;
            int i = 0;
            while (i < changes.Count)
            {
                w.Begin(MsgType.WorldDelta);
                int start = w.Length;
                var batch = new List<(string, string)>();
                int size = 0;
                while (i < changes.Count && size < 1000)
                {
                    batch.Add(changes[i]);
                    size += changes[i].Item1.Length + changes[i].Item2.Length + 4;
                    i++;
                }
                w.UShort((ushort)batch.Count);
                foreach (var (k, d) in batch) { w.Str(k); w.Str(d); }
                SendAll(w, true);
            }
        }

        /// <summary>The wanted person, or the next one along that nobody else is playing.</summary>
        string FreeCharacter(string want, byte forId)
        {
            PlayerCharacters.Entry[] all = PlayerCharacters.All;
            if (all.Length == 0) return want;
            int start = PlayerCharacters.IndexOf(want);
            for (int k = 0; k < all.Length; k++)
            {
                string id = all[(start + k) % all.Length].id;
                if (!Players.Exists(p => p.id != forId && p.character == id)) return id;
            }
            return want;
        }

        void SetCharacter(NetPlayer p, string id)
        {
            p.character = id;
            PlayerBody b = p.proxy != null ? p.proxy.controller.GetComponent<PlayerBody>() : null;
            if (b != null) b.characterId = id;
        }

        void SendAll(NetWriter msg, bool reliable)
        {
            if (transport == null) return;
            byte[] data = msg.ToArray();
            if (reliable) transport.BroadcastReliable(data); else transport.BroadcastUnreliable(data);
        }

        // =====================================================================
        // Receiving
        // =====================================================================

        void OnReceived(NetPeer from, byte[] data, int offset, int count)
        {
            NetReader r;
            MsgType type;
            try
            {
                r = new NetReader(data, offset, count);
                type = (MsgType)r.Byte();
                Handle(from, type, r, data, offset, count);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AfterHours] Bad network message dropped: " + e.Message);
            }
        }

        void Handle(NetPeer from, MsgType type, NetReader r, byte[] raw, int offset, int count)
        {
            bool host = transport.IsHost;
            NetPlayer sender = host ? Players.Find(p => p.peer == from) : null;
            if (phase != Phase.Playing && type >= MsgType.PlayerState) return; // gameplay only while playing

            switch (type)
            {
                case MsgType.Welcome:
                {
                    byte me = r.Byte();
                    int n = r.Byte();
                    Players.Clear();
                    for (int i = 0; i < n; i++)
                    {
                        byte id = r.Byte();
                        string name = r.Str();
                        Players.Add(new NetPlayer { id = id, name = name, colour = Color.gray, local = id == me });
                    }
                    if (Players.Find(p => p.id == me) == null) Players.Add(new NetPlayer { id = me, name = LocalName, colour = Color.gray, local = true });
                    // ask to be the person picked in Settings (the host picks someone else if they're taken)
                    w.Begin(MsgType.PlayerCharacter); w.Byte(me); w.Str(AfterHours.Settings.SettingsService.CurrentOrDefault.character);
                    transport.BroadcastReliable(w.ToArray());
                    break;
                }
                case MsgType.PlayerJoined:
                {
                    byte id = r.Byte();
                    string name = r.Str();
                    if (ById(id) == null)
                    {
                        var np = new NetPlayer { id = id, name = name, colour = Color.gray };
                        Players.Add(np);
                        PlayerController local = FindLocalPlayer();
                        if (phase == Phase.Playing && local != null) { np.proxy = NetProxy.Create(local, id, name, np.colour); np.pc = np.proxy.controller; SetCharacter(np, np.character); }
                    }
                    break;
                }
                case MsgType.PlayerCharacter:
                {
                    byte id = r.Byte();
                    string want = r.Str();
                    if (host)
                    {
                        NetPlayer p = from != null ? ById(from.playerId) : null;
                        if (p == null) break;
                        SetCharacter(p, FreeCharacter(want, p.id));
                        w.Begin(MsgType.PlayerCharacter); w.Byte(p.id); w.Str(p.character);
                        transport.BroadcastReliable(w.ToArray());
                    }
                    else
                    {
                        NetPlayer p = ById(id);
                        if (p != null) SetCharacter(p, want);
                    }
                    break;
                }
                case MsgType.PlayerLeft:
                {
                    NetPlayer p = ById(r.Byte());
                    if (p != null) RemovePlayer(p);
                    break;
                }
                case MsgType.HostLeaving:
                    Status = "The host left the game.";
                    SessionEnded?.Invoke(Status);
                    break;
                case MsgType.StartGame:
                    if (!host && phase == Phase.Lobby)
                    {
                        creatureCount = r.Byte();
                        int n = r.Byte();
                        var order = new List<byte>();
                        for (int i = 0; i < n; i++) order.Add(r.Byte());
                        Players.Sort((a, b) => order.IndexOf(a.id).CompareTo(order.IndexOf(b.id)));
                        BeginGame();
                    }
                    break;

                case MsgType.PlayerState:
                {
                    byte id = r.Byte();
                    if (host)
                    {
                        if (sender == null || sender.id != id) return;
                        byte[] copy = new byte[count];
                        Buffer.BlockCopy(raw, offset, copy, 0, count);
                        transport.BroadcastUnreliable(copy, from); // relay to the other clients
                    }
                    NetPlayer p = ById(id);
                    if (p == null || p.local || p.proxy == null) return;
                    float time = r.Float();
                    Vector3 pos = r.Vec();
                    float yaw = r.Angle();
                    float pitch = r.Float();
                    var stance = (Stance)r.Byte();
                    float height = r.Float();
                    Vector3 vel = r.Vec();
                    var mode = (MoveMode)r.Byte();
                    float stride = r.Float();
                    byte flags = r.Byte();
                    Vector3 knee = pos, feet = pos;
                    if ((flags & 4) != 0) { knee = r.Vec(); feet = r.Vec(); }
                    var surface = (SurfaceType)r.Byte();
                    var route = (EscapeRouteId)r.Byte();
                    p.atExit = (flags & 8) != 0;
                    p.exitRoute = route;
                    if (host && p.atExit) StartCountdown(route);
                    p.proxy.Push(time, pos, yaw, pitch, stance, height, vel, mode, stride, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, knee, feet, surface);
                    break;
                }
                case MsgType.Voice:
                {
                    byte id = r.Byte();
                    float rms = r.Float();
                    byte[] audio = r.Bytes();
                    if (host)
                    {
                        if (sender == null || sender.id != id) return;
                        byte[] copy = new byte[count];
                        Buffer.BlockCopy(raw, offset, copy, 0, count);
                        transport.BroadcastUnreliable(copy, from);
                    }
                    NetPlayer p = ById(id);
                    if (p == null || p.local || p.proxy == null) return;
                    if (p.proxy.voice != null) p.proxy.voice.Feed(audio);
                    if (host) NoiseBus.Emit(new NoiseEvent(p.proxy.head != null ? p.proxy.head.position : p.proxy.transform.position + Vector3.up * 1.6f,
                        NetVoice.NoiseRadius(rms), NoiseKind.Voice, p.proxy.gameObject));
                    break;
                }
                case MsgType.Noise:
                {
                    if (!host || sender == null || sender.proxy == null) return;
                    Vector3 pos = r.Vec();
                    float radius = r.Float();
                    var kind = (NoiseKind)r.Byte();
                    NoiseBus.Emit(new NoiseEvent(pos, radius, kind, sender.proxy.gameObject));
                    break;
                }
                case MsgType.Creatures:
                    if (!host) creatures.Read(r);
                    break;
                case MsgType.WorldDelta:
                {
                    if (host) return;
                    int n = r.UShort();
                    PlayerController local = Local != null ? Local.pc : null;
                    for (int i = 0; i < n; i++)
                    {
                        string key = r.Str();
                        string d = r.Str();
                        world.Apply(key, d, id => ById(id)?.pc, local);
                    }
                    break;
                }
                case MsgType.Sfx:
                {
                    if (host) return;
                    var sfx = (ProceduralSounds.Sfx)r.Byte();
                    Vector3 pos = r.Vec();
                    float vol = r.Float();
                    AudioPool.PlayAt(ProceduralSounds.Effect(sfx), pos, vol, UnityEngine.Random.Range(0.94f, 1.06f), 30f);
                    break;
                }
                case MsgType.Interact:
                    if (host && sender != null && sender.pc != null) world.Interact(r.Str(), sender.pc);
                    break;
                case MsgType.HoldTick:
                    if (host && sender != null && sender.pc != null) world.HoldTick(r.Str(), sender.pc, Mathf.Clamp(r.Float(), 0f, 0.5f));
                    break;
                case MsgType.ItemDrop:
                {
                    if (!host || sender == null || sender.pc == null) return;
                    string key = r.Str();
                    Vector3 pos = r.Vec(), vel = r.Vec();
                    PlayerInventory inv = sender.pc.GetComponent<PlayerInventory>();
                    if (inv != null && inv.Held != null && world.KeyOf(inv.Held) == key) inv.NetRelease(pos, vel);
                    break;
                }
                case MsgType.Grip:
                {
                    if (!host || sender == null) return;
                    HeavyMovable h = world.Find<HeavyMovable>(r.Str());
                    if (h == null) return;
                    if (r.Bool()) h.SetGrip(sender, r.Vec(), r.Float());
                    else h.Release(sender);
                    break;
                }
                case MsgType.CatchStart:
                {
                    if (host) return;
                    byte victimId = r.Byte();
                    int idx = r.Byte();
                    var ctx = (CatchContext)r.Byte();
                    int seed = r.Int();
                    Vector3 ePos = r.Vec();
                    float eYaw = r.Float();
                    NetPlayer victim = ById(victimId);
                    if (victim == null || victim.pc == null || idx >= creatures.All.Count) return;
                    ElliotBrain b = creatures.All[idx];
                    b.motor.Warp(ePos, eYaw);
                    receivingCatch = true;
                    CatchSequence.Begin(b, victim.pc, ctx, seed);
                    receivingCatch = false;
                    victim.caught++;
                    break;
                }
                case MsgType.EscapeCountdown:
                    if (!host) { countdownRoute = (EscapeRouteId)r.Byte(); countdownEnd = Time.unscaledTime + r.Float(); }
                    break;
                case MsgType.EscapeNow:
                {
                    if (host) return;
                    var route = (EscapeRouteId)r.Byte();
                    int n = r.Byte();
                    foreach (NetPlayer p in Players) p.escaped = false;
                    for (int i = 0; i < n; i++)
                    {
                        NetPlayer p = ById(r.Byte());
                        if (p == null) continue;
                        p.escaped = true;
                        p.solved = r.Byte();
                        p.caught = r.Byte();
                    }
                    countdownEnd = -1f;
                    EscapeDecided?.Invoke(route, Local != null && Local.escaped);
                    break;
                }
            }
        }

        // =====================================================================
        // Catching, respawning, escaping
        // =====================================================================

        /// <summary>The game is decided (someone got out): stop exchanging gameplay; the endings play locally.</summary>
        public void FreezeGame()
        {
            phase = Phase.Lobby;
            countdownEnd = -1f;
            Voice.Stop();
        }

        void OnCatchStarted(ElliotBrain brain, PlayerController victim, CatchContext ctx, int seed)
        {
            if (receivingCatch || !transport.IsHost) return;
            NetPlayer np = ByController(victim);
            if (np == null) return;
            np.caught++;
            w.Begin(MsgType.CatchStart);
            w.Byte(np.id);
            w.Byte((byte)Mathf.Max(0, creatures.IndexOf(brain)));
            w.Byte((byte)ctx);
            w.Int(seed);
            w.Vec(brain.transform.position);
            w.Float(brain.transform.eulerAngles.y);
            SendAll(w, true);
        }

        void OnCoopRespawn(ElliotBrain brain, PlayerController victim)
        {
            if (transport != null && transport.IsHost && brain != null) brain.EndCatchCoop();
            if (victim == null || !victim.isLocallyControlled) return;
            // Back to where you woke up. The others are still in there.
            DevLocation start = MallLayout.DevLocations[0];
            int i = Mathf.Max(0, Players.IndexOf(Local));
            victim.SetControlLock(false, false);
            if (victim.Look != null) victim.Look.ExternallyDriven = false;
            victim.IsHidden = false;
            victim.CurrentHidingSpot = null;
            victim.Teleport(start.position + Vector3.right * (i * 1.2f), start.yaw);
        }

        void StartCountdown(EscapeRouteId route)
        {
            if (countdownEnd > 0f) return;
            countdownRoute = route;
            countdownEnd = Time.unscaledTime + 5f;
            w.Begin(MsgType.EscapeCountdown);
            w.Byte((byte)route);
            w.Float(5f);
            SendAll(w, true);
        }

        void FinishCountdown()
        {
            countdownEnd = -1f;
            int solved(NetPlayer p)
            {
                int n = 0;
                foreach (MonoBehaviour m in FindObjectsByType<MonoBehaviour>())
                    if (m is IPuzzleInput input && input.Satisfied && input.LastActor != null && p.pc != null && input.LastActor == p.pc.gameObject) n++;
                return n;
            }
            var outList = new List<NetPlayer>();
            foreach (NetPlayer p in Players)
            {
                p.escaped = p.atExit;
                p.solved = solved(p);
                if (p.escaped) outList.Add(p);
            }
            if (outList.Count == 0) return; // they stepped back inside; nothing happens
            w.Begin(MsgType.EscapeNow);
            w.Byte((byte)countdownRoute);
            w.Byte((byte)outList.Count);
            foreach (NetPlayer p in outList) { w.Byte(p.id); w.Byte((byte)Mathf.Min(255, p.solved)); w.Byte((byte)Mathf.Min(255, p.caught)); }
            SendAll(w, true);
            EscapeDecided?.Invoke(countdownRoute, Local != null && Local.escaped);
        }

        // =====================================================================
        // Little on-screen bits: the countdown and your microphone
        // =====================================================================

        GUIStyle style;

        void OnGUI()
        {
            if (phase != Phase.Playing) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float w0 = Screen.width / scale;
            float left = CountdownLeft;
            if (left >= 0f)
            {
                style.fontSize = 40;
                GUI.color = new Color(0.95f, 0.9f, 0.85f, 0.9f);
                GUI.Label(new Rect(0f, 120f, w0, 60f), "GET OUT  " + Mathf.CeilToInt(left), style);
                style.fontSize = 22;
            }
            if (Players.Count > 1)
            {
                GUI.color = new Color(1f, 1f, 1f, Voice.Muted ? 0.5f : Voice.Talking ? 0.9f : 0.3f);
                style.alignment = TextAnchor.MiddleLeft;
                GUI.Label(new Rect(30f, 1080f - 60f, 300f, 40f), Voice.Muted ? "mic off (V)" : Voice.Talking ? "● speaking" : "mic (V)", style);
                style.alignment = TextAnchor.MiddleCenter;
            }
            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.identity;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;
    }
}
