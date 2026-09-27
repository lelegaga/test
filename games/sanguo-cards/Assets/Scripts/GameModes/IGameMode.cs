using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Utils;

namespace Sanguo.GameModes
{
    /// <summary>Lobby information about one seat when a game is created.</summary>
    public sealed class PlayerSetup
    {
        public string Nickname;
        public int AvatarId;
        public bool IsBot;
        /// <summary>Team chosen in the lobby (manual / host assigned team modes).</summary>
        public Team Team;
        /// <summary>Optional preselected character.</summary>
        public string CharacterId;

        public PlayerSetup(string nickname, bool isBot = false)
        {
            Nickname = nickname;
            IsBot = isBot;
        }
    }

    /// <summary>API a mode uses to lay out seats, teams and roles during preparation.</summary>
    public sealed class GameSetupContext
    {
        internal GameSetupContext(GameState state, IReadOnlyList<PlayerSetup> setups, IRandom random)
        {
            State = state;
            Setups = setups;
            Random = random;
        }

        public GameState State { get; }
        public GameModeConfig Config => State.Config;
        public IReadOnlyList<PlayerState> Players => State.Players;
        public IReadOnlyList<PlayerSetup> Setups { get; }
        public IRandom Random { get; }

        public void SetSeat(PlayerState p, int seat) => p.Seat = seat;
        public void SetTeam(PlayerState p, Team team) => p.Team = team;
        public void SetFaction(PlayerState p, Faction faction) => p.Faction = faction;

        public void SetRole(PlayerState p, Role role, bool revealed)
        {
            p.Role = role;
            p.RoleRevealed = revealed;
        }
    }

    /// <summary>
    /// A game mode: seating, teams, roles, visibility of roles, turn order, death rewards and
    /// victory conditions. The core engine only talks to this interface, so new modes (1v1, 2v2,
    /// kingdom battle, arena, PVE, boss) are added without touching the core.
    /// </summary>
    public interface IGameMode
    {
        string ModeId { get; }
        string DisplayName { get; }

        /// <summary>Checks that a config/player count combination is playable in this mode.</summary>
        ValidationResult ValidateSetup(GameModeConfig config, int playerCount);

        /// <summary>Assigns seats, teams, roles and factions. Runs once at game start.</summary>
        void SetupPlayers(GameSetupContext setup);

        int GetMaxHpBonus(GameState state, PlayerState player);

        /// <summary>Mode-granted skills (e.g. lord skills) added on top of character skills.</summary>
        IEnumerable<string> GetExtraSkillIds(GameState state, PlayerState player);

        PlayerState GetFirstPlayer(GameState state);
        PlayerState GetNextPlayer(GameState state, PlayerState current);

        bool IsRoleVisibleTo(GameState state, PlayerState target, int viewerId);
        bool RevealRoleOnDeath(GameState state, PlayerState victim);
        bool AreAllies(GameState state, PlayerState a, PlayerState b);

        /// <summary>Returns the result if the game is decided, otherwise null. Server only.</summary>
        GameResult CheckVictory(GameState state);

        /// <summary>Result when the round limit is reached.</summary>
        GameResult ResolveRoundLimit(GameState state);

        /// <summary>Death rewards/penalties (pushes actions); called after the victory check found no winner.</summary>
        void OnPlayerKilled(GameContext ctx, PlayerState victim, PlayerState killer);

        void CollectModifiers(GameState state, PlayerState owner, ModifierKind kind, List<Modifier> output);
    }
}
