using System.Collections.Generic;

namespace Sanguo.Cards
{
    /// <summary>
    /// One use of a card (or of a skill treating cards as a card). Shared with triggers, which may
    /// cancel individual targets or change the damage bonus before effects resolve.
    /// </summary>
    public sealed class CardUse
    {
        public CardUse(int useId, int userId, CardBase definition)
        {
            UseId = useId;
            UserId = userId;
            Definition = definition;
        }

        public int UseId { get; }
        public int UserId { get; }

        /// <summary>The card as it is being used (may differ from the physical card when converted).</summary>
        public CardBase Definition { get; }

        /// <summary>Physical cards moved to the processing zone for this use (may be empty for virtual uses).</summary>
        public List<CardInstance> PhysicalCards { get; } = new List<CardInstance>();

        public List<int> Targets { get; } = new List<int>();

        private readonly List<int> _cancelled = new List<int>();

        /// <summary>Skill id that converted the physical cards into <see cref="Definition"/>, if any.</summary>
        public string ConversionSkillId { get; set; }

        public bool IsResponse { get; set; }

        /// <summary>Extra damage applied by damage effects of this use (e.g. from a buff).</summary>
        public int DamageBonus { get; set; }

        /// <summary>Primary physical card or null.</summary>
        public CardInstance Card => PhysicalCards.Count > 0 ? PhysicalCards[0] : null;

        public void CancelTarget(int playerId)
        {
            if (!_cancelled.Contains(playerId)) _cancelled.Add(playerId);
        }

        public bool IsTargetCancelled(int playerId) => _cancelled.Contains(playerId);
    }
}
