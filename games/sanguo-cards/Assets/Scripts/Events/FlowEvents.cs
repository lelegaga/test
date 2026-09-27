using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.GameModes;

namespace Sanguo.Events
{
    public sealed class GameStartedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.GameStarted;
        public string ModeId;
        public int PlayerCount;
    }

    public sealed class TurnStartedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.TurnStarted;
        public int PlayerId;
        public int TurnNumber;
        public int Round;

        public override bool ApplyTo(ClientGameState state)
        {
            state.CurrentPlayerId = PlayerId;
            state.TurnNumber = TurnNumber;
            state.Round = Round;
            return true;
        }
    }

    public sealed class PhaseChangedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.PhaseChanged;
        public int PlayerId;
        public GamePhase Phase;

        public override bool ApplyTo(ClientGameState state)
        {
            state.Phase = Phase;
            return true;
        }
    }

    public sealed class PhaseSkippedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.PhaseSkipped;
        public int PlayerId;
        public GamePhase Phase;
    }

    public sealed class TurnEndedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.TurnEnded;
        public int PlayerId;
    }

    public struct PlayerRole
    {
        public int PlayerId;
        public Role Role;

        public PlayerRole(int playerId, Role role)
        {
            PlayerId = playerId;
            Role = role;
        }
    }

    /// <summary>The game is over. All roles become public.</summary>
    public sealed class GameEndedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.GameEnded;
        public GameResult Result;
        public List<PlayerRole> Roles = new List<PlayerRole>();

        public override bool ApplyTo(ClientGameState state)
        {
            state.IsGameOver = true;
            state.Phase = GamePhase.GameOver;
            state.Result = Result?.Clone();
            state.OpenRequests.Clear();
            foreach (var r in Roles)
            {
                var p = state.GetPlayer(r.PlayerId);
                if (p == null) return false;
                p.Role = r.Role;
                if (r.Role != Role.None) p.RoleRevealed = true;
            }
            return true;
        }
    }
}
