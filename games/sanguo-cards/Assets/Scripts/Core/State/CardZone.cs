using System.Collections;
using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Utils;

namespace Sanguo.Core
{
    /// <summary>
    /// Ordered pile of cards. The draw pile's top is the last element. Mutation is internal: only
    /// <see cref="GameStateMutator"/> moves cards, which guarantees that every move emits an event.
    /// </summary>
    public sealed class CardZone : IReadOnlyList<CardInstance>
    {
        private readonly List<CardInstance> _cards = new List<CardInstance>();

        public CardZone(ZoneType type, int ownerId = -1)
        {
            Type = type;
            OwnerId = ownerId;
        }

        public ZoneType Type { get; }
        public int OwnerId { get; }
        public int Count => _cards.Count;
        public CardInstance this[int index] => _cards[index];

        public bool Contains(CardInstance card)
        {
            return card != null && card.Zone == Type && card.OwnerId == OwnerId && _cards.Contains(card);
        }

        public CardInstance FindById(int instanceId)
        {
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i].InstanceId == instanceId) return _cards[i];
            return null;
        }

        public int IndexOf(CardInstance card) => _cards.IndexOf(card);

        /// <summary>Top card of a pile (last element) or null.</summary>
        public CardInstance Top => _cards.Count > 0 ? _cards[_cards.Count - 1] : null;

        internal void Add(CardInstance card)
        {
            _cards.Add(card);
            card.Zone = Type;
            card.OwnerId = OwnerId;
        }

        internal bool Remove(CardInstance card)
        {
            if (!_cards.Remove(card)) return false;
            card.Zone = ZoneType.None;
            card.OwnerId = -1;
            return true;
        }

        internal void Shuffle(IRandom random)
        {
            random.Shuffle(_cards);
        }

        public List<CardInstance>.Enumerator GetEnumerator() => _cards.GetEnumerator();
        IEnumerator<CardInstance> IEnumerable<CardInstance>.GetEnumerator() => _cards.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _cards.GetEnumerator();
    }
}
