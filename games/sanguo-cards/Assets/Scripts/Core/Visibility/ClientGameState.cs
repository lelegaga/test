using System.Collections.Generic;
using System.Text;
using Sanguo.Events;
using Sanguo.GameModes;

namespace Sanguo.Core
{
    public sealed class ClientStatus
    {
        public string StatusId;
        public int Stacks;
        public int RemainingTurns;
    }

    public sealed class ClientSkillState
    {
        public string SkillId;
        /// <summary>Limited (once per game) skill already spent.</summary>
        public bool UsedUp;
        public bool Disabled;
    }

    /// <summary>
    /// What one viewer is allowed to know about a player ("ClientVisibleState"). Other players'
    /// <see cref="HandCards"/> are always null (only <see cref="HandCount"/> is known) and hidden roles
    /// are <see cref="Core.Role.Unknown"/>.
    /// </summary>
    public sealed class ClientPlayerState
    {
        public int PlayerId;
        public string Nickname;
        public int AvatarId;
        public int Seat;
        public Team Team;
        public Role Role;
        public bool RoleRevealed;
        public string CharacterId;
        public int Hp;
        public int MaxHp;
        public int HandCount;

        /// <summary>Only filled for the viewer's own seat.</summary>
        public List<CardInfo> HandCards;

        /// <summary>Indexed by (int)EquipSlot.</summary>
        public CardInfo[] Equipment = new CardInfo[EquipmentArea.SlotCount];

        public List<CardInfo> JudgeArea = new List<CardInfo>();
        public List<ClientStatus> Statuses = new List<ClientStatus>();
        public List<ClientSkillState> Skills = new List<ClientSkillState>();
        public bool Alive;
        public bool IsDying;
        public bool Connected;
        public bool AIControlled;

        public ClientSkillState FindSkill(string skillId)
        {
            for (int i = 0; i < Skills.Count; i++)
                if (Skills[i].SkillId == skillId) return Skills[i];
            return null;
        }
    }

    /// <summary>
    /// A client's replica of the game: presentation copy only, never authoritative. Built from a
    /// snapshot and kept current by applying projected events in sequence order. A sequence gap means
    /// the replica is stale and a new snapshot must be requested.
    /// </summary>
    public sealed class ClientGameState
    {
        public int ViewerId = -1;
        public string RoomId;
        public string ModeId;
        public int LastEventSequence;
        public GamePhase Phase;
        public int CurrentPlayerId = -1;
        public int TurnNumber;
        public int Round;
        public int DrawPileCount;
        public List<CardInfo> DiscardPile = new List<CardInfo>();
        public List<CardInfo> Processing = new List<CardInfo>();
        public List<ClientPlayerState> Players = new List<ClientPlayerState>();
        public List<RequestInfo> OpenRequests = new List<RequestInfo>();
        public bool IsGameOver;
        public GameResult Result;

        /// <summary>Set when an event could not be applied consistently; the owner should resync.</summary>
        public bool NeedsResync { get; private set; }

        public ClientPlayerState GetPlayer(int playerId)
        {
            return playerId >= 0 && playerId < Players.Count ? Players[playerId] : null;
        }

        public ClientPlayerState Self => GetPlayer(ViewerId);

        /// <summary>Applies the next event. Returns false (and flags a resync) on a sequence gap or inconsistency.</summary>
        public bool Apply(GameEvent e)
        {
            if (e == null) return false;
            if (e.Sequence <= LastEventSequence) return true; // already incorporated (e.g. duplicate after resync)
            if (e.Sequence != LastEventSequence + 1)
            {
                NeedsResync = true;
                return false;
            }
            if (!e.ApplyTo(this))
            {
                NeedsResync = true;
                LastEventSequence = e.Sequence;
                return false;
            }
            LastEventSequence = e.Sequence;
            return true;
        }

        public RequestInfo FindRequestFor(int playerId)
        {
            for (int i = 0; i < OpenRequests.Count; i++)
                if (OpenRequests[i].PlayerId == playerId) return OpenRequests[i];
            return null;
        }

        /// <summary>Canonical text form; two replicas with equal dumps are identical.</summary>
        public string Dump()
        {
            var sb = new StringBuilder();
            sb.Append("viewer=").Append(ViewerId).Append(" seq=").Append(LastEventSequence)
                .Append(" phase=").Append(Phase).Append(" cur=").Append(CurrentPlayerId)
                .Append(" turn=").Append(TurnNumber).Append(" round=").Append(Round)
                .Append(" draw=").Append(DrawPileCount).Append(" over=").Append(IsGameOver).Append('\n');
            sb.Append("discard=");
            AppendCards(sb, DiscardPile);
            sb.Append("\nprocessing=");
            AppendCards(sb, Processing);
            sb.Append('\n');
            foreach (var p in Players)
            {
                sb.Append("P").Append(p.PlayerId).Append(" seat=").Append(p.Seat).Append(" team=").Append(p.Team)
                    .Append(" role=").Append(p.Role).Append(p.RoleRevealed ? "!" : "")
                    .Append(" char=").Append(p.CharacterId).Append(" hp=").Append(p.Hp).Append('/').Append(p.MaxHp)
                    .Append(" hand=").Append(p.HandCount).Append(" alive=").Append(p.Alive).Append(" dying=").Append(p.IsDying)
                    .Append(" conn=").Append(p.Connected).Append(" ai=").Append(p.AIControlled);
                sb.Append(" cards=");
                if (p.HandCards == null) sb.Append("hidden");
                else AppendCards(sb, p.HandCards);
                sb.Append(" equip=");
                for (int i = 1; i < p.Equipment.Length; i++)
                    if (p.Equipment[i] != null) sb.Append(i).Append(':').Append(p.Equipment[i]).Append(',');
                sb.Append(" judge=");
                AppendCards(sb, p.JudgeArea);
                sb.Append(" status=");
                foreach (var s in p.Statuses) sb.Append(s.StatusId).Append('x').Append(s.Stacks).Append('/').Append(s.RemainingTurns).Append(',');
                sb.Append(" skills=");
                foreach (var s in p.Skills) sb.Append(s.SkillId).Append(s.UsedUp ? "(used)" : "").Append(s.Disabled ? "(off)" : "").Append(',');
                sb.Append('\n');
            }
            sb.Append("requests=");
            foreach (var r in OpenRequests)
            {
                sb.Append(r.Describe()).Append("@").Append(r.DeadlineMs).Append(r.HasPrivateDetails ? "+" : "").Append(';');
            }
            if (Result != null) sb.Append("\nresult=").Append(Result.Describe());
            return sb.ToString();
        }

        private static void AppendCards(StringBuilder sb, List<CardInfo> cards)
        {
            sb.Append('[');
            for (int i = 0; i < cards.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(cards[i]);
            }
            sb.Append(']');
        }
    }
}
