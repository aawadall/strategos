// DirectiveProbe.cs
// Verifies the directive topic headlessly: a message from higher arrives one step late like
// everything else on a MessageBus<T>, never decomposes into orders on its own, addresses
// exactly one root under the player's side, is answerable without touching the log it came in
// on, replays byte-identical, and leaves unattended play completely alone.
//
// THE ONE THAT MATTERS MOST is CheckNoAutoDecomposition — the literal acceptance criterion from
// #73 stated as a negative: "must not decompose automatically into unit orders, or the player
// has nothing to do." A directive that silently became orders is indistinguishable from a bug
// until someone notices the player was never asked. This project's recurring defect is a check
// that passes while being unable to fail, so this one and CheckDeterminism were both observed
// FAILING against a deliberately broken build before being trusted — see
// Artifacts/agents/directives-core.md for the red/green transcript; that verification is not
// repeated here because a probe cannot assert its own history.
//
// Menu:  Strategos > Probe Directives
// Batch: -executeMethod Strategos.Editor.DirectiveProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Directives;
using Strategos.Reactions;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class DirectiveProbe
    {
        [MenuItem("Strategos/Probe Directives")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckLogAssignsSequence(log);
            bad += CheckNextStepDelivery(log);
            bad += CheckNoAutoDecomposition(log);
            bad += CheckAddressingResolves(log);
            bad += CheckResponseIsLoggedNotDestructive(log);
            bad += CheckDeterminism(log);
            bad += CheckUnattendedPlayIsUnaffected(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[DirectiveProbe]\n" + log);
            else Debug.LogError("[DirectiveProbe]\n" + log);
        }

        // ─── Fixture ──────────────────────────────────────────────────────────

        /// <summary>TF 1-7 IN — the only root under BLUFOR in the shipped skirmish.</summary>
        private const int BluforRoot = 7;

        private static Directive SkirmishDirective() => new()
        {
            Id = 1,
            TargetUnit = new UnitId(BluforRoot),
            From = "3 BDE",
            Intent = "Seize and hold the crossroads.",
            Constraints = "Do not become decisively engaged.",
            DeadlineTick = 3600,
            ObjectiveIds = new[] { 1 },
        };

        /// <summary>The skirmish, with one directive addressed to the BLUFOR root.</summary>
        private static Simulation NewWithDirective(out Scenario scenario)
        {
            scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;   // dominant generation cost, unneeded here
            scenario.Directive = SkirmishDirective();
            var map = scenario.GenerateMap();

            var sim = new Simulation(scenario, map);
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            return sim;
        }

        // ─── Log basics (brief §6 step 1's own verify note) ──────────────────

        /// <summary>
        /// A bare DirectiveLog assigns Seq the same way CommandLog and ReportLog do: 1, then 2,
        /// regardless of Id. Not in the brief's §4 table — this is the standalone check step 1
        /// asked for before any wiring existed, kept because it is cheap and it is the thing
        /// every other check silently assumes.
        /// </summary>
        private static int CheckLogAssignsSequence(StringBuilder log)
        {
            int bad = 0;
            var directiveLog = new DirectiveLog();

            var first = directiveLog.Append(new Directive { Id = 5, TargetUnit = new UnitId(1) });
            var second = directiveLog.Append(new Directive { Id = 9, TargetUnit = new UnitId(1) });

            if (first.Seq != 1)
            {
                log.AppendLine($"  FAIL first append got Seq {first.Seq}, expected 1");
                bad++;
            }
            if (second.Seq != 2)
            {
                log.AppendLine($"  FAIL second append got Seq {second.Seq}, expected 2");
                bad++;
            }
            if (directiveLog.Count != 2)
            {
                log.AppendLine($"  FAIL log has {directiveLog.Count} entries, expected 2");
                bad++;
            }
            // Id is author data and Append must not touch it — only Seq is the log's to assign.
            if (directiveLog[0].Id != 5 || directiveLog[1].Id != 9)
            {
                log.AppendLine("  FAIL Append changed Id, which is author-assigned scenario data");
                bad++;
            }

            if (bad == 0) log.AppendLine("  log basics: Seq assigned 1, 2; Id left untouched");
            return bad;
        }

        // ─── Rule 1: publish to the next step ────────────────────────────────

        private static int CheckNextStepDelivery(StringBuilder log)
        {
            int bad = 0;
            var sim = NewWithDirective(out _);

            if (sim.DirectiveLog.Count != 1)
            {
                log.AppendLine($"  FAIL {sim.DirectiveLog.Count} directive(s) logged at " +
                               "construction, expected 1");
                return bad + 1;
            }

            if (sim.Directives.PendingCount != 1)
            {
                log.AppendLine($"  FAIL {sim.Directives.PendingCount} directive(s) pending " +
                               "right after construction, expected 1 — it must be queued, not " +
                               "already visible to a subscriber");
                bad++;
            }

            var deliveries = new List<(int atTick, int publishedTick)>();
            sim.Directives.Subscribe("probe", 10, d => deliveries.Add((sim.Tick, d.Tick)));

            sim.Step();

            if (deliveries.Count != 1)
            {
                log.AppendLine($"  FAIL {deliveries.Count} directive(s) delivered after one " +
                               "step, expected 1");
                return bad + 1;
            }

            var (atTick, publishedTick) = deliveries[0];
            if (atTick != publishedTick + 1)
            {
                log.AppendLine($"  FAIL published at t{publishedTick}, delivered at t{atTick}, " +
                               $"expected t{publishedTick + 1}");
                bad++;
            }

            if (sim.Directives.PendingCount != 0)
            {
                log.AppendLine("  FAIL directive still pending after its delivery step");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  next-step delivery: published t{publishedTick}, " +
                               $"delivered t{atTick}");

            return bad;
        }

        // ─── No auto-decomposition — the one that matters most ──────────────

        /// <summary>
        /// Delivering a directive addressed to a formation must append nothing to CommandLog and
        /// must leave every subordinate's queue empty, with no player action taken. This is
        /// #73's own acceptance criterion stated as a negative, and it is the check that was
        /// deliberately observed FAILING before being trusted (see the file header).
        /// </summary>
        private static int CheckNoAutoDecomposition(StringBuilder log)
        {
            int bad = 0;
            var sim = NewWithDirective(out _);
            var directive = sim.DirectiveLog[0];

            var subordinates = sim.Hierarchy.SubordinatesOf(directive.TargetUnit);
            if (subordinates.Count == 0)
            {
                log.AppendLine($"  FAIL fixture: {directive.TargetUnit} has no subordinates — " +
                               "the ORBAT changed under the probe");
                return bad + 1;
            }

            sim.Step(20);   // well past delivery; nothing should act on this alone

            if (sim.Log.Count != 0)
            {
                log.AppendLine($"  FAIL CommandLog has {sim.Log.Count} entr" +
                               (sim.Log.Count == 1 ? "y" : "ies") +
                               " after a directive arrived with no player action — it decomposed");
                bad++;
            }

            foreach (var sub in subordinates)
            {
                var queue = sim.QueueOf(sub.Id);
                if (queue != null && !queue.IsEmpty)
                {
                    log.AppendLine($"  FAIL subordinate {sub.Designation} has a non-empty " +
                                   "queue after an unactioned directive");
                    bad++;
                }
            }

            if (bad == 0)
                log.AppendLine($"  no auto-decomposition: {directive.TargetUnit} " +
                               $"({subordinates.Count} subordinate(s)) held a directive for 20 " +
                               "ticks; CommandLog and every subordinate queue stayed empty");

            return bad;
        }

        // ─── Addressing ───────────────────────────────────────────────────────

        private static int CheckAddressingResolves(StringBuilder log)
        {
            int bad = 0;

            // Well-formed: exactly one root under PlayerSide, and the directive addresses it.
            var scenario = ScenarioSamples.Skirmish();
            scenario.Directive = SkirmishDirective();

            var problems = scenario.Validate();
            var directiveProblems = new List<string>();
            foreach (var p in problems)
                if (p.IndexOf("directive", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    directiveProblems.Add(p);

            foreach (var p in directiveProblems)
                log.AppendLine($"  FAIL well-formed fixture: {p}");
            bad += directiveProblems.Count;

            // Broken: a second BLUFOR root, directive still addressed to the first. Must be a
            // validation error, not a silent pick — there is no stated rule for which root
            // receives it (#36's own comment thread: the ORBAT is deliberately ragged).
            var broken = ScenarioSamples.Skirmish();
            broken.Directive = SkirmishDirective();

            var secondRoot = broken.FindUnit(new UnitId(2));   // 1/A/2-69 AR, parent 7 today
            if (secondRoot == null)
            {
                log.AppendLine("  FAIL fixture: unit 2 not found — the ORBAT changed under the probe");
                return bad + 1;
            }
            secondRoot.ParentId = UnitId.None;   // now a second root under BLUFOR

            var brokenProblems = broken.Validate();
            bool caught = false;
            foreach (var p in brokenProblems)
                if (p.IndexOf("root", System.StringComparison.OrdinalIgnoreCase) >= 0) caught = true;

            if (!caught)
            {
                log.AppendLine("  FAIL a second BLUFOR root with a directive present validated " +
                               "clean; expected a validation error, not a silent pick");
                bad++;
            }

            if (bad == 0)
                log.AppendLine("  addressing: single-root scenario validates clean; a second " +
                               "root under the directive's side is caught as a validation error");

            return bad;
        }

        // ─── Response: logged, not destructive ───────────────────────────────

        private static int CheckResponseIsLoggedNotDestructive(StringBuilder log)
        {
            int bad = 0;
            var sim = NewWithDirective(out _);
            sim.Step();   // deliver, so this exercises the same state a UI would read from

            var before = sim.DirectiveLog[0];
            int reportsBefore = sim.ReportLog.Count;

            sim.AcknowledgeDirective(before);

            var after = sim.DirectiveLog[0];
            if (!DirectiveEntryUnchanged(before, after))
            {
                log.AppendLine("  FAIL the directive entry differs before/after being " +
                               "acknowledged — the log was rewritten");
                bad++;
            }

            int reportsAfter = sim.ReportLog.Count;
            if (reportsAfter != reportsBefore + 1)
            {
                log.AppendLine($"  FAIL acknowledging appended {reportsAfter - reportsBefore} " +
                               "report(s), expected exactly 1");
                bad++;
            }
            else
            {
                var response = sim.ReportLog[reportsAfter - 1];
                if (response.Kind != ReportKind.DirectiveAcknowledged)
                {
                    log.AppendLine($"  FAIL appended report has kind {response.Kind}, expected " +
                                   "DirectiveAcknowledged");
                    bad++;
                }
                if (response.AboutDirective != before.Seq)
                {
                    log.AppendLine($"  FAIL response cites directive #{response.AboutDirective}, " +
                                   $"expected #{before.Seq}");
                    bad++;
                }
            }

            if (bad == 0)
                log.AppendLine($"  response: acknowledging #{before.Seq} appended exactly one " +
                               "DirectiveAcknowledged report citing it; the directive entry is " +
                               "byte-for-byte unchanged");

            return bad;
        }

        private static bool DirectiveEntryUnchanged(Directive a, Directive b) =>
            a.Id == b.Id && a.Seq == b.Seq && a.Tick == b.Tick && a.TargetUnit == b.TargetUnit &&
            a.From == b.From && a.Intent == b.Intent && a.Constraints == b.Constraints &&
            a.DeadlineTick == b.DeadlineTick;

        // ─── Determinism / replay ─────────────────────────────────────────────

        /// <summary>
        /// Two identical runs — same scenario, same scripted acknowledgement at the same tick —
        /// must produce byte-identical Simulation.Signature(). See the file header: this check
        /// was deliberately observed FAILING (two runs acknowledging at different ticks) before
        /// being trusted.
        /// </summary>
        private static int CheckDeterminism(StringBuilder log)
        {
            int bad = 0;
            const int ackTick = 5;

            string first = RunScripted(ackTick, out int directivesA, out int reportsA);
            string second = RunScripted(ackTick, out int directivesB, out int reportsB);

            if (first != second)
            {
                log.AppendLine("  FAIL two identical runs (same script, same ack tick) diverged");
                log.AppendLine($"    run A: {directivesA} directive(s), {reportsA} report(s)");
                log.AppendLine($"    run B: {directivesB} directive(s), {reportsB} report(s)");
                bad++;
            }
            else
            {
                log.AppendLine($"  determinism: two runs with a scripted acknowledgement at " +
                               $"t{ackTick} IDENTICAL ({directivesA} directive(s), {reportsA} " +
                               "report(s))");
            }

            return bad;
        }

        private static string RunScripted(int ackTick, out int directiveCount, out int reportCount)
        {
            var sim = NewWithDirective(out _);
            bool acknowledged = false;

            for (int t = 0; t < 30; t++)
            {
                sim.Step();
                if (!acknowledged && sim.Tick == ackTick)
                {
                    sim.AcknowledgeDirective(sim.DirectiveLog[0]);
                    acknowledged = true;
                }
            }

            directiveCount = sim.DirectiveLog.Count;
            reportCount = sim.ReportLog.Count;
            return sim.Signature();
        }

        // ─── Regression: unattended play is unaffected ───────────────────────

        /// <summary>
        /// The shipped skirmish, run unattended (both sides directed, mirroring
        /// DirectorProbe.NewUnattended), must reach the same decision in the same number of
        /// autonomous orders whether or not a directive is present and unanswered — #36 defers
        /// the AI that would read one, so today nothing may.
        /// </summary>
        private static int CheckUnattendedPlayIsUnaffected(StringBuilder log)
        {
            int bad = 0;

            RunUnattended(withDirective: false, out int ordersWithout, out int tickWithout);
            RunUnattended(withDirective: true, out int ordersWith, out int tickWith);

            if (ordersWith != ordersWithout)
            {
                log.AppendLine($"  FAIL adding a directive changed the autonomous order count " +
                               $"({ordersWithout} -> {ordersWith}) with nobody reading it");
                bad++;
            }

            if (tickWith != tickWithout)
            {
                log.AppendLine($"  FAIL adding a directive changed when the scenario decided " +
                               $"(t{tickWithout} -> t{tickWith})");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  unattended play: {ordersWithout} order(s), decided at " +
                               $"t{tickWithout}, identical with and without a directive present " +
                               "and unanswered");

            return bad;
        }

        private static void RunUnattended(bool withDirective, out int orders, out int tick)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            if (withDirective) scenario.Directive = SkirmishDirective();
            var map = scenario.GenerateMap();

            var sim = new Simulation(scenario, map);
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.EnableReactions();

            var sides = new List<SideId>();
            foreach (var s in scenario.Sides) sides.Add(s.Id);
            sim.EnableDirector(sides);

            foreach (var u in scenario.Units) u.Roe = RulesOfEngagement.FireAtWill;

            int limit = scenario.TimeLimitTicks > 0 ? scenario.TimeLimitTicks + 60 : 4000;
            while (sim.Tick < limit && !sim.IsOver) sim.Step();

            orders = sim.Director.OrdersIssued;
            tick = sim.Tick;
        }
    }
}
#endif
