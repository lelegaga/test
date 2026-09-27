using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Game;

namespace Sanguo.GameModes
{
    /// <summary>
    /// Two teams (A = red, B = blue) of <see cref="GameModeConfig.TeamSize"/> players. Everything
    /// else comes from config: team assignment (random / manual / host), captain or equal teams,
    /// victory by eliminating the other team or by killing its captain, and seat arrangement.
    /// 3v3, 5v5 and 10v10 are the same mode with different team sizes.
    /// </summary>
    public class TeamBattleMode : GameModeBase
    {
        public const string Id = "team";

        public TeamBattleMode(GameModeConfig config, int defaultTeamSize = 0) : base(config)
        {
            if (config.TeamSize <= 0) config.TeamSize = defaultTeamSize > 0 ? defaultTeamSize : config.PlayerCount / 2;
            if (config.TeamVictory == TeamVictoryRule.KillCaptain) VictoryConditions.Add(new CaptainKilledCondition());
            VictoryConditions.Add(new LastFactionStandingCondition());
        }

        public override string ModeId => Id;
        public override string DisplayName => Config.TeamSize + "v" + Config.TeamSize;

        public int TeamSize => Config.TeamSize;

        public override ValidationResult ValidateSetup(GameModeConfig config, int playerCount)
        {
            var baseResult = base.ValidateSetup(config, playerCount);
            if (!baseResult.IsValid) return baseResult;
            if (config.TeamSize < 1 || playerCount != config.TeamSize * 2)
                return ValidationResult.Fail(RejectReason.MalformedCommand, "Team mode needs exactly " + config.TeamSize * 2 + " players.");
            if (config.TeamVictory == TeamVictoryRule.KillCaptain && !config.UseCaptain)
                return ValidationResult.Fail(RejectReason.MalformedCommand, "Kill-captain victory requires captains.");
            return ValidationResult.Ok;
        }

        public override void SetupPlayers(GameSetupContext setup)
        {
            var teamA = new List<PlayerState>();
            var teamB = new List<PlayerState>();
            AssignTeams(setup, teamA, teamB);
            ArrangeSeats(setup, teamA, teamB);
            AssignRoles(setup, teamA, Faction.TeamA);
            AssignRoles(setup, teamB, Faction.TeamB);
        }

        private void AssignTeams(GameSetupContext setup, List<PlayerState> teamA, List<PlayerState> teamB)
        {
            var players = setup.Players;
            bool manual = Config.TeamAssignment != TeamAssignmentMode.Random && LobbyTeamsValid(setup);
            if (manual)
            {
                for (int i = 0; i < players.Count; i++)
                    (setup.Setups[i].Team == Team.A ? teamA : teamB).Add(players[i]);
                return;
            }
            var order = new List<PlayerState>(players);
            Utils.RandomExtensions.Shuffle(setup.Random, order);
            for (int i = 0; i < order.Count; i++) (i < TeamSize ? teamA : teamB).Add(order[i]);
        }

        /// <summary>Lobby teams are used only when every seat picked a side and both sides are full.</summary>
        private bool LobbyTeamsValid(GameSetupContext setup)
        {
            if (setup.Setups.Count != setup.Players.Count) return false;
            int a = 0, b = 0;
            foreach (var s in setup.Setups)
            {
                if (s.Team == Team.A) a++;
                else if (s.Team == Team.B) b++;
                else return false;
            }
            return a == TeamSize && b == TeamSize;
        }

        private void ArrangeSeats(GameSetupContext setup, List<PlayerState> teamA, List<PlayerState> teamB)
        {
            var seats = new List<PlayerState>();
            switch (Config.Seating)
            {
                case SeatArrangement.Grouped:
                    seats.AddRange(teamA);
                    seats.AddRange(teamB);
                    break;
                case SeatArrangement.Random:
                    seats.AddRange(teamA);
                    seats.AddRange(teamB);
                    Utils.RandomExtensions.Shuffle(setup.Random, seats);
                    break;
                default:
                    for (int i = 0; i < TeamSize; i++)
                    {
                        seats.Add(teamA[i]);
                        seats.Add(teamB[i]);
                    }
                    break;
            }
            for (int i = 0; i < seats.Count; i++) setup.SetSeat(seats[i], i);
        }

        private void AssignRoles(GameSetupContext setup, List<PlayerState> team, Faction faction)
        {
            PlayerState captain = null;
            if (Config.UseCaptain)
            {
                // A captain preset in the lobby wins; otherwise pick one at random.
                foreach (var p in team)
                    if (p.PlayerId < setup.Setups.Count && setup.Setups[p.PlayerId].Role == Role.Captain) captain = p;
                if (captain == null) captain = team[setup.Random.Next(team.Count)];
            }
            foreach (var p in team)
            {
                setup.SetTeam(p, faction == Faction.TeamA ? Team.A : Team.B);
                setup.SetFaction(p, faction);
                setup.SetRole(p, ReferenceEquals(p, captain) ? Role.Captain : Role.Member, true);
            }
        }

        /// <summary>Position label within the team in seat order, e.g. "A1" … "A5".</summary>
        public static string PositionLabel(GameState state, PlayerState player)
        {
            int index = 1;
            foreach (var p in state.SeatOrder)
            {
                if (ReferenceEquals(p, player)) break;
                if (p.Team == player.Team) index++;
            }
            return (player.Team == Team.A ? "A" : player.Team == Team.B ? "B" : "?") + index;
        }

        public override int GetMaxHpBonus(GameState state, PlayerState player)
        {
            return Config.UseCaptain && player.Role == Role.Captain ? Config.CaptainExtraHp : 0;
        }

        /// <summary>The team that moves second gets extra opening cards to offset the first-move advantage.</summary>
        public override int GetStartingHandBonus(GameState state, PlayerState player)
        {
            var first = GetFirstPlayer(state);
            return first != null && player.Team != first.Team ? Config.SecondTeamExtraCards : 0;
        }

        public override void CollectModifiers(GameState state, PlayerState owner, ModifierKind kind, List<Modifier> output)
        {
            // First-move compensation: the opening turn of the game draws fewer cards.
            if (kind == ModifierKind.DrawPhaseCount && Config.FirstTurnDrawPenalty > 0 && state.Turn.TurnNumber == 1
                && state.Turn.CurrentPlayerId == owner.PlayerId)
                output.Add(new Modifier(kind, ModifierOp.Add, -Config.FirstTurnDrawPenalty));
        }

        public override bool CanUseLordSkills(GameState state, PlayerState player)
        {
            return Config.UseCaptain && player.Role == Role.Captain;
        }

        public override bool IsRoleVisibleTo(GameState state, PlayerState target, int viewerId) => true;

        public override bool AreAllies(GameState state, PlayerState a, PlayerState b)
        {
            return a != null && b != null && a.Team != Team.None && a.Team == b.Team;
        }

        public override void OnPlayerKilled(GameContext ctx, PlayerState victim, PlayerState killer)
        {
            // Killing an enemy is rewarded so large tables keep their momentum.
            if (killer == null || !killer.Alive || killer.Team == victim.Team || Config.TeamKillReward <= 0) return;
            ctx.Push(new DrawAction(killer.PlayerId, Config.TeamKillReward));
        }

        public override GameResult ResolveRoundLimit(GameState state)
        {
            int aliveA = 0, aliveB = 0, hpA = 0, hpB = 0;
            foreach (var p in state.Players)
            {
                if (!p.Alive) continue;
                if (p.Team == Team.A)
                {
                    aliveA++;
                    hpA += p.Hp;
                }
                else
                {
                    aliveB++;
                    hpB += p.Hp;
                }
            }
            Team winner = aliveA != aliveB ? (aliveA > aliveB ? Team.A : Team.B) : hpA != hpB ? (hpA > hpB ? Team.A : Team.B) : Team.None;
            if (winner == Team.None) return GameResult.Draw("round_limit");
            var faction = winner == Team.A ? Faction.TeamA : Faction.TeamB;
            var r = new GameResult { WinningFaction = faction, WinningTeam = winner, Reason = "round_limit" };
            VictoryUtil.AddFactionMembers(state, faction, r.WinnerIds);
            return r;
        }
    }

    /// <summary>3v3: captain + two members by default.</summary>
    public sealed class Team3v3Mode : TeamBattleMode
    {
        public new const string Id = "team3v3";

        public Team3v3Mode(GameModeConfig config) : base(config, 3)
        {
        }

        public override string ModeId => Id;
        public override string DisplayName => "3V3";
    }

    public sealed class Team5v5Mode : TeamBattleMode
    {
        public new const string Id = "team5v5";

        public Team5v5Mode(GameModeConfig config) : base(config, 5)
        {
        }

        public override string ModeId => Id;
        public override string DisplayName => "5V5";
    }

    public sealed class Team10v10Mode : TeamBattleMode
    {
        public new const string Id = "team10v10";

        public Team10v10Mode(GameModeConfig config) : base(config, 10)
        {
        }

        public override string ModeId => Id;
        public override string DisplayName => "10V10";
    }

    /// <summary>A team loses as soon as its captain dies.</summary>
    public sealed class CaptainKilledCondition : IVictoryCondition
    {
        public GameResult Evaluate(GameState state)
        {
            foreach (var p in state.Players)
            {
                if (p.Role != Role.Captain || p.Alive) continue;
                var winner = p.Team == Team.A ? Team.B : Team.A;
                var faction = winner == Team.A ? Faction.TeamA : Faction.TeamB;
                var r = new GameResult { WinningFaction = faction, WinningTeam = winner, Reason = "captain_killed" };
                VictoryUtil.AddFactionMembers(state, faction, r.WinnerIds);
                return r;
            }
            return null;
        }
    }
}
