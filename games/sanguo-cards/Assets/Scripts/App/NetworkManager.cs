using System;
using Sanguo.Network;
using UnityEngine;

namespace Sanguo.App
{
    /// <summary>
    /// Unity-side owner of LAN networking: hosting (server + room announcements), browsing, and this
    /// device's <see cref="GameClient"/>. The host device plays through the same client/protocol path
    /// as everyone else (connected to its own server over loopback), so there is only one code path.
    /// </summary>
    public sealed class NetworkManager : MonoBehaviour
    {
        private readonly AndroidNetworkLock _lock = new AndroidNetworkLock();
        private LanRoomService _rooms;
        private INetworkTransport _transport;
        private UnityClock _clock;

        public LanRoomService Rooms => _rooms;
        public GameClient Client { get; private set; }
        public bool IsHosting => _rooms?.HostedServer != null;

        public event Action<GameClient> ClientCreated;

        private void Awake()
        {
            _transport = new TcpNetworkTransport();
            _clock = new UnityClock();
        }

        private void EnsureRooms()
        {
            if (_rooms != null) return;
            var gm = GameManager.Instance;
            _rooms = new LanRoomService(_transport, gm.Content, gm.Modes, _clock);
        }

        public void StartBrowsing()
        {
            EnsureRooms();
            _lock.Acquire();
            _rooms.StartBrowsing();
        }

        public void StopBrowsing()
        {
            _rooms?.StopBrowsing();
            if (!IsHosting) _lock.Release();
        }

        /// <summary>Creates a room on this device and joins it as the host.</summary>
        public GameClient HostRoom(RoomSettings settings, string nickname, int avatarId)
        {
            EnsureRooms();
            _lock.Acquire();
            var server = _rooms.HostRoom(settings, new ServerOptions { ServerName = nickname });
            return Join("127.0.0.1", server.Port, nickname, avatarId, false);
        }

        public GameClient Join(string host, int port, string nickname, int avatarId, bool spectator)
        {
            LeaveCurrent();
            Client = new GameClient(_transport, _clock);
            ClientCreated?.Invoke(Client);
            if (!Client.Connect(host, port, nickname, avatarId, spectator))
                Debug.LogWarning("[Sanguo] Connect failed: " + Client.LastError);
            return Client;
        }

        public bool JoinByInviteCode(string code, string nickname, int avatarId)
        {
            EnsureRooms();
            if (!_rooms.TryResolveInviteCode(code, out string host, out int port)) return false;
            Join(host, port, nickname, avatarId, false);
            return true;
        }

        public void LeaveCurrent()
        {
            if (Client != null && Client.Status != ClientConnectionStatus.Disconnected) Client.Leave();
            Client = null;
            if (IsHosting)
            {
                _rooms.StopHosting();
                _lock.Release();
            }
        }

        private void Update()
        {
            _rooms?.Update();
            Client?.Poll();
        }

        private void OnApplicationPause(bool paused)
        {
            // Mobile OSes may kill sockets while the app is in the background; reconnect on return.
            if (!paused && Client != null && Client.Status != ClientConnectionStatus.Disconnected) Client.ReconnectNow();
        }

        private void OnDestroy()
        {
            LeaveCurrent();
            _rooms?.Dispose();
            _lock.Release();
        }
    }
}
