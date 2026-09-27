using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Core;

namespace Sanguo.AI
{
    public enum Relation : byte
    {
        Self = 0,
        Ally = 1,
        Enemy = 2,
        Unknown = 3
    }

    /// <summary>
    /// Public information about another player: everything except the hand contents and hidden role.
    /// A thin wrapper (no copying) so AI decisions stay allocation-light.
    /// </summary>
    public readonly struct PlayerView
    {
        private readonly PlayerState _p;

        public PlayerView(PlayerState p)
        {
            _p = p;
        }

        public bool IsValid => _p != null;
        public int PlayerId => _p.PlayerId;
        public int Seat => _p.Seat;
        public int Hp => _p.Hp;
        public int MaxHp => _p.MaxHp;
        public bool Alive => _p.Alive;
        public bool IsDying => _p.IsDying;
        public bool IsWounded => _p.IsWounded;
        public int HandCount => _p.HandCards.Count;
        public int TotalCardCount => _p.TotalCardCount;
        public Team Team => _p.Team;
        public string CharacterId => _p.Character?.Id;
        public EquipmentArea Equipment => _p.Equipment;
        public CardZone JudgeArea => _p.JudgeArea;

        public bool HasSkill(string skillId) => _p.FindSkill(skillId) != null;
    }

    /// <summary>Estimates relations to players whose allegiance is not public (identity mode reasoning).</summary>
    public interface IRelationEstimator
    {
        /// <summary>-1 (surely ally) .. +1 (surely enemy) for a player whose relation is not known.</summary>
        float EstimateHostility(PlayerPerspective view, int targetId);
    }

    /// <summary>
    /// What one AI-controlled player is allowed to know. AI code reads the game only through this
    /// class and <see cref="IRulesQuery"/>, so bots play by the same information rules as humans.
    /// </summary>
    public sealed class PlayerPerspective
    {
        private readonly GameContext _ctx;
        private readonly IRelationEstimator _estimator;

        public PlayerPerspective(GameContext ctx, int viewerId, IRelationEstimator estimator = null)
        {
            _ctx = ctx;
            ViewerId = viewerId;
            _estimator = estimator;
        }

        public int ViewerId { get; }

        /// <summary>The viewer's own full state (own hand is known).</summary>
        public PlayerState Self => _ctx.State.GetPlayer(ViewerId);

        public IRulesQuery Rules => _ctx.Rules;
        public int PlayerCount => _ctx.State.PlayerCount;
        public int CurrentPlayerId => _ctx.State.Turn.CurrentPlayerId;
        public GamePhase Phase => _ctx.State.Phase;
        public int DrawPileCount => _ctx.State.DrawPile.Count;
        public int AliveCount => _ctx.State.AliveCount;
        public Data.GameContent Content => _ctx.Content;

        public PlayerView GetPlayer(int playerId)
        {
            var p = _ctx.State.GetPlayer(playerId);
            return new PlayerView(p);
        }

        public IEnumerable<PlayerView> Players
        {
            get
            {
                foreach (var p in _ctx.State.SeatOrder) yield return new PlayerView(p);
            }
        }

        /// <summary>Role of a player as far as the viewer knows (Unknown when hidden).</summary>
        public Role KnownRole(int playerId)
        {
            var p = _ctx.State.GetPlayer(playerId);
            if (p == null) return Role.None;
            return _ctx.Mode.IsRoleVisibleTo(_ctx.State, p, ViewerId) ? p.Role : (p.Role == Role.None ? Role.None : Role.Unknown);
        }

        public Role OwnRole => Self.Role;

        public Relation GetRelation(int playerId)
        {
            if (playerId == ViewerId) return Relation.Self;
            var target = _ctx.State.GetPlayer(playerId);
            var self = Self;
            if (target == null) return Relation.Unknown;
            if (self.Team != Team.None && target.Team != Team.None)
                return self.Team == target.Team ? Relation.Ally : Relation.Enemy;
            if (_ctx.Mode.IsRoleVisibleTo(_ctx.State, target, ViewerId))
                return _ctx.Mode.AreAllies(_ctx.State, self, target) ? Relation.Ally : Relation.Enemy;
            return Relation.Unknown;
        }

        /// <summary>-1 ally .. +1 enemy. Self is -1.</summary>
        public float Hostility(int playerId)
        {
            switch (GetRelation(playerId))
            {
                case Relation.Self:
                case Relation.Ally:
                    return -1f;
                case Relation.Enemy:
                    return 1f;
                default:
                    return _estimator?.EstimateHostility(this, playerId) ?? 0f;
            }
        }

        public int CountInHand(string cardId)
        {
            int n = 0;
            foreach (var c in Self.HandCards)
                if (c.CardId == cardId) n++;
            return n;
        }

        /// <summary>Share of cards with this id in the deck (rough prior for "does the target hold one").</summary>
        public float DeckFrequency(string cardId)
        {
            int total = 0, match = 0;
            foreach (var c in _ctx.State.Cards.Values)
            {
                total++;
                if (c.CardId == cardId) match++;
            }
            return total == 0 ? 0f : (float)match / total;
        }

        public CardBase GetCardDefinition(string cardId) => _ctx.Content.Cards.TryGet(cardId, out var d) ? d : null;
    }
}
