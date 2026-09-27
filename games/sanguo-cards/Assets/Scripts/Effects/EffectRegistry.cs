using System;
using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Data;

namespace Sanguo.Effects
{
    /// <summary>
    /// Builds effects from JSON by their "type" field. New effect types are added by registering a
    /// factory; card data can then use them without engine changes.
    /// </summary>
    public sealed class EffectRegistry
    {
        private readonly Dictionary<string, Func<JsonValue, EffectRegistry, ICardEffect>> _instant =
            new Dictionary<string, Func<JsonValue, EffectRegistry, ICardEffect>>(StringComparer.Ordinal);

        private readonly Dictionary<string, Func<JsonValue, IContinuousEffect>> _continuous =
            new Dictionary<string, Func<JsonValue, IContinuousEffect>>(StringComparer.Ordinal);

        public void Register(string type, Func<JsonValue, EffectRegistry, ICardEffect> factory)
        {
            _instant[type] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public void RegisterContinuous(string type, Func<JsonValue, IContinuousEffect> factory)
        {
            _continuous[type] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public ICardEffect Create(JsonValue json)
        {
            string type = json.GetString("type");
            if (type == null || !_instant.TryGetValue(type, out var factory))
                throw new FormatException("Unknown effect type '" + type + "'.");
            return factory(json, this);
        }

        public List<ICardEffect> CreateList(JsonValue array)
        {
            var list = new List<ICardEffect>();
            foreach (var item in array.Items) list.Add(Create(item));
            return list;
        }

        public IContinuousEffect CreateContinuous(JsonValue json)
        {
            string type = json.GetString("type");
            if (type == null || !_continuous.TryGetValue(type, out var factory))
                throw new FormatException("Unknown continuous effect type '" + type + "'.");
            return factory(json);
        }

        public static EffectSubject ParseSubject(string s, EffectSubject fallback)
        {
            if (s == null) return fallback;
            return string.Equals(s, "source", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "self", StringComparison.OrdinalIgnoreCase)
                ? EffectSubject.Source
                : EffectSubject.Target;
        }

        public static ZoneMask ParseZones(JsonValue json, ZoneMask fallback)
        {
            var names = json.GetStringList("zones");
            if (names.Count == 0) return fallback;
            var mask = ZoneMask.None;
            foreach (var n in names)
            {
                switch (n.ToLowerInvariant())
                {
                    case "hand": mask |= ZoneMask.Hand; break;
                    case "equipment": mask |= ZoneMask.Equipment; break;
                    case "judge": mask |= ZoneMask.Judge; break;
                    case "all": mask |= ZoneMask.All; break;
                    default: throw new FormatException("Unknown zone '" + n + "'.");
                }
            }
            return mask;
        }

        /// <summary>Registry with every built-in effect type.</summary>
        public static EffectRegistry CreateDefault()
        {
            var r = new EffectRegistry();
            r.Register("Damage", (j, _) => new DamageEffect(j.GetInt("amount", 1), ParseSubject(j.GetString("subject"), EffectSubject.Target)));
            r.Register("Heal", (j, _) => new HealEffect(j.GetInt("amount", 1), ParseSubject(j.GetString("subject"), EffectSubject.Target)));
            r.Register("DrawCard", (j, _) => new DrawCardEffect(j.GetInt("count", 1), ParseSubject(j.GetString("subject"), EffectSubject.Target)));
            r.Register("Discard", (j, _) => new DiscardEffect(j.GetInt("count", 1), ParseSubject(j.GetString("chooser"), EffectSubject.Source), ParseZones(j, ZoneMask.All)));
            r.Register("StealCard", (j, _) => new StealCardEffect(ParseZones(j, ZoneMask.All)));
            r.Register("AOE", (j, reg) => new AOEEffect(
                string.Equals(j.GetString("scope"), "all", StringComparison.OrdinalIgnoreCase) ? AreaScope.All : AreaScope.AllOthers,
                reg.CreateList(j["effects"])));
            r.Register("Equip", (j, _) => new EquipEffect());
            r.Register("DelayedTrick", (j, _) => new DelayedTrickEffect());
            r.Register("RequireResponse", (j, reg) => new RequireResponseEffect(
                j.GetString("card") ?? throw new FormatException("RequireResponse needs 'card'."),
                j.GetInt("count", 1),
                reg.CreateList(j["onFail"]),
                reg.CreateList(j["onSuccess"])));
            r.Register("Duel", (j, _) => new DuelEffect(j.GetString("card", "strike"), j.GetInt("damage", 1)));
            r.Register("SkipPhase", (j, _) =>
            {
                if (!Enum.TryParse(j.GetString("phase", "PlayPhase"), true, out GamePhase phase))
                    throw new FormatException("Unknown phase in SkipPhase.");
                return new SkipPhaseEffect(phase);
            });

            r.RegisterContinuous("DistanceModifier", j => new DistanceModifierEffect(j.GetInt("outgoing"), j.GetInt("incoming")));
            r.RegisterContinuous("AttackRange", j => new AttackRangeEffect(j.GetInt("range", 1)));
            r.RegisterContinuous("UsageLimit", j => new UsageLimitEffect(j.GetString("key"), j.GetInt("add", 1)));
            return r;
        }
    }
}
