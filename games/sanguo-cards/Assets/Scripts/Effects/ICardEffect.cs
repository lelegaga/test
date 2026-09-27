using Sanguo.Cards;
using Sanguo.Core;

namespace Sanguo.Effects
{
    /// <summary>Which player of the effect context an effect applies to.</summary>
    public enum EffectSubject : byte
    {
        /// <summary>The card's target (or the player an AOE is currently resolving on).</summary>
        Target = 0,
        /// <summary>The card user / skill owner.</summary>
        Source = 1
    }

    /// <summary>Who resolves an effect on whom.</summary>
    public sealed class EffectContext
    {
        public int SourceId = -1;
        public int TargetId = -1;
        public CardUse CardUse;
        public string SkillId;

        public int Resolve(EffectSubject subject) => subject == EffectSubject.Source ? SourceId : TargetId;

        public EffectContext WithTarget(int targetId)
        {
            return new EffectContext { SourceId = SourceId, TargetId = targetId, CardUse = CardUse, SkillId = SkillId };
        }

        public string CardId => CardUse?.Definition.CardId;
    }

    /// <summary>
    /// Instant, composable card/skill effect. A card lists several effects that resolve in order per
    /// target (e.g. [RequireResponse(dodge, onFail:[Damage 1])]). Effects produce actions instead of
    /// changing state so they can wait for player input and trigger skills.
    /// </summary>
    public interface ICardEffect
    {
        string EffectType { get; }

        /// <summary>Action applying the effect, or null if there is nothing to do.</summary>
        GameAction CreateAction(GameContext ctx, EffectContext ec);
    }

    /// <summary>Effect active while its card is equipped (distance, range, usage limit changes).</summary>
    public interface IContinuousEffect : IModifierProvider
    {
        string EffectType { get; }
    }
}
