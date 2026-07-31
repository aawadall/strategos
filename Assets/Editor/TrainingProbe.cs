// TrainingProbe.cs
// What proficiency actually costs a unit, measured rather than asserted.
//
// Menu:  Strategos > Probe Training
// Batch: -executeMethod Strategos.Editor.TrainingProbe.Run
//
// THE TABLE IS THE POINT, as it is for CombatProbe. The assertions below only catch the
// mechanism breaking; they cannot tell a defensible hesitation curve from an indefensible
// one, and the curve is the whole design. Read the numbers.
//
// This change moves every divergence baseline in the project — hesitation is part of
// CommandQueue's signature and delayed reports change the report log — so "the other probes
// still pass" is not evidence that this is right. They pass *differently*. What is asserted
// here is the property that must hold whatever the numbers are: a trained unit behaves
// exactly as it did before this existed, and a green one is slower by an amount that only
// ever costs time.

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Maps;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class TrainingProbe
    {
        [MenuItem("Strategos/Probe Training")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = true;

            PrintCurve(log);
            ok &= FullyTrainedIsUnchanged(log);
            ok &= GreenUnitStartsLate(log);
            ok &= GreenObserverReportsLate(log);
            ok &= HesitationRestartsOnPreempt(log);

            Debug.Log(log.ToString());
            Debug.Log(ok ? "[TrainingProbe] PROBE PASSED" : "[TrainingProbe] PROBE FAILED");
        }

        private static void PrintCurve(StringBuilder log)
        {
            log.AppendLine("  hesitation by training (ticks before an order is begun)");
            log.Append("    training ");
            foreach (int t in new[] { 100, 90, 80, 70, 60, 50, 40, 30, 20, 10, 0 })
                log.Append(t.ToString().PadLeft(5));
            log.AppendLine();
            log.Append("    ticks    ");
            foreach (int t in new[] { 100, 90, 80, 70, 60, 50, 40, 30, 20, 10, 0 })
                log.Append(new UnitInstance { Training = t }.HesitationTicks
                    .ToString().PadLeft(5));
            log.AppendLine();
        }

        // ─── Properties that must hold whatever the curve is ──────────────────

        /// <summary>
        /// A unit at 100 must behave exactly as it did before training existed.
        /// </summary>
        /// <remarks>
        /// The property that makes this change safe to land: every shipped scenario defaults
        /// to 100, so anything that moved in another probe's numbers is a real regression
        /// rather than this feature showing up. Without it, a divergence introduced here would
        /// be indistinguishable from the intended effect.
        /// </remarks>
        private static bool FullyTrainedIsUnchanged(StringBuilder log)
        {
            int hesitation = new UnitInstance { Training = 100f }.HesitationTicks;
            if (hesitation != 0)
            {
                log.AppendLine($"  trained baseline: FAILED, a unit at 100 hesitates " +
                               $"{hesitation} tick(s) — every existing scenario just changed");
                return false;
            }

            log.AppendLine("  trained baseline: a unit at 100 hesitates 0 ticks  ok");
            return true;
        }

        private static bool GreenUnitStartsLate(StringBuilder log)
        {
            var queue = new CommandQueue();
            queue.Enqueue(Command.MoveTo(new ActorId(1), new UnitId(1), new Vector2(5f, 5f)));

            const int hesitation = 3;
            int begunAt = -1;

            for (int tick = 0; tick < 10; tick++)
            {
                if (!queue.TryBegin(hesitation, out var entry)) continue;
                begunAt = tick;
                if (entry.Status != CommandStatus.Executing)
                {
                    log.AppendLine("  late start: FAILED, began without reaching Executing");
                    return false;
                }
                break;
            }

            if (begunAt != hesitation)
            {
                log.AppendLine($"  late start: FAILED, began on tick {begunAt}, expected " +
                               $"{hesitation}");
                return false;
            }

            log.AppendLine($"  late start: order held {hesitation} tick(s), began on " +
                           $"{begunAt}  ok");
            return true;
        }

        /// <summary>
        /// A green observer's contact must arrive late and carry when it was seen.
        /// </summary>
        /// <remarks>
        /// The staleness is the point, not the delay. `SituationReport` has always separated
        /// ObservedTick from Tick and Simulation.Report has always refused to overwrite the
        /// former; this is the first thing to actually use that, so it is worth asserting that
        /// a delayed contact still names the moment it was observed rather than the moment it
        /// arrived.
        /// </remarks>
        private static bool GreenObserverReportsLate(StringBuilder log)
        {
            var scenario = ScenarioIO.Load(ScenarioSamples.SkirmishName);
            if (scenario == null)
            {
                log.AppendLine("  late report: no sample scenario, skipped");
                return true;
            }

            // Assert the fixture before trusting the result. The first run of this probe
            // reported a lag of 0 and passed, because the shipped skirmish.json predates the
            // Training field and every unit deserialised to the default 100 — so the test was
            // measuring a fully trained scout and concluding nothing. A guard that skips when
            // the data is uninteresting is a guard that cannot fail.
            bool anyGreen = false;
            foreach (var u in scenario.Units) if (u.Training < 100f) anyGreen = true;

            if (!anyGreen)
            {
                log.AppendLine("  late report: FAILED, no unit in the sample scenario is " +
                               "below training 100 — the shipped JSON is stale, run " +
                               "Strategos > Write Sample Scenarios");
                return false;
            }

            var map = scenario.GenerateMap();
            var sim = new Simulation(scenario, map, UnitCatalogue.Default());

            SituationReport? firstContact = null;
            sim.Reports.Subscribe("probe", 200, r =>
            {
                if (firstContact == null && r.Kind == ReportKind.Contact) firstContact = r;
            });

            sim.Step(12);

            if (firstContact == null)
            {
                log.AppendLine("  late report: FAILED, no contact reported in 12 ticks");
                return false;
            }

            var report = firstContact.Value;
            int lag = report.Tick - report.ObservedTick;
            var observer = sim.UnitOf(report.Source);

            log.AppendLine($"  late report: {observer?.Designation} (training " +
                           $"{observer?.Training:0}) observed at T+{report.ObservedTick}, " +
                           $"published at T+{report.Tick}, lag {lag}");

            if (observer != null && observer.HesitationTicks > 0 && lag <= 0)
            {
                log.AppendLine("  late report: FAILED, a green observer reported instantly");
                return false;
            }

            if (observer != null && observer.HesitationTicks == 0 && lag != 0)
            {
                log.AppendLine("  late report: FAILED, a trained observer reported late");
                return false;
            }

            if (report.ObservedTick > report.Tick)
            {
                log.AppendLine("  late report: FAILED, observed after it was published");
                return false;
            }

            return true;
        }

        /// <summary>
        /// A displaced order must form up again rather than resuming instantly.
        /// </summary>
        /// <remarks>
        /// Otherwise a green unit could dodge its own hesitation by being interrupted: the
        /// march would come back off the reflex already counted down, and being shot at would
        /// have made the unit faster.
        /// </remarks>
        private static bool HesitationRestartsOnPreempt(StringBuilder log)
        {
            var queue = new CommandQueue();
            queue.Enqueue(Command.MoveTo(new ActorId(1), new UnitId(1), new Vector2(5f, 5f)));

            const int hesitation = 3;
            for (int i = 0; i < hesitation + 1; i++) queue.TryBegin(hesitation, out _);

            if (queue[0].Status != CommandStatus.Executing)
            {
                log.AppendLine("  preempt reset: FAILED, setup never reached Executing");
                return false;
            }

            queue.InsertFront(Command.Engage(new ActorId(1), new UnitId(1), new UnitId(2)));

            if (queue[1].TicksPending != 0)
            {
                log.AppendLine($"  preempt reset: FAILED, displaced order kept " +
                               $"{queue[1].TicksPending} tick(s) of hesitation");
                return false;
            }

            log.AppendLine("  preempt reset: displaced order forms up again  ok");
            return true;
        }
    }
}
#endif
