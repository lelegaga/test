using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Core;

namespace Sanguo.Skills
{
    public enum SkillCategory : byte
    {
        /// <summary>主动技: used by the owner during their play phase (UseSkillCommand).</summary>
        Active = 0,
        /// <summary>被动技: always-on rule changes (modifiers, conversions, targeting immunity).</summary>
        Passive = 1,
        /// <summary>触发技: reacts to trigger timings through the SkillTriggerManager.</summary>
        Trigger = 2
    }

    /// <summary>
    /// Base of every character skill. A skill never touches the game state machine or state
    /// directly: it influences the game only by
    ///  - reacting to trigger timings (returning a <see cref="GameAction"/> to resolve),
    ///  - contributing modifiers (distance, limits, draw count...),
    ///  - allowing card conversions or forbidding targets,
    ///  - (active skills) validating and creating an activation action.
    /// New skills subclass one of <see cref="ActiveSkillBase"/>, <see cref="TriggerSkillBase"/> or
    /// <see cref="PassiveSkillBase"/> and register a factory in <see cref="SkillRegistry"/>.
    /// </summary>
    public abstract class SkillBase
    {
        protected SkillBase(SkillDefinition definition)
        {
            Definition = definition;
        }

        public SkillDefinition Definition { get; }
        public string SkillId => Definition.Id;
        public string Name => Definition.Name;
        public string Description => Definition.Description;

        public abstract SkillCategory Category { get; }

        /// <summary>锁定技: compulsory, resolves without asking the owner.</summary>
        public virtual bool IsLocked => Definition.Locked;

        /// <summary>限定技: may be used once per game.</summary>
        public virtual bool IsLimited => Definition.Limited;

        public bool IsLordSkill => Definition.LordSkill;

        // ------------------------------------------------------------- triggers

        public virtual bool ListensTo(TriggerTiming timing) => false;

        /// <summary>Whether the skill wants to respond to this trigger (checked when queued and again before resolving).</summary>
        public virtual bool CanTrigger(GameContext ctx, PlayerState owner, TriggerEventArgs args) => false;

        /// <summary>Action resolving the triggered effect (may be null for skills that only modify args).</summary>
        public virtual GameAction CreateTriggerAction(GameContext ctx, PlayerState owner, TriggerEventArgs args) => null;

        /// <summary>Optional triggers ask the owner first; locked ones resolve automatically.</summary>
        public virtual bool IsOptionalTrigger => !IsLocked;

        /// <summary>Default answer used by AI for this optional trigger.</summary>
        public virtual bool AIAcceptTrigger(PlayerState owner) => true;

        /// <summary>Card ids this skill can turn other cards into (lets AI enumerate conversions).</summary>
        public virtual IEnumerable<string> ConvertibleCardIds => System.Array.Empty<string>();

        // ------------------------------------------------------------- passive hooks

        public virtual void CollectModifiers(PlayerState owner, ModifierKind kind, List<Modifier> output)
        {
        }

        /// <summary>Allows <paramref name="card"/> to be used/played as <paramref name="asCardId"/>.</summary>
        public virtual bool CanConvert(GameContext ctx, PlayerState owner, CardInstance card, string asCardId, bool forResponse) => false;

        /// <summary>Forbids <paramref name="user"/> from targeting the skill owner with <paramref name="card"/>.</summary>
        public virtual bool ProhibitsTargeting(GameContext ctx, PlayerState owner, PlayerState user, CardBase card) => false;

        public override string ToString() => Name + "(" + SkillId + ")";
    }

    /// <summary>Skill used explicitly by its owner in the play phase.</summary>
    public abstract class ActiveSkillBase : SkillBase
    {
        protected ActiveSkillBase(SkillDefinition definition) : base(definition)
        {
        }

        public override SkillCategory Category => SkillCategory.Active;

        /// <summary>Uses allowed per play phase (0 = unlimited). Read from the "usesPerPhase" param.</summary>
        public virtual int UsesPerPhase => Definition.Params.GetInt("usesPerPhase", 1);

        /// <summary>Whether equipped cards may be spent on this skill.</summary>
        public virtual bool AllowsEquipmentCards => Definition.Params.GetBool("allowEquipment", false);

        // UI hints (the server still validates through ValidateActivation).
        public virtual int MinCards => Definition.Params.GetInt("minCards", 0);
        public virtual int MaxCards => Definition.Params.GetInt("maxCards", MinCards);
        public virtual int MinTargets => Definition.Params.GetInt("minTargets", 0);
        public virtual int MaxTargets => Definition.Params.GetInt("maxTargets", MinTargets);

        /// <summary>Players the skill may target now (UI hint). Default: every other living player when targets are used.</summary>
        public virtual void GetLegalTargets(GameContext ctx, PlayerState owner, List<int> output)
        {
            if (MaxTargets <= 0) return;
            foreach (var p in ctx.State.SeatOrder)
                if (p.Alive && p.PlayerId != owner.PlayerId) output.Add(p.PlayerId);
        }

        public virtual bool CanActivate(GameContext ctx, PlayerState owner, SkillInstance instance)
        {
            if (IsLimited && instance.UsedUp) return false;
            return UsesPerPhase <= 0 || instance.UsesThisPhase < UsesPerPhase;
        }

        /// <summary>Skill-specific checks (card count, targets...). Generic ownership checks are already done.</summary>
        public abstract ValidationResult ValidateActivation(GameContext ctx, PlayerState owner, UseSkillCommand command);

        public abstract GameAction CreateActivationAction(GameContext ctx, PlayerState owner, UseSkillCommand command);
    }

    /// <summary>Skill reacting to one or more trigger timings.</summary>
    public abstract class TriggerSkillBase : SkillBase
    {
        private readonly TriggerTiming[] _timings;

        protected TriggerSkillBase(SkillDefinition definition, params TriggerTiming[] timings) : base(definition)
        {
            _timings = timings;
        }

        public override SkillCategory Category => SkillCategory.Trigger;

        public override bool ListensTo(TriggerTiming timing)
        {
            for (int i = 0; i < _timings.Length; i++)
                if (_timings[i] == timing) return true;
            return false;
        }
    }

    /// <summary>Always-on rule change (locked by nature).</summary>
    public abstract class PassiveSkillBase : SkillBase
    {
        protected PassiveSkillBase(SkillDefinition definition) : base(definition)
        {
        }

        public override SkillCategory Category => SkillCategory.Passive;
        public override bool IsLocked => true;
    }

    /// <summary>Per-player runtime state of a skill (usage counters, limited flag).</summary>
    public sealed class SkillInstance
    {
        public SkillInstance(SkillBase skill)
        {
            Skill = skill;
        }

        public SkillBase Skill { get; }
        public int UsesThisPhase { get; internal set; }
        public int UsesThisTurn { get; internal set; }
        public int UsesThisGame { get; internal set; }
        public bool UsedUp { get; internal set; }
        public bool Disabled { get; internal set; }
    }
}
