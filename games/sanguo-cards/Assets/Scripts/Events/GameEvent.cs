using Sanguo.Core;

namespace Sanguo.Events
{
    public enum GameEventType : byte
    {
        GameStarted = 0,
        TurnStarted = 1,
        PhaseChanged = 2,
        PhaseSkipped = 3,
        CardDrawn = 4,
        CardPlayed = 5,
        CardDiscarded = 6,
        CardMoved = 7,
        DeckReshuffled = 8,
        TargetSelected = 9,
        DamageCreated = 10,
        DamagePrevented = 11,
        DamageApplied = 12,
        HpChanged = 13,
        HealApplied = 14,
        PlayerDying = 15,
        PlayerDied = 16,
        RoleRevealed = 17,
        SkillActivated = 18,
        SkillStateChanged = 19,
        StatusChanged = 20,
        Judgement = 21,
        RequestOpened = 22,
        RequestClosed = 23,
        TurnEnded = 24,
        GameEnded = 25,
        PlayerStatusChanged = 26,
        CharacterAssigned = 27
    }

    /// <summary>
    /// Immutable record of something that happened on the server. Events are the only thing the
    /// server broadcasts during normal play (incremental sync); UI, logs, animation and audio all
    /// listen to them. Events describing hidden information override <see cref="ProjectFor"/> to
    /// strip what a given viewer must not learn.
    /// </summary>
    public abstract class GameEvent
    {
        /// <summary>Monotonic per game, assigned by <see cref="EventLog"/>. Clients detect gaps with it.</summary>
        public int Sequence;

        public abstract GameEventType Type { get; }

        /// <summary>
        /// Version of this event that <paramref name="viewerId"/> may see (-1 = spectator). Must never
        /// modify this instance; return a redacted copy (with the same Sequence) when needed.
        /// </summary>
        public virtual GameEvent ProjectFor(int viewerId) => this;

        /// <summary>
        /// Applies the state change carried by this event to a client replica. Pure notification
        /// events keep the default no-op. Returns false if the replica is inconsistent with the event.
        /// </summary>
        public virtual bool ApplyTo(ClientGameState state) => true;

        public override string ToString() => Type + "#" + Sequence;
    }
}
