using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Effects;

namespace Sanguo.Cards
{
    public enum CardType : byte
    {
        Basic = 0,
        Trick = 1,
        Equipment = 2
    }

    /// <summary>
    /// Card definition ("what the card does"). Loaded from data (Resources/Data/cards.json) and shared
    /// by every physical copy. Suit and number belong to the physical copy (<see cref="CardInstance"/>)
    /// because one definition appears many times in a deck with different suits.
    ///
    /// Behaviour is expressed only through <see cref="Effects"/> (instant effects applied per target),
    /// never by checking the card name in engine code.
    /// </summary>
    public abstract class CardBase
    {
        protected CardBase(string cardId, string cardName)
        {
            CardId = cardId;
            CardName = cardName;
        }

        public string CardId { get; }
        public string CardName { get; }
        public abstract CardType CardType { get; }
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;

        public TargetRule TargetRule { get; set; } = TargetRule.SelfOnly;
        public RangeRequirement Range { get; set; } = RangeRequirement.None;

        /// <summary>Instant effects, applied in order to each target when the card resolves.</summary>
        public List<ICardEffect> Effects { get; } = new List<ICardEffect>();

        /// <summary>False for response-only cards such as Dodge.</summary>
        public bool CanUseActively { get; set; } = true;

        /// <summary>Key of the per-turn usage counter (for example "strike"); null means unlimited.</summary>
        public string UsageLimitKey { get; set; }

        /// <summary>Base per-turn limit for <see cref="UsageLimitKey"/>; modifiers may raise it.</summary>
        public int BaseUsageLimit { get; set; }

        private readonly HashSet<string> _tags = new HashSet<string>();

        /// <summary>Free-form tags used by rules, skills and AI (for example "attack", "aoe", "heal").</summary>
        public IEnumerable<string> Tags => _tags;

        public void AddTag(string tag)
        {
            if (!string.IsNullOrEmpty(tag)) _tags.Add(tag);
        }

        public bool HasTag(string tag) => _tags.Contains(tag);

        public override string ToString() => CardName + "(" + CardId + ")";
    }

    public sealed class BasicCard : CardBase
    {
        public BasicCard(string cardId, string cardName) : base(cardId, cardName)
        {
        }

        public override CardType CardType => CardType.Basic;
    }

    public sealed class TrickCard : CardBase
    {
        public TrickCard(string cardId, string cardName) : base(cardId, cardName)
        {
        }

        public override CardType CardType => CardType.Trick;

        /// <summary>
        /// Delayed tricks are placed in the target's judge area and resolve in their judge phase via
        /// <see cref="Judge"/> and <see cref="JudgeFailEffects"/>.
        /// </summary>
        public bool IsDelayed { get; set; }

        public JudgeRule Judge { get; set; }

        /// <summary>Effects applied to the judged player when the judgement fails.</summary>
        public List<ICardEffect> JudgeFailEffects { get; } = new List<ICardEffect>();
    }

    public sealed class EquipmentCard : CardBase
    {
        public EquipmentCard(string cardId, string cardName, EquipSlot slot) : base(cardId, cardName)
        {
            Slot = slot;
        }

        public override CardType CardType => CardType.Equipment;
        public EquipSlot Slot { get; }

        /// <summary>Effects active while the card sits in its owner's equipment area.</summary>
        public List<IContinuousEffect> ContinuousEffects { get; } = new List<IContinuousEffect>();
    }

    /// <summary>Success test for a judgement card.</summary>
    public sealed class JudgeRule
    {
        private readonly HashSet<Suit> _successSuits = new HashSet<Suit>();

        public JudgeRule(IEnumerable<Suit> successSuits)
        {
            foreach (var s in successSuits) _successSuits.Add(s);
        }

        public IEnumerable<Suit> SuccessSuits => _successSuits;

        public bool IsSuccess(CardInstance judgeCard) => judgeCard != null && _successSuits.Contains(judgeCard.Suit);
    }
}
