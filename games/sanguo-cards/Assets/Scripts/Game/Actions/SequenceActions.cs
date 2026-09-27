using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Effects;

namespace Sanguo.Game
{
    /// <summary>Runs child actions one after another (each fully resolves before the next starts).</summary>
    public sealed class SequenceAction : GameAction
    {
        private readonly List<GameAction> _actions;
        private int _index;

        public SequenceAction(List<GameAction> actions)
        {
            _actions = actions ?? new List<GameAction>();
        }

        public override ActionResult Step(GameContext ctx)
        {
            while (_index < _actions.Count)
            {
                var next = _actions[_index++];
                if (next == null) continue;
                ctx.Push(next);
                return ActionResult.Continue;
            }
            return ActionResult.Done;
        }
    }

    /// <summary>
    /// Resolves a list of effects in order for one effect context. Actions are created lazily so
    /// each effect sees the state left by the previous one.
    /// </summary>
    public sealed class EffectSequenceAction : GameAction
    {
        private readonly IReadOnlyList<ICardEffect> _effects;
        private readonly EffectContext _context;
        private int _index;

        public EffectSequenceAction(IReadOnlyList<ICardEffect> effects, EffectContext context)
        {
            _effects = effects;
            _context = context;
        }

        public override ActionResult Step(GameContext ctx)
        {
            while (_effects != null && _index < _effects.Count)
            {
                var effect = _effects[_index++];
                var action = effect.CreateAction(ctx, _context);
                if (action == null) continue;
                ctx.Push(action);
                return ActionResult.Continue;
            }
            return ActionResult.Done;
        }

        /// <summary>Pushes the effects if there are any. Returns true when something was pushed.</summary>
        public static bool PushIfAny(GameContext ctx, IReadOnlyList<ICardEffect> effects, EffectContext context)
        {
            if (effects == null || effects.Count == 0) return false;
            ctx.Push(new EffectSequenceAction(effects, context));
            return true;
        }
    }
}
