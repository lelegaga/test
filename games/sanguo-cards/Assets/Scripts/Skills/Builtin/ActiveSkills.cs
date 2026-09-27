using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Core;
using Sanguo.Effects;
using Sanguo.Game;

namespace Sanguo.Skills.Builtin
{
    /// <summary>
    /// "Once per play phase, discard N hand cards to deal D damage to a player in your attack range."
    /// (class DiscardToDamage; params cost, damage, usesPerPhase)
    /// </summary>
    public sealed class DiscardToDamageSkill : ActiveSkillBase
    {
        public DiscardToDamageSkill(SkillDefinition d) : base(d)
        {
        }

        public int Cost => Definition.Params.GetInt("cost", 2);
        public int Damage => Definition.Params.GetInt("damage", 1);

        public override int MinCards => Cost;
        public override int MaxCards => Cost;
        public override int MinTargets => 1;
        public override int MaxTargets => 1;

        public override void GetLegalTargets(GameContext ctx, PlayerState owner, List<int> output)
        {
            foreach (var p in ctx.State.SeatOrder)
                if (ctx.Rules.IsInAttackRange(owner, p)) output.Add(p.PlayerId);
        }

        public override bool CanActivate(GameContext ctx, PlayerState owner, SkillInstance instance)
        {
            return base.CanActivate(ctx, owner, instance) && owner.HandCards.Count >= Cost;
        }

        public override ValidationResult ValidateActivation(GameContext ctx, PlayerState owner, UseSkillCommand cmd)
        {
            if (cmd.CardIds.Length != Cost) return ValidationResult.Fail(RejectReason.WrongCardCount, "Discard exactly " + Cost + " cards.");
            foreach (int id in cmd.CardIds)
                if (owner.HandCards.FindById(id) == null) return ValidationResult.Fail(RejectReason.CardNotOwned);
            if (cmd.TargetIds.Length != 1) return ValidationResult.Fail(RejectReason.WrongTargetCount);
            var target = ctx.GetPlayer(cmd.TargetIds[0]);
            if (!ctx.Rules.IsInAttackRange(owner, target)) return ValidationResult.Fail(RejectReason.TargetOutOfRange);
            return ValidationResult.Ok;
        }

        public override GameAction CreateActivationAction(GameContext ctx, PlayerState owner, UseSkillCommand cmd)
        {
            var cards = new List<CardInstance>();
            foreach (int id in cmd.CardIds) cards.Add(owner.HandCards.FindById(id));
            int ownerId = owner.PlayerId;
            int targetId = cmd.TargetIds[0];
            return new SequenceAction(new List<GameAction>
            {
                new CallbackAction(c => c.Mutator.Discard(cards, MoveReason.Skill)),
                new DamageAction(ownerId, targetId, Damage, null)
            });
        }
    }

    /// <summary>"Limited: deal D damage to every other player." (class AreaDamage; usually limited)</summary>
    public sealed class AreaDamageSkill : ActiveSkillBase
    {
        public AreaDamageSkill(SkillDefinition d) : base(d)
        {
        }

        public int Damage => Definition.Params.GetInt("damage", 1);

        public override ValidationResult ValidateActivation(GameContext ctx, PlayerState owner, UseSkillCommand cmd)
        {
            if (cmd.CardIds.Length != 0 || cmd.TargetIds.Length != 0)
                return ValidationResult.Fail(RejectReason.MalformedCommand, "This skill takes no cards or targets.");
            return ValidationResult.Ok;
        }

        public override GameAction CreateActivationAction(GameContext ctx, PlayerState owner, UseSkillCommand cmd)
        {
            var ec = new EffectContext { SourceId = owner.PlayerId, TargetId = -1, SkillId = SkillId };
            return new AOEAction(ec, AreaScope.AllOthers, new List<ICardEffect> { new DamageEffect(Damage) });
        }
    }

    /// <summary>Registers every built-in skill class name used by skills.json.</summary>
    public static class BuiltinSkills
    {
        public static void RegisterClasses(SkillRegistry registry)
        {
            registry.RegisterClass("DrawOnDamaged", d => new DrawOnDamagedSkill(d));
            registry.RegisterClass("DamageCap", d => new DamageCapSkill(d));
            registry.RegisterClass("DeathGift", d => new DeathGiftSkill(d));
            registry.RegisterClass("HealDraw", d => new HealDrawSkill(d));
            registry.RegisterClass("ExtraDraw", d => new ExtraDrawSkill(d));
            registry.RegisterClass("UsageLimitBonus", d => new UsageLimitBonusSkill(d));
            registry.RegisterClass("DistanceBonus", d => new DistanceBonusSkill(d));
            registry.RegisterClass("CardConversion", d => new CardConversionSkill(d));
            registry.RegisterClass("DiscardToDamage", d => new DiscardToDamageSkill(d));
            registry.RegisterClass("AreaDamage", d => new AreaDamageSkill(d));
        }
    }
}
