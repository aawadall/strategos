// LandcoverStage.cs
// Classifies every cell from elevation, slope and moisture.
//
// Runs after hydrology because moisture is its dominant input. Cells already
// marked Water are left alone — only hydrology knows where standing water went,
// and re-deriving it here from elevation would drown or drain lakes at random.

using UnityEngine;

namespace Strategos.Maps
{
    public sealed class LandcoverStage : IMapGenerationStage
    {
        public string Name => "Landcover";

        private const int StreamId = 303;

        /// <summary>Slope in degrees above which soil does not hold and rock shows.</summary>
        private const float RockSlopeDegrees = 34f;

        public void Apply(MapGenContext ctx)
        {
            var map = ctx.Map;
            int w = ctx.Width, h = ctx.Height;

            var  p    = ctx.Params;
            var  rng  = ctx.StageRandom(StreamId);
            uint seed = (uint)ctx.Settings.Seed ^ 0x4D5A9Bu;

            // Patchiness field: without it, classification follows the moisture
            // gradient exactly and forest edges come out as smooth contour bands
            // instead of the ragged blocks a real landscape has.
            float patchScale = 1f / Mathf.Max(20f, p.FeatureScaleCells * 0.22f);
            float patchOffsetX = rng.Range(-2000f, 2000f);
            float patchOffsetY = rng.Range(-2000f, 2000f);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (map.Landcover[i] == (byte)LandcoverClass.Water) continue;

                float elevation  = map.Elevation[i];
                float normalised = map.NormalisedElevation(elevation);
                float slope      = map.SampleSlopeDegrees(x, y);
                float moisture   = ctx.Moisture != null ? ctx.Moisture[i] : 0.4f;

                float patch = MapNoise.Fbm(
                    x * patchScale + patchOffsetX,
                    y * patchScale + patchOffsetY,
                    seed, 4) * 0.5f + 0.5f;

                map.Landcover[i] = (byte)Classify(p, normalised, slope, moisture, patch);
            }

            AddSpotHeights(ctx, w, h);
        }

        private static LandcoverClass Classify(ReliefParameters p,
            float normalisedElevation, float slopeDegrees, float moisture, float patch)
        {
            if (normalisedElevation >= p.SnowlineFraction)
                return LandcoverClass.Snow;

            if (slopeDegrees >= RockSlopeDegrees || normalisedElevation >= p.TreelineFraction)
                return LandcoverClass.Rock;

            // Arid maps: everything that is not actively watered turns to sand.
            if (p.Aridity > 0.6f && moisture < 0.28f)
                return patch > 0.42f ? LandcoverClass.Sand : LandcoverClass.Open;

            // Marsh needs the combination of very wet and very flat; wet alone on a
            // slope is just good pasture.
            if (moisture > 0.72f && slopeDegrees < 2.5f && patch > 0.45f)
                return LandcoverClass.Marsh;

            // Forest probability rises with moisture and is broken up by the patch
            // field, so a wood has an edge rather than fading out.
            float forestScore = moisture * 0.75f + patch * 0.45f - 0.55f;
            if (forestScore > 0f && slopeDegrees < RockSlopeDegrees)
                return LandcoverClass.Forest;

            // Cultivation wants gentle ground and moderate water.
            if (slopeDegrees < 6f && moisture > 0.22f && patch < 0.52f)
                return LandcoverClass.Cropland;

            return LandcoverClass.Open;
        }

        /// <summary>
        /// Records local summits as spot-height POIs. These are what a topographic
        /// sheet labels, and later phases need named high ground to reason about.
        /// </summary>
        private static void AddSpotHeights(MapGenContext ctx, int w, int h)
        {
            var map = ctx.Map;

            // A summit must dominate a neighbourhood this wide, in cells. Scaled to
            // the map so a small map does not fill up with trivial bumps.
            int radius = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(w, h) * 0.045f), 4, 40);

            for (int y = radius; y < h - radius; y += radius)
            for (int x = radius; x < w - radius; x += radius)
            {
                float best = float.MinValue;
                int bestX = x, bestY = y;

                for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    float e = map.GetElevation(x + dx, y + dy);
                    if (e <= best) continue;
                    best  = e;
                    bestX = x + dx;
                    bestY = y + dy;
                }

                // Only keep it if the local maximum is genuinely inside this window,
                // otherwise adjacent windows all label the same ridge.
                if (bestX < x - radius / 2 || bestX > x + radius / 2 ||
                    bestY < y - radius / 2 || bestY > y + radius / 2)
                    continue;

                if (map.GetLandcover(bestX, bestY) == LandcoverClass.Water) continue;

                map.Pois.Add(new MapPoi(MapPoiKind.SpotHeight, new Vector2(bestX, bestY))
                {
                    ElevationMetres = best,
                });
            }
        }
    }
}
