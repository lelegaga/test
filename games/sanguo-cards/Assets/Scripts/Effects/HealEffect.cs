using Sanguo.Core;
using Sanguo.Game;

namespace Sanguo.Effects
{
    /// <summary>Restores HP to the subject (never above max HP).</summary>
    public sealed class HealEffect : ICardEffect
    {
        public HealEffect(int amount, EffectSubject subject = EffectSubject.Target)
        {
            Amount = amount;
            Subject = subject;
        }

        public string EffectType => "Heal";
        public int Amount { get; }
        public EffectSubject Subject { get; }

        public GameAction CreateAction(GameContext ctx, EffectContext ec)
        {
            int target = ec.Resolve(Subject);
            return ctx.GetPlayer(target) == null ? null : new HealAction(ec.SourceId, target, Amount);
        }
    }
}
