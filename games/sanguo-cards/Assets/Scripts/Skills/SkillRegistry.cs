using System;
using System.Collections.Generic;
using Sanguo.Data;

namespace Sanguo.Skills
{
    /// <summary>
    /// Data half of a skill: id, texts, flags and parameters from skills.json. The behaviour comes
    /// from the C# class named by <see cref="ClassName"/>, so new characters that reuse existing
    /// mechanics (with different numbers) need only data.
    /// </summary>
    public sealed class SkillDefinition
    {
        public string Id;
        public string Name;
        public string Description = string.Empty;
        public string ClassName;
        public bool Locked;
        public bool Limited;
        public bool LordSkill;
        public JsonValue Params = JsonValue.NewObject();
    }

    /// <summary>Maps skill classes to factories and skill ids to instances.</summary>
    public sealed class SkillRegistry
    {
        private readonly Dictionary<string, Func<SkillDefinition, SkillBase>> _classes =
            new Dictionary<string, Func<SkillDefinition, SkillBase>>(StringComparer.Ordinal);

        private readonly Dictionary<string, SkillBase> _skills = new Dictionary<string, SkillBase>(StringComparer.Ordinal);

        public IEnumerable<SkillBase> All => _skills.Values;

        public void RegisterClass(string className, Func<SkillDefinition, SkillBase> factory)
        {
            if (string.IsNullOrEmpty(className)) throw new ArgumentException("Class name required.", nameof(className));
            _classes[className] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public bool HasClass(string className) => className != null && _classes.ContainsKey(className);

        public SkillBase Add(SkillDefinition definition)
        {
            if (!_classes.TryGetValue(definition.ClassName ?? string.Empty, out var factory))
                throw new InvalidOperationException("Skill '" + definition.Id + "' uses unknown class '" + definition.ClassName + "'.");
            if (_skills.ContainsKey(definition.Id)) throw new InvalidOperationException("Duplicate skill id '" + definition.Id + "'.");
            var skill = factory(definition);
            _skills.Add(definition.Id, skill);
            return skill;
        }

        /// <summary>Adds a skill instance built in code (tests, mods).</summary>
        public void Add(SkillBase skill)
        {
            _skills[skill.SkillId] = skill;
        }

        public bool TryGet(string skillId, out SkillBase skill)
        {
            if (skillId == null)
            {
                skill = null;
                return false;
            }
            return _skills.TryGetValue(skillId, out skill);
        }

        public SkillBase Get(string skillId)
        {
            if (!TryGet(skillId, out var s)) throw new KeyNotFoundException("Unknown skill id '" + skillId + "'.");
            return s;
        }

        public string GetName(string skillId) => TryGet(skillId, out var s) ? s.Name : skillId;
    }
}
