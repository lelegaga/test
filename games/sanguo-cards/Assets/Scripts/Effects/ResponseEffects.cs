using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Game;

namespace Sanguo.Effects
{
    /// <summary>
    /// The target must respond with <see cref="Count"/> card(s) usable as <see cref="RequiredCardId"/>;
    /// otherwise <see cref="OnFail"/> effects apply (e.g. Strike: respond with dodge or take 1 damage).
    /// </summary>
    public sealed class RequireResponseEffect : ICardEffect
    {
        public RequireResponseEffect(string requiredCardId, int count, List<ICardEffect> onFail, List<ICardEffect> onSuccess)
        {
            RequiredCardId = requiredCardId;
            Count = count < 1 ? 1 : count;
            OnFail = onFail ?? new List<ICardEffect>();
            OnSuccess = onSuccess ?? new List<ICardEffect>();
        }

        public string EffectType => "RequireResponse";
        public string RequiredCardId { get; }
        public int Count { get; }
        public List<ICardEffect> OnFail { get; }
        public List<ICardEffect> OnSuccess { get; }

        public GameAction CreateAction(GameContext ctx, EffectContext ec)
        {
            if (ctx.GetPlayer(ec.TargetId) == null) return null;
            return new RequireResponseAction(ec, RequiredCardId, Count, OnFail, OnSuccess);
        }
    }

    /// <summary>
    /// Duel: starting with the target, the two players alternately play <see cref="RequiredCardId"/>;
    /// the first who does not takes <see cref="Damage"/> from the other.
    /// </summary>
    public sealed class DuelEffect : ICardEffect
    {
        public DuelEffect(string requiredCardId, int damage)
        {
            RequiredCardId = requiredCardId;
            Damage = damage;
        }

        public string EffectType => "Duel";
        public string RequiredCardId { get; }
        public int Damage { get; }

        public GameAction CreateAction(GameContext ctx, EffectContext ec)
        {
            if (ctx.GetPlayer(ec.TargetId) == null || ctx.GetPlayer(ec.SourceId) == null) return null;
            return new DuelAction(ec, RequiredCardId, Damage);
        }
    }

    /// <summary>Places a delayed trick in the target's judge area (it resolves in their judge phase).</summary>
    public sealed class DelayedTrickEffect : ICardEffect
    {
        public string EffectType => "DelayedTrick";

        public GameAction CreateAction(GameContext ctx, EffectContext ec)
        {
            var card = ec.CardUse?.Card;
            return card == null ? null : new PlaceDelayedTrickAction(ec.TargetId, card);
        }
    }

    /// <summary>The subject skips a phase of the current turn (used by judgement failures).</summary>
    public sealed class SkipPhaseEffect : ICardEffect
    {
        public SkipPhaseEffect(GamePhase phase)
        {
            Phase = phase;
        }

        public string EffectType => "SkipPhase";
        public GamePhase Phase { get; }

        public GameAction CreateAction(GameContext ctx, EffectContext ec)
        {
            return new SkipPhaseAction(ec.TargetId, Phase);
        }
    }
}
