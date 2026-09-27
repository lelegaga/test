using Sanguo.Core;
using Sanguo.Game;

namespace Sanguo.Effects
{
    /// <summary>Puts the used equipment card into the user's matching slot (replacing the old one).</summary>
    public sealed class EquipEffect : ICardEffect
    {
        public string EffectType => "Equip";

        public GameAction CreateAction(GameContext ctx, EffectContext ec)
        {
            var card = ec.CardUse?.Card;
            return card == null ? null : new EquipAction(ec.SourceId, card);
        }
    }
}
