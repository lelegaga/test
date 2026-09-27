using System.Collections.Generic;
using Sanguo.Core;

namespace Sanguo.GameModes
{
    /// <summary>A rule that can end the game. Evaluated by the server only, never by UI code.</summary>
    public interface IVictoryCondition
    {
        /// <summary>Returns the result if this condition decides the game, otherwise null.</summary>
        GameResult Evaluate(GameState state);
    }

    /// <summary>
    /// The game ends when every living player belongs to one faction (for Solo factions: one player
    /// left). All members of the winning faction win, dead or alive.
    /// </summary>
    public sealed class LastFactionStandingCondition : IVictoryCondition
    {
        public GameResult Evaluate(GameState state)
        {
            PlayerState first = null;
            int alive = 0;
            foreach (var p in state.Players)
            {
                if (!p.Alive) continue;
                alive++;
                if (first == null)
                {
                    first = p;
                    continue;
                }
                if (first.Faction == Faction.Solo || p.Faction != first.Faction) return null;
            }
            if (alive == 0) return GameResult.Draw("all_dead");

            var result = new GameResult { WinningFaction = first.Faction, Reason = "last_standing" };
            if (first.Faction == Faction.Solo)
            {
                result.WinnerIds.Add(first.PlayerId);
            }
            else
            {
                foreach (var p in state.Players)
                    if (p.Faction == first.Faction) result.WinnerIds.Add(p.PlayerId);
            }
            result.WinningTeam = FactionTeam(first.Faction);
            return result;
        }

        internal static Team FactionTeam(Faction f)
        {
            return f == Faction.TeamA ? Team.A : f == Faction.TeamB ? Team.B : Team.None;
        }
    }

    /// <summary>Helpers shared by victory conditions.</summary>
    public static class VictoryUtil
    {
        public static void AddFactionMembers(GameState state, Faction faction, List<int> output)
        {
            foreach (var p in state.Players)
                if (p.Faction == faction) output.Add(p.PlayerId);
        }

        public static int CountAlive(GameState state, Faction faction)
        {
            int n = 0;
            foreach (var p in state.Players)
                if (p.Alive && p.Faction == faction) n++;
            return n;
        }
    }
}
