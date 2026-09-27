using System.Diagnostics;

namespace Sanguo.Utils
{
    /// <summary>Monotonic millisecond time source. Injected so timeouts are testable.</summary>
    public interface IClock
    {
        long NowMs { get; }
    }

    public sealed class SystemClock : IClock
    {
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        public long NowMs => _watch.ElapsedMilliseconds;
    }

    /// <summary>Clock advanced by hand; used by tests and deterministic simulations.</summary>
    public sealed class ManualClock : IClock
    {
        public long NowMs { get; set; }

        public void Advance(long ms)
        {
            NowMs += ms;
        }
    }
}
