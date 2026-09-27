using System;
using System.Collections.Generic;
using Sanguo.Core;

namespace Sanguo.Characters
{
    /// <summary>
    /// A playable character (武将). Pure data loaded from Resources/Data/characters.json (or built from
    /// a ScriptableObject in the Unity layer); adding one needs no engine code, only its skills.
    /// </summary>
    public sealed class CharacterData
    {
        public string Id;
        public string Name;
        public string Title = string.Empty;
        public Kingdom Kingdom;
        public int MaxHp = 4;
        public Gender Gender;
        public List<string> SkillIds = new List<string>();
        public string Description = string.Empty;
        /// <summary>Resource key of the portrait (placeholder art during development).</summary>
        public string Portrait = string.Empty;

        public override string ToString() => Name + "(" + Id + ")";
    }

    public sealed class CharacterDatabase
    {
        private readonly Dictionary<string, CharacterData> _byId = new Dictionary<string, CharacterData>(StringComparer.Ordinal);
        private readonly List<CharacterData> _ordered = new List<CharacterData>();

        public IReadOnlyList<CharacterData> All => _ordered;
        public int Count => _ordered.Count;

        public void Add(CharacterData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (_byId.ContainsKey(data.Id)) throw new InvalidOperationException("Duplicate character id '" + data.Id + "'.");
            _byId.Add(data.Id, data);
            _ordered.Add(data);
        }

        public bool TryGet(string id, out CharacterData data)
        {
            if (id == null)
            {
                data = null;
                return false;
            }
            return _byId.TryGetValue(id, out data);
        }

        public CharacterData Get(string id)
        {
            if (!TryGet(id, out var c)) throw new KeyNotFoundException("Unknown character id '" + id + "'.");
            return c;
        }
    }
}
