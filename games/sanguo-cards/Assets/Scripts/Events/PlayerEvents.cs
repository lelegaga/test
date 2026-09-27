using System.Collections.Generic;
using Sanguo.Core;

namespace Sanguo.Events
{
    /// <summary>A character was assigned to a seat (setup, or a later character change).</summary>
    public sealed class CharacterAssignedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.CharacterAssigned;
        public int PlayerId;
        public string CharacterId;
        public int Hp;
        public int MaxHp;
        public List<string> SkillIds = new List<string>();

        public override bool ApplyTo(ClientGameState state)
        {
            var p = state.GetPlayer(PlayerId);
            if (p == null) return false;
            p.CharacterId = CharacterId;
            p.Hp = Hp;
            p.MaxHp = MaxHp;
            p.Skills.Clear();
            foreach (var id in SkillIds) p.Skills.Add(new ClientSkillState { SkillId = id });
            return true;
        }
    }

    /// <summary>A player entered (Entered=true) or left (rescued, Entered=false) the dying state.</summary>
    public sealed class PlayerDyingEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.PlayerDying;
        public int PlayerId;
        public bool Entered;

        public override bool ApplyTo(ClientGameState state)
        {
            var p = state.GetPlayer(PlayerId);
            if (p == null) return false;
            p.IsDying = Entered;
            return true;
        }
    }

    public sealed class PlayerDiedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.PlayerDied;
        public int PlayerId;
        public int KillerId = -1;
        /// <summary>Role revealed on death, or None when the mode keeps it hidden / has no roles.</summary>
        public Role RevealedRole;

        public override bool ApplyTo(ClientGameState state)
        {
            var p = state.GetPlayer(PlayerId);
            if (p == null) return false;
            p.Alive = false;
            p.IsDying = false;
            if (RevealedRole != Role.None && RevealedRole != Role.Unknown)
            {
                p.Role = RevealedRole;
                p.RoleRevealed = true;
            }
            return true;
        }
    }

    public sealed class RoleRevealedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.RoleRevealed;
        public int PlayerId;
        public Role Role;

        public override bool ApplyTo(ClientGameState state)
        {
            var p = state.GetPlayer(PlayerId);
            if (p == null) return false;
            p.Role = Role;
            p.RoleRevealed = true;
            return true;
        }
    }

    public sealed class SkillActivatedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.SkillActivated;
        public int PlayerId;
        public string SkillId;
        public int[] Targets;
    }

    public sealed class SkillStateChangedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.SkillStateChanged;
        public int PlayerId;
        public string SkillId;
        public bool UsedUp;
        public bool Disabled;

        public override bool ApplyTo(ClientGameState state)
        {
            var p = state.GetPlayer(PlayerId);
            var s = p?.FindSkill(SkillId);
            if (s == null)
            {
                if (p == null) return false;
                s = new ClientSkillState { SkillId = SkillId };
                p.Skills.Add(s);
            }
            s.UsedUp = UsedUp;
            s.Disabled = Disabled;
            return true;
        }
    }

    /// <summary>Status added, changed or removed (Stacks == 0 means removed).</summary>
    public sealed class StatusChangedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.StatusChanged;
        public int PlayerId;
        public string StatusId;
        public int Stacks;
        public int RemainingTurns;

        public override bool ApplyTo(ClientGameState state)
        {
            var p = state.GetPlayer(PlayerId);
            if (p == null) return false;
            int idx = p.Statuses.FindIndex(s => s.StatusId == StatusId);
            if (Stacks <= 0)
            {
                if (idx >= 0) p.Statuses.RemoveAt(idx);
                return true;
            }
            if (idx < 0)
            {
                p.Statuses.Add(new ClientStatus { StatusId = StatusId, Stacks = Stacks, RemainingTurns = RemainingTurns });
            }
            else
            {
                p.Statuses[idx].Stacks = Stacks;
                p.Statuses[idx].RemainingTurns = RemainingTurns;
            }
            return true;
        }
    }

    /// <summary>Connection / AI takeover state of a seat changed.</summary>
    public sealed class PlayerStatusChangedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.PlayerStatusChanged;
        public int PlayerId;
        public bool Connected;
        public bool AIControlled;

        public override bool ApplyTo(ClientGameState state)
        {
            var p = state.GetPlayer(PlayerId);
            if (p == null) return false;
            p.Connected = Connected;
            p.AIControlled = AIControlled;
            return true;
        }
    }
}
