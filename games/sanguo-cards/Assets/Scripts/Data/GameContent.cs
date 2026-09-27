using System;
using System.Collections.Generic;
using System.IO;
using Sanguo.Cards;
using Sanguo.Characters;
using Sanguo.Effects;
using Sanguo.Skills;

namespace Sanguo.Data
{
    /// <summary>All static content of a build: cards, decks, characters, skills and effect types.</summary>
    public sealed class GameContent
    {
        public GameContent(EffectRegistry effects, SkillRegistry skills)
        {
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
            Skills = skills ?? throw new ArgumentNullException(nameof(skills));
        }

        public CardDatabase Cards { get; } = new CardDatabase();
        public CharacterDatabase Characters { get; } = new CharacterDatabase();
        public SkillRegistry Skills { get; }
        public EffectRegistry Effects { get; }
        public Dictionary<string, DeckDefinition> Decks { get; } = new Dictionary<string, DeckDefinition>(StringComparer.Ordinal);

        public DeckDefinition GetDeck(string deckId)
        {
            if (deckId != null && Decks.TryGetValue(deckId, out var deck)) return deck;
            throw new KeyNotFoundException("Unknown deck '" + deckId + "'.");
        }
    }

    /// <summary>Where content JSON comes from (Unity Resources, files, embedded strings...).</summary>
    public interface IContentSource
    {
        /// <summary>Returns the text of a content file such as "cards.json", or null if missing.</summary>
        string ReadText(string fileName);
    }

    /// <summary>Reads content files from a directory on disk.</summary>
    public sealed class FileContentSource : IContentSource
    {
        private readonly string _directory;

        public FileContentSource(string directory)
        {
            _directory = directory;
        }

        public string ReadText(string fileName)
        {
            string path = Path.Combine(_directory, fileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <summary>
        /// Finds "Assets/Resources/Data" by walking up from the given directories. Works inside the
        /// Unity editor (cwd = project root) and from the headless .NET test harness.
        /// </summary>
        public static string FindDataDirectory(params string[] startDirectories)
        {
            foreach (var start in startDirectories)
            {
                if (string.IsNullOrEmpty(start)) continue;
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, "Assets", "Resources", "Data");
                    if (File.Exists(Path.Combine(candidate, "cards.json"))) return candidate;
                    candidate = Path.Combine(dir.FullName, "games", "sanguo-cards", "Assets", "Resources", "Data");
                    if (File.Exists(Path.Combine(candidate, "cards.json"))) return candidate;
                    dir = dir.Parent;
                }
            }
            return null;
        }
    }

    /// <summary>In-memory source (tests, downloaded expansion packs).</summary>
    public sealed class DictionaryContentSource : IContentSource
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);

        public DictionaryContentSource Set(string fileName, string text)
        {
            _files[fileName] = text;
            return this;
        }

        public string ReadText(string fileName) => _files.TryGetValue(fileName, out var t) ? t : null;
    }
}
