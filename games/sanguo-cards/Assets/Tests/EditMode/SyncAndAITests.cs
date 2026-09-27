using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.Game;
using Sanguo.GameModes;
using Sanguo.Utils;

namespace Sanguo.Tests
{
    internal static class AIGames
    {
        public static GameModeConfig FreeForAllConfig(int players, bool characters = true)
        {
            return new GameModeConfig
            {
                ModeId = FreeForAllMode.Id,
                PlayerCount = players,
                StartingHandSize = 4,
                DrawPerTurn = 2,
                CharacterSelection = characters ? CharacterSelectionMode.Random : CharacterSelectionMode.None,
                ShuffleSeats = true,
                MaxRounds = 60
            };
        }

        public static GameSession CreateBotSession(GameModeConfig config, int seed, IGameMode mode = null, bool keepHistory = false)
        {
            var setups = Enumerable.Range(0, config.PlayerCount).Select(i => new PlayerSetup("Bot" + i, true)).ToList();
            var engine = new GameEngine(TestContent.Shared, mode ?? new FreeForAllMode(config), config, setups, seed, "room", keepHistory);
            return new GameSession(engine, new ManualClock());
        }
    }

    /// <summary>
    /// Incremental sync: a client replica built from its initial snapshot plus the projected event
    /// stream must always equal a fresh snapshot for that viewer. This is what guarantees clients
    /// never drift from the server without resending whole states.
    /// </summary>
    public class SyncTests
    {
        [Test]
        public void ReplicasMatchSnapshotsThroughoutAIGame([Values(1, 2, 3, 4, 5, 6)] int seed)
        {
            var session = AIGames.CreateBotSession(AIGames.FreeForAllConfig(5), seed);
            session.Start();
            var replicas = new Dictionary<int, ClientGameState>();
            for (int v = -1; v < 5; v++)
            {
                int viewer = v;
                replicas[viewer] = session.GetSnapshot(viewer);
                session.AddViewer(viewer, e => Assert.That(replicas[viewer].Apply(e), Is.True, "viewer " + viewer + " failed to apply " + e));
            }
            int commands = 0;
            while (!session.Engine.IsGameOver && commands < 20000)
            {
                session.RunUntilHumanInputOrEnd(1);
                commands++;
                if (commands % 25 == 0) CompareAll(session, replicas);
                Assert.That(session.State.ValidateInvariants(), Is.Null);
            }
            Assert.That(session.Engine.IsGameOver, Is.True, "game did not finish");
            CompareAll(session, replicas);
        }

        private static void CompareAll(GameSession session, Dictionary<int, ClientGameState> replicas)
        {
            foreach (var kv in replicas)
            {
                Assert.That(kv.Value.NeedsResync, Is.False);
                Assert.That(kv.Value.Dump(), Is.EqualTo(session.GetSnapshot(kv.Key).Dump()), "replica of viewer " + kv.Key + " diverged");
                foreach (var p in kv.Value.Players)
                    if (p.PlayerId != kv.Key) Assert.That(p.HandCards, Is.Null);
            }
        }

        [Test]
        public void SequenceGapFlagsResync()
        {
            var session = AIGames.CreateBotSession(AIGames.FreeForAllConfig(3), 3);
            session.Start();
            var replica = session.GetSnapshot(0);
            var received = new List<GameEvent>();
            session.AddViewer(0, received.Add);
            session.RunUntilHumanInputOrEnd(3);
            Assert.That(received.Count, Is.GreaterThan(2));
            Assert.That(replica.Apply(received[1]), Is.False);
            Assert.That(replica.NeedsResync, Is.True);
        }
    }

    public class AITests
    {
        [Test]
        public void FourBotsFinishAMinimalGame([Range(1, 12)] int seed)
        {
            var session = AIGames.CreateBotSession(AIGames.FreeForAllConfig(4, characters: false), seed);
            session.Start();
            bool over = session.RunUntilHumanInputOrEnd();
            Assert.That(over, Is.True);
            var result = session.State.Result;
            Assert.That(result, Is.Not.Null);
            if (!result.IsDraw)
            {
                Assert.That(result.WinnerIds.Count, Is.EqualTo(1));
                Assert.That(session.State.GetPlayer(result.WinnerIds[0]).Alive, Is.True);
            }
        }

        [Test]
        public void MostMinimalGamesEndWithAWinner()
        {
            int winners = 0;
            for (int seed = 100; seed < 120; seed++)
            {
                var session = AIGames.CreateBotSession(AIGames.FreeForAllConfig(4, characters: false), seed);
                session.Start();
                session.RunUntilHumanInputOrEnd();
                if (!session.State.Result.IsDraw) winners++;
            }
            Assert.That(winners, Is.GreaterThanOrEqualTo(16), "bots should usually kill each other well before the round limit");
        }

        [Test]
        public void SameSeedReplaysIdentically()
        {
            string Run(int seed)
            {
                var session = AIGames.CreateBotSession(AIGames.FreeForAllConfig(6), seed, keepHistory: true);
                session.Start();
                session.RunUntilHumanInputOrEnd();
                var history = session.Engine.Context.Events.History;
                return string.Join(",", history.Select(e => (int)e.Type)) + "|" + session.State.Result.Describe();
            }
            Assert.That(Run(77), Is.EqualTo(Run(77)));
            Assert.That(Run(77), Is.Not.EqualTo(Run(78)));
        }

        [Test]
        public void BotsWithCharactersAndSkillsPlayLargeTables([Values(8, 12, 20)] int players)
        {
            var session = AIGames.CreateBotSession(AIGames.FreeForAllConfig(players), players * 31);
            session.Start();
            Assert.That(session.RunUntilHumanInputOrEnd(), Is.True);
            Assert.That(session.State.ValidateInvariants(), Is.Null);
        }

        [Test]
        public void AIRespondsToAttacksAndRescuesItself()
        {
            var s = Scenario.Create(3, setups: new List<PlayerSetup>
            {
                new PlayerSetup("Human"), new PlayerSetup("Bot1", true), new PlayerSetup("Bot2", true)
            });
            var session = new GameSession(s.Engine, s.Clock);
            s.SetHp(1, 1);
            var dodge = s.Give(1, "dodge");
            var heal = s.Give(1, "heal");
            var strike = s.Give(0, "strike");
            var duel = s.Give(0, "duel");
            Scenario.AssertAccepted(session.Submit(new PlayCardCommand { PlayerId = 0, CardInstanceId = strike.InstanceId, TargetIds = new[] { 1 }, SequenceNumber = 1, RequestId = s.RequestFor(0).RequestId }));
            session.RunUntilHumanInputOrEnd();
            Assert.That(dodge.Zone, Is.EqualTo(ZoneType.DiscardPile), "bot should dodge");
            Assert.That(s.P(1).Hp, Is.EqualTo(1));
            Scenario.AssertAccepted(session.Submit(new PlayCardCommand { PlayerId = 0, CardInstanceId = duel.InstanceId, TargetIds = new[] { 1 }, SequenceNumber = 2, RequestId = s.RequestFor(0).RequestId }));
            session.RunUntilHumanInputOrEnd();
            // Bot 1 has no strike, loses the duel, goes dying and uses its own heal.
            Assert.That(heal.Zone, Is.EqualTo(ZoneType.DiscardPile), "bot should rescue itself");
            Assert.That(s.P(1).Alive, Is.True);
            Assert.That(s.P(1).Hp, Is.EqualTo(1));
        }

        [Test]
        public void DisconnectedPlayerIsTakenOverByAIAndCanReturn()
        {
            var s = Scenario.Create(3, c => c.DisconnectAITakeoverMs = 5000, setups: new List<PlayerSetup>
            {
                new PlayerSetup("A"), new PlayerSetup("B"), new PlayerSetup("C")
            });
            var session = new GameSession(s.Engine, s.Clock);
            session.SetConnected(0, false);
            Assert.That(s.P(0).Connected, Is.False);
            Assert.That(s.P(0).AIControlled, Is.False);
            s.Clock.Advance(5000);
            session.Update();
            Assert.That(s.P(0).AIControlled, Is.True);
            // The AI ended player 0's (empty-handed) turn.
            Assert.That(s.State.Turn.CurrentPlayerId, Is.EqualTo(1));
            session.SetConnected(0, true);
            Assert.That(s.P(0).Connected, Is.True);
            Assert.That(s.P(0).AIControlled, Is.False);
        }
    }
}
