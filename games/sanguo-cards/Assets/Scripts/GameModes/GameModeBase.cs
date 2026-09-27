using System;
using System.Collections.Generic;
using Sanguo.Core;

namespace Sanguo.GameModes
{
    /// <summary>
    /// Common mode behaviour: seat-order turns, role visibility (own role + revealed roles),
    /// faction-based alliances and a list of <see cref="IVictoryCondition"/>s checked in order.
    /// </summary>
    public abstract class GameModeBase : IGameMode
    {
        protected GameModeBase(GameModeConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public GameModeConfig Config { get; }
        public abstract string ModeId { get; }
        public abstract string DisplayName { get; }

        protected List<IVictoryCondition> VictoryConditions { get; } = new List<IVictoryCondition>();

        public virtual ValidationResult ValidateSetup(GameModeConfig config, int playerCount)
        {
            if (playerCount < 2) return ValidationResult.Fail(RejectReason.MalformedCommand, "At least two players are required.");
            if (playerCount != config.PlayerCount)
                return ValidationResult.Fail(RejectReason.MalformedCommand, "Config expects " + config.PlayerCount + " players, got " + playerCount + ".");
            return ValidationResult.Ok;
        }

        public abstract void SetupPlayers(GameSetupContext setup);

        public virtual int GetMaxHpBonus(GameState state, PlayerState player) => 0;

        public virtual int GetStartingHandBonus(GameState state, PlayerState player) => 0;

        public virtual IReadOnlyList<IReadOnlyList<PlayerState>> GetCharacterSelectionGroups(GameState state)
        {
            return new List<IReadOnlyList<PlayerState>> { new List<PlayerState>(state.SeatOrder) };
        }

        public virtual int GetCharacterChoiceCount(GameState state, PlayerState player, int configured) => configured;

        public virtual bool CanUseLordSkills(GameState state, PlayerState player) => false;

        public virtual IEnumerable<string> GetExtraSkillIds(GameState state, PlayerState player) => Array.Empty<string>();

        public virtual PlayerState GetFirstPlayer(GameState state)
        {
            foreach (var p in state.SeatOrder)
                if (p.Alive) return p;
            return null;
        }

        public virtual PlayerState GetNextPlayer(GameState state, PlayerState current) => state.NextAlive(current);

        public virtual bool IsRoleVisibleTo(GameState state, PlayerState target, int viewerId)
        {
            return target.Role == Role.None || target.RoleRevealed || state.IsGameOver || target.PlayerId == viewerId;
        }

        public virtual bool RevealRoleOnDeath(GameState state, PlayerState victim) => true;

        public virtual bool AreAllies(GameState state, PlayerState a, PlayerState b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            return a.Faction != Faction.None && a.Faction != Faction.Solo && a.Faction == b.Faction;
        }

        public virtual GameResult CheckVictory(GameState state)
        {
            foreach (var condition in VictoryConditions)
            {
                var result = condition.Evaluate(state);
                if (result != null) return result;
            }
            return null;
        }

        public virtual GameResult ResolveRoundLimit(GameState state) => GameResult.Draw("round_limit");

        public virtual void OnPlayerKilled(GameContext ctx, PlayerState victim, PlayerState killer)
        {
        }

        public virtual void CollectModifiers(GameState state, PlayerState owner, ModifierKind kind, List<Modifier> output)
        {
        }

        /// <summary>Seats players in PlayerId order.</summary>
        protected static void SeatInOrder(GameSetupContext setup)
        {
            for (int i = 0; i < setup.Players.Count; i++) setup.SetSeat(setup.Players[i], i);
        }

        /// <summary>Seats players in a random order.</summary>
        protected static void SeatRandomly(GameSetupContext setup)
        {
            var order = new List<PlayerState>(setup.Players);
            Utils.RandomExtensions.Shuffle(setup.Random, order);
            for (int i = 0; i < order.Count; i++) setup.SetSeat(order[i], i);
        }
    }

    /// <summary>Everyone for themselves; the last player standing wins.</summary>
    public sealed class FreeForAllMode : GameModeBase
    {
        public const string Id = "ffa";

        public FreeForAllMode(GameModeConfig config) : base(config)
        {
            VictoryConditions.Add(new LastFactionStandingCondition());
        }

        public override string ModeId => Id;
        public override string DisplayName => "混战";

        public override void SetupPlayers(GameSetupContext setup)
        {
            if (setup.Config.ShuffleSeats) SeatRandomly(setup);
            else SeatInOrder(setup);
            foreach (var p in setup.Players)
            {
                setup.SetFaction(p, Faction.Solo);
                setup.SetRole(p, Role.None, false);
                setup.SetTeam(p, Team.None);
            }
        }

        public override GameResult ResolveRoundLimit(GameState state)
        {
            // Highest HP wins; ties are a draw.
            PlayerState best = null;
            bool tie = false;
            foreach (var p in state.Players)
            {
                if (!p.Alive) continue;
                if (best == null || p.Hp > best.Hp)
                {
                    best = p;
                    tie = false;
                }
                else if (p.Hp == best.Hp)
                {
                    tie = true;
                }
            }
            if (best == null || tie) return GameResult.Draw("round_limit");
            var r = new GameResult { WinningFaction = Faction.Solo, Reason = "round_limit" };
            r.WinnerIds.Add(best.PlayerId);
            return r;
        }
    }

    /// <summary>Creates modes by id. Future modes register here (no engine change needed).</summary>
    public sealed class GameModeRegistry
    {
        private readonly Dictionary<string, Func<GameModeConfig, IGameMode>> _factories =
            new Dictionary<string, Func<GameModeConfig, IGameMode>>(StringComparer.Ordinal);

        public IEnumerable<string> ModeIds => _factories.Keys;

        public void Register(string modeId, Func<GameModeConfig, IGameMode> factory)
        {
            _factories[modeId] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public bool Has(string modeId) => modeId != null && _factories.ContainsKey(modeId);

        public IGameMode Create(GameModeConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!_factories.TryGetValue(config.ModeId ?? string.Empty, out var f))
                throw new KeyNotFoundException("Unknown game mode '" + config.ModeId + "'.");
            return f(config);
        }

        public static GameModeRegistry CreateDefault()
        {
            var r = new GameModeRegistry();
            r.Register(FreeForAllMode.Id, c => new FreeForAllMode(c));
            r.Register(IdentityMode.Id, c => new IdentityMode(c));
            r.Register(TeamBattleMode.Id, c => new TeamBattleMode(c));
            r.Register(Team3v3Mode.Id, c => new Team3v3Mode(c));
            r.Register(Team5v5Mode.Id, c => new Team5v5Mode(c));
            r.Register(Team10v10Mode.Id, c => new Team10v10Mode(c));
            return r;
        }
    }
}
