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
    /// <summary>Mode with secret roles, to exercise role hiding before identity mode exists.</summary>
    internal sealed class SecretRoleTestMode : GameModeBase
    {
        public SecretRoleTestMode(GameModeConfig config) : base(config)
        {
            VictoryConditions.Add(new LastFactionStandingCondition());
        }

        public override string ModeId => "secret_test";
        public override string DisplayName => "secret";

        public override void SetupPlayers(GameSetupContext setup)
        {
            SeatInOrder(setup);
            foreach (var p in setup.Players)
            {
                setup.SetRole(p, p.PlayerId % 2 == 0 ? Role.Rebel : Role.Loyalist, false);
                setup.SetFaction(p, Faction.Solo);
            }
        }
    }

    /// <summary>Clients must never be able to read other players' hands or hidden roles.</summary>
    public class HiddenInfoTests
    {
        [Test]
        public void SnapshotHidesOtherHands()
        {
            var s = Scenario.Create(4, c => c.StartingHandSize = 4);
            for (int viewer = 0; viewer < 4; viewer++)
            {
                var snap = s.Engine.CreateSnapshot(viewer);
                foreach (var p in snap.Players)
                {
                    Assert.That(p.HandCount, Is.EqualTo(s.P(p.PlayerId).HandCardCount));
                    if (p.PlayerId == viewer) Assert.That(p.HandCards.Select(c => c.InstanceId), Is.EquivalentTo(s.P(viewer).HandCards.Select(c => c.InstanceId)));
                    else Assert.That(p.HandCards, Is.Null, "viewer " + viewer + " can see hand of " + p.PlayerId);
                }
            }
            var spectator = s.Engine.CreateSnapshot(SnapshotBuilder.Spectator);
            Assert.That(spectator.Players.All(p => p.HandCards == null), Is.True);
        }

        [Test]
        public void SnapshotHidesSecretRolesUntilRevealed()
        {
            var s = Scenario.Create(4, mode: c => new SecretRoleTestMode(c));
            var snap = s.Engine.CreateSnapshot(0);
            Assert.That(snap.Players[0].Role, Is.EqualTo(Role.Rebel));
            Assert.That(snap.Players[1].Role, Is.EqualTo(Role.Unknown));
            Assert.That(snap.Players[2].Role, Is.EqualTo(Role.Unknown));
            Assert.That(s.Engine.CreateSnapshot(-1).Players.All(p => p.Role == Role.Unknown), Is.True);

            s.SetHp(1, 1);
            var strike = s.Give(0, "strike");
            s.Play(0, strike, 1);
            var died = s.EventsOf<PlayerDiedEvent>().Single();
            Assert.That(died.RevealedRole, Is.EqualTo(Role.Loyalist));
            Assert.That(s.Engine.CreateSnapshot(2).Players[1].Role, Is.EqualTo(Role.Loyalist));
            Assert.That(s.Engine.CreateSnapshot(2).Players[3].Role, Is.EqualTo(Role.Unknown));
        }

        [Test]
        public void StolenCardIsVisibleOnlyToTheTwoPlayersInvolved()
        {
            var s = Scenario.Create(4);
            var seize = s.Give(0, "seize");
            var victimCard = s.Give(1, "heal");
            s.ClearEvents();
            Scenario.AssertAccepted(s.Play(0, seize, 1));
            var pick = s.ExpectRequest<ChooseCardFromPlayerRequest>(0);
            Assert.That(pick.TargetId, Is.EqualTo(1));
            Scenario.AssertAccepted(s.Submit(new RespondCommand { PlayerId = 0, PickZone = ZoneType.Hand, PickIndex = 0 }));
            Assert.That(victimCard.OwnerId, Is.EqualTo(0));
            var move = s.EventsOf<CardMovedEvent>().Single(e => e.Reason == MoveReason.Steal);
            Assert.That(((CardMoveEvent)move.ProjectFor(0)).Cards, Is.Not.Null);
            Assert.That(((CardMoveEvent)move.ProjectFor(1)).Cards, Is.Not.Null);
            Assert.That(((CardMoveEvent)move.ProjectFor(2)).Cards, Is.Null);
            Assert.That(((CardMoveEvent)move.ProjectFor(-1)).Cards, Is.Null);
        }

        [Test]
        public void PrivateRequestDetailsGoOnlyToTheAskedPlayer()
        {
            var s = Scenario.Create(3, c => c.CharacterSelection = CharacterSelectionMode.Choose, start: true);
            var opened = s.EventsOf<RequestOpenedEvent>().Where(e => e.Info.Kind == RequestKind.ChooseCharacter).ToList();
            Assert.That(opened.Count, Is.EqualTo(3));
            foreach (var e in opened)
            {
                int owner = e.Info.PlayerId;
                Assert.That(((RequestOpenedEvent)e.ProjectFor(owner)).Info.Options, Has.Count.EqualTo(3));
                for (int v = -1; v < 3; v++)
                {
                    if (v == owner) continue;
                    var info = ((RequestOpenedEvent)e.ProjectFor(v)).Info;
                    Assert.That(info.Options, Is.Null);
                    Assert.That(info.HasPrivateDetails, Is.False);
                }
            }
        }

        [Test]
        public void FullAIGameNeverLeaksHiddenCardsToOtherViewers()
        {
            var config = Scenario.DefaultConfig(5);
            config.StartingHandSize = 4;
            config.DrawPerTurn = 2;
            config.CharacterSelection = CharacterSelectionMode.Random;
            config.ShuffleSeats = true;
            var setups = Enumerable.Range(0, 5).Select(i => new PlayerSetup("Bot" + i, true)).ToList();
            var engine = new GameEngine(TestContent.Shared, new SecretRoleTestMode(config), config, setups, 11);
            var session = new GameSession(engine, new ManualClock());
            var leaks = new List<string>();
            for (int v = -1; v < 5; v++)
            {
                int viewer = v;
                session.AddViewer(viewer, e =>
                {
                    if (e is CardMoveEvent m && m.Cards != null && !m.IsPublic && (m.KnownTo == null || !m.KnownTo.Contains(viewer)))
                        leaks.Add("viewer " + viewer + " saw " + m);
                    if (e is RequestOpenedEvent r && r.Info.HasPrivateDetails && r.Info.PlayerId != viewer)
                        leaks.Add("viewer " + viewer + " saw private request " + r.Info.Describe());
                });
            }
            session.Start();
            session.RunUntilHumanInputOrEnd();
            Assert.That(engine.IsGameOver, Is.True);
            Assert.That(leaks, Is.Empty);
        }
    }
}
