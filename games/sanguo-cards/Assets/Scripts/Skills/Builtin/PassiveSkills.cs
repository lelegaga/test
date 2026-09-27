using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Core;

namespace Sanguo.Skills.Builtin
{
    /// <summary>"Draw N more cards in your draw phase." (class ExtraDraw)</summary>
    public sealed class ExtraDrawSkill : PassiveSkillBase
    {
        public ExtraDrawSkill(SkillDefinition d) : base(d)
        {
        }

        public override void CollectModifiers(PlayerState owner, ModifierKind kind, List<Modifier> output)
        {
            if (kind == ModifierKind.DrawPhaseCount)
                output.Add(new Modifier(kind, ModifierOp.Add, Definition.Params.GetInt("amount", 1)));
        }
    }

    /// <summary>"Your per-turn limit for [key] is raised by N" (e.g. unlimited strikes). (class UsageLimitBonus)</summary>
    public sealed class UsageLimitBonusSkill : PassiveSkillBase
    {
        public UsageLimitBonusSkill(SkillDefinition d) : base(d)
        {
        }

        public override void CollectModifiers(PlayerState owner, ModifierKind kind, List<Modifier> output)
        {
            if (kind == ModifierKind.CardUsageLimit)
                output.Add(new Modifier(kind, ModifierOp.Add, Definition.Params.GetInt("amount", 1), Definition.Params.GetString("key", "strike")));
        }
    }

    /// <summary>Distance changes (outgoing: your distance to others; incoming: others' distance to you). (class DistanceBonus)</summary>
    public sealed class DistanceBonusSkill : PassiveSkillBase
    {
        public DistanceBonusSkill(SkillDefinition d) : base(d)
        {
        }

        public override void CollectModifiers(PlayerState owner, ModifierKind kind, List<Modifier> output)
        {
            int outgoing = Definition.Params.GetInt("outgoing");
            int incoming = Definition.Params.GetInt("incoming");
            if (kind == ModifierKind.OutgoingDistance && outgoing != 0) output.Add(new Modifier(kind, ModifierOp.Add, outgoing));
            if (kind == ModifierKind.IncomingDistance && incoming != 0) output.Add(new Modifier(kind, ModifierOp.Add, incoming));
        }
    }

    /// <summary>
    /// "You may use/play card A as card B" for each [A, B] pair in params.pairs. With
    /// params.responseOnly the conversion only works for responses. (class CardConversion)
    /// </summary>
    public sealed class CardConversionSkill : PassiveSkillBase
    {
        private readonly List<KeyValuePair<string, string>> _pairs = new List<KeyValuePair<string, string>>();
        private readonly List<string> _targets = new List<string>();

        public CardConversionSkill(SkillDefinition d) : base(d)
        {
            foreach (var pair in d.Params["pairs"].Items)
            {
                string from = pair[0].AsString();
                string to = pair[1].AsString();
                if (from == null || to == null) continue;
                _pairs.Add(new KeyValuePair<string, string>(from, to));
                if (!_targets.Contains(to)) _targets.Add(to);
            }
        }

        public override bool IsLocked => false;
        private bool ResponseOnly => Definition.Params.GetBool("responseOnly");

        public override IEnumerable<string> ConvertibleCardIds => _targets;

        public override bool CanConvert(GameContext ctx, PlayerState owner, CardInstance card, string asCardId, bool forResponse)
        {
            if (card == null || (!forResponse && ResponseOnly)) return false;
            foreach (var p in _pairs)
                if (p.Key == card.CardId && p.Value == asCardId) return true;
            return false;
        }
    }
}
