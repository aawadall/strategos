// DrillProbe.cs
// Calling a drill: that it becomes orders, that a formation's drill reaches its troops, and
// that what could not be carried out is said rather than dropped.
//
// Menu:  Strategos > Probe Drills
// Batch: -executeMethod Strategos.Editor.DrillProbe.Run
//
// THE TABLE IS THE HONEST STATEMENT OF HOW MUCH DOCTRINE THE ENGINE ACTUALLY SPEAKS. Every
// step is one of three things — an order it issues, something the simulation does by itself,
// or a mechanic that does not exist — and the third column is the one worth watching. It
// should fall over time; if it rises, drills are being authored ahead of the engine.

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Doctrine;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class DrillProbe
    {
        [MenuItem("Strategos/Probe Drills")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = true;

            PrintCoverage(log);
            ok &= DrillBecomesOrders(log);
            ok &= BindingIsDirectional(log);
            ok &= FormationDrillReachesTroops(log);
            ok &= UnknownCodeIsReported(log);
            ok &= Deterministic(log);

            Debug.Log(log.ToString());
            Debug.Log(ok ? "[DrillProbe] PROBE PASSED" : "[DrillProbe] PROBE FAILED");
        }

        private static Simulation Fresh()
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.AddExecutor(new DefendExecutor());
            return sim;
        }

        // ─── The table ────────────────────────────────────────────────────────

        private static void PrintCoverage(StringBuilder log)
        {
            int orders = 0, inherent = 0, unmodelled = 0, total = 0;

            log.AppendLine("  how much of each drill the engine can carry out");
            log.AppendLine("    code  drill                          steps  orders  inherent  unmodelled");

            foreach (var d in TtpLibrary.All)
            {
                log.AppendLine($"    {d.Code,-6}{d.Name,-31}{d.Steps.Length,5}" +
                               $"{d.MechanisedSteps,8}{d.InherentSteps,10}{d.UnmodelledSteps,12}");
                orders += d.MechanisedSteps;
                inherent += d.InherentSteps;
                unmodelled += d.UnmodelledSteps;
                total += d.Steps.Length;
            }

            log.AppendLine($"    {"",-6}{"TOTAL",-31}{total,5}{orders,8}{inherent,10}{unmodelled,12}");
            log.AppendLine($"    {orders * 100 / total}% issue an order, " +
                           $"{inherent * 100 / total}% happen by themselves, " +
                           $"{unmodelled * 100 / total}% want a mechanic that does not exist");
        }

        // ─── Assertions ───────────────────────────────────────────────────────

        private static bool DrillBecomesOrders(StringBuilder log)
        {
            var sim = Fresh();
            var unit = sim.Units[0];
            var drill = TtpLibrary.Find("2");   // React to Contact

            int before = sim.Log.Count;
            sim.Issue(Command.Drill(ActorId.ForSide(unit.Side), unit.Id, "2"));

            // One step to deliver the drill and expand it, one to deliver what it became.
            sim.Step(2);

            var queue = sim.QueueOf(unit.Id);
            int queued = queue?.Count ?? 0;

            if (queued != drill.MechanisedSteps)
            {
                log.AppendLine($"  expansion: FAILED, drill 2 has {drill.MechanisedSteps} " +
                               $"issuable steps and produced {queued} queued order(s)");
                return false;
            }

            log.AppendLine($"  expansion: drill 2 became {queued} order(s), " +
                           $"{sim.Log.Count - before} entries in the log  ok");
            return true;
        }

        /// <summary>
        /// A drill binds against the threat, so its moves must point sensibly relative to it.
        /// </summary>
        /// <remarks>
        /// The assertion that catches a sign error, which is the mistake this binding invites
        /// and the one that would look like a unit fleeing an assault order.
        /// </remarks>
        private static bool BindingIsDirectional(StringBuilder log)
        {
            var sim = Fresh();
            var unit = sim.Units[0];

            // Break Contact: its bounds are away from the enemy.
            sim.Issue(Command.Drill(ActorId.ForSide(unit.Side), unit.Id, "3"));
            sim.Step(2);

            var queue = sim.QueueOf(unit.Id);
            if (queue == null || queue.IsEmpty)
            {
                log.AppendLine("  binding: FAILED, drill 3 queued nothing");
                return false;
            }

            // Distance to the nearest hostile, before and after each move step.
            var threat = NearestHostile(sim, unit);
            if (threat == null)
            {
                log.AppendLine("  binding: FAILED, no hostile to bind against");
                return false;
            }

            float now = Vector2.Distance(unit.Cell, threat.Cell);
            bool sawMove = false;

            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].Command.Kind != CommandKind.MoveTo) continue;
                sawMove = true;

                float to = Vector2.Distance(queue[i].Command.TargetCell, threat.Cell);
                if (to <= now)
                {
                    log.AppendLine($"  binding: FAILED, a Break Contact bound moves from " +
                                   $"{now:0.0} to {to:0.0} cells of the threat — the sign is " +
                                   "inverted and the unit is running at the enemy");
                    return false;
                }
            }

            if (!sawMove)
            {
                log.AppendLine("  binding: FAILED, drill 3 produced no movement to check");
                return false;
            }

            log.AppendLine($"  binding: Break Contact bounds open the range from {now:0.0} " +
                           "cells  ok");
            return true;
        }

        private static UnitInstance NearestHostile(Simulation sim, UnitInstance unit)
        {
            var side = sim.Scenario.FindSide(unit.Side);
            UnitInstance best = null;
            float bestSq = float.MaxValue;

            foreach (var other in sim.Units)
            {
                if (other.Id == unit.Id || other.IsDestroyed) continue;
                if (!Side.AreHostile(side, sim.Scenario.FindSide(other.Side))) continue;
                float sq = (other.Cell - unit.Cell).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = other; }
            }
            return best;
        }

        /// <summary>
        /// "2 Squad, React to Contact" has to reach the squads.
        /// </summary>
        private static bool FormationDrillReachesTroops(StringBuilder log)
        {
            var sim = Fresh();

            UnitInstance formation = null;
            foreach (var u in sim.AllUnits)
                if (sim.Hierarchy.IsFormation(u.Id)) { formation = u; break; }

            if (formation == null)
            {
                log.AppendLine("  formation: FAILED, no formation in the sample scenario");
                return false;
            }

            var subordinates = sim.Hierarchy.SubordinatesOf(formation.Id);
            sim.Issue(Command.Drill(ActorId.ForSide(formation.Side), formation.Id, "2"));

            // Three steps: decompose, expand, deliver.
            sim.Step(3);

            int reached = 0;
            foreach (var s in subordinates)
            {
                var q = sim.QueueOf(s.Id);
                if (q != null && !q.IsEmpty) reached++;
            }

            if (reached != subordinates.Count)
            {
                log.AppendLine($"  formation: FAILED, {reached} of {subordinates.Count} " +
                               $"subordinates of {formation.Designation} have orders");
                return false;
            }

            log.AppendLine($"  formation: one drill to {formation.Designation} reached all " +
                           $"{reached} subordinates  ok");
            return true;
        }

        /// <summary>
        /// A code nobody knows must be reported, not ignored.
        /// </summary>
        /// <remarks>
        /// The failure mode this guards is a mistyped code doing nothing at all, which is
        /// indistinguishable from a dropped click — and once codes are transmitted and can be
        /// garbled (#62), an unknown code is a thing that will genuinely happen.
        /// </remarks>
        private static bool UnknownCodeIsReported(StringBuilder log)
        {
            var sim = Fresh();
            var unit = sim.Units[0];

            int failures = 0;
            sim.Reports.Subscribe("probe", 300, r =>
            {
                if (r.Kind == Reports.ReportKind.OrderFailed && r.Source == unit.Id) failures++;
            });

            sim.Issue(Command.Drill(ActorId.ForSide(unit.Side), unit.Id, "NOSUCHCODE"));
            sim.Step(3);

            if (failures == 0)
            {
                log.AppendLine("  unknown code: FAILED, an unrecognised drill was silently " +
                               "ignored");
                return false;
            }

            var queue = sim.QueueOf(unit.Id);
            if (queue != null && !queue.IsEmpty)
            {
                log.AppendLine("  unknown code: FAILED, an unrecognised drill queued orders");
                return false;
            }

            log.AppendLine("  unknown code: reported as unable to comply, nothing queued  ok");
            return true;
        }

        private static bool Deterministic(StringBuilder log)
        {
            string Run()
            {
                var sim = Fresh();
                var unit = sim.Units[0];
                sim.Issue(Command.Drill(ActorId.ForSide(unit.Side), unit.Id, "1A"));
                sim.Step(60);
                return sim.Signature();
            }

            if (Run() != Run())
            {
                log.AppendLine("  determinism: FAILED, two identical drill runs diverged");
                return false;
            }

            log.AppendLine("  determinism: 60 ticks after a drill IDENTICAL across runs  ok");
            return true;
        }
    }
}
#endif
