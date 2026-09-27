namespace Sanguo.Core
{
    /// <summary>
    /// A public marker/buff on a player (for example "chained", "drunk", "+1 attack this turn").
    /// Rules read statuses through the modifier system; statuses never run code by themselves.
    /// </summary>
    public sealed class StatusEffect
    {
        public StatusEffect(string statusId, int stacks, int remainingTurns, int sourcePlayerId)
        {
            StatusId = statusId;
            Stacks = stacks;
            RemainingTurns = remainingTurns;
            SourcePlayerId = sourcePlayerId;
        }

        public string StatusId { get; }
        public int Stacks { get; internal set; }

        /// <summary>Turns of the owner left before expiry; -1 means until removed.</summary>
        public int RemainingTurns { get; internal set; }

        public int SourcePlayerId { get; }
    }
}
