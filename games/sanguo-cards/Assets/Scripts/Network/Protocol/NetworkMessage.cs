using System;
using System.Collections.Generic;

namespace Sanguo.Network
{
    public static class ProtocolInfo
    {
        /// <summary>Bumped on every incompatible wire change; peers with another version are rejected.</summary>
        public const int Version = 1;

        /// <summary>Default TCP port of a LAN host.</summary>
        public const int DefaultGamePort = 47700;

        /// <summary>UDP port used for room announcements.</summary>
        public const int DiscoveryPort = 47777;

        /// <summary>Largest accepted frame. Bigger frames are treated as an attack and close the connection.</summary>
        public const int MaxFrameSize = 1 << 20;
    }

    public enum MessageType : ushort
    {
        // ---- connection
        Hello = 1,
        Welcome = 2,
        Ping = 3,
        Pong = 4,
        Disconnect = 5,
        Error = 6,

        // ---- room / lobby
        CreateRoom = 10,
        JoinRoom = 11,
        JoinAccepted = 12,
        JoinRejected = 13,
        LeaveRoom = 14,
        RoomState = 15,
        PlayerReady = 16,
        KickPlayer = 17,
        TransferHost = 18,
        UpdateRoomSettings = 19,
        StartGame = 20,
        ChangeTeam = 21,
        Chat = 22,

        // ---- game commands (client → server intents)
        PlayCard = 30,
        UseSkill = 31,
        SelectTarget = 32,
        RespondCard = 33,
        EndTurn = 34,
        SetAutoPlay = 35,

        // ---- game state (server → client)
        CommandResult = 40,
        GameStateSync = 41,
        GameEvents = 42,
        RequestResync = 43,
        Reconnect = 44,
        ReconnectAccepted = 45,
        ReconnectRejected = 46,
        GameFinished = 47
    }

    /// <summary>
    /// Every packet: MessageType, PlayerID, RoomID, SequenceID, Timestamp, Payload. The server never
    /// trusts PlayerID from a client — it uses the identity bound to the connection.
    /// </summary>
    public sealed class NetworkMessage
    {
        public MessageType Type;
        public int PlayerId = -1;
        public string RoomId = string.Empty;
        /// <summary>Per-sender message counter (for commands it equals the command sequence number).</summary>
        public int SequenceId;
        /// <summary>Sender clock in milliseconds.</summary>
        public long Timestamp;
        public byte[] Payload;

        public NetworkMessage()
        {
        }

        public NetworkMessage(MessageType type, byte[] payload = null)
        {
            Type = type;
            Payload = payload;
        }

        public NetReader PayloadReader() => new NetReader(Payload ?? Array.Empty<byte>());

        public void Write(NetWriter w)
        {
            w.WriteUInt16((ushort)Type);
            w.WriteVarInt(PlayerId);
            w.WriteString(RoomId);
            w.WriteVarInt(SequenceId);
            w.WriteInt64(Timestamp);
            w.WriteBytes(Payload ?? Array.Empty<byte>());
        }

        public static NetworkMessage Read(NetReader r)
        {
            var m = new NetworkMessage();
            ushort type = r.ReadUInt16();
            if (!Enum.IsDefined(typeof(MessageType), type)) throw new ProtocolException("Unknown message type " + type + ".");
            m.Type = (MessageType)type;
            m.PlayerId = r.ReadVarInt();
            m.RoomId = r.ReadString() ?? string.Empty;
            m.SequenceId = r.ReadVarInt();
            m.Timestamp = r.ReadInt64();
            m.Payload = r.ReadBytes() ?? Array.Empty<byte>();
            if (!r.AtEnd) throw new ProtocolException("Trailing bytes after message.");
            return m;
        }

        public byte[] ToBytes()
        {
            var w = new NetWriter(32 + (Payload?.Length ?? 0));
            Write(w);
            return w.ToArray();
        }

        public static NetworkMessage FromBytes(byte[] data) => Read(new NetReader(data));

        public override string ToString() => Type + "(p" + PlayerId + ",seq" + SequenceId + "," + (Payload?.Length ?? 0) + "B)";
    }

    /// <summary>
    /// Length-prefixed framing over a byte stream: [u32 little-endian length][message bytes].
    /// <see cref="FrameDecoder"/> accepts arbitrary chunks (partial or several frames at once).
    /// </summary>
    public static class FrameCodec
    {
        public static byte[] Encode(NetworkMessage message)
        {
            var body = message.ToBytes();
            if (body.Length > ProtocolInfo.MaxFrameSize) throw new ProtocolException("Frame too large.");
            var frame = new byte[body.Length + 4];
            frame[0] = (byte)body.Length;
            frame[1] = (byte)(body.Length >> 8);
            frame[2] = (byte)(body.Length >> 16);
            frame[3] = (byte)(body.Length >> 24);
            Buffer.BlockCopy(body, 0, frame, 4, body.Length);
            return frame;
        }
    }

    public sealed class FrameDecoder
    {
        private byte[] _buffer = new byte[4096];
        private int _count;

        /// <summary>Appends received bytes and extracts complete messages into <paramref name="output"/>.</summary>
        public void Feed(byte[] data, int offset, int count, List<NetworkMessage> output)
        {
            if (count <= 0) return;
            if (_count + count > _buffer.Length)
            {
                int size = _buffer.Length;
                while (size < _count + count) size *= 2;
                if (size > ProtocolInfo.MaxFrameSize * 2 + 8) throw new ProtocolException("Receive buffer overflow.");
                Array.Resize(ref _buffer, size);
            }
            Buffer.BlockCopy(data, offset, _buffer, _count, count);
            _count += count;

            int pos = 0;
            while (_count - pos >= 4)
            {
                int length = _buffer[pos] | (_buffer[pos + 1] << 8) | (_buffer[pos + 2] << 16) | (_buffer[pos + 3] << 24);
                if (length < 0 || length > ProtocolInfo.MaxFrameSize) throw new ProtocolException("Invalid frame length " + length + ".");
                if (_count - pos - 4 < length) break;
                output.Add(NetworkMessage.Read(new NetReader(_buffer, pos + 4, length)));
                pos += 4 + length;
            }
            if (pos > 0)
            {
                Buffer.BlockCopy(_buffer, pos, _buffer, 0, _count - pos);
                _count -= pos;
            }
        }
    }
}
