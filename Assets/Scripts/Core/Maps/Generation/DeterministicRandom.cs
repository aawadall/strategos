// DeterministicRandom.cs
// Explicitly seeded PRNG for map generation.
//
// UnityEngine.Random is deliberately NOT used anywhere in the generator: it is a
// single piece of global mutable state, so a map's output would depend on
// whatever else in the process happened to draw from it first. That makes a seed
// worthless and a headless CI bake unreproducible. Every stage draws from an
// instance of this class threaded through MapGenContext instead.

using System;

namespace Strategos.Maps
{
    /// <summary>
    /// PCG-XSH-RR 32-bit generator. Small, fast, well-distributed, and — the point
    /// here — specified entirely in integer arithmetic, so the same seed yields the
    /// same stream on every platform and every Unity version.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _increment;

        public DeterministicRandom(int seed) : this((ulong)(uint)seed, 1442695040888963407UL) { }

        public DeterministicRandom(ulong seed, ulong sequence)
        {
            unchecked
            {
                _increment = (sequence << 1) | 1UL;
                _state     = 0UL;
                NextUInt();
                _state += seed;
                NextUInt();
            }
        }

        /// <summary>
        /// Derives an independent stream from this one. Use it to give each
        /// generation stage its own generator, so adding or reordering draws inside
        /// one stage does not shift every later stage's output.
        /// </summary>
        public DeterministicRandom Fork(int streamId) =>
            new DeterministicRandom(NextULong(), (ulong)(uint)streamId * 2654435761UL + 1UL);

        public uint NextUInt()
        {
            unchecked
            {
                ulong old = _state;
                _state = old * Multiplier + _increment;

                uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
                int  rot        = (int)(old >> 59);
                return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
            }
        }

        public ulong NextULong()
        {
            unchecked
            {
                return ((ulong)NextUInt() << 32) | NextUInt();
            }
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1.0f / 16777216.0f);

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + NextFloat() * (max - min);

        /// <summary>
        /// Uniform int in [min, max). Rejection-sampled so the distribution is
        /// exactly uniform rather than modulo-biased.
        /// </summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;

            uint span      = (uint)(max - min);
            uint threshold = (uint)(-(int)span) % span; // 2^32 % span
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return min + (int)(r % span);
            }
        }

        public bool NextBool() => (NextUInt() & 1u) != 0u;

        public bool Chance(float probability) => NextFloat() < probability;

        /// <summary>Standard normal deviate via the Box–Muller transform.</summary>
        public float NextGaussian()
        {
            float u1 = Math.Max(NextFloat(), 1e-7f);
            float u2 = NextFloat();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        /// <summary>In-place Fisher–Yates shuffle.</summary>
        public void Shuffle<T>(System.Collections.Generic.IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
