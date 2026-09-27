using System;
using System.Collections.Generic;

namespace Sanguo.Utils
{
    /// <summary>
    /// Deterministic random source. Game logic only ever uses the server's instance, so a seed
    /// plus the ordered command list reproduces an entire game (replays, bug reports, tests).
    /// </summary>
    public interface IRandom
    {
        /// <summary>Returns an integer in [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }

    /// <summary>xorshift128+ seeded through splitmix64. Allocation free and platform independent.</summary>
    public sealed class XorShiftRandom : IRandom
    {
        private ulong _s0;
        private ulong _s1;

        public XorShiftRandom(int seed)
        {
            ulong x = unchecked((ulong)seed);
            _s0 = SplitMix(ref x);
            _s1 = SplitMix(ref x);
            if (_s0 == 0 && _s1 == 0) _s1 = 1;
        }

        private static ulong SplitMix(ref ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                ulong z = x;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                ulong s1 = _s0;
                ulong s0 = _s1;
                _s0 = s0;
                s1 ^= s1 << 23;
                _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
                return _s1 + s0;
            }
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            // Rejection sampling removes modulo bias.
            ulong bound = (ulong)maxExclusive;
            ulong limit = ulong.MaxValue - (ulong.MaxValue % bound);
            ulong r;
            do { r = NextUInt64(); } while (r >= limit);
            return (int)(r % bound);
        }
    }

    public static class RandomExtensions
    {
        public static int Range(this IRandom rng, int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + rng.Next(maxExclusive - minInclusive);
        }

        /// <summary>Fisher-Yates shuffle in place.</summary>
        public static void Shuffle<T>(this IRandom rng, IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        public static T Pick<T>(this IRandom rng, IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0) throw new ArgumentException("Cannot pick from an empty list.", nameof(list));
            return list[rng.Next(list.Count)];
        }
    }
}
