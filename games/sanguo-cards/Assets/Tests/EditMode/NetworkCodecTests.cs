using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NUnit.Framework;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.Game;
using Sanguo.GameModes;
using Sanguo.Network;
using Sanguo.Utils;

namespace Sanguo.Tests
{
    public class SerializationTests
    {
        [Test]
        public void PrimitivesRoundTrip()
        {
            var w = new NetWriter(4);
            w.WriteVarInt(0);
            w.WriteVarInt(-1);
            w.WriteVarInt(int.MaxValue);
            w.WriteVarInt(int.MinValue);
            w.WriteInt64(-1234567890123L);
            w.WriteString(null);
            w.WriteString("");
            w.WriteString("闪避 dodge");
            w.WriteIntArray(null);
            w.WriteIntArray(new int[0]);
            w.WriteIntArray(new[] { 5, -7 });
            w.WriteBool(true);
            var r = new NetReader(w.ToArray());
            Assert.That(r.ReadVarInt(), Is.EqualTo(0));
            Assert.That(r.ReadVarInt(), Is.EqualTo(-1));
            Assert.That(r.ReadVarInt(), Is.EqualTo(int.MaxValue));
            Assert.That(r.ReadVarInt(), Is.EqualTo(int.MinValue));
            Assert.That(r.ReadInt64(), Is.EqualTo(-1234567890123L));
            Assert.That(r.ReadString(), Is.Null);
            Assert.That(r.ReadString(), Is.EqualTo(""));
            Assert.That(r.ReadString(), Is.EqualTo("闪避 dodge"));
            Assert.That(r.ReadIntArray(), Is.Null);
            Assert.That(r.ReadIntArray(), Is.Empty);
            Assert.That(r.ReadIntArray(), Is.EqualTo(new[] { 5, -7 }));
            Assert.That(r.ReadBool(), Is.True);
            Assert.That(r.AtEnd, Is.True);
        }

        [Test]
        public void TruncatedOrHostileInputThrowsProtocolException()
        {
            Assert.Throws<ProtocolException>(() => new NetReader(new byte[] { 0x80 }).ReadVarUInt());
            Assert.Throws<ProtocolException>(() => new NetReader(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }).ReadString());
            Assert.Throws<ProtocolException>(() => new NetReader(new byte[] { 2 }).ReadBool());
            Assert.Throws<ProtocolException>(() => new NetReader(new byte[] { 200 }).ReadEnum<ZoneType>());
        }

        [Test]
        public void RandomGarbageNeverCrashesDecoders()
        {
            var rng = new Random(5);
            for (int i = 0; i < 3000; i++)
            {
                var data = new byte[rng.Next(0, 64)];
                rng.NextBytes(data);
                try
                {
                    NetworkMessage.FromBytes(data);
                }
                catch (ProtocolException)
                {
                }
                try
                {
                    EventCodec.DecodeBatch(data);
                }
                catch (ProtocolException)
                {
                }
                try
                {
                    SnapshotCodec.Decode(data, out _);
                }
                catch (ProtocolException)
                {
                }
            }
        }

        [Test]
        public void FramesSurviveArbitraryChunking()
        {
            var messages = Enumerable.Range(0, 20).Select(i => new NetworkMessage(MessageType.Chat, Payloads.Chat(i, new string('x', i * 37)))
            {
                PlayerId = i,
                RoomId = "r",
                SequenceId = i * 3,
                Timestamp = i
            }).ToList();
            var stream = messages.SelectMany(FrameCodec.Encode).ToArray();
            var decoder = new FrameDecoder();
            var output = new List<NetworkMessage>();
            var rng = new Random(9);
            int pos = 0;
            while (pos < stream.Length)
            {
                int n = Math.Min(rng.Next(1, 50), stream.Length - pos);
                decoder.Feed(stream, pos, n, output);
                pos += n;
            }
            Assert.That(output.Count, Is.EqualTo(20));
            for (int i = 0; i < 20; i++)
            {
                Assert.That(output[i].PlayerId, Is.EqualTo(i));
                Assert.That(Payloads.ReadChat(output[i], out int slot), Is.EqualTo(new string('x', i * 37)));
                Assert.That(slot, Is.EqualTo(i));
            }
        }

        [Test]
        public void OversizedFrameIsRejected()
        {
            var decoder = new FrameDecoder();
            var header = BitConverter.GetBytes(ProtocolInfo.MaxFrameSize + 1);
            Assert.Throws<ProtocolException>(() => decoder.Feed(header, 0, 4, new List<NetworkMessage>()));
        }

        [Test]
        public void InviteCodeRoundTrip()
        {
            var ip = IPAddress.Parse("192.168.31.207");
            string code = InviteCode.Encode(ip, ProtocolInfo.DefaultGamePort + 3);
            Assert.That(code.Length, Is.EqualTo(9));
            Assert.That(InviteCode.TryDecode(code.ToLowerInvariant().Replace('0', 'O'), out var back, out int port), Is.True);
            Assert.That(back, Is.EqualTo(ip));
            Assert.That(port, Is.EqualTo(ProtocolInfo.DefaultGamePort + 3));
            Assert.That(InviteCode.TryDecode("ZZZ", out _, out _), Is.False);
        }

        [Test]
        public void InviteCodeOnlyDecodesLanAddresses()
        {
            foreach (var lan in new[] { "192.168.43.1", "10.0.0.7", "172.20.10.2", "127.0.0.1", "169.254.3.4", "100.72.1.9" })
            {
                string code = InviteCode.Encode(IPAddress.Parse(lan), ProtocolInfo.DefaultGamePort);
                Assert.That(InviteCode.TryDecode(code, out var back, out _), Is.True, lan);
                Assert.That(back.ToString(), Is.EqualTo(lan));
            }
            string wan = InviteCode.Encode(IPAddress.Parse("8.8.8.8"), ProtocolInfo.DefaultGamePort);
            Assert.That(InviteCode.TryDecode(wan, out _, out _), Is.False, "not a LAN host");
            Assert.That(InviteCode.TryDecode("not-a-code", out _, out _), Is.False, "typo-like input is rejected");
        }

        [Test]
        public void CommandsRoundTrip()
        {
            var commands = new GameCommand[]
            {
                new PlayCardCommand { RequestId = 7, CardInstanceId = 42, TargetIds = new[] { 3, 1 }, SkillId = "dragon_dance", AsCardId = "strike" },
                new UseSkillCommand { RequestId = 8, SkillId = "raid", CardIds = new[] { 1, 2 }, TargetIds = new[] { 5 } },
                new SelectTargetCommand { RequestId = 9, TargetIds = new[] { 4 } },
                new RespondCommand { RequestId = 10, Pass = false, CardIds = new[] { 11 }, OptionIndex = 2, PickZone = ZoneType.Equipment, PickIndex = 3, SkillId = "x" },
                new EndTurnCommand { RequestId = 11 }
            };
            foreach (var cmd in commands)
            {
                var msg = new NetworkMessage(CommandCodec.MessageTypeOf(cmd), CommandCodec.Encode(cmd)) { PlayerId = 2, SequenceId = 17 };
                var back = CommandCodec.Decode(NetworkMessage.FromBytes(msg.ToBytes()));
                Assert.That(back.GetType(), Is.EqualTo(cmd.GetType()));
                Assert.That(back.RequestId, Is.EqualTo(cmd.RequestId));
                Assert.That(back.SequenceNumber, Is.EqualTo(17));
                Assert.That(CommandCodec.Encode(back), Is.EqualTo(CommandCodec.Encode(cmd)));
            }
        }

        [Test]
        public void EveryEventTypeHasAWireFormat()
        {
            var eventTypes = typeof(GameEvent).Assembly.GetTypes().Where(t => typeof(GameEvent).IsAssignableFrom(t) && !t.IsAbstract).ToList();
            Assert.That(eventTypes.Count, Is.GreaterThanOrEqualTo(28));
            foreach (var t in eventTypes)
            {
                var e = (GameEvent)Activator.CreateInstance(t);
                if (e is CardMoveEvent m) m.Cards = new List<CardInfo> { new CardInfo(3, "strike", Suit.Spade, 7) };
                if (e is RequestOpenedEvent ro) ro.Info = new RequestInfo { RequestId = 1, Kind = RequestKind.PlayAction };
                if (e is GameEndedEvent ge) ge.Result = GameResult.Draw("x");
                e.Sequence = 99;
                var w = new NetWriter();
                Assert.DoesNotThrow(() => EventCodec.Write(w, e), t.Name);
                var back = EventCodec.Read(new NetReader(w.ToArray()));
                Assert.That(back.GetType(), Is.EqualTo(t));
                Assert.That(back.Sequence, Is.EqualTo(99));
                var w2 = new NetWriter();
                EventCodec.Write(w2, back);
                Assert.That(w2.ToArray(), Is.EqualTo(w.ToArray()), t.Name + " did not round-trip");
            }
        }

        [Test]
        public void RoomStateAndSettingsRoundTrip()
        {
            var room = new RoomState { RoomId = "abc", InviteCode = "1234-5678", HostSlotId = 2, InGame = true, SpectatorCount = 1 };
            room.Settings.RoomName = "测试房";
            room.Settings.Config.ModeId = Team5v5Mode.Id;
            room.Settings.Config.PlayerCount = 10;
            room.Slots.Add(new RoomSlot { SlotId = 0, Occupied = true, Nickname = "甲", Ready = true, Team = Team.B, Connected = true, PingMs = 23 });
            var back = RoomCodec.ReadState(new NetReader(RoomCodec.EncodeState(room)));
            Assert.That(back.RoomId, Is.EqualTo("abc"));
            Assert.That(back.HostSlotId, Is.EqualTo(2));
            Assert.That(back.Settings.RoomName, Is.EqualTo("测试房"));
            Assert.That(back.Settings.Config.ModeId, Is.EqualTo(Team5v5Mode.Id));
            Assert.That(back.Settings.Config.PlayerCount, Is.EqualTo(10));
            Assert.That(back.Slots[0].Nickname, Is.EqualTo("甲"));
            Assert.That(back.Slots[0].Team, Is.EqualTo(Team.B));
            Assert.That(back.Slots[0].PingMs, Is.EqualTo(23));
        }

        /// <summary>
        /// The whole sync pipeline through bytes: snapshot → encode → decode, then every projected
        /// event → encode batch → decode → apply. The decoded replica must equal a freshly
        /// encoded/decoded snapshot, for players and spectators, in several modes.
        /// </summary>
        [TestCase(IdentityMode.Id, 8)]
        [TestCase(Team3v3Mode.Id, 6)]
        [TestCase(FreeForAllMode.Id, 5)]
        public void WireReplicasMatchServerSnapshots(string modeId, int players)
        {
            var config = new GameModeConfig { ModeId = modeId, PlayerCount = players, CharacterSelection = CharacterSelectionMode.Random, MaxRounds = 60 };
            var setups = Enumerable.Range(0, players).Select(i => new PlayerSetup("Bot" + i, true)).ToList();
            var engine = new GameEngine(TestContent.Shared, GameModeRegistry.CreateDefault().Create(config), config, setups, 7);
            var session = new GameSession(engine, new ManualClock());
            session.Start();
            var replicas = new Dictionary<int, ClientGameState>();
            var pending = new Dictionary<int, List<GameEvent>>();
            foreach (int v in new[] { -1, 0, players - 1 })
            {
                int viewer = v;
                replicas[viewer] = SnapshotCodec.Decode(SnapshotCodec.Encode(session.GetSnapshot(viewer), 0), out _);
                pending[viewer] = new List<GameEvent>();
                session.AddViewer(viewer, e => pending[viewer].Add(e));
            }
            while (!engine.IsGameOver)
            {
                session.RunUntilHumanInputOrEnd(5);
                foreach (var kv in pending)
                {
                    foreach (var e in EventCodec.DecodeBatch(EventCodec.EncodeBatch(kv.Value)))
                        Assert.That(replicas[kv.Key].Apply(e), Is.True, "viewer " + kv.Key + " could not apply " + e);
                    kv.Value.Clear();
                }
            }
            foreach (var kv in replicas)
            {
                var fresh = SnapshotCodec.Decode(SnapshotCodec.Encode(session.GetSnapshot(kv.Key), 0), out _);
                Assert.That(kv.Value.Dump(), Is.EqualTo(fresh.Dump()));
            }
        }
    }
}
