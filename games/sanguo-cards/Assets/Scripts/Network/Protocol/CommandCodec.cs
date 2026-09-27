using Sanguo.Core;

namespace Sanguo.Network
{
    /// <summary>Wire format of player commands (intents).</summary>
    public static class CommandCodec
    {
        public static MessageType MessageTypeOf(GameCommand cmd)
        {
            switch (cmd.Type)
            {
                case CommandType.PlayCard: return MessageType.PlayCard;
                case CommandType.UseSkill: return MessageType.UseSkill;
                case CommandType.SelectTarget: return MessageType.SelectTarget;
                case CommandType.Respond: return MessageType.RespondCard;
                default: return MessageType.EndTurn;
            }
        }

        public static bool IsCommandMessage(MessageType type)
        {
            return type == MessageType.PlayCard || type == MessageType.UseSkill || type == MessageType.SelectTarget
                   || type == MessageType.RespondCard || type == MessageType.EndTurn;
        }

        public static byte[] Encode(GameCommand cmd)
        {
            var w = new NetWriter(64);
            w.WriteVarInt(cmd.RequestId);
            switch (cmd)
            {
                case PlayCardCommand p:
                    w.WriteVarInt(p.CardInstanceId);
                    w.WriteIntArray(p.TargetIds);
                    w.WriteString(p.SkillId);
                    w.WriteString(p.AsCardId);
                    break;
                case UseSkillCommand u:
                    w.WriteString(u.SkillId);
                    w.WriteIntArray(u.CardIds);
                    w.WriteIntArray(u.TargetIds);
                    break;
                case SelectTargetCommand s:
                    w.WriteIntArray(s.TargetIds);
                    break;
                case RespondCommand r:
                    w.WriteBool(r.Pass);
                    w.WriteIntArray(r.CardIds);
                    w.WriteVarInt(r.OptionIndex);
                    w.WriteByte((byte)r.PickZone);
                    w.WriteVarInt(r.PickIndex);
                    w.WriteString(r.SkillId);
                    break;
            }
            return w.ToArray();
        }

        /// <summary>Decodes a command message. Player id and sequence come from the envelope (the server overrides the id).</summary>
        public static GameCommand Decode(NetworkMessage m)
        {
            var r = m.PayloadReader();
            int requestId = r.ReadVarInt();
            GameCommand cmd;
            switch (m.Type)
            {
                case MessageType.PlayCard:
                    cmd = new PlayCardCommand
                    {
                        CardInstanceId = r.ReadVarInt(),
                        TargetIds = r.ReadIntArray() ?? new int[0],
                        SkillId = r.ReadString(),
                        AsCardId = r.ReadString()
                    };
                    break;
                case MessageType.UseSkill:
                    cmd = new UseSkillCommand
                    {
                        SkillId = r.ReadString(),
                        CardIds = r.ReadIntArray() ?? new int[0],
                        TargetIds = r.ReadIntArray() ?? new int[0]
                    };
                    break;
                case MessageType.SelectTarget:
                    cmd = new SelectTargetCommand { TargetIds = r.ReadIntArray() ?? new int[0] };
                    break;
                case MessageType.RespondCard:
                    cmd = new RespondCommand
                    {
                        Pass = r.ReadBool(),
                        CardIds = r.ReadIntArray() ?? new int[0],
                        OptionIndex = r.ReadVarInt(),
                        PickZone = r.ReadEnum<ZoneType>(),
                        PickIndex = r.ReadVarInt(),
                        SkillId = r.ReadString()
                    };
                    break;
                case MessageType.EndTurn:
                    cmd = new EndTurnCommand();
                    break;
                default:
                    throw new ProtocolException(m.Type + " is not a command.");
            }
            if (!r.AtEnd) throw new ProtocolException("Trailing bytes in command.");
            cmd.RequestId = requestId;
            cmd.PlayerId = m.PlayerId;
            cmd.SequenceNumber = m.SequenceId;
            return cmd;
        }

        public static byte[] EncodeResult(int sequence, CommandResult result)
        {
            var w = new NetWriter(32);
            w.WriteVarInt(sequence);
            w.WriteBool(result.Accepted);
            w.WriteByte((byte)result.Reason);
            w.WriteString(result.Message);
            return w.ToArray();
        }

        public static CommandResult DecodeResult(NetworkMessage m, out int sequence)
        {
            var r = m.PayloadReader();
            sequence = r.ReadVarInt();
            return new CommandResult { Accepted = r.ReadBool(), Reason = r.ReadEnum<RejectReason>(), Message = r.ReadString() };
        }
    }
}
