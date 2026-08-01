// ReportProbe.cs
// Verifies the situation topic headlessly: that detection publishes on the right tick, from
// the right source, exactly once per edge, and that a replay reproduces every report.
//
// The contact assertions use an INDEPENDENT ORACLE rather than a recorded tick number. The
// probe recomputes the range from the same documented seam the tracker uses
// (UnitCapabilities.DetectionRangeAt) and finds the first step where the true distance falls
// inside it, then asserts the report landed on exactly that step. A hard-coded "contact at
// T+83" would pass for the wrong reason the moment the terrain, the speed or the route
// changed, which is the failure mode this whole file exists to avoid.
//
// Menu:  Strategos > Probe Reports
// Batch: -executeMethod Strategos.Editor.ReportProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Maps;
using Strategos.Movement;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class ReportProbe
    {
        /// <summary>Ceiling on how long an approach may run before it is called a failure.</summary>
        private const int MaxApproachTicks = 900;

        private static ActorId Blue => new(1);

        [MenuItem("Strategos/Probe Reports")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckNextStepDelivery(log);
            bad += CheckArrivalReport(log);
            bad += CheckHaltReport(log);
            bad += CheckContactOnApproach(log);
            bad += CheckNoReportStorm(log);
            bad += CheckReplayReproducesReports(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[ReportProbe]\n" + log);
            else Debug.LogError("[ReportProbe]\n" + log);
        }

        // ─── Fixtures ─────────────────────────────────────────────────────────

        /// <summary>
        /// The skirmish, cut down to one unit per side.
        /// </summary>
        /// <remarks>
        /// Two units, not six, because every assertion below is about one pair and the other
        /// four generate contacts of their own that each one would have to filter out. The map
        /// is unchanged, so the ground and the routes are the same as everywhere else.
        ///
        /// Erosion off: it is the dominant generation cost and nothing here depends on it.
        /// CommandProbe.CheckPathfinding establishes that (58,72) still reaches (180,176) on
        /// the un-eroded surface, which is the route this leans on.
        /// </remarks>
        private static Simulation NewPair(out UnitInstance mover, out UnitInstance target,
            out MapData map)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            map = scenario.GenerateMap();

            // By designation, not by index. These were Units[0] and Units[3] until the ORBAT
            // gained battalion formations above the fighting units and every index shifted by
            // two — at which point this probe was moving a battalion and testing "detection"
            // between two units on the same side, which produces no contacts and five
            // failures that all point somewhere other than the cause.
            mover = ByName(scenario, "A/1-7 IN");     // mechanised, at (58,72), BLUFOR
            target = ByName(scenario, "3/2 MRR");     // motorised, OPFOR

            scenario.Units.Clear();
            scenario.Units.Add(mover);
            scenario.Units.Add(target);

            var sim = new Simulation(scenario, map);
            sim.AddExecutor(new MoveToExecutor());
            return sim;
        }

        /// <summary>
        /// A unit by designation. Stable across ORBAT changes in a way an index is not.
        /// </summary>
        private static UnitInstance ByName(Scenario scenario, string designation)
        {
            foreach (var u in scenario.Units)
                if (u.Designation == designation) return u;

            throw new System.InvalidOperationException(
                $"ReportProbe fixture wants '{designation}' and the sample scenario has no " +
                "such unit — the ORBAT changed under the probe.");
        }

        /// <summary>Detection range in cells for a unit standing where it currently stands.</summary>
        private static float RangeCells(UnitInstance unit, MapData map, UnitCatalogue catalogue)
        {
            var caps = unit.Capabilities(catalogue);
            return caps.DetectionRangeAt(unit.Landcover(map)) /
                   Mathf.Max(0.0001f, map.Header.MetresPerCell);
        }

        private static Vector2Int Round(Vector2 cell) =>
            new(Mathf.RoundToInt(cell.x), Mathf.RoundToInt(cell.y));

        /// <summary>
        /// The mover's route to the far corner, unsimplified — the cells it will actually walk
        /// past, which is what a target has to be planted on to be approached.
        /// </summary>
        private static List<Vector2Int> RouteAcross(UnitInstance mover, MapData map,
            UnitCatalogue catalogue, out Vector2Int destination)
        {
            destination = new Vector2Int(180, 176);
            var grid = MovementGrid.Build(map, mover.Capabilities(catalogue));
            var found = PathFinder.Find(grid, Round(mover.Cell), destination);
            return found.Found ? found.Cells : null;
        }

        private static bool FindReport(Simulation sim, ReportKind kind, UnitId source,
            UnitId subject, out SituationReport hit)
        {
            foreach (var r in sim.ReportLog.Entries)
                if (r.Kind == kind && r.Source == source && r.Subject == subject)
                { hit = r; return true; }
            hit = default;
            return false;
        }

        private static int CountReports(Simulation sim, ReportKind kind, UnitId source)
        {
            int n = 0;
            foreach (var r in sim.ReportLog.Entries)
                if (r.Kind == kind && r.Source == source) n++;
            return n;
        }

        // ─── Rule 1, on the other topic ───────────────────────────────────────

        /// <summary>
        /// A report published during step N must be delivered at N+1, exactly like a command.
        /// The rule is shared code now, but a shared implementation still has to be *wired*
        /// into the step, and the failure mode of forgetting to call Deliver is a topic that
        /// silently never fires.
        /// </summary>
        private static int CheckNextStepDelivery(StringBuilder log)
        {
            int bad = 0;
            var sim = NewPair(out var mover, out _, out _);

            var deliveries = new List<(int atTick, int publishedTick, ReportKind kind)>();
            sim.Reports.Subscribe("probe", 10,
                r => deliveries.Add((sim.Tick, r.Tick, r.Kind)));

            // A short hop: the arrival report it produces is the thing being timed.
            sim.Issue(Command.MoveTo(Blue, mover.Id, mover.Cell + new Vector2(2f, 0f)));

            for (int i = 0; i < 60 && deliveries.Count == 0; i++) sim.Step();

            if (deliveries.Count == 0)
            {
                log.AppendLine("  FAIL no report was ever delivered to a subscriber");
                return bad + 1;
            }

            foreach (var d in deliveries)
                if (d.atTick != d.publishedTick + 1)
                {
                    log.AppendLine($"  FAIL {d.kind} published at t{d.publishedTick} was " +
                                   $"delivered at t{d.atTick}, expected t{d.publishedTick + 1}");
                    bad++;
                }

            if (bad == 0)
                log.AppendLine($"  next-step delivery holds over {deliveries.Count} report(s)");

            return bad;
        }

        // ─── Status reports ───────────────────────────────────────────────────

        private static int CheckArrivalReport(StringBuilder log)
        {
            int bad = 0;
            var sim = NewPair(out var mover, out _, out var map);
            var route = RouteAcross(mover, map, sim.Catalogue, out _);

            if (route == null || route.Count < 6)
            {
                log.AppendLine("  FAIL no route to plant an arrival on");
                return bad + 1;
            }

            var goal = new Vector2(route[5].x, route[5].y);
            var order = sim.Issue(Command.MoveTo(Blue, mover.Id, goal));

            for (int i = 0; i < 300; i++)
            {
                sim.Step();
                var q = sim.QueueOf(mover.Id);
                if (q != null && q.IsEmpty && sim.Tick > 1) break;
            }

            if (!FindReport(sim, ReportKind.Arrived, mover.Id, mover.Id, out var arrival))
            {
                log.AppendLine("  FAIL a completed move published no Arrived report");
                return bad + 1;
            }

            // The report must point at the order it discharges, or an after-action review
            // cannot connect what was told to what happened.
            if (arrival.AboutCommand != order.Seq)
            {
                log.AppendLine($"  FAIL Arrived cites command #{arrival.AboutCommand}, " +
                               $"expected #{order.Seq}");
                bad++;
            }

            if (Vector2.Distance(arrival.Cell, goal) > 0.5f)
            {
                log.AppendLine($"  FAIL Arrived reports {arrival.Cell}, unit went to {goal}");
                bad++;
            }

            if (arrival.ObservedTick != arrival.Tick)
            {
                log.AppendLine("  FAIL a self-report has an observation tick unequal to its " +
                               "publication tick");
                bad++;
            }

            if (bad == 0) log.AppendLine($"  arrival: {arrival}");
            return bad;
        }

        private static int CheckHaltReport(StringBuilder log)
        {
            int bad = 0;
            var sim = NewPair(out var mover, out _, out var map);
            var route = RouteAcross(mover, map, sim.Catalogue, out var destination);

            if (route == null)
            {
                log.AppendLine("  FAIL no route to abort");
                return bad + 1;
            }

            sim.Issue(Command.MoveTo(Blue, mover.Id, new Vector2(destination.x, destination.y)));
            sim.Step(6);

            var abort = sim.Issue(Command.Abort(Blue, mover.Id));
            sim.Step();   // delivery

            if (!FindReport(sim, ReportKind.Halted, mover.Id, mover.Id, out var halt))
            {
                log.AppendLine("  FAIL an abort that cancelled a live plan published no Halted");
                return bad + 1;
            }

            if (halt.AboutCommand != abort.Seq)
            {
                log.AppendLine($"  FAIL Halted cites command #{halt.AboutCommand}, " +
                               $"expected the abort #{abort.Seq}");
                bad++;
            }

            // A second abort cancels nothing and must therefore say nothing.
            int before = CountReports(sim, ReportKind.Halted, mover.Id);
            sim.Issue(Command.Abort(Blue, mover.Id));
            sim.Step();

            int after = CountReports(sim, ReportKind.Halted, mover.Id);
            if (after != before)
            {
                log.AppendLine($"  FAIL aborting an empty plan reported a halt " +
                               $"({before} -> {after})");
                bad++;
            }

            if (bad == 0) log.AppendLine($"  halt: {halt}  (no-op abort stayed silent)");
            return bad;
        }

        // ─── Contact ──────────────────────────────────────────────────────────

        /// <summary>
        /// The issue's own verification: step a scenario where one unit moves into another's
        /// detection range, and assert the contact is published on the expected tick from the
        /// expected source.
        /// </summary>
        private static int CheckContactOnApproach(StringBuilder log)
        {
            int bad = 0;
            var sim = NewPair(out var mover, out var target, out var map);
            var route = RouteAcross(mover, map, sim.Catalogue, out var destination);

            if (route == null || route.Count < 4)
            {
                log.AppendLine("  FAIL no route for the approach");
                return bad + 1;
            }

            // Plant the target ON the mover's route, comfortably outside detection, so the
            // approach is guaranteed to close the distance whatever the terrain does. A cell
            // on a found route is passable and reachable by construction.
            float startRange = RangeCells(mover, map, sim.Catalogue);
            Vector2 start = mover.Cell;
            Vector2Int planted = route[route.Count - 1];

            for (int i = 0; i < route.Count; i++)
            {
                if (Vector2.Distance(start, route[i]) < startRange * 1.25f) continue;
                planted = route[i];
                break;
            }

            target.Cell = new Vector2(planted.x, planted.y);

            float initial = Vector2.Distance(start, target.Cell);
            if (initial <= startRange)
            {
                log.AppendLine($"  FAIL target planted at {initial:0.#} cells is already " +
                               $"inside the {startRange:0.#}-cell detection range");
                return bad + 1;
            }

            sim.Issue(Command.MoveTo(Blue, mover.Id, new Vector2(destination.x, destination.y)));

            // The oracle: the first step at which the true distance falls inside the range the
            // observer has from where it is standing. Computed here, not read from the tracker.
            int expectedTick = -1;
            int observedTick = -1;

            for (int i = 0; i < MaxApproachTicks; i++)
            {
                sim.Step();

                if (expectedTick < 0 &&
                    Vector2.Distance(mover.Cell, target.Cell) <=
                        RangeCells(mover, map, sim.Catalogue))
                    expectedTick = sim.Tick;

                if (FindReport(sim, ReportKind.Contact, mover.Id, target.Id, out var seen))
                {
                    observedTick = seen.Tick;
                    break;
                }
            }

            if (observedTick < 0)
            {
                log.AppendLine($"  FAIL no contact after {MaxApproachTicks} ticks closing from " +
                               $"{initial:0.#} cells; mover reached {mover.Cell}");
                return bad + 1;
            }

            FindReport(sim, ReportKind.Contact, mover.Id, target.Id, out var contact);

            if (expectedTick != observedTick)
            {
                log.AppendLine($"  FAIL contact published at t{observedTick}; distance first " +
                               $"fell inside range at t{expectedTick}");
                bad++;
            }

            if (contact.Source != mover.Id)
            {
                log.AppendLine($"  FAIL contact sourced to {contact.Source}, expected {mover.Id}");
                bad++;
            }

            if (contact.Subject != target.Id || contact.SubjectSide != target.Side)
            {
                log.AppendLine($"  FAIL contact names {contact.Subject}/{contact.SubjectSide}, " +
                               $"expected {target.Id}/{target.Side}");
                bad++;
            }

            if (contact.ObservedTick != contact.Tick)
            {
                log.AppendLine($"  FAIL contact observed at t{contact.ObservedTick} but " +
                               $"published at t{contact.Tick}; they are equal until C3 lands");
                bad++;
            }

            if (contact.Confidence != Confidence.Confirmed)
            {
                log.AppendLine($"  FAIL direct observation reported as {contact.Confidence}");
                bad++;
            }

            // Exactly one edge, not one message per tick in range.
            int contacts = CountReports(sim, ReportKind.Contact, mover.Id);
            if (contacts != 1)
            {
                log.AppendLine($"  FAIL {contacts} contact reports for one approach; " +
                               "detection must publish edges, not state");
                bad++;
            }

            // A unit that is never hostile to itself, and a friendly pair, must produce
            // nothing — the whole log should be this one contact plus status reports.
            foreach (var r in sim.ReportLog.Entries)
                if (r.IsObservation && r.Source == r.Subject)
                {
                    log.AppendLine($"  FAIL a unit reported contact with itself: {r}");
                    bad++;
                }

            if (bad == 0)
                log.AppendLine($"  contact at t{observedTick} closing from {initial:0.#} to " +
                               $"{Vector2.Distance(mover.Cell, target.Cell):0.#} cells " +
                               $"(range {RangeCells(mover, map, sim.Catalogue):0.#}): {contact}");

            return bad;
        }

        /// <summary>
        /// A held contact must not re-publish. Two stationary units in range of each other for
        /// fifty steps are one report each, not a hundred.
        /// </summary>
        private static int CheckNoReportStorm(StringBuilder log)
        {
            int bad = 0;
            var sim = NewPair(out var mover, out var target, out var map);

            // Just inside range, and stationary — the case that would flap without hysteresis.
            float range = RangeCells(mover, map, sim.Catalogue);
            target.Cell = mover.Cell + new Vector2(range * 0.98f, 0f);

            sim.Step(50);

            int contacts = CountReports(sim, ReportKind.Contact, mover.Id);
            int losses = CountReports(sim, ReportKind.ContactLost, mover.Id);

            if (contacts != 1)
            {
                log.AppendLine($"  FAIL {contacts} contact reports over 50 stationary steps, " +
                               "expected 1");
                bad++;
            }

            if (losses != 0)
            {
                log.AppendLine($"  FAIL {losses} contact-lost reports while nothing moved");
                bad++;
            }

            if (sim.ActiveContacts < 1)
            {
                log.AppendLine("  FAIL the contact was published but is not held");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  50 stationary steps at {range * 0.98f:0.#}/{range:0.#} cells: " +
                               $"{contacts} contact, {losses} lost, {sim.ReportLog.Count} total");

            return bad;
        }

        // ─── Replay ───────────────────────────────────────────────────────────

        /// <summary>
        /// The same orders on the same ground must produce the same reports, in the same order,
        /// at the same ticks.
        ///
        /// Reports are derived state, so this is a stricter test than it looks: it fails on any
        /// divergence in position, speed, routing or detection, and it fails on the tick rather
        /// than on the value, which is where an ordering bug hides.
        /// </summary>
        private static int CheckReplayReproducesReports(StringBuilder log)
        {
            int bad = 0;

            string first = RunScripted(out int reportsA, out int contactsA);
            string second = RunScripted(out int reportsB, out int contactsB);

            if (first != second)
            {
                log.AppendLine("  FAIL two identical runs produced different reports");
                log.AppendLine($"    run A: {reportsA} reports, {contactsA} contacts");
                log.AppendLine($"    run B: {reportsB} reports, {contactsB} contacts");
                bad++;
            }
            else
            {
                log.AppendLine($"  replay: {reportsA} reports ({contactsA} contact) " +
                               "IDENTICAL across two runs");
            }

            return bad;
        }

        private static string RunScripted(out int reports, out int contacts)
        {
            var sim = NewPair(out var mover, out var target, out var map);
            var route = RouteAcross(mover, map, sim.Catalogue, out var destination);

            if (route != null && route.Count > 40)
                target.Cell = new Vector2(route[40].x, route[40].y);

            var far = new Vector2(destination.x, destination.y);

            for (int t = 0; t < 120; t++)
            {
                // A scripted plan with a change of mind in it, so the run exercises arrival,
                // abort and contact rather than one long uninterrupted march.
                if (t == 0) sim.Issue(Command.MoveTo(Blue, mover.Id, far));
                if (t == 45) sim.Issue(Command.Abort(Blue, mover.Id));
                if (t == 50) sim.Issue(Command.MoveTo(Blue, mover.Id, far));
                sim.Step();
            }

            reports = sim.ReportLog.Count;
            contacts = CountReports(sim, ReportKind.Contact, mover.Id);
            return sim.ReportLog.Signature();
        }
    }
}
#endif
