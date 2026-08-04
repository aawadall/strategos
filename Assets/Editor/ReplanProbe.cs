// ReplanProbe.cs
// #35 / #272: mid-march HazardBlocking invalidates the MoveTo route; unit detours or fails
// without walking through the hazard cell.
//
// Menu:  Strategos > Probe Replan
// Batch: -executeMethod Strategos.Editor.ReplanProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Scenarios;
using Strategos.Units;
using Strategos.World;

namespace Strategos.Editor
{
    public static class ReplanProbe
    {
        private static readonly ActorId Blue = new(1);

        [MenuItem("Strategos/Probe Replan")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckDetourAroundHazard(log);
            bad += CheckFailWhenBoxedIn(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[ReplanProbe]\n" + log);
            else Debug.LogError("[ReplanProbe]\n" + log);
        }

        private static Simulation Fresh()
        {
            // PushNorth: small, erosion-off, open enough that a one-cell hazard has a detour.
            var scenario = ScenarioSamples.PushNorth();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();
            var sim = new Simulation(scenario, map, UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            return sim;
        }

        /// <summary>
        /// March toward a far cell, block one remaining route waypoint, assert the unit never
        /// occupies that cell and arrives via a detour.
        /// </summary>
        private static int CheckDetourAroundHazard(StringBuilder log)
        {
            var sim = Fresh();
            var unit = FirstLeaf(sim, Blue);
            if (unit == null)
            {
                log.AppendLine("  detour: FAILED — no blue leaf");
                return 1;
            }

            var target = new Vector2(sim.Map.Width - 8f, sim.Map.Height - 8f);
            sim.Issue(Command.MoveTo(Blue, unit.Id, target));

            IReadOnlyList<Vector2Int> route = null;
            for (int i = 0; i < 400 && (route == null || route.Count < 2); i++)
            {
                sim.Step();
                route = sim.RouteOf(unit.Id);
            }

            if (route == null || route.Count < 2)
            {
                log.AppendLine(
                    $"  detour: FAILED — no usable route after march start (count={route?.Count ?? 0})");
                return 1;
            }

            var unitRound = RoundCell(unit.Cell);
            Vector2Int hazard = default;
            bool picked = false;
            for (int i = 0; i < route.Count; i++)
            {
                var c = route[i];
                if (c == unitRound) continue;
                int manhattan = Mathf.Abs(c.x - unitRound.x) + Mathf.Abs(c.y - unitRound.y);
                if (manhattan < 2) continue;
                // Do not seal the destination itself — we want a detour, not a fail.
                if (Mathf.Abs(c.x - Mathf.RoundToInt(target.x)) +
                    Mathf.Abs(c.y - Mathf.RoundToInt(target.y)) <= 1)
                    continue;
                hazard = c;
                picked = true;
                if (i >= route.Count / 2) break;
            }

            if (!picked)
            {
                log.AppendLine("  detour: FAILED — no route cell far enough to block");
                return 1;
            }

            sim.SpawnWorldObject(WorldObjectKind.HazardBlocking, hazard, lifetimeTicks: -1);

            bool touchedHazard = false;
            bool arrived = false;

            for (int i = 0; i < 12000; i++)
            {
                sim.Step();
                if (RoundCell(unit.Cell) == hazard) touchedHazard = true;

                if (Vector2.Distance(unit.Cell, target) <= 1.5f)
                {
                    arrived = true;
                    break;
                }

                var q = sim.QueueOf(unit.Id);
                if (q == null || q.Count == 0) break;
            }

            if (touchedHazard)
            {
                log.AppendLine($"  detour: FAILED — unit occupied hazard cell {hazard}");
                return 1;
            }

            if (!arrived)
            {
                log.AppendLine(
                    $"  detour: FAILED — expected arrival around hazard {hazard}; " +
                    $"ended at {unit.Cell}");
                return 1;
            }

            log.AppendLine(
                $"  detour: OK — arrived at {unit.Cell} around hazard {hazard}");
            return 0;
        }

        /// <summary>
        /// Surround the destination with hazards so replan cannot reach it → Failed.
        /// </summary>
        private static int CheckFailWhenBoxedIn(StringBuilder log)
        {
            var sim = Fresh();
            var unit = FirstLeaf(sim, Blue);
            if (unit == null)
            {
                log.AppendLine("  boxed: FAILED — no blue leaf");
                return 1;
            }

            // Short hop so the first plan succeeds quickly.
            var target = unit.Cell + new Vector2(8f, 0f);
            target.x = Mathf.Clamp(target.x, 2f, sim.Map.Width - 3);
            target.y = Mathf.Clamp(target.y, 2f, sim.Map.Height - 3);
            var goal = RoundCell(target);

            sim.Issue(Command.MoveTo(Blue, unit.Id, target));
            for (int i = 0; i < 50; i++)
            {
                sim.Step();
                if (sim.RouteOf(unit.Id) != null) break;
            }

            // Ring of hazards around the goal (and the goal itself) so no path remains.
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var c = new Vector2Int(goal.x + dx, goal.y + dy);
                if (c.x < 0 || c.y < 0 || c.x >= sim.Map.Width || c.y >= sim.Map.Height)
                    continue;
                sim.SpawnWorldObject(WorldObjectKind.HazardBlocking, c, lifetimeTicks: -1);
            }

            bool touched = false;
            for (int i = 0; i < 4000; i++)
            {
                sim.Step();
                var cell = RoundCell(unit.Cell);
                if (cell == goal) touched = true;

                var q = sim.QueueOf(unit.Id);
                if (q == null || q.Count == 0) break;
            }

            var queue = sim.QueueOf(unit.Id);
            bool idle = queue == null || queue.Count == 0;
            bool atGoal = Vector2.Distance(unit.Cell, target) < 1.5f;

            if (touched || atGoal)
            {
                log.AppendLine("  boxed: FAILED — unit reached sealed goal");
                return 1;
            }

            if (!idle)
            {
                log.AppendLine("  boxed: FAILED — still has orders after replan should fail");
                return 1;
            }

            log.AppendLine($"  boxed: OK — MoveTo failed with goal sealed at {goal}");
            return 0;
        }

        private static UnitInstance FirstLeaf(Simulation sim, ActorId side)
        {
            var sideId = new SideId(side.Value);
            var hierarchy = new UnitHierarchy(sim.Units);
            foreach (var leaf in hierarchy.Leaves)
                if (leaf.Side == sideId && !leaf.IsDestroyed) return leaf;
            return null;
        }

        private static Vector2Int RoundCell(Vector2 cell) =>
            new(Mathf.RoundToInt(cell.x), Mathf.RoundToInt(cell.y));
    }
}
#endif
