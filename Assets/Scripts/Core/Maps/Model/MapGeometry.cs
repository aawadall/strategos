// MapGeometry.cs
// Geometry helpers shared by generation, rendering and visibility.
// Cell space throughout: x east, y north, fractional coordinates allowed.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strategos.Maps
{
    public static class MapGeometry
    {
        /// <summary>
        /// Ramer–Douglas–Peucker simplification. Generated drainage and road
        /// networks come out one point per cell; without this a 512-cell map ships
        /// tens of thousands of collinear vertices that cost memory and draw time
        /// and change nothing on screen.
        /// </summary>
        public static List<Vector2> Simplify(IReadOnlyList<Vector2> points, float tolerance)
        {
            if (points == null || points.Count < 3) return new List<Vector2>(points ?? Array.Empty<Vector2>());

            var keep = new bool[points.Count];
            keep[0] = keep[points.Count - 1] = true;
            SimplifySegment(points, 0, points.Count - 1, tolerance, keep);

            var result = new List<Vector2>();
            for (int i = 0; i < points.Count; i++)
                if (keep[i]) result.Add(points[i]);
            return result;
        }

        private static void SimplifySegment(IReadOnlyList<Vector2> pts, int first, int last,
            float tolerance, bool[] keep)
        {
            if (last <= first + 1) return;

            float maxDist = -1f;
            int   maxIdx  = -1;

            for (int i = first + 1; i < last; i++)
            {
                float d = DistanceToSegment(pts[i], pts[first], pts[last]);
                if (d > maxDist) { maxDist = d; maxIdx = i; }
            }

            if (maxDist <= tolerance || maxIdx < 0) return;

            keep[maxIdx] = true;
            SimplifySegment(pts, first, maxIdx, tolerance, keep);
            SimplifySegment(pts, maxIdx, last,  tolerance, keep);
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-9f) return Vector2.Distance(p, a);

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Even-odd point-in-polygon test on an implicitly closed ring.</summary>
        public static bool PointInPolygon(IReadOnlyList<Vector2> ring, Vector2 p)
        {
            if (ring == null || ring.Count < 3) return false;

            bool inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                Vector2 pi = ring[i], pj = ring[j];
                if ((pi.y > p.y) != (pj.y > p.y) &&
                    p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y) + pi.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>Axis-aligned bounds of a point set, or a zero rect when empty.</summary>
        public static Rect Bounds(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0) return new Rect();

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                if (p.x < minX) minX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.x > maxX) maxX = p.x;
                if (p.y > maxY) maxY = p.y;
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// Walks the integer cells a straight line passes through (Bresenham).
        /// Used by line-of-sight and by stamping authored features into the grid.
        /// </summary>
        public static IEnumerable<Vector2Int> LineCells(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                yield return new Vector2Int(x0, y0);
                if (x0 == x1 && y0 == y1) yield break;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 <  dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>Total length of a polyline in cell units.</summary>
        public static float Length(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 2) return 0f;

            float total = 0f;
            for (int i = 1; i < points.Count; i++)
                total += Vector2.Distance(points[i - 1], points[i]);
            return total;
        }

        /// <summary>
        /// Smooths a polyline by Chaikin corner-cutting. One or two iterations turn
        /// the staircase of a cell-traced river into something that reads as drawn
        /// rather than rasterised.
        /// </summary>
        public static List<Vector2> Chaikin(IReadOnlyList<Vector2> points, int iterations = 1)
        {
            var current = new List<Vector2>(points);

            for (int it = 0; it < iterations; it++)
            {
                if (current.Count < 3) break;

                var next = new List<Vector2>(current.Count * 2) { current[0] };
                for (int i = 0; i < current.Count - 1; i++)
                {
                    Vector2 p = current[i], q = current[i + 1];
                    next.Add(p * 0.75f + q * 0.25f);
                    next.Add(p * 0.25f + q * 0.75f);
                }
                next.Add(current[current.Count - 1]);
                current = next;
            }

            return current;
        }
    }

    /// <summary>
    /// Minimal binary min-heap over (key, value) pairs.
    /// Priority-flood depression filling needs one and neither System.Collections
    /// nor Unity supplies a priority queue in this runtime profile.
    /// </summary>
    public sealed class MinHeap
    {
        private float[] _keys;
        private int[]   _values;
        private int     _count;

        public MinHeap(int capacity = 1024)
        {
            capacity = Mathf.Max(4, capacity);
            _keys    = new float[capacity];
            _values  = new int[capacity];
        }

        public int Count => _count;

        public void Push(float key, int value)
        {
            if (_count == _keys.Length) Grow();

            int i = _count++;
            _keys[i]   = key;
            _values[i] = value;

            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_keys[parent] <= _keys[i]) break;
                Swap(parent, i);
                i = parent;
            }
        }

        public bool TryPop(out float key, out int value)
        {
            if (_count == 0) { key = 0f; value = 0; return false; }

            key   = _keys[0];
            value = _values[0];

            _count--;
            if (_count > 0)
            {
                _keys[0]   = _keys[_count];
                _values[0] = _values[_count];

                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, smallest = i;
                    if (l < _count && _keys[l] < _keys[smallest]) smallest = l;
                    if (r < _count && _keys[r] < _keys[smallest]) smallest = r;
                    if (smallest == i) break;
                    Swap(i, smallest);
                    i = smallest;
                }
            }
            return true;
        }

        private void Grow()
        {
            Array.Resize(ref _keys,   _keys.Length * 2);
            Array.Resize(ref _values, _values.Length * 2);
        }

        private void Swap(int a, int b)
        {
            (_keys[a],   _keys[b])   = (_keys[b],   _keys[a]);
            (_values[a], _values[b]) = (_values[b], _values[a]);
        }
    }
}
