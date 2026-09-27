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
    public class TeamModeTests
    {
        /// <summary>Team scenario: players 0..n-1 alternate A, B, A, B (lobby assignment), seats alternate too.</summary>
        private static Scenario Create(int teamSize, bool captains = false, TeamVictoryRule victory = TeamVictoryRule.EliminateAll,
            SeatArrangement seating = SeatArrangement.Alternating, int captainA = -1, int captainB = -1)
        {
            var setups = new List<PlayerSetup>();
            for (int i = 0; i < teamSize * 2; i++)
            {
                var team = i % 2 == 0 ? Team.A : Team.B;
                setups.Add(new PlayerSetup("P" + i) { Team = team, Role = i == captainA || i == captainB ? Role.Captain : Role.None });
            }
            return Scenario.Create(teamSize * 2, c =>
            {
                c.ModeId = TeamBattleMode.Id;
                c.TeamSize = teamSize;
                c.TeamAssignment = TeamAssignmentMode.HostAssigned;
                c.UseCaptain = captains;
                c.TeamVictory = victory;
                c.Seating = seating;
            }, content: TestKills.Content, mode: c => new TeamBattleMode(c), setups: setups);
        }

        [Test]
        public void ThreeVersusThreeUsesAlternatingSeatsAndLobbyTeams()
        {
            var s = Create(3);
            var seatTeams = s.State.SeatOrder.Select(p => p.Team).ToArray();
            Assert.That(seatTeams, Is.EqualTo(new[] { Team.A, Team.B, Team.A, Team.B, Team.A, Team.B }));
            for (int i = 0; i < 6; i++) Assert.That(s.P(i).Team, Is.EqualTo(i % 2 == 0 ? Team.A : Team.B));
            var labels = s.State.SeatOrder.Select(p => TeamBattleMode.PositionLabel(s.State, p)).ToArray();
            Assert.That(labels, Is.EqualTo(new[] { "A1", "B1", "A2", "B2", "A3", "B3" }));
            // Teams are public: every viewer sees every team.
            var snap = s.Engine.CreateSnapshot(1);
            Assert.That(snap.Players.Select(p => p.Team), Is.EqualTo(Enumerable.Range(0, 6).Select(i => i % 2 == 0 ? Team.A : Team.B)));
        }

        [Test]
        public void GroupedSeatingPutsTeamsTogether()
        {
            var s = Create(3, seating: SeatArrangement.Grouped);
            Assert.That(s.State.SeatOrder.Select(p => p.Team).ToArray(), Is.EqualTo(new[] { Team.A, Team.A, Team.A, Team.B, Team.B, Team.B }));
        }

        [Test]
        public void RandomAssignmentMakesBalancedTeams()
        {
            var config = new GameModeConfig { ModeId = Team5v5Mode.Id, PlayerCount = 10 };
            var setups = Enumerable.Range(0, 10).Select(i => new PlayerSetup("P" + i)).ToList();
            var engine = new GameEngine(TestContent.Shared, new Team5v5Mode(config), config, setups, 9);
            engine.Start(0);
            Assert.That(engine.State.Players.Count(p => p.Team == Team.A), Is.EqualTo(5));
            Assert.That(engine.State.Players.Count(p => p.Team == Team.B), Is.EqualTo(5));
            Assert.That(engine.State.Players.All(p => p.Role == Role.Member && p.RoleRevealed), Is.True);
        }

        [Test]
        public void CaptainsAreChosenPublicAndStronger()
        {
            var s = Create(3, captains: true, captainA: 2, captainB: 5);
            Assert.That(s.P(2).Role, Is.EqualTo(Role.Captain));
            Assert.That(s.P(5).Role, Is.EqualTo(Role.Captain));
            Assert.That(s.State.Players.Count(p => p.Role == Role.Captain), Is.EqualTo(2));
            Assert.That(s.P(2).MaxHp, Is.EqualTo(5));
            Assert.That(s.P(0).MaxHp, Is.EqualTo(4));
            Assert.That(s.Engine.CreateSnapshot(1).Players[2].Role, Is.EqualTo(Role.Captain));
        }

        [Test]
        public void KillCaptainVictoryNeedsCaptains()
        {
            var config = new GameModeConfig { ModeId = Team3v3Mode.Id, PlayerCount = 6, TeamVictory = TeamVictoryRule.KillCaptain, UseCaptain = false };
            var setups = Enumerable.Range(0, 6).Select(i => new PlayerSetup("P" + i)).ToList();
            Assert.Throws<System.ArgumentException>(() => new GameEngine(TestContent.Shared, new Team3v3Mode(config), config, setups, 1));
        }

        [Test]
        public void EliminatingTheOtherTeamWins3v3()
        {
            var s = Create(3);
            TestKills.Kill(s, 0, 1);
            TestKills.Kill(s, 0, 3);
            Assert.That(s.State.IsGameOver, Is.False);
            TestKills.Kill(s, 2, 0 + 5);
            Assert.That(s.State.IsGameOver, Is.True);
            Assert.That(s.State.Result.WinningTeam, Is.EqualTo(Team.A));
            Assert.That(s.State.Result.WinnerIds, Is.EquivalentTo(new[] { 0, 2, 4 }));
        }

        [Test]
        public void KillingTheCaptainEndsTheGame()
        {
            var s = Create(3, captains: true, victory: TeamVictoryRule.KillCaptain, captainA: 0, captainB: 3);
            TestKills.Kill(s, 0, 1);
            Assert.That(s.State.IsGameOver, Is.False, "a member died, the captain lives");
            TestKills.Kill(s, 2, 3);
            Assert.That(s.State.IsGameOver, Is.True);
            Assert.That(s.State.Result.Reason, Is.EqualTo("captain_killed"));
            Assert.That(s.State.Result.WinningTeam, Is.EqualTo(Team.A));
        }

        [Test]
        public void TeamWinsIncludeDeadTeammates5v5()
        {
            var s = Create(5);
            TestKills.Kill(s, 1, 0);
            foreach (int victim in new[] { 1, 3, 5, 7, 9 }) TestKills.Kill(s, 2, victim);
            Assert.That(s.State.IsGameOver, Is.True);
            Assert.That(s.State.Result.WinningTeam, Is.EqualTo(Team.A));
            Assert.That(s.State.Result.WinnerIds, Does.Contain(0), "dead teammates share the win");
        }

        [Test]
        public void TenVersusTenDecidedByElimination()
        {
            var s = Create(10);
            Assert.That(s.State.PlayerCount, Is.EqualTo(20));
            Assert.That(s.State.Cards.Count, Is.EqualTo(84 * 3), "big tables use more deck copies");
            for (int victim = 0; victim < 20; victim += 2) TestKills.Kill(s, 1, victim);
            Assert.That(s.State.Result.WinningTeam, Is.EqualTo(Team.B));
            Assert.That(s.State.Result.WinnerIds.Count, Is.EqualTo(10));
        }

        [Test]
        public void KillingAnEnemyDrawsACard()
        {
            var s = Create(3);
            int before = s.P(0).HandCardCount;
            TestKills.Kill(s, 0, 1);
            Assert.That(s.P(0).HandCardCount, Is.EqualTo(before + 1));
        }

        [Test]
        public void FirstTurnDrawsOneFewerCard()
        {
            var s = Scenario.Create(6, c =>
            {
                c.ModeId = Team3v3Mode.Id;
                c.DrawPerTurn = 2;
            }, mode: c => new Team3v3Mode(c));
            var first = s.State.CurrentPlayer;
            Assert.That(first.HandCardCount, Is.EqualTo(1));
            s.EndTurn(first.PlayerId);
            Assert.That(s.State.CurrentPlayer.HandCardCount, Is.EqualTo(2));
        }

        [Test]
        public void BotsNeverTargetTeammatesWithSingleTargetAttacks([Values(3, 5, 10)] int teamSize)
        {
            var config = new GameModeConfig { ModeId = TeamBattleMode.Id, PlayerCount = teamSize * 2, TeamSize = teamSize, CharacterSelection = CharacterSelectionMode.Random, MaxRounds = 80 };
            var setups = Enumerable.Range(0, teamSize * 2).Select(i => new PlayerSetup("Bot" + i, true)).ToList();
            var engine = new GameEngine(TestContent.Shared, new TeamBattleMode(config), config, setups, teamSize * 13, keepEventHistory: true);
            var session = new GameSession(engine, new ManualClock());
            session.Start();
            Assert.That(session.RunUntilHumanInputOrEnd(), Is.True);
            var state = engine.State;
            var friendlyFire = engine.Context.Events.History.OfType<TargetSelectedEvent>()
                .Where(e => e.CardId == "strike" || e.CardId == "duel" || e.CardId == "confinement")
                .Where(e => state.GetPlayer(e.SourceId).Team == state.GetPlayer(e.TargetId).Team)
                .ToList();
            Assert.That(friendlyFire, Is.Empty);
            Assert.That(state.ValidateInvariants(), Is.Null);
        }

        [Test]
        public void ThreeHumansAndSevenBotsFinish5v5ThroughTimeouts()
        {
            var config = new GameModeConfig
            {
                ModeId = Team5v5Mode.Id,
                PlayerCount = 10,
                CharacterSelection = CharacterSelectionMode.Random,
                PlayTimeoutMs = 5000,
                ResponseTimeoutMs = 3000,
                DiscardTimeoutMs = 3000,
                MaxRounds = 60
            };
            var setups = Enumerable.Range(0, 10).Select(i => new PlayerSetup(i < 3 ? "Human" + i : "Bot" + i, i >= 3)).ToList();
            var clock = new ManualClock();
            var session = new GameSession(new GameEngine(TestContent.Shared, new Team5v5Mode(config), config, setups, 44), clock);
            session.Start();
            // Humans are idle: every request of theirs times out and gets the default answer.
            for (int step = 0; step < 200000 && !session.Engine.IsGameOver; step++)
            {
                clock.Advance(500);
                session.Update();
            }
            Assert.That(session.Engine.IsGameOver, Is.True);
            Assert.That(session.State.ValidateInvariants(), Is.Null);
        }

        [Test]
        public void TenVersusTenReplicasStayInSync()
        {
            var config = new GameModeConfig { ModeId = Team10v10Mode.Id, PlayerCount = 20, CharacterSelection = CharacterSelectionMode.Random, MaxRounds = 80 };
            var setups = Enumerable.Range(0, 20).Select(i => new PlayerSetup("Bot" + i, true)).ToList();
            var session = new GameSession(new GameEngine(TestContent.Shared, new Team10v10Mode(config), config, setups, 3), new ManualClock());
            session.Start();
            var replicas = new Dictionary<int, ClientGameState>();
            foreach (int v in new[] { -1, 0, 7, 19 })
            {
                int viewer = v;
                replicas[viewer] = session.GetSnapshot(viewer);
                session.AddViewer(viewer, e => Assert.That(replicas[viewer].Apply(e), Is.True));
            }
            session.RunUntilHumanInputOrEnd();
            foreach (var kv in replicas) Assert.That(kv.Value.Dump(), Is.EqualTo(session.GetSnapshot(kv.Key).Dump()));
        }
    }
}
