using System.Collections.Generic;
using Sanguo.GameModes;

namespace Sanguo.UI
{
    /// <summary>Mode / player-count / rule choices offered by the setup screens, mapped to GameModeConfig.</summary>
    public static class ModeOptions
    {
        public static readonly string[] ModeIds = { IdentityMode.Id, FreeForAllMode.Id, Team3v3Mode.Id, Team5v5Mode.Id, Team10v10Mode.Id };
        public static readonly string[] ModeNames = { "身份模式", "混战", "3V3", "5V5", "10V10" };
        public static readonly string[] SelectionNames = { "自选武将", "随机武将", "无武将（4 体力）" };
        public static readonly CharacterSelectionMode[] Selections = { CharacterSelectionMode.Choose, CharacterSelectionMode.Random, CharacterSelectionMode.None };
        public static readonly int[] TimeoutSeconds = { 15, 30, 45, 60 };
        public static readonly string[] TimeoutNames = { "15 秒", "30 秒", "45 秒", "60 秒" };
        public static readonly string[] TeamAssignmentNames = { "随机分队", "玩家自选", "房主分配" };
        public static readonly string[] TeamVictoryNames = { "消灭全部敌人", "击败敌方队长" };

        public static int ModeIndex(string modeId)
        {
            int i = System.Array.IndexOf(ModeIds, modeId);
            return i < 0 ? 0 : i;
        }

        public static string ModeName(string modeId) => ModeNames[ModeIndex(modeId)];

        public static bool IsTeamMode(string modeId) => modeId == Team3v3Mode.Id || modeId == Team5v5Mode.Id || modeId == Team10v10Mode.Id || modeId == TeamBattleMode.Id;

        /// <summary>Player counts allowed for a mode.</summary>
        public static List<int> PlayerCounts(string modeId)
        {
            var list = new List<int>();
            switch (modeId)
            {
                case Team3v3Mode.Id: list.Add(6); break;
                case Team5v5Mode.Id: list.Add(10); break;
                case Team10v10Mode.Id: list.Add(20); break;
                case FreeForAllMode.Id:
                    for (int i = 2; i <= 8; i++) list.Add(i);
                    break;
                default:
                    for (int i = 4; i <= 12; i++) list.Add(i);
                    break;
            }
            return list;
        }

        public static int DefaultPlayers(string modeId)
        {
            switch (modeId)
            {
                case FreeForAllMode.Id: return 4;
                case IdentityMode.Id: return 5;
                default: return PlayerCounts(modeId)[0];
            }
        }

        public static GameModeConfig Build(string modeId, int players, CharacterSelectionMode selection, int timeoutSeconds)
        {
            var c = new GameModeConfig
            {
                ModeId = modeId,
                PlayerCount = players,
                CharacterSelection = selection,
                PlayTimeoutMs = timeoutSeconds * 1000,
                ResponseTimeoutMs = System.Math.Max(8, timeoutSeconds / 2) * 1000,
                DiscardTimeoutMs = System.Math.Max(10, timeoutSeconds * 2 / 3) * 1000
            };
            if (IsTeamMode(modeId)) c.TeamSize = players / 2;
            return c;
        }

        public static string Describe(GameModeConfig c)
        {
            string s = ModeName(c.ModeId) + " · " + c.PlayerCount + " 人 · " + SelectionNames[System.Math.Max(0, System.Array.IndexOf(Selections, c.CharacterSelection))]
                       + " · 出牌 " + c.PlayTimeoutMs / 1000 + " 秒";
            if (IsTeamMode(c.ModeId))
                s += " · " + TeamAssignmentNames[(int)c.TeamAssignment] + (c.UseCaptain ? " · 队长制" : "") + " · " + TeamVictoryNames[(int)c.TeamVictory];
            return s;
        }
    }
}
