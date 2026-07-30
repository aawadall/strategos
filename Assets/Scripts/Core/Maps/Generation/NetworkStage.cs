// NetworkStage.cs
// Builds the road network connecting settlements, by least-cost path over terrain.
//
// Last generated stage: it needs the finished surface (for slope), the finished
// landcover (for going around a marsh), and the settlements (for endpoints).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strategos.Maps
{
    public sealed class NetworkStage : IMapGenerationStage
    {
        public string Name => "Road network";

        private const int StreamId = 505;

        /// <summary>
        /// Extra connections beyond the minimum spanning tree, as a fraction of the
        /// settlement count. A pure MST reads as a river system, not a road network:
        /// real networks have loops.
        /// </summary>
        private const float ExtraEdgeFraction = 0.35f;

        public void Apply(MapGenContext ctx)
        {
            var settlements = new List<MapPoi>();
            foreach (var poi in ctx.Map.Pois)
            {
                if (poi.Kind == MapPoiKind.City ||
                    poi.Kind == MapPoiKind.Town ||
                    poi.Kind == MapPoiKind.Village)
                    settlements.Add(poi);
            }

            if (settlements.Count < 2) return;

            var rng   = ctx.StageRandom(StreamId);
            var cost  = BuildCostField(ctx);
            var usage = new int[ctx.CellCount];

            foreach (var (a, b) in ChooseConnections(settlements, rng))
            {
                var path = FindPath(ctx, cost, usage,
                    Cell(settlements[a]), Cell(settlements[b]));
                if (path == null || path.Count < 2) continue;

                // Reuse bonus: later routes prefer cells an earlier route already
                // used, so the network bundles into trunk roads instead of laying
                // a separate parallel track for every pair of towns.
                foreach (var cell in path) usage[cell.y * ctx.Width + cell.x]++;

                AddRoad(ctx, path, settlements[a], settlements[b]);
            }

            ClassifyByUsage(ctx, usage);
            AddCrossings(ctx);
        }

        private static Vector2Int Cell(MapPoi poi) =>
            new(Mathf.RoundToInt(poi.Position.x), Mathf.RoundToInt(poi.Position.y));

        // ─── Which settlements to connect ─────────────────────────────────────

        /// <summary>
        /// A Euclidean minimum spanning tree (Prim) plus a few short extra edges.
        /// The MST guarantees every settlement is reachable; the extras give the
        /// network its loops.
        /// </summary>
        private static List<(int, int)> ChooseConnections(List<MapPoi> settlements,
            DeterministicRandom rng)
        {
            int n = settlements.Count;
            var edges = new List<(int, int)>();

            var inTree   = new bool[n];
            var bestCost = new float[n];
            var bestFrom = new int[n];

            for (int i = 0; i < n; i++)
            {
                bestCost[i] = float.MaxValue;
                bestFrom[i] = -1;
            }

            inTree[0]   = true;
            bestCost[0] = 0f;
            for (int i = 1; i < n; i++)
            {
                bestCost[i] = Vector2.Distance(settlements[0].Position, settlements[i].Position);
                bestFrom[i] = 0;
            }

            for (int added = 1; added < n; added++)
            {
                int   next = -1;
                float min  = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (inTree[i] || bestCost[i] >= min) continue;
                    min  = bestCost[i];
                    next = i;
                }
                if (next < 0) break;

                inTree[next] = true;
                edges.Add((bestFrom[next], next));

                for (int i = 0; i < n; i++)
                {
                    if (inTree[i]) continue;
                    float d = Vector2.Distance(settlements[next].Position, settlements[i].Position);
                    if (d >= bestCost[i]) continue;
                    bestCost[i] = d;
                    bestFrom[i] = next;
                }
            }

            int extras = Mathf.RoundToInt(n * ExtraEdgeFraction);
            for (int e = 0; e < extras; e++)
            {
                int a = rng.Range(0, n);
                int b = rng.Range(0, n);
                if (a == b) continue;

                // Only short-range extras — a long-haul duplicate of an MST edge
                // just draws a second road beside the first.
                if (Vector2.Distance(settlements[a].Position, settlements[b].Position) > 140f) continue;
                edges.Add((a, b));
            }

            return edges;
        }

        // ─── Cost field ───────────────────────────────────────────────────────

        /// <summary>
        /// Per-cell traversal cost for road building. Water is impassable; slope
        /// dominates everything else, which is what puts roads in valleys and
        /// through passes rather than straight over crests.
        /// </summary>
        private static float[] BuildCostField(MapGenContext ctx)
        {
            int w = ctx.Width, h = ctx.Height;
            var cost = new float[w * h];

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var cover = ctx.Map.GetLandcover(x, y);

                if (cover == LandcoverClass.Water)
                {
                    cost[y * w + x] = float.PositiveInfinity;
                    continue;
                }

                float slope = ctx.Map.SampleSlopeDegrees(x, y);
                float c = 1f + slope * slope * 0.05f;

                c *= cover switch
                {
                    LandcoverClass.Marsh    => 6f,
                    LandcoverClass.Rock     => 4f,
                    LandcoverClass.Snow     => 3f,
                    LandcoverClass.Forest   => 1.6f,
                    LandcoverClass.Urban    => 0.7f, // already built through
                    LandcoverClass.Cropland => 1.1f,
                    _                       => 1f,
                };

                cost[y * w + x] = c;
            }

            return cost;
        }

        // ─── Pathfinding ──────────────────────────────────────────────────────

        /// <summary>
        /// A* over the cost field. A* rather than Dijkstra because the network needs
        /// one search per connection and a 512² Dijkstra per edge is needlessly slow.
        /// The heuristic is the straight-line distance at the minimum possible cell
        /// cost, so it never overestimates and the path stays optimal.
        /// </summary>
        private static List<Vector2Int> FindPath(MapGenContext ctx, float[] cost, int[] usage,
            Vector2Int start, Vector2Int goal)
        {
            int w = ctx.Width, h = ctx.Height;
            int n = w * h;

            if (!ctx.InBounds(start.x, start.y) || !ctx.InBounds(goal.x, goal.y)) return null;

            int startIndex = start.y * w + start.x;
            int goalIndex  = goal.y  * w + goal.x;

            var gScore = new float[n];
            var from   = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++)
            {
                gScore[i] = float.PositiveInfinity;
                from[i]   = -1;
            }

            var open = new MinHeap(1024);
            gScore[startIndex] = 0f;
            open.Push(Heuristic(start, goal), startIndex);

            while (open.TryPop(out _, out int current))
            {
                if (current == goalIndex) return Reconstruct(from, current, w);
                if (closed[current]) continue;
                closed[current] = true;

                int cx = current % w, cy = current / w;

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + MapGenContext.NeighbourDx[d];
                    int ny = cy + MapGenContext.NeighbourDy[d];
                    if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;

                    int ni = ny * w + nx;
                    if (closed[ni]) continue;

                    float cellCost = cost[ni];
                    if (float.IsPositiveInfinity(cellCost)) continue;

                    // Roads already here are cheap to widen and reuse.
                    if (usage[ni] > 0) cellCost *= 0.35f;

                    float tentative = gScore[current] + cellCost * MapGenContext.NeighbourDistance[d];
                    if (tentative >= gScore[ni]) continue;

                    gScore[ni] = tentative;
                    from[ni]   = current;
                    open.Push(tentative + Heuristic(new Vector2Int(nx, ny), goal), ni);
                }
            }

            return null; // unreachable — usually an island settlement
        }

        /// <summary>Straight-line distance at the cheapest possible cell cost of 1.</summary>
        private static float Heuristic(Vector2Int a, Vector2Int b)
        {
            float dx = a.x - b.x, dy = a.y - b.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static List<Vector2Int> Reconstruct(int[] from, int current, int w)
        {
            var path = new List<Vector2Int>();
            while (current >= 0)
            {
                path.Add(new Vector2Int(current % w, current / w));
                current = from[current];
            }
            path.Reverse();
            return path;
        }

        // ─── Emitting features ────────────────────────────────────────────────

        private static void AddRoad(MapGenContext ctx, List<Vector2Int> path,
            MapPoi a, MapPoi b)
        {
            var points = new List<Vector2>(path.Count);
            foreach (var cell in path) points.Add(new Vector2(cell.x, cell.y));

            var simplified = MapGeometry.Simplify(points, 1.1f);
            var smoothed   = MapGeometry.Chaikin(simplified, 1);

            // Class from the settlements it joins; refined afterwards by usage.
            MapLineKind kind =
                a.Kind == MapPoiKind.City || b.Kind == MapPoiKind.City ? MapLineKind.Road
                                                                      : MapLineKind.Track;

            ctx.Map.Lines.Add(new MapPolyline(kind, smoothed, 1f, $"{a.Name}-{b.Name}"));
        }

        /// <summary>
        /// Promotes the busiest routes to highways. Traffic is inferred from how
        /// many separate connections chose the same cells.
        /// </summary>
        private static void ClassifyByUsage(MapGenContext ctx, int[] usage)
        {
            foreach (var line in ctx.Map.Lines)
            {
                if (line.Kind != MapLineKind.Road && line.Kind != MapLineKind.Track) continue;

                int peak = 0;
                foreach (var point in line.Points)
                {
                    int x = Mathf.RoundToInt(point.x);
                    int y = Mathf.RoundToInt(point.y);
                    if (!ctx.InBounds(x, y)) continue;
                    peak = Math.Max(peak, usage[y * ctx.Width + x]);
                }

                line.Kind = peak >= 4 ? MapLineKind.Highway
                          : peak >= 2 ? MapLineKind.Road
                                      : MapLineKind.Track;
                line.Weight = Mathf.Clamp01(peak / 6f);
            }
        }

        /// <summary>
        /// Records a Ford or Bridge wherever a road meets a watercourse. These are
        /// choke points, so they need to exist as named features rather than as an
        /// incidental crossing of two drawn lines.
        /// </summary>
        private static void AddCrossings(MapGenContext ctx)
        {
            var map = ctx.Map;

            var waterCells = new HashSet<long>();
            foreach (var line in map.Lines)
            {
                if (line.Kind != MapLineKind.River && line.Kind != MapLineKind.Stream) continue;
                foreach (var point in line.Points)
                    waterCells.Add(Key(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y)));
            }

            if (waterCells.Count == 0) return;

            var seen = new HashSet<long>();

            foreach (var line in map.Lines)
            {
                bool isRoad = line.Kind == MapLineKind.Highway ||
                              line.Kind == MapLineKind.Road ||
                              line.Kind == MapLineKind.Track;
                if (!isRoad) continue;

                foreach (var point in line.Points)
                {
                    int x = Mathf.RoundToInt(point.x);
                    int y = Mathf.RoundToInt(point.y);

                    // Tolerate a cell of slack: both polylines were simplified and
                    // smoothed, so an exact cell match would almost never happen.
                    bool crosses = false;
                    for (int dy = -1; dy <= 1 && !crosses; dy++)
                    for (int dx = -1; dx <= 1 && !crosses; dx++)
                        crosses = waterCells.Contains(Key(x + dx, y + dy));

                    if (!crosses) continue;
                    if (!seen.Add(Key(x / 4, y / 4))) continue; // one crossing per 4-cell block

                    map.Pois.Add(new MapPoi(
                        line.Kind == MapLineKind.Highway ? MapPoiKind.Bridge : MapPoiKind.Ford,
                        new Vector2(x, y))
                    {
                        ElevationMetres = map.GetElevation(x, y),
                    });
                }
            }
        }

        private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
