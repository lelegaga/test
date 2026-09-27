using System;
using System.Collections.Generic;

namespace Sanguo.Events
{
    /// <summary>
    /// Publish/subscribe hub for game events. Observers only: UI, logs, animation, audio, network
    /// broadcast and AI bookkeeping subscribe here instead of calling each other. Rules never react
    /// through the bus (skills use the trigger system), so handlers cannot corrupt resolution order.
    /// Handlers may subscribe/unsubscribe while an event is being dispatched.
    /// </summary>
    public sealed class GameEventBus
    {
        private readonly Dictionary<GameEventType, Action<GameEvent>[]> _byType = new Dictionary<GameEventType, Action<GameEvent>[]>();
        private Action<GameEvent>[] _all = Array.Empty<Action<GameEvent>>();

        public IDisposable Subscribe(GameEventType type, Action<GameEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _byType.TryGetValue(type, out var arr);
            _byType[type] = Append(arr, handler);
            return new Subscription(() =>
            {
                if (_byType.TryGetValue(type, out var current)) _byType[type] = Without(current, handler);
            });
        }

        /// <summary>Typed convenience subscription.</summary>
        public IDisposable Subscribe<T>(Action<T> handler) where T : GameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Action<GameEvent> wrapper = e =>
            {
                if (e is T t) handler(t);
            };
            return SubscribeAll(wrapper);
        }

        public IDisposable SubscribeAll(Action<GameEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _all = Append(_all, handler);
            return new Subscription(() => _all = Without(_all, handler));
        }

        public void Publish(GameEvent e)
        {
            if (e == null) return;
            // Arrays are replaced (copy-on-write) on subscription changes, so iterating the captured
            // array is safe even if a handler changes subscriptions.
            if (_byType.TryGetValue(e.Type, out var typed))
            {
                for (int i = 0; i < typed.Length; i++) typed[i](e);
            }
            var all = _all;
            for (int i = 0; i < all.Length; i++) all[i](e);
        }

        public void Clear()
        {
            _byType.Clear();
            _all = Array.Empty<Action<GameEvent>>();
        }

        private static Action<GameEvent>[] Append(Action<GameEvent>[] arr, Action<GameEvent> h)
        {
            if (arr == null || arr.Length == 0) return new[] { h };
            var result = new Action<GameEvent>[arr.Length + 1];
            Array.Copy(arr, result, arr.Length);
            result[arr.Length] = h;
            return result;
        }

        private static Action<GameEvent>[] Without(Action<GameEvent>[] arr, Action<GameEvent> h)
        {
            int idx = Array.IndexOf(arr, h);
            if (idx < 0) return arr;
            var result = new Action<GameEvent>[arr.Length - 1];
            if (idx > 0) Array.Copy(arr, 0, result, 0, idx);
            if (idx < arr.Length - 1) Array.Copy(arr, idx + 1, result, idx, arr.Length - idx - 1);
            return result;
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }

    /// <summary>
    /// Server-side ordered event record. Assigns sequence numbers and buffers events produced while
    /// a command resolves until the session drains and broadcasts them.
    /// </summary>
    public sealed class EventLog
    {
        private readonly List<GameEvent> _pending = new List<GameEvent>();
        private readonly List<GameEvent> _history;

        public EventLog(bool keepHistory)
        {
            if (keepHistory) _history = new List<GameEvent>();
        }

        public int LastSequence { get; private set; }

        /// <summary>Full event history (for replays/debugging) when enabled, otherwise null.</summary>
        public IReadOnlyList<GameEvent> History => _history;

        public void Append(GameEvent e)
        {
            e.Sequence = ++LastSequence;
            _pending.Add(e);
            _history?.Add(e);
        }

        public int PendingCount => _pending.Count;

        /// <summary>Moves buffered events into <paramref name="output"/>.</summary>
        public void Drain(List<GameEvent> output)
        {
            output.AddRange(_pending);
            _pending.Clear();
        }
    }
}
