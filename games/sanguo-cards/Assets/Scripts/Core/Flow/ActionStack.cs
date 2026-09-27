using System;
using System.Collections.Generic;

namespace Sanguo.Core
{
    /// <summary>Raised when resolution violates an engine invariant (runaway loop, stack overflow).</summary>
    public sealed class GameFlowException : Exception
    {
        public GameFlowException(string message) : base(message)
        {
        }
    }

    /// <summary>LIFO stack of pending <see cref="GameAction"/>s.</summary>
    public sealed class ActionStack
    {
        /// <summary>Hard cap on nesting. Real games stay far below; hitting it means a rules bug.</summary>
        public const int MaxDepth = 256;

        private readonly List<GameAction> _actions = new List<GameAction>(32);

        public int Count => _actions.Count;

        public GameAction Peek() => _actions.Count > 0 ? _actions[_actions.Count - 1] : null;

        public GameAction this[int index] => _actions[index];

        public void Push(GameAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (_actions.Count >= MaxDepth)
                throw new GameFlowException("Action stack exceeded " + MaxDepth + " entries while pushing " + action + ".");
            _actions.Add(action);
        }

        /// <summary>Removes a specific action (it may sit below children it pushed while finishing).</summary>
        public void Remove(GameAction action)
        {
            for (int i = _actions.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_actions[i], action))
                {
                    _actions.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>Counts the actions of type T currently on the stack.</summary>
        public int CountOf<T>() where T : GameAction
        {
            int n = 0;
            for (int i = 0; i < _actions.Count; i++)
                if (_actions[i] is T) n++;
            return n;
        }

        public void Clear(GameContext ctx)
        {
            for (int i = _actions.Count - 1; i >= 0; i--)
            {
                var a = _actions[i];
                _actions.RemoveAt(i);
                a.OnAborted(ctx);
            }
        }
    }
}
