using System.Collections.Generic;
using Sanguo.Core;

namespace Sanguo.Network
{
    /// <summary>Wire format of a per-viewer <see cref="ClientGameState"/> snapshot.</summary>
    public static class SnapshotCodec
    {
        public static byte[] Encode(ClientGameState s, int lastAcceptedSequence)
        {
            var w = new NetWriter(2048);
            w.WriteVarInt(lastAcceptedSequence);
            w.WriteVarInt(s.ViewerId);
            w.WriteString(s.RoomId);
            w.WriteString(s.ModeId);
            w.WriteVarInt(s.LastEventSequence);
            w.WriteByte((byte)s.Phase);
            w.WriteVarInt(s.CurrentPlayerId);
            w.WriteVarInt(s.TurnNumber);
            w.WriteVarInt(s.Round);
            w.WriteVarInt(s.DrawPileCount);
            CommonCodec.WriteCards(w, s.DiscardPile);
            CommonCodec.WriteCards(w, s.Processing);
            w.WriteBool(s.IsGameOver);
            CommonCodec.WriteResult(w, s.Result);
            w.WriteVarUInt((uint)s.Players.Count);
            foreach (var p in s.Players) WritePlayer(w, p);
            w.WriteVarUInt((uint)s.OpenRequests.Count);
            foreach (var r in s.OpenRequests) CommonCodec.WriteRequest(w, r);
            return w.ToArray();
        }

        public static ClientGameState Decode(byte[] payload, out int lastAcceptedSequence)
        {
            var r = new NetReader(payload);
            lastAcceptedSequence = r.ReadVarInt();
            var s = new ClientGameState
            {
                ViewerId = r.ReadVarInt(),
                RoomId = r.ReadString(),
                ModeId = r.ReadString(),
                LastEventSequence = r.ReadVarInt(),
                Phase = r.ReadEnum<GamePhase>(),
                CurrentPlayerId = r.ReadVarInt(),
                TurnNumber = r.ReadVarInt(),
                Round = r.ReadVarInt(),
                DrawPileCount = r.ReadVarInt(),
                DiscardPile = CommonCodec.ReadCards(r) ?? new List<CardInfo>(),
                Processing = CommonCodec.ReadCards(r) ?? new List<CardInfo>(),
                IsGameOver = r.ReadBool(),
                Result = CommonCodec.ReadResult(r)
            };
            uint players = r.ReadVarUInt();
            if (players > 64) throw new ProtocolException("Too many players.");
            for (int i = 0; i < players; i++) s.Players.Add(ReadPlayer(r));
            uint requests = r.ReadVarUInt();
            if (requests > 64) throw new ProtocolException("Too many requests.");
            for (int i = 0; i < requests; i++) s.OpenRequests.Add(CommonCodec.ReadRequest(r));
            if (!r.AtEnd) throw new ProtocolException("Trailing bytes in snapshot.");
            return s;
        }

        private static void WritePlayer(NetWriter w, ClientPlayerState p)
        {
            w.WriteVarInt(p.PlayerId);
            w.WriteString(p.Nickname);
            w.WriteVarInt(p.AvatarId);
            w.WriteVarInt(p.Seat);
            w.WriteByte((byte)p.Team);
            w.WriteByte((byte)p.Role);
            w.WriteBool(p.RoleRevealed);
            w.WriteString(p.CharacterId);
            w.WriteVarInt(p.Hp);
            w.WriteVarInt(p.MaxHp);
            w.WriteVarInt(p.HandCount);
            CommonCodec.WriteCards(w, p.HandCards);
            for (int i = 1; i < EquipmentArea.SlotCount; i++) CommonCodec.WriteCard(w, p.Equipment[i]);
            CommonCodec.WriteCards(w, p.JudgeArea);
            w.WriteVarUInt((uint)p.Statuses.Count);
            foreach (var st in p.Statuses)
            {
                w.WriteString(st.StatusId);
                w.WriteVarInt(st.Stacks);
                w.WriteVarInt(st.RemainingTurns);
            }
            w.WriteVarUInt((uint)p.Skills.Count);
            foreach (var sk in p.Skills)
            {
                w.WriteString(sk.SkillId);
                w.WriteBool(sk.UsedUp);
                w.WriteBool(sk.Disabled);
            }
            w.WriteBool(p.Alive);
            w.WriteBool(p.IsDying);
            w.WriteBool(p.Connected);
            w.WriteBool(p.AIControlled);
        }

        private static ClientPlayerState ReadPlayer(NetReader r)
        {
            var p = new ClientPlayerState
            {
                PlayerId = r.ReadVarInt(),
                Nickname = r.ReadString(),
                AvatarId = r.ReadVarInt(),
                Seat = r.ReadVarInt(),
                Team = r.ReadEnum<Team>(),
                Role = r.ReadEnum<Role>(),
                RoleRevealed = r.ReadBool(),
                CharacterId = r.ReadString(),
                Hp = r.ReadVarInt(),
                MaxHp = r.ReadVarInt(),
                HandCount = r.ReadVarInt(),
                HandCards = CommonCodec.ReadCards(r)
            };
            for (int i = 1; i < EquipmentArea.SlotCount; i++) p.Equipment[i] = CommonCodec.ReadCard(r);
            p.JudgeArea = CommonCodec.ReadCards(r) ?? new List<CardInfo>();
            uint statuses = r.ReadVarUInt();
            if (statuses > 256) throw new ProtocolException("Too many statuses.");
            for (int i = 0; i < statuses; i++)
                p.Statuses.Add(new ClientStatus { StatusId = r.ReadString(), Stacks = r.ReadVarInt(), RemainingTurns = r.ReadVarInt() });
            uint skills = r.ReadVarUInt();
            if (skills > 64) throw new ProtocolException("Too many skills.");
            for (int i = 0; i < skills; i++)
                p.Skills.Add(new ClientSkillState { SkillId = r.ReadString(), UsedUp = r.ReadBool(), Disabled = r.ReadBool() });
            p.Alive = r.ReadBool();
            p.IsDying = r.ReadBool();
            p.Connected = r.ReadBool();
            p.AIControlled = r.ReadBool();
            return p;
        }
    }
}
