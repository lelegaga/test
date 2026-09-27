using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.GameModes;

namespace Sanguo.Network
{
    /// <summary>Wire format of shared value types (cards, requests, results).</summary>
    public static class CommonCodec
    {
        public static void WriteCard(NetWriter w, CardInfo c)
        {
            if (c == null)
            {
                w.WriteBool(false);
                return;
            }
            w.WriteBool(true);
            w.WriteVarInt(c.InstanceId);
            w.WriteString(c.CardId);
            w.WriteByte((byte)c.Suit);
            w.WriteVarInt(c.Number);
        }

        public static CardInfo ReadCard(NetReader r)
        {
            if (!r.ReadBool()) return null;
            int id = r.ReadVarInt();
            string cardId = r.ReadString();
            var suit = r.ReadEnum<Suit>();
            int number = r.ReadVarInt();
            return new CardInfo(id, cardId, suit, number);
        }

        public static void WriteCards(NetWriter w, List<CardInfo> cards)
        {
            if (!w.WriteListHeader(cards)) return;
            foreach (var c in cards) WriteCard(w, c);
        }

        public static List<CardInfo> ReadCards(NetReader r)
        {
            int n = r.ReadListHeader();
            if (n < 0) return null;
            var list = new List<CardInfo>(n);
            for (int i = 0; i < n; i++) list.Add(ReadCard(r));
            return list;
        }

        public static void WriteResult(NetWriter w, GameResult result)
        {
            if (result == null)
            {
                w.WriteBool(false);
                return;
            }
            w.WriteBool(true);
            w.WriteBool(result.IsDraw);
            w.WriteByte((byte)result.WinningFaction);
            w.WriteByte((byte)result.WinningTeam);
            w.WriteIntList(result.WinnerIds);
            w.WriteString(result.Reason);
        }

        public static GameResult ReadResult(NetReader r)
        {
            if (!r.ReadBool()) return null;
            return new GameResult
            {
                IsDraw = r.ReadBool(),
                WinningFaction = r.ReadEnum<Faction>(),
                WinningTeam = r.ReadEnum<Team>(),
                WinnerIds = r.ReadIntList() ?? new List<int>(),
                Reason = r.ReadString() ?? string.Empty
            };
        }

        public static void WriteRequest(NetWriter w, RequestInfo info)
        {
            w.WriteVarInt(info.RequestId);
            w.WriteVarInt(info.PlayerId);
            w.WriteByte((byte)info.Kind);
            w.WriteInt64(info.DeadlineMs);
            w.WriteBool(info.IsResponseWindow);
            w.WriteBool(info.AllowPass);
            w.WriteString(info.RequiredCardId);
            w.WriteVarInt(info.Count);
            w.WriteVarInt(info.MinCount);
            w.WriteVarInt(info.SourcePlayerId);
            w.WriteVarInt(info.TargetPlayerId);
            w.WriteString(info.ContextCardId);
            w.WriteString(info.SkillId);
            w.WriteString(info.Purpose);
            w.WriteVarInt(info.Zones);
            w.WriteBool(info.HasPrivateDetails);
            if (!info.HasPrivateDetails) return;
            w.WriteIntList(info.Candidates);
            w.WriteStringList(info.Options);
            if (w.WriteListHeader(info.PlayHints))
            {
                foreach (var h in info.PlayHints)
                {
                    w.WriteVarInt(h.CardInstanceId);
                    w.WriteString(h.AsCardId);
                    w.WriteString(h.SkillId);
                    w.WriteBool(h.NeedsTargets);
                    w.WriteVarInt(h.MinTargets);
                    w.WriteVarInt(h.MaxTargets);
                    w.WriteIntList(h.LegalTargets);
                }
            }
            if (w.WriteListHeader(info.SkillHints))
            {
                foreach (var h in info.SkillHints)
                {
                    w.WriteString(h.SkillId);
                    w.WriteVarInt(h.MinCards);
                    w.WriteVarInt(h.MaxCards);
                    w.WriteVarInt(h.MinTargets);
                    w.WriteVarInt(h.MaxTargets);
                    w.WriteIntList(h.LegalTargets);
                }
            }
        }

        public static RequestInfo ReadRequest(NetReader r)
        {
            var info = new RequestInfo
            {
                RequestId = r.ReadVarInt(),
                PlayerId = r.ReadVarInt(),
                Kind = r.ReadEnum<RequestKind>(),
                DeadlineMs = r.ReadInt64(),
                IsResponseWindow = r.ReadBool(),
                AllowPass = r.ReadBool(),
                RequiredCardId = r.ReadString(),
                Count = r.ReadVarInt(),
                MinCount = r.ReadVarInt(),
                SourcePlayerId = r.ReadVarInt(),
                TargetPlayerId = r.ReadVarInt(),
                ContextCardId = r.ReadString(),
                SkillId = r.ReadString(),
                Purpose = r.ReadString(),
                Zones = r.ReadVarInt(),
                HasPrivateDetails = r.ReadBool()
            };
            if (!info.HasPrivateDetails) return info;
            info.Candidates = r.ReadIntList();
            info.Options = r.ReadStringList();
            int n = r.ReadListHeader();
            if (n >= 0)
            {
                info.PlayHints = new List<PlayHint>(n);
                for (int i = 0; i < n; i++)
                {
                    info.PlayHints.Add(new PlayHint
                    {
                        CardInstanceId = r.ReadVarInt(),
                        AsCardId = r.ReadString(),
                        SkillId = r.ReadString(),
                        NeedsTargets = r.ReadBool(),
                        MinTargets = r.ReadVarInt(),
                        MaxTargets = r.ReadVarInt(),
                        LegalTargets = r.ReadIntList() ?? new List<int>()
                    });
                }
            }
            n = r.ReadListHeader();
            if (n >= 0)
            {
                info.SkillHints = new List<SkillHint>(n);
                for (int i = 0; i < n; i++)
                {
                    info.SkillHints.Add(new SkillHint
                    {
                        SkillId = r.ReadString(),
                        MinCards = r.ReadVarInt(),
                        MaxCards = r.ReadVarInt(),
                        MinTargets = r.ReadVarInt(),
                        MaxTargets = r.ReadVarInt(),
                        LegalTargets = r.ReadIntList() ?? new List<int>()
                    });
                }
            }
            return info;
        }
    }
}
