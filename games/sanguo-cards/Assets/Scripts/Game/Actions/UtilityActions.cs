using System;
using System.Collections.Generic;
using Sanguo.Core;

namespace Sanguo.Game
{
    /// <summary>Runs a callback once. Handy for small skill effects (e.g. modifying trigger args).</summary>
    public sealed class CallbackAction : GameAction
    {
        private readonly Action<GameContext> _callback;

        public CallbackAction(Action<GameContext> callback)
        {
            _callback = callback;
        }

        public override ActionResult Step(GameContext ctx)
        {
            _callback?.Invoke(ctx);
            return ActionResult.Done;
        }
    }

    /// <summary>
    /// Asks a player to choose targets, then pushes the action built from the choice.
    /// </summary>
    public sealed class ChooseTargetsAction : GameAction
    {
        private readonly int _playerId;
        private readonly List<int> _candidates;
        private readonly int _min;
        private readonly int _max;
        private readonly string _purpose;
        private readonly string _skillId;
        private readonly Func<GameContext, int[], GameAction> _onChosen;
        private ChooseTargetsRequest _request;

        public ChooseTargetsAction(int playerId, List<int> candidates, int min, int max, string purpose, string skillId,
            Func<GameContext, int[], GameAction> onChosen)
        {
            _playerId = playerId;
            _candidates = candidates;
            _min = min;
            _max = max;
            _purpose = purpose;
            _skillId = skillId;
            _onChosen = onChosen;
        }

        public override ActionResult Step(GameContext ctx)
        {
            if (_request != null)
            {
                var selected = _request.Selected;
                _request = null;
                if (selected.Length == 0) return ActionResult.Done;
                var next = _onChosen(ctx, selected);
                if (next != null) ctx.Push(next);
                return ActionResult.Done;
            }
            if (_candidates == null || _candidates.Count == 0) return ActionResult.Done;
            _request = ctx.Requests.Open(ctx, new ChooseTargetsRequest(_playerId, _candidates, _min, _max, _purpose, _skillId));
            return ActionResult.Wait;
        }
    }
}
