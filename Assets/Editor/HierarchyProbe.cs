// HierarchyProbe.cs
// The ORBAT tree: that it is a tree, that state rolls up, and that an order to a formation
// reaches the troops.
//
// Menu:  Strategos > Probe Hierarchy
// Batch: -executeMethod Strategos.Editor.HierarchyProbe.Run
//
// THE FAILURE THIS EXISTS TO CATCH is double counting. Every consumer in Core enumerates units
// to answer "what can be seen, shot at, moved or counted". If a formation leaked into those
// lists, a battalion would be detected separately from its companies, engaged separately, and
// counted separately for victory — and none of it would look wrong. Strengths would simply be
// off, fights would last the wrong length of time, and there would be nothing to point at.
//
// So the first assertion is the boring one: Simulation.Units contains no formations, and the
// leaf count is what it should be. It is the assertion most likely to save an afternoon.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class HierarchyProbe
    {
        [MenuItem("Strategos/Probe Hierarchy")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = true;

            var scenario = ScenarioIO.Load(ScenarioSamples.SkirmishName);
            if (scenario == null)
            {
                Debug.LogError("[HierarchyProbe] no sample scenario — run " +
                               "Strategos > Write Sample Scenarios");
                Debug.Log("[HierarchyProbe] PROBE FAILED");
                return;
            }

            var map = scenario.GenerateMap();
            var sim = new Simulation(scenario, map, UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());

            PrintTree(sim, log);
            ok &= FormationsAreNotCombatants(sim, log);
            ok &= RollupMatchesSubordinates(sim, log);
            ok &= OrderToFormationReachesTroops(log);
            ok &= DecompositionIsDeterministic(log);

            Debug.Log(log.ToString());
            Debug.Log(ok ? "[HierarchyProbe] PROBE PASSED" : "[HierarchyProbe] PROBE FAILED");
        }

        // ─── The table ────────────────────────────────────────────────────────

        private static void PrintTree(Simulation sim, StringBuilder log)
        {
            var h = sim.Hierarchy;
            log.AppendLine($"  orbat: {h.All.Count} units, {h.Leaves.Count} fighting, " +
                           $"{h.Roots.Count} root(s)");

            foreach (var root in h.Roots) Print(h, root, log);
        }

        private static void Print(UnitHierarchy h, UnitInstance unit, StringBuilder log)
        {
            string indent = new(' ', 4 + h.DepthOf(unit.Id) * 3);
            bool formation = h.IsFormation(unit.Id);

            log.AppendLine($"{indent}{unit.Designation,-14} " +
                           $"{(formation ? "formation" : "fighting ")}  " +
                           $"str {h.StrengthOf(unit.Id),5:0.0}  " +
                           $"rdy {h.ReadinessOf(unit.Id),5:0.0}  " +
                           $"at {h.CellOf(unit.Id).x:0},{h.CellOf(unit.Id).y:0}");

            foreach (var child in h.SubordinatesOf(unit.Id)) Print(h, child, log);
        }

        // ─── Assertions ───────────────────────────────────────────────────────

        /// <summary>
        /// The one that matters: no formation may appear in the list everything fights from.
        /// </summary>
        private static bool FormationsAreNotCombatants(Simulation sim, StringBuilder log)
        {
            var h = sim.Hierarchy;

            foreach (var u in sim.Units)
                if (h.IsFormation(u.Id))
                {
                    log.AppendLine($"  combatants: FAILED, formation {u.Designation} is in " +
                                   "Simulation.Units — it will be detected, engaged and " +
                                   "counted alongside its own subordinates");
                    return false;
                }

            if (sim.Units.Count != h.Leaves.Count)
            {
                log.AppendLine($"  combatants: FAILED, {sim.Units.Count} in Units but " +
                               $"{h.Leaves.Count} leaves");
                return false;
            }

            if (sim.AllUnits.Count <= sim.Units.Count)
            {
                log.AppendLine("  combatants: FAILED, AllUnits is no larger than Units — the " +
                               "sample scenario has no formations, so this proves nothing");
                return false;
            }

            log.AppendLine($"  combatants: {sim.Units.Count} fighting of " +
                           $"{sim.AllUnits.Count} total, no formation among them  ok");
            return true;
        }

        private static bool RollupMatchesSubordinates(Simulation sim, StringBuilder log)
        {
            var h = sim.Hierarchy;

            foreach (var unit in sim.AllUnits)
            {
                if (!h.IsFormation(unit.Id)) continue;

                var leaves = new List<UnitInstance>();
                h.LeavesUnder(unit.Id, leaves);

                if (leaves.Count == 0)
                {
                    log.AppendLine($"  rollup: FAILED, formation {unit.Designation} has no " +
                                   "fighting units under it");
                    return false;
                }

                float expected = 0f;
                foreach (var l in leaves) expected += l.Strength;
                expected /= leaves.Count;

                if (Mathf.Abs(h.StrengthOf(unit.Id) - expected) > 0.001f)
                {
                    log.AppendLine($"  rollup: FAILED, {unit.Designation} rolled up to " +
                                   $"{h.StrengthOf(unit.Id):0.00}, expected {expected:0.00}");
                    return false;
                }

                // A formation's stored Strength is meaningless and must never be read; the
                // rollup is the answer. Asserted so nobody starts trusting the field.
                if (Mathf.Abs(unit.Strength - h.StrengthOf(unit.Id)) < 0.001f &&
                    leaves.Count > 1 && expected == 100f)
                {
                    // Not a failure — they legitimately coincide when everyone is full — but
                    // worth saying so the coincidence is not mistaken for the field working.
                    log.AppendLine($"  rollup: note, {unit.Designation}'s stored field happens " +
                                   "to match its rollup because every subordinate is at full " +
                                   "strength; the field is still not the source of truth");
                }
            }

            log.AppendLine("  rollup: every formation's strength is the mean of its troops  ok");
            return true;
        }

        /// <summary>
        /// An order to a battalion has to arrive in its companies' queues, one echelon per step.
        /// </summary>
        private static bool OrderToFormationReachesTroops(StringBuilder log)
        {
            var sim = Fresh();
            var formation = FirstFormation(sim);
            if (formation == null)
            {
                log.AppendLine("  decomposition: FAILED, no formation in the sample scenario");
                return false;
            }

            var subordinates = sim.Hierarchy.SubordinatesOf(formation.Id);

            sim.Issue(Command.MoveTo(ActorId.ForSide(formation.Side), formation.Id,
                new Vector2(120f, 120f)));

            // Two steps: one to deliver the directive and decompose it, one to deliver the
            // orders that came out of it.
            sim.Step(2);

            int reached = 0;
            foreach (var s in subordinates)
            {
                var q = sim.QueueOf(s.Id);
                if (q != null && !q.IsEmpty) reached++;
            }

            if (reached != subordinates.Count)
            {
                log.AppendLine($"  decomposition: FAILED, {reached} of {subordinates.Count} " +
                               $"subordinates of {formation.Designation} have orders");
                return false;
            }

            if (sim.QueueOf(formation.Id) != null)
            {
                log.AppendLine("  decomposition: FAILED, the formation itself has a queue — " +
                               "plan state must live in exactly one place");
                return false;
            }

            log.AppendLine($"  decomposition: one order to {formation.Designation} became " +
                           $"{reached} orders, {sim.Log.Count} in the log, formation holds " +
                           "no queue  ok");
            return true;
        }

        /// <summary>
        /// The property that keeps replay alive: decomposition must be identical every run.
        /// </summary>
        /// <remarks>
        /// Subordinate order comes from `UnitHierarchy`, which fixes it at construction from
        /// the scenario's own list. Dictionary iteration there would produce a different order
        /// of derived commands, different sequence numbers, and a divergence that surfaces
        /// long after the change that caused it.
        /// </remarks>
        private static bool DecompositionIsDeterministic(StringBuilder log)
        {
            string Signature()
            {
                // A FRESH scenario per run, not a fresh Simulation over the same one.
                // Simulation holds references to the scenario's UnitInstance objects and
                // mutates them, so a second run over one scenario starts from wherever the
                // first left off — which this probe reported as a divergence on its first
                // outing. CommandProbe.NewRealSim exists for the same reason.
                var sim = Fresh();
                sim.AddExecutor(new EngageExecutor());

                var formation = FirstFormation(sim);
                sim.Issue(Command.MoveTo(ActorId.ForSide(formation.Side), formation.Id,
                    new Vector2(120f, 120f)));
                sim.Step(40);
                return sim.Signature();
            }

            string a = Signature(), b = Signature();
            if (a != b)
            {
                log.AppendLine("  determinism: FAILED, two runs of the same decomposition " +
                               "diverged");
                return false;
            }

            log.AppendLine("  determinism: 40 ticks after a formation order IDENTICAL across " +
                           "runs  ok");
            return true;
        }

        /// <summary>A simulation over a scenario nothing else has touched.</summary>
        private static Simulation Fresh()
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            return sim;
        }

        private static UnitInstance FirstFormation(Simulation sim)
        {
            foreach (var u in sim.AllUnits)
                if (sim.Hierarchy.IsFormation(u.Id)) return u;
            return null;
        }
    }
}
#endif
