// MoveToExecutor.cs
// Carries out a MoveTo order: route around what the unit cannot cross, then walk the route at
// the speed its capabilities allow over the ground it is on.
//
// #8a was a straight line, deliberately, to prove the chain from order to arrival while it was
// still trivial to debug. This is #8b: only the choice of *where to step next* changes. Speed,
// arrival tolerance, failure and the fixed-step rule are unchanged.
//
// FIXED STEP, NO WALL CLOCK. Distance moved is speed multiplied by the simulation's own
// seconds-per-tick, never Time.deltaTime.
//
// PATHS ARE DERIVED, NOT STORED. A route is recomputed from the unit's position, the target,
// the map and the capability, all of which a replay reproduces exactly, so caching one is safe
// and rebuilding it on replay yields the same cells. Nothing about a path is written into the
// command log.

using System.Collections.Generic;
using UnityEngine;
using Strategos.Maps;
using Strategos.Movement;
using Strategos.Units;

namespace Strategos.Commands
{
    public sealed class MoveToExecutor : ICommandExecutor, IRouteProvider
    {
        /// <summary>How close, in cells, counts as arrived at a waypoint or the destination.</summary>
        public const float ArrivalCells = 0.05f;

        /// <summary>
        /// Ticks a single move may run before being abandoned. A guard against a unit that
        /// cannot make progress holding the head of its queue for ever, which looks exactly
        /// like a pathfinding bug.
        /// </summary>
        public const int MaxTicks = 20000;

        public CommandKind Kind => CommandKind.MoveTo;

        /// <summary>A unit's current route and how far along it the unit is.</summary>
        private sealed class Route
        {
            public Vector2 Target;
            public List<Vector2Int> Cells;
            public int Index;
        }

        // Keyed by unit id. Lookup only — never iterated — so it cannot affect ordering.
        private readonly Dictionary<int, Route> _routes = new();

        // One grid per capability, not per unit: two mechanised companies path identically.
        private readonly Dictionary<string, MovementGrid> _grids = new();

        /// <summary>
        /// The route a unit is following, for drawing. Null when it has none.
        ///
        /// Exposed rather than recomputed by the view, so what is drawn is what is being
        /// walked — a second computation could disagree after a grid changed.
        /// </summary>
        public IReadOnlyList<Vector2Int> RouteOf(UnitId id) =>
            _routes.TryGetValue(id.Value, out var r) ? r.Cells : null;

        public CommandOutcome Step(UnitInstance unit, in QueuedCommand entry,
            ExecutionContext context)
        {
            var map = context.Map;
            if (map == null) return CommandOutcome.Failed;

            Vector2 target = entry.Command.TargetCell;

            if (Vector2.Distance(unit.Cell, target) <= ArrivalCells)
            {
                Arrive(unit, target);
                return CommandOutcome.Completed;
            }

            if (entry.TicksExecuting >= MaxTicks)
            {
                Halt(unit);
                return CommandOutcome.Failed;
            }

            var caps = unit.Capabilities(context.Catalogue);
            var grid = GridForCached(map, caps, context.World);

            var route = RouteFor(unit, target, grid);
            if (route == null)
            {
                // Nowhere to go: the destination is unreachable, or the unit is standing
                // somewhere it should not be. Failing is better than stalling.
                Halt(unit);
                return CommandOutcome.Failed;
            }

            // Advance past waypoints already reached; a fast unit can cross several in a tick.
            while (route.Index < route.Cells.Count &&
                   Vector2.Distance(unit.Cell, ToCell(route.Cells[route.Index])) <= ArrivalCells)
                route.Index++;

            if (route.Index >= route.Cells.Count)
            {
                Arrive(unit, target);
                _routes.Remove(unit.Id.Value);
                return CommandOutcome.Completed;
            }

            unit.Posture = Posture.Moving;

            // Budget in seconds, spent walking waypoints. Spending a whole tick's travel even
            // when it spans several short legs keeps speed honest around corners — otherwise a
            // twisty route is slower than a straight one of the same length for no reason a
            // player could see.
            float budget = context.SecondsPerTick;
            int guard = 0;

            while (budget > 0f && route.Index < route.Cells.Count && guard++ < 64)
            {
                Vector2 waypoint = ToCell(route.Cells[route.Index]);
                Vector2 delta = waypoint - unit.Cell;
                float remaining = delta.magnitude;

                if (remaining <= ArrivalCells) { route.Index++; continue; }

                // Speed comes from the ground being crossed, sampled where the unit is now, so
                // it slows entering forest rather than when it set off.
                int cx = Mathf.Clamp(Mathf.RoundToInt(unit.Cell.x), 0, map.Width - 1);
                int cy = Mathf.Clamp(Mathf.RoundToInt(unit.Cell.y), 0, map.Height - 1);
                float mps = caps.SpeedMps(map.GetLandcover(cx, cy),
                                          map.SampleSlopeDegrees(cx, cy),
                                          grid.OnRoad(cx, cy));

                if (mps <= 0.0001f) { Halt(unit); return CommandOutcome.Failed; }

                float cellsThisSlice = mps * budget / Mathf.Max(0.0001f, map.Header.MetresPerCell);

                if (cellsThisSlice >= remaining)
                {
                    unit.Cell = waypoint;
                    budget -= remaining * map.Header.MetresPerCell / mps;
                    route.Index++;
                }
                else
                {
                    unit.Cell += delta / remaining * cellsThisSlice;
                    budget = 0f;
                }
            }

            if (Vector2.Distance(unit.Cell, target) <= ArrivalCells)
            {
                Arrive(unit, target);
                _routes.Remove(unit.Id.Value);
                return CommandOutcome.Completed;
            }

            return CommandOutcome.Running;
        }

        // ─── Routing ──────────────────────────────────────────────────────────

        /// <summary>
        /// Pathfinder cells from <paramref name="from"/> to <paramref name="to"/> — the same
        /// Find → Simplify → Smooth chain the executor walks. Used by PLAY to preview pending
        /// and draft legs (#54) so the arrow cannot disagree with the march.
        /// </summary>
        /// <returns>Cell list excluding the start cell, or null if unreachable.</returns>
        public static List<Vector2Int> PlanCells(MovementGrid grid, Vector2Int from, Vector2Int to)
        {
            if (grid == null) return null;

            from = new Vector2Int(
                Mathf.Clamp(from.x, 0, grid.Width - 1),
                Mathf.Clamp(from.y, 0, grid.Height - 1));
            to = new Vector2Int(
                Mathf.Clamp(to.x, 0, grid.Width - 1),
                Mathf.Clamp(to.y, 0, grid.Height - 1));

            var found = PathFinder.Find(grid, from, to);
            if (!found.Found || found.Cells == null || found.Cells.Count == 0) return null;

            PathFinder.Simplify(found.Cells);
            PathFinder.Smooth(grid, found.Cells);

            // The unit is already standing on the first cell; keeping it would make the first
            // leg a step backwards to the cell centre.
            if (found.Cells.Count > 1) found.Cells.RemoveAt(0);
            return found.Cells;
        }

        /// <summary>
        /// Builds (or reuses) a movement grid for preview — same as the executor's cache key.
        /// </summary>
        public static MovementGrid GridFor(MapData map, UnitCapabilities caps,
            World.WorldLayer world = null)
        {
            if (map == null || caps == null) return null;
            return MovementGrid.Build(map, caps, world);
        }

        private Route RouteFor(UnitInstance unit, Vector2 target, MovementGrid grid)
        {
            if (_routes.TryGetValue(unit.Id.Value, out var existing) &&
                Vector2.Distance(existing.Target, target) <= ArrivalCells &&
                existing.Cells != null && existing.Cells.Count > 0)
                return existing;

            var from = new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(unit.Cell.x), 0, grid.Width - 1),
                Mathf.Clamp(Mathf.RoundToInt(unit.Cell.y), 0, grid.Height - 1));
            var to = new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(target.x), 0, grid.Width - 1),
                Mathf.Clamp(Mathf.RoundToInt(target.y), 0, grid.Height - 1));

            var cells = PlanCells(grid, from, to);
            if (cells == null) { _routes.Remove(unit.Id.Value); return null; }

            var route = new Route { Target = target, Cells = cells, Index = 0 };
            _routes[unit.Id.Value] = route;
            return route;
        }

        private MovementGrid GridForCached(MapData map, UnitCapabilities caps,
            World.WorldLayer world)
        {
            if (_grids.TryGetValue(caps.Id, out var grid) && grid.Map == map) return grid;
            grid = MovementGrid.Build(map, caps, world);
            _grids[caps.Id] = grid;
            return grid;
        }

        private static Vector2 ToCell(Vector2Int c) => new(c.x, c.y);

        private void Arrive(UnitInstance unit, Vector2 target)
        {
            unit.Cell = target;
            unit.Posture = Posture.Halted;
            _routes.Remove(unit.Id.Value);
        }

        private void Halt(UnitInstance unit)
        {
            unit.Posture = Posture.Halted;
            _routes.Remove(unit.Id.Value);
        }
    }
}
