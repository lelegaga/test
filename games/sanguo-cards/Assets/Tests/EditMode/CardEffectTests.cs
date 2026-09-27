using System.Linq;
using NUnit.Framework;
using Sanguo.Core;
using Sanguo.Events;

namespace Sanguo.Tests
{
    public class CardEffectTests
    {
        [Test]
        public void DuelLoserIsFirstToRunOutOfStrikes()
        {
            var s = Scenario.Create(3);
            var duel = s.Give(0, "duel");
            var targetStrike = s.Give(2, "strike");
            Scenario.AssertAccepted(s.Play(0, duel, 2));
            s.ExpectRequest<CardResponseRequest>(2);
            Scenario.AssertAccepted(s.Respond(2, targetStrike));
            // Player 0 has no strike: auto-skip makes them lose immediately.
            Assert.That(s.P(0).Hp, Is.EqualTo(3));
            Assert.That(s.P(2).Hp, Is.EqualTo(4));
            Assert.That(s.EventsOf<DamageAppliedEvent>().Single().SourceId, Is.EqualTo(2));
        }

        [Test]
        public void DuelTargetWithoutStrikeTakesDamage()
        {
            var s = Scenario.Create(3);
            var duel = s.Give(0, "duel");
            s.Play(0, duel, 2);
            Assert.That(s.P(2).Hp, Is.EqualTo(3));
        }

        [Test]
        public void AmbushHitsEveryoneWithoutAStrikeInSeatOrder()
        {
            var s = Scenario.Create(4);
            var ambush = s.Give(0, "ambush");
            var p2Strike = s.Give(2, "strike");
            s.ClearEvents();
            Scenario.AssertAccepted(s.Play(0, ambush));
            // Player 1 cannot respond (auto-skipped) and is hit first; then player 2 is asked.
            Assert.That(s.P(1).Hp, Is.EqualTo(3));
            s.ExpectRequest<CardResponseRequest>(2);
            s.Respond(2, p2Strike);
            Assert.That(s.P(2).Hp, Is.EqualTo(4));
            Assert.That(s.P(3).Hp, Is.EqualTo(3));
            Assert.That(s.P(0).Hp, Is.EqualTo(4));
            var targets = s.EventsOf<TargetSelectedEvent>().Select(e => e.TargetId).ToArray();
            Assert.That(targets, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void ArrowVolleyRequiresDodges()
        {
            var s = Scenario.Create(3);
            var volley = s.Give(1, "arrow_volley");
            var dodge = s.Give(0, "dodge");
            s.EndTurn(0);
            s.Play(1, volley);
            Assert.That(s.P(2).Hp, Is.EqualTo(3), "player 2 had no dodge");
            Assert.That(s.ExpectRequest<CardResponseRequest>(0).RequiredCardId, Is.EqualTo("dodge"));
            s.Respond(0, dodge);
            Assert.That(s.P(0).Hp, Is.EqualTo(4));
        }

        [Test]
        public void SupplyDrawsTwo()
        {
            var s = Scenario.Create(2);
            var supply = s.Give(0, "supply");
            s.Play(0, supply);
            Assert.That(s.P(0).HandCardCount, Is.EqualTo(2));
            Assert.That(supply.Zone, Is.EqualTo(ZoneType.DiscardPile));
        }

        [Test]
        public void SabotageDiscardsChosenEquipment()
        {
            var s = Scenario.Create(4);
            var sabotage = s.Give(0, "sabotage");
            var horse = s.Equip(2, "war_horse");
            s.Give(2, "strike");
            Scenario.AssertAccepted(s.Play(0, sabotage, 2));
            var req = s.ExpectRequest<ChooseCardFromPlayerRequest>(0);
            Assert.That(req.Zones, Is.EqualTo(ZoneMask.All));
            Scenario.AssertRejected(s.Submit(new RespondCommand { PlayerId = 0, PickZone = ZoneType.Equipment, PickIndex = (int)EquipSlot.Weapon }), RejectReason.InvalidOption);
            Scenario.AssertAccepted(s.Submit(new RespondCommand { PlayerId = 0, PickZone = ZoneType.Equipment, PickIndex = (int)EquipSlot.DefensiveMount }));
            Assert.That(horse.Zone, Is.EqualTo(ZoneType.DiscardPile));
            Assert.That(s.P(2).HandCardCount, Is.EqualTo(1));
        }

        [Test]
        public void SabotageNeedsATargetWithCards()
        {
            var s = Scenario.Create(3);
            var sabotage = s.Give(0, "sabotage");
            Scenario.AssertRejected(s.Play(0, sabotage, 1), RejectReason.InvalidTarget);
        }

        [Test]
        public void SeizeOnlyReachesDistanceOne()
        {
            var s = Scenario.Create(5);
            var seize = s.Give(0, "seize");
            s.Give(2, "dodge");
            Scenario.AssertRejected(s.Play(0, seize, 2), RejectReason.TargetOutOfRange);
            s.Equip(0, "swift_horse");
            Scenario.AssertAccepted(s.Play(0, seize, 2));
            s.Submit(new RespondCommand { PlayerId = 0, PickZone = ZoneType.Hand, PickIndex = 0 });
            Assert.That(s.P(0).HandCardCount, Is.EqualTo(1));
            Assert.That(s.P(2).HandCardCount, Is.EqualTo(0));
        }

        [Test]
        public void EquippingReplacesOldCardInSlot()
        {
            var s = Scenario.Create(3);
            var spear = s.Give(0, "long_spear");
            var bow = s.Give(0, "repeating_crossbow");
            Scenario.AssertAccepted(s.Play(0, spear));
            Assert.That(s.P(0).Equipment.Get(EquipSlot.Weapon), Is.SameAs(spear));
            Assert.That(s.Ctx.Rules.GetAttackRange(0), Is.EqualTo(3));
            Scenario.AssertAccepted(s.Play(0, bow));
            Assert.That(s.P(0).Equipment.Get(EquipSlot.Weapon), Is.SameAs(bow));
            Assert.That(spear.Zone, Is.EqualTo(ZoneType.DiscardPile));
            Assert.That(s.Ctx.Rules.GetAttackRange(0), Is.EqualTo(1));
        }

        [Test]
        public void CrossbowRemovesStrikeLimit()
        {
            var s = Scenario.Create(3);
            s.Equip(0, "repeating_crossbow");
            for (int i = 0; i < 3; i++) Scenario.AssertAccepted(s.Play(0, s.Give(0, "strike"), 1));
            Assert.That(s.P(1).Hp, Is.EqualTo(1));
        }

        [Test]
        public void ConfinementSkipsPlayPhaseOnFailedJudgement()
        {
            var s = Scenario.Create(3);
            var trick = s.Give(0, "confinement");
            Scenario.AssertAccepted(s.Play(0, trick, 1));
            Assert.That(trick.Zone, Is.EqualTo(ZoneType.JudgeArea));
            Assert.That(trick.OwnerId, Is.EqualTo(1));
            var second = s.Give(0, "confinement");
            Scenario.AssertRejected(s.Play(0, second, 1), RejectReason.InvalidTarget);
            s.PutOnTop(c => c.Suit == Suit.Spade);
            s.ClearEvents();
            s.EndTurn(0);
            var judgement = s.EventsOf<JudgementEvent>().Single();
            Assert.That(judgement.Success, Is.False);
            Assert.That(s.EventsOf<PhaseSkippedEvent>().Single().Phase, Is.EqualTo(GamePhase.PlayPhase));
            Assert.That(trick.Zone, Is.EqualTo(ZoneType.DiscardPile));
            // Player 1's play phase was skipped: the turn went straight on to player 2.
            Assert.That(s.State.Turn.CurrentPlayerId, Is.EqualTo(2));
        }

        [Test]
        public void ConfinementEscapedWithHeart()
        {
            var s = Scenario.Create(3);
            var trick = s.Give(0, "confinement");
            s.Play(0, trick, 1);
            s.PutOnTop(c => c.Suit == Suit.Heart);
            s.EndTurn(0);
            Assert.That(s.EventsOf<JudgementEvent>().Single().Success, Is.True);
            s.ExpectRequest<PlayActionRequest>(1);
        }
    }

    public class DistanceTests
    {
        [Test]
        public void SeatDistanceUsesShortestWayAroundTheTable()
        {
            var s = Scenario.Create(6);
            var r = s.Ctx.Rules;
            Assert.That(r.GetDistance(0, 1), Is.EqualTo(1));
            Assert.That(r.GetDistance(0, 2), Is.EqualTo(2));
            Assert.That(r.GetDistance(0, 3), Is.EqualTo(3));
            Assert.That(r.GetDistance(0, 4), Is.EqualTo(2));
            Assert.That(r.GetDistance(0, 5), Is.EqualTo(1));
        }

        [Test]
        public void DeadPlayersDoNotCountForDistance()
        {
            var s = Scenario.Create(5);
            s.SetHp(1, 1);
            var strike = s.Give(0, "strike");
            s.Play(0, strike, 1);
            Assert.That(s.P(1).Alive, Is.False);
            Assert.That(s.Ctx.Rules.GetDistance(0, 2), Is.EqualTo(1));
        }

        [Test]
        public void MountsChangeDistance()
        {
            var s = Scenario.Create(6);
            var r = s.Ctx.Rules;
            s.Equip(1, "war_horse");
            Assert.That(r.GetDistance(0, 1), Is.EqualTo(2));
            Assert.That(r.GetDistance(1, 0), Is.EqualTo(1));
            Assert.That(r.IsInAttackRange(0, 1), Is.False);
            s.Equip(0, "swift_horse");
            Assert.That(r.GetDistance(0, 1), Is.EqualTo(1));
            Assert.That(r.GetDistance(0, 3), Is.EqualTo(2));
        }
    }

    public class LogTests
    {
        [Test]
        public void LogReadsLikeTheTable()
        {
            var s = Scenario.Create(3);
            var strike = s.Give(0, "strike");
            s.ClearEvents();
            s.Play(0, strike, 1);
            var names = new Events.GameLogNames(s.Ctx.Content, id => s.P(id).Nickname);
            var lines = s.Events.Select(e => Events.GameLogFormatter.Format(e, names, 0)).Where(l => l != null).ToList();
            Assert.That(lines, Does.Contain("P0 对 P1 使用 【攻击】"));
            Assert.That(lines, Does.Contain("P1 受到 P0 造成的 1 点伤害"));
            Assert.That(lines, Does.Contain("P1 剩余 3 点生命"));
            Assert.That(lines.IndexOf("P1 受到 P0 造成的 1 点伤害"), Is.LessThan(lines.IndexOf("P1 剩余 3 点生命")));
        }

        [Test]
        public void OthersOnlySeeDrawCounts()
        {
            var s = Scenario.Create(2, c => c.DrawPerTurn = 2);
            s.ClearEvents();
            s.EndTurn(0);
            var draw = s.EventsOf<CardDrawnEvent>().Single();
            var names = new Events.GameLogNames(s.Ctx.Content, id => s.P(id).Nickname);
            Assert.That(Events.GameLogFormatter.Format(draw.ProjectFor(0), names, 0), Is.EqualTo("P1 摸取 2 张牌"));
            Assert.That(Events.GameLogFormatter.Format(draw.ProjectFor(1), names, 1), Does.StartWith("P1 摸取 【"));
        }
    }
}
