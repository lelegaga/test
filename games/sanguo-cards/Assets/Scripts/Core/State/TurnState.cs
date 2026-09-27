using System.Collections.Generic;

namespace Sanguo.Core
{
    /// <summary>Per-turn bookkeeping. Reset at every turn start.</summary>
    public sealed class TurnState
    {
        private readonly Dictionary<string, int> _cardUsage = new Dictionary<string, int>();
        private readonly List<GamePhase> _skipped = new List<GamePhase>();

        public int CurrentPlayerId { get; internal set; } = -1;
        public int TurnNumber { get; internal set; }

        /// <summary>Round number: increments whenever the turn order wraps past the first player.</summary>
        public int Round { get; internal set; }

        /// <summary>Set when the turn must end early (current player died, or a rule says so).</summary>
        public bool EndTurnRequested { get; internal set; }

        /// <summary>Number of actions taken in the current play phase (safety cap).</summary>
        public int PlayActionsThisPhase { get; internal set; }

        public int GetCardUsage(string key)
        {
            if (key == null) return 0;
            return _cardUsage.TryGetValue(key, out var n) ? n : 0;
        }

        internal void IncrementCardUsage(string key)
        {
            if (key == null) return;
            _cardUsage[key] = GetCardUsage(key) + 1;
        }

        public bool IsPhaseSkipped(GamePhase phase) => _skipped.Contains(phase);

        internal void SkipPhase(GamePhase phase)
        {
            if (!_skipped.Contains(phase)) _skipped.Add(phase);
        }

        internal void Reset(int playerId)
        {
            CurrentPlayerId = playerId;
            EndTurnRequested = false;
            PlayActionsThisPhase = 0;
            _cardUsage.Clear();
            _skipped.Clear();
        }
    }
}
