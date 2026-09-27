using System.Collections.Generic;

namespace Sanguo.Core
{
    public enum RejectReason : byte
    {
        None = 0,
        GameNotRunning = 1,
        GameOver = 2,
        UnknownPlayer = 3,
        PlayerDead = 4,
        DuplicateSequence = 5,
        OutOfOrderSequence = 6,
        NoPendingRequest = 7,
        RequestMismatch = 8,
        WrongCommandType = 9,
        CardNotOwned = 10,
        CardNotUsable = 11,
        UsageLimitReached = 12,
        InvalidTarget = 13,
        TargetOutOfRange = 14,
        WrongTargetCount = 15,
        WrongCardCount = 16,
        InvalidCard = 17,
        InvalidOption = 18,
        PassNotAllowed = 19,
        SkillUnavailable = 20,
        MalformedCommand = 21,
        InternalError = 22,
        RateLimited = 23
    }

    /// <summary>Outcome of a rule check. A failed check never changes game state.</summary>
    public readonly struct ValidationResult
    {
        public readonly RejectReason Reason;
        public readonly string Message;

        private ValidationResult(RejectReason reason, string message)
        {
            Reason = reason;
            Message = message;
        }

        public bool IsValid => Reason == RejectReason.None;

        public static ValidationResult Ok => new ValidationResult(RejectReason.None, null);

        public static ValidationResult Fail(RejectReason reason, string message = null)
        {
            return new ValidationResult(reason == RejectReason.None ? RejectReason.InternalError : reason, message ?? reason.ToString());
        }

        public override string ToString() => IsValid ? "Ok" : Reason + ": " + Message;
    }

    /// <summary>Result of submitting a command to the engine.</summary>
    public sealed class CommandResult
    {
        public bool Accepted;
        public RejectReason Reason;
        public string Message;

        public static CommandResult Ok() => new CommandResult { Accepted = true };

        public static CommandResult Reject(ValidationResult v)
        {
            return new CommandResult { Accepted = false, Reason = v.Reason, Message = v.Message };
        }

        public static CommandResult Reject(RejectReason reason, string message = null)
        {
            return new CommandResult { Accepted = false, Reason = reason, Message = message ?? reason.ToString() };
        }

        public override string ToString() => Accepted ? "Accepted" : "Rejected(" + Reason + ": " + Message + ")";
    }

    public enum SequenceCheck : byte
    {
        Ok = 0,
        Duplicate = 1,
        OutOfOrder = 2
    }

    /// <summary>
    /// Enforces strictly increasing per-player sequence numbers (last + 1). A sequence number is
    /// consumed even if the command is later rejected by the rules, so the client simply moves on.
    /// On reconnect the server tells the client its last accepted number.
    /// </summary>
    public sealed class CommandSequencer
    {
        private readonly Dictionary<int, int> _last = new Dictionary<int, int>();

        public int GetLast(int playerId) => _last.TryGetValue(playerId, out var n) ? n : 0;

        public SequenceCheck Check(int playerId, int sequence)
        {
            int last = GetLast(playerId);
            if (sequence <= last) return SequenceCheck.Duplicate;
            if (sequence != last + 1) return SequenceCheck.OutOfOrder;
            return SequenceCheck.Ok;
        }

        public void Consume(int playerId, int sequence)
        {
            _last[playerId] = sequence;
        }
    }
}
