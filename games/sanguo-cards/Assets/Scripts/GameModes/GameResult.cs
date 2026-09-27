using System.Collections.Generic;
using System.Text;
using Sanguo.Core;

namespace Sanguo.GameModes
{
    /// <summary>Final outcome produced by the server's victory conditions.</summary>
    public sealed class GameResult
    {
        public bool IsDraw;
        public Faction WinningFaction;
        public Team WinningTeam;
        public List<int> WinnerIds = new List<int>();
        /// <summary>Machine readable reason key, e.g. "last_standing", "lord_killed", "round_limit".</summary>
        public string Reason = string.Empty;

        public static GameResult Draw(string reason)
        {
            return new GameResult { IsDraw = true, Reason = reason };
        }

        public bool IsWinner(int playerId) => WinnerIds.Contains(playerId);

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append(IsDraw ? "draw" : "win").Append(" faction=").Append(WinningFaction).Append(" team=").Append(WinningTeam)
                .Append(" reason=").Append(Reason).Append(" winners=");
            for (int i = 0; i < WinnerIds.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(WinnerIds[i]);
            }
            return sb.ToString();
        }

        public GameResult Clone()
        {
            return new GameResult
            {
                IsDraw = IsDraw,
                WinningFaction = WinningFaction,
                WinningTeam = WinningTeam,
                WinnerIds = new List<int>(WinnerIds),
                Reason = Reason
            };
        }
    }
}
