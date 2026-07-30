// ContourTracer.cs
// Extracts elevation isolines from MapData by marching squares.
//
// Contours are traced from the elevation grid rather than baked into it, because
// the interval is a presentation choice: the same map draws at 10 m for a valley
// fight and 50 m for a mountain one. Output is cell space, matching MapData
// indexing, so the 2D rasteriser and any future 3D overlay consume the same lines.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strategos.Maps
{
    /// <summary>
    /// One isoline elevation and the polylines that make it up. A single level is
    /// normally several disjoint lines — one per hill, plus open lines that run off
    /// the sheet edge.
    /// </summary>
    public sealed class ContourLevel
    {
        public float Metres;

        /// <summary>Every nth contour is drawn heavy and carries the elevation label.</summary>
        public bool IsIndex;

        /// <summary>Below sea level: drawn in the water ink rather than the earth ink.</summary>
        public bool IsSubmarine;

        public List<List<Vector2>> Lines = new();
    }

    public struct ContourOptions
    {
        /// <summary>Contour spacing in metres. 0 or less takes the map header's value.</summary>
        public float IntervalMetres;

        /// <summary>Index (heavy) contour every n levels, counted from 0 m.</summary>
        public int IndexEvery;

        /// <summary>
        /// Douglas–Peucker tolerance in cells. Marching squares emits one vertex per
        /// cell edge crossing; a 512-cell map at 20 m runs to six figures of vertices
        /// before this, nearly all of them collinear.
        /// </summary>
        public float SimplifyTolerance;

        /// <summary>Chaikin passes. One is enough to lose the cell-grid staircase.</summary>
        public int SmoothIterations;

        /// <summary>Drop lines shorter than this, in cells. Kills speckle on rough ground.</summary>
        public float MinLengthCells;

        public static ContourOptions Default => new ContourOptions
        {
            IntervalMetres    = 0f,
            IndexEvery        = 0,
            SimplifyTolerance = 0.22f,
            SmoothIterations  = 1,
            MinLengthCells    = 3f,
        };
    }

    public static class ContourTracer
    {
        /// <summary>
        /// Ceiling on how many levels one trace may produce. A caller that asks for a
        /// 0.5 m interval across 800 m of relief wants 1600 levels, which is neither
        /// legible nor affordable; clamping is preferable to grinding to a halt.
        /// </summary>
        public const int MaxLevels = 512;

        public static List<ContourLevel> Trace(MapData map) =>
            Trace(map, ContourOptions.Default);

        public static List<ContourLevel> Trace(MapData map, ContourOptions options)
        {
            var levels = new List<ContourLevel>();
            if (map?.Elevation == null || map.Width < 2 || map.Height < 2) return levels;

            var header = map.Header;

            float interval = options.IntervalMetres > 0f
                ? options.IntervalMetres
                : Mathf.Max(0.5f, header.ContourInterval);

            int indexEvery = options.IndexEvery > 0 ? options.IndexEvery
                                                   : Mathf.Max(1, header.IndexContourEvery);

            // Levels are absolute multiples of the interval, not offsets from the
            // map's own minimum, so the same ground carries the same contour value
            // on two maps that happen to have different extents.
            int firstK = Mathf.FloorToInt(header.MinElevation / interval) + 1;
            int lastK  = Mathf.FloorToInt(header.MaxElevation / interval);
            if (lastK < firstK) return levels;

            if (lastK - firstK + 1 > MaxLevels)
            {
                Debug.LogWarning(
                    $"ContourTracer: {lastK - firstK + 1} levels requested at {interval} m " +
                    $"over {header.MaxElevation - header.MinElevation:F0} m of relief; " +
                    $"clamped to {MaxLevels}.");
                lastK = firstK + MaxLevels - 1;
            }

            int levelCount = lastK - firstK + 1;
            var builders = new LevelBuilder[levelCount];
            for (int i = 0; i < levelCount; i++) builders[i] = new LevelBuilder();

            int w = map.Width, h = map.Height;
            float[] e = map.Elevation;

            // One pass over the grid, visiting each cell for only the levels that
            // actually cross it. Tracing level by level would re-read the whole
            // elevation array once per level instead.
            for (int y = 0; y < h - 1; y++)
            {
                int row = y * w;
                for (int x = 0; x < w - 1; x++)
                {
                    int i = row + x;
                    float v00 = e[i];
                    float v10 = e[i + 1];
                    float v01 = e[i + w];
                    float v11 = e[i + w + 1];

                    float min = v00, max = v00;
                    if (v10 < min) min = v10; else if (v10 > max) max = v10;
                    if (v11 < min) min = v11; else if (v11 > max) max = v11;
                    if (v01 < min) min = v01; else if (v01 > max) max = v01;
                    if (min == max) continue;

                    int kFrom = Mathf.FloorToInt(min / interval) + 1;
                    int kTo   = Mathf.FloorToInt(max / interval);
                    if (kFrom < firstK) kFrom = firstK;
                    if (kTo   > lastK)  kTo   = lastK;

                    for (int k = kFrom; k <= kTo; k++)
                        EmitCell(builders[k - firstK], x, y, w,
                            v00, v10, v11, v01, k * interval);
                }
            }

            for (int k = firstK; k <= lastK; k++)
            {
                var lines = builders[k - firstK].Build(
                    options.SimplifyTolerance, options.SmoothIterations, options.MinLengthCells);
                if (lines.Count == 0) continue;

                float metres = k * interval;
                levels.Add(new ContourLevel
                {
                    Metres      = metres,
                    IsIndex     = Mod(k, indexEvery) == 0,
                    IsSubmarine = metres < header.SeaLevel,
                    Lines       = lines,
                });
            }

            return levels;
        }

        // ─── Marching squares ─────────────────────────────────────────────────

        /// <summary>
        /// Emits the 0, 1 or 2 segments where <paramref name="level"/> crosses one
        /// cell. Corners are v00 at (x, y) counter-clockwise to v01 at (x, y+1);
        /// a corner exactly at the level counts as above, consistently, so two
        /// neighbouring cells never disagree about whether an edge is crossed.
        /// </summary>
        private static void EmitCell(LevelBuilder b, int x, int y, int w,
            float v00, float v10, float v11, float v01, float level)
        {
            int mask = 0;
            if (v00 >= level) mask |= 1;
            if (v10 >= level) mask |= 2;
            if (v11 >= level) mask |= 4;
            if (v01 >= level) mask |= 8;
            if (mask == 0 || mask == 15) return;

            // Crossings are keyed by the grid edge they sit on rather than by their
            // coordinates. Two cells sharing an edge derive the crossing from the
            // same pair of corner values, so the key is exact and the chains join
            // without a distance tolerance.
            int eBottom = y * w + x;              // horizontal edge (x, y)
            int eTop    = (y + 1) * w + x;        // horizontal edge (x, y+1)
            int eLeft   = HalfEdge + y * w + x;   // vertical edge   (x, y)
            int eRight  = HalfEdge + y * w + x + 1;

            var pBottom = new Vector2(x + Fraction(v00, v10, level), y);
            var pTop    = new Vector2(x + Fraction(v01, v11, level), y + 1);
            var pLeft   = new Vector2(x,     y + Fraction(v00, v01, level));
            var pRight  = new Vector2(x + 1, y + Fraction(v10, v11, level));

            switch (mask)
            {
                case 1:  case 14: b.Add(eLeft,   pLeft,   eBottom, pBottom); break;
                case 2:  case 13: b.Add(eBottom, pBottom, eRight,  pRight);  break;
                case 3:  case 12: b.Add(eLeft,   pLeft,   eRight,  pRight);  break;
                case 4:  case 11: b.Add(eRight,  pRight,  eTop,    pTop);    break;
                case 6:  case 9:  b.Add(eBottom, pBottom, eTop,    pTop);    break;
                case 7:  case 8:  b.Add(eLeft,   pLeft,   eTop,    pTop);    break;

                // Saddles. Which pair of corners the isoline wraps is genuinely
                // ambiguous from the corners alone; the bilinear centre decides it.
                // Guessing per-cell instead would let one contour cross itself.
                case 5:
                    if ((v00 + v10 + v11 + v01) * 0.25f >= level)
                    {
                        b.Add(eLeft,   pLeft,   eTop,   pTop);
                        b.Add(eBottom, pBottom, eRight, pRight);
                    }
                    else
                    {
                        b.Add(eLeft,  pLeft,  eBottom, pBottom);
                        b.Add(eRight, pRight, eTop,    pTop);
                    }
                    break;

                case 10:
                    if ((v00 + v10 + v11 + v01) * 0.25f >= level)
                    {
                        b.Add(eLeft,  pLeft,  eBottom, pBottom);
                        b.Add(eRight, pRight, eTop,    pTop);
                    }
                    else
                    {
                        b.Add(eBottom, pBottom, eRight, pRight);
                        b.Add(eTop,    pTop,    eLeft,  pLeft);
                    }
                    break;
            }
        }

        /// <summary>
        /// Offset separating vertical edge keys from horizontal ones. Large enough
        /// that no realistic grid collides: a map would need 2^30 cells.
        /// </summary>
        private const int HalfEdge = 1 << 30;

        /// <summary>
        /// Where <paramref name="level"/> falls between two corner values. A flat
        /// edge cannot be crossed by the caller's own test, but returning the
        /// midpoint rather than dividing by zero keeps a degenerate grid drawable.
        /// </summary>
        private static float Fraction(float a, float b, float level)
        {
            float span = b - a;
            if (Mathf.Abs(span) < 1e-6f) return 0.5f;
            return Mathf.Clamp01((level - a) / span);
        }

        private static int Mod(int value, int modulus)
        {
            if (modulus <= 0) return 0;
            int r = value % modulus;
            return r < 0 ? r + modulus : r;
        }

        // ─── Segment stitching ────────────────────────────────────────────────

        /// <summary>
        /// Accumulates one level's segments and chains them into polylines.
        ///
        /// Chaining matters beyond vertex count: a dash pattern, a smoothing pass and
        /// an elevation label all need to know where a line runs, and a bag of
        /// unordered two-point segments cannot tell them.
        /// </summary>
        private sealed class LevelBuilder
        {
            private readonly List<int> _from = new();
            private readonly List<int> _to   = new();
            private readonly Dictionary<int, Vector2> _points = new();

            public void Add(int edgeA, Vector2 pointA, int edgeB, Vector2 pointB)
            {
                if (edgeA == edgeB) return;

                _points[edgeA] = pointA;
                _points[edgeB] = pointB;
                _from.Add(edgeA);
                _to.Add(edgeB);
            }

            public List<List<Vector2>> Build(
                float simplifyTolerance, int smoothIterations, float minLengthCells)
            {
                var result = new List<List<Vector2>>();
                int n = _from.Count;
                if (n == 0) return result;

                var adjacency = new Dictionary<int, List<int>>(n * 2);
                for (int s = 0; s < n; s++)
                {
                    Link(adjacency, _from[s], s);
                    Link(adjacency, _to[s],   s);
                }

                var used = new bool[n];

                // Open lines first, from their loose ends. Starting mid-line would
                // split one contour running off the sheet into two.
                foreach (var entry in adjacency)
                {
                    if (entry.Value.Count != 1) continue;
                    int seed = entry.Value[0];
                    if (used[seed]) continue;
                    Collect(result, Walk(entry.Key, seed, used, adjacency),
                        simplifyTolerance, smoothIterations, minLengthCells);
                }

                // Whatever is left is closed: a hill or a basin.
                for (int s = 0; s < n; s++)
                {
                    if (used[s]) continue;
                    var chain = Walk(_from[s], s, used, adjacency);
                    if (chain.Count > 2) chain.Add(chain[0]);
                    Collect(result, chain,
                        simplifyTolerance, smoothIterations, minLengthCells);
                }

                return result;
            }

            private static void Link(Dictionary<int, List<int>> adjacency, int edge, int segment)
            {
                if (!adjacency.TryGetValue(edge, out var list))
                    adjacency[edge] = list = new List<int>(2);
                list.Add(segment);
            }

            /// <summary>
            /// Follows unused segments from one end, consuming them as it goes, until
            /// the line runs out of continuation or closes on itself.
            /// </summary>
            private List<Vector2> Walk(int startEdge, int startSegment,
                bool[] used, Dictionary<int, List<int>> adjacency)
            {
                var points = new List<Vector2> { _points[startEdge] };

                int edge    = startEdge;
                int segment = startSegment;

                while (true)
                {
                    used[segment] = true;

                    int next = _from[segment] == edge ? _to[segment] : _from[segment];
                    points.Add(_points[next]);
                    edge = next;

                    segment = -1;
                    if (adjacency.TryGetValue(edge, out var candidates))
                    {
                        for (int i = 0; i < candidates.Count; i++)
                        {
                            if (used[candidates[i]]) continue;
                            segment = candidates[i];
                            break;
                        }
                    }

                    if (segment < 0) return points;
                }
            }

            private static void Collect(List<List<Vector2>> result, List<Vector2> chain,
                float simplifyTolerance, int smoothIterations, float minLengthCells)
            {
                if (chain.Count < 2) return;
                if (MapGeometry.Length(chain) < minLengthCells) return;

                var line = simplifyTolerance > 0f
                    ? MapGeometry.Simplify(chain, simplifyTolerance)
                    : chain;

                if (smoothIterations > 0 && line.Count >= 3)
                    line = MapGeometry.Chaikin(line, smoothIterations);

                if (line.Count >= 2) result.Add(line);
            }
        }
    }
}
