using Sanguo.Core;
using Sanguo.Game;

namespace Sanguo.Effects
{
    /// <summary>Deals damage from the effect source to the subject.</summary>
    public sealed class DamageEffect : ICardEffect
    {
        public DamageEffect(int amount, EffectSubject subject = EffectSubject.Target)
        {
            Amount = amount;
            Subject = subject;
        }

        public string EffectType => "Damage";
        public int Amount { get; }
        public EffectSubject Subject { get; }

        public GameAction CreateAction(GameContext ctx, EffectContext ec)
        {
            int target = ec.Resolve(Subject);
            if (ctx.GetPlayer(target) == null) return null;
            int bonus = ec.CardUse?.DamageBonus ?? 0;
            return new DamageAction(ec.SourceId, target, Amount + bonus, ec.CardUse);
        }
    }
}
