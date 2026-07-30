// HydrologyStage.cs
// Derives the drainage network from the eroded surface: depression filling, D8
// flow routing, flow accumulation, rivers, lakes and a moisture field.
//
// Runs after erosion because it reads the weathered surface, and before landcover
// because moisture is the main input to classification.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strategos.Maps
{
    public sealed class HydrologyStage : IMapGenerationStage
    {
        public string Name => "Hydrology";

        /// <summary>Epsilon slope imposed across filled flats so routing always terminates.</summary>
        private const float FillEpsilon = 0.001f;

        /// <summary>Fill depth in metres above which a filled cell counts as a lake.</summary>
        private const float LakeDepth = 0.75f;

        public void Apply(MapGenContext ctx)
        {
            int w = ctx.Width, h = ctx.Height;
            int n = w * h;

            float[] elevation = ctx.Map.Elevation;
            FillDepressions(elevation, w, h, out float[] filled, out float[] standing);

            MarkWater(ctx, elevation, standing, w, h);

            ctx.FlowDirection    = ComputeFlowDirections(filled, w, h);
            ctx.FlowAccumulation = ComputeFlowAccumulation(filled, ctx.FlowDirection, w, h);
            ctx.Moisture         = ComputeMoisture(ctx, w, h);

            ExtractRivers(ctx, w, h);
        }

        // ─── Depression filling ───────────────────────────────────────────────

        /// <summary>
        /// Priority-flood (Barnes et al.), producing two surfaces in one sweep.
        ///
        /// Both are *separate* arrays — the real elevation keeps its basins. Flooding
        /// the actual terrain would erase every hollow and closed valley on the map,
        /// which is exactly the kind of feature that matters tactically.
        ///
        /// <paramref name="routing"/> is the epsilon variant: every cell is raised at
        /// least a hair above its predecessor, so no cell is flat with respect to its
        /// downstream neighbour and D8 routing always terminates.
        ///
        /// <paramref name="standing"/> is the same flood without the epsilon, and is
        /// the true water surface: a cell's value is the elevation of the lowest pass
        /// it can spill over. The two must not be confused. Epsilon accumulates along
        /// every gently-sloped path — a valley floor of a thousand cells ends up a
        /// metre above itself — so measuring lake depth against the routing surface
        /// floods every drainage line on the map and calls it a lake.
        ///
        /// The standing surface starts at negative infinity on the border rather than
        /// at the border's elevation, because a map is a window cut out of a landscape
        /// and its edges are not a rim. Seeded at their own height, any interior ground
        /// lower than the lowest edge cell counts as a closed basin and fills to the
        /// edge — which on hill and mountain maps, where the window's edges are usually
        /// high ground, drowns the valley floors under a lake with a dead flat top.
        /// Water leaves a real landscape at the edge of the sheet; here it does too.
        /// </summary>
        private static void FillDepressions(float[] elevation, int w, int h,
            out float[] routing, out float[] standing)
        {
            int n = w * h;

            // Locals rather than the out parameters directly: a local function may
            // not capture an out parameter at all, and Seed needs both surfaces.
            var fill  = new float[n];
            var water = new float[n];

            var closed = new bool[n];
            var heap   = new MinHeap(Mathf.Max(64, (w + h) * 2));

            void Seed(int x, int y)
            {
                int i = y * w + x;
                if (closed[i]) return;
                closed[i] = true;
                fill[i]  = elevation[i];
                water[i] = float.NegativeInfinity;
                heap.Push(fill[i], i);
            }

            for (int x = 0; x < w; x++) { Seed(x, 0); Seed(x, h - 1); }
            for (int y = 0; y < h; y++) { Seed(0, y); Seed(w - 1, y); }

            while (heap.TryPop(out float key, out int i))
            {
                int cx = i % w, cy = i / w;

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + MapGenContext.NeighbourDx[d];
                    int ny = cy + MapGenContext.NeighbourDy[d];
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;

                    int ni = ny * w + nx;
                    if (closed[ni]) continue;

                    closed[ni] = true;
                    fill[ni]  = Mathf.Max(elevation[ni], key + FillEpsilon);
                    water[ni] = Mathf.Max(elevation[ni], water[i]);
                    heap.Push(fill[ni], ni);
                }
            }

            routing  = fill;
            standing = water;
        }

        // ─── Standing water ───────────────────────────────────────────────────

        /// <summary>
        /// Marks sea (below the header's sea level) and lakes (cells standing water
        /// covers appreciably). Landcover is written here rather than in
        /// LandcoverStage because only hydrology knows where the water went.
        ///
        /// <paramref name="standing"/> must be the epsilon-free flood surface; see
        /// <see cref="FillDepressions"/> for what happens if it is not.
        /// </summary>
        private static void MarkWater(MapGenContext ctx, float[] elevation, float[] standing,
            int w, int h)
        {
            float seaLevel = ctx.Map.Header.SeaLevel;
            var   map      = ctx.Map;

            for (int i = 0; i < elevation.Length; i++)
            {
                bool isSea  = elevation[i] <= seaLevel;
                bool isLake = !isSea && (standing[i] - elevation[i]) > LakeDepth;

                if (isSea || isLake)
                    map.Landcover[i] = (byte)LandcoverClass.Water;
            }

            TraceCoastline(ctx, w, h);
        }

        /// <summary>
        /// Extracts the sea's edge as a polyline so the renderer can draw a shore
        /// line heavier than a lake edge. Only meaningful on coastal maps.
        /// </summary>
        private static void TraceCoastline(MapGenContext ctx, int w, int h)
        {
            var map = ctx.Map;
            if (map.Header.SeaLevel <= map.Header.MinElevation) return;

            var points = new List<Vector2>();
            float sea = map.Header.SeaLevel;

            // Sample the sea-level isoline coarsely; the contour renderer draws the
            // precise edge, this polyline only needs to carry the shore's identity.
            for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                float e = map.GetElevation(x, y);
                if (e > sea) continue;
                if (map.GetElevation(x + 1, y) > sea || map.GetElevation(x, y + 1) > sea)
                    points.Add(new Vector2(x, y));
            }

            if (points.Count >= 2)
                map.Lines.Add(new MapPolyline(MapLineKind.Coast, points, 1f, "Shore"));
        }

        // ─── Flow routing ─────────────────────────────────────────────────────

        /// <summary>
        /// Steepest-descent (D8) neighbour per cell, by drop divided by ground
        /// distance so diagonals are not unfairly favoured. 255 marks an outlet.
        /// </summary>
        private static byte[] ComputeFlowDirections(float[] filled, int w, int h)
        {
            var dirs = new byte[w * h];

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int   i    = y * w + x;
                float here = filled[i];
                float best = 0f;
                byte  bestDir = 255;

                for (int d = 0; d < 8; d++)
                {
                    int nx = x + MapGenContext.NeighbourDx[d];
                    int ny = y + MapGenContext.NeighbourDy[d];
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;

                    float slope = (here - filled[ny * w + nx]) / MapGenContext.NeighbourDistance[d];
                    if (slope > best)
                    {
                        best    = slope;
                        bestDir = (byte)d;
                    }
                }

                dirs[i] = bestDir;
            }

            return dirs;
        }

        /// <summary>
        /// Upslope contributing area, in cells. Processing in descending elevation
        /// order means every cell's own contribution is final before it is pushed
        /// downstream, so one pass suffices.
        /// </summary>
        private static float[] ComputeFlowAccumulation(float[] filled, byte[] dirs, int w, int h)
        {
            int n = w * h;
            var accumulation = new float[n];
            for (int i = 0; i < n; i++) accumulation[i] = 1f;

            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;

            var keys = new float[n];
            Array.Copy(filled, keys, n);
            Array.Sort(keys, order);
            // Array.Sort gives ascending; walk it backwards for descending.

            for (int k = n - 1; k >= 0; k--)
            {
                int i = order[k];
                byte d = dirs[i];
                if (d == 255) continue;

                int x = i % w, y = i / w;
                int nx = x + MapGenContext.NeighbourDx[d];
                int ny = y + MapGenContext.NeighbourDy[d];
                if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;

                accumulation[ny * w + nx] += accumulation[i];
            }

            return accumulation;
        }

        // ─── Moisture ─────────────────────────────────────────────────────────

        /// <summary>
        /// Per-cell wetness in 0–1, from proximity to standing water, drainage
        /// concentration, and the profile's aridity. LandcoverStage classifies
        /// against this rather than against elevation alone, which is what stops
        /// forest appearing uniformly at one altitude band.
        /// </summary>
        private static float[] ComputeMoisture(MapGenContext ctx, int w, int h)
        {
            int n = w * h;
            var moisture = new float[n];
            var distance = DistanceToWater(ctx, w, h);

            float maxAcc = 1f;
            for (int i = 0; i < n; i++)
                if (ctx.FlowAccumulation[i] > maxAcc) maxAcc = ctx.FlowAccumulation[i];
            float logMax = Mathf.Log(maxAcc + 1f);

            float dryness = ctx.Params.Aridity;
            const float wetRadiusCells = 40f;

            for (int i = 0; i < n; i++)
            {
                float drainage = Mathf.Log(ctx.FlowAccumulation[i] + 1f) / logMax;
                float nearWater = 1f - Mathf.Clamp01(distance[i] / wetRadiusCells);

                float wet = Mathf.Clamp01(drainage * 0.55f + nearWater * 0.45f);
                moisture[i] = Mathf.Clamp01(wet * (1f - dryness));
            }

            return moisture;
        }

        /// <summary>Multi-source BFS distance in cells to the nearest water cell.</summary>
        private static float[] DistanceToWater(MapGenContext ctx, int w, int h)
        {
            int n = w * h;
            var distance = new float[n];
            for (int i = 0; i < n; i++) distance[i] = float.MaxValue;

            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                if (ctx.Map.Landcover[i] != (byte)LandcoverClass.Water) continue;
                distance[i] = 0f;
                queue.Enqueue(i);
            }

            // No water anywhere: everything is equally far from it.
            if (queue.Count == 0)
            {
                for (int i = 0; i < n; i++) distance[i] = float.MaxValue;
                return distance;
            }

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w, y = i / w;

                for (int d = 0; d < 8; d++)
                {
                    int nx = x + MapGenContext.NeighbourDx[d];
                    int ny = y + MapGenContext.NeighbourDy[d];
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;

                    int ni = ny * w + nx;
                    float candidate = distance[i] + MapGenContext.NeighbourDistance[d];
                    if (candidate >= distance[ni]) continue;

                    distance[ni] = candidate;
                    queue.Enqueue(ni);
                }
            }

            return distance;
        }

        // ─── River extraction ─────────────────────────────────────────────────

        /// <summary>
        /// Turns the accumulation raster into polylines. Chains start at drainage
        /// heads and run downstream until they meet a channel already traced, so a
        /// trunk river is one polyline with tributaries joining it rather than a
        /// hundred overlapping copies of the same trunk.
        /// </summary>
        private static void ExtractRivers(MapGenContext ctx, int w, int h)
        {
            int n = w * h;
            var map = ctx.Map;

            float threshold = Mathf.Max(8f, ctx.Params.RiverThresholdFraction * n);
            float riverThreshold = threshold * 6f;

            var isChannel = new bool[n];
            for (int i = 0; i < n; i++)
            {
                isChannel[i] = ctx.FlowAccumulation[i] >= threshold &&
                               map.Landcover[i] != (byte)LandcoverClass.Water;
            }

            // A channel cell is a head if nothing upstream of it is also a channel.
            var hasUpstream = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (!isChannel[i]) continue;
                byte d = ctx.FlowDirection[i];
                if (d == 255) continue;

                int x = i % w, y = i / w;
                int nx = x + MapGenContext.NeighbourDx[d];
                int ny = y + MapGenContext.NeighbourDy[d];
                if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;

                if (isChannel[ny * w + nx]) hasUpstream[ny * w + nx] = true;
            }

            var traced = new bool[n];

            for (int start = 0; start < n; start++)
            {
                if (!isChannel[start] || hasUpstream[start] || traced[start]) continue;

                var points = new List<Vector2>();
                float peakAccumulation = 0f;

                int current = start;
                while (true)
                {
                    int x = current % w, y = current / w;
                    points.Add(new Vector2(x, y));
                    peakAccumulation = Mathf.Max(peakAccumulation, ctx.FlowAccumulation[current]);

                    bool alreadyTraced = traced[current];
                    traced[current] = true;

                    // Stop *after* including the junction cell so tributaries visibly
                    // meet the trunk instead of stopping one cell short of it.
                    if (alreadyTraced && points.Count > 1) break;

                    byte d = ctx.FlowDirection[current];
                    if (d == 255) break;

                    int nx = x + MapGenContext.NeighbourDx[d];
                    int ny = y + MapGenContext.NeighbourDy[d];
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) break;

                    int next = ny * w + nx;
                    if (!isChannel[next])
                    {
                        // Runs into standing water or off the map: include the outlet.
                        points.Add(new Vector2(nx, ny));
                        break;
                    }
                    current = next;
                }

                if (points.Count < 3) continue;

                var simplified = MapGeometry.Simplify(points, 0.7f);
                var smoothed   = MapGeometry.Chaikin(simplified, 1);

                map.Lines.Add(new MapPolyline(
                    peakAccumulation >= riverThreshold ? MapLineKind.River : MapLineKind.Stream,
                    smoothed,
                    Mathf.Clamp01(peakAccumulation / (riverThreshold * 4f))));
            }
        }
    }
}
