using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Game;

namespace Sanguo.GameModes
{
    /// <summary>
    /// Classic identity mode: 主公 (lord, public), 忠臣 (loyalists), 反贼 (rebels), 内奸 (renegades).
    /// Player count and role distribution come from <see cref="GameModeConfig"/>; when
    /// <see cref="GameModeConfig.Roles"/> is empty the standard table for the player count is used.
    /// </summary>
    public sealed class IdentityMode : GameModeBase
    {
        public const string Id = "identity";

        /// <summary>Cards drawn by whoever kills a rebel.</summary>
        public const int RebelKillReward = 3;

        public IdentityMode(GameModeConfig config) : base(config)
        {
            VictoryConditions.Add(new IdentityVictoryCondition());
        }

        public override string ModeId => Id;
        public override string DisplayName => "身份模式";

        /// <summary>Standard distribution (lord, loyalists, rebels, renegades) for a player count.</summary>
        public static List<RoleCount> DefaultRoles(int players)
        {
            int loyalists, rebels, renegades;
            switch (players)
            {
                case 2: loyalists = 0; rebels = 1; renegades = 0; break;
                case 3: loyalists = 0; rebels = 1; renegades = 1; break;
                case 4: loyalists = 1; rebels = 1; renegades = 1; break;
                case 5: loyalists = 1; rebels = 2; renegades = 1; break;
                case 6: loyalists = 1; rebels = 3; renegades = 1; break;
                case 7: loyalists = 2; rebels = 3; renegades = 1; break;
                case 8: loyalists = 2; rebels = 4; renegades = 1; break;
                case 9: loyalists = 3; rebels = 4; renegades = 1; break;
                case 10: loyalists = 3; rebels = 4; renegades = 2; break;
                case 11: loyalists = 3; rebels = 5; renegades = 2; break;
                case 12: loyalists = 4; rebels = 5; renegades = 2; break;
                default:
                    renegades = players >= 10 ? 2 : 1;
                    rebels = (players - renegades) / 2;
                    loyalists = players - 1 - renegades - rebels;
                    break;
            }
            var list = new List<RoleCount> { new RoleCount(Role.Lord, 1) };
            if (loyalists > 0) list.Add(new RoleCount(Role.Loyalist, loyalists));
            if (rebels > 0) list.Add(new RoleCount(Role.Rebel, rebels));
            if (renegades > 0) list.Add(new RoleCount(Role.Renegade, renegades));
            return list;
        }

        /// <summary>Role counts in effect (config or default table).</summary>
        public List<RoleCount> EffectiveRoles(int players) => Config.Roles.Count > 0 ? Config.Roles : DefaultRoles(players);

        public override ValidationResult ValidateSetup(GameModeConfig config, int playerCount)
        {
            var baseResult = base.ValidateSetup(config, playerCount);
            if (!baseResult.IsValid) return baseResult;
            var roles = config.Roles.Count > 0 ? config.Roles : DefaultRoles(playerCount);
            int total = 0, lords = 0;
            foreach (var r in roles)
            {
                if (r.Count < 0 || (r.Role != Role.Lord && r.Role != Role.Loyalist && r.Role != Role.Rebel && r.Role != Role.Renegade))
                    return ValidationResult.Fail(RejectReason.MalformedCommand, "Invalid role entry " + r.Role + ".");
                total += r.Count;
                if (r.Role == Role.Lord) lords += r.Count;
            }
            if (lords != 1) return ValidationResult.Fail(RejectReason.MalformedCommand, "Identity mode needs exactly one lord.");
            if (total != playerCount) return ValidationResult.Fail(RejectReason.MalformedCommand, "Role counts (" + total + ") do not match player count (" + playerCount + ").");
            return ValidationResult.Ok;
        }

        public override void SetupPlayers(GameSetupContext setup)
        {
            if (setup.Config.ShuffleSeats) SeatRandomly(setup);
            else SeatInOrder(setup);

            var roles = new List<Role>();
            foreach (var rc in EffectiveRoles(setup.Players.Count))
                for (int i = 0; i < rc.Count; i++) roles.Add(rc.Role);

            if (!TryUsePresetRoles(setup, roles))
            {
                Utils.RandomExtensions.Shuffle(setup.Random, roles);
                for (int i = 0; i < setup.Players.Count; i++) Assign(setup, setup.Players[i], roles[i]);
            }
        }

        private static bool TryUsePresetRoles(GameSetupContext setup, List<Role> expected)
        {
            if (setup.Setups.Count != setup.Players.Count) return false;
            var preset = new List<Role>();
            foreach (var s in setup.Setups)
            {
                if (s.Role == Role.None || s.Role == Role.Unknown) return false;
                preset.Add(s.Role);
            }
            var a = new List<Role>(preset);
            var b = new List<Role>(expected);
            a.Sort();
            b.Sort();
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            for (int i = 0; i < setup.Players.Count; i++) Assign(setup, setup.Players[i], preset[i]);
            return true;
        }

        private static void Assign(GameSetupContext setup, PlayerState p, Role role)
        {
            setup.SetRole(p, role, role == Role.Lord);
            setup.SetFaction(p, FactionOf(role));
            setup.SetTeam(p, Team.None);
        }

        public static Faction FactionOf(Role role)
        {
            switch (role)
            {
                case Role.Lord:
                case Role.Loyalist:
                    return Faction.LordSide;
                case Role.Rebel:
                    return Faction.Rebels;
                case Role.Renegade:
                    return Faction.Renegade;
                default:
                    return Faction.None;
            }
        }

        public static PlayerState FindLord(GameState state)
        {
            foreach (var p in state.Players)
                if (p.Role == Role.Lord) return p;
            return null;
        }

        public override int GetMaxHpBonus(GameState state, PlayerState player)
        {
            return player.Role == Role.Lord && Config.LordExtraHp && state.PlayerCount > 4 ? 1 : 0;
        }

        public override IReadOnlyList<IReadOnlyList<PlayerState>> GetCharacterSelectionGroups(GameState state)
        {
            // The lord picks first (publicly); everyone else then picks knowing who the lord is.
            var lord = FindLord(state);
            var others = new List<PlayerState>();
            foreach (var p in state.SeatOrder)
                if (!ReferenceEquals(p, lord)) others.Add(p);
            var groups = new List<IReadOnlyList<PlayerState>>();
            if (lord != null) groups.Add(new List<PlayerState> { lord });
            groups.Add(others);
            return groups;
        }

        public override int GetCharacterChoiceCount(GameState state, PlayerState player, int configured)
        {
            return player.Role == Role.Lord ? configured + 2 : configured;
        }

        public override bool CanUseLordSkills(GameState state, PlayerState player) => player.Role == Role.Lord;

        public override PlayerState GetFirstPlayer(GameState state) => FindLord(state) ?? base.GetFirstPlayer(state);

        public override bool AreAllies(GameState state, PlayerState a, PlayerState b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            // Renegades are on their own, even when there are two of them.
            if (a.Faction == Faction.Renegade || b.Faction == Faction.Renegade) return false;
            return a.Faction != Faction.None && a.Faction == b.Faction;
        }

        public override void OnPlayerKilled(GameContext ctx, PlayerState victim, PlayerState killer)
        {
            if (killer == null || !killer.Alive || ReferenceEquals(killer, victim)) return;
            if (victim.Role == Role.Rebel)
            {
                ctx.Push(new DrawAction(killer.PlayerId, RebelKillReward));
            }
            else if (victim.Role == Role.Loyalist && killer.Role == Role.Lord)
            {
                // The lord killed a loyalist: discard all hand and equipped cards.
                var cards = new List<Cards.CardInstance>(killer.HandCards);
                killer.Equipment.CopyTo(cards);
                if (cards.Count > 0) ctx.Mutator.Discard(cards, MoveReason.Discard);
            }
        }
    }

    /// <summary>
    /// Identity victory rules, evaluated after every death:
    ///  - lord dead and the only survivor is a renegade → that renegade wins;
    ///  - lord dead otherwise → rebels win (all rebels, dead or alive);
    ///  - lord alive and no rebel or renegade alive → lord and loyalists win.
    /// </summary>
    public sealed class IdentityVictoryCondition : IVictoryCondition
    {
        public GameResult Evaluate(GameState state)
        {
            var lord = IdentityMode.FindLord(state);
            if (lord == null) return null;
            if (!lord.Alive)
            {
                PlayerState onlySurvivor = null;
                int alive = 0;
                foreach (var p in state.Players)
                {
                    if (!p.Alive) continue;
                    alive++;
                    onlySurvivor = p;
                }
                if (alive == 1 && onlySurvivor.Role == Role.Renegade)
                {
                    var r = new GameResult { WinningFaction = Faction.Renegade, Reason = "renegade_last" };
                    r.WinnerIds.Add(onlySurvivor.PlayerId);
                    return r;
                }
                var rebels = new GameResult { WinningFaction = Faction.Rebels, Reason = "lord_killed" };
                VictoryUtil.AddFactionMembers(state, Faction.Rebels, rebels.WinnerIds);
                return rebels;
            }
            if (VictoryUtil.CountAlive(state, Faction.Rebels) == 0 && VictoryUtil.CountAlive(state, Faction.Renegade) == 0)
            {
                var result = new GameResult { WinningFaction = Faction.LordSide, Reason = "enemies_eliminated" };
                VictoryUtil.AddFactionMembers(state, Faction.LordSide, result.WinnerIds);
                return result;
            }
            return null;
        }
    }
}
