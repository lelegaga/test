using Sanguo.Core;

namespace Sanguo.Events
{
    /// <summary>A player became a target of a card use.</summary>
    public sealed class TargetSelectedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.TargetSelected;
        public int SourceId;
        public int TargetId;
        public string CardId;
        public int UseId;
    }

    /// <summary>Damage is about to be dealt (before modification by skills).</summary>
    public sealed class DamageCreatedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.DamageCreated;
        public int SourceId = -1;
        public int TargetId;
        public int Amount;
        public string CardId;
    }

    public sealed class DamagePreventedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.DamagePrevented;
        public int SourceId = -1;
        public int TargetId;
        public string CardId;
    }

    /// <summary>Damage was dealt. Presentation event; the HP change itself is <see cref="HpChangedEvent"/>.</summary>
    public sealed class DamageAppliedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.DamageApplied;
        public int SourceId = -1;
        public int TargetId;
        public int Amount;
        public int NewHp;
        public string CardId;
    }

    /// <summary>Presentation event for healing; the HP change itself is <see cref="HpChangedEvent"/>.</summary>
    public sealed class HealAppliedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.HealApplied;
        public int SourceId = -1;
        public int TargetId;
        public int Amount;
        public int NewHp;
    }

    /// <summary>Authoritative HP / max HP change of a player.</summary>
    public sealed class HpChangedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.HpChanged;
        public int PlayerId;
        public int OldHp;
        public int NewHp;
        public int MaxHp;
        public HpChangeReason Reason;

        public override bool ApplyTo(ClientGameState state)
        {
            var p = state.GetPlayer(PlayerId);
            if (p == null) return false;
            p.Hp = NewHp;
            p.MaxHp = MaxHp;
            return true;
        }
    }
}
