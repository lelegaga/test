using Sanguo.Cards;
using Sanguo.Core;
using Sanguo.Data;

namespace Sanguo.Tests
{
    /// <summary>Adds test-only cards to a private content copy (shows cards are pure data).</summary>
    public static class TestCards
    {
        public const string HeavyStrike = "test_heavy_strike";

        /// <summary>Content with a 2-damage strike variant added to the standard deck.</summary>
        public static GameContent WithHeavyStrike()
        {
            var content = TestContent.LoadFresh();
            var json = JsonValue.Parse(
                "{\"id\":\"" + HeavyStrike + "\",\"name\":\"重击\",\"type\":\"basic\",\"target\":{\"kind\":\"single\",\"filters\":[\"notSelf\"]}," +
                "\"range\":\"attack\",\"effects\":[{\"type\":\"RequireResponse\",\"card\":\"dodge\",\"onFail\":[{\"type\":\"Damage\",\"amount\":2}]}]}");
            content.Cards.Add(ContentLoader.ParseCard(json, content.Effects));
            var deck = content.GetDeck("standard");
            deck.Entries.Add(new DeckEntry(HeavyStrike, Suit.Spade, 1));
            deck.Entries.Add(new DeckEntry(HeavyStrike, Suit.Club, 1));
            return content;
        }
    }
}
