using System.Linq;
using NUnit.Framework;
using Sanguo.Core;
using Sanguo.Data;
using Sanguo.Game;
using Sanguo.Skills;

namespace Sanguo.Tests
{
    /// <summary>Test-only active skill: unlimited uses, 1 damage to any other player.</summary>
    internal sealed class SmiteSkill : ActiveSkillBase
    {
        public const string Id = "test_smite";

        public SmiteSkill() : base(new SkillDefinition { Id = Id, Name = "天罚", ClassName = "Test", Params = JsonValue.Parse("{\"usesPerPhase\":0}") })
        {
        }

        public override ValidationResult ValidateActivation(GameContext ctx, PlayerState owner, UseSkillCommand cmd)
        {
            if (cmd.CardIds.Length != 0 || cmd.TargetIds.Length != 1 || cmd.TargetIds[0] == owner.PlayerId)
                return ValidationResult.Fail(RejectReason.WrongTargetCount);
            return ValidationResult.Ok;
        }

        public override GameAction CreateActivationAction(GameContext ctx, PlayerState owner, UseSkillCommand cmd)
        {
            return new DamageAction(owner.PlayerId, cmd.TargetIds[0], 1, null);
        }
    }

    /// <summary>Drives a scenario so that one player kills another on their own turn.</summary>
    internal static class TestKills
    {
        private static GameContent _content;

        /// <summary>Real content plus the test smite skill.</summary>
        public static GameContent Content
        {
            get
            {
                if (_content == null)
                {
                    _content = TestContent.LoadFresh();
                    _content.Skills.Add(new SmiteSkill());
                }
                return _content;
            }
        }

        /// <summary>Ends turns (answering discards by default) until it is <paramref name="playerId"/>'s play phase.</summary>
        public static void AdvanceTo(Scenario s, int playerId)
        {
            for (int guard = 0; guard < 100; guard++)
            {
                var req = s.Request;
                if (req is PlayActionRequest && req.PlayerId == playerId) return;
                if (req == null) Assert.Fail("No open request while advancing turns.");
                switch (req)
                {
                    case PlayActionRequest play:
                        s.EndTurn(play.PlayerId);
                        break;
                    case DiscardRequest d:
                        s.Submit(d.CreateDefaultResponse(s.Ctx));
                        break;
                    default:
                        s.Submit(req.CreateDefaultResponse(s.Ctx));
                        break;
                }
            }
            Assert.Fail("Could not reach the turn of player " + playerId);
        }

        public static void Kill(Scenario s, int killer, int victim)
        {
            AdvanceTo(s, killer);
            s.SetHp(victim, 1);
            s.Ctx.Mutator.MoveCards(s.P(victim).HandCards.ToList(), ZoneType.DiscardPile, -1, MoveReason.Discard);
            s.Drain();
            if (s.P(killer).FindSkill(SmiteSkill.Id) == null) s.GrantSkill(killer, SmiteSkill.Id);
            Scenario.AssertAccepted(s.UseSkill(killer, SmiteSkill.Id, null, new[] { victim }));
            // Nobody rescues: pass any rescue request (someone may have drawn a heal).
            for (int guard = 0; guard < 50 && s.Request is CardResponseRequest r && r.Purpose == "rescue"; guard++) s.Pass(r.PlayerId);
            Assert.That(s.P(victim).Alive, Is.False, "victim should be dead");
        }
    }
}
