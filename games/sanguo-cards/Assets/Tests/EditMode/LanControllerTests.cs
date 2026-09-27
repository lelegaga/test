using System;
using NUnit.Framework;
using Sanguo.GameModes;
using Sanguo.Network;
using Sanguo.Utils;

namespace Sanguo.Tests
{
    /// <summary>
    /// Host / join / leave orchestration used by the Unity NetworkManager, over the in-memory
    /// transport (no sockets): the host plays through its own server, a second device joins, and
    /// leaving or switching rooms tears down exactly what it should.
    /// </summary>
    public class LanControllerTests
    {
        private sealed class FakeLock : INetworkLock
        {
            public bool Held;
            public void Acquire() => Held = true;
            public void Release() => Held = false;
        }

        private InMemoryNetworkTransport _net;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            _net = new InMemoryNetworkTransport();
            _clock = new ManualClock { NowMs = 1000 };
        }

        private LanController Device(INetworkLock networkLock = null)
        {
            var rooms = new LanRoomService(_net, TestContent.Shared, GameModeRegistry.CreateDefault(), _clock, enableDiscovery: false);
            return new LanController(_net, _clock, rooms, networkLock);
        }

        private void PumpUntil(Func<bool> condition, string what, params LanController[] devices)
        {
            for (int i = 0; i < 2000; i++)
            {
                if (condition()) return;
                _clock.Advance(20);
                foreach (var d in devices) d.Update();
            }
            Assert.Fail("Timed out waiting for " + what);
        }

        private static RoomSettings FourPlayerRoom()
        {
            return new RoomSettings
            {
                RoomName = "测试房间",
                FillWithAI = true,
                Config = new GameModeConfig { ModeId = FreeForAllMode.Id, PlayerCount = 4, CharacterSelection = CharacterSelectionMode.None }
            };
        }

        [Test]
        public void HostingKeepsTheServerRunningAndJoinsItAsHost()
        {
            var networkLock = new FakeLock();
            var host = Device(networkLock);
            var client = host.HostRoom(FourPlayerRoom(), "房主", 2);
            PumpUntil(() => client.Status == ClientConnectionStatus.InLobby, "host joins own room", host);

            Assert.That(host.IsHosting, Is.True, "joining the own room must not stop the server");
            Assert.That(host.Client, Is.SameAs(client));
            Assert.That(client.IsHost, Is.True);
            Assert.That(client.Room.GetSlot(client.SlotId).Nickname, Is.EqualTo("房主"));
            Assert.That(client.Room.Slots, Has.Count.EqualTo(4));
            Assert.That(networkLock.Held, Is.True, "hosting holds the multicast lock");

            host.LeaveCurrent();
            Assert.That(host.IsHosting, Is.False);
            Assert.That(host.Client, Is.Null);
            Assert.That(networkLock.Held, Is.False);
        }

        [Test]
        public void SecondDeviceJoinsReadiesAndBothReceiveTheirOwnView()
        {
            var host = Device();
            var hostClient = host.HostRoom(FourPlayerRoom(), "房主", 0);
            PumpUntil(() => hostClient.Status == ClientConnectionStatus.InLobby, "host in lobby", host);

            var guest = Device();
            int port = host.Rooms.HostedServer.Port;
            var guestClient = guest.Join("192.168.1.20", port, "客人", 1, false);
            PumpUntil(() => guestClient.Status == ClientConnectionStatus.InLobby && hostClient.Room.HumanCount == 2, "guest in lobby", host, guest);
            Assert.That(guestClient.IsHost, Is.False);
            Assert.That(guestClient.SlotId, Is.Not.EqualTo(hostClient.SlotId));

            guestClient.SetReady(true);
            PumpUntil(() => hostClient.Room.GetSlot(guestClient.SlotId).Ready, "guest ready", host, guest);
            hostClient.StartGame();
            PumpUntil(() => hostClient.Game != null && guestClient.Game != null, "game snapshots", host, guest);

            Assert.That(hostClient.Game.ViewerId, Is.EqualTo(hostClient.SlotId));
            Assert.That(guestClient.Game.ViewerId, Is.EqualTo(guestClient.SlotId));
            Assert.That(guestClient.Game.Self.HandCards, Is.Not.Null.And.Not.Empty);
            Assert.That(guestClient.Game.GetPlayer(hostClient.SlotId).HandCards, Is.Null, "never another player's hand");
            Assert.That(hostClient.Game.Players, Has.Count.EqualTo(4), "empty seats filled with AI");
        }

        [Test]
        public void JoiningAnotherRoomStopsHostingTheOwnOne()
        {
            var other = Device();
            var otherClient = other.HostRoom(FourPlayerRoom(), "别人", 0);
            PumpUntil(() => otherClient.Status == ClientConnectionStatus.InLobby, "other room", other);
            int otherPort = other.Rooms.HostedServer.Port;

            // A second in-memory "device" must not overwrite the first listener's port.
            var me = new LanController(_net, _clock, new SinglePortRooms(_net, otherPort + 1));
            var mine = me.HostRoom(FourPlayerRoom(), "我", 0);
            PumpUntil(() => mine.Status == ClientConnectionStatus.InLobby, "own room", me);
            Assert.That(me.IsHosting, Is.True);

            var joined = me.Join("192.168.1.30", otherPort, "我", 0, false);
            Assert.That(me.IsHosting, Is.False, "switching rooms stops the own server");
            Assert.That(mine.Status, Is.EqualTo(ClientConnectionStatus.Disconnected));
            PumpUntil(() => joined.Status == ClientConnectionStatus.InLobby, "joined other room", other, me);
            Assert.That(otherClient.Room.HumanCount, Is.EqualTo(2));
        }

        [Test]
        public void InvalidInviteCodeReturnsNullAndKeepsState()
        {
            var device = Device();
            Assert.That(device.JoinByInviteCode("not-a-code", "玩家", 0), Is.Null);
            Assert.That(device.Client, Is.Null);
        }

        [Test]
        public void BrowsingHoldsTheLockUntilStopped()
        {
            var networkLock = new FakeLock();
            var device = Device(networkLock);
            device.StartBrowsing();
            Assert.That(networkLock.Held, Is.True);
            device.StopBrowsing();
            Assert.That(networkLock.Held, Is.False);
            device.Dispose();
        }

        /// <summary>Room service that hosts on a fixed in-memory port (lets two hosts share one fake network).</summary>
        private sealed class SinglePortRooms : IRoomService
        {
            private readonly INetworkTransport _net;
            private readonly int _port;
            private LanGameServer _server;

            public SinglePortRooms(INetworkTransport net, int port)
            {
                _net = net;
                _port = port;
            }

            public System.Collections.Generic.IReadOnlyList<RoomInfo> Rooms => Array.Empty<RoomInfo>();
            public IGameServer HostedServer => _server;
            public void StartBrowsing() { }
            public void StopBrowsing() { }

            public IGameServer HostRoom(RoomSettings settings, ServerOptions options = null)
            {
                StopHosting();
                _server = new LanGameServer(_net, TestContent.Shared, GameModeRegistry.CreateDefault(), new ManualClock(), settings, options);
                _server.Start(_port);
                return _server;
            }

            public void StopHosting()
            {
                _server?.Stop();
                _server = null;
            }

            public void Update() => _server?.Poll();
            public bool TryResolveInviteCode(string code, out string host, out int port)
            {
                host = null;
                port = 0;
                return false;
            }

            public void Dispose() => StopHosting();
        }
    }
}
