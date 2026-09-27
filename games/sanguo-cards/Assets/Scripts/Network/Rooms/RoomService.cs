using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Sanguo.Data;
using Sanguo.GameModes;
using Sanguo.Utils;

namespace Sanguo.Network
{
    /// <summary>
    /// Finding and creating rooms. The LAN implementation browses UDP announcements and hosts rooms
    /// on this device; an internet implementation would talk to a lobby server instead.
    /// </summary>
    public interface IRoomService : IDisposable
    {
        IReadOnlyList<RoomInfo> Rooms { get; }
        IGameServer HostedServer { get; }

        void StartBrowsing();
        void StopBrowsing();

        /// <summary>Creates a room hosted on this device and starts announcing it.</summary>
        IGameServer HostRoom(RoomSettings settings, ServerOptions options = null);

        void StopHosting();

        /// <summary>Polls discovery and the hosted server. Call every frame.</summary>
        void Update();

        bool TryResolveInviteCode(string code, out string host, out int port);
    }

    public sealed class LanRoomService : IRoomService
    {
        private readonly INetworkTransport _transport;
        private readonly GameContent _content;
        private readonly GameModeRegistry _modes;
        private readonly IClock _clock;
        private readonly int _discoveryPort;
        private LanRoomBrowser _browser;
        private LanRoomAnnouncer _announcer;
        private LanGameServer _server;
        private string _address = string.Empty;
        private IReadOnlyList<RoomInfo> _rooms = Array.Empty<RoomInfo>();

        public LanRoomService(INetworkTransport transport, GameContent content, GameModeRegistry modes, IClock clock,
            int discoveryPort = ProtocolInfo.DiscoveryPort)
        {
            _transport = transport;
            _content = content;
            _modes = modes;
            _clock = clock;
            _discoveryPort = discoveryPort;
        }

        public IReadOnlyList<RoomInfo> Rooms => _rooms;
        public IGameServer HostedServer => _server;

        /// <summary>Last discovery error (e.g. port in use); discovery is best effort.</summary>
        public string DiscoveryError { get; private set; }

        public void StartBrowsing()
        {
            if (_browser != null) return;
            try
            {
                _browser = new LanRoomBrowser(_discoveryPort);
            }
            catch (SocketException ex)
            {
                DiscoveryError = ex.Message;
            }
        }

        public void StopBrowsing()
        {
            _browser?.Dispose();
            _browser = null;
            _rooms = Array.Empty<RoomInfo>();
        }

        public IGameServer HostRoom(RoomSettings settings, ServerOptions options = null)
        {
            StopHosting();
            _server = new LanGameServer(_transport, _content, _modes, _clock, settings, options);
            SocketException last = null;
            for (int offset = 0; offset < 32; offset++)
            {
                try
                {
                    _server.Start(ProtocolInfo.DefaultGamePort + offset);
                    last = null;
                    break;
                }
                catch (SocketException ex)
                {
                    last = ex;
                }
            }
            if (last != null) throw last;
            var ip = LocalNetwork.GetLanIPv4();
            _address = ip.ToString();
            _server.SetInviteCode(InviteCode.Encode(ip, _server.Port));
            try
            {
                _announcer = new LanRoomAnnouncer(_discoveryPort);
            }
            catch (SocketException ex)
            {
                DiscoveryError = ex.Message;
            }
            return _server;
        }

        public void StopHosting()
        {
            _server?.Stop();
            _server = null;
            _announcer?.Dispose();
            _announcer = null;
        }

        public void Update()
        {
            long now = _clock.NowMs;
            if (_server != null)
            {
                _server.Poll();
                _announcer?.Update(now, _server.BuildRoomInfo(_address));
            }
            if (_browser != null) _rooms = _browser.Update(now);
        }

        public bool TryResolveInviteCode(string code, out string host, out int port)
        {
            host = null;
            if (!InviteCode.TryDecode(code, out var address, out port)) return false;
            host = address.ToString();
            return true;
        }

        public void Dispose()
        {
            StopHosting();
            StopBrowsing();
        }
    }
}
