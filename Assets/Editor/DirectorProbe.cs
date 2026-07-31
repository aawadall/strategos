// DirectorProbe.cs
// Verifies that a side left to itself does something, and that it stops.
//
// The assertion worth having is the whole-scenario one: run the shipped skirmish with no
// player input at all and require it to REACH A DECISION. That is the difference between a
// simulation you watch and a game, and nothing smaller demonstrates it — every part can behave
// correctly while the scenario still sits still for an hour.
//
// Menu:  Strategos > Probe Director
// Batch: -executeMethod Strategos.Editor.DirectorProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Objectives;
using Strategos.Reactions;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class DirectorProbe
    {
        [MenuItem("Strategos/Probe Director")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckObjectivesAreReachable(log);
            bad += CheckUnattendedScenarioDecides(log);
            bad += CheckOnlyDirectedSidesAreOrdered(log);
            bad += CheckSpentUnitsAreNotSentBack(log);
            bad += CheckDeterminism(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[DirectorProbe]\n" + log);
            else Debug.LogError("[DirectorProbe]\n" + log);
        }

        // ─── Fixture ──────────────────────────────────────────────────────────

        /// <summary>The shipped skirmish, with both sides directed unless told otherwise.</summary>
        private static Simulation NewUnattended(out Scenario scenario, params int[] directedSides)
        {
            scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;   // dominant cost, and nothing here needs it
            var map = scenario.GenerateMap();

            var sim = new Simulation(scenario, map);
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.EnableReactions();

            var sides = new List<SideId>();
            if (directedSides.Length == 0)
                foreach (var s in scenario.Sides) sides.Add(s.Id);
            else
                foreach (int v in directedSides) sides.Add(new SideId(v));

            sim.EnableDirector(sides);
            return sim;
        }

        /// <summary>
        /// Every objective must sit on ground a unit can actually occupy.
        /// </summary>
        /// <remarks>
        /// The failure is silent and total: a MoveTo to an unreachable cell fails on the tick
        /// it is issued, so the director finds the unit idle, reissues, and the scenario runs
        /// its full hour with nobody going anywhere. It looks like an opponent that will not
        /// move rather than like a coordinate on a lake.
        /// </remarks>
        private static int CheckObjectivesAreReachable(StringBuilder log)
        {
            int bad = 0;
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();
            var caps = UnitCatalogue.Default().Get(UnitCatalogue.InfantryMech);

            foreach (var objective in scenario.Objectives)
            {
                int ox = Mathf.Clamp(Mathf.RoundToInt(objective.Cell.x), 0, map.Width - 1);
                int oy = Mathf.Clamp(Mathf.RoundToInt(objective.Cell.y), 0, map.Height - 1);

                if (caps.CanEnter(map.GetLandcover(ox, oy), map.SampleSlopeDegrees(ox, oy)))
                {
                    log.AppendLine($"  objective '{objective.Name}' at ({ox},{oy}) is passable " +
                                   $"({map.GetLandcover(ox, oy)})");
                    continue;
                }

                // Name the nearest cell that would work, so the fix is a coordinate rather
                // than a search.
                var suggestion = new Vector2Int(-1, -1);
                for (int r = 1; r < 40 && suggestion.x < 0; r++)
                    for (int dy = -r; dy <= r && suggestion.x < 0; dy++)
                        for (int dx = -r; dx <= r && suggestion.x < 0; dx++)
                        {
                            if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                            int x = ox + dx, y = oy + dy;
                            if (x < 0 || y < 0 || x >= map.Width || y >= map.Height) continue;
                            if (caps.CanEnter(map.GetLandcover(x, y), map.SampleSlopeDegrees(x, y)))
                                suggestion = new Vector2Int(x, y);
                        }

                log.AppendLine($"  FAIL objective '{objective.Name}' at ({ox},{oy}) is " +
                               $"{map.GetLandcover(ox, oy)}, which nothing can occupy. " +
                               $"Nearest passable cell: {suggestion}");
                bad++;
            }

            return bad;
        }

        // ─── The one that matters ─────────────────────────────────────────────

        private static int CheckUnattendedScenarioDecides(StringBuilder log)
        {
            int bad = 0;
            var sim = NewUnattended(out var scenario);

            // Fire at will on both sides, so a meeting engagement is actually fought rather
            // than two forces walking past each other.
            foreach (var u in scenario.Units) u.Roe = RulesOfEngagement.FireAtWill;

            int limit = scenario.TimeLimitTicks > 0 ? scenario.TimeLimitTicks + 60 : 4000;
            while (sim.Tick < limit && !sim.IsOver) sim.Step();

            if (!sim.IsOver)
            {
                log.AppendLine($"  FAIL nobody won and nothing timed out after {sim.Tick} ticks");
                return bad + 1;
            }

            if (sim.Director.OrdersIssued == 0)
            {
                log.AppendLine("  FAIL the scenario ended without a single autonomous order");
                bad++;
            }

            // A draw at the time limit is a legitimate outcome, but if it is the *only* thing
            // that ever happens the opponent is not contesting anything.
            var outcome = sim.Victory.Outcome;
            bool contested = sim.ReportLog.Count > 0;

            if (!contested)
            {
                log.AppendLine("  FAIL nothing was ever reported; the sides never met");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  unattended: {outcome}  ({sim.Director.OrdersIssued} autonomous " +
                               $"order(s), {sim.Log.Count} total, {sim.ReportLog.Count} report(s))");
            return bad;
        }

        // ─── Ownership ────────────────────────────────────────────────────────

        /// <summary>
        /// A side that is not directed must be left entirely alone — that is what makes the
        /// player's own force theirs.
        /// </summary>
        private static int CheckOnlyDirectedSidesAreOrdered(StringBuilder log)
        {
            int bad = 0;
            var sim = NewUnattended(out var scenario, 2);   // OPFOR only
            var player = new SideId(1);

            sim.Step(400);

            foreach (var command in sim.Log.Entries)
            {
                var unit = sim.UnitOf(command.TargetUnit);
                if (unit != null && unit.Side == player)
                {
                    log.AppendLine($"  FAIL an undirected side was ordered: {command}");
                    bad++;
                    break;
                }
            }

            bool opposingMoved = false;
            foreach (var u in scenario.Units)
                if (u.Side != player && u.Posture == Posture.Moving) opposingMoved = true;

            if (!opposingMoved && sim.Director.OrdersIssued == 0)
            {
                log.AppendLine("  FAIL the directed side did nothing");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  ownership: {sim.Director.OrdersIssued} order(s) issued, none " +
                               "to the undirected side");
            return bad;
        }

        /// <summary>
        /// A unit that has broken contact must not be found idle and sent straight back.
        /// </summary>
        private static int CheckSpentUnitsAreNotSentBack(StringBuilder log)
        {
            int bad = 0;
            var sim = NewUnattended(out var scenario);

            var spent = scenario.Units[0];
            spent.Strength = ReactionController.BreakStrengthPercent - 5f;

            sim.Step(200);

            foreach (var command in sim.Log.Entries)
                if (command.TargetUnit == spent.Id && command.Kind == CommandKind.MoveTo)
                {
                    log.AppendLine($"  FAIL a unit below the break-contact threshold was " +
                                   $"ordered forward: {command}");
                    bad++;
                    break;
                }

            if (bad == 0)
                log.AppendLine($"  spent units: a unit at {spent.Strength:0}% was left alone");
            return bad;
        }

        // ─── Determinism ──────────────────────────────────────────────────────

        private static int CheckDeterminism(StringBuilder log)
        {
            int bad = 0;

            string first = RunUnattended(out int ordersA, out int tickA);
            string second = RunUnattended(out int ordersB, out int tickB);

            if (first != second)
            {
                log.AppendLine($"  FAIL two unattended runs diverged ({ordersA}/{tickA} vs " +
                               $"{ordersB}/{tickB})");
                bad++;
            }
            else
            {
                log.AppendLine($"  determinism: two unattended runs to t{tickA} with {ordersA} " +
                               "autonomous order(s) IDENTICAL");
            }

            return bad;
        }

        private static string RunUnattended(out int orders, out int tick)
        {
            var sim = NewUnattended(out var scenario);
            foreach (var u in scenario.Units) u.Roe = RulesOfEngagement.FireAtWill;

            sim.Step(600);
            orders = sim.Director.OrdersIssued;
            tick = sim.Tick;
            return sim.Signature();
        }
    }
}
#endif
