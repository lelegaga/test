using System;
using System.Collections.Generic;
using Sanguo.Core;

namespace Sanguo.Cards
{
    /// <summary>All card definitions known to this build (base set plus loaded expansion packs).</summary>
    public sealed class CardDatabase
    {
        private readonly Dictionary<string, CardBase> _cards = new Dictionary<string, CardBase>(StringComparer.Ordinal);
        private readonly List<CardBase> _ordered = new List<CardBase>();

        public int Count => _ordered.Count;
        public IReadOnlyList<CardBase> All => _ordered;

        public void Add(CardBase card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (_cards.ContainsKey(card.CardId)) throw new InvalidOperationException("Duplicate card id '" + card.CardId + "'.");
            _cards.Add(card.CardId, card);
            _ordered.Add(card);
        }

        public bool TryGet(string cardId, out CardBase card)
        {
            if (cardId == null)
            {
                card = null;
                return false;
            }
            return _cards.TryGetValue(cardId, out card);
        }

        public CardBase Get(string cardId)
        {
            if (!TryGet(cardId, out var card)) throw new KeyNotFoundException("Unknown card id '" + cardId + "'.");
            return card;
        }

        public string GetName(string cardId) => TryGet(cardId, out var c) ? c.CardName : cardId;
    }

    /// <summary>One physical card in a deck list.</summary>
    public readonly struct DeckEntry
    {
        public readonly string CardId;
        public readonly Suit Suit;
        public readonly int Number;

        public DeckEntry(string cardId, Suit suit, int number)
        {
            CardId = cardId;
            Suit = suit;
            Number = number;
        }
    }

    /// <summary>Named deck list. Modes choose a deck and a copy count (bigger tables use more copies).</summary>
    public sealed class DeckDefinition
    {
        public DeckDefinition(string deckId)
        {
            DeckId = deckId;
        }

        public string DeckId { get; }
        public List<DeckEntry> Entries { get; } = new List<DeckEntry>();

        /// <summary>Creates fresh instances with ids starting at 1 (0 is reserved as "no card").</summary>
        public List<CardInstance> Build(CardDatabase database, int copies)
        {
            if (copies < 1) copies = 1;
            var result = new List<CardInstance>(Entries.Count * copies);
            int nextId = 1;
            for (int c = 0; c < copies; c++)
            {
                foreach (var e in Entries)
                {
                    result.Add(new CardInstance(nextId++, database.Get(e.CardId), e.Suit, e.Number));
                }
            }
            return result;
        }
    }
}
