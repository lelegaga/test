using System.Collections.Generic;
using Sanguo.Cards;

namespace Sanguo.Core
{
    /// <summary>
    /// Builds the per-viewer <see cref="ClientGameState"/> ("GameStateSnapshot") sent on game entry,
    /// reconnection and desync recovery. This is the gate that keeps hidden information on the
    /// server: other players' hands are reduced to counts and hidden roles become Unknown.
    /// </summary>
    public static class SnapshotBuilder
    {
        /// <summary>Viewer id used for spectators (sees public information only).</summary>
        public const int Spectator = -1;

        public static ClientGameState Build(GameContext ctx, int viewerId)
        {
            var s = ctx.State;
            var cs = new ClientGameState
            {
                ViewerId = viewerId,
                RoomId = s.RoomId,
                ModeId = ctx.Mode.ModeId,
                LastEventSequence = ctx.Events.LastSequence,
                Phase = s.Phase,
                CurrentPlayerId = s.Turn.CurrentPlayerId,
                TurnNumber = s.Turn.TurnNumber,
                Round = s.Turn.Round,
                DrawPileCount = s.DrawPile.Count,
                DiscardPile = ToInfos(s.DiscardPile),
                Processing = ToInfos(s.Processing),
                IsGameOver = s.IsGameOver,
                Result = s.Result?.Clone()
            };

            foreach (var p in s.Players)
            {
                bool self = p.PlayerId == viewerId;
                bool roleVisible = ctx.Mode.IsRoleVisibleTo(s, p, viewerId);
                var cp = new ClientPlayerState
                {
                    PlayerId = p.PlayerId,
                    Nickname = p.Nickname,
                    AvatarId = p.AvatarId,
                    Seat = p.Seat,
                    Team = p.Team,
                    Role = roleVisible ? p.Role : (p.Role == Role.None ? Role.None : Role.Unknown),
                    RoleRevealed = p.RoleRevealed,
                    CharacterId = p.Character?.Id,
                    Hp = p.Hp,
                    MaxHp = p.MaxHp,
                    HandCount = p.HandCards.Count,
                    HandCards = self ? ToInfos(p.HandCards) : null,
                    JudgeArea = ToInfos(p.JudgeArea),
                    Alive = p.Alive,
                    IsDying = p.IsDying,
                    Connected = p.Connected,
                    AIControlled = p.AIControlled
                };
                foreach (var card in p.Equipment.Cards) cp.Equipment[(int)EquipmentArea.SlotOf(card)] = CardInfo.From(card);
                foreach (var st in p.StatusEffects)
                    cp.Statuses.Add(new ClientStatus { StatusId = st.StatusId, Stacks = st.Stacks, RemainingTurns = st.RemainingTurns });
                foreach (var sk in p.Skills)
                    cp.Skills.Add(new ClientSkillState { SkillId = sk.Skill.SkillId, UsedUp = sk.UsedUp, Disabled = sk.Disabled });
                cs.Players.Add(cp);
            }

            foreach (var r in ctx.Requests.OpenRequests)
            {
                var info = r.ToInfo(ctx);
                cs.OpenRequests.Add(r.PlayerId == viewerId ? info : info.PublicView());
            }
            return cs;
        }

        private static List<CardInfo> ToInfos(IEnumerable<CardInstance> cards)
        {
            var list = new List<CardInfo>();
            foreach (var c in cards) list.Add(CardInfo.From(c));
            return list;
        }
    }
}
