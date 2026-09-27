using System;
using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Data;
using Sanguo.Events;
using Sanguo.Game;
using Sanguo.GameModes;
using Sanguo.Utils;

namespace Sanguo.Network
{
    /// <summary>A server hosting one room: lobby, then an authoritative game session.</summary>
    public interface IGameServer
    {
        RoomState Room { get; }
        bool IsRunning { get; }
        int Port { get; }

        /// <summary>The running game (null while in the lobby).</summary>
        GameSession Session { get; }

        void Start(int port);
        void Stop();

        /// <summary>Processes network input, advances the game and sends output. Call regularly on one thread.</summary>
        void Poll();

        RoomInfo BuildRoomInfo(string address);
    }

    public sealed class ServerOptions
    {
        public string ServerName = "房主";
        public int PingIntervalMs = 2000;
        public int ConnectionTimeoutMs = 12000;
        public int MaxMessagesPerSecond = 120;
        public int MaxSpectators = 8;
        public int MaxNicknameLength = 16;
        public int MaxChatLength = 100;
        /// <summary>Delay before AI seats act, so humans can follow the table.</summary>
        public int AIThinkDelayMs = 600;
        /// <summary>Fixed seed for reproducible games (tests); null = random.</summary>
        public int? Seed;
    }

    /// <summary>
    /// Server-authoritative host for LAN play (transport agnostic, so the same class can run as a
    /// dedicated server later). Clients only ever send intents; the server validates everything,
    /// decides every outcome and sends each client only what that client may see.
    /// </summary>
    public sealed class LanGameServer : IGameServer
    {
        private sealed class Peer
        {
            public IConnection Connection;
            public bool HelloDone;
            public int SlotId = -1;
            public bool Spectator;
            public string Nickname = string.Empty;
            public int AvatarId;
            public long LastReceivedMs;
            public long LastPingSentMs;
            public long RateWindowStart;
            public int RateCount;
        }

        private readonly INetworkTransport _transport;
        private readonly GameContent _content;
        private readonly GameModeRegistry _modes;
        private readonly IClock _clock;
        private readonly ServerOptions _options;
        private readonly List<Peer> _peers = new List<Peer>();
        private readonly List<string> _tokens = new List<string>();
        private readonly Dictionary<int, List<GameEvent>> _outbox = new Dictionary<int, List<GameEvent>>();
        private readonly System.Security.Cryptography.RandomNumberGenerator _tokenRandom = System.Security.Cryptography.RandomNumberGenerator.Create();
        private IConnectionListener _listener;
        private GameSession _session;
        private bool _roomDirty;
        private bool _pingDirty;
        private long _lastRoomBroadcastMs;

        /// <summary>Ping-only room updates are sent at most this often.</summary>
        public int PingBroadcastIntervalMs { get; set; } = 2000;

        public LanGameServer(INetworkTransport transport, GameContent content, GameModeRegistry modes, IClock clock, RoomSettings settings,
            ServerOptions options = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _modes = modes ?? GameModeRegistry.CreateDefault();
            _clock = clock ?? new SystemClock();
            _options = options ?? new ServerOptions();
            Room = new RoomState { RoomId = NewId(8), Settings = settings?.Clone() ?? new RoomSettings() };
            ResizeSlots(Room.Settings.Config.PlayerCount);
        }

        public RoomState Room { get; }
        public bool IsRunning => _listener != null;
        public int Port => _listener?.Port ?? 0;
        public GameSession Session => _session;

        /// <summary>Diagnostics hook (connection events, rejected actions).</summary>
        public event Action<string> Log;

        private long Now => _clock.NowMs;

        public void Start(int port)
        {
            if (_listener != null) throw new InvalidOperationException("Server already started.");
            _listener = _transport.Listen(port);
            Log?.Invoke("listening on port " + _listener.Port);
        }

        public void Stop()
        {
            foreach (var p in _peers)
            {
                Send(p, MessageType.Disconnect, Payloads.Reason("server_stopped", "房主关闭了房间"));
                p.Connection.Close("server stopped");
            }
            _peers.Clear();
            _listener?.Stop();
            _listener = null;
        }

        public void SetInviteCode(string code)
        {
            Room.InviteCode = code ?? string.Empty;
            _roomDirty = true;
        }

        public RoomInfo BuildRoomInfo(string address)
        {
            var host = Room.GetSlot(Room.HostSlotId);
            return new RoomInfo
            {
                RoomId = Room.RoomId,
                RoomName = Room.Settings.RoomName,
                HostName = host != null && host.Occupied ? host.Nickname : _options.ServerName,
                ModeId = Room.Settings.Config.ModeId,
                Humans = Room.HumanCount,
                MaxPlayers = Room.Slots.Count,
                InGame = Room.InGame,
                AllowSpectators = Room.Settings.Config.AllowSpectators,
                Address = address ?? string.Empty,
                Port = Port,
                InviteCode = Room.InviteCode
            };
        }

        // ================================================================== main loop

        public void Poll()
        {
            if (_listener == null) return;
            while (_listener.TryAccept(out var conn))
            {
                _peers.Add(new Peer { Connection = conn, LastReceivedMs = Now, RateWindowStart = Now });
                Log?.Invoke("connection " + conn.Id + " from " + conn.RemoteAddress);
            }

            for (int i = 0; i < _peers.Count; i++)
            {
                var peer = _peers[i];
                for (int n = 0; n < 256 && peer.Connection.TryReceive(out var message); n++)
                {
                    peer.LastReceivedMs = Now;
                    if (!RateOk(peer))
                    {
                        // Tell the client instead of silently dropping, so it never waits forever.
                        if (peer.Connection.IsConnected && CommandCodec.IsCommandMessage(message.Type))
                            Send(peer, MessageType.CommandResult, CommandCodec.EncodeResult(message.SequenceId, CommandResult.Reject(RejectReason.RateLimited)));
                        continue;
                    }
                    try
                    {
                        Handle(peer, message);
                    }
                    catch (ProtocolException ex)
                    {
                        Drop(peer, "protocol_error", ex.Message);
                    }
                }
            }

            CheckConnections();

            if (_session != null)
            {
                _session.Update();
                FlushEvents();
                CheckGameOver();
            }

            // Membership/settings changes go out at once; ping updates are throttled.
            if (_roomDirty || (_pingDirty && Now - _lastRoomBroadcastMs >= PingBroadcastIntervalMs)) BroadcastRoom();
        }

        private bool RateOk(Peer peer)
        {
            if (Now - peer.RateWindowStart >= 1000)
            {
                peer.RateWindowStart = Now;
                peer.RateCount = 0;
            }
            peer.RateCount++;
            if (peer.RateCount > _options.MaxMessagesPerSecond * 4)
            {
                Drop(peer, "flood", "消息过于频繁");
                return false;
            }
            return peer.RateCount <= _options.MaxMessagesPerSecond;
        }

        private void CheckConnections()
        {
            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                var peer = _peers[i];
                if (peer.Connection.IsConnected && Now - peer.LastReceivedMs > _options.ConnectionTimeoutMs)
                    peer.Connection.Close("timeout");
                if (!peer.Connection.IsConnected)
                {
                    _peers.RemoveAt(i);
                    OnPeerGone(peer, peer.Connection.CloseReason ?? "closed");
                    continue;
                }
                if (peer.HelloDone && Now - peer.LastPingSentMs >= _options.PingIntervalMs)
                {
                    peer.LastPingSentMs = Now;
                    Send(peer, MessageType.Ping, Payloads.Long(Now));
                }
            }
        }

        // ================================================================== dispatch

        private void Handle(Peer peer, NetworkMessage m)
        {
            if (!peer.HelloDone)
            {
                if (m.Type != MessageType.Hello)
                {
                    Drop(peer, "hello_required", "需要先握手");
                    return;
                }
                OnHello(peer, Payloads.ReadHello(m));
                return;
            }
            switch (m.Type)
            {
                case MessageType.Ping:
                    Send(peer, MessageType.Pong, m.Payload);
                    break;
                case MessageType.Pong:
                    OnPong(peer, Payloads.ReadLong(m));
                    break;
                case MessageType.JoinRoom:
                    OnJoin(peer, Payloads.ReadJoin(m));
                    break;
                case MessageType.Reconnect:
                {
                    int slot = Payloads.ReadReconnect(m, out string token);
                    OnReconnect(peer, slot, token);
                    break;
                }
                case MessageType.LeaveRoom:
                    OnLeave(peer);
                    break;
                case MessageType.PlayerReady:
                    OnReady(peer, Payloads.ReadBool(m));
                    break;
                case MessageType.KickPlayer:
                    OnKick(peer, Payloads.ReadInt(m));
                    break;
                case MessageType.TransferHost:
                    OnTransferHost(peer, Payloads.ReadInt(m));
                    break;
                case MessageType.UpdateRoomSettings:
                    OnUpdateSettings(peer, Payloads.ReadSettings(m));
                    break;
                case MessageType.ChangeTeam:
                {
                    int slot = Payloads.ReadChangeTeam(m, out var team);
                    OnChangeTeam(peer, slot, team);
                    break;
                }
                case MessageType.StartGame:
                    OnStartGame(peer);
                    break;
                case MessageType.Chat:
                    OnChat(peer, Payloads.ReadChat(m, out _));
                    break;
                case MessageType.SetAutoPlay:
                    if (_session != null && peer.SlotId >= 0) _session.SetAutoPlay(peer.SlotId, Payloads.ReadBool(m));
                    break;
                case MessageType.RequestResync:
                    if (_session != null) SendSnapshot(peer);
                    break;
                case MessageType.Disconnect:
                    peer.Connection.Close("client disconnected");
                    break;
                default:
                    if (CommandCodec.IsCommandMessage(m.Type)) OnCommand(peer, m);
                    else Send(peer, MessageType.Error, Payloads.Reason("unexpected", m.Type.ToString()));
                    break;
            }
        }

        // ================================================================== connection & lobby

        private void OnHello(Peer peer, HelloPayload hello)
        {
            if (hello.ProtocolVersion != ProtocolInfo.Version)
            {
                Drop(peer, "version_mismatch", "版本不一致（服务器 " + ProtocolInfo.Version + "，客户端 " + hello.ProtocolVersion + "）");
                return;
            }
            peer.HelloDone = true;
            peer.Nickname = SanitizeName(hello.Nickname, "玩家");
            peer.AvatarId = hello.AvatarId;
            Send(peer, MessageType.Welcome, Payloads.Welcome(peer.Connection.Id, _options.ServerName, Room.RoomId));
        }

        private void OnPong(Peer peer, long sentAt)
        {
            if (peer.SlotId < 0) return;
            var slot = Room.GetSlot(peer.SlotId);
            int rtt = (int)Math.Max(0, Math.Min(9999, Now - sentAt));
            if (slot != null && Math.Abs(slot.PingMs - rtt) >= 5)
            {
                slot.PingMs = rtt;
                _pingDirty = true;
            }
        }

        private void OnJoin(Peer peer, JoinPayload join)
        {
            if (peer.SlotId >= 0 || peer.Spectator)
            {
                Send(peer, MessageType.JoinRejected, Payloads.Reason("already_joined", "已在房间中"));
                return;
            }
            string name = SanitizeName(join.Nickname, peer.Nickname);
            if (join.Spectator)
            {
                if (!Room.Settings.Config.AllowSpectators || SpectatorCount() >= _options.MaxSpectators)
                {
                    Send(peer, MessageType.JoinRejected, Payloads.Reason("no_spectators", "房间不允许观战"));
                    return;
                }
                peer.Spectator = true;
                peer.Nickname = name;
                Room.SpectatorCount = SpectatorCount();
                Send(peer, MessageType.JoinAccepted, Payloads.JoinAccepted(-1, null, Room));
                if (_session != null) SendSnapshot(peer);
                _roomDirty = true;
                return;
            }
            if (Room.InGame)
            {
                Send(peer, MessageType.JoinRejected, Payloads.Reason("in_game", "游戏已开始"));
                return;
            }
            int free = Room.Slots.FindIndex(s => !s.Occupied);
            if (free < 0)
            {
                Send(peer, MessageType.JoinRejected, Payloads.Reason("room_full", "房间已满"));
                return;
            }
            var slot = Room.Slots[free];
            slot.Occupied = true;
            slot.Nickname = name;
            slot.AvatarId = join.AvatarId;
            slot.Ready = false;
            slot.Connected = true;
            slot.PingMs = 0;
            slot.Team = PickTeam(free);
            peer.SlotId = free;
            peer.Nickname = name;
            peer.AvatarId = join.AvatarId;
            _tokens[free] = NewId(24);
            if (!HostPresent()) Room.HostSlotId = free;
            Send(peer, MessageType.JoinAccepted, Payloads.JoinAccepted(free, _tokens[free], Room));
            Log?.Invoke(name + " joined slot " + free);
            _roomDirty = true;
        }

        private void OnReconnect(Peer peer, int slotId, string token)
        {
            var slot = Room.GetSlot(slotId);
            if (slot == null || !slot.Occupied || token == null || _tokens[slotId] != token)
            {
                Send(peer, MessageType.ReconnectRejected, Payloads.Reason("bad_token", "无法恢复该座位"));
                return;
            }
            // A stale connection for the same seat (half-open socket) is replaced.
            foreach (var other in _peers)
            {
                if (other != peer && other.SlotId == slotId)
                {
                    other.SlotId = -1;
                    other.Connection.Close("replaced by reconnect");
                }
            }
            peer.SlotId = slotId;
            peer.Nickname = slot.Nickname;
            slot.Connected = true;
            if (_session != null) _session.SetConnected(slotId, true);
            Send(peer, MessageType.ReconnectAccepted, Payloads.JoinAccepted(slotId, token, Room));
            if (_session != null) SendSnapshot(peer);
            Log?.Invoke(slot.Nickname + " reconnected to slot " + slotId);
            _roomDirty = true;
        }

        private void OnLeave(Peer peer)
        {
            int slotId = peer.SlotId;
            peer.SlotId = -1;
            peer.Spectator = false;
            ReleaseSeat(slotId, true);
            Room.SpectatorCount = SpectatorCount();
            _roomDirty = true;
        }

        private void OnPeerGone(Peer peer, string reason)
        {
            Log?.Invoke("connection " + peer.Connection.Id + " closed: " + reason);
            if (peer.SlotId >= 0) ReleaseSeat(peer.SlotId, false);
            Room.SpectatorCount = SpectatorCount();
            _roomDirty = true;
        }

        /// <summary>
        /// In the lobby a leaving player frees the seat. During a game the seat is kept for
        /// reconnection and the AI plays it (immediately if the player left on purpose).
        /// </summary>
        private void ReleaseSeat(int slotId, bool leftOnPurpose)
        {
            var slot = Room.GetSlot(slotId);
            if (slot == null || !slot.Occupied) return;
            if (Room.InGame && _session != null)
            {
                slot.Connected = false;
                _session.SetConnected(slotId, false);
                if (leftOnPurpose) _session.SetAutoPlay(slotId, true);
            }
            else
            {
                slot.Occupied = false;
                slot.Nickname = string.Empty;
                slot.Ready = false;
                slot.Connected = false;
                slot.PingMs = 0;
                _tokens[slotId] = null;
            }
            if (Room.HostSlotId == slotId) TransferHostToNextHuman();
        }

        private void OnReady(Peer peer, bool ready)
        {
            var slot = Room.GetSlot(peer.SlotId);
            if (slot == null || Room.InGame) return;
            slot.Ready = ready;
            _roomDirty = true;
        }

        private void OnKick(Peer peer, int slotId)
        {
            if (!IsHost(peer) || slotId == peer.SlotId) return;
            var slot = Room.GetSlot(slotId);
            if (slot == null || !slot.Occupied) return;
            foreach (var other in _peers.ToArray())
            {
                if (other.SlotId != slotId) continue;
                Send(other, MessageType.Disconnect, Payloads.Reason("kicked", "你被房主移出房间"));
                other.SlotId = -1;
                other.Connection.Close("kicked");
            }
            if (Room.InGame && _session != null)
            {
                slot.Connected = false;
                _session.SetConnected(slotId, false);
                _session.SetAutoPlay(slotId, true);
                _tokens[slotId] = NewId(24); // the kicked player cannot come back
            }
            else
            {
                ReleaseSeat(slotId, true);
            }
            _roomDirty = true;
        }

        private void OnTransferHost(Peer peer, int slotId)
        {
            if (!IsHost(peer)) return;
            var slot = Room.GetSlot(slotId);
            if (slot == null || !slot.Occupied || !slot.Connected) return;
            Room.HostSlotId = slotId;
            _roomDirty = true;
        }

        private void OnUpdateSettings(Peer peer, RoomSettings settings)
        {
            if (!IsHost(peer) || Room.InGame) return;
            var config = settings.Config;
            int count = config.PlayerCount;
            string error = null;
            if (count < 2 || count > 20) error = "人数需在 2 到 20 之间";
            else if (!_modes.Has(config.ModeId)) error = "未知模式";
            else if (count < Room.HumanCount) error = "人数少于房间内玩家";
            else
            {
                for (int i = count; i < Room.Slots.Count; i++)
                    if (Room.Slots[i].Occupied) error = "有玩家坐在将被移除的座位上";
            }
            if (error == null)
            {
                try
                {
                    var probe = _modes.Create(config.Clone());
                    var v = probe.ValidateSetup(config, count);
                    if (!v.IsValid) error = v.Message;
                }
                catch (Exception ex) when (ex is ArgumentException || ex is KeyNotFoundException)
                {
                    error = ex.Message;
                }
            }
            if (error != null)
            {
                Send(peer, MessageType.Error, Payloads.Reason("bad_settings", error));
                return;
            }
            settings.RoomName = SanitizeName(settings.RoomName, Room.Settings.RoomName, 24);
            Room.Settings = settings;
            ResizeSlots(count);
            bool teamMode = IsTeamMode(settings.Config);
            foreach (var s in Room.Slots)
            {
                s.Ready = false;
                if (!teamMode) s.Team = Team.None;
            }
            if (teamMode)
            {
                // Re-balance: keep valid choices, place everyone else on the smaller side.
                foreach (var s in Room.Slots)
                    if (s.Occupied) s.Team = Team.None;
                foreach (var s in Room.Slots)
                    if (s.Occupied) s.Team = PickTeam(s.SlotId);
            }
            _roomDirty = true;
        }

        private void OnChangeTeam(Peer peer, int slotId, Team team)
        {
            if (Room.InGame || (team != Team.A && team != Team.B)) return;
            var slot = Room.GetSlot(slotId);
            if (slot == null || !slot.Occupied) return;
            var assignment = Room.Settings.Config.TeamAssignment;
            bool allowed = (assignment == TeamAssignmentMode.Manual && (slotId == peer.SlotId || IsHost(peer)))
                           || (assignment == TeamAssignmentMode.HostAssigned && IsHost(peer));
            if (!allowed) return;
            int teamSize = Room.Slots.Count / 2;
            int members = 0;
            foreach (var s in Room.Slots)
                if (s.Occupied && s.Team == team && s.SlotId != slotId) members++;
            if (members >= teamSize) return;
            slot.Team = team;
            _roomDirty = true;
        }

        private void OnChat(Peer peer, string text)
        {
            if (peer.SlotId < 0 && !peer.Spectator) return;
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0) return;
            if (text.Length > _options.MaxChatLength) text = text.Substring(0, _options.MaxChatLength);
            var payload = Payloads.Chat(peer.SlotId, text);
            foreach (var p in _peers)
                if (p.SlotId >= 0 || p.Spectator) Send(p, MessageType.Chat, payload);
        }

        // ================================================================== game

        private void OnStartGame(Peer peer)
        {
            if (!IsHost(peer) || Room.InGame) return;
            string error = null;
            foreach (var s in Room.Slots)
                if (s.Occupied && s.SlotId != Room.HostSlotId && !s.Ready) error = "还有玩家未准备";
            if (!Room.Settings.FillWithAI && Room.HumanCount < Room.Slots.Count) error = "座位未满且未开启 AI 补位";
            if (error != null)
            {
                Send(peer, MessageType.Error, Payloads.Reason("cannot_start", error));
                return;
            }
            var config = Room.Settings.Config.Clone();
            config.PlayerCount = Room.Slots.Count;
            var mode = _modes.Create(config);
            var setups = BuildSetups(config);
            int seed = _options.Seed ?? Environment.TickCount ^ Room.RoomId.GetHashCode();
            var engine = new GameEngine(_content, mode, config, setups, seed, Room.RoomId) { RethrowInternalErrors = false };
            _session = new GameSession(engine, _clock) { AIThinkDelayMs = _options.AIThinkDelayMs };
            _outbox.Clear();
            for (int i = 0; i < Room.Slots.Count; i++)
            {
                int viewer = i;
                _outbox[viewer] = new List<GameEvent>();
                _session.AddViewer(viewer, e => _outbox[viewer].Add(e));
            }
            _outbox[SnapshotBuilder.Spectator] = new List<GameEvent>();
            _session.AddViewer(SnapshotBuilder.Spectator, e => _outbox[SnapshotBuilder.Spectator].Add(e));
            _session.Start();
            foreach (var slot in Room.Slots)
                if (slot.Occupied && !slot.Connected) _session.SetConnected(slot.SlotId, false);
            foreach (var list in _outbox.Values) list.Clear(); // everything so far is in the snapshots
            Room.InGame = true;
            BroadcastRoom();
            foreach (var p in _peers)
                if (p.SlotId >= 0 || p.Spectator) SendSnapshot(p);
            Log?.Invoke("game started: " + config.ModeId + " with " + config.PlayerCount + " seats, seed " + seed);
        }

        private List<PlayerSetup> BuildSetups(GameModeConfig config)
        {
            var setups = new List<PlayerSetup>();
            int botNumber = 1;
            int teamA = 0, teamB = 0;
            foreach (var s in Room.Slots)
            {
                if (!s.Occupied) continue;
                if (s.Team == Team.A) teamA++;
                else if (s.Team == Team.B) teamB++;
            }
            foreach (var s in Room.Slots)
            {
                if (s.Occupied)
                {
                    setups.Add(new PlayerSetup(s.Nickname) { AvatarId = s.AvatarId, Team = s.Team });
                    continue;
                }
                var bot = new PlayerSetup("AI-" + botNumber++, true) { AvatarId = -1 };
                if (config.TeamAssignment != TeamAssignmentMode.Random && config.TeamSize > 0)
                {
                    bot.Team = teamA <= teamB ? Team.A : Team.B;
                    if (bot.Team == Team.A) teamA++;
                    else teamB++;
                }
                setups.Add(bot);
            }
            return setups;
        }

        private void OnCommand(Peer peer, NetworkMessage m)
        {
            if (_session == null || peer.SlotId < 0) return;
            var cmd = CommandCodec.Decode(m);
            cmd.PlayerId = peer.SlotId; // never trust the player id claimed by the client
            var result = _session.Submit(cmd);
            Send(peer, MessageType.CommandResult, CommandCodec.EncodeResult(cmd.SequenceNumber, result));
            if (!result.Accepted) Log?.Invoke("rejected " + cmd + " from slot " + peer.SlotId + ": " + result);
        }

        private void SendSnapshot(Peer peer)
        {
            int viewer = peer.Spectator ? SnapshotBuilder.Spectator : peer.SlotId;
            var snapshot = _session.GetSnapshot(viewer);
            int lastSeq = viewer >= 0 ? _session.Engine.GetLastSequence(viewer) : 0;
            Send(peer, MessageType.GameStateSync, SnapshotCodec.Encode(snapshot, lastSeq));
        }

        private void FlushEvents()
        {
            foreach (var kv in _outbox)
            {
                var events = kv.Value;
                if (events.Count == 0) continue;
                byte[] payload = null;
                foreach (var p in _peers)
                {
                    bool target = kv.Key == SnapshotBuilder.Spectator ? p.Spectator : p.SlotId == kv.Key;
                    if (!target) continue;
                    payload = payload ?? EventCodec.EncodeBatch(events);
                    Send(p, MessageType.GameEvents, payload);
                }
                // Viewers without a live connection get a fresh snapshot when they come back.
                events.Clear();
            }
        }

        private void CheckGameOver()
        {
            if (_session == null || !_session.Engine.IsGameOver || !Room.InGame) return;
            var result = _session.State.Result;
            foreach (var p in _peers)
                if (p.SlotId >= 0 || p.Spectator) Send(p, MessageType.GameFinished, Payloads.Result(result));
            Room.InGame = false;
            foreach (var s in Room.Slots)
            {
                s.Ready = false;
                if (s.Occupied && !s.Connected)
                {
                    s.Occupied = false;
                    s.Nickname = string.Empty;
                    _tokens[s.SlotId] = null;
                }
            }
            if (!HostPresent()) TransferHostToNextHuman();
            _session = null;
            _roomDirty = true;
            Log?.Invoke("game finished: " + result?.Describe());
        }

        // ================================================================== helpers

        private void BroadcastRoom()
        {
            _roomDirty = false;
            _pingDirty = false;
            _lastRoomBroadcastMs = Now;
            var payload = Payloads.RoomStatePayload(Room);
            foreach (var p in _peers)
                if (p.SlotId >= 0 || p.Spectator) Send(p, MessageType.RoomState, payload);
        }

        private void Send(Peer peer, MessageType type, byte[] payload)
        {
            peer.Connection.Send(new NetworkMessage(type, payload) { PlayerId = peer.SlotId, RoomId = Room.RoomId, Timestamp = Now });
        }

        private void Drop(Peer peer, string code, string message)
        {
            Send(peer, MessageType.Disconnect, Payloads.Reason(code, message));
            peer.Connection.Close(code);
        }

        private bool IsHost(Peer peer) => peer.SlotId >= 0 && peer.SlotId == Room.HostSlotId;

        private bool HostPresent()
        {
            var host = Room.GetSlot(Room.HostSlotId);
            return host != null && host.Occupied && host.Connected;
        }

        private void TransferHostToNextHuman()
        {
            foreach (var s in Room.Slots)
            {
                if (s.Occupied && s.Connected)
                {
                    Room.HostSlotId = s.SlotId;
                    return;
                }
            }
        }

        private int SpectatorCount()
        {
            int n = 0;
            foreach (var p in _peers)
                if (p.Spectator && p.Connection.IsConnected) n++;
            return n;
        }

        private bool IsTeamMode(GameModeConfig config)
        {
            try
            {
                return _modes.Create(config.Clone()) is TeamBattleMode;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private Team PickTeam(int slotId)
        {
            if (!IsTeamMode(Room.Settings.Config)) return Team.None;
            int a = 0, b = 0;
            foreach (var s in Room.Slots)
            {
                if (!s.Occupied || s.SlotId == slotId) continue;
                if (s.Team == Team.A) a++;
                else if (s.Team == Team.B) b++;
            }
            return a <= b ? Team.A : Team.B;
        }

        private void ResizeSlots(int count)
        {
            while (Room.Slots.Count < count)
            {
                Room.Slots.Add(new RoomSlot { SlotId = Room.Slots.Count });
                _tokens.Add(null);
            }
            while (Room.Slots.Count > count && !Room.Slots[Room.Slots.Count - 1].Occupied)
            {
                Room.Slots.RemoveAt(Room.Slots.Count - 1);
                _tokens.RemoveAt(_tokens.Count - 1);
            }
        }

        private string SanitizeName(string name, string fallback, int maxLength = 0)
        {
            if (maxLength <= 0) maxLength = _options.MaxNicknameLength;
            name = (name ?? string.Empty).Trim();
            var sb = new System.Text.StringBuilder();
            foreach (char c in name)
                if (!char.IsControl(c)) sb.Append(c);
            name = sb.ToString();
            if (name.Length > maxLength) name = name.Substring(0, maxLength);
            return name.Length == 0 ? fallback : name;
        }

        /// <summary>Random id from a cryptographic source (reconnect tokens must not be guessable).</summary>
        private string NewId(int length)
        {
            const string chars = "abcdefghijkmnpqrstuvwxyz23456789";
            var bytes = new byte[length];
            _tokenRandom.GetBytes(bytes);
            var sb = new System.Text.StringBuilder(length);
            for (int i = 0; i < length; i++) sb.Append(chars[bytes[i] % chars.Length]);
            return sb.ToString();
        }
    }
}
