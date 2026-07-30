// TectonicStage.cs
// Lays down the base landform. First stage of the pipeline; writes elevation only.

using UnityEngine;

namespace Strategos.Maps
{
    /// <summary>
    /// Builds the raw heightfield by blending warped fBm (rolling relief) with a
    /// warped ridged multifractal (crests and ranges), then adding a low-frequency
    /// regional trend so the whole map is not uniformly busy.
    ///
    /// This stage intentionally produces terrain that is *plausible but unweathered*
    /// — no valleys, no drainage. ErosionStage supplies those, which is why the two
    /// are separate.
    /// </summary>
    public sealed class TectonicStage : IMapGenerationStage
    {
        public string Name => "Tectonic";

        private const int StreamId = 101;

        public void Apply(MapGenContext ctx)
        {
            var p   = ctx.Params;
            var map = ctx.Map;
            int w = ctx.Width, h = ctx.Height;

            uint seed  = (uint)ctx.Settings.Seed;
            var  rng   = ctx.StageRandom(StreamId);
            float scale = 1f / Mathf.Max(1f, p.FeatureScaleCells);

            // Random offset so two maps with different seeds sample different parts
            // of the noise field rather than the same corner at a different phase.
            float ox = rng.Range(-4000f, 4000f);
            float oy = rng.Range(-4000f, 4000f);

            // Coast direction for profiles that have a sea.
            bool hasSea = p.SeaLevelFraction > 0f;
            int  coastEdge = rng.Range(0, 4); // 0 south, 1 west, 2 north, 3 east

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x * scale) + ox;
                    float ny = (y * scale) + oy;

                    float rolling = MapNoise.WarpedFbm(nx, ny, seed, p.WarpStrength, p.Octaves)
                                    * 0.5f + 0.5f;
                    float crests  = MapNoise.WarpedRidged(nx, ny, seed ^ 0x5BD1E995u,
                                        p.WarpStrength, p.Octaves);

                    float height = Mathf.Lerp(rolling, crests, p.RidgedWeight);

                    // Regional trend at a quarter of the landform frequency: the
                    // difference between "hills everywhere" and "an upland and a lowland".
                    float regional = MapNoise.Fbm(nx * 0.22f + 11.7f, ny * 0.22f - 4.3f,
                                         seed ^ 0x27D4EB2Fu, 3) * 0.5f + 0.5f;
                    height = height * 0.72f + regional * 0.28f;

                    if (hasSea)
                        height *= ShoreMask(x, y, w, h, coastEdge, nx, ny, seed);

                    map.Elevation[y * w + x] = p.BaseElevationMetres + height * p.ReliefMetres;
                }
            }

            map.RecalculateElevationBounds();

            // Sea level is stored as an absolute elevation so later stages never
            // have to re-derive it. Inland profiles are pushed below the map floor
            // so no cell can ever qualify as sea.
            map.Header.SeaLevel = hasSea
                ? p.BaseElevationMetres + p.SeaLevelFraction * p.ReliefMetres
                : map.Header.MinElevation - 1000f;
        }

        /// <summary>
        /// Ramps elevation down toward one edge so a coastline forms there. The ramp
        /// coordinate is perturbed by noise, otherwise the shore is a ruler-straight
        /// line across the map.
        /// </summary>
        private static float ShoreMask(int x, int y, int w, int h, int edge,
            float nx, float ny, uint seed)
        {
            float t = edge switch
            {
                0 => y / (float)(h - 1),          // sea to the south
                1 => x / (float)(w - 1),          // sea to the west
                2 => 1f - y / (float)(h - 1),     // sea to the north
                _ => 1f - x / (float)(w - 1),     // sea to the east
            };

            t += MapNoise.Fbm(nx * 0.55f, ny * 0.55f, seed ^ 0x7F4A7C15u, 4) * 0.18f;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.45f));
        }
    }
}
