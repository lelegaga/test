using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Core;
using Sanguo.Effects;
using Sanguo.Events;
using Sanguo.Skills;

namespace Sanguo.Game
{
    /// <summary>
    /// Damage pipeline: DamageCreated → OnDamageBefore (skills may change/prevent) → apply HP loss →
    /// dying (rescue) if HP ≤ 0 → OnDamageAfter if the target survived.
    /// </summary>
    public sealed class DamageAction : GameAction
    {
        private readonly DamageInfo _info;
        private int _stage;

        public DamageAction(int sourceId, int targetId, int amount, CardUse cardUse)
        {
            _info = new DamageInfo
            {
                SourceId = sourceId,
                TargetId = targetId,
                Amount = amount,
                CardUse = cardUse,
                CardId = cardUse?.Definition.CardId
            };
        }

        public DamageInfo Info => _info;

        public override ActionResult Step(GameContext ctx)
        {
            var target = ctx.GetPlayer(_info.TargetId);
            switch (_stage)
            {
                case 0:
                    if (target == null || !target.Alive) return ActionResult.Done;
                    ctx.Emit(new DamageCreatedEvent { SourceId = _info.SourceId, TargetId = _info.TargetId, Amount = _info.Amount, CardId = _info.CardId });
                    _stage = 1;
                    ctx.Triggers.Fire(ctx, new TriggerEventArgs(TriggerTiming.OnDamageBefore) { PlayerId = _info.TargetId, SourceId = _info.SourceId, Damage = _info, CardUse = _info.CardUse });
                    return ActionResult.Continue;
                case 1:
                    if (!target.Alive) return ActionResult.Done;
                    if (_info.Prevented || _info.Amount <= 0)
                    {
                        ctx.Emit(new DamagePreventedEvent { SourceId = _info.SourceId, TargetId = _info.TargetId, CardId = _info.CardId });
                        return ActionResult.Done;
                    }
                    // Presentation event first so logs read "takes 1 damage" then "2 HP left".
                    ctx.Emit(new DamageAppliedEvent { SourceId = _info.SourceId, TargetId = _info.TargetId, Amount = _info.Amount, NewHp = target.Hp - _info.Amount, CardId = _info.CardId });
                    ctx.Mutator.ChangeHp(target, -_info.Amount, HpChangeReason.Damage);
                    _stage = 2;
                    if (target.Hp <= 0) ctx.Push(new DyingAction(_info.TargetId, _info.SourceId));
                    return ActionResult.Continue;
                case 2:
                    _stage = 3;
                    if (target.Alive)
                        ctx.Triggers.Fire(ctx, new TriggerEventArgs(TriggerTiming.OnDamageAfter) { PlayerId = _info.TargetId, SourceId = _info.SourceId, Damage = _info, CardUse = _info.CardUse, Amount = _info.Amount });
                    return ActionResult.Continue;
                default:
                    return ActionResult.Done;
            }
        }
    }

    /// <summary>Restores HP (capped at max), then OnHeal triggers.</summary>
    public sealed class HealAction : GameAction
    {
        private readonly int _sourceId;
        private readonly int _targetId;
        private readonly int _amount;
        private bool _done;

        public HealAction(int sourceId, int targetId, int amount)
        {
            _sourceId = sourceId;
            _targetId = targetId;
            _amount = amount;
        }

        public override ActionResult Step(GameContext ctx)
        {
            if (_done) return ActionResult.Done;
            _done = true;
            var target = ctx.GetPlayer(_targetId);
            if (target == null || !target.Alive || _amount <= 0 || target.Hp >= target.MaxHp) return ActionResult.Done;
            int healed = System.Math.Min(_amount, target.MaxHp - target.Hp);
            ctx.Emit(new HealAppliedEvent { SourceId = _sourceId, TargetId = _targetId, Amount = healed, NewHp = target.Hp + healed });
            ctx.Mutator.ChangeHp(target, healed, HpChangeReason.Heal);
            ctx.Triggers.Fire(ctx, new TriggerEventArgs(TriggerTiming.OnHeal) { PlayerId = _targetId, SourceId = _sourceId, Amount = healed });
            return ActionResult.Continue;
        }
    }

    /// <summary>
    /// Dying: every living player, in seat order starting from the current player, may use rescue
    /// cards on the dying player (repeatedly) until HP is above 0; otherwise the player dies.
    /// </summary>
    public sealed class DyingAction : GameAction
    {
        private readonly int _playerId;
        private readonly int _killerId;
        private readonly List<PlayerState> _rescuers = new List<PlayerState>();
        private CardResponseRequest _request;
        private int _stage;
        private int _index;

        public DyingAction(int playerId, int killerId)
        {
            _playerId = playerId;
            _killerId = killerId;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var s = ctx.State;
            var p = ctx.GetPlayer(_playerId);
            string rescueCard = ctx.Config.RescueCardId;
            switch (_stage)
            {
                case 0:
                    if (!p.Alive || p.Hp > 0) return ActionResult.Done;
                    ctx.Mutator.SetDying(p, true);
                    _stage = 1;
                    ctx.Triggers.Fire(ctx, new TriggerEventArgs(TriggerTiming.OnDying) { PlayerId = _playerId, SourceId = _killerId });
                    return ActionResult.Continue;
                case 1:
                    s.GetAliveInSeatOrder(s.CurrentPlayer ?? p, true, _rescuers);
                    _stage = 2;
                    return ActionResult.Continue;
                case 2:
                    while (true)
                    {
                        if (!p.Alive) return ActionResult.Done;
                        if (p.Hp > 0)
                        {
                            ctx.Mutator.SetDying(p, false);
                            return ActionResult.Done;
                        }
                        if (_index >= _rescuers.Count)
                        {
                            ctx.Push(new DeathAction(_playerId, _killerId));
                            return ActionResult.Done;
                        }
                        var rescuer = _rescuers[_index];
                        if (!rescuer.Alive || (ctx.Config.AutoSkipImpossibleResponses && !ctx.Rules.HasPossibleResponse(rescuer, rescueCard)))
                        {
                            _index++;
                            continue;
                        }
                        _request = ctx.Requests.Open(ctx, new CardResponseRequest(rescuer.PlayerId, rescueCard, _killerId, null, "rescue") { SubjectPlayerId = _playerId });
                        _stage = 3;
                        return ActionResult.Wait;
                    }
                default:
                {
                    var req = _request;
                    _request = null;
                    _stage = 2;
                    if (req.Passed || !(req.Response is RespondCommand r))
                    {
                        _index++;
                        return ActionResult.Continue;
                    }
                    // Rescue = use the rescue card on the dying player (same pipeline as a normal card use).
                    var rescuer = ctx.GetPlayer(req.PlayerId);
                    var card = rescuer.HandCards.FindById(r.CardIds[0]);
                    var def = r.SkillId == null ? card.Definition : ctx.Content.Cards.Get(rescueCard);
                    var use = new CardUse(s.NextUseId++, rescuer.PlayerId, def) { ConversionSkillId = r.SkillId };
                    use.PhysicalCards.Add(card);
                    use.Targets.Add(_playerId);
                    if (r.SkillId != null) PlayPhaseAction.AnnounceConversion(ctx, rescuer, r.SkillId, use.Targets);
                    ctx.Push(new UseCardAction(use));
                    return ActionResult.Continue;
                }
            }
        }
    }

    /// <summary>
    /// Death: mark dead (reveal role per mode) → OnDeath triggers (the victim's own skills included) →
    /// discard all the victim's cards → victory check → mode rewards/penalties.
    /// </summary>
    public sealed class DeathAction : GameAction
    {
        private readonly int _victimId;
        private readonly int _killerId;
        private int _stage;

        public DeathAction(int victimId, int killerId)
        {
            _victimId = victimId;
            _killerId = killerId;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var s = ctx.State;
            var v = ctx.GetPlayer(_victimId);
            switch (_stage)
            {
                case 0:
                    if (!v.Alive) return ActionResult.Done;
                    ctx.Mutator.Kill(v, _killerId, ctx.Mode.RevealRoleOnDeath(s, v));
                    _stage = 1;
                    ctx.Triggers.Fire(ctx, new TriggerEventArgs(TriggerTiming.OnDeath) { PlayerId = _victimId, SourceId = _killerId });
                    return ActionResult.Continue;
                case 1:
                {
                    var cards = new List<CardInstance>(v.HandCards);
                    v.Equipment.CopyTo(cards);
                    cards.AddRange(v.JudgeArea);
                    if (cards.Count > 0) ctx.Mutator.Discard(cards, MoveReason.Death);
                    _stage = 2;
                    return ActionResult.Continue;
                }
                default:
                {
                    var result = ctx.Mode.CheckVictory(s);
                    if (result != null)
                    {
                        ctx.Mutator.EndGame(result);
                        return ActionResult.Done;
                    }
                    if (s.Turn.CurrentPlayerId == _victimId) ctx.Mutator.RequestEndTurn();
                    ctx.Mode.OnPlayerKilled(ctx, v, ctx.GetPlayer(_killerId));
                    return ActionResult.Done;
                }
            }
        }
    }

    /// <summary>
    /// The target must play <c>count</c> card(s) usable as the required card, one request at a time;
    /// otherwise the failure effects apply.
    /// </summary>
    public sealed class RequireResponseAction : GameAction
    {
        private readonly EffectContext _ec;
        private readonly string _required;
        private readonly List<ICardEffect> _onFail;
        private readonly List<ICardEffect> _onSuccess;
        private int _remaining;
        private CardResponseRequest _request;

        public RequireResponseAction(EffectContext ec, string requiredCardId, int count, List<ICardEffect> onFail, List<ICardEffect> onSuccess)
        {
            _ec = ec;
            _required = requiredCardId;
            _remaining = count;
            _onFail = onFail;
            _onSuccess = onSuccess;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var target = ctx.GetPlayer(_ec.TargetId);
            if (_request != null)
            {
                var req = _request;
                _request = null;
                if (req.Passed || !(req.Response is RespondCommand r))
                {
                    EffectSequenceAction.PushIfAny(ctx, _onFail, _ec);
                    return ActionResult.Done;
                }
                ctx.Push(RespondWithCardAction.FromCommand(ctx, target, r, _required));
                _remaining--;
                return ActionResult.Continue;
            }
            if (target == null || !target.Alive) return ActionResult.Done;
            if (_remaining <= 0)
            {
                EffectSequenceAction.PushIfAny(ctx, _onSuccess, _ec);
                return ActionResult.Done;
            }
            if (ctx.Config.AutoSkipImpossibleResponses && !ctx.Rules.HasPossibleResponse(target, _required))
            {
                EffectSequenceAction.PushIfAny(ctx, _onFail, _ec);
                return ActionResult.Done;
            }
            _request = ctx.Requests.Open(ctx, new CardResponseRequest(_ec.TargetId, _required, _ec.SourceId, _ec.CardId, "respond"));
            return ActionResult.Wait;
        }
    }

    /// <summary>Alternating response duel between the target (first) and the source.</summary>
    public sealed class DuelAction : GameAction
    {
        private readonly EffectContext _ec;
        private readonly string _required;
        private readonly int _damage;
        private int _current;
        private int _other;
        private CardResponseRequest _request;

        public DuelAction(EffectContext ec, string requiredCardId, int damage)
        {
            _ec = ec;
            _required = requiredCardId;
            _damage = damage;
            _current = ec.TargetId;
            _other = ec.SourceId;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var current = ctx.GetPlayer(_current);
            var other = ctx.GetPlayer(_other);
            if (_request != null)
            {
                var req = _request;
                _request = null;
                if (req.Passed || !(req.Response is RespondCommand r))
                {
                    Lose(ctx);
                    return ActionResult.Done;
                }
                ctx.Push(RespondWithCardAction.FromCommand(ctx, current, r, _required));
                int t = _current;
                _current = _other;
                _other = t;
                return ActionResult.Continue;
            }
            if (current == null || other == null || !current.Alive || !other.Alive) return ActionResult.Done;
            if (ctx.Config.AutoSkipImpossibleResponses && !ctx.Rules.HasPossibleResponse(current, _required))
            {
                Lose(ctx);
                return ActionResult.Done;
            }
            _request = ctx.Requests.Open(ctx, new CardResponseRequest(_current, _required, _other, _ec.CardId, "duel"));
            return ActionResult.Wait;
        }

        private void Lose(GameContext ctx)
        {
            int bonus = _other == _ec.SourceId ? _ec.CardUse?.DamageBonus ?? 0 : 0;
            ctx.Push(new DamageAction(_other, _current, _damage + bonus, _ec.CardUse));
        }
    }

    /// <summary>Applies child effects to each living player in scope, in seat order from the source.</summary>
    public sealed class AOEAction : GameAction
    {
        private readonly EffectContext _ec;
        private readonly AreaScope _scope;
        private readonly List<ICardEffect> _effects;
        private List<int> _targets;
        private int _index;

        public AOEAction(EffectContext ec, AreaScope scope, List<ICardEffect> effects)
        {
            _ec = ec;
            _scope = scope;
            _effects = effects;
        }

        public override ActionResult Step(GameContext ctx)
        {
            if (_targets == null)
            {
                _targets = new List<int>();
                var order = new List<PlayerState>();
                var source = ctx.GetPlayer(_ec.SourceId);
                ctx.State.GetAliveInSeatOrder(source, _scope == AreaScope.All, order);
                foreach (var p in order)
                    if (_scope == AreaScope.All || p.PlayerId != _ec.SourceId) _targets.Add(p.PlayerId);
            }
            while (_index < _targets.Count)
            {
                int t = _targets[_index++];
                var p = ctx.GetPlayer(t);
                if (p == null || !p.Alive) continue;
                ctx.Push(new EffectSequenceAction(_effects, _ec.WithTarget(t)));
                return ActionResult.Continue;
            }
            return ActionResult.Done;
        }
    }
}
