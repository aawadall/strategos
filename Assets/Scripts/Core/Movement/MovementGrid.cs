// MovementGrid.cs
// What one unit type can do on one map, precomputed per cell.
//
// The pathfinder needs three questions answered per cell — can I be here, how fast, and can I
// cross into the next one — and answering them from MapData's vector features on every visit
// would mean rasterising the road and river networks thousands of times per search. They are
// rasterised once here instead.
//
// Per capability, not per unit: two mechanised companies share a grid. Building one is
// proportional to the map area plus the feature count, so it is worth reusing and not worth
// caching cleverly.
//
// Nothing here is a rule of its own. Passability comes from UnitCapabilities.CanEnter and
// speed from UnitCapabilities.SpeedMps, so a wheeled unit and a foot unit disagree about the
// same ground because their capabilities do, not because this file knows about unit types.

using System.Collections.Generic;
using UnityEngine;
using Strategos.Maps;
using Strategos.Units;

namespace Strategos.Movement
{
    public sealed class MovementGrid
    {
        /// <summary>
        /// How close a bridge or ford has to be, in cells, to open a river crossing.
        ///
        /// A crossing is a point feature on a line that has been rasterised to whole cells, so
        /// the two rarely land on exactly the same cell. This is the tolerance that keeps a
        /// bridge actually usable.
        /// </summary>
        private const int CrossingRadius = 2;

        public readonly MapData Map;
        public readonly UnitCapabilities Capabilities;

        private readonly bool[] _passable;
        private readonly bool[] _road;

        /// <summary>
        /// Optional dynamic blockers (#34). Same instance as <see cref="World.WorldLayer"/> on
        /// the live Simulation — spawn/despawn update Passable without rebuilding the grid.
        /// </summary>
        private readonly World.WorldLayer _world;

        /// <summary>
        /// Cells occupied by a major watercourse, minus the ones a crossing opens.
        ///
        /// A COST, NOT A WALL. Blocking rivers outright was tried and is unworkable on this
        /// generator's output: a 256-cell map carries 126 river polylines against 21 fords and
        /// no bridges at all, so a hard block partitions the map into regions that cannot
        /// reach each other -- 90% of cells passable and no route between two of them.
        ///
        /// It also contradicts the project's own model: phases.md §4.1 lists "bridge vs. ford
        /// vs. wet gap crossing", three options rather than two. A unit can cross a river
        /// without a bridge; it is slow, and that slowness is exactly what makes a ford worth
        /// routing to.
        /// </summary>
        private readonly bool[] _barrier;

        /// <summary>
        /// Time multiplier for entering an unbridged watercourse cell. High enough that a
        /// detour of many cells to a ford wins, low enough that a crossing is possible when no
        /// ford exists.
        /// </summary>
        public const float WetCrossingPenalty = 15f;

        private readonly float[] _secondsToEnter;

        public int Width => Map.Width;
        public int Height => Map.Height;

        private MovementGrid(MapData map, UnitCapabilities caps, World.WorldLayer world)
        {
            Map = map;
            Capabilities = caps;
            _world = world;

            int n = map.Width * map.Height;
            _passable = new bool[n];
            _road = new bool[n];
            _barrier = new bool[n];
            _secondsToEnter = new float[n];
        }

        public static MovementGrid Build(MapData map, UnitCapabilities caps,
            World.WorldLayer world = null)
        {
            if (map == null || caps == null) return null;

            var grid = new MovementGrid(map, caps, world);
            grid.MarkRoads();
            grid.MarkBarriers();
            grid.MarkPassability();
            return grid;
        }

        // ─── Construction ─────────────────────────────────────────────────────

        /// <summary>
        /// Rasterises the road network. Anything a vehicle would follow counts; a trail helps a
        /// foot unit and not a tank, but that difference is already expressed by the two speeds
        /// in the capability rather than by which lines are roads.
        /// </summary>
        private void MarkRoads()
        {
            foreach (var line in Map.Lines)
            {
                if (line.Kind != MapLineKind.Highway && line.Kind != MapLineKind.Road &&
                    line.Kind != MapLineKind.Track && line.Kind != MapLineKind.Trail)
                    continue;

                StampPolyline(line.Points, _road);
            }
        }

        /// <summary>
        /// Rasterises the watercourses that stop a unit, then reopens the cells a bridge or
        /// ford covers.
        ///
        /// Rivers and canals block; streams do not. That is a judgement about this generator's
        /// output rather than about hydrology: the drainage network it produces is dense, and
        /// treating every stream as an obstacle would leave most of the map unreachable while
        /// modelling something a company would step across.
        /// </summary>
        private void MarkBarriers()
        {
            foreach (var line in Map.Lines)
            {
                if (line.Kind != MapLineKind.River && line.Kind != MapLineKind.Canal) continue;
                StampPolyline(line.Points, _barrier);
            }

            foreach (var poi in Map.Pois)
            {
                if (poi.Kind != MapPoiKind.Bridge && poi.Kind != MapPoiKind.Ford) continue;

                int cx = Mathf.RoundToInt(poi.Position.x);
                int cy = Mathf.RoundToInt(poi.Position.y);

                for (int dy = -CrossingRadius; dy <= CrossingRadius; dy++)
                for (int dx = -CrossingRadius; dx <= CrossingRadius; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (!InBounds(x, y)) continue;
                    _barrier[y * Width + x] = false;
                }
            }
        }

        private void MarkPassability()
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x;

                var cover = Map.GetLandcover(x, y);
                float slope = Map.SampleSlopeDegrees(x, y);

                // Watercourses do not affect passability -- see the note on _barrier. Only the
                // capability's own view of the landcover and the slope can make a cell
                // impassable.
                bool ok = Capabilities.CanEnter(cover, slope);
                _passable[i] = ok;

                if (!ok)
                {
                    _secondsToEnter[i] = float.PositiveInfinity;
                    continue;
                }

                float mps = Capabilities.SpeedMps(cover, slope, _road[i]);
                if (mps <= 0.0001f)
                {
                    _secondsToEnter[i] = float.PositiveInfinity;
                    continue;
                }

                float seconds = Map.Header.MetresPerCell / mps;
                if (_barrier[i]) seconds *= WetCrossingPenalty;
                _secondsToEnter[i] = seconds;
            }
        }

        private void StampPolyline(List<Vector2> points, bool[] into)
        {
            if (points == null || points.Count < 2) return;

            for (int i = 1; i < points.Count; i++)
            {
                int x0 = Mathf.RoundToInt(points[i - 1].x), y0 = Mathf.RoundToInt(points[i - 1].y);
                int x1 = Mathf.RoundToInt(points[i].x), y1 = Mathf.RoundToInt(points[i].y);

                foreach (var c in MapGeometry.LineCells(x0, y0, x1, y1))
                    if (InBounds(c.x, c.y)) into[c.y * Width + c.x] = true;
            }
        }

        // ─── Queries ──────────────────────────────────────────────────────────

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool Passable(int x, int y) =>
            InBounds(x, y) && _passable[y * Width + x] &&
            (_world == null || !_world.BlocksMovement(x, y));

        public bool OnRoad(int x, int y) => InBounds(x, y) && _road[y * Width + x];

        public bool IsBarrier(int x, int y) => InBounds(x, y) && _barrier[y * Width + x];

        /// <summary>
        /// Seconds to enter a cell, ignoring direction. Infinite when impassable.
        /// </summary>
        public float SecondsToEnter(int x, int y) =>
            InBounds(x, y) ? _secondsToEnter[y * Width + x] : float.PositiveInfinity;

        /// <summary>Counts of each overlay, for probes and diagnostics.</summary>
        public (int passable, int road, int barrier) Census()
        {
            int p = 0, r = 0, b = 0;
            for (int i = 0; i < _passable.Length; i++)
            {
                if (_passable[i]) p++;
                if (_road[i]) r++;
                if (_barrier[i]) b++;
            }
            return (p, r, b);
        }
    }
}
