using System;

namespace Sanguo.Core
{
    public enum CommandType : byte
    {
        PlayCard = 0,
        UseSkill = 1,
        SelectTarget = 2,
        Respond = 3,
        EndTurn = 4
    }

    /// <summary>
    /// A player's intent ("I use card #123 on player 5"), never a result ("player 5 takes 3 damage").
    /// Every command names the request it answers (<see cref="RequestId"/>) and carries a strictly
    /// increasing per-player <see cref="SequenceNumber"/> so duplicated, stale and out-of-order
    /// commands are rejected by the server.
    /// </summary>
    public abstract class GameCommand
    {
        public int PlayerId;
        public int SequenceNumber;
        public int RequestId;

        public abstract CommandType Type { get; }

        public override string ToString() => Type + "(p" + PlayerId + ",seq" + SequenceNumber + ",req" + RequestId + ")";
    }

    /// <summary>Use a card from hand during the play phase.</summary>
    public sealed class PlayCardCommand : GameCommand
    {
        public override CommandType Type => CommandType.PlayCard;
        public int CardInstanceId;
        public int[] TargetIds = Array.Empty<int>();

        /// <summary>Optional conversion skill that lets the card be used as another card.</summary>
        public string SkillId;
        /// <summary>Card id the physical card is used as (requires <see cref="SkillId"/>).</summary>
        public string AsCardId;
    }

    /// <summary>Activate an active skill.</summary>
    public sealed class UseSkillCommand : GameCommand
    {
        public override CommandType Type => CommandType.UseSkill;
        public string SkillId;
        public int[] CardIds = Array.Empty<int>();
        public int[] TargetIds = Array.Empty<int>();
    }

    /// <summary>Answer to a ChooseTargets request.</summary>
    public sealed class SelectTargetCommand : GameCommand
    {
        public override CommandType Type => CommandType.SelectTarget;
        public int[] TargetIds = Array.Empty<int>();
    }

    /// <summary>
    /// Generic answer: respond with cards (dodge, heal...), discard cards, pick an option, confirm
    /// a skill, or pick a card from another player's area (<see cref="PickZone"/> + <see cref="PickIndex"/>).
    /// </summary>
    public sealed class RespondCommand : GameCommand
    {
        public override CommandType Type => CommandType.Respond;
        public bool Pass;
        public int[] CardIds = Array.Empty<int>();
        public int OptionIndex = -1;
        public ZoneType PickZone;
        /// <summary>Hand: position in the (hidden) hand. Equipment: (int)EquipSlot. Judge area: index.</summary>
        public int PickIndex = -1;
        /// <summary>Optional conversion skill used for the responding card.</summary>
        public string SkillId;
    }

    /// <summary>End the play phase.</summary>
    public sealed class EndTurnCommand : GameCommand
    {
        public override CommandType Type => CommandType.EndTurn;
    }
}
