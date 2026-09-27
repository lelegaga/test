using Sanguo.Core;

namespace Sanguo.Cards
{
    /// <summary>
    /// A physical card in this game. The instance id is unique per game and is only ever revealed to
    /// clients that are allowed to see the card, so hidden cards cannot be tracked across moves.
    /// Location (<see cref="Zone"/>, <see cref="OwnerId"/>) is maintained by CardZone/EquipmentArea.
    /// </summary>
    public sealed class CardInstance
    {
        public CardInstance(int instanceId, CardBase definition, Suit suit, int number)
        {
            InstanceId = instanceId;
            Definition = definition;
            Suit = suit;
            Number = number;
        }

        public int InstanceId { get; }
        public CardBase Definition { get; }
        public Suit Suit { get; }
        public int Number { get; }

        public ZoneType Zone { get; internal set; }

        /// <summary>Owning player for Hand/Equipment/JudgeArea; -1 for shared zones.</summary>
        public int OwnerId { get; internal set; } = -1;

        public string CardId => Definition.CardId;
        public string CardName => Definition.CardName;
        public CardType CardType => Definition.CardType;

        public CardColor Color => ColorOf(Suit);

        public static CardColor ColorOf(Suit suit)
        {
            switch (suit)
            {
                case Suit.Heart:
                case Suit.Diamond:
                    return CardColor.Red;
                case Suit.Spade:
                case Suit.Club:
                    return CardColor.Black;
                default:
                    return CardColor.None;
            }
        }

        public override string ToString() => Definition.CardName + "#" + InstanceId + "[" + Suit + " " + Number + "]";
    }
}
