using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Game;

namespace Sanguo.Effects
{
    public enum AreaScope : byte
    {
        AllOthers = 0,
        All = 1
    }

    /// <summary>
    /// Applies child effects to every living player in scope, one player at a time in seat order
    /// starting after the source. Used by skills; cards with area targeting use TargetKind.AllOthers
    /// so each target also gets "became target" triggers.
    /// </summary>
    public sealed class AOEEffect : ICardEffect
    {
        public AOEEffect(AreaScope scope, List<ICardEffect> effects)
        {
            Scope = scope;
            Effects = effects ?? new List<ICardEffect>();
        }

        public string EffectType => "AOE";
        public AreaScope Scope { get; }
        public List<ICardEffect> Effects { get; }

        public GameAction CreateAction(GameContext ctx, EffectContext ec)
        {
            return Effects.Count == 0 ? null : new AOEAction(ec, Scope, Effects);
        }
    }
}
