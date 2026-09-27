using System.Collections.Generic;
using Sanguo.Characters;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.GameModes;
using Sanguo.Skills;
using Sanguo.Utils;

namespace Sanguo.Game
{
    /// <summary>Root action: setup, game start, then turns until the game ends.</summary>
    public sealed class GameFlowAction : GameAction
    {
        private readonly IReadOnlyList<PlayerSetup> _setups;
        private int _stage;
        private PlayerState _firstPlayer;

        public GameFlowAction(IReadOnlyList<PlayerSetup> setups)
        {
            _setups = setups;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var s = ctx.State;
            switch (_stage)
            {
                case 0:
                    _stage = 1;
                    ctx.Push(new GameSetupAction(_setups));
                    return ActionResult.Continue;
                case 1:
                    _stage = 2;
                    ctx.Mutator.TransitionPhase(GamePhase.GameStart);
                    ctx.Emit(new GameStartedEvent { ModeId = ctx.Mode.ModeId, PlayerCount = s.PlayerCount });
                    ctx.Triggers.Fire(ctx, new TriggerEventArgs(TriggerTiming.OnGameStart));
                    return ActionResult.Continue;
                default:
                {
                    if (s.IsGameOver) return ActionResult.Done;
                    var current = s.CurrentPlayer;
                    PlayerState next;
                    if (current == null)
                    {
                        next = ctx.Mode.GetFirstPlayer(s);
                        _firstPlayer = next;
                    }
                    else
                    {
                        next = ctx.Mode.GetNextPlayer(s, current);
                    }
                    if (next == null)
                    {
                        ctx.Mutator.EndGame(GameResult.Draw("no_players"));
                        return ActionResult.Done;
                    }
                    if (current != null && WrapsRound(s, current, next) && s.Turn.Round >= ctx.Config.MaxRounds)
                    {
                        ctx.Mutator.EndGame(ctx.Mode.ResolveRoundLimit(s) ?? GameResult.Draw("round_limit"));
                        return ActionResult.Done;
                    }
                    ctx.Push(new TurnAction(next.PlayerId, _firstPlayer));
                    return ActionResult.Continue;
                }
            }
        }

        private bool WrapsRound(GameState s, PlayerState current, PlayerState next)
        {
            int n = s.SeatOrder.Count;
            int first = s.SeatIndexOf(_firstPlayer ?? current);
            int rc = (s.SeatIndexOf(current) - first + n) % n;
            int rn = (s.SeatIndexOf(next) - first + n) % n;
            return rn <= rc;
        }
    }

    /// <summary>
    /// Preparation: seats/teams/roles (mode), deck, characters (none/random/choose), opening hands.
    /// </summary>
    public sealed class GameSetupAction : GameAction
    {
        private readonly IReadOnlyList<PlayerSetup> _setups;
        private readonly Dictionary<int, CharacterData> _chosen = new Dictionary<int, CharacterData>();
        private readonly List<ChooseCharacterRequest> _requests = new List<ChooseCharacterRequest>();
        private int _stage;

        public GameSetupAction(IReadOnlyList<PlayerSetup> setups)
        {
            _setups = setups;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var s = ctx.State;
            switch (_stage)
            {
                case 0:
                {
                    ctx.Mutator.TransitionPhase(GamePhase.Preparing);
                    ctx.Mode.SetupPlayers(new GameSetupContext(s, _setups, ctx.Random));
                    s.RebuildSeatOrder();
                    var deck = ctx.Content.GetDeck(ctx.Config.DeckId).Build(ctx.Content.Cards, ctx.Config.ResolveDeckCopies());
                    foreach (var card in deck)
                    {
                        s.RegisterCard(card);
                        s.DrawPile.Add(card);
                    }
                    s.DrawPile.Shuffle(ctx.Random);
                    _stage = 1;
                    return ActionResult.Continue;
                }
                case 1:
                    return SelectCharacters(ctx);
                case 2:
                    foreach (var r in _requests)
                    {
                        if (ctx.Content.Characters.TryGet(r.SelectedCharacterId, out var c)) _chosen[r.PlayerId] = c;
                    }
                    _requests.Clear();
                    _stage = 3;
                    return ActionResult.Continue;
                case 3:
                    foreach (var p in s.SeatOrder) ApplyCharacter(ctx, p);
                    _stage = 4;
                    return ActionResult.Continue;
                default:
                    foreach (var p in s.SeatOrder) ctx.Mutator.DrawCards(p, ctx.Config.StartingHandSize, MoveReason.Deal);
                    return ActionResult.Done;
            }
        }

        private ActionResult SelectCharacters(GameContext ctx)
        {
            var s = ctx.State;
            var config = ctx.Config;
            // Preselected characters from the lobby always win.
            for (int i = 0; i < s.PlayerCount && i < _setups.Count; i++)
            {
                if (_setups[i].CharacterId != null && ctx.Content.Characters.TryGet(_setups[i].CharacterId, out var pre))
                    _chosen[i] = pre;
            }
            if (config.CharacterSelection == CharacterSelectionMode.None)
            {
                _stage = 3;
                return ActionResult.Continue;
            }

            var pool = BuildPool(ctx);
            ctx.Random.Shuffle(pool);
            int next = 0;
            foreach (var p in s.SeatOrder)
            {
                if (_chosen.ContainsKey(p.PlayerId)) continue;
                if (config.CharacterSelection == CharacterSelectionMode.Random)
                {
                    if (next < pool.Count) _chosen[p.PlayerId] = pool[next++];
                    continue;
                }
                var options = new List<string>();
                for (int k = 0; k < config.CharacterChoices && next < pool.Count; k++) options.Add(pool[next++].Id);
                if (options.Count == 0) continue;
                _requests.Add(ctx.Requests.Open(ctx, new ChooseCharacterRequest(p.PlayerId, options)));
            }
            if (_requests.Count == 0)
            {
                _stage = 3;
                return ActionResult.Continue;
            }
            _stage = 2;
            return ActionResult.Wait;
        }

        private List<CharacterData> BuildPool(GameContext ctx)
        {
            var pool = new List<CharacterData>();
            var config = ctx.Config;
            if (config.CharacterPool.Count > 0)
            {
                foreach (var id in config.CharacterPool)
                    if (ctx.Content.Characters.TryGet(id, out var c) && !IsAlreadyChosen(c)) pool.Add(c);
            }
            else
            {
                foreach (var c in ctx.Content.Characters.All)
                    if (c.Id != config.DefaultCharacterId && !IsAlreadyChosen(c)) pool.Add(c);
            }
            return pool;
        }

        private bool IsAlreadyChosen(CharacterData c)
        {
            foreach (var v in _chosen.Values)
                if (ReferenceEquals(v, c)) return true;
            return false;
        }

        private void ApplyCharacter(GameContext ctx, PlayerState p)
        {
            var config = ctx.Config;
            bool useDefaultHp = config.CharacterSelection == CharacterSelectionMode.None && !_chosen.ContainsKey(p.PlayerId);
            if (!_chosen.TryGetValue(p.PlayerId, out var character))
            {
                if (!ctx.Content.Characters.TryGet(config.DefaultCharacterId, out character))
                    character = new CharacterData { Id = config.DefaultCharacterId, Name = "无名小卒", MaxHp = config.DefaultMaxHp };
            }
            p.Character = character;
            int baseHp = useDefaultHp ? config.DefaultMaxHp : character.MaxHp;
            p.MaxHp = baseHp + ctx.Mode.GetMaxHpBonus(ctx.State, p);
            p.Hp = p.MaxHp;
            p.Skills.Clear();
            var ev = new CharacterAssignedEvent { PlayerId = p.PlayerId, CharacterId = character.Id, Hp = p.Hp, MaxHp = p.MaxHp };
            foreach (var id in character.SkillIds) AddSkill(ctx, p, id, ev);
            foreach (var id in ctx.Mode.GetExtraSkillIds(ctx.State, p)) AddSkill(ctx, p, id, ev);
            ctx.Emit(ev);
        }

        private static void AddSkill(GameContext ctx, PlayerState p, string skillId, CharacterAssignedEvent ev)
        {
            if (!ctx.Content.Skills.TryGet(skillId, out var skill) || p.FindSkill(skillId) != null) return;
            p.Skills.Add(new SkillInstance(skill));
            ev.SkillIds.Add(skillId);
        }
    }

    /// <summary>
    /// One player's turn: turn start → judge → draw → play → discard → turn end. Phases are always
    /// entered in order through the state machine; a skipped phase is entered and left at once, and
    /// if the player dies (or the turn is ended) the remaining phases are cut to TurnEnd.
    /// </summary>
    public sealed class TurnAction : GameAction
    {
        private readonly int _playerId;
        private readonly PlayerState _firstPlayer;
        private int _stage;

        public TurnAction(int playerId, PlayerState firstPlayer)
        {
            _playerId = playerId;
            _firstPlayer = firstPlayer;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var s = ctx.State;
            var p = s.GetPlayer(_playerId);
            if (s.IsGameOver) return ActionResult.Done;
            switch (_stage)
            {
                case 0:
                    ctx.Mutator.StartTurn(p, _firstPlayer);
                    ctx.Mutator.ResetTurnSkillCounters();
                    ctx.Mutator.TransitionPhase(GamePhase.TurnStart);
                    _stage = 1;
                    ctx.Triggers.Fire(ctx, new TriggerEventArgs(TriggerTiming.OnTurnStart) { PlayerId = p.PlayerId });
                    return ActionResult.Continue;
                case 1:
                    return EnterPhase(ctx, p, GamePhase.JudgePhase);
                case 2:
                    return EnterPhase(ctx, p, GamePhase.DrawPhase);
                case 3:
                    return EnterPhase(ctx, p, GamePhase.PlayPhase);
                case 4:
                    return EnterPhase(ctx, p, GamePhase.DiscardPhase);
                case 5:
                    ctx.Mutator.TransitionPhase(GamePhase.TurnEnd);
                    _stage = 6;
                    if (p.Alive) ctx.Triggers.Fire(ctx, new TriggerEventArgs(TriggerTiming.OnTurnEnd) { PlayerId = p.PlayerId });
                    return ActionResult.Continue;
                default:
                    TickStatuses(ctx, p);
                    ctx.Mutator.EndTurn(p);
                    return ActionResult.Done;
            }
        }

        private ActionResult EnterPhase(GameContext ctx, PlayerState p, GamePhase phase)
        {
            if (!p.Alive || ctx.State.Turn.EndTurnRequested)
            {
                _stage = 5;
                return ActionResult.Continue;
            }
            ctx.Mutator.TransitionPhase(phase);
            ctx.Mutator.ResetPhaseSkillCounters();
            _stage++;
            if (ctx.State.Turn.IsPhaseSkipped(phase))
            {
                ctx.Mutator.SkipPhase(phase);
                return ActionResult.Continue;
            }
            switch (phase)
            {
                case GamePhase.JudgePhase: ctx.Push(new JudgePhaseAction(p.PlayerId)); break;
                case GamePhase.DrawPhase: ctx.Push(new DrawPhaseAction(p.PlayerId)); break;
                case GamePhase.PlayPhase: ctx.Push(new PlayPhaseAction(p.PlayerId)); break;
                case GamePhase.DiscardPhase: ctx.Push(new DiscardPhaseAction(p.PlayerId)); break;
            }
            return ActionResult.Continue;
        }

        private static void TickStatuses(GameContext ctx, PlayerState p)
        {
            for (int i = p.StatusEffects.Count - 1; i >= 0; i--)
            {
                var st = p.StatusEffects[i];
                if (st.RemainingTurns < 0) continue;
                int left = st.RemainingTurns - 1;
                ctx.Mutator.SetStatus(p, st.StatusId, left <= 0 ? 0 : st.Stacks, left, st.SourcePlayerId);
            }
        }
    }
}
