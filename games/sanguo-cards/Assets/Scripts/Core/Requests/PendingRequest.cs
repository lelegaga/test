namespace Sanguo.Core
{
    /// <summary>
    /// Something the server is waiting for from one player. Every command must answer an open
    /// request, so at any moment the server knows exactly which commands are acceptable from whom.
    /// Requests validate their own answers and provide the answer used on timeout.
    /// </summary>
    public abstract class PendingRequest
    {
        protected PendingRequest(int playerId)
        {
            PlayerId = playerId;
        }

        public int RequestId { get; internal set; }
        public int PlayerId { get; }
        public abstract RequestKind Kind { get; }

        /// <summary>True for out-of-turn responses (dodge, rescue, choices during resolution).</summary>
        public virtual bool IsResponseWindow => true;

        /// <summary>Free-form purpose key for UI/AI (for example "dodge_strike", "rescue", "discard_phase").</summary>
        public string Purpose { get; set; }

        /// <summary>Explicit timeout; 0 uses the mode config default for the request kind.</summary>
        public int TimeoutMs { get; set; }

        public long OpenedAtMs { get; internal set; }
        public long DeadlineMs { get; internal set; }
        public bool IsClosed { get; internal set; }
        public bool TimedOut { get; internal set; }
        public GameCommand Response { get; internal set; }

        public bool Passed => Response is RespondCommand r && r.Pass;

        /// <summary>Checks whether <paramref name="command"/> is a legal answer. Must not change state.</summary>
        public abstract ValidationResult Validate(GameContext ctx, GameCommand command);

        /// <summary>Answer applied when the player does not respond in time. Must be valid.</summary>
        public abstract GameCommand CreateDefaultResponse(GameContext ctx);

        /// <summary>Called once when the request is opened (before it is announced).</summary>
        protected internal virtual void OnOpened(GameContext ctx)
        {
        }

        public RequestInfo ToInfo()
        {
            var info = new RequestInfo
            {
                RequestId = RequestId,
                PlayerId = PlayerId,
                Kind = Kind,
                DeadlineMs = DeadlineMs,
                IsResponseWindow = IsResponseWindow,
                Purpose = Purpose
            };
            FillInfo(info);
            return info;
        }

        protected virtual void FillInfo(RequestInfo info)
        {
        }

        public override string ToString() => Kind + "#" + RequestId + "(p" + PlayerId + ")";
    }
}
