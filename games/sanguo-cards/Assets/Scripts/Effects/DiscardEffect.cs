using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Game;

namespace Sanguo.Effects
{
    /// <summary>
    /// Removes cards from the target into the discard pile. With Chooser = Source the user picks
    /// the target's cards ("dismantle"); with Chooser = Target the target discards cards of its choice.
    /// </summary>
    public sealed class DiscardEffect : ICardEffect
    {
        public DiscardEffect(int count, EffectSubject chooser, ZoneMask zones)
        {
            Count = count;
            Chooser = chooser;
            Zones = zones;
        }

        public string EffectType => "Discard";
        public int Count { get; }
        public EffectSubject Chooser { get; }
        public ZoneMask Zones { get; }

        public GameAction CreateAction(GameContext ctx, EffectContext ec)
        {
            if (Count <= 0 || ctx.GetPlayer(ec.TargetId) == null) return null;
            if (Chooser == EffectSubject.Target)
                return new DiscardCardsAction(ec.TargetId, Count, (Zones & ZoneMask.Equipment) != 0, "effect_discard");
            if (Count == 1)
                return new ChooseAndMoveCardAction(ec.SourceId, ec.TargetId, Zones, CardTakeMode.Discard, ec.CardId);
            var steps = new List<GameAction>();
            for (int i = 0; i < Count; i++)
                steps.Add(new ChooseAndMoveCardAction(ec.SourceId, ec.TargetId, Zones, CardTakeMode.Discard, ec.CardId));
            return new SequenceAction(steps);
        }
    }
}
