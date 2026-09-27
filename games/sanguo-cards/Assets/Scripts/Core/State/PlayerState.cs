using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Characters;
using Sanguo.Skills;

namespace Sanguo.Core
{
    /// <summary>
    /// Server-side (authoritative) state of one player, including hidden information (hand, role).
    /// Clients never receive this object; they get a filtered <see cref="ClientPlayerState"/>.
    /// Setters are internal so only the engine (through GameStateMutator) can change it.
    /// </summary>
    public sealed class PlayerState
    {
        public PlayerState(int playerId, string nickname)
        {
            PlayerId = playerId;
            Nickname = nickname ?? ("Player" + playerId);
            HandCards = new CardZone(ZoneType.Hand, playerId);
            Equipment = new EquipmentArea(playerId);
            JudgeArea = new CardZone(ZoneType.JudgeArea, playerId);
        }

        public int PlayerId { get; }
        public string Nickname { get; internal set; }
        public int AvatarId { get; internal set; }

        /// <summary>Seat index around the table; turn order follows seats.</summary>
        public int Seat { get; internal set; }

        public Team Team { get; internal set; }
        public Role Role { get; internal set; }
        public Faction Faction { get; internal set; }

        /// <summary>True once the role is public (lord, dead players, game over, team modes).</summary>
        public bool RoleRevealed { get; internal set; }

        public CharacterData Character { get; internal set; }
        public int Hp { get; internal set; }
        public int MaxHp { get; internal set; }

        public CardZone HandCards { get; }
        public EquipmentArea Equipment { get; }
        public CardZone JudgeArea { get; }

        public List<StatusEffect> StatusEffects { get; } = new List<StatusEffect>();
        public List<SkillInstance> Skills { get; } = new List<SkillInstance>();

        public bool Alive { get; internal set; } = true;
        public bool IsDying { get; internal set; }
        public bool Connected { get; internal set; } = true;

        /// <summary>True when the host AI makes this player's decisions (bot seat or disconnected human).</summary>
        public bool AIControlled { get; internal set; }

        /// <summary>True for seats created as bots (as opposed to humans temporarily handed to AI).</summary>
        public bool IsBot { get; internal set; }

        public int HandCardCount => HandCards.Count;
        public bool IsWounded => Hp < MaxHp;
        public int TotalCardCount => HandCards.Count + Equipment.Count + JudgeArea.Count;

        public SkillInstance FindSkill(string skillId)
        {
            for (int i = 0; i < Skills.Count; i++)
                if (Skills[i].Skill.SkillId == skillId) return Skills[i];
            return null;
        }

        public bool HasStatus(string statusId) => FindStatus(statusId) != null;

        public StatusEffect FindStatus(string statusId)
        {
            for (int i = 0; i < StatusEffects.Count; i++)
                if (StatusEffects[i].StatusId == statusId) return StatusEffects[i];
            return null;
        }

        /// <summary>Finds a card this player owns in hand or equipment.</summary>
        public CardInstance FindOwnedCard(int instanceId, bool includeEquipment)
        {
            var c = HandCards.FindById(instanceId);
            if (c == null && includeEquipment) c = Equipment.FindById(instanceId);
            return c;
        }

        public override string ToString() => Nickname + "#" + PlayerId;
    }
}
