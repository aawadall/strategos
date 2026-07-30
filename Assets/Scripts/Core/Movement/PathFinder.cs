// PathFinder.cs
// A* over the cell grid, minimising time rather than distance.
//
// Time, not distance, is what makes a road worth taking: a longer route on a highway beats a
// shorter one through forest precisely because the cost being minimised is how long it takes,
// and UnitCapabilities already expresses that as metres per second over each kind of going.
// Minimising distance and adding a road "bonus" would be the same idea expressed worse.
//
// DETERMINISM. Ties are broken by cell index, not by insertion order or anything derived from
// a hash, so the same search returns the same path on every machine and every run. A path is
// recomputed rather than stored in a save, so a replay that produced a different one would
// diverge silently — which is exactly what the divergence test in CommandProbe is there to
// catch.

using System.Collections.Generic;
using UnityEngine;
using Strategos.Maps;

namespace Strategos.Movement
{
    public static class PathFinder
    {
        /// <summary>
        /// Ceiling on cells examined, so an unreachable destination fails in bounded time
        /// rather than sweeping the whole map. A 512-cell map is 262 144 cells; this allows a
        /// generous search while still terminating on the pathological case.
        /// </summary>
        public const int MaxExpanded = 120000;

        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>Diagonal steps cross more ground and must cost more, or paths comb diagonally.</summary>
        private static readonly float[] StepScale =
            { 1f, 1f, 1f, 1f, 1.41421356f, 1.41421356f, 1.41421356f, 1.41421356f };

        public struct Result
        {
            public bool Found;
            public float Seconds;
            public int Expanded;

            /// <summary>Cells from start to goal inclusive. Empty when nothing was found.</summary>
            public List<Vector2Int> Cells;
        }

        public static Result Find(MovementGrid grid, Vector2Int start, Vector2Int goal)
        {
            var result = new Result { Cells = new List<Vector2Int>() };
            if (grid == null) return result;

            if (!grid.Passable(start.x, start.y) || !grid.Passable(goal.x, goal.y))
                return result;

            if (start == goal)
            {
                result.Found = true;
                result.Cells.Add(start);
                return result;
            }

            int w = grid.Width, h = grid.Height;
            int n = w * h;

            var cost = new float[n];
            var cameFrom = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { cost[i] = float.PositiveInfinity; cameFrom[i] = -1; }

            // The heuristic must never overestimate or A* stops being optimal. The fastest a
            // unit can possibly move is its road speed on level ground, so time-to-go is at
            // least straight-line distance divided by that.
            float bestSecondsPerCell = BestCaseSecondsPerCell(grid);

            var open = new MinHeap(1024);
            int startIndex = start.y * w + start.x;
            int goalIndex = goal.y * w + goal.x;

            cost[startIndex] = 0f;
            open.Push(Heuristic(start, goal, bestSecondsPerCell), startIndex);

            while (open.TryPop(out _, out int current))
            {
                if (closed[current]) continue;
                closed[current] = true;
                result.Expanded++;

                if (current == goalIndex)
                {
                    result.Found = true;
                    result.Seconds = cost[current];
                    Reconstruct(cameFrom, current, w, result.Cells);
                    return result;
                }

                if (result.Expanded >= MaxExpanded) break;

                int cx = current % w, cy = current / w;

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Dx[d], ny = cy + Dy[d];
                    if (!grid.Passable(nx, ny)) continue;

                    // Refuse to cut a diagonal between two blocked cells: a unit that can
                    // squeeze through the corner of a lake and a cliff is a pathfinding
                    // artefact, not a manoeuvre.
                    if (d >= 4 && (!grid.Passable(cx + Dx[d], cy) || !grid.Passable(cx, cy + Dy[d])))
                        continue;

                    int next = ny * w + nx;
                    if (closed[next]) continue;

                    float step = grid.SecondsToEnter(nx, ny) * StepScale[d];
                    if (float.IsInfinity(step)) continue;

                    float tentative = cost[current] + step;
                    if (tentative >= cost[next]) continue;

                    cost[next] = tentative;
                    cameFrom[next] = current;
                    open.Push(tentative + Heuristic(new Vector2Int(nx, ny), goal, bestSecondsPerCell),
                              next);
                }
            }

            return result;
        }

        /// <summary>
        /// The cheapest a single cell could possibly cost this unit, used to keep the
        /// heuristic admissible.
        /// </summary>
        private static float BestCaseSecondsPerCell(MovementGrid grid)
        {
            var caps = grid.Capabilities;
            float fastest = Mathf.Max(caps.RoadSpeedMps, caps.CrossCountrySpeedMps);
            if (fastest <= 0.0001f) return 0f;
            return grid.Map.Header.MetresPerCell / fastest;
        }

        private static float Heuristic(Vector2Int from, Vector2Int to, float secondsPerCell)
        {
            // Octile distance: the exact number of cells an 8-connected walk needs on open
            // ground, so the estimate is tight as well as admissible.
            int dx = Mathf.Abs(from.x - to.x);
            int dy = Mathf.Abs(from.y - to.y);
            int lo = Mathf.Min(dx, dy), hi = Mathf.Max(dx, dy);
            float cells = (hi - lo) + lo * 1.41421356f;
            return cells * secondsPerCell;
        }

        private static void Reconstruct(int[] cameFrom, int goal, int width, List<Vector2Int> into)
        {
            int current = goal;
            while (current >= 0)
            {
                into.Add(new Vector2Int(current % width, current / width));
                current = cameFrom[current];
            }
            into.Reverse();
        }

        /// <summary>
        /// Replaces runs of waypoints with straight segments wherever a straight line is
        /// passable and not appreciably more expensive.
        ///
        /// A* minimises cost cell by cell, and on a terrain field where neighbouring cells
        /// differ slightly it weaves constantly to shave fractions of a second — a 160-cell
        /// journey came out as 186 direction changes, which is a direction change per cell.
        /// That is optimal and it is also not how anything moves; it looks like a fault and it
        /// draws as a caterpillar.
        ///
        /// The tolerance is what keeps the smoothing honest. Straightening is only accepted
        /// when it costs no more than <see cref="SmoothingTolerance"/> above the weave it
        /// replaces, so a route still follows a road rather than cutting the corner off it,
        /// and still goes round a river rather than straight through.
        /// </summary>
        public static void Smooth(MovementGrid grid, List<Vector2Int> cells)
        {
            if (grid == null || cells == null || cells.Count < 3) return;

            var result = new List<Vector2Int> { cells[0] };
            int anchor = 0;

            while (anchor < cells.Count - 1)
            {
                // Furthest waypoint the anchor can reach directly. Walking back from the end
                // takes the longest legal straightening rather than the first one found.
                int best = anchor + 1;
                for (int probe = cells.Count - 1; probe > anchor + 1; probe--)
                {
                    if (!StraightRunIsAcceptable(grid, cells, anchor, probe)) continue;
                    best = probe;
                    break;
                }

                result.Add(cells[best]);
                anchor = best;
            }

            cells.Clear();
            cells.AddRange(result);
        }

        /// <summary>How much dearer a straight segment may be than the path it replaces.</summary>
        public const float SmoothingTolerance = 1.15f;

        private static bool StraightRunIsAcceptable(MovementGrid grid, List<Vector2Int> cells,
            int from, int to)
        {
            float direct = 0f;
            Vector2Int a = cells[from], b = cells[to];

            // Every cell the straight line touches must be enterable, or the unit would be
            // smoothed through a lake it was routed around.
            Vector2Int previous = a;
            foreach (var c in MapGeometry.LineCells(a.x, a.y, b.x, b.y))
            {
                if (!grid.Passable(c.x, c.y)) return false;
                if (c != a)
                {
                    bool diagonal = c.x != previous.x && c.y != previous.y;
                    direct += grid.SecondsToEnter(c.x, c.y) * (diagonal ? 1.41421356f : 1f);
                    if (float.IsInfinity(direct)) return false;
                }
                previous = c;
            }

            // Cost of the original waypoints being replaced.
            float original = 0f;
            for (int i = from + 1; i <= to; i++)
            {
                Vector2Int p = cells[i - 1], q = cells[i];
                bool diagonal = p.x != q.x && p.y != q.y;
                float legCells = Mathf.Max(Mathf.Abs(q.x - p.x), Mathf.Abs(q.y - p.y));
                original += grid.SecondsToEnter(q.x, q.y) * legCells * (diagonal ? 1.41421356f : 1f);
            }

            return direct <= original * SmoothingTolerance;
        }

        /// <summary>
        /// Drops waypoints that lie on a straight run, so a path across open ground is a few
        /// legs rather than one per cell. Purely cosmetic to the route taken — the cells
        /// removed are ones the unit passes through anyway — but it is the difference between
        /// an order that draws as a line and one that draws as a caterpillar.
        /// </summary>
        public static void Simplify(List<Vector2Int> cells)
        {
            if (cells == null || cells.Count < 3) return;

            var kept = new List<Vector2Int> { cells[0] };
            for (int i = 1; i < cells.Count - 1; i++)
            {
                Vector2Int a = kept[kept.Count - 1], b = cells[i], c = cells[i + 1];
                // Keep b only where the direction changes.
                if ((b.x - a.x) * (c.y - b.y) != (b.y - a.y) * (c.x - b.x)) kept.Add(b);
            }
            kept.Add(cells[cells.Count - 1]);

            cells.Clear();
            cells.AddRange(kept);
        }
    }
}
