using System.Collections.Generic;
using Sanguo.Core;

namespace Sanguo.Events
{
    /// <summary>
    /// Base for every event that moves physical cards between zones. Carries the resulting location
    /// so client replicas stay in sync. Card identities are included only for viewers allowed to see
    /// them: moves touching a public zone are public; moves between hidden zones (draw pile, hands)
    /// are visible to the hand owners involved (<see cref="KnownTo"/>), everybody else gets a count.
    /// </summary>
    public abstract class CardMoveEvent : GameEvent
    {
        public ZoneType FromZone;
        public int FromOwner = -1;
        public ZoneType ToZone;
        public int ToOwner = -1;
        /// <summary>Destination slot when <see cref="ToZone"/> is Equipment.</summary>
        public EquipSlot Slot;
        public MoveReason Reason;
        public int Count;
        /// <summary>Null when the cards are hidden from the receiving viewer.</summary>
        public List<CardInfo> Cards;
        public bool IsPublic;
        /// <summary>Players who may see the cards when the move is not public.</summary>
        public int[] KnownTo;

        public bool IsVisibleTo(int viewerId)
        {
            if (IsPublic) return true;
            if (viewerId < 0 || KnownTo == null) return false;
            for (int i = 0; i < KnownTo.Length; i++)
                if (KnownTo[i] == viewerId) return true;
            return false;
        }

        public override GameEvent ProjectFor(int viewerId)
        {
            if (Cards == null || IsVisibleTo(viewerId)) return this;
            var copy = (CardMoveEvent)MemberwiseClone();
            copy.Cards = null;
            return copy;
        }

        public override bool ApplyTo(ClientGameState state)
        {
            return Remove(state) && Add(state);
        }

        private bool Remove(ClientGameState s)
        {
            switch (FromZone)
            {
                case ZoneType.None:
                case ZoneType.Removed:
                    return true;
                case ZoneType.DrawPile:
                    s.DrawPileCount -= Count;
                    return s.DrawPileCount >= 0;
                case ZoneType.DiscardPile:
                    return RemoveCards(s.DiscardPile);
                case ZoneType.Processing:
                    return RemoveCards(s.Processing);
                case ZoneType.Hand:
                {
                    var p = s.GetPlayer(FromOwner);
                    if (p == null) return false;
                    p.HandCount -= Count;
                    if (p.HandCards != null && !RemoveCards(p.HandCards)) return false;
                    return p.HandCount >= 0;
                }
                case ZoneType.Equipment:
                {
                    var p = s.GetPlayer(FromOwner);
                    if (p == null || Cards == null) return false;
                    foreach (var c in Cards)
                    {
                        bool found = false;
                        for (int i = 1; i < p.Equipment.Length; i++)
                        {
                            if (p.Equipment[i] != null && p.Equipment[i].InstanceId == c.InstanceId)
                            {
                                p.Equipment[i] = null;
                                found = true;
                                break;
                            }
                        }
                        if (!found) return false;
                    }
                    return true;
                }
                case ZoneType.JudgeArea:
                {
                    var p = s.GetPlayer(FromOwner);
                    return p != null && RemoveCards(p.JudgeArea);
                }
                default:
                    return false;
            }
        }

        private bool Add(ClientGameState s)
        {
            switch (ToZone)
            {
                case ZoneType.None:
                case ZoneType.Removed:
                    return true;
                case ZoneType.DrawPile:
                    s.DrawPileCount += Count;
                    return true;
                case ZoneType.DiscardPile:
                    return AddCards(s.DiscardPile);
                case ZoneType.Processing:
                    return AddCards(s.Processing);
                case ZoneType.Hand:
                {
                    var p = s.GetPlayer(ToOwner);
                    if (p == null) return false;
                    p.HandCount += Count;
                    return p.HandCards == null || AddCards(p.HandCards);
                }
                case ZoneType.Equipment:
                {
                    var p = s.GetPlayer(ToOwner);
                    if (p == null || Cards == null || Cards.Count != 1 || Slot == EquipSlot.None) return false;
                    p.Equipment[(int)Slot] = Cards[0];
                    return true;
                }
                case ZoneType.JudgeArea:
                {
                    var p = s.GetPlayer(ToOwner);
                    return p != null && AddCards(p.JudgeArea);
                }
                default:
                    return false;
            }
        }

        private bool RemoveCards(List<CardInfo> list)
        {
            if (Count == 0) return true;
            if (Cards == null) return false;
            foreach (var c in Cards)
            {
                int idx = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].InstanceId == c.InstanceId)
                    {
                        idx = i;
                        break;
                    }
                }
                if (idx < 0) return false;
                list.RemoveAt(idx);
            }
            return true;
        }

        private bool AddCards(List<CardInfo> list)
        {
            if (Count == 0) return true;
            if (Cards == null) return false;
            list.AddRange(Cards);
            return true;
        }
    }

    /// <summary>Cards drawn from the draw pile into a hand.</summary>
    public sealed class CardDrawnEvent : CardMoveEvent
    {
        public override GameEventType Type => GameEventType.CardDrawn;
        public int PlayerId => ToOwner;
    }

    /// <summary>Cards put into the discard pile (always face up).</summary>
    public sealed class CardDiscardedEvent : CardMoveEvent
    {
        public override GameEventType Type => GameEventType.CardDiscarded;
    }

    /// <summary>Any other card movement (equip, steal, judge placement, reshuffle...).</summary>
    public sealed class CardMovedEvent : CardMoveEvent
    {
        public override GameEventType Type => GameEventType.CardMoved;
    }

    /// <summary>
    /// A card was used (or played as a response). Moves the physical cards to the processing zone and
    /// records how they are used. Virtual uses (a skill "treating" nothing as a card) move zero cards.
    /// </summary>
    public sealed class CardPlayedEvent : CardMoveEvent
    {
        public override GameEventType Type => GameEventType.CardPlayed;
        public int UserId;
        public int UseId;
        /// <summary>The card id as used (may differ from the physical card when converted by a skill).</summary>
        public string UsedAsCardId;
        public string ConversionSkillId;
        public int[] Targets;
        public bool IsResponse;
    }

    /// <summary>Discard pile shuffled back into the draw pile (the card move itself is a CardMovedEvent).</summary>
    public sealed class DeckReshuffledEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.DeckReshuffled;
        public int DrawPileCount;
    }

    /// <summary>A judgement card was revealed.</summary>
    public sealed class JudgementEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.Judgement;
        public int PlayerId;
        public CardInfo Card;
        public string ForCardId;
        public bool Success;
    }
}
