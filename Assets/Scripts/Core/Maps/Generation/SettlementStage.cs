// SettlementStage.cs
// Places towns and villages, and stamps their built-up footprints into landcover.
//
// Runs after landcover so it can refuse to build on rock, marsh or water, and
// before the road network, which exists only to connect what this stage places.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.Maps
{
    public sealed class SettlementStage : IMapGenerationStage
    {
        public string Name => "Settlements";

        private const int StreamId = 404;

        /// <summary>Candidate sites tried per settlement actually placed.</summary>
        private const int CandidatesPerSite = 24;

        public void Apply(MapGenContext ctx)
        {
            var map = ctx.Map;
            int w = ctx.Width, h = ctx.Height;

            int target = Mathf.RoundToInt(ctx.Params.SettlementsPerKiloCell * (w * h) / 1000f);
            if (target <= 0) return;

            var rng = ctx.StageRandom(StreamId);

            // Minimum spacing, so settlements distribute over the map instead of
            // clustering in the single most suitable valley.
            float minSpacing = Mathf.Max(12f, Mathf.Sqrt((float)(w * h) / target) * 0.55f);

            var placed = new List<MapPoi>();

            for (int s = 0; s < target; s++)
            {
                Vector2Int bestCell = default;
                float bestScore = float.MinValue;

                for (int c = 0; c < CandidatesPerSite; c++)
                {
                    int x = rng.Range(4, w - 4);
                    int y = rng.Range(4, h - 4);

                    float score = Suitability(ctx, x, y);
                    if (score <= 0f) continue;

                    // Reject anything too close to an existing settlement outright,
                    // rather than letting a high score override spacing.
                    if (TooClose(placed, x, y, minSpacing)) continue;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCell  = new Vector2Int(x, y);
                    }
                }

                if (bestScore <= float.MinValue) continue;

                var poi = new MapPoi(MapPoiKind.Village, new Vector2(bestCell.x, bestCell.y))
                {
                    ElevationMetres = map.GetElevation(bestCell.x, bestCell.y),
                    Name            = MapNames.Settlement(rng),
                };
                placed.Add(poi);
            }

            RankAndStamp(ctx, placed, rng);
        }

        /// <summary>
        /// Site quality in 0–1, or 0 for unbuildable. Flat, low, dry-but-watered
        /// ground scores best — the same criteria that put real villages where they
        /// are.
        /// </summary>
        private static float Suitability(MapGenContext ctx, int x, int y)
        {
            var map = ctx.Map;
            var cover = map.GetLandcover(x, y);

            if (cover == LandcoverClass.Water ||
                cover == LandcoverClass.Marsh ||
                cover == LandcoverClass.Snow  ||
                cover == LandcoverClass.Rock)
                return 0f;

            float slope = map.SampleSlopeDegrees(x, y);
            if (slope > 12f) return 0f;

            float flatness   = 1f - Mathf.Clamp01(slope / 12f);
            float lowland    = 1f - map.NormalisedElevation(map.GetElevation(x, y));
            float moisture   = ctx.Moisture != null ? ctx.Moisture[y * ctx.Width + x] : 0.4f;

            // Wanting water nearby but not standing in it: peaks around mid moisture.
            float watered = 1f - Mathf.Abs(moisture - 0.55f) / 0.55f;

            return flatness * 0.45f + lowland * 0.25f + Mathf.Clamp01(watered) * 0.30f;
        }

        private static bool TooClose(List<MapPoi> placed, int x, int y, float minSpacing)
        {
            float minSq = minSpacing * minSpacing;
            for (int i = 0; i < placed.Count; i++)
            {
                float dx = placed[i].Position.x - x;
                float dy = placed[i].Position.y - y;
                if (dx * dx + dy * dy < minSq) return true;
            }
            return false;
        }

        /// <summary>
        /// Assigns settlement class by rank and stamps the built-up area into
        /// landcover so the renderer and line-of-sight both see the town.
        /// </summary>
        private static void RankAndStamp(MapGenContext ctx, List<MapPoi> placed,
            DeterministicRandom rng)
        {
            if (placed.Count == 0) return;

            var map = ctx.Map;

            // Largest settlements sit on the best ground; ranking by suitability
            // keeps the city out of the mountains.
            placed.Sort((a, b) => Suitability(ctx, (int)b.Position.x, (int)b.Position.y)
                .CompareTo(Suitability(ctx, (int)a.Position.x, (int)a.Position.y)));

            int cityCount = Mathf.Max(1, placed.Count / 12);
            int townCount = Mathf.Max(1, placed.Count / 4);

            for (int i = 0; i < placed.Count; i++)
            {
                var poi = placed[i];

                if (i < cityCount)
                {
                    poi.Kind       = MapPoiKind.City;
                    poi.Population = rng.Range(40000, 180000);
                }
                else if (i < cityCount + townCount)
                {
                    poi.Kind       = MapPoiKind.Town;
                    poi.Population = rng.Range(4000, 30000);
                }
                else
                {
                    poi.Kind       = MapPoiKind.Village;
                    poi.Population = rng.Range(200, 2500);
                }

                map.Pois.Add(poi);
                StampBuiltUp(ctx, poi, rng);
            }
        }

        private static void StampBuiltUp(MapGenContext ctx, MapPoi poi, DeterministicRandom rng)
        {
            var map = ctx.Map;

            float radius = poi.Kind switch
            {
                MapPoiKind.City => 9f,
                MapPoiKind.Town => 5f,
                _               => 2.5f,
            };

            int cx = Mathf.RoundToInt(poi.Position.x);
            int cy = Mathf.RoundToInt(poi.Position.y);
            int r  = Mathf.CeilToInt(radius) + 1;

            // Irregular outline: a circle reads as a stamp, not a settlement.
            float lobe  = rng.Range(0.55f, 0.9f);
            float phase = rng.Range(0f, 6.2831853f);

            var ring = new List<Vector2>();

            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                int px = cx + x, py = cy + y;
                if (!ctx.InBounds(px, py)) continue;
                if (map.GetLandcover(px, py) == LandcoverClass.Water) continue;

                float distance = Mathf.Sqrt(x * x + y * y);
                float angle    = Mathf.Atan2(y, x);
                float limit    = radius * (1f + lobe * 0.25f * Mathf.Sin(angle * 3f + phase));

                if (distance <= limit)
                    map.SetLandcover(px, py, LandcoverClass.Urban);
            }

            // Coarse ring for the areal feature, at 16 points around the same lobed radius.
            for (int i = 0; i < 16; i++)
            {
                float angle = i / 16f * 6.2831853f;
                float limit = radius * (1f + lobe * 0.25f * Mathf.Sin(angle * 3f + phase));
                ring.Add(new Vector2(cx + Mathf.Cos(angle) * limit, cy + Mathf.Sin(angle) * limit));
            }

            map.Areas.Add(new MapPolygon(MapAreaKind.BuiltUp, ring, poi.Name));
        }
    }
}
