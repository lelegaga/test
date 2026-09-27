using System.Collections.Generic;
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
