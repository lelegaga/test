using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Utils;

namespace Sanguo.Core
{
    public enum ModifierKind : byte
    {
        /// <summary>Added to the owner's distance to others (offensive mount: -1).</summary>
        OutgoingDistance = 0,
        /// <summary>Added to others' distance to the owner (defensive mount: +1).</summary>
        IncomingDistance = 1,
        /// <summary>Owner's attack range (base 1, weapons Set it).</summary>
        AttackRange = 2,
        /// <summary>Per-turn usage limit for the card usage key in <see cref="Modifier.Key"/>.</summary>
        CardUsageLimit = 3,
        /// <summary>Hand size kept at the discard phase (base: current HP).</summary>
        MaxHandSize = 4,
        /// <summary>Cards drawn in the draw phase.</summary>
        DrawPhaseCount = 5,
        /// <summary>Extra targets for cards with the usage key in <see cref="Modifier.Key"/>.</summary>
        ExtraTargets = 6
    }

    public enum ModifierOp : byte
    {
        Add = 0,
        /// <summary>Replaces the base value (highest Set wins); Adds apply afterwards.</summary>
        Set = 1
    }

    public readonly struct Modifier
    {
        public readonly ModifierKind Kind;
        public readonly ModifierOp Op;
        public readonly int Value;
        /// <summary>Optional key restricting the modifier (e.g. usage key "strike"); null applies to all.</summary>
        public readonly string Key;

        public Modifier(ModifierKind kind, ModifierOp op, int value, string key = null)
        {
            Kind = kind;
            Op = op;
            Value = value;
            Key = key;
        }
    }

    /// <summary>Anything that changes rule numbers while it is in play (equipment, passive skills, statuses).</summary>
    public interface IModifierProvider
    {
        void CollectModifiers(PlayerState owner, ModifierKind kind, List<Modifier> output);
    }

    /// <summary>
    /// Aggregates modifiers from equipment, skills and globally registered providers. Rule numbers
    /// (distance, attack range, usage limits, draw count, hand size) are always computed through
    /// here, so new content changes them without touching the rule engine.
    /// </summary>
    public sealed class ModifierSystem
    {
        private readonly GameContext _ctx;
        private readonly List<IModifierProvider> _global = new List<IModifierProvider>();

        internal ModifierSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>Providers consulted for every player (mode rules, status effect definitions...).</summary>
        public void AddGlobalProvider(IModifierProvider provider)
        {
            if (provider != null && !_global.Contains(provider)) _global.Add(provider);
        }

        public int Evaluate(ModifierKind kind, PlayerState owner, int baseValue, string key = null)
        {
            if (owner == null) return baseValue;
            var list = ListPool<Modifier>.Get();
            try
            {
                Collect(owner, kind, list);
                bool hasSet = false;
                int set = 0;
                int add = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    if (m.Kind != kind) continue;
                    if (m.Key != null && m.Key != key) continue;
                    if (m.Op == ModifierOp.Set)
                    {
                        if (!hasSet || m.Value > set) set = m.Value;
                        hasSet = true;
                    }
                    else
                    {
                        add += m.Value;
                    }
                }
                long result = (long)(hasSet ? set : baseValue) + add;
                if (result > int.MaxValue / 2) result = int.MaxValue / 2;
                if (result < int.MinValue / 2) result = int.MinValue / 2;
                return (int)result;
            }
            finally
            {
                ListPool<Modifier>.Release(list);
            }
        }

        private void Collect(PlayerState owner, ModifierKind kind, List<Modifier> output)
        {
            foreach (var card in owner.Equipment.Cards)
            {
                if (card.Definition is EquipmentCard eq)
                {
                    for (int i = 0; i < eq.ContinuousEffects.Count; i++)
                        eq.ContinuousEffects[i].CollectModifiers(owner, kind, output);
                }
            }
            for (int i = 0; i < owner.Skills.Count; i++)
            {
                var s = owner.Skills[i];
                if (!s.Disabled) s.Skill.CollectModifiers(owner, kind, output);
            }
            for (int i = 0; i < _global.Count; i++) _global[i].CollectModifiers(owner, kind, output);
            _ctx.Mode?.CollectModifiers(_ctx.State, owner, kind, output);
        }
    }
}
