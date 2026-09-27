using System;
using Sanguo.Utils;

namespace Sanguo.Network
{
    /// <summary>Platform hook that keeps LAN broadcast reception alive (Android multicast lock).</summary>
    public interface INetworkLock
    {
        void Acquire();
        void Release();
    }

    /// <summary>
    /// This device's LAN play state: browsing rooms, hosting one, and the single <see cref="GameClient"/>
    /// it plays through. The host joins its own server over loopback, so hosting and joining share
    /// one client code path. Engine-free, so the host/join/leave rules are unit tested; the Unity
    /// NetworkManager only forwards frames and app lifecycle events.
    /// </summary>
    public sealed class LanController : IDisposable
    {
        private readonly INetworkTransport _transport;
        private readonly IClock _clock;
        private readonly IRoomService _rooms;
        private readonly INetworkLock _lock;
        private bool _browsing;

        public LanController(INetworkTransport transport, IClock clock, IRoomService rooms, INetworkLock networkLock = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            _lock = networkLock;
        }

        public IRoomService Rooms => _rooms;
        public GameClient Client { get; private set; }
        public bool IsHosting => _rooms.HostedServer != null;
        public bool IsBrowsing => _browsing;

        /// <summary>Loopback address the host's own client connects to.</summary>
        public string LoopbackHost { get; set; } = "127.0.0.1";

        public event Action<GameClient> ClientCreated;

        public void StartBrowsing()
        {
            _browsing = true;
            UpdateLock();
            _rooms.StartBrowsing();
        }

        public void StopBrowsing()
        {
            _browsing = false;
            _rooms.StopBrowsing();
            UpdateLock();
        }

        /// <summary>Leaves any current room, hosts a new one on this device and joins it as the host.</summary>
        public GameClient HostRoom(RoomSettings settings, string nickname, int avatarId)
        {
            LeaveCurrent();
            var server = _rooms.HostRoom(settings, new ServerOptions { ServerName = nickname });
            UpdateLock();
            return Connect(LoopbackHost, server.Port, nickname, avatarId, false);
        }

        /// <summary>Leaves any current room (stopping a hosted one) and joins another device's room.</summary>
        public GameClient Join(string host, int port, string nickname, int avatarId, bool spectator)
        {
            LeaveCurrent();
            return Connect(host, port, nickname, avatarId, spectator);
        }

        /// <summary>Returns null when the code is not a valid invite code.</summary>
        public GameClient JoinByInviteCode(string code, string nickname, int avatarId)
        {
            if (!_rooms.TryResolveInviteCode(code, out string host, out int port)) return null;
            return Join(host, port, nickname, avatarId, false);
        }

        public void LeaveCurrent()
        {
            if (Client != null && Client.Status != ClientConnectionStatus.Disconnected) Client.Leave();
            Client = null;
            if (IsHosting) _rooms.StopHosting();
            UpdateLock();
        }

        /// <summary>Call every frame: discovery, the hosted server and the client.</summary>
        public void Update()
        {
            _rooms.Update();
            Client?.Poll();
        }

        /// <summary>Mobile OSes may kill sockets in the background; reconnect when the app returns.</summary>
        public void OnResume()
        {
            if (Client != null && Client.Status != ClientConnectionStatus.Disconnected) Client.ReconnectNow();
        }

        private GameClient Connect(string host, int port, string nickname, int avatarId, bool spectator)
        {
            Client = new GameClient(_transport, _clock);
            ClientCreated?.Invoke(Client);
            Client.Connect(host, port, nickname, avatarId, spectator);
            return Client;
        }

        private void UpdateLock()
        {
            if (_lock == null) return;
            if (_browsing || IsHosting) _lock.Acquire();
            else _lock.Release();
        }

        public void Dispose()
        {
            LeaveCurrent();
            _browsing = false;
            _rooms.Dispose();
            _lock?.Release();
        }
    }
}
