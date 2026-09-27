using System;

namespace Sanguo.Cards
{
    public enum TargetKind : byte
    {
        /// <summary>No player target (the card affects nobody directly).</summary>
        None = 0,
        /// <summary>Automatically targets the user.</summary>
        Self = 1,
        /// <summary>The user picks exactly one target.</summary>
        Single = 2,
        /// <summary>The user picks between MinTargets and MaxTargets targets.</summary>
        Multiple = 3,
        /// <summary>Automatically targets every other living player, in seat order from the user.</summary>
        AllOthers = 4,
        /// <summary>Automatically targets every living player, in seat order starting with the user.</summary>
        All = 5
    }

    [Flags]
    public enum TargetFilter
    {
        None = 0,
        NotSelf = 1,
        /// <summary>Target must have lost HP.</summary>
        Wounded = 2,
        /// <summary>Target must have at least one card in hand, equipment or judge area.</summary>
        HasCards = 4,
        HasHandCards = 8,
        /// <summary>Target's judge area must not already contain a card with the same id.</summary>
        NoSameDelayedTrick = 16
    }

    /// <summary>Who a card (or skill) may target. Pure data; checked by GameRuleEngine.</summary>
    public sealed class TargetRule
    {
        public TargetKind Kind { get; }
        public TargetFilter Filters { get; }
        public int MinTargets { get; }
        public int MaxTargets { get; }

        public TargetRule(TargetKind kind, TargetFilter filters = TargetFilter.None, int minTargets = -1, int maxTargets = -1)
        {
            Kind = kind;
            Filters = filters;
            switch (kind)
            {
                case TargetKind.Single:
                    MinTargets = 1;
                    MaxTargets = 1;
                    break;
                case TargetKind.Multiple:
                    MinTargets = minTargets < 0 ? 1 : minTargets;
                    MaxTargets = maxTargets < 0 ? Math.Max(1, MinTargets) : maxTargets;
                    break;
                default:
                    MinTargets = 0;
                    MaxTargets = 0;
                    break;
            }
        }

        /// <summary>True when the user must choose targets explicitly.</summary>
        public bool RequiresSelection => Kind == TargetKind.Single || Kind == TargetKind.Multiple;

        public bool Has(TargetFilter filter) => (Filters & filter) == filter;

        public static readonly TargetRule SelfOnly = new TargetRule(TargetKind.Self);
        public static readonly TargetRule NoTarget = new TargetRule(TargetKind.None);
    }

    public enum RangeKind : byte
    {
        None = 0,
        /// <summary>Target must be inside the user's attack range (weapon based).</summary>
        AttackRange = 1,
        /// <summary>Target must be within a fixed distance.</summary>
        Distance = 2
    }

    /// <summary>Distance requirement between the user and each target.</summary>
    public readonly struct RangeRequirement
    {
        public readonly RangeKind Kind;
        public readonly int Distance;

        public RangeRequirement(RangeKind kind, int distance = 0)
        {
            Kind = kind;
            Distance = distance;
        }

        public static RangeRequirement None => new RangeRequirement(RangeKind.None);
        public static RangeRequirement AttackRange => new RangeRequirement(RangeKind.AttackRange);
        public static RangeRequirement Within(int distance) => new RangeRequirement(RangeKind.Distance, distance);
    }
}
