using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Core;

namespace Sanguo.Skills
{
    /// <summary>Points in resolution where skills may react.</summary>
    public enum TriggerTiming : byte
    {
        OnGameStart = 0,
        OnTurnStart = 1,
        /// <summary>Before drawing in the draw phase; Amount is the draw count and may be changed.</summary>
        OnDrawPhase = 2,
        /// <summary>After a player drew cards (Cards/Amount).</summary>
        OnDraw = 3,
        /// <summary>After a card was used or played as a response (CardUse; IsResponse tells which).</summary>
        OnCardPlayed = 4,
        /// <summary>A player became the target of a card use (PlayerId = target). Skills may cancel the target.</summary>
        OnCardTargeted = 5,
        /// <summary>Before damage is applied. Damage.Amount may be changed or Damage.Prevented set.</summary>
        OnDamageBefore = 6,
        /// <summary>After damage was applied (and dying resolved) while the target is alive.</summary>
        OnDamageAfter = 7,
        OnHeal = 8,
        /// <summary>A player entered the dying state (before rescue requests).</summary>
        OnDying = 9,
        /// <summary>A player died. The dead player's own skills can still react to this timing.</summary>
        OnDeath = 10,
        OnTurnEnd = 11
    }

    /// <summary>Damage being resolved. Shared with OnDamageBefore/After triggers, which may modify it.</summary>
    public sealed class DamageInfo
    {
        public int SourceId = -1;
        public int TargetId;
        public int Amount;
        public CardUse CardUse;
        public string CardId;
        public bool Prevented;
    }

    /// <summary>Data for one trigger firing. Mutable fields let skills change the outcome.</summary>
    public sealed class TriggerEventArgs
    {
        public TriggerEventArgs(TriggerTiming timing)
        {
            Timing = timing;
        }

        public TriggerTiming Timing { get; }
        public int EventId { get; internal set; }

        /// <summary>Subject: turn player, drawer, target, damaged/healed/dying/dead player.</summary>
        public int PlayerId = -1;

        /// <summary>Cause: damage source, card user, healer, killer.</summary>
        public int SourceId = -1;

        public DamageInfo Damage;
        public CardUse CardUse;
        public int Amount;
        public bool Cancelled;
        public List<CardInstance> Cards;
    }
}
