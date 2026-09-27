using System;
using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.GameModes;
using Sanguo.Utils;

namespace Sanguo.Network
{
    public enum ClientConnectionStatus : byte
    {
        Disconnected = 0,
        Connecting = 1,
        /// <summary>Connected and joined a room (lobby).</summary>
        InLobby = 2,
        InGame = 3,
        /// <summary>Connection lost; trying to get back into the same seat.</summary>
        Reconnecting = 4
    }

    /// <summary>
    /// Client side of the protocol. Owns the presentation replica (<see cref="Game"/>), which is
    /// only ever changed by server snapshots and events; the UI reads it and sends intents through
    /// <see cref="Send(GameCommand)"/>. Call <see cref="Poll"/> every frame from the main thread.
    /// </summary>
    public sealed class GameClient
    {
        private readonly INetworkTransport _transport;
        private readonly IClock _clock;
        private IConnection _connection;
        private string _host;
        private int _port;
        private string _nickname = string.Empty;
        private int _avatarId;
        private bool _spectator;
        private int _lastCommandSequence;
        private int _messageSequence;
        private long _lastPingSentMs;
        private long _lastReceivedMs;
        private long _nextReconnectMs;
        private int _reconnectDelayMs;
        private bool _resyncRequested;

        public GameClient(INetworkTransport transport, IClock clock)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _clock = clock ?? new SystemClock();
        }

        public ClientConnectionStatus Status { get; private set; }
        public RoomState Room { get; private set; }
        public int SlotId { get; private set; } = -1;
        public bool IsSpectator => _spectator;
        public string ReconnectToken { get; private set; }
        public ClientGameState Game { get; private set; }
        public GameResult LastResult { get; private set; }
        public int PingMs { get; private set; }
        public string LastError { get; private set; }
        public bool IsHost => Room != null && SlotId >= 0 && Room.HostSlotId == SlotId;

        public int PingIntervalMs { get; set; } = 2000;
        public int ConnectionTimeoutMs { get; set; } = 10000;
        public int ConnectTimeoutMs { get; set; } = 4000;
        /// <summary>Give up reconnecting after this long (the server will have handed the seat to AI).</summary>
        public int MaxReconnectMs { get; set; } = 120000;

        /// <summary>Client-side event hub for UI, animation, audio and logs (projected events only).</summary>
        public GameEventBus Events { get; } = new GameEventBus();

        public event Action<RoomState> RoomChanged;
        public event Action<string, string> JoinRejected;
        public event Action<ClientGameState> GameStateReplaced;
        public event Action<GameResult> GameFinished;
        public event Action<int, CommandResult> CommandRejected;
        public event Action<string, string> ServerMessage;
        public event Action<string> Disconnected;
        public event Action Reconnected;
        public event Action<int, string> ChatReceived;

        private long Now => _clock.NowMs;
        private long _reconnectStartedMs;

        // ================================================================== lobby API

        /// <summary>Connects, handshakes and asks to join the room. Result arrives as RoomChanged / JoinRejected.</summary>
        public bool Connect(string host, int port, string nickname, int avatarId, bool spectator = false)
        {
            _host = host;
            _port = port;
            _nickname = nickname ?? string.Empty;
            _avatarId = avatarId;
            _spectator = spectator;
            Status = ClientConnectionStatus.Connecting;
            if (!OpenConnection()) return false;
            SendRaw(MessageType.JoinRoom, Payloads.Join(new JoinPayload { Nickname = _nickname, AvatarId = _avatarId, Spectator = spectator }));
            return true;
        }

        private bool OpenConnection()
        {
            _connection = _transport.Connect(_host, _port, ConnectTimeoutMs, out string error);
            if (_connection == null)
            {
                LastError = error;
                return false;
            }
            _lastReceivedMs = Now;
            _lastPingSentMs = Now;
            SendRaw(MessageType.Hello, Payloads.Hello(new HelloPayload { Nickname = _nickname, AvatarId = _avatarId }));
            return true;
        }

        public void SetReady(bool ready) => SendRaw(MessageType.PlayerReady, Payloads.Bool(ready));
        public void Kick(int slotId) => SendRaw(MessageType.KickPlayer, Payloads.Int(slotId));
        public void TransferHost(int slotId) => SendRaw(MessageType.TransferHost, Payloads.Int(slotId));
        public void UpdateSettings(RoomSettings settings) => SendRaw(MessageType.UpdateRoomSettings, Payloads.Settings(settings));
        public void ChangeTeam(int slotId, Team team) => SendRaw(MessageType.ChangeTeam, Payloads.ChangeTeam(slotId, team));
        public void StartGame() => SendRaw(MessageType.StartGame, null);
        public void SendChat(string text) => SendRaw(MessageType.Chat, Payloads.Chat(SlotId, text));
        public void SetAutoPlay(bool enabled) => SendRaw(MessageType.SetAutoPlay, Payloads.Bool(enabled));

        /// <summary>Leaves the room (during a game the AI takes over the seat).</summary>
        public void Leave()
        {
            SendRaw(MessageType.LeaveRoom, null);
            CloseLocal("left");
        }

        /// <summary>Sends a game intent. RequestId must name the open request being answered.</summary>
        public int Send(GameCommand command)
        {
            command.SequenceNumber = ++_lastCommandSequence;
            command.PlayerId = SlotId;
            var msg = new NetworkMessage(CommandCodec.MessageTypeOf(command), CommandCodec.Encode(command))
            {
                PlayerId = SlotId,
                RoomId = Room?.RoomId ?? string.Empty,
                SequenceId = command.SequenceNumber,
                Timestamp = Now
            };
            _connection?.Send(msg);
            return command.SequenceNumber;
        }

        /// <summary>Test hook: the underlying connection.</summary>
        public IConnection Connection => _connection;

        // ================================================================== polling

        public void Poll()
        {
            if (_connection != null)
            {
                for (int n = 0; n < 512 && _connection != null && _connection.TryReceive(out var m); n++)
                {
                    _lastReceivedMs = Now;
                    try
                    {
                        Handle(m);
                    }
                    catch (ProtocolException ex)
                    {
                        LastError = ex.Message;
                        _connection.Close("protocol error");
                    }
                }
            }

            if (_connection != null && _connection.IsConnected)
            {
                if (Now - _lastPingSentMs >= PingIntervalMs)
                {
                    _lastPingSentMs = Now;
                    SendRaw(MessageType.Ping, Payloads.Long(Now));
                }
                if (Now - _lastReceivedMs > ConnectionTimeoutMs) _connection.Close("timeout");
            }

            if (_connection != null && !_connection.IsConnected) OnConnectionLost(_connection.CloseReason ?? "closed");
            if (Status == ClientConnectionStatus.Reconnecting && Now >= _nextReconnectMs) TryReconnect();
        }

        private void Handle(NetworkMessage m)
        {
            switch (m.Type)
            {
                case MessageType.Welcome:
                    break;
                case MessageType.Ping:
                    SendRaw(MessageType.Pong, m.Payload);
                    break;
                case MessageType.Pong:
                    PingMs = (int)Math.Max(0, Now - Payloads.ReadLong(m));
                    break;
                case MessageType.JoinAccepted:
                {
                    Room = Payloads.ReadJoinAccepted(m, out int slot, out string token);
                    SlotId = slot;
                    ReconnectToken = token;
                    Status = Room.InGame ? ClientConnectionStatus.InGame : ClientConnectionStatus.InLobby;
                    RoomChanged?.Invoke(Room);
                    break;
                }
                case MessageType.JoinRejected:
                {
                    string code = Payloads.ReadReason(m, out string message);
                    LastError = message;
                    JoinRejected?.Invoke(code, message);
                    CloseLocal("join rejected");
                    break;
                }
                case MessageType.ReconnectAccepted:
                {
                    Room = Payloads.ReadJoinAccepted(m, out int slot, out string token);
                    SlotId = slot;
                    ReconnectToken = token;
                    Status = Room.InGame ? ClientConnectionStatus.InGame : ClientConnectionStatus.InLobby;
                    _reconnectDelayMs = 0;
                    RoomChanged?.Invoke(Room);
                    Reconnected?.Invoke();
                    break;
                }
                case MessageType.ReconnectRejected:
                {
                    Payloads.ReadReason(m, out string message);
                    LastError = message;
                    CloseLocal("reconnect rejected");
                    Disconnected?.Invoke(message);
                    break;
                }
                case MessageType.RoomState:
                    Room = Payloads.ReadRoomState(m);
                    if (Status == ClientConnectionStatus.InLobby && Room.InGame) Status = ClientConnectionStatus.InGame;
                    else if (Status == ClientConnectionStatus.InGame && !Room.InGame && (Game == null || Game.IsGameOver)) Status = ClientConnectionStatus.InLobby;
                    RoomChanged?.Invoke(Room);
                    break;
                case MessageType.GameStateSync:
                {
                    Game = SnapshotCodec.Decode(m.Payload, out int lastSeq);
                    if (!_spectator) _lastCommandSequence = Math.Max(_lastCommandSequence, lastSeq);
                    _resyncRequested = false;
                    Status = ClientConnectionStatus.InGame;
                    LastResult = null;
                    GameStateReplaced?.Invoke(Game);
                    break;
                }
                case MessageType.GameEvents:
                    OnEvents(EventCodec.DecodeBatch(m.Payload));
                    break;
                case MessageType.CommandResult:
                {
                    var result = CommandCodec.DecodeResult(m, out int seq);
                    if (!result.Accepted) CommandRejected?.Invoke(seq, result);
                    break;
                }
                case MessageType.GameFinished:
                    LastResult = Payloads.ReadResult(m);
                    GameFinished?.Invoke(LastResult);
                    break;
                case MessageType.Chat:
                {
                    string text = Payloads.ReadChat(m, out int slot);
                    ChatReceived?.Invoke(slot, text);
                    break;
                }
                case MessageType.Error:
                {
                    string code = Payloads.ReadReason(m, out string message);
                    LastError = message;
                    ServerMessage?.Invoke(code, message);
                    break;
                }
                case MessageType.Disconnect:
                {
                    string code = Payloads.ReadReason(m, out string message);
                    LastError = message;
                    ServerMessage?.Invoke(code, message);
                    ReconnectToken = code == "kicked" || code == "server_stopped" ? null : ReconnectToken;
                    break;
                }
            }
        }

        private void OnEvents(List<GameEvent> events)
        {
            if (Game == null) return;
            foreach (var e in events)
            {
                if (_resyncRequested) return;
                if (!Game.Apply(e))
                {
                    // Gap or inconsistency: ask for a fresh snapshot and ignore events until it arrives.
                    _resyncRequested = true;
                    SendRaw(MessageType.RequestResync, null);
                    return;
                }
                Events.Publish(e);
            }
        }

        // ================================================================== reconnection

        private void OnConnectionLost(string reason)
        {
            _connection = null;
            bool canResume = ReconnectToken != null && SlotId >= 0 && Room != null;
            if (canResume && Status != ClientConnectionStatus.Disconnected)
            {
                if (Status != ClientConnectionStatus.Reconnecting)
                {
                    Status = ClientConnectionStatus.Reconnecting;
                    _reconnectStartedMs = Now;
                    _reconnectDelayMs = 500;
                    _nextReconnectMs = Now;
                    Disconnected?.Invoke(reason);
                }
                return;
            }
            Status = ClientConnectionStatus.Disconnected;
            Disconnected?.Invoke(reason);
        }

        private void TryReconnect()
        {
            if (Now - _reconnectStartedMs > MaxReconnectMs)
            {
                Status = ClientConnectionStatus.Disconnected;
                LastError = "reconnect timed out";
                return;
            }
            if (OpenConnection())
            {
                SendRaw(MessageType.Reconnect, Payloads.Reconnect(SlotId, ReconnectToken));
            }
            _reconnectDelayMs = Math.Min(Math.Max(500, _reconnectDelayMs * 2), 8000);
            _nextReconnectMs = Now + _reconnectDelayMs;
        }

        /// <summary>Forces a reconnection attempt now (e.g. when the app returns to the foreground).</summary>
        public void ReconnectNow()
        {
            if (_connection != null && _connection.IsConnected) return;
            if (ReconnectToken == null || SlotId < 0) return;
            if (Status != ClientConnectionStatus.Reconnecting)
            {
                Status = ClientConnectionStatus.Reconnecting;
                _reconnectStartedMs = Now;
            }
            _nextReconnectMs = Now;
            TryReconnect();
        }

        private void CloseLocal(string reason)
        {
            _connection?.Close(reason);
            _connection = null;
            Status = ClientConnectionStatus.Disconnected;
        }

        private void SendRaw(MessageType type, byte[] payload)
        {
            if (_connection == null) return;
            _connection.Send(new NetworkMessage(type, payload)
            {
                PlayerId = SlotId,
                RoomId = Room?.RoomId ?? string.Empty,
                SequenceId = ++_messageSequence,
                Timestamp = Now
            });
        }
    }
}
