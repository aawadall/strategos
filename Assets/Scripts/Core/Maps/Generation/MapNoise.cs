// MapNoise.cs
// Coherent noise for terrain generation.
//
// Mathf.PerlinNoise is deliberately NOT used: Unity does not contract its output
// to be stable across versions or platforms, so a seed that reproduces a map on a
// dev machine would not necessarily reproduce it in CI. Everything here is built
// on an integer hash and plain IEEE-754 float arithmetic, which is reproducible.

using System.Runtime.CompilerServices;
using UnityEngine;

namespace Strategos.Maps
{
    /// <summary>
    /// Gradient noise and the fractal combinators built on it. All functions are
    /// pure: output depends only on the arguments and the seed.
    /// </summary>
    public static class MapNoise
    {
        /// <summary>
        /// Eight unit-ish gradient directions. Using a fixed table rather than
        /// trigonometry keeps the noise bit-identical everywhere.
        /// </summary>
        private static readonly Vector2[] Gradients =
        {
            new( 1f,  0f), new(-1f,  0f), new( 0f,  1f), new( 0f, -1f),
            new( 0.7071068f,  0.7071068f), new(-0.7071068f,  0.7071068f),
            new( 0.7071068f, -0.7071068f), new(-0.7071068f, -0.7071068f),
        };

        // ─── Base noise ───────────────────────────────────────────────────────

        /// <summary>2D gradient (Perlin-style) noise, output roughly in [-1, 1].</summary>
        public static float Gradient2D(float x, float y, uint seed)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;

            float u = Fade(xf);
            float v = Fade(yf);

            float n00 = Dot(xi,     yi,     xf,        yf,        seed);
            float n10 = Dot(xi + 1, yi,     xf - 1f,   yf,        seed);
            float n01 = Dot(xi,     yi + 1, xf,        yf - 1f,   seed);
            float n11 = Dot(xi + 1, yi + 1, xf - 1f,   yf - 1f,   seed);

            float bottom = Mathf.Lerp(n00, n10, u);
            float top    = Mathf.Lerp(n01, n11, u);

            // Unit gradients cap the raw range near ±0.707; scale to about ±1.
            return Mathf.Lerp(bottom, top, v) * 1.4142136f;
        }

        // ─── Fractal combinators ──────────────────────────────────────────────

        /// <summary>
        /// Fractional Brownian motion: summed octaves at doubling frequency and
        /// halving amplitude. Produces rolling, hill-like relief. Range ≈ [-1, 1].
        /// </summary>
        public static float Fbm(float x, float y, uint seed, int octaves = 5,
            float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, norm = 0f;

            for (int i = 0; i < octaves; i++)
            {
                sum  += Gradient2D(x * frequency, y * frequency, seed + (uint)i * 1013u) * amplitude;
                norm += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>
        /// Ridged multifractal. Inverting and squaring each octave turns the smooth
        /// zero-crossings of fBm into sharp crests, which is what makes a mountain
        /// range read as a range rather than as lumps. Range ≈ [0, 1].
        /// </summary>
        public static float Ridged(float x, float y, uint seed, int octaves = 5,
            float lacunarity = 2f, float gain = 0.5f, float offset = 1f)
        {
            float sum = 0f, amplitude = 0.5f, frequency = 1f, norm = 0f, weight = 1f;

            for (int i = 0; i < octaves; i++)
            {
                float n = Gradient2D(x * frequency, y * frequency, seed + (uint)i * 2027u);
                n = offset - Mathf.Abs(n);
                n *= n;
                n *= weight;

                // Feeding the previous octave forward suppresses detail in the
                // valleys and concentrates it along the crests.
                weight = Mathf.Clamp01(n * 2f);

                sum  += n * amplitude;
                norm += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return norm > 0f ? Mathf.Clamp01(sum / norm) : 0f;
        }

        /// <summary>
        /// Billowy noise — absolute-valued fBm. Reads as rounded hills and dunes.
        /// Range ≈ [0, 1].
        /// </summary>
        public static float Billow(float x, float y, uint seed, int octaves = 5,
            float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, norm = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float n = Mathf.Abs(Gradient2D(x * frequency, y * frequency, seed + (uint)i * 3079u));
                sum  += n * amplitude;
                norm += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return norm > 0f ? Mathf.Clamp01(sum / norm) : 0f;
        }

        /// <summary>
        /// Displaces the sample point by another noise field before sampling.
        /// This is what stops fBm looking like woodgrain: warping bends the
        /// contours into the sinuous shapes real landforms have.
        /// </summary>
        public static float WarpedFbm(float x, float y, uint seed, float warpStrength,
            int octaves = 5)
        {
            float wx = Fbm(x + 5.2f, y + 1.3f, seed ^ 0x51F3A1u, 3);
            float wy = Fbm(x - 3.7f, y + 8.9f, seed ^ 0x9B27C5u, 3);
            return Fbm(x + wx * warpStrength, y + wy * warpStrength, seed, octaves);
        }

        /// <summary>Warped variant of <see cref="Ridged"/>.</summary>
        public static float WarpedRidged(float x, float y, uint seed, float warpStrength,
            int octaves = 5)
        {
            float wx = Fbm(x + 2.8f, y - 6.1f, seed ^ 0x1A77B3u, 3);
            float wy = Fbm(x - 9.4f, y + 4.5f, seed ^ 0xC4E19Du, 3);
            return Ridged(x + wx * warpStrength, y + wy * warpStrength, seed, octaves);
        }

        // ─── Internals ────────────────────────────────────────────────────────

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Dot(int gx, int gy, float dx, float dy, uint seed)
        {
            Vector2 g = Gradients[Hash(gx, gy, seed) & 7u];
            return g.x * dx + g.y * dy;
        }

        /// <summary>
        /// Integer hash of a lattice point. Avalanche constants from the
        /// MurmurHash3 finaliser family; chosen for mixing quality, not magic.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(int x, int y, uint seed)
        {
            unchecked
            {
                uint h = seed;
                h ^= (uint)x * 0x9E3779B1u;
                h ^= (uint)y * 0x85EBCA77u;
                h ^= h >> 15;
                h *= 0x2545F491u;
                h ^= h >> 13;
                h *= 0x9E3779B1u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
