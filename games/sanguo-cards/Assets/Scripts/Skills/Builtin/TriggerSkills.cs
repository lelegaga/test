using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Game;

namespace Sanguo.Skills.Builtin
{
    /// <summary>"After you take damage, you may draw N cards per damage point." (class DrawOnDamaged)</summary>
    public sealed class DrawOnDamagedSkill : TriggerSkillBase
    {
        public DrawOnDamagedSkill(SkillDefinition d) : base(d, TriggerTiming.OnDamageAfter)
        {
        }

        private int PerDamage => Definition.Params.GetInt("perDamage", 1);

        public override bool CanTrigger(GameContext ctx, PlayerState owner, TriggerEventArgs args)
        {
            return args.Damage != null && args.Damage.TargetId == owner.PlayerId && owner.Alive && args.Damage.Amount > 0;
        }

        public override GameAction CreateTriggerAction(GameContext ctx, PlayerState owner, TriggerEventArgs args)
        {
            return new DrawAction(owner.PlayerId, PerDamage * args.Damage.Amount);
        }
    }

    /// <summary>"Damage you take is reduced to at most N." Usually locked. (class DamageCap)</summary>
    public sealed class DamageCapSkill : TriggerSkillBase
    {
        public DamageCapSkill(SkillDefinition d) : base(d, TriggerTiming.OnDamageBefore)
        {
        }

        private int Max => Definition.Params.GetInt("max", 1);

        public override bool CanTrigger(GameContext ctx, PlayerState owner, TriggerEventArgs args)
        {
            return args.Damage != null && args.Damage.TargetId == owner.PlayerId && !args.Damage.Prevented && args.Damage.Amount > Max;
        }

        public override GameAction CreateTriggerAction(GameContext ctx, PlayerState owner, TriggerEventArgs args)
        {
            var damage = args.Damage;
            int max = Max;
            return new CallbackAction(_ =>
            {
                if (damage.Amount > max) damage.Amount = max;
            });
        }
    }

    /// <summary>"When you die, you may make another player draw N cards." (class DeathGift)</summary>
    public sealed class DeathGiftSkill : TriggerSkillBase
    {
        public DeathGiftSkill(SkillDefinition d) : base(d, TriggerTiming.OnDeath)
        {
        }

        private int Count => Definition.Params.GetInt("count", 3);

        public override bool CanTrigger(GameContext ctx, PlayerState owner, TriggerEventArgs args)
        {
            return args.PlayerId == owner.PlayerId && ctx.State.AliveCount > 0;
        }

        public override GameAction CreateTriggerAction(GameContext ctx, PlayerState owner, TriggerEventArgs args)
        {
            var candidates = new List<int>();
            foreach (var p in ctx.State.SeatOrder)
                if (p.Alive && p.PlayerId != owner.PlayerId) candidates.Add(p.PlayerId);
            int count = Count;
            return new ChooseTargetsAction(owner.PlayerId, candidates, 1, 1, "benefit", SkillId,
                (c, chosen) => new DrawAction(chosen[0], count));
        }
    }

    /// <summary>"After you heal another player, you may draw N cards." (class HealDraw)</summary>
    public sealed class HealDrawSkill : TriggerSkillBase
    {
        public HealDrawSkill(SkillDefinition d) : base(d, TriggerTiming.OnHeal)
        {
        }

        public override bool CanTrigger(GameContext ctx, PlayerState owner, TriggerEventArgs args)
        {
            return args.SourceId == owner.PlayerId && args.PlayerId != owner.PlayerId && owner.Alive;
        }

        public override GameAction CreateTriggerAction(GameContext ctx, PlayerState owner, TriggerEventArgs args)
        {
            return new DrawAction(owner.PlayerId, Definition.Params.GetInt("count", 1));
        }
    }
}
