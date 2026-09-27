using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.Game;
using Sanguo.Skills;

namespace Sanguo.Tests
{
    public class SkillTests
    {
        [Test]
        public void EndureDrawsAfterDamageWhenAccepted()
        {
            var s = Scenario.Create(3);
            s.GrantSkill(1, "endure");
            var strike = s.Give(0, "strike");
            s.Play(0, strike, 1);
            var confirm = s.ExpectRequest<ConfirmRequest>(1);
            Assert.That(confirm.SkillId, Is.EqualTo("endure"));
            int before = s.P(1).HandCardCount;
            Scenario.AssertAccepted(s.Confirm(1, true));
            Assert.That(s.P(1).HandCardCount, Is.EqualTo(before + 1));
            Assert.That(s.EventsOf<SkillActivatedEvent>().Any(e => e.SkillId == "endure" && e.PlayerId == 1), Is.True);
        }

        [Test]
        public void DecliningOptionalSkillDoesNothing()
        {
            var s = Scenario.Create(3);
            s.GrantSkill(1, "endure");
            var strike = s.Give(0, "strike");
            s.Play(0, strike, 1);
            int before = s.P(1).HandCardCount;
            s.Confirm(1, false);
            Assert.That(s.P(1).HandCardCount, Is.EqualTo(before));
            Assert.That(s.EventsOf<SkillActivatedEvent>().Any(e => e.SkillId == "endure"), Is.False);
        }

        [Test]
        public void StrategistDrawsExtraCard()
        {
            var s = Scenario.Create(2, c => c.DrawPerTurn = 2);
            s.GrantSkill(1, "strategist");
            int before = s.P(1).HandCardCount;
            s.EndTurn(0);
            Assert.That(s.P(1).HandCardCount, Is.EqualTo(before + 3));
        }

        [Test]
        public void ValorAllowsRepeatedStrikes()
        {
            var s = Scenario.Create(3);
            s.GrantSkill(0, "valor");
            var a = s.Give(0, "strike");
            var b = s.Give(0, "strike");
            Scenario.AssertAccepted(s.Play(0, a, 1));
            Scenario.AssertAccepted(s.Play(0, b, 1));
            Assert.That(s.P(1).Hp, Is.EqualTo(2));
        }

        [Test]
        public void HorsemanshipShortensDistance()
        {
            var s = Scenario.Create(5);
            Assert.That(s.Ctx.Rules.GetDistance(0, 2), Is.EqualTo(2));
            s.GrantSkill(0, "horsemanship");
            Assert.That(s.Ctx.Rules.GetDistance(0, 2), Is.EqualTo(1));
            Assert.That(s.Ctx.Rules.GetDistance(0, 1), Is.EqualTo(1), "distance never drops below 1");
            Assert.That(s.Ctx.Rules.GetDistance(2, 0), Is.EqualTo(2), "only outgoing distance changes");
        }

        [Test]
        public void IronWallCapsDamage()
        {
            var s = Scenario.Create(3, content: TestCards.WithHeavyStrike());
            var heavy = s.Give(0, TestCards.HeavyStrike);
            s.Play(0, heavy, 1);
            Assert.That(s.P(1).Hp, Is.EqualTo(2), "heavy strike deals 2 without the skill");
            s.GrantSkill(2, "iron_wall");
            s.EndTurn(0);
            s.EndTurn(1);
            var heavy2 = s.Give(2, TestCards.HeavyStrike);
            s.Play(2, heavy2, 0);
            Assert.That(s.P(0).Hp, Is.EqualTo(2));
            // Iron wall protects its owner.
            var heavy3 = s.Give(0, TestCards.HeavyStrike);
            s.EndTurn(2);
            s.Play(0, heavy3, 2);
            Assert.That(s.P(2).Hp, Is.EqualTo(3));
        }

        [Test]
        public void RaidDiscardsTwoCardsToDealDamage()
        {
            var s = Scenario.Create(3);
            s.GrantSkill(0, "raid");
            var c1 = s.Give(0, "dodge");
            var c2 = s.Give(0, "supply");
            Scenario.AssertRejected(s.UseSkill(0, "raid", new[] { c1.InstanceId }, new[] { 1 }), RejectReason.WrongCardCount);
            Scenario.AssertAccepted(s.UseSkill(0, "raid", new[] { c1.InstanceId, c2.InstanceId }, new[] { 1 }));
            Assert.That(s.P(1).Hp, Is.EqualTo(3));
            Assert.That(c1.Zone, Is.EqualTo(ZoneType.DiscardPile));
            var c3 = s.Give(0, "dodge");
            var c4 = s.Give(0, "dodge");
            Scenario.AssertRejected(s.UseSkill(0, "raid", new[] { c3.InstanceId, c4.InstanceId }, new[] { 1 }), RejectReason.SkillUnavailable);
        }

        [Test]
        public void LastStandIsUsableOncePerGame()
        {
            var s = Scenario.Create(4);
            s.GrantSkill(0, "last_stand");
            Scenario.AssertAccepted(s.UseSkill(0, "last_stand"));
            Assert.That(s.P(1).Hp, Is.EqualTo(3));
            Assert.That(s.P(2).Hp, Is.EqualTo(3));
            Assert.That(s.P(3).Hp, Is.EqualTo(3));
            Assert.That(s.P(0).Hp, Is.EqualTo(4));
            Assert.That(s.P(0).FindSkill("last_stand").UsedUp, Is.True);
            Assert.That(s.EventsOf<SkillStateChangedEvent>().Any(e => e.SkillId == "last_stand" && e.UsedUp), Is.True);
            s.EndTurn(0);
            s.EndTurn(1);
            s.EndTurn(2);
            s.EndTurn(3);
            Scenario.AssertRejected(s.UseSkill(0, "last_stand"), RejectReason.SkillUnavailable);
        }

        [Test]
        public void DragonDanceConvertsStrikeAndDodge()
        {
            var s = Scenario.Create(3, c => c.AutoSkipImpossibleResponses = false);
            s.GrantSkill(1, "dragon_dance");
            var strike = s.Give(0, "strike");
            var myStrike = s.Give(1, "strike");
            s.Play(0, strike, 1);
            // Plain strike is not a dodge...
            Scenario.AssertRejected(s.Respond(1, myStrike), RejectReason.InvalidCard);
            // ...but with the skill it is.
            Scenario.AssertAccepted(s.Submit(new RespondCommand { PlayerId = 1, CardIds = new[] { myStrike.InstanceId }, SkillId = "dragon_dance" }));
            Assert.That(s.P(1).Hp, Is.EqualTo(4));
            var played = s.EventsOf<CardPlayedEvent>().Last(e => e.UserId == 1);
            Assert.That(played.UsedAsCardId, Is.EqualTo("dodge"));
            Assert.That(played.Cards[0].CardId, Is.EqualTo("strike"));

            s.EndTurn(0);
            var dodge = s.Give(1, "dodge");
            Scenario.AssertAccepted(s.PlayAs(1, dodge, "dragon_dance", "strike", 2));
            s.Pass(2);
            Assert.That(s.P(2).Hp, Is.EqualTo(3));
            Scenario.AssertRejected(s.PlayAs(1, s.Give(1, "heal"), "dragon_dance", "strike", 2), RejectReason.InvalidCard);
        }

        [Test]
        public void LegacyLetsDyingOwnerGiveCards()
        {
            var s = Scenario.Create(3);
            s.GrantSkill(1, "legacy");
            s.SetHp(1, 1);
            var strike = s.Give(0, "strike");
            s.Play(0, strike, 1);
            s.ExpectRequest<ConfirmRequest>(1);
            s.Confirm(1, true);
            var choose = s.ExpectRequest<ChooseTargetsRequest>(1);
            Assert.That(choose.Candidates, Is.EquivalentTo(new[] { 0, 2 }));
            Scenario.AssertAccepted(s.Submit(new SelectTargetCommand { PlayerId = 1, TargetIds = new[] { 2 } }));
            Assert.That(s.P(2).HandCardCount, Is.EqualTo(3));
            Assert.That(s.P(1).Alive, Is.False);
        }

        [Test]
        public void BenevolenceDrawsAfterRescuingSomeone()
        {
            var s = Scenario.Create(3);
            s.GrantSkill(2, "benevolence");
            s.SetHp(1, 1);
            var strike = s.Give(0, "strike");
            var heal = s.Give(2, "heal");
            s.Play(0, strike, 1);
            s.Respond(2, heal);
            s.ExpectRequest<ConfirmRequest>(2);
            s.Confirm(2, true);
            Assert.That(s.P(2).HandCardCount, Is.EqualTo(1));
            Assert.That(s.P(1).Hp, Is.EqualTo(1));
        }
    }

    /// <summary>A pathological skill that reacts to its own effect.</summary>
    internal sealed class DrawOnDrawSkill : TriggerSkillBase
    {
        public DrawOnDrawSkill() : base(new SkillDefinition { Id = "test_draw_on_draw", Name = "无限摸牌", ClassName = "Test", Locked = true }, TriggerTiming.OnDraw)
        {
        }

        public override bool CanTrigger(GameContext ctx, PlayerState owner, TriggerEventArgs args) => args.PlayerId == owner.PlayerId;

        public override GameAction CreateTriggerAction(GameContext ctx, PlayerState owner, TriggerEventArgs args) => new DrawAction(owner.PlayerId, 1);
    }

    public class TriggerRecursionTests
    {
        [Test]
        public void SelfTriggeringSkillIsBounded()
        {
            var content = TestContent.LoadFresh();
            content.Skills.Add(new DrawOnDrawSkill());
            var s = Scenario.Create(2, c => c.DrawPerTurn = 1, content: content);
            s.GrantSkill(1, "test_draw_on_draw");
            s.EndTurn(0);
            // Draw phase draws 1, then the chain is cut at the maximum trigger depth.
            Assert.That(s.P(1).HandCardCount, Is.EqualTo(1 + SkillTriggerManager.MaxTriggerDepth));
            Assert.That(s.Ctx.Triggers.SuppressedFirings, Is.EqualTo(1));
            s.ExpectRequest<PlayActionRequest>(1);
        }
    }
}
