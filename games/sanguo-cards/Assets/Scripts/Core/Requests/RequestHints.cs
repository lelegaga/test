using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Skills;

namespace Sanguo.Core
{
    /// <summary>
    /// Computes the private UI hints attached to requests (usable cards, legal targets, usable
    /// skills). Hints are advisory: the server still validates every command.
    /// </summary>
    public static class RequestHints
    {
        public static List<PlayHint> PlayableCards(GameContext ctx, int playerId)
        {
            var hints = new List<PlayHint>();
            var p = ctx.GetPlayer(playerId);
            if (p == null) return hints;
            foreach (var card in p.HandCards)
            {
                TryAdd(ctx, p, card, card.Definition, null, hints);
                foreach (var si in p.Skills)
                {
                    if (si.Disabled) continue;
                    foreach (var asId in si.Skill.ConvertibleCardIds)
                    {
                        if (asId == card.CardId || !ctx.Content.Cards.TryGet(asId, out var asDef)) continue;
                        TryAdd(ctx, p, card, asDef, si.Skill.SkillId, hints);
                    }
                }
            }
            return hints;
        }

        private static void TryAdd(GameContext ctx, PlayerState p, CardInstance card, CardBase def, string skillId, List<PlayHint> hints)
        {
            string asId = skillId != null ? def.CardId : null;
            if (!ctx.Rules.CanUseCard(p.PlayerId, card.InstanceId, skillId, asId).IsValid) return;
            var hint = new PlayHint
            {
                CardInstanceId = card.InstanceId,
                AsCardId = def.CardId,
                SkillId = skillId,
                NeedsTargets = def.TargetRule.RequiresSelection
            };
            if (hint.NeedsTargets)
            {
                hint.MinTargets = def.TargetRule.MinTargets;
                hint.MaxTargets = ctx.Rules.GetMaxTargets(p, def);
                ctx.Rules.GetLegalTargets(p.PlayerId, def, hint.LegalTargets);
            }
            else
            {
                ctx.Rules.GetAutoTargets(p, def, hint.LegalTargets);
            }
            hints.Add(hint);
        }

        public static List<SkillHint> UsableSkills(GameContext ctx, int playerId)
        {
            var hints = new List<SkillHint>();
            var p = ctx.GetPlayer(playerId);
            if (p == null) return hints;
            foreach (var si in p.Skills)
            {
                if (si.Disabled || !(si.Skill is ActiveSkillBase active) || !active.CanActivate(ctx, p, si)) continue;
                var hint = new SkillHint
                {
                    SkillId = active.SkillId,
                    MinCards = active.MinCards,
                    MaxCards = active.MaxCards,
                    MinTargets = active.MinTargets,
                    MaxTargets = active.MaxTargets
                };
                active.GetLegalTargets(ctx, p, hint.LegalTargets);
                hints.Add(hint);
            }
            return hints;
        }

        /// <summary>Hand cards that can answer a card response (directly or through a conversion skill).</summary>
        public static List<PlayHint> ResponseCards(GameContext ctx, int playerId, string requiredCardId)
        {
            var hints = new List<PlayHint>();
            var p = ctx.GetPlayer(playerId);
            if (p == null) return hints;
            foreach (var card in p.HandCards)
            {
                if (ctx.Rules.CanCardServeAs(p, card, requiredCardId, null))
                    hints.Add(new PlayHint { CardInstanceId = card.InstanceId, AsCardId = requiredCardId });
                foreach (var si in p.Skills)
                {
                    if (si.Disabled || card.CardId == requiredCardId) continue;
                    if (ctx.Rules.CanCardServeAs(p, card, requiredCardId, si.Skill.SkillId))
                        hints.Add(new PlayHint { CardInstanceId = card.InstanceId, AsCardId = requiredCardId, SkillId = si.Skill.SkillId });
                }
            }
            return hints;
        }
    }
}
