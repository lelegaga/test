using System;
using System.Collections.Generic;

namespace Sanguo.Core
{
    /// <summary>
    /// Authoritative phase tracker. Only legal transitions are accepted, so no skill or effect can
    /// corrupt the turn structure: skipping a phase means entering it and leaving it immediately
    /// (see TurnAction), never jumping over it.
    ///
    /// <see cref="GamePhase.WaitingResponse"/> is an overlay: while any response request is open the
    /// machine reports WaitingResponse and returns to the underlying phase when it closes.
    /// </summary>
    public sealed class GameStateMachine
    {
        private readonly Dictionary<GamePhase, HashSet<GamePhase>> _transitions = new Dictionary<GamePhase, HashSet<GamePhase>>();
        private int _responseDepth;

        public GamePhase Phase { get; private set; } = GamePhase.Waiting;

        /// <summary>Current state including the WaitingResponse overlay.</summary>
        public GamePhase Current => Phase == GamePhase.GameOver ? GamePhase.GameOver
            : _responseDepth > 0 ? GamePhase.WaitingResponse : Phase;

        public bool IsWaitingResponse => _responseDepth > 0 && Phase != GamePhase.GameOver;

        public event Action<GamePhase, GamePhase> PhaseChanged;

        public GameStateMachine()
        {
            Allow(GamePhase.Waiting, GamePhase.Preparing);
            Allow(GamePhase.Preparing, GamePhase.GameStart);
            Allow(GamePhase.GameStart, GamePhase.TurnStart);
            Allow(GamePhase.TurnStart, GamePhase.JudgePhase);
            Allow(GamePhase.JudgePhase, GamePhase.DrawPhase);
            Allow(GamePhase.DrawPhase, GamePhase.PlayPhase);
            Allow(GamePhase.PlayPhase, GamePhase.DiscardPhase);
            Allow(GamePhase.DiscardPhase, GamePhase.TurnEnd);
            Allow(GamePhase.TurnEnd, GamePhase.TurnStart);

            // The turn is cut short when the current player dies (or a rule ends the turn early).
            Allow(GamePhase.TurnStart, GamePhase.TurnEnd);
            Allow(GamePhase.JudgePhase, GamePhase.TurnEnd);
            Allow(GamePhase.DrawPhase, GamePhase.TurnEnd);
            Allow(GamePhase.PlayPhase, GamePhase.TurnEnd);

            foreach (GamePhase p in Enum.GetValues(typeof(GamePhase)))
            {
                if (p != GamePhase.GameOver && p != GamePhase.WaitingResponse) Allow(p, GamePhase.GameOver);
            }
        }

        /// <summary>Extension point for modes that need extra transitions (for example extra phases).</summary>
        public void Allow(GamePhase from, GamePhase to)
        {
            if (from == GamePhase.WaitingResponse || to == GamePhase.WaitingResponse)
                throw new ArgumentException("WaitingResponse is an overlay and is not part of the transition table.");
            if (!_transitions.TryGetValue(from, out var set))
            {
                set = new HashSet<GamePhase>();
                _transitions[from] = set;
            }
            set.Add(to);
        }

        public bool CanTransition(GamePhase to)
        {
            return _transitions.TryGetValue(Phase, out var set) && set.Contains(to);
        }

        public void TransitionTo(GamePhase to)
        {
            if (!CanTransition(to))
                throw new InvalidOperationException("Illegal phase transition " + Phase + " -> " + to + ".");
            var from = Phase;
            Phase = to;
            if (to == GamePhase.GameOver) _responseDepth = 0;
            PhaseChanged?.Invoke(from, to);
        }

        public void EnterWaitingResponse()
        {
            if (Phase == GamePhase.Waiting || Phase == GamePhase.GameOver)
                throw new InvalidOperationException("Cannot wait for a response in phase " + Phase + ".");
            _responseDepth++;
        }

        public void ExitWaitingResponse()
        {
            if (Phase == GamePhase.GameOver) return; // overlay already cleared by the game ending
            if (_responseDepth == 0) throw new InvalidOperationException("Not waiting for a response.");
            _responseDepth--;
        }
    }
}
