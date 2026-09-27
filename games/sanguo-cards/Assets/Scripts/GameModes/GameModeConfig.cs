using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Data;

namespace Sanguo.GameModes
{
    public enum CharacterSelectionMode : byte
    {
        /// <summary>Everyone plays the placeholder character with <see cref="GameModeConfig.DefaultMaxHp"/>.</summary>
        None = 0,
        Random = 1,
        /// <summary>Each player picks from <see cref="GameModeConfig.CharacterChoices"/> random options.</summary>
        Choose = 2
    }

    public enum TeamAssignmentMode : byte
    {
        Random = 0,
        /// <summary>Players picked their team in the lobby.</summary>
        Manual = 1,
        /// <summary>The host assigned teams in the lobby.</summary>
        HostAssigned = 2
    }

    public enum TeamVictoryRule : byte
    {
        /// <summary>Win by eliminating every enemy.</summary>
        EliminateAll = 0,
        /// <summary>Win by killing the enemy captain (core character).</summary>
        KillCaptain = 1
    }

    public enum SeatArrangement : byte
    {
        /// <summary>A1 B1 A2 B2 ... (teams interleaved).</summary>
        Alternating = 0,
        /// <summary>All of team A then all of team B.</summary>
        Grouped = 1,
        Random = 2
    }

    public sealed class RoleCount
    {
        public Role Role;
        public int Count;

        public RoleCount(Role role, int count)
        {
            Role = role;
            Count = count;
        }
    }

    /// <summary>
    /// Every tunable rule of a game: player count, identity distribution, team options, timers.
    /// Chosen in the room by the host; nothing about player counts is hard coded in the engine.
    /// </summary>
    public sealed class GameModeConfig
    {
        public string ModeId = "ffa";
        public int PlayerCount = 4;

        /// <summary>Random seating (free-for-all / identity). Tests and fixed lobbies turn it off.</summary>
        public bool ShuffleSeats = true;

        public int StartingHandSize = 4;
        public int DrawPerTurn = 2;
        public int DefaultMaxHp = 4;

        public CharacterSelectionMode CharacterSelection = CharacterSelectionMode.None;
        public int CharacterChoices = 3;
        /// <summary>Character used when selection is None (and as a fallback).</summary>
        public string DefaultCharacterId = "recruit";
        /// <summary>Restricts selectable characters; empty means every character except the default.</summary>
        public List<string> CharacterPool = new List<string>();

        public string DeckId = "standard";
        /// <summary>0 = pick automatically from the player count.</summary>
        public int DeckCopies = 0;

        public int PlayTimeoutMs = 30000;
        public int ResponseTimeoutMs = 15000;
        public int DiscardTimeoutMs = 20000;
        public int ChooseCharacterTimeoutMs = 30000;

        /// <summary>Safety net against endless games; the mode decides the result at the limit.</summary>
        public int MaxRounds = 100;
        public int MaxActionsPerPlayPhase = 60;

        /// <summary>Card that can be used on a dying player to save them.</summary>
        public string RescueCardId = "heal";

        /// <summary>
        /// Skip response requests the player cannot possibly answer (faster big games). Off by
        /// default because an instant skip tells everyone the player holds no such card.
        /// </summary>
        public bool AutoSkipImpossibleResponses;

        public bool AllowSpectators = true;
        /// <summary>Disconnected players are handed to the AI after this long.</summary>
        public int DisconnectAITakeoverMs = 20000;

        // ---- identity mode
        public List<RoleCount> Roles = new List<RoleCount>();
        /// <summary>Lord gets +1 max HP when there are more than four players.</summary>
        public bool LordExtraHp = true;

        // ---- team modes
        public int TeamSize;
        public TeamAssignmentMode TeamAssignment = TeamAssignmentMode.Random;
        public bool UseCaptain;
        public TeamVictoryRule TeamVictory = TeamVictoryRule.EliminateAll;
        public SeatArrangement Seating = SeatArrangement.Alternating;
        /// <summary>Extra max HP for captains.</summary>
        public int CaptainExtraHp = 1;
        /// <summary>Cards drawn by a player who kills an enemy in team modes.</summary>
        public int TeamKillReward = 1;
        /// <summary>Extra opening cards for the team that moves second (first-move compensation).</summary>
        public int SecondTeamExtraCards;
        /// <summary>The very first turn of the game draws this many fewer cards (first-move compensation).</summary>
        public int FirstTurnDrawPenalty = 1;

        public int GetTimeoutMs(RequestKind kind)
        {
            switch (kind)
            {
                case RequestKind.PlayAction: return PlayTimeoutMs;
                case RequestKind.Discard: return DiscardTimeoutMs;
                case RequestKind.ChooseCharacter: return ChooseCharacterTimeoutMs;
                default: return ResponseTimeoutMs;
            }
        }

        public int GetRoleCount(Role role)
        {
            int n = 0;
            foreach (var r in Roles)
                if (r.Role == role) n += r.Count;
            return n;
        }

        public int ResolveDeckCopies()
        {
            if (DeckCopies > 0) return DeckCopies;
            if (PlayerCount <= 8) return 1;
            if (PlayerCount <= 14) return 2;
            return 3;
        }

        public GameModeConfig Clone()
        {
            var c = (GameModeConfig)MemberwiseClone();
            c.CharacterPool = new List<string>(CharacterPool);
            c.Roles = new List<RoleCount>();
            foreach (var r in Roles) c.Roles.Add(new RoleCount(r.Role, r.Count));
            return c;
        }

        public JsonValue ToJson()
        {
            var j = JsonValue.NewObject();
            j.Set("modeId", ModeId).Set("playerCount", PlayerCount).Set("startingHandSize", StartingHandSize)
                .Set("drawPerTurn", DrawPerTurn).Set("shuffleSeats", ShuffleSeats).Set("defaultMaxHp", DefaultMaxHp)
                .Set("characterSelection", CharacterSelection.ToString()).Set("characterChoices", CharacterChoices)
                .Set("defaultCharacterId", DefaultCharacterId).Set("deckId", DeckId).Set("deckCopies", DeckCopies)
                .Set("playTimeoutMs", PlayTimeoutMs).Set("responseTimeoutMs", ResponseTimeoutMs)
                .Set("discardTimeoutMs", DiscardTimeoutMs).Set("chooseCharacterTimeoutMs", ChooseCharacterTimeoutMs)
                .Set("maxRounds", MaxRounds).Set("rescueCardId", RescueCardId).Set("autoSkipImpossibleResponses", AutoSkipImpossibleResponses)
                .Set("allowSpectators", AllowSpectators).Set("disconnectAITakeoverMs", DisconnectAITakeoverMs)
                .Set("lordExtraHp", LordExtraHp).Set("teamSize", TeamSize).Set("teamAssignment", TeamAssignment.ToString())
                .Set("useCaptain", UseCaptain).Set("teamVictory", TeamVictory.ToString()).Set("seating", Seating.ToString())
                .Set("captainExtraHp", CaptainExtraHp).Set("teamKillReward", TeamKillReward).Set("secondTeamExtraCards", SecondTeamExtraCards).Set("firstTurnDrawPenalty", FirstTurnDrawPenalty);
            var roles = JsonValue.NewArray();
            foreach (var r in Roles) roles.Add(JsonValue.NewObject().Set("role", r.Role.ToString()).Set("count", r.Count));
            j.Set("roles", roles);
            var pool = JsonValue.NewArray();
            foreach (var id in CharacterPool) pool.Add(JsonValue.From(id));
            j.Set("characterPool", pool);
            return j;
        }

        public static GameModeConfig FromJson(JsonValue j)
        {
            var c = new GameModeConfig();
            c.ModeId = j.GetString("modeId", c.ModeId);
            c.PlayerCount = j.GetInt("playerCount", c.PlayerCount);
            c.StartingHandSize = j.GetInt("startingHandSize", c.StartingHandSize);
            c.DrawPerTurn = j.GetInt("drawPerTurn", c.DrawPerTurn);
            c.ShuffleSeats = j.GetBool("shuffleSeats", c.ShuffleSeats);
            c.DefaultMaxHp = j.GetInt("defaultMaxHp", c.DefaultMaxHp);
            c.CharacterSelection = ParseEnum(j.GetString("characterSelection"), c.CharacterSelection);
            c.CharacterChoices = j.GetInt("characterChoices", c.CharacterChoices);
            c.DefaultCharacterId = j.GetString("defaultCharacterId", c.DefaultCharacterId);
            c.DeckId = j.GetString("deckId", c.DeckId);
            c.DeckCopies = j.GetInt("deckCopies", c.DeckCopies);
            c.PlayTimeoutMs = j.GetInt("playTimeoutMs", c.PlayTimeoutMs);
            c.ResponseTimeoutMs = j.GetInt("responseTimeoutMs", c.ResponseTimeoutMs);
            c.DiscardTimeoutMs = j.GetInt("discardTimeoutMs", c.DiscardTimeoutMs);
            c.ChooseCharacterTimeoutMs = j.GetInt("chooseCharacterTimeoutMs", c.ChooseCharacterTimeoutMs);
            c.MaxRounds = j.GetInt("maxRounds", c.MaxRounds);
            c.RescueCardId = j.GetString("rescueCardId", c.RescueCardId);
            c.AutoSkipImpossibleResponses = j.GetBool("autoSkipImpossibleResponses", c.AutoSkipImpossibleResponses);
            c.AllowSpectators = j.GetBool("allowSpectators", c.AllowSpectators);
            c.DisconnectAITakeoverMs = j.GetInt("disconnectAITakeoverMs", c.DisconnectAITakeoverMs);
            c.LordExtraHp = j.GetBool("lordExtraHp", c.LordExtraHp);
            c.TeamSize = j.GetInt("teamSize", c.TeamSize);
            c.TeamAssignment = ParseEnum(j.GetString("teamAssignment"), c.TeamAssignment);
            c.UseCaptain = j.GetBool("useCaptain", c.UseCaptain);
            c.TeamVictory = ParseEnum(j.GetString("teamVictory"), c.TeamVictory);
            c.Seating = ParseEnum(j.GetString("seating"), c.Seating);
            c.CaptainExtraHp = j.GetInt("captainExtraHp", c.CaptainExtraHp);
            c.TeamKillReward = j.GetInt("teamKillReward", c.TeamKillReward);
            c.SecondTeamExtraCards = j.GetInt("secondTeamExtraCards", c.SecondTeamExtraCards);
            c.FirstTurnDrawPenalty = j.GetInt("firstTurnDrawPenalty", c.FirstTurnDrawPenalty);
            foreach (var r in j["roles"].Items)
                c.Roles.Add(new RoleCount(ParseEnum(r.GetString("role"), Role.None), r.GetInt("count")));
            c.CharacterPool.AddRange(j.GetStringList("characterPool"));
            return c;
        }

        private static T ParseEnum<T>(string s, T fallback) where T : struct
        {
            return s != null && System.Enum.TryParse(s, true, out T v) ? v : fallback;
        }
    }
}
