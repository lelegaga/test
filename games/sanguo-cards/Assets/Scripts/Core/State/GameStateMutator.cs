using System;
using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Events;
using Sanguo.GameModes;
using Sanguo.Skills;

namespace Sanguo.Core
{
    /// <summary>
    /// The only code path that changes <see cref="GameState"/>. Every mutation emits the event that
    /// describes it, so the event stream is always a complete, ordered description of state changes
    /// (which is what makes incremental client sync, logs and replays reliable).
    ///
    /// Only reachable through <see cref="GameContext"/>, which exists only on the server.
    /// </summary>
    public sealed class GameStateMutator
    {
        private readonly GameContext _ctx;

        internal GameStateMutator(GameContext ctx)
        {
            _ctx = ctx;
        }

        private GameState S => _ctx.State;

        // ================================================================== cards

        /// <summary>Draws cards from the top of the draw pile, reshuffling the discard pile when needed.</summary>
        public List<CardInstance> DrawCards(PlayerState player, int count, MoveReason reason = MoveReason.Draw)
        {
            var all = new List<CardInstance>();
            if (player == null || count <= 0) return all;
            while (all.Count < count)
            {
                if (S.DrawPile.Count == 0 && !ReshuffleDiscardIntoDrawPile()) break;
                int take = Math.Min(count - all.Count, S.DrawPile.Count);
                var segment = new List<CardInstance>(take);
                for (int i = 0; i < take; i++)
                {
                    var card = S.DrawPile.Top;
                    S.DrawPile.Remove(card);
                    player.HandCards.Add(card);
                    segment.Add(card);
                }
                var e = CreateMove<CardDrawnEvent>(ZoneType.DrawPile, -1, ZoneType.Hand, player.PlayerId, reason, segment, EquipSlot.None);
                _ctx.Emit(e);
                all.AddRange(segment);
            }
            return all;
        }

        /// <summary>Takes the top card of the draw pile into the processing zone (judgements), or null.</summary>
        public CardInstance RevealTopCard(MoveReason reason)
        {
            if (S.DrawPile.Count == 0 && !ReshuffleDiscardIntoDrawPile()) return null;
            var card = S.DrawPile.Top;
            MoveCards(new[] { card }, ZoneType.Processing, -1, reason);
            return card;
        }

        /// <summary>Moves the whole discard pile into the draw pile and shuffles it. False if nothing to move.</summary>
        public bool ReshuffleDiscardIntoDrawPile()
        {
            if (S.DiscardPile.Count == 0) return false;
            var cards = new List<CardInstance>(S.DiscardPile);
            MoveCards(cards, ZoneType.DrawPile, -1, MoveReason.Reshuffle);
            S.DrawPile.Shuffle(_ctx.Random);
            _ctx.Emit(new DeckReshuffledEvent { DrawPileCount = S.DrawPile.Count });
            return true;
        }

        /// <summary>Generic move. Cards from different source zones produce one event per source.</summary>
        public void MoveCards(IReadOnlyList<CardInstance> cards, ZoneType toZone, int toOwner, MoveReason reason)
        {
            MoveInternal<CardMovedEvent>(cards, toZone, toOwner, reason, null);
        }

        /// <summary>Puts cards face up into the discard pile.</summary>
        public void Discard(IReadOnlyList<CardInstance> cards, MoveReason reason)
        {
            MoveInternal<CardDiscardedEvent>(cards, ZoneType.DiscardPile, -1, reason, null);
        }

        /// <summary>
        /// Moves the physical cards of a card use to the processing zone and records the use
        /// (<see cref="CardPlayedEvent"/>). Works for virtual uses with no physical card too.
        /// </summary>
        public void PlayCards(CardUse use)
        {
            var e = new CardPlayedEvent
            {
                UserId = use.UserId,
                UseId = use.UseId,
                UsedAsCardId = use.Definition.CardId,
                ConversionSkillId = use.ConversionSkillId,
                Targets = use.Targets.ToArray(),
                IsResponse = use.IsResponse,
                ToZone = ZoneType.Processing,
                Reason = use.IsResponse ? MoveReason.Respond : MoveReason.Use,
                IsPublic = true
            };
            if (use.PhysicalCards.Count == 0)
            {
                e.FromZone = ZoneType.None;
                e.Cards = new List<CardInfo>();
                _ctx.Emit(e);
                return;
            }
            // A use may combine cards from several zones (hand + equipment); all but the last group
            // move as plain CardMoved events so the CardPlayed event has a single source.
            var groups = GroupBySource(use.PhysicalCards);
            for (int g = 0; g < groups.Count - 1; g++)
                MoveGroup<CardMovedEvent>(groups[g], ZoneType.Processing, -1, e.Reason, null);
            MoveGroup(groups[groups.Count - 1], ZoneType.Processing, -1, e.Reason, e);
        }

        /// <summary>Equips a card, discarding whatever occupied the slot.</summary>
        public void Equip(PlayerState player, CardInstance card)
        {
            var slot = EquipmentArea.SlotOf(card);
            if (slot == EquipSlot.None) throw new InvalidOperationException(card + " is not equipment.");
            var old = player.Equipment.Get(slot);
            if (old != null && !ReferenceEquals(old, card)) Discard(new[] { old }, MoveReason.Replace);
            MoveInternal<CardMovedEvent>(new[] { card }, ZoneType.Equipment, player.PlayerId, MoveReason.Equip, null);
        }

        public void PlaceInJudgeArea(PlayerState target, CardInstance card)
        {
            MoveInternal<CardMovedEvent>(new[] { card }, ZoneType.JudgeArea, target.PlayerId, MoveReason.PlaceDelayedTrick, null);
        }

        private void MoveInternal<T>(IReadOnlyList<CardInstance> cards, ZoneType toZone, int toOwner, MoveReason reason, T prepared)
            where T : CardMoveEvent, new()
        {
            if (cards == null || cards.Count == 0) return;
            var groups = GroupBySource(cards);
            for (int g = 0; g < groups.Count; g++)
                MoveGroup(groups[g], toZone, toOwner, reason, g == groups.Count - 1 ? prepared : null);
        }

        private void MoveGroup<T>(List<CardInstance> group, ZoneType toZone, int toOwner, MoveReason reason, T prepared)
            where T : CardMoveEvent, new()
        {
            var fromZone = group[0].Zone;
            int fromOwner = group[0].OwnerId;
            var slot = toZone == ZoneType.Equipment ? EquipmentArea.SlotOf(group[0]) : EquipSlot.None;
            var e = prepared ?? new T();
            FillMove(e, fromZone, fromOwner, toZone, toOwner, reason, group, slot);
            foreach (var card in group)
            {
                Detach(card);
                Attach(card, toZone, toOwner);
            }
            _ctx.Emit(e);
        }

        private static List<List<CardInstance>> GroupBySource(IReadOnlyList<CardInstance> cards)
        {
            var groups = new List<List<CardInstance>>();
            foreach (var c in cards)
            {
                if (c == null) continue;
                List<CardInstance> target = null;
                foreach (var g in groups)
                {
                    if (g[0].Zone == c.Zone && g[0].OwnerId == c.OwnerId)
                    {
                        target = g;
                        break;
                    }
                }
                if (target == null)
                {
                    target = new List<CardInstance>();
                    groups.Add(target);
                }
                if (!target.Contains(c)) target.Add(c);
            }
            return groups;
        }

        private T CreateMove<T>(ZoneType from, int fromOwner, ZoneType to, int toOwner, MoveReason reason, List<CardInstance> cards, EquipSlot slot)
            where T : CardMoveEvent, new()
        {
            var e = new T();
            FillMove(e, from, fromOwner, to, toOwner, reason, cards, slot);
            return e;
        }

        private static void FillMove(CardMoveEvent e, ZoneType from, int fromOwner, ZoneType to, int toOwner, MoveReason reason, List<CardInstance> cards, EquipSlot slot)
        {
            e.FromZone = from;
            e.FromOwner = IsOwnedZone(from) ? fromOwner : -1;
            e.ToZone = to;
            e.ToOwner = IsOwnedZone(to) ? toOwner : -1;
            e.Reason = reason;
            e.Slot = slot;
            e.Count = cards.Count;
            e.Cards = new List<CardInfo>(cards.Count);
            foreach (var c in cards) e.Cards.Add(CardInfo.From(c));
            ComputeVisibility(from, e.FromOwner, to, e.ToOwner, out e.IsPublic, out e.KnownTo);
        }

        private static bool IsOwnedZone(ZoneType z) => z == ZoneType.Hand || z == ZoneType.Equipment || z == ZoneType.JudgeArea;

        private static bool IsPublicZone(ZoneType z)
        {
            return z == ZoneType.DiscardPile || z == ZoneType.Processing || z == ZoneType.Equipment || z == ZoneType.JudgeArea || z == ZoneType.Removed;
        }

        /// <summary>
        /// Moves touching a face-up zone are public. Moves between hidden zones (draw pile, hands)
        /// are known only to the hand owners involved.
        /// </summary>
        internal static void ComputeVisibility(ZoneType from, int fromOwner, ZoneType to, int toOwner, out bool isPublic, out int[] knownTo)
        {
            if (IsPublicZone(from) || IsPublicZone(to))
            {
                isPublic = true;
                knownTo = null;
                return;
            }
            isPublic = false;
            int a = from == ZoneType.Hand ? fromOwner : -1;
            int b = to == ZoneType.Hand ? toOwner : -1;
            if (a >= 0 && b >= 0 && a != b) knownTo = new[] { a, b };
            else if (a >= 0) knownTo = new[] { a };
            else if (b >= 0) knownTo = new[] { b };
            else knownTo = Array.Empty<int>();
        }

        private void Detach(CardInstance card)
        {
            bool removed;
            switch (card.Zone)
            {
                case ZoneType.DrawPile: removed = S.DrawPile.Remove(card); break;
                case ZoneType.DiscardPile: removed = S.DiscardPile.Remove(card); break;
                case ZoneType.Processing: removed = S.Processing.Remove(card); break;
                case ZoneType.Hand: removed = S.GetPlayer(card.OwnerId).HandCards.Remove(card); break;
                case ZoneType.Equipment: removed = S.GetPlayer(card.OwnerId).Equipment.Remove(card); break;
                case ZoneType.JudgeArea: removed = S.GetPlayer(card.OwnerId).JudgeArea.Remove(card); break;
                default: removed = false; break;
            }
            if (!removed) throw new InvalidOperationException("Card " + card + " was not found in its zone " + card.Zone + ".");
        }

        private void Attach(CardInstance card, ZoneType zone, int owner)
        {
            switch (zone)
            {
                case ZoneType.DrawPile: S.DrawPile.Add(card); break;
                case ZoneType.DiscardPile: S.DiscardPile.Add(card); break;
                case ZoneType.Processing: S.Processing.Add(card); break;
                case ZoneType.Hand: S.GetPlayer(owner).HandCards.Add(card); break;
                case ZoneType.Equipment: S.GetPlayer(owner).Equipment.Set(EquipmentArea.SlotOf(card), card); break;
                case ZoneType.JudgeArea: S.GetPlayer(owner).JudgeArea.Add(card); break;
                default: throw new InvalidOperationException("Cannot move a card to zone " + zone + ".");
            }
        }

        // ================================================================== HP and life

        /// <summary>Changes HP by <paramref name="delta"/>. Healing is capped at max HP; damage may go below 0.</summary>
        public void ChangeHp(PlayerState player, int delta, HpChangeReason reason)
        {
            int old = player.Hp;
            int next = old + delta;
            if (delta > 0 && next > player.MaxHp) next = player.MaxHp;
            if (next == old) return;
            player.Hp = next;
            _ctx.Emit(new HpChangedEvent { PlayerId = player.PlayerId, OldHp = old, NewHp = next, MaxHp = player.MaxHp, Reason = reason });
        }

        public void SetMaxHp(PlayerState player, int maxHp)
        {
            int oldHp = player.Hp;
            player.MaxHp = Math.Max(0, maxHp);
            if (player.Hp > player.MaxHp) player.Hp = player.MaxHp;
            _ctx.Emit(new HpChangedEvent { PlayerId = player.PlayerId, OldHp = oldHp, NewHp = player.Hp, MaxHp = player.MaxHp, Reason = HpChangeReason.MaxHpChange });
        }

        public void SetDying(PlayerState player, bool dying)
        {
            if (player.IsDying == dying) return;
            player.IsDying = dying;
            _ctx.Emit(new PlayerDyingEvent { PlayerId = player.PlayerId, Entered = dying });
        }

        public void Kill(PlayerState player, int killerId, bool revealRole)
        {
            player.Alive = false;
            player.IsDying = false;
            bool hasRole = player.Role != Role.None;
            if (revealRole && hasRole) player.RoleRevealed = true;
            _ctx.Emit(new PlayerDiedEvent
            {
                PlayerId = player.PlayerId,
                KillerId = killerId,
                RevealedRole = revealRole && hasRole ? player.Role : Role.None
            });
        }

        public void RevealRole(PlayerState player)
        {
            if (player.RoleRevealed || player.Role == Role.None) return;
            player.RoleRevealed = true;
            _ctx.Emit(new RoleRevealedEvent { PlayerId = player.PlayerId, Role = player.Role });
        }

        // ================================================================== turn flow

        public void TransitionPhase(GamePhase phase)
        {
            S.StateMachine.TransitionTo(phase);
            _ctx.Emit(new PhaseChangedEvent { PlayerId = S.Turn.CurrentPlayerId, Phase = phase });
        }

        public void StartTurn(PlayerState player, PlayerState firstPlayer)
        {
            int n = S.SeatOrder.Count;
            int firstIdx = S.SeatIndexOf(firstPlayer ?? player);
            int Rel(PlayerState p) => (S.SeatIndexOf(p) - firstIdx + n) % n;
            var previous = S.CurrentPlayer;
            if (S.Turn.TurnNumber == 0 || previous == null) S.Turn.Round = 1;
            else if (Rel(player) <= Rel(previous)) S.Turn.Round++;
            S.Turn.TurnNumber++;
            S.Turn.Reset(player.PlayerId);
            _ctx.Emit(new TurnStartedEvent { PlayerId = player.PlayerId, TurnNumber = S.Turn.TurnNumber, Round = S.Turn.Round });
        }

        public void SkipPhase(GamePhase phase)
        {
            _ctx.Emit(new PhaseSkippedEvent { PlayerId = S.Turn.CurrentPlayerId, Phase = phase });
        }

        /// <summary>Marks a phase of the current turn to be skipped (e.g. by a delayed trick).</summary>
        public void MarkPhaseSkipped(GamePhase phase) => S.Turn.SkipPhase(phase);

        public void RequestEndTurn() => S.Turn.EndTurnRequested = true;

        public void EndTurn(PlayerState player)
        {
            _ctx.Emit(new TurnEndedEvent { PlayerId = player.PlayerId });
        }

        public void IncrementCardUsage(string key) => S.Turn.IncrementCardUsage(key);

        internal void CountPlayAction() => S.Turn.PlayActionsThisPhase++;

        // ================================================================== skills and statuses

        public void RecordSkillUse(PlayerState player, SkillInstance skill)
        {
            skill.UsesThisTurn++;
            skill.UsesThisPhase++;
            skill.UsesThisGame++;
            if (skill.Skill.IsLimited && !skill.UsedUp)
            {
                skill.UsedUp = true;
                _ctx.Emit(new SkillStateChangedEvent { PlayerId = player.PlayerId, SkillId = skill.Skill.SkillId, UsedUp = true, Disabled = skill.Disabled });
            }
        }

        public void SetSkillDisabled(PlayerState player, SkillInstance skill, bool disabled)
        {
            if (skill.Disabled == disabled) return;
            skill.Disabled = disabled;
            _ctx.Emit(new SkillStateChangedEvent { PlayerId = player.PlayerId, SkillId = skill.Skill.SkillId, UsedUp = skill.UsedUp, Disabled = disabled });
        }

        public SkillInstance AddSkill(PlayerState player, SkillBase skill)
        {
            var existing = player.FindSkill(skill.SkillId);
            if (existing != null) return existing;
            var si = new SkillInstance(skill);
            player.Skills.Add(si);
            _ctx.Emit(new SkillStateChangedEvent { PlayerId = player.PlayerId, SkillId = skill.SkillId });
            return si;
        }

        public void SetStatus(PlayerState player, string statusId, int stacks, int remainingTurns, int sourcePlayerId)
        {
            var s = player.FindStatus(statusId);
            if (stacks <= 0)
            {
                if (s == null) return;
                player.StatusEffects.Remove(s);
            }
            else if (s == null)
            {
                player.StatusEffects.Add(new StatusEffect(statusId, stacks, remainingTurns, sourcePlayerId));
            }
            else
            {
                s.Stacks = stacks;
                s.RemainingTurns = remainingTurns;
            }
            _ctx.Emit(new StatusChangedEvent { PlayerId = player.PlayerId, StatusId = statusId, Stacks = Math.Max(0, stacks), RemainingTurns = remainingTurns });
        }

        /// <summary>Resets per-phase skill counters of every player (called at each phase start).</summary>
        internal void ResetPhaseSkillCounters()
        {
            foreach (var p in S.Players)
                foreach (var s in p.Skills) s.UsesThisPhase = 0;
        }

        /// <summary>Resets per-turn skill counters of every player (called at each turn start).</summary>
        internal void ResetTurnSkillCounters()
        {
            foreach (var p in S.Players)
                foreach (var s in p.Skills)
                {
                    s.UsesThisTurn = 0;
                    s.UsesThisPhase = 0;
                }
        }

        // ================================================================== connection

        public void SetConnection(PlayerState player, bool connected, bool aiControlled)
        {
            if (player.Connected == connected && player.AIControlled == aiControlled) return;
            player.Connected = connected;
            player.AIControlled = aiControlled;
            _ctx.Emit(new PlayerStatusChangedEvent { PlayerId = player.PlayerId, Connected = connected, AIControlled = aiControlled });
        }

        // ================================================================== game end

        public void EndGame(GameResult result)
        {
            if (S.IsGameOver) return;
            S.IsGameOver = true;
            S.Result = result;
            var e = new GameEndedEvent { Result = result.Clone() };
            foreach (var p in S.Players)
            {
                if (p.Role != Role.None) p.RoleRevealed = true;
                e.Roles.Add(new PlayerRole(p.PlayerId, p.Role));
            }
            _ctx.Requests.AbortAll();
            if (S.StateMachine.Phase != GamePhase.GameOver) S.StateMachine.TransitionTo(GamePhase.GameOver);
            _ctx.Emit(e);
        }
    }
}
