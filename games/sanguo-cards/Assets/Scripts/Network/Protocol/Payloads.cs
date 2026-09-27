using Sanguo.Core;
using Sanguo.GameModes;

namespace Sanguo.Network
{
    public sealed class HelloPayload
    {
        public int ProtocolVersion = ProtocolInfo.Version;
        public string Nickname = string.Empty;
        public int AvatarId;
        public string AppVersion = string.Empty;
    }

    public sealed class JoinPayload
    {
        public string Nickname = string.Empty;
        public int AvatarId;
        public bool Spectator;
    }

    /// <summary>Encoders/decoders for the small lobby and control payloads.</summary>
    public static class Payloads
    {
        public static byte[] Hello(HelloPayload h)
        {
            var w = new NetWriter(64);
            w.WriteVarInt(h.ProtocolVersion);
            w.WriteString(h.Nickname);
            w.WriteVarInt(h.AvatarId);
            w.WriteString(h.AppVersion);
            return w.ToArray();
        }

        public static HelloPayload ReadHello(NetworkMessage m)
        {
            var r = m.PayloadReader();
            return new HelloPayload { ProtocolVersion = r.ReadVarInt(), Nickname = r.ReadString() ?? string.Empty, AvatarId = r.ReadVarInt(), AppVersion = r.ReadString() ?? string.Empty };
        }

        public static byte[] Welcome(int connectionId, string serverName, string roomId)
        {
            var w = new NetWriter(64);
            w.WriteVarInt(connectionId);
            w.WriteString(serverName);
            w.WriteString(roomId);
            return w.ToArray();
        }

        public static byte[] Join(JoinPayload j)
        {
            var w = new NetWriter(64);
            w.WriteString(j.Nickname);
            w.WriteVarInt(j.AvatarId);
            w.WriteBool(j.Spectator);
            return w.ToArray();
        }

        public static JoinPayload ReadJoin(NetworkMessage m)
        {
            var r = m.PayloadReader();
            return new JoinPayload { Nickname = r.ReadString() ?? string.Empty, AvatarId = r.ReadVarInt(), Spectator = r.ReadBool() };
        }

        public static byte[] JoinAccepted(int slotId, string token, RoomState room)
        {
            var w = new NetWriter(512);
            w.WriteVarInt(slotId);
            w.WriteString(token);
            RoomCodec.WriteState(w, room);
            return w.ToArray();
        }

        public static RoomState ReadJoinAccepted(NetworkMessage m, out int slotId, out string token)
        {
            var r = m.PayloadReader();
            slotId = r.ReadVarInt();
            token = r.ReadString();
            return RoomCodec.ReadState(r);
        }

        public static byte[] Reason(string code, string message)
        {
            var w = new NetWriter(64);
            w.WriteString(code);
            w.WriteString(message);
            return w.ToArray();
        }

        public static string ReadReason(NetworkMessage m, out string message)
        {
            var r = m.PayloadReader();
            string code = r.ReadString();
            message = r.ReadString();
            return code;
        }

        public static byte[] Int(int value)
        {
            var w = new NetWriter(8);
            w.WriteVarInt(value);
            return w.ToArray();
        }

        public static int ReadInt(NetworkMessage m) => m.PayloadReader().ReadVarInt();

        public static byte[] Bool(bool value) => new[] { value ? (byte)1 : (byte)0 };

        public static bool ReadBool(NetworkMessage m) => m.PayloadReader().ReadBool();

        public static byte[] Long(long value)
        {
            var w = new NetWriter(8);
            w.WriteInt64(value);
            return w.ToArray();
        }

        public static long ReadLong(NetworkMessage m) => m.PayloadReader().ReadInt64();

        public static byte[] Settings(RoomSettings s)
        {
            var w = new NetWriter(512);
            RoomCodec.WriteSettings(w, s);
            return w.ToArray();
        }

        public static RoomSettings ReadSettings(NetworkMessage m) => RoomCodec.ReadSettings(m.PayloadReader());

        public static byte[] ChangeTeam(int slotId, Team team)
        {
            var w = new NetWriter(8);
            w.WriteVarInt(slotId);
            w.WriteByte((byte)team);
            return w.ToArray();
        }

        public static int ReadChangeTeam(NetworkMessage m, out Team team)
        {
            var r = m.PayloadReader();
            int slot = r.ReadVarInt();
            team = r.ReadEnum<Team>();
            return slot;
        }

        public static byte[] Chat(int slotId, string text)
        {
            var w = new NetWriter(128);
            w.WriteVarInt(slotId);
            w.WriteString(text);
            return w.ToArray();
        }

        public static string ReadChat(NetworkMessage m, out int slotId)
        {
            var r = m.PayloadReader();
            slotId = r.ReadVarInt();
            return r.ReadString() ?? string.Empty;
        }

        public static byte[] Reconnect(int slotId, string token)
        {
            var w = new NetWriter(64);
            w.WriteVarInt(slotId);
            w.WriteString(token);
            return w.ToArray();
        }

        public static int ReadReconnect(NetworkMessage m, out string token)
        {
            var r = m.PayloadReader();
            int slot = r.ReadVarInt();
            token = r.ReadString();
            return slot;
        }

        public static byte[] RoomStatePayload(RoomState room) => RoomCodec.EncodeState(room);

        public static RoomState ReadRoomState(NetworkMessage m) => RoomCodec.ReadState(m.PayloadReader());

        public static byte[] Result(GameResult result)
        {
            var w = new NetWriter(64);
            CommonCodec.WriteResult(w, result);
            return w.ToArray();
        }

        public static GameResult ReadResult(NetworkMessage m) => CommonCodec.ReadResult(m.PayloadReader());
    }
}
