using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Core;
using Sanguo.Effects;
using Sanguo.Events;
using Sanguo.Skills;

namespace Sanguo.Game
{
    /// <summary>Resolves the delayed tricks in the player's judge area, last placed first.</summary>
    public sealed class JudgePhaseAction : GameAction
    {
        private readonly int _playerId;
        private readonly List<int> _processed = new List<int>();

        public JudgePhaseAction(int playerId)
        {
            _playerId = playerId;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var p = ctx.GetPlayer(_playerId);
            if (!p.Alive || p.JudgeArea.Count == 0) return ActionResult.Done;
            var card = p.JudgeArea[p.JudgeArea.Count - 1];
            if (_processed.Contains(card.InstanceId))
                throw new GameFlowException("Delayed trick " + card + " was not removed from the judge area.");
            _processed.Add(card.InstanceId);
            ctx.Push(new DelayedTrickResolveAction(_playerId, card));
            return ActionResult.Continue;
        }
    }

    /// <summary>Flips a judgement card for one delayed trick and applies its failure effects.</summary>
    public sealed class DelayedTrickResolveAction : GameAction
    {
        private readonly int _playerId;
        private readonly CardInstance _trickCard;
        private CardInstance _judgeCard;
        private int _stage;

        public DelayedTrickResolveAction(int playerId, CardInstance trickCard)
        {
            _playerId = playerId;
            _trickCard = trickCard;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var p = ctx.GetPlayer(_playerId);
            var trick = _trickCard.Definition as TrickCard;
            switch (_stage)
            {
                case 0:
                    ctx.Mutator.MoveCards(new[] { _trickCard }, ZoneType.Processing, -1, MoveReason.Judge);
                    _stage = 1;
                    return ActionResult.Continue;
                case 1:
                {
                    _judgeCard = ctx.Mutator.RevealTopCard(MoveReason.Judge);
                    bool success = trick?.Judge == null || _judgeCard == null || trick.Judge.IsSuccess(_judgeCard);
                    ctx.Emit(new JudgementEvent { PlayerId = _playerId, Card = CardInfo.From(_judgeCard), ForCardId = _trickCard.CardId, Success = success });
                    _stage = 2;
                    if (!success && trick != null && p.Alive)
                    {
                        var ec = new EffectContext { SourceId = _playerId, TargetId = _playerId };
                        EffectSequenceAction.PushIfAny(ctx, trick.JudgeFailEffects, ec);
                    }
                    return ActionResult.Continue;
                }
                default:
                {
                    var leftovers = new List<CardInstance>(2);
                    if (_judgeCard != null && _judgeCard.Zone == ZoneType.Processing) leftovers.Add(_judgeCard);
                    if (_trickCard.Zone == ZoneType.Processing) leftovers.Add(_trickCard);
                    if (leftovers.Count > 0) ctx.Mutator.Discard(leftovers, MoveReason.JudgeFinished);
                    return ActionResult.Done;
                }
            }
        }
    }

    /// <summary>Draw phase: OnDrawPhase triggers may change the count (or cancel the draw).</summary>
    public sealed class DrawPhaseAction : GameAction
    {
        private readonly int _playerId;
        private TriggerEventArgs _args;

        public DrawPhaseAction(int playerId)
        {
            _playerId = playerId;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var p = ctx.GetPlayer(_playerId);
            if (_args == null)
            {
                _args = new TriggerEventArgs(TriggerTiming.OnDrawPhase) { PlayerId = _playerId, Amount = ctx.Rules.GetDrawPhaseCount(p) };
                ctx.Triggers.Fire(ctx, _args);
                return ActionResult.Continue;
            }
            if (p.Alive && !_args.Cancelled && _args.Amount > 0) ctx.Push(new DrawAction(_playerId, _args.Amount));
            return ActionResult.Done;
        }
    }

    /// <summary>
    /// Play phase: repeatedly asks the current player for an action (use a card, use a skill, or end)
    /// until they end the phase, die, the turn is ended, or the safety cap is reached.
    /// </summary>
    public sealed class PlayPhaseAction : GameAction
    {
        private readonly int _playerId;
        private PlayActionRequest _request;

        public PlayPhaseAction(int playerId)
        {
            _playerId = playerId;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var s = ctx.State;
            var p = ctx.GetPlayer(_playerId);
            if (_request != null)
            {
                var response = _request.Response;
                _request = null;
                switch (response)
                {
                    case PlayCardCommand play:
                        ctx.Mutator.CountPlayAction();
                        ctx.Push(new UseCardAction(BuildCardUse(ctx, p, play)));
                        return ActionResult.Continue;
                    case UseSkillCommand skill:
                        ctx.Mutator.CountPlayAction();
                        ActivateSkill(ctx, p, skill);
                        return ActionResult.Continue;
                    default:
                        return ActionResult.Done; // EndTurnCommand or timeout
                }
            }

            if (!p.Alive || s.IsGameOver || s.Turn.EndTurnRequested) return ActionResult.Done;
            if (s.Turn.PlayActionsThisPhase >= ctx.Config.MaxActionsPerPlayPhase) return ActionResult.Done;
            _request = ctx.Requests.Open(ctx, new PlayActionRequest(_playerId));
            return ActionResult.Wait;
        }

        /// <summary>Turns a validated PlayCardCommand into a CardUse with resolved, ordered targets.</summary>
        public static CardUse BuildCardUse(GameContext ctx, PlayerState p, PlayCardCommand play)
        {
            var card = p.HandCards.FindById(play.CardInstanceId);
            var def = ctx.Rules.ResolveUsedDefinition(p, card, play.SkillId, play.AsCardId, false, out _);
            var use = new CardUse(ctx.State.NextUseId++, p.PlayerId, def) { ConversionSkillId = play.SkillId };
            use.PhysicalCards.Add(card);
            if (def.TargetRule.RequiresSelection)
            {
                use.Targets.AddRange(play.TargetIds);
                ctx.Rules.SortBySeatFrom(p, use.Targets);
            }
            else
            {
                ctx.Rules.GetAutoTargets(p, def, use.Targets);
            }
            if (play.SkillId != null) AnnounceConversion(ctx, p, play.SkillId, use.Targets);
            return use;
        }

        internal static void AnnounceConversion(GameContext ctx, PlayerState p, string skillId, List<int> targets)
        {
            var si = p.FindSkill(skillId);
            if (si == null) return;
            ctx.Mutator.RecordSkillUse(p, si);
            ctx.Emit(new SkillActivatedEvent { PlayerId = p.PlayerId, SkillId = skillId, Targets = targets?.ToArray() ?? System.Array.Empty<int>() });
        }

        private static void ActivateSkill(GameContext ctx, PlayerState p, UseSkillCommand cmd)
        {
            var si = p.FindSkill(cmd.SkillId);
            var active = (ActiveSkillBase)si.Skill;
            ctx.Mutator.RecordSkillUse(p, si);
            ctx.Emit(new SkillActivatedEvent { PlayerId = p.PlayerId, SkillId = cmd.SkillId, Targets = (int[])cmd.TargetIds.Clone() });
            var action = active.CreateActivationAction(ctx, p, cmd);
            if (action != null) ctx.Push(action);
        }
    }

    /// <summary>Discard phase: discard down to the maximum hand size (current HP by default).</summary>
    public sealed class DiscardPhaseAction : GameAction
    {
        private readonly int _playerId;
        private DiscardRequest _request;

        public DiscardPhaseAction(int playerId)
        {
            _playerId = playerId;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var p = ctx.GetPlayer(_playerId);
            if (_request != null)
            {
                var r = _request.Response as RespondCommand;
                _request = null;
                DiscardChosen(ctx, p, r, false);
                return ActionResult.Done;
            }
            if (!p.Alive) return ActionResult.Done;
            int excess = p.HandCards.Count - ctx.Rules.GetMaxHandSize(p);
            if (excess <= 0) return ActionResult.Done;
            _request = ctx.Requests.Open(ctx, new DiscardRequest(_playerId, excess, "discard_phase"));
            return ActionResult.Wait;
        }

        internal static void DiscardChosen(GameContext ctx, PlayerState p, RespondCommand r, bool includeEquipment)
        {
            if (r == null || r.CardIds == null || r.CardIds.Length == 0) return;
            var cards = new List<CardInstance>(r.CardIds.Length);
            foreach (int id in r.CardIds)
            {
                var c = p.FindOwnedCard(id, includeEquipment);
                if (c != null) cards.Add(c);
            }
            ctx.Mutator.Discard(cards, MoveReason.DiscardPhase);
        }
    }
}
