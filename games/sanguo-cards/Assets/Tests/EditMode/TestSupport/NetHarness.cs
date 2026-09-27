using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using Sanguo.Core;
using Sanguo.GameModes;
using Sanguo.Network;
using Sanguo.Utils;

namespace Sanguo.Tests
{
    /// <summary>Records every message a client connection receives (to inspect raw wire content).</summary>
    public sealed class SniffingTransport : INetworkTransport
    {
        private readonly INetworkTransport _inner;

        public SniffingTransport(INetworkTransport inner)
        {
            _inner = inner;
        }

        public List<NetworkMessage> Received { get; } = new List<NetworkMessage>();

        public IConnectionListener Listen(int port) => _inner.Listen(port);

        public IConnection Connect(string host, int port, int timeoutMs, out string error)
        {
            var c = _inner.Connect(host, port, timeoutMs, out error);
            return c == null ? null : new SniffingConnection(c, Received);
        }

        private sealed class SniffingConnection : IConnection
        {
            private readonly IConnection _inner;
            private readonly List<NetworkMessage> _log;

            public SniffingConnection(IConnection inner, List<NetworkMessage> log)
            {
                _inner = inner;
                _log = log;
            }

            public int Id => _inner.Id;
            public bool IsConnected => _inner.IsConnected;
            public string RemoteAddress => _inner.RemoteAddress;
            public string CloseReason => _inner.CloseReason;
            public void Send(NetworkMessage message) => _inner.Send(message);
            public void Close(string reason) => _inner.Close(reason);

            public bool TryReceive(out NetworkMessage message)
            {
                if (!_inner.TryReceive(out message)) return false;
                lock (_log) _log.Add(message);
                return true;
            }
        }
    }

    /// <summary>
    /// A remote player that decides only from what its client knows (replica + private request
    /// hints) and sends intents over the network, like a real UI would.
    /// </summary>
    public sealed class NaiveClientBot
    {
        private readonly GameClient _client;
        private readonly Random _random;
        private int _answeredRequest;
        private int _playsThisTurn;
        private int _turnSeen;

        private bool _safeMode;

        public NaiveClientBot(GameClient client, int seed)
        {
            _client = client;
            _random = new Random(seed);
            client.CommandRejected += (seq, result) =>
            {
                Rejections++;
                // Retry the same request with the safest answer (end turn / pass / default).
                _answeredRequest = 0;
                _safeMode = true;
            };
        }

        public int CommandsSent { get; private set; }
        public int Rejections { get; private set; }

        public void Act()
        {
            var game = _client.Game;
            if (game == null || game.IsGameOver || _client.SlotId < 0) return;
            var req = game.FindRequestFor(_client.SlotId);
            if (req == null || req.RequestId == _answeredRequest) return;
            if (game.TurnNumber != _turnSeen)
            {
                _turnSeen = game.TurnNumber;
                _playsThisTurn = 0;
            }
            var cmd = _safeMode ? Safe(game, req) : Decide(game, req);
            _safeMode = false;
            if (cmd == null) return;
            cmd.RequestId = req.RequestId;
            _answeredRequest = req.RequestId;
            _client.Send(cmd);
            CommandsSent++;
        }

        private GameCommand Safe(ClientGameState game, RequestInfo req)
        {
            switch (req.Kind)
            {
                case RequestKind.PlayAction: return new EndTurnCommand();
                case RequestKind.CardResponse:
                case RequestKind.Confirm: return new RespondCommand { Pass = true };
                default: return Decide(game, req);
            }
        }

        private GameCommand Decide(ClientGameState game, RequestInfo req)
        {
            var self = game.Self;
            switch (req.Kind)
            {
                case RequestKind.PlayAction:
                {
                    if (_playsThisTurn < 3 && req.PlayHints != null && req.PlayHints.Count > 0)
                    {
                        var hint = req.PlayHints[_random.Next(req.PlayHints.Count)];
                        if (!hint.NeedsTargets || hint.LegalTargets.Count >= Math.Max(1, hint.MinTargets))
                        {
                            _playsThisTurn++;
                            var targets = new List<int>();
                            if (hint.NeedsTargets)
                                for (int i = 0; i < Math.Max(1, hint.MinTargets); i++) targets.Add(hint.LegalTargets[i]);
                            return new PlayCardCommand
                            {
                                CardInstanceId = hint.CardInstanceId,
                                TargetIds = targets.ToArray(),
                                SkillId = hint.SkillId,
                                AsCardId = hint.SkillId != null ? hint.AsCardId : null
                            };
                        }
                    }
                    return new EndTurnCommand();
                }
                case RequestKind.CardResponse:
                    if (req.PlayHints != null && req.PlayHints.Count >= req.Count && req.Count == 1)
                        return new RespondCommand { CardIds = new[] { req.PlayHints[0].CardInstanceId }, SkillId = req.PlayHints[0].SkillId };
                    return new RespondCommand { Pass = true };
                case RequestKind.Discard:
                {
                    var ids = new List<int>();
                    for (int i = 0; i < self.HandCards.Count && ids.Count < req.Count; i++) ids.Add(self.HandCards[i].InstanceId);
                    return new RespondCommand { CardIds = ids.ToArray() };
                }
                case RequestKind.ChooseCardFromPlayer:
                {
                    var target = game.GetPlayer(req.TargetPlayerId);
                    var zones = (ZoneMask)req.Zones;
                    if ((zones & ZoneMask.Hand) != 0 && target.HandCount > 0) return new RespondCommand { PickZone = ZoneType.Hand, PickIndex = 0 };
                    if ((zones & ZoneMask.Equipment) != 0)
                        for (int i = 1; i < target.Equipment.Length; i++)
                            if (target.Equipment[i] != null) return new RespondCommand { PickZone = ZoneType.Equipment, PickIndex = i };
                    return new RespondCommand { PickZone = ZoneType.JudgeArea, PickIndex = 0 };
                }
                case RequestKind.ChooseTargets:
                {
                    int n = Math.Max(req.MinCount, 1);
                    var ids = req.Candidates.GetRange(0, Math.Min(n, req.Candidates.Count));
                    return new SelectTargetCommand { TargetIds = ids.ToArray() };
                }
                case RequestKind.Confirm:
                    return new RespondCommand { OptionIndex = 1 };
                default:
                    return new RespondCommand { OptionIndex = 0 };
            }
        }
    }

    /// <summary>Server + clients wired over a transport, with pumping helpers.</summary>
    public sealed class NetHarness : IDisposable
    {
        public NetHarness(INetworkTransport transport, RoomSettings settings, ServerOptions options = null)
        {
            Transport = transport;
            Clock = new ManualClock { NowMs = 1000 };
            options = options ?? new ServerOptions();
            options.AIThinkDelayMs = 0;
            if (options.Seed == null) options.Seed = 1234;
            Server = new LanGameServer(transport, TestContent.Shared, GameModeRegistry.CreateDefault(), Clock, settings, options);
            Server.Start(0);
        }

        public INetworkTransport Transport { get; }
        public ManualClock Clock { get; }
        public LanGameServer Server { get; }
        public List<GameClient> Clients { get; } = new List<GameClient>();

        public GameClient AddClient(string nickname, INetworkTransport transport = null, bool spectator = false)
        {
            var c = new GameClient(transport ?? Transport, Clock);
            Assert.That(c.Connect("127.0.0.1", Server.Port, nickname, 0, spectator), Is.True, c.LastError);
            Clients.Add(c);
            return c;
        }

        /// <summary>Simulated time per pump, so pings, timeouts and rate windows behave like real time.</summary>
        public int MsPerPump { get; set; } = 20;

        public void PumpOnce()
        {
            Clock.Advance(MsPerPump);
            Server.Poll();
            foreach (var c in Clients) c.Poll();
        }

        /// <summary>Pumps until the condition holds (real time bounded; TCP needs a few ms per round trip).</summary>
        public void PumpUntil(Func<bool> condition, string what, int timeoutMs = 10000, Func<string> diagnostics = null)
        {
            var sw = Stopwatch.StartNew();
            while (!condition())
            {
                if (sw.ElapsedMilliseconds > timeoutMs) Assert.Fail("Timed out waiting for " + what + (diagnostics != null ? " — " + diagnostics() : ""));
                PumpOnce();
                Thread.Sleep(1);
            }
        }

        public void Pump(int rounds)
        {
            for (int i = 0; i < rounds; i++)
            {
                PumpOnce();
                Thread.Sleep(1);
            }
        }

        public void Dispose()
        {
            foreach (var c in Clients) c.Connection?.Close("test end");
            Server.Stop();
        }
    }
}
