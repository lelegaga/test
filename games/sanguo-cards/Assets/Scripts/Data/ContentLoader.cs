using System;
using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Characters;
using Sanguo.Core;
using Sanguo.Effects;
using Sanguo.Skills;
using Sanguo.Skills.Builtin;

namespace Sanguo.Data
{
    /// <summary>
    /// Parses content JSON (cards.json, skills.json, characters.json) into a <see cref="GameContent"/>.
    /// Expansion packs are loaded the same way with <see cref="LoadInto"/>.
    /// </summary>
    public static class ContentLoader
    {
        public const string CardsFile = "cards.json";
        public const string SkillsFile = "skills.json";
        public const string CharactersFile = "characters.json";

        public static GameContent Load(IContentSource source, EffectRegistry effects = null, SkillRegistry skills = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            effects = effects ?? EffectRegistry.CreateDefault();
            if (skills == null)
            {
                skills = new SkillRegistry();
                BuiltinSkills.RegisterClasses(skills);
            }
            var content = new GameContent(effects, skills);
            LoadInto(content, source, true);
            return content;
        }

        /// <summary>Adds the content files found in <paramref name="source"/> to existing content.</summary>
        public static void LoadInto(GameContent content, IContentSource source, bool requireAll)
        {
            string cards = source.ReadText(CardsFile);
            string skills = source.ReadText(SkillsFile);
            string characters = source.ReadText(CharactersFile);
            if (requireAll && (cards == null || skills == null || characters == null))
                throw new InvalidOperationException("Content source is missing " + CardsFile + ", " + SkillsFile + " or " + CharactersFile + ".");
            if (cards != null) LoadCards(content, JsonValue.Parse(cards));
            if (skills != null) LoadSkills(content, JsonValue.Parse(skills));
            if (characters != null) LoadCharacters(content, JsonValue.Parse(characters));
            Validate(content);
        }

        // ------------------------------------------------------------------ cards

        public static void LoadCards(GameContent content, JsonValue root)
        {
            foreach (var j in root["cards"].Items) content.Cards.Add(ParseCard(j, content.Effects));
            foreach (var d in root["decks"].Items)
            {
                var deck = new DeckDefinition(d.GetString("id") ?? throw new FormatException("Deck without id."));
                foreach (var entry in d["cards"].Items)
                {
                    string cardId = entry[0].AsString();
                    var suit = ParseEnum(entry[1].AsString(), Suit.None);
                    int number = entry[2].AsInt();
                    if (!content.Cards.TryGet(cardId, out _)) throw new FormatException("Deck '" + deck.DeckId + "' references unknown card '" + cardId + "'.");
                    deck.Entries.Add(new DeckEntry(cardId, suit, number));
                }
                content.Decks[deck.DeckId] = deck;
            }
        }

        public static CardBase ParseCard(JsonValue j, EffectRegistry effects)
        {
            string id = j.GetString("id") ?? throw new FormatException("Card without id.");
            string name = j.GetString("name", id);
            string type = (j.GetString("type") ?? "basic").ToLowerInvariant();
            CardBase card;
            switch (type)
            {
                case "basic":
                    card = new BasicCard(id, name);
                    break;
                case "trick":
                {
                    var trick = new TrickCard(id, name) { IsDelayed = j.GetBool("delayed") };
                    if (j.Has("judge"))
                    {
                        var suits = new List<Suit>();
                        foreach (var s in j["judge"].GetStringList("successSuits")) suits.Add(ParseEnum(s, Suit.None));
                        trick.Judge = new JudgeRule(suits);
                    }
                    trick.JudgeFailEffects.AddRange(effects.CreateList(j["judgeFail"]));
                    card = trick;
                    break;
                }
                case "equipment":
                {
                    var slot = ParseEnum(j.GetString("slot"), EquipSlot.None);
                    if (slot == EquipSlot.None) throw new FormatException("Equipment '" + id + "' needs a slot.");
                    var eq = new EquipmentCard(id, name, slot);
                    foreach (var c in j["continuous"].Items) eq.ContinuousEffects.Add(effects.CreateContinuous(c));
                    card = eq;
                    break;
                }
                default:
                    throw new FormatException("Card '" + id + "' has unknown type '" + type + "'.");
            }

            card.Description = j.GetString("description", string.Empty);
            card.Icon = j.GetString("icon", id);
            card.CanUseActively = j.GetBool("activeUse", true);
            card.UsageLimitKey = j.GetString("usageKey");
            card.BaseUsageLimit = j.GetInt("usageLimit", 1);
            foreach (var tag in j.GetStringList("tags")) card.AddTag(tag);
            card.TargetRule = ParseTargetRule(j["target"], card.CardType == CardType.Equipment);
            card.Range = ParseRange(j["range"]);
            card.Effects.AddRange(effects.CreateList(j["effects"]));
            if (card is EquipmentCard && card.Effects.Count == 0) card.Effects.Add(new EquipEffect());
            return card;
        }

        private static TargetRule ParseTargetRule(JsonValue j, bool isEquipment)
        {
            if (j.IsNull) return isEquipment ? TargetRule.SelfOnly : TargetRule.NoTarget;
            var kind = ParseEnum(j.GetString("kind"), TargetKind.None);
            var filters = TargetFilter.None;
            foreach (var f in j.GetStringList("filters"))
            {
                if (!Enum.TryParse(f, true, out TargetFilter parsed)) throw new FormatException("Unknown target filter '" + f + "'.");
                filters |= parsed;
            }
            return new TargetRule(kind, filters, j.GetInt("min", -1), j.GetInt("max", -1));
        }

        private static RangeRequirement ParseRange(JsonValue j)
        {
            if (j.IsNull) return RangeRequirement.None;
            if (j.Kind == JsonKind.String)
            {
                return string.Equals(j.AsString(), "attack", StringComparison.OrdinalIgnoreCase)
                    ? RangeRequirement.AttackRange
                    : RangeRequirement.None;
            }
            if (j.Has("distance")) return RangeRequirement.Within(j.GetInt("distance", 1));
            return RangeRequirement.None;
        }

        // ------------------------------------------------------------------ skills

        public static void LoadSkills(GameContent content, JsonValue root)
        {
            foreach (var j in root["skills"].Items)
            {
                var def = new SkillDefinition
                {
                    Id = j.GetString("id") ?? throw new FormatException("Skill without id."),
                    ClassName = j.GetString("class"),
                    Locked = j.GetBool("locked"),
                    Limited = j.GetBool("limited"),
                    LordSkill = j.GetBool("lord"),
                    Params = j["params"].IsObject ? j["params"] : JsonValue.NewObject()
                };
                def.Name = j.GetString("name", def.Id);
                def.Description = j.GetString("description", string.Empty);
                content.Skills.Add(def);
            }
        }

        // ------------------------------------------------------------------ characters

        public static void LoadCharacters(GameContent content, JsonValue root)
        {
            foreach (var j in root["characters"].Items)
            {
                var c = new CharacterData
                {
                    Id = j.GetString("id") ?? throw new FormatException("Character without id."),
                    Title = j.GetString("title", string.Empty),
                    Kingdom = ParseEnum(j.GetString("kingdom"), Kingdom.None),
                    MaxHp = j.GetInt("maxHp", 4),
                    Gender = ParseEnum(j.GetString("gender"), Gender.Male),
                    Description = j.GetString("description", string.Empty),
                    Portrait = j.GetString("portrait", string.Empty)
                };
                c.Name = j.GetString("name", c.Id);
                c.SkillIds.AddRange(j.GetStringList("skills"));
                content.Characters.Add(c);
            }
        }

        // ------------------------------------------------------------------ validation

        private static void Validate(GameContent content)
        {
            foreach (var c in content.Characters.All)
            {
                foreach (var s in c.SkillIds)
                    if (!content.Skills.TryGet(s, out _))
                        throw new FormatException("Character '" + c.Id + "' references unknown skill '" + s + "'.");
            }
        }

        private static T ParseEnum<T>(string s, T fallback) where T : struct
        {
            if (s == null) return fallback;
            return Enum.TryParse(s, true, out T v) ? v : fallback;
        }
    }
}
