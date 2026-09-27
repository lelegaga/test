using System;
using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Skills;
using Sanguo.Skills.Builtin;

namespace Sanguo.AI
{
    /// <summary>
    /// Teaches the AI how to use one active skill class (candidate generation + scoring). Kept out of
    /// the skill classes so rules code stays free of AI concerns. Skills without an advisor are simply
    /// never used actively by bots.
    /// </summary>
    public abstract class AISkillAdvisor
    {
        public abstract void AddCandidates(PlayerPerspective view, SkillInstance skill, AIActionEvaluator evaluator, List<AICandidate> output);
    }

    public sealed class AISkillAdvisorRegistry
    {
        private readonly Dictionary<Type, AISkillAdvisor> _byType = new Dictionary<Type, AISkillAdvisor>();

        public void Register<TSkill>(AISkillAdvisor advisor) where TSkill : SkillBase
        {
            _byType[typeof(TSkill)] = advisor;
        }

        public AISkillAdvisor Find(SkillBase skill)
        {
            return skill != null && _byType.TryGetValue(skill.GetType(), out var a) ? a : null;
        }

        public static AISkillAdvisorRegistry CreateDefault()
        {
            var r = new AISkillAdvisorRegistry();
            r.Register<DiscardToDamageSkill>(new DiscardToDamageAdvisor());
            r.Register<AreaDamageSkill>(new AreaDamageAdvisor());
            return r;
        }
    }

    internal sealed class DiscardToDamageAdvisor : AISkillAdvisor
    {
        public override void AddCandidates(PlayerPerspective view, SkillInstance skill, AIActionEvaluator evaluator, List<AICandidate> output)
        {
            var s = (DiscardToDamageSkill)skill.Skill;
            var self = view.Self;
            if (self.HandCards.Count < s.Cost) return;
            var cards = new List<Cards.CardInstance>(self.HandCards);
            cards.Sort((a, b) => evaluator.KeepValue(view, a).CompareTo(evaluator.KeepValue(view, b)));
            var cost = new int[s.Cost];
            float costValue = 0f;
            for (int i = 0; i < s.Cost; i++)
            {
                cost[i] = cards[i].InstanceId;
                costValue += evaluator.KeepValue(view, cards[i]) * 0.6f;
            }
            foreach (var p in view.Players)
            {
                if (!p.Alive || p.PlayerId == self.PlayerId || !view.Rules.IsInAttackRange(self.PlayerId, p.PlayerId)) continue;
                float score = evaluator.ScoreDamage(view, p.PlayerId, s.Damage) - costValue;
                var cmd = new UseSkillCommand { SkillId = s.SkillId, CardIds = (int[])cost.Clone(), TargetIds = new[] { p.PlayerId } };
                output.Add(new AICandidate(cmd, score, s.SkillId + "->" + p.PlayerId));
            }
        }
    }

    internal sealed class AreaDamageAdvisor : AISkillAdvisor
    {
        public override void AddCandidates(PlayerPerspective view, SkillInstance skill, AIActionEvaluator evaluator, List<AICandidate> output)
        {
            var s = (AreaDamageSkill)skill.Skill;
            if (skill.UsedUp) return;
            float total = 0f;
            foreach (var p in view.Players)
                if (p.Alive && p.PlayerId != view.ViewerId) total += evaluator.ScoreDamage(view, p.PlayerId, s.Damage);
            // A once-per-game skill should be worth a lot before it is spent.
            float score = total - 12f;
            output.Add(new AICandidate(new UseSkillCommand { SkillId = s.SkillId }, score, s.SkillId));
        }
    }
}
