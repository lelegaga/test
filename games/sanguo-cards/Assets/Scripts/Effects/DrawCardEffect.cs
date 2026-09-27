using Sanguo.Core;
using Sanguo.Game;

namespace Sanguo.Effects
{
    /// <summary>The subject draws cards.</summary>
    public sealed class DrawCardEffect : ICardEffect
    {
        public DrawCardEffect(int count, EffectSubject subject = EffectSubject.Target)
        {
            Count = count;
            Subject = subject;
        }

        public string EffectType => "DrawCard";
        public int Count { get; }
        public EffectSubject Subject { get; }

        public GameAction CreateAction(GameContext ctx, EffectContext ec)
        {
            int target = ec.Resolve(Subject);
            return ctx.GetPlayer(target) == null || Count <= 0 ? null : new DrawAction(target, Count);
        }
    }
}
