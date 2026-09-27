using System;
using Sanguo.Cards;

namespace Sanguo.Core
{
    /// <summary>
    /// Identity of a physical card as sent to a client that is allowed to see it. Hidden cards are
    /// never described by a CardInfo; the client only receives counts for them.
    /// </summary>
    public sealed class CardInfo : IEquatable<CardInfo>
    {
        public CardInfo(int instanceId, string cardId, Suit suit, int number)
        {
            InstanceId = instanceId;
            CardId = cardId;
            Suit = suit;
            Number = number;
        }

        public int InstanceId { get; }
        public string CardId { get; }
        public Suit Suit { get; }
        public int Number { get; }

        public static CardInfo From(CardInstance card)
        {
            return card == null ? null : new CardInfo(card.InstanceId, card.CardId, card.Suit, card.Number);
        }

        public bool Equals(CardInfo other)
        {
            return other != null && InstanceId == other.InstanceId && CardId == other.CardId && Suit == other.Suit && Number == other.Number;
        }

        public override bool Equals(object obj) => Equals(obj as CardInfo);
        public override int GetHashCode() => InstanceId;
        public override string ToString() => CardId + "#" + InstanceId + "[" + Suit + " " + Number + "]";
    }
}
