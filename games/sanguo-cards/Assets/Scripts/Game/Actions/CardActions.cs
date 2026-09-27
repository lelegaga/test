using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Core;
using Sanguo.Effects;
using Sanguo.Events;
using Sanguo.Skills;

namespace Sanguo.Game
{
    /// <summary>
    /// Uses a card: announce (move to processing, count usage, OnCardPlayed) → targeting (per-target
    /// TargetSelected + OnCardTargeted, which may cancel targets) → resolve effects per target in seat
    /// order → put leftover cards into the discard pile.
    /// </summary>
    public sealed class UseCardAction : GameAction
    {
        private readonly CardUse _use;
        private int _stage;
        private int _index;

        public UseCardAction(CardUse use)
        {
            _use = use;
        }

        public CardUse Use => _use;

        public override ActionResult Step(GameContext ctx)
        {
            var def = _use.Definition;
            switch (_stage)
            {
                case 0:
                    ctx.Mutator.PlayCards(_use);
                    if (!_use.IsResponse && def.UsageLimitKey != null) ctx.Mutator.IncrementCardUsage(def.UsageLimitKey);
                    _stage = 1;
                    ctx.Triggers.Fire(ctx, new TriggerEventArgs(TriggerTiming.OnCardPlayed) { PlayerId = _use.UserId, SourceId = _use.UserId, CardUse = _use });
                    return ActionResult.Continue;
                case 1:
                    if (_index < _use.Targets.Count)
                    {
                        int t = _use.Targets[_index++];
                        ctx.Emit(new TargetSelectedEvent { SourceId = _use.UserId, TargetId = t, CardId = def.CardId, UseId = _use.UseId });
                        ctx.Triggers.Fire(ctx, new TriggerEventArgs(TriggerTiming.OnCardTargeted) { PlayerId = t, SourceId = _use.UserId, CardUse = _use });
                        return ActionResult.Continue;
                    }
                    _index = 0;
                    _stage = 2;
                    return ActionResult.Continue;
                case 2:
                    return Resolve(ctx, def);
                default:
                {
                    var leftovers = new List<CardInstance>();
                    foreach (var c in _use.PhysicalCards)
                        if (c.Zone == ZoneType.Processing) leftovers.Add(c);
                    if (leftovers.Count > 0) ctx.Mutator.Discard(leftovers, MoveReason.UseFinished);
                    return ActionResult.Done;
                }
            }
        }

        private ActionResult Resolve(GameContext ctx, CardBase def)
        {
            bool singleShot = def is EquipmentCard || (def is TrickCard trick && trick.IsDelayed) || def.TargetRule.Kind == TargetKind.None;
            if (singleShot)
            {
                _stage = 3;
                int target = def.TargetRule.Kind == TargetKind.None || def is EquipmentCard ? _use.UserId : FirstLiveTarget(ctx);
                if (target < 0) return ActionResult.Continue;
                var ec = new EffectContext { SourceId = _use.UserId, TargetId = target, CardUse = _use };
                EffectSequenceAction.PushIfAny(ctx, def.Effects, ec);
                return ActionResult.Continue;
            }
            while (_index < _use.Targets.Count)
            {
                int t = _use.Targets[_index++];
                var target = ctx.GetPlayer(t);
                if (target == null || !target.Alive || _use.IsTargetCancelled(t)) continue;
                ctx.Push(new CardEffectResolutionAction(_use, t));
                return ActionResult.Continue;
            }
            _stage = 3;
            return ActionResult.Continue;
        }

        private int FirstLiveTarget(GameContext ctx)
        {
            foreach (int t in _use.Targets)
            {
                var p = ctx.GetPlayer(t);
                if (p != null && p.Alive && !_use.IsTargetCancelled(t)) return t;
            }
            return -1;
        }
    }

    /// <summary>
    /// Applies a card's effects to one target. Separate action so a future "negate" window
    /// (cancelling the card for this target) can be inserted before the effects.
    /// </summary>
    public sealed class CardEffectResolutionAction : GameAction
    {
        private readonly CardUse _use;
        private readonly int _targetId;
        private bool _started;

        public CardEffectResolutionAction(CardUse use, int targetId)
        {
            _use = use;
            _targetId = targetId;
        }

        public override ActionResult Step(GameContext ctx)
        {
            if (_started) return ActionResult.Done;
            _started = true;
            var target = ctx.GetPlayer(_targetId);
            if (target == null || !target.Alive || _use.IsTargetCancelled(_targetId)) return ActionResult.Done;
            var ec = new EffectContext { SourceId = _use.UserId, TargetId = _targetId, CardUse = _use };
            EffectSequenceAction.PushIfAny(ctx, _use.Definition.Effects, ec);
            return ActionResult.Continue;
        }
    }

    /// <summary>Plays cards as a response (dodge, strike in a duel...). They go to processing then the discard pile.</summary>
    public sealed class RespondWithCardAction : GameAction
    {
        private readonly CardUse _use;
        private int _stage;

        public RespondWithCardAction(GameContext ctx, PlayerState responder, IReadOnlyList<CardInstance> cards, CardBase asDefinition, string skillId)
        {
            _use = new CardUse(ctx.State.NextUseId++, responder.PlayerId, asDefinition) { IsResponse = true, ConversionSkillId = skillId };
            _use.PhysicalCards.AddRange(cards);
        }

        public override ActionResult Step(GameContext ctx)
        {
            if (_stage == 0)
            {
                var responder = ctx.GetPlayer(_use.UserId);
                ctx.Mutator.PlayCards(_use);
                if (_use.ConversionSkillId != null) PlayPhaseAction.AnnounceConversion(ctx, responder, _use.ConversionSkillId, null);
                _stage = 1;
                ctx.Triggers.Fire(ctx, new TriggerEventArgs(TriggerTiming.OnCardPlayed) { PlayerId = _use.UserId, SourceId = _use.UserId, CardUse = _use });
                return ActionResult.Continue;
            }
            var leftovers = new List<CardInstance>();
            foreach (var c in _use.PhysicalCards)
                if (c.Zone == ZoneType.Processing) leftovers.Add(c);
            if (leftovers.Count > 0) ctx.Mutator.Discard(leftovers, MoveReason.UseFinished);
            return ActionResult.Done;
        }

        /// <summary>Builds the response action from a validated RespondCommand.</summary>
        public static RespondWithCardAction FromCommand(GameContext ctx, PlayerState responder, RespondCommand r, string requiredCardId)
        {
            var cards = new List<CardInstance>(r.CardIds.Length);
            foreach (int id in r.CardIds)
            {
                var c = responder.HandCards.FindById(id);
                if (c != null) cards.Add(c);
            }
            var def = ctx.Content.Cards.Get(requiredCardId);
            return new RespondWithCardAction(ctx, responder, cards, def, r.SkillId);
        }
    }

    /// <summary>Moves a used equipment card from processing into its owner's slot.</summary>
    public sealed class EquipAction : GameAction
    {
        private readonly int _playerId;
        private readonly CardInstance _card;

        public EquipAction(int playerId, CardInstance card)
        {
            _playerId = playerId;
            _card = card;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var p = ctx.GetPlayer(_playerId);
            if (p != null && p.Alive && _card.Zone == ZoneType.Processing) ctx.Mutator.Equip(p, _card);
            return ActionResult.Done;
        }
    }

    public sealed class PlaceDelayedTrickAction : GameAction
    {
        private readonly int _targetId;
        private readonly CardInstance _card;

        public PlaceDelayedTrickAction(int targetId, CardInstance card)
        {
            _targetId = targetId;
            _card = card;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var t = ctx.GetPlayer(_targetId);
            if (t != null && t.Alive && _card.Zone == ZoneType.Processing) ctx.Mutator.PlaceInJudgeArea(t, _card);
            return ActionResult.Done;
        }
    }

    /// <summary>Marks a phase of the current turn as skipped (only affects the player whose turn it is).</summary>
    public sealed class SkipPhaseAction : GameAction
    {
        private readonly int _playerId;
        private readonly GamePhase _phase;

        public SkipPhaseAction(int playerId, GamePhase phase)
        {
            _playerId = playerId;
            _phase = phase;
        }

        public override ActionResult Step(GameContext ctx)
        {
            if (ctx.State.Turn.CurrentPlayerId == _playerId) ctx.Mutator.MarkPhaseSkipped(_phase);
            return ActionResult.Done;
        }
    }

    /// <summary>A player draws cards, then OnDraw triggers.</summary>
    public sealed class DrawAction : GameAction
    {
        private readonly int _playerId;
        private readonly int _count;
        private bool _drawn;

        public DrawAction(int playerId, int count)
        {
            _playerId = playerId;
            _count = count;
        }

        public override ActionResult Step(GameContext ctx)
        {
            if (_drawn) return ActionResult.Done;
            _drawn = true;
            var p = ctx.GetPlayer(_playerId);
            if (p == null || !p.Alive || _count <= 0) return ActionResult.Done;
            var cards = ctx.Mutator.DrawCards(p, _count);
            if (cards.Count > 0)
                ctx.Triggers.Fire(ctx, new TriggerEventArgs(TriggerTiming.OnDraw) { PlayerId = _playerId, Amount = cards.Count, Cards = cards });
            return ActionResult.Continue;
        }
    }

    /// <summary>A player discards cards of their own choice (forced by an effect).</summary>
    public sealed class DiscardCardsAction : GameAction
    {
        private readonly int _playerId;
        private readonly int _count;
        private readonly bool _includeEquipment;
        private readonly string _purpose;
        private DiscardRequest _request;

        public DiscardCardsAction(int playerId, int count, bool includeEquipment, string purpose)
        {
            _playerId = playerId;
            _count = count;
            _includeEquipment = includeEquipment;
            _purpose = purpose;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var p = ctx.GetPlayer(_playerId);
            if (_request != null)
            {
                var r = _request.Response as RespondCommand;
                _request = null;
                if (r?.CardIds != null && r.CardIds.Length > 0)
                {
                    var cards = new List<CardInstance>();
                    foreach (int id in r.CardIds)
                    {
                        var c = p.FindOwnedCard(id, _includeEquipment);
                        if (c != null) cards.Add(c);
                    }
                    ctx.Mutator.Discard(cards, MoveReason.Discard);
                }
                return ActionResult.Done;
            }
            if (p == null || !p.Alive) return ActionResult.Done;
            int available = p.HandCards.Count + (_includeEquipment ? p.Equipment.Count : 0);
            int n = System.Math.Min(_count, available);
            if (n <= 0) return ActionResult.Done;
            _request = ctx.Requests.Open(ctx, new DiscardRequest(_playerId, n, _purpose, n, _includeEquipment));
            return ActionResult.Wait;
        }
    }

    public enum CardTakeMode : byte
    {
        Discard = 0,
        Steal = 1
    }

    /// <summary>The chooser picks one card from the target's areas; it is discarded or taken into the chooser's hand.</summary>
    public sealed class ChooseAndMoveCardAction : GameAction
    {
        private readonly int _chooserId;
        private readonly int _targetId;
        private readonly ZoneMask _zones;
        private readonly CardTakeMode _mode;
        private readonly string _contextCardId;
        private ChooseCardFromPlayerRequest _request;

        public ChooseAndMoveCardAction(int chooserId, int targetId, ZoneMask zones, CardTakeMode mode, string contextCardId)
        {
            _chooserId = chooserId;
            _targetId = targetId;
            _zones = zones;
            _mode = mode;
            _contextCardId = contextCardId;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var chooser = ctx.GetPlayer(_chooserId);
            var target = ctx.GetPlayer(_targetId);
            if (_request != null)
            {
                var card = _request.ResolvePick(ctx, _request.Response as RespondCommand);
                _request = null;
                if (card == null) return ActionResult.Done;
                if (_mode == CardTakeMode.Steal && chooser.Alive)
                    ctx.Mutator.MoveCards(new[] { card }, ZoneType.Hand, _chooserId, MoveReason.Steal);
                else
                    ctx.Mutator.Discard(new[] { card }, MoveReason.Dismantle);
                return ActionResult.Done;
            }
            if (chooser == null || target == null || !target.Alive) return ActionResult.Done;
            if (_mode == CardTakeMode.Steal && !chooser.Alive) return ActionResult.Done;
            if (!ChooseCardFromPlayerRequest.HasPickableCard(target, _zones)) return ActionResult.Done;
            string purpose = _mode == CardTakeMode.Steal ? "steal" : "dismantle";
            _request = ctx.Requests.Open(ctx, new ChooseCardFromPlayerRequest(_chooserId, _targetId, _zones, purpose, _contextCardId));
            return ActionResult.Wait;
        }
    }
}
