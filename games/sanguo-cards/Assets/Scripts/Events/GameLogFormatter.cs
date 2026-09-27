using System;
using System.Collections.Generic;
using System.Text;
using Sanguo.Core;
using Sanguo.Data;

namespace Sanguo.Events
{
    /// <summary>Name lookups used to render log lines.</summary>
    public interface IGameLogNames
    {
        string PlayerName(int playerId);
        string CardName(string cardId);
        string SkillName(string skillId);
    }

    /// <summary>Name lookup backed by content data and a player-name callback.</summary>
    public sealed class GameLogNames : IGameLogNames
    {
        private readonly GameContent _content;
        private readonly Func<int, string> _playerName;

        public GameLogNames(GameContent content, Func<int, string> playerName)
        {
            _content = content;
            _playerName = playerName;
        }

        public string PlayerName(int playerId) => playerId < 0 ? "系统" : _playerName?.Invoke(playerId) ?? ("玩家" + playerId);
        public string CardName(string cardId) => cardId == null ? "?" : _content?.Cards.GetName(cardId) ?? cardId;
        public string SkillName(string skillId) => skillId == null ? "?" : _content?.Skills.GetName(skillId) ?? skillId;

        /// <summary>Names from a client replica (what the local player sees).</summary>
        public static GameLogNames ForClient(GameContent content, ClientGameState state)
        {
            return new GameLogNames(content, id => state.GetPlayer(id)?.Nickname);
        }
    }

    /// <summary>
    /// Turns (projected) events into human readable log lines. Because it only uses what the event
    /// carries, a client log never reveals more than that client was sent.
    /// </summary>
    public static class GameLogFormatter
    {
        /// <summary>Returns the log line for an event, or null if the event is not worth logging.</summary>
        public static string Format(GameEvent e, IGameLogNames n, int viewerId)
        {
            switch (e)
            {
                case GameStartedEvent _:
                    return "游戏开始";
                case CharacterAssignedEvent ca:
                    return n.PlayerName(ca.PlayerId) + " 的武将为【" + ca.CharacterId + "】，体力上限 " + ca.MaxHp;
                case TurnStartedEvent ts:
                    return "第" + ts.Round + "轮：" + n.PlayerName(ts.PlayerId) + " 开始回合";
                case TurnEndedEvent te:
                    return n.PlayerName(te.PlayerId) + " 结束回合";
                case PhaseSkippedEvent ps:
                    return n.PlayerName(ps.PlayerId) + " 跳过了" + PhaseName(ps.Phase);
                case CardDrawnEvent cd:
                {
                    string who = n.PlayerName(cd.ToOwner);
                    string verb = cd.Reason == MoveReason.Deal ? " 获得起始手牌" : " 摸取";
                    if (cd.Cards != null && viewerId == cd.ToOwner) return who + verb + " " + Cards(cd.Cards, n);
                    return who + verb + " " + cd.Count + " 张牌";
                }
                case CardPlayedEvent cp:
                    return FormatPlayed(cp, n);
                case CardDiscardedEvent dis:
                    return FormatDiscard(dis, n);
                case CardMovedEvent mv:
                    return FormatMove(mv, n, viewerId);
                case DeckReshuffledEvent _:
                    return "弃牌堆洗入牌堆";
                case DamageAppliedEvent da:
                    return n.PlayerName(da.TargetId) + " 受到" + (da.SourceId >= 0 && da.SourceId != da.TargetId ? " " + n.PlayerName(da.SourceId) + " 造成的 " : " ") + da.Amount + " 点伤害";
                case DamagePreventedEvent dp:
                    return n.PlayerName(dp.TargetId) + " 防止了伤害";
                case HealAppliedEvent ha:
                    return n.PlayerName(ha.TargetId) + " 回复 " + ha.Amount + " 点生命";
                case HpChangedEvent hp:
                    if (hp.Reason == HpChangeReason.Damage || hp.Reason == HpChangeReason.LoseHp)
                        return n.PlayerName(hp.PlayerId) + " 剩余 " + hp.NewHp + " 点生命";
                    if (hp.Reason == HpChangeReason.Heal)
                        return n.PlayerName(hp.PlayerId) + " 当前 " + hp.NewHp + " 点生命";
                    if (hp.Reason == HpChangeReason.MaxHpChange)
                        return n.PlayerName(hp.PlayerId) + " 的体力上限变为 " + hp.MaxHp;
                    return null;
                case PlayerDyingEvent dy:
                    return n.PlayerName(dy.PlayerId) + (dy.Entered ? " 进入濒死状态" : " 脱离濒死状态");
                case PlayerDiedEvent pd:
                {
                    var sb = new StringBuilder(n.PlayerName(pd.PlayerId)).Append(" 阵亡");
                    if (pd.RevealedRole != Role.None && pd.RevealedRole != Role.Unknown) sb.Append("，身份是【").Append(RoleName(pd.RevealedRole)).Append('】');
                    return sb.ToString();
                }
                case RoleRevealedEvent rr:
                    return n.PlayerName(rr.PlayerId) + " 的身份是【" + RoleName(rr.Role) + "】";
                case SkillActivatedEvent sa:
                    return n.PlayerName(sa.PlayerId) + " 发动技能【" + n.SkillName(sa.SkillId) + "】";
                case JudgementEvent j:
                    return n.PlayerName(j.PlayerId) + " 的【" + n.CardName(j.ForCardId) + "】判定为 " + (j.Card != null ? Card(j.Card, n) : "无") + "，" + (j.Success ? "判定成功" : "判定失败");
                case RequestClosedEvent rc:
                    return rc.TimedOut ? n.PlayerName(rc.PlayerId) + " 操作超时" : null;
                case PlayerStatusChangedEvent st:
                    if (!st.Connected) return n.PlayerName(st.PlayerId) + (st.AIControlled ? " 断线，由AI托管" : " 断开连接");
                    return n.PlayerName(st.PlayerId) + (st.AIControlled ? " 开启托管" : " 恢复操作");
                case GameEndedEvent ge:
                    return FormatEnd(ge, n);
                default:
                    return null;
            }
        }

        private static string FormatPlayed(CardPlayedEvent cp, IGameLogNames n)
        {
            var sb = new StringBuilder(n.PlayerName(cp.UserId));
            string card = "【" + n.CardName(cp.UsedAsCardId) + "】";
            bool converted = cp.Cards != null && cp.Cards.Count > 0 && cp.Cards[0].CardId != cp.UsedAsCardId;
            if (cp.IsResponse)
            {
                sb.Append(" 打出 ").Append(card);
            }
            else if (cp.Targets != null && cp.Targets.Length > 0 && !(cp.Targets.Length == 1 && cp.Targets[0] == cp.UserId))
            {
                sb.Append(" 对 ");
                for (int i = 0; i < cp.Targets.Length; i++)
                {
                    if (i > 0) sb.Append('、');
                    sb.Append(n.PlayerName(cp.Targets[i]));
                }
                sb.Append(" 使用 ").Append(card);
            }
            else
            {
                sb.Append(" 使用 ").Append(card);
            }
            if (converted) sb.Append("（由").Append(Cards(cp.Cards, n)).Append("当作）");
            return sb.ToString();
        }

        private static string FormatDiscard(CardDiscardedEvent d, IGameLogNames n)
        {
            if (d.Cards == null || d.Count == 0) return null;
            switch (d.Reason)
            {
                case MoveReason.DiscardPhase:
                case MoveReason.Discard:
                case MoveReason.Skill:
                    return n.PlayerName(d.FromOwner) + " 弃置了 " + Cards(d.Cards, n);
                case MoveReason.Dismantle:
                    return n.PlayerName(d.FromOwner) + " 的 " + Cards(d.Cards, n) + " 被弃置";
                case MoveReason.Replace:
                    return n.PlayerName(d.FromOwner) + " 替换下 " + Cards(d.Cards, n);
                case MoveReason.Death:
                    return n.PlayerName(d.FromOwner) + " 的 " + d.Count + " 张牌进入弃牌堆";
                default:
                    return null;
            }
        }

        private static string FormatMove(CardMovedEvent m, IGameLogNames n, int viewerId)
        {
            switch (m.Reason)
            {
                case MoveReason.Equip:
                    return n.PlayerName(m.ToOwner) + " 装备了 " + (m.Cards != null ? Cards(m.Cards, n) : "装备");
                case MoveReason.PlaceDelayedTrick:
                    return n.PlayerName(m.ToOwner) + " 的判定区被置入 " + (m.Cards != null ? Cards(m.Cards, n) : "一张牌");
                case MoveReason.Steal:
                case MoveReason.Give:
                {
                    string what = m.Cards != null ? Cards(m.Cards, n) : m.Count + " 张牌";
                    return n.PlayerName(m.ToOwner) + " 获得了 " + n.PlayerName(m.FromOwner) + " 的 " + what;
                }
                default:
                    return null;
            }
        }

        private static string FormatEnd(GameEndedEvent ge, IGameLogNames n)
        {
            var r = ge.Result;
            if (r == null || r.IsDraw) return "游戏结束：平局";
            var sb = new StringBuilder("游戏结束：");
            for (int i = 0; i < r.WinnerIds.Count; i++)
            {
                if (i > 0) sb.Append('、');
                sb.Append(n.PlayerName(r.WinnerIds[i]));
            }
            sb.Append(" 获胜");
            return sb.ToString();
        }

        private static string Card(CardInfo c, IGameLogNames n) => "【" + n.CardName(c.CardId) + "】";

        private static string Cards(List<CardInfo> cards, IGameLogNames n)
        {
            var sb = new StringBuilder();
            foreach (var c in cards) sb.Append(Card(c, n));
            return sb.ToString();
        }

        public static string RoleName(Role role)
        {
            switch (role)
            {
                case Role.Lord: return "主公";
                case Role.Loyalist: return "忠臣";
                case Role.Rebel: return "反贼";
                case Role.Renegade: return "内奸";
                case Role.Captain: return "队长";
                case Role.Member: return "队员";
                case Role.Unknown: return "未知";
                default: return "无";
            }
        }

        public static string PhaseName(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.TurnStart: return "回合开始阶段";
                case GamePhase.JudgePhase: return "判定阶段";
                case GamePhase.DrawPhase: return "摸牌阶段";
                case GamePhase.PlayPhase: return "出牌阶段";
                case GamePhase.DiscardPhase: return "弃牌阶段";
                case GamePhase.TurnEnd: return "回合结束阶段";
                default: return phase.ToString();
            }
        }
    }
}
