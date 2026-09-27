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
    public class IdentityModeTests
    {
        /// <summary>Identity scenario with preset roles in seat order (seat i = player i).</summary>
        private static Scenario Create(params Role[] roles)
        {
            var setups = roles.Select((r, i) => new PlayerSetup("P" + i) { Role = r }).ToList();
            return Scenario.Create(roles.Length, c => c.ModeId = IdentityMode.Id, content: TestKills.Content, mode: c => new IdentityMode(c), setups: setups);
        }

        private static readonly Role[] Five = { Role.Lord, Role.Rebel, Role.Loyalist, Role.Rebel, Role.Renegade };

        [TestCase(4, 1, 1, 1)]
        [TestCase(5, 1, 2, 1)]
        [TestCase(8, 2, 4, 1)]
        [TestCase(10, 3, 4, 2)]
        [TestCase(12, 4, 5, 2)]
        public void DefaultRoleDistribution(int players, int loyalists, int rebels, int renegades)
        {
            var roles = IdentityMode.DefaultRoles(players);
            int Count(Role r) => roles.Where(x => x.Role == r).Sum(x => x.Count);
            Assert.That(Count(Role.Lord), Is.EqualTo(1));
            Assert.That(Count(Role.Loyalist), Is.EqualTo(loyalists));
            Assert.That(Count(Role.Rebel), Is.EqualTo(rebels));
            Assert.That(Count(Role.Renegade), Is.EqualTo(renegades));
        }

        [Test]
        public void RandomDealMatchesConfiguredCounts()
        {
            var config = new GameModeConfig { ModeId = IdentityMode.Id, PlayerCount = 8 };
            config.Roles.Add(new RoleCount(Role.Lord, 1));
            config.Roles.Add(new RoleCount(Role.Loyalist, 1));
            config.Roles.Add(new RoleCount(Role.Rebel, 4));
            config.Roles.Add(new RoleCount(Role.Renegade, 2));
            var setups = Enumerable.Range(0, 8).Select(i => new PlayerSetup("P" + i)).ToList();
            var engine = new GameEngine(TestContent.Shared, new IdentityMode(config), config, setups, 5);
            engine.Start(0);
            var roles = engine.State.Players.Select(p => p.Role).ToList();
            Assert.That(roles.Count(r => r == Role.Lord), Is.EqualTo(1));
            Assert.That(roles.Count(r => r == Role.Loyalist), Is.EqualTo(1));
            Assert.That(roles.Count(r => r == Role.Rebel), Is.EqualTo(4));
            Assert.That(roles.Count(r => r == Role.Renegade), Is.EqualTo(2));
        }

        [Test]
        public void InvalidRoleConfigIsRejected()
        {
            var config = new GameModeConfig { ModeId = IdentityMode.Id, PlayerCount = 5 };
            config.Roles.Add(new RoleCount(Role.Rebel, 5));
            var setups = Enumerable.Range(0, 5).Select(i => new PlayerSetup("P" + i)).ToList();
            Assert.Throws<System.ArgumentException>(() => new GameEngine(TestContent.Shared, new IdentityMode(config), config, setups, 1));
            config.Roles.Clear();
            config.Roles.Add(new RoleCount(Role.Lord, 1));
            config.Roles.Add(new RoleCount(Role.Rebel, 3));
            Assert.Throws<System.ArgumentException>(() => new GameEngine(TestContent.Shared, new IdentityMode(config), config, setups, 1));
        }

        [Test]
        public void LordIsPublicOthersAreHidden()
        {
            var s = Create(Five);
            for (int viewer = 0; viewer < 5; viewer++)
            {
                var snap = s.Engine.CreateSnapshot(viewer);
                for (int p = 0; p < 5; p++)
                {
                    var expected = p == 0 || p == viewer ? Five[p] : Role.Unknown;
                    Assert.That(snap.Players[p].Role, Is.EqualTo(expected), "viewer " + viewer + " sees player " + p);
                }
            }
        }

        [Test]
        public void LordGetsExtraHpGoesFirstAndKeepsLordSkills()
        {
            var setups = Five.Select((r, i) => new PlayerSetup("P" + i) { Role = r, CharacterId = i == 0 ? "liubei" : i == 2 ? "caocao" : null }).ToList();
            var s = Scenario.Create(5, c =>
            {
                c.ModeId = IdentityMode.Id;
                c.CharacterSelection = CharacterSelectionMode.Random;
            }, mode: c => new IdentityMode(c), setups: setups);
            Assert.That(s.P(0).MaxHp, Is.EqualTo(5));
            Assert.That(s.P(2).MaxHp, Is.EqualTo(4));
            Assert.That(s.State.Turn.CurrentPlayerId, Is.EqualTo(0));
            Assert.That(s.P(0).FindSkill("imperial_aura"), Is.Not.Null, "lord keeps the lord skill");
            Assert.That(s.P(2).FindSkill("imperial_aura"), Is.Null, "a non-lord with the same kind of character does not");
            Assert.That(s.P(2).FindSkill("endure"), Is.Not.Null);
        }

        [Test]
        public void LordChoosesCharacterFirst()
        {
            var setups = Five.Select((r, i) => new PlayerSetup("P" + i) { Role = r }).ToList();
            var s = Scenario.Create(5, c =>
            {
                c.ModeId = IdentityMode.Id;
                c.CharacterSelection = CharacterSelectionMode.Choose;
            }, mode: c => new IdentityMode(c), setups: setups);
            var lordReq = s.ExpectRequest<ChooseCharacterRequest>(0);
            Assert.That(lordReq.CharacterIds.Count, Is.EqualTo(5), "lord gets two extra options");
            Assert.That(s.RequestFor(1), Is.Null, "others wait for the lord");
            s.Submit(new RespondCommand { PlayerId = 0, OptionIndex = 1 });
            string lordPick = lordReq.CharacterIds[1];
            Assert.That(s.P(0).Character.Id, Is.EqualTo(lordPick));
            for (int i = 1; i < 5; i++)
            {
                var r = s.ExpectRequest<ChooseCharacterRequest>(i);
                Assert.That(r.CharacterIds, Has.Count.EqualTo(3));
                Assert.That(r.CharacterIds, Does.Not.Contain(lordPick));
            }
            for (int i = 1; i < 5; i++) s.Submit(new RespondCommand { PlayerId = i, OptionIndex = 0 });
            s.ExpectRequest<PlayActionRequest>(0);
            var all = s.State.Players.Select(p => p.Character.Id).ToList();
            Assert.That(all.Distinct().Count(), Is.EqualTo(5));
        }

        [Test]
        public void BigTablesShareTheRosterSoEveryoneChooses()
        {
            var roles = new[] { Role.Lord, Role.Loyalist, Role.Loyalist, Role.Rebel, Role.Rebel, Role.Rebel, Role.Rebel, Role.Renegade };
            var setups = roles.Select((r, i) => new PlayerSetup("P" + i) { Role = r }).ToList();
            var s = Scenario.Create(8, c =>
            {
                c.ModeId = IdentityMode.Id;
                c.CharacterSelection = CharacterSelectionMode.Choose;
            }, mode: c => new IdentityMode(c), setups: setups);
            int roster = s.Ctx.Content.Characters.All.Count(ch => ch.Id != s.Ctx.Config.DefaultCharacterId);
            s.ExpectRequest<ChooseCharacterRequest>(0);
            s.Submit(new RespondCommand { PlayerId = 0, OptionIndex = 0 });
            int expected = System.Math.Min(3, (roster - 1) / 7);
            Assert.That(expected, Is.GreaterThanOrEqualTo(1), "test content has enough characters");
            var offered = new List<string>();
            for (int i = 1; i < 8; i++)
            {
                var r = s.ExpectRequest<ChooseCharacterRequest>(i);
                Assert.That(r.CharacterIds, Has.Count.EqualTo(expected), "seat " + i + " gets a fair share of the remaining roster");
                offered.AddRange(r.CharacterIds);
            }
            Assert.That(offered.Distinct().Count(), Is.EqualTo(offered.Count), "no character is offered twice");
            for (int i = 1; i < 8; i++) s.Submit(new RespondCommand { PlayerId = i, OptionIndex = 0 });
            Assert.That(s.State.Players.All(p => p.Character.Id != s.Ctx.Config.DefaultCharacterId), Is.True, "nobody is left with the default character");
        }

        private static void Kill(Scenario s, int killer, int victim) => TestKills.Kill(s, killer, victim);

        [Test]
        public void KillingARebelRewardsThreeCards()
        {
            var s = Create(Five);
            int before = s.P(0).HandCardCount;
            Kill(s, 0, 1);
            Assert.That(s.P(0).HandCardCount, Is.EqualTo(before + 3));
            Assert.That(s.EventsOf<PlayerDiedEvent>().Single().RevealedRole, Is.EqualTo(Role.Rebel));
            Assert.That(s.Engine.CreateSnapshot(3).Players[1].Role, Is.EqualTo(Role.Rebel));
        }

        [Test]
        public void LordKillingLoyalistDiscardsEverything()
        {
            var s = Create(Five);
            s.Give(0, "dodge");
            s.Give(0, "strike");
            s.Equip(0, "war_horse");
            Kill(s, 0, 2);
            Assert.That(s.P(0).HandCardCount, Is.EqualTo(0));
            Assert.That(s.P(0).Equipment.Count, Is.EqualTo(0));
            Assert.That(s.State.IsGameOver, Is.False);
        }

        [Test]
        public void LordDeathWithRebelsAliveMeansRebelsWin()
        {
            var s = Create(Five);
            Kill(s, 1, 0);
            Assert.That(s.State.IsGameOver, Is.True);
            Assert.That(s.State.Result.WinningFaction, Is.EqualTo(Faction.Rebels));
            Assert.That(s.State.Result.WinnerIds, Is.EquivalentTo(new[] { 1, 3 }));
            // Game over reveals every role to everyone.
            Assert.That(s.Engine.CreateSnapshot(-1).Players.Select(p => p.Role), Is.EqualTo(Five));
        }

        [Test]
        public void DeadRebelsStillWinWhenLordFallsWithOthersAlive()
        {
            var s = Create(Five);
            Kill(s, 0, 1);
            Kill(s, 0, 3);
            Assert.That(s.State.IsGameOver, Is.False, "renegade still alive");
            // Renegade kills the lord while the loyalist is alive: rebels (all dead) win.
            Kill(s, 4, 0);
            Assert.That(s.State.Result.WinningFaction, Is.EqualTo(Faction.Rebels));
            Assert.That(s.State.Result.WinnerIds, Is.EquivalentTo(new[] { 1, 3 }));
        }

        [Test]
        public void RenegadeWinsAsLastSurvivor()
        {
            var s = Create(Role.Lord, Role.Rebel, Role.Renegade);
            Kill(s, 0, 1);
            Assert.That(s.State.IsGameOver, Is.False);
            Kill(s, 2, 0);
            Assert.That(s.State.IsGameOver, Is.True);
            Assert.That(s.State.Result.WinningFaction, Is.EqualTo(Faction.Renegade));
            Assert.That(s.State.Result.WinnerIds, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void LordSideWinsWhenRebelsAndRenegadesAreGone()
        {
            var s = Create(Five);
            Kill(s, 0, 1);
            Kill(s, 0, 3);
            Kill(s, 2, 4);
            Assert.That(s.State.IsGameOver, Is.True);
            Assert.That(s.State.Result.WinningFaction, Is.EqualTo(Faction.LordSide));
            Assert.That(s.State.Result.WinnerIds, Is.EquivalentTo(new[] { 0, 2 }));
        }

        [Test]
        public void LoyalistDeathDoesNotEndTheGame()
        {
            var s = Create(Five);
            Kill(s, 1, 2);
            Assert.That(s.State.IsGameOver, Is.False);
            Assert.That(s.EventsOf<PlayerDiedEvent>().Last().RevealedRole, Is.EqualTo(Role.Loyalist));
        }

        [Test]
        public void BotsFinishIdentityGamesAndEveryFactionCanWin()
        {
            var factions = new HashSet<Faction>();
            for (int seed = 1; seed <= 40; seed++)
            {
                var config = new GameModeConfig
                {
                    ModeId = IdentityMode.Id,
                    PlayerCount = 5,
                    CharacterSelection = CharacterSelectionMode.Random,
                    MaxRounds = 60
                };
                var setups = Enumerable.Range(0, 5).Select(i => new PlayerSetup("Bot" + i, true)).ToList();
                var session = new GameSession(new GameEngine(TestContent.Shared, new IdentityMode(config), config, setups, seed), new ManualClock());
                session.Start();
                Assert.That(session.RunUntilHumanInputOrEnd(), Is.True);
                Assert.That(session.State.ValidateInvariants(), Is.Null);
                if (!session.State.Result.IsDraw) factions.Add(session.State.Result.WinningFaction);
            }
            Assert.That(factions, Does.Contain(Faction.LordSide));
            Assert.That(factions, Does.Contain(Faction.Rebels));
        }

        [Test]
        public void IdentityReplicasStayInSync()
        {
            var config = new GameModeConfig { ModeId = IdentityMode.Id, PlayerCount = 8, CharacterSelection = CharacterSelectionMode.Random, MaxRounds = 60 };
            var setups = Enumerable.Range(0, 8).Select(i => new PlayerSetup("Bot" + i, true)).ToList();
            var session = new GameSession(new GameEngine(TestContent.Shared, new IdentityMode(config), config, setups, 21), new ManualClock());
            session.Start();
            var replicas = new Dictionary<int, ClientGameState>();
            for (int v = -1; v < 8; v++)
            {
                int viewer = v;
                replicas[viewer] = session.GetSnapshot(viewer);
                session.AddViewer(viewer, e => Assert.That(replicas[viewer].Apply(e), Is.True));
            }
            session.RunUntilHumanInputOrEnd();
            foreach (var kv in replicas)
            {
                Assert.That(kv.Value.Dump(), Is.EqualTo(session.GetSnapshot(kv.Key).Dump()));
            }
        }
    }
}
