using System.Collections.Generic;
using System.Linq;
using System.Net;
using NUnit.Framework;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.GameModes;
using Sanguo.Network;

namespace Sanguo.Tests
{
    public class LobbyTests
    {
        private static RoomSettings Settings(string mode = FreeForAllMode.Id, int players = 4)
        {
            return new RoomSettings
            {
                RoomName = "测试",
                Config = new GameModeConfig { ModeId = mode, PlayerCount = players, CharacterSelection = CharacterSelectionMode.Random, MaxRounds = 60 }
            };
        }

        [Test]
        public void PlayersJoinAndSeeEachOther()
        {
            using (var h = new NetHarness(new InMemoryNetworkTransport(), Settings()))
            {
                var host = h.AddClient("房主");
                h.PumpUntil(() => host.Status == ClientConnectionStatus.InLobby, "host joined");
                var b = h.AddClient("小明");
                var c = h.AddClient("小红");
                h.PumpUntil(() => host.Room.HumanCount == 3 && c.Room != null && c.Room.HumanCount == 3, "all joined");
                Assert.That(host.SlotId, Is.EqualTo(0));
                Assert.That(host.IsHost, Is.True);
                Assert.That(b.SlotId, Is.EqualTo(1));
                Assert.That(c.Room.Slots.Select(s => s.Nickname).Take(3), Is.EqualTo(new[] { "房主", "小明", "小红" }));
                Assert.That(b.ReconnectToken, Is.Not.Null.And.Not.EqualTo(c.ReconnectToken));
            }
        }

        [Test]
        public void RoomRejectsWhenFullOrStarted()
        {
            using (var h = new NetHarness(new InMemoryNetworkTransport(), Settings(players: 2)))
            {
                var a = h.AddClient("A");
                var b = h.AddClient("B");
                h.PumpUntil(() => a.Room != null && a.Room.HumanCount == 2, "two joined");
                string rejected = null;
                var c = h.AddClient("C");
                c.JoinRejected += (code, msg) => rejected = code;
                h.PumpUntil(() => rejected != null, "rejection");
                Assert.That(rejected, Is.EqualTo("room_full"));
            }
        }

        [Test]
        public void OnlyHostControlsTheRoom()
        {
            using (var h = new NetHarness(new InMemoryNetworkTransport(), Settings()))
            {
                var host = h.AddClient("Host");
                h.PumpUntil(() => host.Status == ClientConnectionStatus.InLobby, "host");
                var guest = h.AddClient("Guest");
                h.PumpUntil(() => guest.Status == ClientConnectionStatus.InLobby, "guest");

                var hacked = Settings(players: 8);
                guest.UpdateSettings(hacked);
                guest.StartGame();
                guest.Kick(0);
                h.Pump(20);
                Assert.That(h.Server.Room.Slots.Count, Is.EqualTo(4));
                Assert.That(h.Server.Room.InGame, Is.False);
                Assert.That(host.Status, Is.EqualTo(ClientConnectionStatus.InLobby));

                host.UpdateSettings(Settings(IdentityMode.Id, 6));
                h.PumpUntil(() => guest.Room.Slots.Count == 6 && guest.Room.Settings.Config.ModeId == IdentityMode.Id, "settings applied");

                host.TransferHost(guest.SlotId);
                h.PumpUntil(() => guest.IsHost, "host transferred");
                Assert.That(host.IsHost, Is.False);
            }
        }

        [Test]
        public void HostCanKickAPlayer()
        {
            using (var h = new NetHarness(new InMemoryNetworkTransport(), Settings()))
            {
                var host = h.AddClient("Host");
                h.PumpUntil(() => host.Status == ClientConnectionStatus.InLobby, "host");
                var guest = h.AddClient("Guest");
                h.PumpUntil(() => host.Room.HumanCount == 2, "guest joined");
                string code = null;
                guest.ServerMessage += (c, m) => code = c;
                host.Kick(guest.SlotId);
                h.PumpUntil(() => host.Room.HumanCount == 1 && guest.Status == ClientConnectionStatus.Disconnected, "kicked");
                Assert.That(code, Is.EqualTo("kicked"));
                Assert.That(guest.ReconnectToken, Is.Null);
            }
        }

        [Test]
        public void StartRequiresEveryoneReadyAndFillsWithAI()
        {
            using (var h = new NetHarness(new InMemoryNetworkTransport(), Settings(players: 5)))
            {
                var host = h.AddClient("Host");
                h.PumpUntil(() => host.Status == ClientConnectionStatus.InLobby, "host");
                var guest = h.AddClient("Guest");
                h.PumpUntil(() => host.Room.HumanCount == 2, "guest");
                string error = null;
                host.ServerMessage += (c, m) => error = c;
                host.StartGame();
                h.PumpUntil(() => error != null, "not ready error");
                Assert.That(error, Is.EqualTo("cannot_start"));
                guest.SetReady(true);
                h.PumpUntil(() => host.Room.Slots[1].Ready, "ready");
                host.StartGame();
                h.PumpUntil(() => host.Game != null && guest.Game != null, "game started");
                Assert.That(h.Server.Session.State.PlayerCount, Is.EqualTo(5));
                Assert.That(h.Server.Session.State.Players.Count(p => p.IsBot), Is.EqualTo(3));
                Assert.That(guest.Game.ViewerId, Is.EqualTo(guest.SlotId));
                Assert.That(guest.Game.Players.Where(p => p.PlayerId != guest.SlotId).All(p => p.HandCards == null), Is.True);
                Assert.That(guest.Game.Self.HandCards.Count, Is.EqualTo(guest.Game.Self.HandCount));
            }
        }

        [Test]
        public void TeamModeLobbyBalancesTeamsAndHonoursManualChoice()
        {
            var settings = Settings(Team3v3Mode.Id, 6);
            settings.Config.TeamAssignment = TeamAssignmentMode.Manual;
            using (var h = new NetHarness(new InMemoryNetworkTransport(), settings))
            {
                var a = h.AddClient("A");
                h.PumpUntil(() => a.Status == ClientConnectionStatus.InLobby, "a");
                var b = h.AddClient("B");
                h.PumpUntil(() => a.Room.HumanCount == 2, "b");
                Assert.That(a.Room.Slots[0].Team, Is.EqualTo(Team.A));
                Assert.That(a.Room.Slots[1].Team, Is.EqualTo(Team.B));
                b.ChangeTeam(b.SlotId, Team.A);
                h.PumpUntil(() => a.Room.Slots[1].Team == Team.A, "team change");
                b.SetReady(true);
                h.PumpUntil(() => a.Room.Slots[1].Ready, "ready");
                a.StartGame();
                h.PumpUntil(() => b.Game != null, "started");
                var state = h.Server.Session.State;
                Assert.That(state.GetPlayer(0).Team, Is.EqualTo(Team.A));
                Assert.That(state.GetPlayer(1).Team, Is.EqualTo(Team.A));
                Assert.That(state.Players.Count(p => p.Team == Team.A), Is.EqualTo(3));
            }
        }
    }

    public class NetworkGameTests
    {
        private static RoomSettings Settings(string mode, int players)
        {
            return new RoomSettings
            {
                Config = new GameModeConfig
                {
                    ModeId = mode,
                    PlayerCount = players,
                    CharacterSelection = CharacterSelectionMode.Random,
                    MaxRounds = 60,
                    PlayTimeoutMs = 60000,
                    ResponseTimeoutMs = 60000
                }
            };
        }

        /// <summary>Two remote humans (TCP) + AI seats play a whole identity game.</summary>
        [Test]
        public void TwoDevicesFinishAGameOverTcp()
        {
            var sniffA = new SniffingTransport(new TcpNetworkTransport());
            using (var h = new NetHarness(new TcpNetworkTransport(), Settings(IdentityMode.Id, 5)))
            {
                var a = h.AddClient("甲", sniffA);
                h.PumpUntil(() => a.Status == ClientConnectionStatus.InLobby, "a joined");
                var b = h.AddClient("乙", new TcpNetworkTransport());
                h.PumpUntil(() => a.Room.HumanCount == 2, "b joined");
                b.SetReady(true);
                h.PumpUntil(() => a.Room.Slots[1].Ready, "b ready");
                a.StartGame();
                h.PumpUntil(() => a.Game != null && b.Game != null, "game started");

                var botA = new NaiveClientBot(a, 1);
                var botB = new NaiveClientBot(b, 2);
                h.PumpUntil(() =>
                {
                    botA.Act();
                    botB.Act();
                    return a.Game.IsGameOver && b.Game.IsGameOver && a.LastResult != null && b.LastResult != null;
                }, "game over", 60000);

                Assert.That(botA.CommandsSent + botB.CommandsSent, Is.GreaterThan(5), "humans actually played");
                Assert.That(a.Game.Result.Describe(), Is.EqualTo(b.Game.Result.Describe()));
                Assert.That(a.LastResult.Describe(), Is.EqualTo(a.Game.Result.Describe()));
                Assert.That(h.Server.Room.InGame, Is.False, "room returns to the lobby");

                // Wire-level secrecy: nothing player A received ever described another player's hand card.
                AssertNoForeignHandCards(sniffA.Received, a.SlotId);
            }
        }

        private static void AssertNoForeignHandCards(List<NetworkMessage> received, int viewer)
        {
            List<NetworkMessage> copy;
            lock (received) copy = new List<NetworkMessage>(received);
            int checkedEvents = 0;
            foreach (var m in copy)
            {
                if (m.Type == MessageType.GameStateSync)
                {
                    var snap = SnapshotCodec.Decode(m.Payload, out _);
                    foreach (var p in snap.Players)
                        if (p.PlayerId != viewer) Assert.That(p.HandCards, Is.Null);
                }
                if (m.Type != MessageType.GameEvents) continue;
                foreach (var e in EventCodec.DecodeBatch(m.Payload))
                {
                    checkedEvents++;
                    if (e is CardMoveEvent mv && mv.Cards != null && !mv.IsPublic)
                        Assert.That(mv.KnownTo, Does.Contain(viewer), "received hidden cards of a move it may not see: " + mv);
                    if (e is RequestOpenedEvent ro && ro.Info.PlayerId != viewer)
                    {
                        Assert.That(ro.Info.HasPrivateDetails, Is.False);
                        Assert.That(ro.Info.PlayHints, Is.Null);
                    }
                }
            }
            Assert.That(checkedEvents, Is.GreaterThan(100));
        }

        [Test]
        public void ForgedPlayerIdIsIgnored()
        {
            using (var h = new NetHarness(new InMemoryNetworkTransport(), Settings(FreeForAllMode.Id, 3)))
            {
                var a = h.AddClient("A");
                h.PumpUntil(() => a.Status == ClientConnectionStatus.InLobby, "a");
                var b = h.AddClient("B");
                h.PumpUntil(() => a.Room.HumanCount == 2, "b");
                b.SetReady(true);
                h.PumpUntil(() => a.Room.Slots[1].Ready, "ready");
                a.StartGame();
                h.PumpUntil(() => a.Game != null && b.Game != null, "started");
                var engine = h.Server.Session.Engine;
                // AI seats have played up to a human decision; someone is being asked something.
                var req = engine.OpenRequests[0];
                int victimSeat = req.PlayerId;
                var attacker = victimSeat == a.SlotId ? b : a;
                // The attacker claims to be the asked player and answers for them.
                GameCommand answer = req is PlayActionRequest ? new EndTurnCommand() : (GameCommand)new RespondCommand { Pass = true };
                answer.RequestId = req.RequestId;
                var forged = new NetworkMessage(CommandCodec.MessageTypeOf(answer), CommandCodec.Encode(answer)) { PlayerId = victimSeat, SequenceId = 1 };
                int rejections = 0;
                attacker.CommandRejected += (seq, r) => rejections++;
                attacker.Connection.Send(forged);
                h.PumpUntil(() => rejections == 1, "rejection of the forged command");
                Assert.That(engine.Context.Requests.Find(req.RequestId), Is.SameAs(req), "the victim's request must still be open");
            }
        }

        [Test]
        public void ClientReconnectsAndResumesItsSeat()
        {
            var transport = new InMemoryNetworkTransport();
            using (var h = new NetHarness(transport, Settings(FreeForAllMode.Id, 4)))
            {
                var a = h.AddClient("A");
                h.PumpUntil(() => a.Status == ClientConnectionStatus.InLobby, "a");
                var b = h.AddClient("B");
                h.PumpUntil(() => a.Room.HumanCount == 2, "b");
                b.SetReady(true);
                h.PumpUntil(() => a.Room.Slots[1].Ready, "ready");
                a.StartGame();
                h.PumpUntil(() => b.Game != null, "started");
                var botA = new NaiveClientBot(a, 1);
                // Play a little.
                for (int i = 0; i < 200; i++)
                {
                    botA.Act();
                    h.PumpOnce();
                }
                int seat = b.SlotId;
                bool reconnected = false;
                b.Reconnected += () => reconnected = true;
                b.Connection.Close("wifi lost");
                h.PumpUntil(() => h.Server.Session.State.GetPlayer(seat).Connected == false, "server noticed");
                h.PumpUntil(() => reconnected && b.Status == ClientConnectionStatus.InGame, "reconnected");
                Assert.That(b.SlotId, Is.EqualTo(seat));
                Assert.That(h.Server.Session.State.GetPlayer(seat).Connected, Is.True);
                Assert.That(h.Server.Session.State.GetPlayer(seat).AIControlled, Is.False);
                h.Pump(5);
                Assert.That(b.Game.Dump(), Is.EqualTo(h.Server.Session.GetSnapshot(seat).Dump()), "state restored from snapshot");
                // The reconnected player can keep playing to the end.
                var botB = new NaiveClientBot(b, 2);
                h.PumpUntil(() =>
                {
                    botA.Act();
                    botB.Act();
                    return b.Game.IsGameOver;
                }, "game over", 60000, () => "open: " + string.Join(",", h.Server.Session.Engine.OpenRequests.Select(r => r.ToString()))
                    + " a.status=" + a.Status + " b.status=" + b.Status + " b.req=" + b.Game.FindRequestFor(b.SlotId)?.Describe()
                    + " a.req=" + a.Game.FindRequestFor(a.SlotId)?.Describe() + " b.seq=" + b.Game.LastEventSequence
                    + " server.seq=" + h.Server.Session.Engine.Context.Events.LastSequence + " rejA=" + botA.Rejections + " rejB=" + botB.Rejections);
            }
        }

        [Test]
        public void LongDisconnectHandsSeatToAIUntilReturn()
        {
            var settings = Settings(FreeForAllMode.Id, 3);
            settings.Config.DisconnectAITakeoverMs = 5000;
            using (var h = new NetHarness(new InMemoryNetworkTransport(), settings))
            {
                var a = h.AddClient("A");
                h.PumpUntil(() => a.Status == ClientConnectionStatus.InLobby, "a");
                var b = h.AddClient("B");
                h.PumpUntil(() => a.Room.HumanCount == 2, "b");
                b.SetReady(true);
                h.PumpUntil(() => a.Room.Slots[1].Ready, "ready");
                a.StartGame();
                h.PumpUntil(() => b.Game != null, "started");
                int seat = b.SlotId;
                b.MaxReconnectMs = -1; // give up immediately: stay away for now
                b.Connection.Close("gone");
                h.PumpUntil(() => !h.Server.Session.State.GetPlayer(seat).Connected && b.Status == ClientConnectionStatus.Disconnected, "disconnect noticed");
                Assert.That(h.Server.Session.State.GetPlayer(seat).AIControlled, Is.False, "grace period before AI takeover");
                h.Clock.Advance(5000);
                h.PumpUntil(() => h.Server.Session.State.GetPlayer(seat).AIControlled, "ai takeover");

                // A stranger cannot take the seat with a plain join while the game runs.
                var stranger = h.AddClient("Stranger");
                string rejected = null;
                stranger.JoinRejected += (code, m) => rejected = code;
                h.PumpUntil(() => rejected != null, "join rejected");
                Assert.That(rejected, Is.EqualTo("in_game"));

                // The original player comes back with its token and regains control.
                b.MaxReconnectMs = 120000;
                b.ReconnectNow();
                h.PumpUntil(() => b.Status == ClientConnectionStatus.InGame && h.Server.Session.State.GetPlayer(seat).Connected, "returned");
                Assert.That(h.Server.Session.State.GetPlayer(seat).AIControlled, Is.False);
            }
        }

        [Test]
        public void WrongProtocolVersionIsRejected()
        {
            var transport = new InMemoryNetworkTransport();
            using (var h = new NetHarness(transport, new RoomSettings()))
            {
                var conn = transport.Connect("x", h.Server.Port, 1000, out _);
                conn.Send(new NetworkMessage(MessageType.Hello, Payloads.Hello(new HelloPayload { ProtocolVersion = 999, Nickname = "old" })));
                h.Pump(10);
                NetworkMessage reply = null;
                while (conn.TryReceive(out var m)) reply = m;
                Assert.That(reply?.Type, Is.EqualTo(MessageType.Disconnect));
                Assert.That(Payloads.ReadReason(reply, out _), Is.EqualTo("version_mismatch"));
                Assert.That(conn.IsConnected, Is.False);
            }
        }

        [Test]
        public void MessagesBeforeHandshakeOrFloodsDisconnect()
        {
            var transport = new InMemoryNetworkTransport();
            using (var h = new NetHarness(transport, new RoomSettings()))
            {
                var early = transport.Connect("x", h.Server.Port, 1000, out _);
                early.Send(new NetworkMessage(MessageType.StartGame));
                h.Pump(5);
                Assert.That(early.IsConnected, Is.False);

                var flooder = transport.Connect("x", h.Server.Port, 1000, out _);
                flooder.Send(new NetworkMessage(MessageType.Hello, Payloads.Hello(new HelloPayload())));
                for (int i = 0; i < 1000; i++) flooder.Send(new NetworkMessage(MessageType.Chat, Payloads.Chat(0, "spam")));
                h.Pump(5);
                Assert.That(flooder.IsConnected, Is.False);
            }
        }

        [Test]
        public void SpectatorSeesOnlyPublicInformation()
        {
            var settings = Settings(IdentityMode.Id, 5);
            settings.Config.AllowSpectators = true;
            using (var h = new NetHarness(new InMemoryNetworkTransport(), settings))
            {
                var host = h.AddClient("Host");
                h.PumpUntil(() => host.Status == ClientConnectionStatus.InLobby, "host");
                var watcher = h.AddClient("Watcher", spectator: true);
                h.PumpUntil(() => watcher.Status == ClientConnectionStatus.InLobby, "spectator joined");
                Assert.That(watcher.SlotId, Is.EqualTo(-1));
                host.StartGame();
                h.PumpUntil(() => watcher.Game != null, "spectator snapshot");
                Assert.That(watcher.Game.ViewerId, Is.EqualTo(-1));
                Assert.That(watcher.Game.Players.All(p => p.HandCards == null), Is.True);
                Assert.That(watcher.Game.Players.Count(p => p.Role == Role.Unknown), Is.EqualTo(4));
            }
        }
    }

    public class DiscoveryTests
    {
        [Test]
        public void BrowserSeesAnnouncedRoom()
        {
            int port = 47000 + System.Environment.TickCount % 500;
            using (var browser = new LanRoomBrowser(port))
            using (var announcer = new LanRoomAnnouncer(port, new[] { new IPEndPoint(IPAddress.Loopback, port) }))
            {
                var info = new RoomInfo { RoomId = "r1", RoomName = "桃园", HostName = "刘备", ModeId = IdentityMode.Id, Humans = 2, MaxPlayers = 8, Port = 47701, InviteCode = "1234-5678" };
                List<RoomInfo> rooms = null;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 5000)
                {
                    announcer.Update(sw.ElapsedMilliseconds + 10000, info);
                    rooms = browser.Update(sw.ElapsedMilliseconds);
                    if (rooms.Count > 0) break;
                    System.Threading.Thread.Sleep(20);
                }
                Assert.That(rooms, Has.Count.EqualTo(1));
                Assert.That(rooms[0].RoomName, Is.EqualTo("桃园"));
                Assert.That(rooms[0].Address, Is.EqualTo("127.0.0.1"));
                Assert.That(rooms[0].Port, Is.EqualTo(47701));
                Assert.That(browser.Update(sw.ElapsedMilliseconds + 60000), Is.Empty, "stale rooms expire");
            }
        }
    }
}
