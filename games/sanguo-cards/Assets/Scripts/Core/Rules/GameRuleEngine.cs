using System;
using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Skills;
using Sanguo.Utils;

namespace Sanguo.Core
{
    /// <summary>
    /// Read-only rule queries an AI (or a UI helper on the host) may use. Everything here depends
    /// only on the asking player's own cards and on public information.
    /// </summary>
    public interface IRulesQuery
    {
        int GetDistance(int fromId, int toId);
        int GetAttackRange(int playerId);
        bool IsInAttackRange(int fromId, int toId);
        int GetMaxHandSize(int playerId);
        int GetRemainingUsage(int playerId, CardBase definition);
        ValidationResult CanUseCard(int userId, int cardInstanceId, string skillId = null, string asCardId = null);
        void GetLegalTargets(int userId, CardBase definition, List<int> output);
        ValidationResult ValidateCardUse(int userId, PlayCardCommand command);
        ValidationResult ValidateSkillActivation(int playerId, UseSkillCommand command);
        bool CanCardServeAs(PlayerState player, CardInstance card, string requiredCardId, string skillId);
    }

    /// <summary>
    /// Server-side rule validation: whether a command is legal right now (phase, turn, ownership,
    /// usage limits, targets, distance, liveness). Pure: never modifies state. Every client command
    /// passes through here before anything happens, which is what stops malicious clients.
    /// </summary>
    public sealed class GameRuleEngine : IRulesQuery
    {
        private readonly GameContext _ctx;

        internal GameRuleEngine(GameContext ctx)
        {
            _ctx = ctx;
        }

        private GameState S => _ctx.State;

        // ------------------------------------------------------------------ commands

        /// <summary>Full check of an incoming command (excluding sequence numbers, handled by the engine).</summary>
        public ValidationResult ValidateCommand(GameCommand cmd, out PendingRequest request)
        {
            request = null;
            if (cmd == null) return ValidationResult.Fail(RejectReason.MalformedCommand);
            if (S.IsGameOver) return ValidationResult.Fail(RejectReason.GameOver);
            if (S.Phase == GamePhase.Waiting) return ValidationResult.Fail(RejectReason.GameNotRunning);
            var player = S.GetPlayer(cmd.PlayerId);
            if (player == null) return ValidationResult.Fail(RejectReason.UnknownPlayer);

            var req = _ctx.Requests.Find(cmd.RequestId);
            if (req == null || req.PlayerId != cmd.PlayerId)
            {
                if (_ctx.Requests.FindForPlayer(cmd.PlayerId) != null)
                    return ValidationResult.Fail(RejectReason.RequestMismatch, "Command does not answer the open request.");
                return player.Alive
                    ? ValidationResult.Fail(RejectReason.NoPendingRequest, "Nothing is expected from this player.")
                    : ValidationResult.Fail(RejectReason.PlayerDead);
            }
            var v = req.Validate(_ctx, cmd);
            if (v.IsValid) request = req;
            return v;
        }

        // ------------------------------------------------------------------ card use

        public ValidationResult ValidateCardUse(int userId, PlayCardCommand cmd)
        {
            var user = S.GetPlayer(userId);
            if (user == null) return ValidationResult.Fail(RejectReason.UnknownPlayer);
            if (!user.Alive) return ValidationResult.Fail(RejectReason.PlayerDead);
            if (cmd == null) return ValidationResult.Fail(RejectReason.MalformedCommand);
            var basic = CheckPlayWindow(user);
            if (!basic.IsValid) return basic;
            var card = user.HandCards.FindById(cmd.CardInstanceId);
            if (card == null) return ValidationResult.Fail(RejectReason.CardNotOwned, "Card " + cmd.CardInstanceId + " is not in hand.");
            var def = ResolveUsedDefinition(user, card, cmd.SkillId, cmd.AsCardId, false, out var v);
            if (!v.IsValid) return v;
            v = CheckUsable(user, def);
            if (!v.IsValid) return v;
            return ValidateTargets(user, def, cmd.TargetIds);
        }

        /// <summary>Usability ignoring targets (used by UI highlighting and AI candidate generation).</summary>
        public ValidationResult CanUseCard(int userId, int cardInstanceId, string skillId = null, string asCardId = null)
        {
            var user = S.GetPlayer(userId);
            if (user == null || !user.Alive) return ValidationResult.Fail(RejectReason.PlayerDead);
            var basic = CheckPlayWindow(user);
            if (!basic.IsValid) return basic;
            var card = user.HandCards.FindById(cardInstanceId);
            if (card == null) return ValidationResult.Fail(RejectReason.CardNotOwned);
            var def = ResolveUsedDefinition(user, card, skillId, asCardId, false, out var v);
            if (!v.IsValid) return v;
            v = CheckUsable(user, def);
            if (!v.IsValid) return v;
            if (def.TargetRule.RequiresSelection)
            {
                var list = ListPool<int>.Get();
                GetLegalTargets(userId, def, list);
                int n = list.Count;
                ListPool<int>.Release(list);
                if (n < def.TargetRule.MinTargets) return ValidationResult.Fail(RejectReason.InvalidTarget, "No legal target.");
            }
            return ValidationResult.Ok;
        }

        private ValidationResult CheckPlayWindow(PlayerState user)
        {
            if (S.Phase != GamePhase.PlayPhase || S.Turn.CurrentPlayerId != user.PlayerId)
                return ValidationResult.Fail(RejectReason.RequestMismatch, "Cards can only be used in your own play phase.");
            if (S.StateMachine.IsWaitingResponse)
                return ValidationResult.Fail(RejectReason.RequestMismatch, "Waiting for a response.");
            return ValidationResult.Ok;
        }

        /// <summary>The definition a physical card is used as (itself, or a conversion through a skill).</summary>
        public CardBase ResolveUsedDefinition(PlayerState user, CardInstance card, string skillId, string asCardId, bool forResponse, out ValidationResult result)
        {
            if (skillId == null && asCardId == null)
            {
                result = ValidationResult.Ok;
                return card.Definition;
            }
            if (skillId == null || asCardId == null)
            {
                result = ValidationResult.Fail(RejectReason.MalformedCommand, "Conversion needs both a skill and a card id.");
                return null;
            }
            var si = user.FindSkill(skillId);
            if (si == null || si.Disabled)
            {
                result = ValidationResult.Fail(RejectReason.SkillUnavailable);
                return null;
            }
            if (!_ctx.Content.Cards.TryGet(asCardId, out var asDef) || !si.Skill.CanConvert(_ctx, user, card, asCardId, forResponse))
            {
                result = ValidationResult.Fail(RejectReason.InvalidCard, card.CardName + " cannot be used as " + asCardId + ".");
                return null;
            }
            result = ValidationResult.Ok;
            return asDef;
        }

        private ValidationResult CheckUsable(PlayerState user, CardBase def)
        {
            if (!def.CanUseActively) return ValidationResult.Fail(RejectReason.CardNotUsable, def.CardName + " cannot be used actively.");
            if (def.UsageLimitKey != null && GetRemainingUsage(user.PlayerId, def) <= 0)
                return ValidationResult.Fail(RejectReason.UsageLimitReached, def.CardName + " usage limit reached this turn.");
            if (!def.TargetRule.RequiresSelection && def.TargetRule.Kind != TargetKind.None)
            {
                var auto = ListPool<int>.Get();
                GetAutoTargets(user, def, auto);
                int n = auto.Count;
                ListPool<int>.Release(auto);
                if (n == 0) return ValidationResult.Fail(RejectReason.CardNotUsable, def.CardName + " has no valid target now.");
            }
            return ValidationResult.Ok;
        }

        public int GetRemainingUsage(int playerId, CardBase def)
        {
            var p = S.GetPlayer(playerId);
            if (p == null || def == null) return 0;
            if (def.UsageLimitKey == null) return int.MaxValue;
            int limit = _ctx.Modifiers.Evaluate(ModifierKind.CardUsageLimit, p, def.BaseUsageLimit, def.UsageLimitKey);
            int used = S.Turn.CurrentPlayerId == playerId ? S.Turn.GetCardUsage(def.UsageLimitKey) : 0;
            return limit - used;
        }

        public int GetMaxTargets(PlayerState user, CardBase def)
        {
            int max = def.TargetRule.MaxTargets;
            if (def.TargetRule.RequiresSelection)
                max = _ctx.Modifiers.Evaluate(ModifierKind.ExtraTargets, user, 0, def.UsageLimitKey ?? def.CardId) + max;
            return max;
        }

        private ValidationResult ValidateTargets(PlayerState user, CardBase def, int[] targetIds)
        {
            var rule = def.TargetRule;
            int n = targetIds?.Length ?? 0;
            if (!rule.RequiresSelection)
            {
                return n == 0 ? ValidationResult.Ok : ValidationResult.Fail(RejectReason.WrongTargetCount, def.CardName + " picks its targets automatically.");
            }
            int max = GetMaxTargets(user, def);
            if (n < rule.MinTargets || n > max)
                return ValidationResult.Fail(RejectReason.WrongTargetCount, def.CardName + " needs " + rule.MinTargets + "-" + max + " target(s).");
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                    if (targetIds[i] == targetIds[j]) return ValidationResult.Fail(RejectReason.MalformedCommand, "Duplicate target.");
                var t = S.GetPlayer(targetIds[i]);
                var reason = CheckTarget(user, def, t);
                if (reason != RejectReason.None) return ValidationResult.Fail(reason, "Illegal target " + targetIds[i] + ".");
            }
            return ValidationResult.Ok;
        }

        /// <summary>Returns None if <paramref name="target"/> is a legal target of <paramref name="def"/> used by <paramref name="user"/>.</summary>
        public RejectReason CheckTarget(PlayerState user, CardBase def, PlayerState target)
        {
            if (target == null || !target.Alive) return RejectReason.InvalidTarget;
            var rule = def.TargetRule;
            bool self = ReferenceEquals(user, target);
            if (rule.Has(TargetFilter.NotSelf) && self) return RejectReason.InvalidTarget;
            if (rule.Has(TargetFilter.Wounded) && !target.IsWounded) return RejectReason.InvalidTarget;
            if (rule.Has(TargetFilter.HasCards) && target.TotalCardCount == 0) return RejectReason.InvalidTarget;
            if (rule.Has(TargetFilter.HasHandCards) && target.HandCards.Count == 0) return RejectReason.InvalidTarget;
            if (rule.Has(TargetFilter.NoSameDelayedTrick))
            {
                foreach (var c in target.JudgeArea)
                    if (c.CardId == def.CardId) return RejectReason.InvalidTarget;
            }
            switch (def.Range.Kind)
            {
                case RangeKind.AttackRange:
                    if (self || !IsInAttackRange(user, target)) return RejectReason.TargetOutOfRange;
                    break;
                case RangeKind.Distance:
                    if (!self && GetDistance(user, target) > def.Range.Distance) return RejectReason.TargetOutOfRange;
                    break;
            }
            for (int i = 0; i < target.Skills.Count; i++)
            {
                var s = target.Skills[i];
                if (!s.Disabled && s.Skill.ProhibitsTargeting(_ctx, target, user, def)) return RejectReason.InvalidTarget;
            }
            return RejectReason.None;
        }

        /// <summary>All players that may be chosen as a target of <paramref name="def"/>.</summary>
        public void GetLegalTargets(int userId, CardBase def, List<int> output)
        {
            var user = S.GetPlayer(userId);
            if (user == null || def == null) return;
            foreach (var p in S.SeatOrder)
            {
                if (CheckTarget(user, def, p) == RejectReason.None) output.Add(p.PlayerId);
            }
        }

        /// <summary>Targets of cards that do not require selection (Self, AllOthers, All).</summary>
        public void GetAutoTargets(PlayerState user, CardBase def, List<int> output)
        {
            switch (def.TargetRule.Kind)
            {
                case TargetKind.Self:
                    if (CheckTarget(user, def, user) == RejectReason.None) output.Add(user.PlayerId);
                    break;
                case TargetKind.AllOthers:
                case TargetKind.All:
                {
                    var order = ListPool<PlayerState>.Get();
                    S.GetAliveInSeatOrder(user, def.TargetRule.Kind == TargetKind.All, order);
                    foreach (var p in order)
                    {
                        if (def.TargetRule.Kind == TargetKind.AllOthers && ReferenceEquals(p, user)) continue;
                        if (CheckTarget(user, def, p) == RejectReason.None) output.Add(p.PlayerId);
                    }
                    ListPool<PlayerState>.Release(order);
                    break;
                }
            }
        }

        /// <summary>Orders chosen targets by seat starting after the user (resolution order).</summary>
        public void SortBySeatFrom(PlayerState user, List<int> targets)
        {
            int n = S.SeatOrder.Count;
            int start = S.SeatIndexOf(user);
            targets.Sort((a, b) =>
            {
                int da = (S.SeatIndexOf(S.GetPlayer(a)) - start + n) % n;
                int db = (S.SeatIndexOf(S.GetPlayer(b)) - start + n) % n;
                return da.CompareTo(db);
            });
        }

        // ------------------------------------------------------------------ distance

        /// <summary>Seat distance counting only living players (dead seats are skipped).</summary>
        public int GetSeatDistance(PlayerState from, PlayerState to)
        {
            if (ReferenceEquals(from, to)) return 0;
            int n = S.SeatOrder.Count;
            int fromIdx = S.SeatIndexOf(from);
            int toIdx = S.SeatIndexOf(to);
            int clockwise = 0;
            for (int i = (fromIdx + 1) % n; ; i = (i + 1) % n)
            {
                if (i == toIdx)
                {
                    clockwise++;
                    break;
                }
                if (S.SeatOrder[i].Alive) clockwise++;
            }
            int counter = 0;
            for (int i = (fromIdx - 1 + n) % n; ; i = (i - 1 + n) % n)
            {
                if (i == toIdx)
                {
                    counter++;
                    break;
                }
                if (S.SeatOrder[i].Alive) counter++;
            }
            return Math.Min(clockwise, counter);
        }

        public int GetDistance(PlayerState from, PlayerState to)
        {
            if (from == null || to == null) return int.MaxValue;
            if (ReferenceEquals(from, to)) return 0;
            int d = GetSeatDistance(from, to)
                    + _ctx.Modifiers.Evaluate(ModifierKind.OutgoingDistance, from, 0)
                    + _ctx.Modifiers.Evaluate(ModifierKind.IncomingDistance, to, 0);
            return Math.Max(1, d);
        }

        public int GetDistance(int fromId, int toId) => GetDistance(S.GetPlayer(fromId), S.GetPlayer(toId));

        public int GetAttackRange(PlayerState p) => p == null ? 0 : Math.Max(1, _ctx.Modifiers.Evaluate(ModifierKind.AttackRange, p, 1));

        public int GetAttackRange(int playerId) => GetAttackRange(S.GetPlayer(playerId));

        public bool IsInAttackRange(PlayerState from, PlayerState to)
        {
            return from != null && to != null && !ReferenceEquals(from, to) && to.Alive && GetDistance(from, to) <= GetAttackRange(from);
        }

        public bool IsInAttackRange(int fromId, int toId) => IsInAttackRange(S.GetPlayer(fromId), S.GetPlayer(toId));

        public int GetMaxHandSize(PlayerState p) => p == null ? 0 : Math.Max(0, _ctx.Modifiers.Evaluate(ModifierKind.MaxHandSize, p, Math.Max(0, p.Hp)));

        public int GetMaxHandSize(int playerId) => GetMaxHandSize(S.GetPlayer(playerId));

        public int GetDrawPhaseCount(PlayerState p) => Math.Max(0, _ctx.Modifiers.Evaluate(ModifierKind.DrawPhaseCount, p, _ctx.Config.DrawPerTurn));

        // ------------------------------------------------------------------ responses

        public bool CanCardServeAs(PlayerState player, CardInstance card, string requiredCardId, string skillId)
        {
            if (player == null || card == null || requiredCardId == null) return false;
            if (skillId == null) return card.CardId == requiredCardId;
            var si = player.FindSkill(skillId);
            return si != null && !si.Disabled && si.Skill.CanConvert(_ctx, player, card, requiredCardId, true);
        }

        /// <summary>
        /// Whether the player could possibly answer a card response request. Used to skip hopeless
        /// requests when <see cref="GameModes.GameModeConfig.AutoSkipImpossibleResponses"/> is on.
        /// </summary>
        public bool HasPossibleResponse(PlayerState player, string requiredCardId)
        {
            if (player == null || !player.Alive) return false;
            foreach (var c in player.HandCards)
            {
                if (c.CardId == requiredCardId) return true;
                for (int i = 0; i < player.Skills.Count; i++)
                {
                    var si = player.Skills[i];
                    if (!si.Disabled && si.Skill.CanConvert(_ctx, player, c, requiredCardId, true)) return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ skills

        public ValidationResult ValidateSkillActivation(int playerId, UseSkillCommand cmd)
        {
            var p = S.GetPlayer(playerId);
            if (p == null) return ValidationResult.Fail(RejectReason.UnknownPlayer);
            if (!p.Alive) return ValidationResult.Fail(RejectReason.PlayerDead);
            if (cmd == null || cmd.SkillId == null) return ValidationResult.Fail(RejectReason.MalformedCommand);
            var basic = CheckPlayWindow(p);
            if (!basic.IsValid) return basic;
            var si = p.FindSkill(cmd.SkillId);
            if (si == null || si.Disabled || !(si.Skill is ActiveSkillBase active))
                return ValidationResult.Fail(RejectReason.SkillUnavailable, "No usable skill '" + cmd.SkillId + "'.");
            if (!active.CanActivate(_ctx, p, si)) return ValidationResult.Fail(RejectReason.SkillUnavailable, "Skill cannot be used now.");
            var cards = cmd.CardIds ?? Array.Empty<int>();
            var targets = cmd.TargetIds ?? Array.Empty<int>();
            if (!RequestValidation.AllDistinct(cards) || !RequestValidation.AllDistinct(targets))
                return ValidationResult.Fail(RejectReason.MalformedCommand, "Duplicate ids.");
            foreach (int id in cards)
            {
                if (p.FindOwnedCard(id, active.AllowsEquipmentCards) == null)
                    return ValidationResult.Fail(RejectReason.CardNotOwned, "Card " + id + " is not owned.");
            }
            foreach (int id in targets)
            {
                var t = S.GetPlayer(id);
                if (t == null || !t.Alive) return ValidationResult.Fail(RejectReason.InvalidTarget);
            }
            return active.ValidateActivation(_ctx, p, cmd);
        }
    }
}
