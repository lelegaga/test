using Sanguo.Core;
using Sanguo.Game;

namespace Sanguo.Effects
{
    /// <summary>The source takes one card of its choice from the target's areas into its hand.</summary>
    public sealed class StealCardEffect : ICardEffect
    {
        public StealCardEffect(ZoneMask zones)
        {
            Zones = zones;
        }

        public string EffectType => "StealCard";
        public ZoneMask Zones { get; }

        public GameAction CreateAction(GameContext ctx, EffectContext ec)
        {
            if (ctx.GetPlayer(ec.TargetId) == null || ctx.GetPlayer(ec.SourceId) == null) return null;
            return new ChooseAndMoveCardAction(ec.SourceId, ec.TargetId, Zones, CardTakeMode.Steal, ec.CardId);
        }
    }
}
