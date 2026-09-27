using System;
using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Data;
using Sanguo.GameModes;

namespace Sanguo.Network
{
    /// <summary>One seat in the room (lobby). Seat ids become game player ids.</summary>
    public sealed class RoomSlot
    {
        public int SlotId;
        public bool Occupied;
        public string Nickname = string.Empty;
        public int AvatarId;
        public bool Ready;
        public Team Team;
        public bool Connected;
        public int PingMs;

        public RoomSlot Clone() => (RoomSlot)MemberwiseClone();
    }

    /// <summary>Host-controlled room options (mode, seats, AI, teams, timers, rules, spectators).</summary>
    public sealed class RoomSettings
    {
        public string RoomName = "三国战场";
        public GameModeConfig Config = new GameModeConfig
        {
            ModeId = IdentityMode.Id,
            PlayerCount = 5,
            CharacterSelection = CharacterSelectionMode.Choose
        };
        /// <summary>Empty seats are filled with AI players when the game starts.</summary>
        public bool FillWithAI = true;

        public RoomSettings Clone()
        {
            return new RoomSettings { RoomName = RoomName, Config = Config.Clone(), FillWithAI = FillWithAI };
        }
    }

    /// <summary>Everything the lobby UI shows; broadcast to every member on change.</summary>
    public sealed class RoomState
    {
        public string RoomId = string.Empty;
        public string InviteCode = string.Empty;
        public int HostSlotId;
        public bool InGame;
        public RoomSettings Settings = new RoomSettings();
        public List<RoomSlot> Slots = new List<RoomSlot>();
        public int SpectatorCount;

        public int HumanCount
        {
            get
            {
                int n = 0;
                foreach (var s in Slots)
                    if (s.Occupied) n++;
                return n;
            }
        }

        public RoomSlot GetSlot(int slotId) => slotId >= 0 && slotId < Slots.Count ? Slots[slotId] : null;
    }

    /// <summary>Room announcement for the LAN browser.</summary>
    public sealed class RoomInfo
    {
        public string RoomId = string.Empty;
        public string RoomName = string.Empty;
        public string HostName = string.Empty;
        public string ModeId = string.Empty;
        public int Humans;
        public int MaxPlayers;
        public bool InGame;
        public bool AllowSpectators;
        public string Address = string.Empty;
        public int Port;
        public string InviteCode = string.Empty;
        public int ProtocolVersion = ProtocolInfo.Version;

        /// <summary>Local receive time (not serialized); used to expire stale rooms.</summary>
        public long LastSeenMs;
    }

    public static class RoomCodec
    {
        public static void WriteSettings(NetWriter w, RoomSettings s)
        {
            w.WriteString(s.RoomName);
            w.WriteBool(s.FillWithAI);
            w.WriteString(s.Config.ToJson().ToJson());
        }

        public static RoomSettings ReadSettings(NetReader r)
        {
            var s = new RoomSettings { RoomName = r.ReadString() ?? string.Empty, FillWithAI = r.ReadBool() };
            string json = r.ReadString();
            try
            {
                s.Config = GameModeConfig.FromJson(JsonValue.Parse(json ?? "{}"));
            }
            catch (FormatException ex)
            {
                throw new ProtocolException("Bad room settings: " + ex.Message);
            }
            return s;
        }

        public static void WriteState(NetWriter w, RoomState s)
        {
            w.WriteString(s.RoomId);
            w.WriteString(s.InviteCode);
            w.WriteVarInt(s.HostSlotId);
            w.WriteBool(s.InGame);
            w.WriteVarInt(s.SpectatorCount);
            WriteSettings(w, s.Settings);
            w.WriteVarUInt((uint)s.Slots.Count);
            foreach (var slot in s.Slots)
            {
                w.WriteVarInt(slot.SlotId);
                w.WriteBool(slot.Occupied);
                w.WriteString(slot.Nickname);
                w.WriteVarInt(slot.AvatarId);
                w.WriteBool(slot.Ready);
                w.WriteByte((byte)slot.Team);
                w.WriteBool(slot.Connected);
                w.WriteVarInt(slot.PingMs);
            }
        }

        public static RoomState ReadState(NetReader r)
        {
            var s = new RoomState
            {
                RoomId = r.ReadString() ?? string.Empty,
                InviteCode = r.ReadString() ?? string.Empty,
                HostSlotId = r.ReadVarInt(),
                InGame = r.ReadBool(),
                SpectatorCount = r.ReadVarInt(),
                Settings = ReadSettings(r)
            };
            uint n = r.ReadVarUInt();
            if (n > 64) throw new ProtocolException("Too many slots.");
            for (int i = 0; i < n; i++)
            {
                s.Slots.Add(new RoomSlot
                {
                    SlotId = r.ReadVarInt(),
                    Occupied = r.ReadBool(),
                    Nickname = r.ReadString() ?? string.Empty,
                    AvatarId = r.ReadVarInt(),
                    Ready = r.ReadBool(),
                    Team = r.ReadEnum<Team>(),
                    Connected = r.ReadBool(),
                    PingMs = r.ReadVarInt()
                });
            }
            return s;
        }

        public static byte[] EncodeState(RoomState s)
        {
            var w = new NetWriter(512);
            WriteState(w, s);
            return w.ToArray();
        }

        public static void WriteInfo(NetWriter w, RoomInfo i)
        {
            w.WriteVarInt(i.ProtocolVersion);
            w.WriteString(i.RoomId);
            w.WriteString(i.RoomName);
            w.WriteString(i.HostName);
            w.WriteString(i.ModeId);
            w.WriteVarInt(i.Humans);
            w.WriteVarInt(i.MaxPlayers);
            w.WriteBool(i.InGame);
            w.WriteBool(i.AllowSpectators);
            w.WriteString(i.Address);
            w.WriteVarInt(i.Port);
            w.WriteString(i.InviteCode);
        }

        public static RoomInfo ReadInfo(NetReader r)
        {
            return new RoomInfo
            {
                ProtocolVersion = r.ReadVarInt(),
                RoomId = r.ReadString() ?? string.Empty,
                RoomName = r.ReadString() ?? string.Empty,
                HostName = r.ReadString() ?? string.Empty,
                ModeId = r.ReadString() ?? string.Empty,
                Humans = r.ReadVarInt(),
                MaxPlayers = r.ReadVarInt(),
                InGame = r.ReadBool(),
                AllowSpectators = r.ReadBool(),
                Address = r.ReadString() ?? string.Empty,
                Port = r.ReadVarInt(),
                InviteCode = r.ReadString() ?? string.Empty
            };
        }
    }
}
