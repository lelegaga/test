using System.Collections.Generic;
using Sanguo.Core;

namespace Sanguo.Effects
{
    /// <summary>
    /// Continuous distance change while equipped. Outgoing -1 = "offensive mount" (owner reaches
    /// others more easily); Incoming +1 = "defensive mount" (others reach the owner less easily).
    /// </summary>
    public sealed class DistanceModifierEffect : IContinuousEffect
    {
        public DistanceModifierEffect(int outgoing, int incoming)
        {
            Outgoing = outgoing;
            Incoming = incoming;
        }

        public string EffectType => "DistanceModifier";
        public int Outgoing { get; }
        public int Incoming { get; }

        public void CollectModifiers(PlayerState owner, ModifierKind kind, List<Modifier> output)
        {
            if (kind == ModifierKind.OutgoingDistance && Outgoing != 0)
                output.Add(new Modifier(ModifierKind.OutgoingDistance, ModifierOp.Add, Outgoing));
            else if (kind == ModifierKind.IncomingDistance && Incoming != 0)
                output.Add(new Modifier(ModifierKind.IncomingDistance, ModifierOp.Add, Incoming));
        }
    }

    /// <summary>Weapon attack range while equipped (sets the base range).</summary>
    public sealed class AttackRangeEffect : IContinuousEffect
    {
        public AttackRangeEffect(int range)
        {
            Range = range;
        }

        public string EffectType => "AttackRange";
        public int Range { get; }

        public void CollectModifiers(PlayerState owner, ModifierKind kind, List<Modifier> output)
        {
            if (kind == ModifierKind.AttackRange) output.Add(new Modifier(ModifierKind.AttackRange, ModifierOp.Set, Range));
        }
    }

    /// <summary>Raises a per-turn card usage limit while equipped (e.g. unlimited strikes).</summary>
    public sealed class UsageLimitEffect : IContinuousEffect
    {
        public UsageLimitEffect(string key, int add)
        {
            Key = key;
            Add = add;
        }

        public string EffectType => "UsageLimit";
        public string Key { get; }
        public int Add { get; }

        public void CollectModifiers(PlayerState owner, ModifierKind kind, List<Modifier> output)
        {
            if (kind == ModifierKind.CardUsageLimit) output.Add(new Modifier(ModifierKind.CardUsageLimit, ModifierOp.Add, Add, Key));
        }
    }
}
