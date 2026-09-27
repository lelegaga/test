using System;
using System.Collections.Generic;

namespace Sanguo.Utils
{
    /// <summary>
    /// Tiny list pool for hot paths (rule queries, AI candidate generation) to keep GC pressure low
    /// on mobile. Pools are per thread, so a host running the game loop on a worker thread is safe.
    /// </summary>
    public static class ListPool<T>
    {
        [ThreadStatic] private static Stack<List<T>> _pool;

        public static List<T> Get()
        {
            var pool = _pool;
            return pool != null && pool.Count > 0 ? pool.Pop() : new List<T>();
        }

        public static void Release(List<T> list)
        {
            if (list == null) return;
            list.Clear();
            var pool = _pool ?? (_pool = new Stack<List<T>>());
            if (pool.Count < 64) pool.Push(list);
        }
    }
}
