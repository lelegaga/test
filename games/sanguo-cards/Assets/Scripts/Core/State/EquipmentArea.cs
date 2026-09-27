using System.Collections.Generic;
using Sanguo.Cards;

namespace Sanguo.Core
{
    /// <summary>One card per <see cref="EquipSlot"/>. Public information.</summary>
    public sealed class EquipmentArea
    {
        public const int SlotCount = 6; // indexed by (int)EquipSlot, slot 0 unused

        private readonly CardInstance[] _slots = new CardInstance[SlotCount];

        public EquipmentArea(int ownerId)
        {
            OwnerId = ownerId;
        }

        public int OwnerId { get; }

        public CardInstance Get(EquipSlot slot) => _slots[(int)slot];

        public int Count
        {
            get
            {
                int n = 0;
                for (int i = 1; i < SlotCount; i++)
                    if (_slots[i] != null) n++;
                return n;
            }
        }

        /// <summary>Equipped cards in slot order.</summary>
        public IEnumerable<CardInstance> Cards
        {
            get
            {
                for (int i = 1; i < SlotCount; i++)
                    if (_slots[i] != null) yield return _slots[i];
            }
        }

        public void CopyTo(List<CardInstance> output)
        {
            for (int i = 1; i < SlotCount; i++)
                if (_slots[i] != null) output.Add(_slots[i]);
        }

        public bool Contains(CardInstance card)
        {
            if (card == null) return false;
            for (int i = 1; i < SlotCount; i++)
                if (ReferenceEquals(_slots[i], card)) return true;
            return false;
        }

        public CardInstance FindById(int instanceId)
        {
            for (int i = 1; i < SlotCount; i++)
                if (_slots[i] != null && _slots[i].InstanceId == instanceId) return _slots[i];
            return null;
        }

        public static EquipSlot SlotOf(CardInstance card)
        {
            return card?.Definition is EquipmentCard eq ? eq.Slot : EquipSlot.None;
        }

        internal void Set(EquipSlot slot, CardInstance card)
        {
            _slots[(int)slot] = card;
            card.Zone = ZoneType.Equipment;
            card.OwnerId = OwnerId;
        }

        internal bool Remove(CardInstance card)
        {
            for (int i = 1; i < SlotCount; i++)
            {
                if (ReferenceEquals(_slots[i], card))
                {
                    _slots[i] = null;
                    card.Zone = ZoneType.None;
                    card.OwnerId = -1;
                    return true;
                }
            }
            return false;
        }
    }
}
