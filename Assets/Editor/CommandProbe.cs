// CommandProbe.cs
// Verifies the command system headlessly, including the four delivery rules and — the one
// that matters most — that a recorded run replays to byte-identical state.
//
// The divergence test is written now, with the first executor, because confidence in
// determinism is close to impossible to retrofit. Once something has quietly broken it, every
// run still *looks* fine and the failure surfaces months later as an unreproducible replay.
//
// Menu:  Strategos > Probe Commands
// Batch: -executeMethod Strategos.Editor.CommandProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Maps;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class CommandProbe
    {
        /// <summary>
        /// Stand-in for a world command: takes a fixed number of ticks and moves the unit to
        /// the target when it finishes.
        ///
        /// #9 deliberately ships no real executors — movement is #8. This one exists so
        /// sequencing, cancellation and determinism can be proven before anything real
        /// depends on them.
        /// </summary>
        private sealed class StubMoveExecutor : ICommandExecutor
        {
            public const int TicksToComplete = 3;
            public CommandKind Kind => CommandKind.MoveTo;

            public CommandOutcome Step(UnitInstance unit, in QueuedCommand entry,
                ExecutionContext context)
            {
                unit.Posture = Posture.Moving;
                if (entry.TicksExecuting + 1 < TicksToComplete) return CommandOutcome.Running;

                unit.Cell = entry.Command.TargetCell;
                unit.Posture = Posture.Halted;
                return CommandOutcome.Completed;
            }
        }

        [MenuItem("Strategos/Probe Commands")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckNextStepDelivery(log);
            bad += CheckDispatchOrder(log);
            bad += CheckQueueSequencing(log);
            bad += CheckAbort(log);
            bad += CheckCancelFrom(log);
            bad += CheckLogIsAppendOnly(log);
            bad += CheckRealMovement(log);
            bad += CheckPathfinding(log);
            bad += CheckReplayDivergence(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[CommandProbe]\n" + log);
            else Debug.LogError("[CommandProbe]\n" + log);
        }

        // ─── Fixtures ─────────────────────────────────────────────────────────

        private static Simulation NewSim()
        {
            var scenario = ScenarioSamples.Skirmish();
            // Erosion is the dominant generation cost and nothing here depends on it.
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();

            var sim = new Simulation(scenario, map);
            sim.AddExecutor(new StubMoveExecutor());
            return sim;
        }

        private static UnitId FirstUnit(Simulation sim) => sim.Units[0].Id;
        private static ActorId Blue => new(1);

        // ─── Rule 1: publish to the next step ─────────────────────────────────

        private static int CheckNextStepDelivery(StringBuilder log)
        {
            int bad = 0;
            var sim = NewSim();
            var unit = FirstUnit(sim);

            sim.Issue(Command.MoveTo(Blue, unit, new Vector2(60f, 70f)));

            // Published, not yet delivered: the queue must still be empty.
            if (sim.QueueOf(unit).Count != 0)
            {
                log.AppendLine("  FAIL command reached the queue before a step ran");
                bad++;
            }

            sim.Step();

            if (sim.QueueOf(unit).Count != 1)
            {
                log.AppendLine($"  FAIL after one step the queue holds {sim.QueueOf(unit).Count}, expected 1");
                bad++;
            }

            log.AppendLine($"  rule 1 publish-to-next-step: queued only after a step  " +
                           $"{(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        // ─── Rule 2: stable, declared dispatch order ──────────────────────────

        private static int CheckDispatchOrder(StringBuilder log)
        {
            int bad = 0;
            var bus = new CommandBus();
            var seen = new List<string>();

            // Registered out of order on purpose; delivery must follow Order, not registration.
            bus.Subscribe("late", 20, _ => seen.Add("late"));
            bus.Subscribe("early", -5, _ => seen.Add("early"));
            bus.Subscribe("middle", 0, _ => seen.Add("middle"));

            bus.Publish(Command.Hold(Blue, new UnitId(1)));
            bus.Deliver();

            var expected = new[] { "early", "middle", "late" };
            if (seen.Count != expected.Length)
            {
                log.AppendLine($"  FAIL {seen.Count} subscribers fired, expected {expected.Length}");
                bad++;
            }
            else
            {
                for (int i = 0; i < expected.Length; i++)
                    if (seen[i] != expected[i])
                    {
                        log.AppendLine($"  FAIL dispatch order [{i}] was '{seen[i]}', expected '{expected[i]}'");
                        bad++;
                    }
            }

            // Publishing during dispatch must not deliver within the same call.
            var reentrant = new CommandBus();
            int inner = 0;
            reentrant.Subscribe("republish", 0, c =>
            {
                if (c.Kind == CommandKind.Hold)
                    reentrant.Publish(Command.Abort(Blue, new UnitId(2)));
                else inner++;
            });
            reentrant.Publish(Command.Hold(Blue, new UnitId(1)));
            reentrant.Deliver();
            if (inner != 0)
            {
                log.AppendLine("  FAIL a command published during dispatch was delivered in the same step");
                bad++;
            }
            reentrant.Deliver();
            if (inner != 1)
            {
                log.AppendLine("  FAIL the republished command did not arrive on the following step");
                bad++;
            }

            log.AppendLine($"  rule 2 dispatch order: {string.Join(" -> ", seen)}; " +
                           $"republish lands next step  {(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        // ─── Queue sequencing ─────────────────────────────────────────────────

        private static int CheckQueueSequencing(StringBuilder log)
        {
            int bad = 0;
            var sim = NewSim();
            var id = FirstUnit(sim);
            var unit = sim.UnitOf(id);

            var a = new Vector2(60f, 70f);
            var b = new Vector2(62f, 74f);
            var c = new Vector2(66f, 78f);

            sim.Issue(Command.MoveTo(Blue, id, a));
            sim.Issue(Command.MoveTo(Blue, id, b));
            sim.Issue(Command.MoveTo(Blue, id, c));

            sim.Step();   // deliver: all three queued
            if (sim.QueueOf(id).Count != 3)
            {
                log.AppendLine($"  FAIL expected 3 queued, got {sim.QueueOf(id).Count}");
                bad++;
            }

            // Each stub command takes 3 ticks. Run enough steps for all three.
            var reached = new List<Vector2>();
            for (int i = 0; i < StubMoveExecutor.TicksToComplete * 3 + 2; i++)
            {
                sim.Step();
                if (reached.Count == 0 && unit.Cell == a) reached.Add(a);
                else if (reached.Count == 1 && unit.Cell == b) reached.Add(b);
                else if (reached.Count == 2 && unit.Cell == c) reached.Add(c);
            }

            if (reached.Count != 3)
            {
                log.AppendLine($"  FAIL a three-command plan reached {reached.Count} of 3 waypoints in order");
                bad++;
            }
            if (!sim.QueueOf(id).IsEmpty)
            {
                log.AppendLine($"  FAIL queue not empty after the plan finished ({sim.QueueOf(id).Count} left)");
                bad++;
            }

            log.AppendLine($"  queue sequencing: three commands executed in order, queue drained  " +
                           $"{(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        // ─── Abort ────────────────────────────────────────────────────────────

        private static int CheckAbort(StringBuilder log)
        {
            int bad = 0;
            var sim = NewSim();
            var id = FirstUnit(sim);
            var unit = sim.UnitOf(id);
            var start = unit.Cell;

            sim.Issue(Command.MoveTo(Blue, id, new Vector2(60f, 70f)));
            sim.Issue(Command.MoveTo(Blue, id, new Vector2(62f, 74f)));
            sim.Issue(Command.MoveTo(Blue, id, new Vector2(66f, 78f)));

            sim.Step();   // queued
            sim.Step();   // first command executing

            int loggedBefore = sim.Log.Count;

            sim.Issue(Command.Abort(Blue, id));
            sim.Step();   // abort delivered

            if (!sim.QueueOf(id).IsEmpty)
            {
                log.AppendLine($"  FAIL abort left {sim.QueueOf(id).Count} command(s) queued");
                bad++;
            }
            if (unit.Cell != start)
            {
                log.AppendLine("  FAIL unit moved despite being aborted mid-command");
                bad++;
            }
            if (unit.Posture == Posture.Moving)
            {
                log.AppendLine("  FAIL aborted unit left in Moving posture");
                bad++;
            }

            // The log is never rewritten: aborting appends, it does not remove.
            if (sim.Log.Count != loggedBefore + 1)
            {
                log.AppendLine($"  FAIL log went from {loggedBefore} to {sim.Log.Count}; " +
                               $"abort must append exactly one entry and remove none");
                bad++;
            }

            int moveEntries = 0;
            foreach (var c in sim.Log.Entries) if (c.Kind == CommandKind.MoveTo) moveEntries++;
            if (moveEntries != 3)
            {
                log.AppendLine($"  FAIL log holds {moveEntries} MoveTo entries after abort, expected 3");
                bad++;
            }

            log.AppendLine($"  abort: plan cleared, unit halted where it stood, log still holds " +
                           $"all {sim.Log.Count} orders  {(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        private static int CheckCancelFrom(StringBuilder log)
        {
            int bad = 0;
            var sim = NewSim();
            var id = FirstUnit(sim);

            sim.Issue(Command.MoveTo(Blue, id, new Vector2(60f, 70f)));
            sim.Issue(Command.MoveTo(Blue, id, new Vector2(62f, 74f)));
            sim.Issue(Command.MoveTo(Blue, id, new Vector2(66f, 78f)));
            sim.Step();

            sim.Issue(Command.CancelFrom(Blue, id, 1));
            sim.Step();

            if (sim.QueueOf(id).Count != 1)
            {
                log.AppendLine($"  FAIL CancelFrom(1) left {sim.QueueOf(id).Count} commands, expected 1");
                bad++;
            }

            log.AppendLine($"  cancel-from: a FRAGO at finer grain keeps the head  " +
                           $"{(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        private static int CheckLogIsAppendOnly(StringBuilder log)
        {
            int bad = 0;
            var sim = NewSim();
            var id = FirstUnit(sim);

            for (int i = 0; i < 5; i++)
                sim.Issue(Command.MoveTo(Blue, id, new Vector2(60f + i, 70f)));

            // Sequence numbers must be dense, monotonic and start at 1.
            ulong expected = 1;
            foreach (var c in sim.Log.Entries)
            {
                if (c.Seq != expected)
                {
                    log.AppendLine($"  FAIL log sequence {c.Seq}, expected {expected}");
                    bad++;
                    break;
                }
                expected++;
            }

            log.AppendLine($"  log: {sim.Log.Count} entries, sequence dense from 1  " +
                           $"{(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        // ─── The real MoveTo executor (#8a) ───────────────────────────────────

        /// <summary>
        /// A simulation using the real MoveToExecutor rather than the stub.
        /// </summary>
        private static Simulation NewRealSim()
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();
            var sim = new Simulation(scenario, map);
            sim.AddExecutor(new MoveToExecutor());
            return sim;
        }

        /// <summary>
        /// The unit actually crosses ground, at the speed its own capabilities allow, and
        /// stops on arrival rather than oscillating around the destination.
        /// </summary>
        private static int CheckRealMovement(StringBuilder log)
        {
            int bad = 0;
            var sim = NewRealSim();
            var unit = sim.Units[0];              // A/1-7 IN, mechanised
            var caps = unit.Capabilities(sim.Catalogue);
            var start = unit.Cell;

            // A destination a few cells away on the same open ground.
            var target = start + new Vector2(6f, 4f);
            float distanceCells = (target - start).magnitude;

            sim.Issue(Command.MoveTo(Blue, unit.Id, target));

            // Nothing happens until a step runs: publishing is not executing.
            if (unit.Cell != start)
            {
                log.AppendLine("  FAIL unit moved before any step ran");
                bad++;
            }

            // Step delivers AND advances in the same tick, by design — an order issued at
            // tick N takes effect at N+1 rather than sitting a further step — so the unit is
            // expected to be under way after this.
            sim.Step();
            if (unit.Cell == start)
            {
                log.AppendLine("  FAIL unit had not begun moving after the delivery step");
                bad++;
            }

            int steps = 0;
            const int limit = 500;
            while (!sim.QueueOf(unit.Id).IsEmpty && steps < limit) { sim.Step(); steps++; }

            if (steps >= limit)
            {
                log.AppendLine($"  FAIL move did not finish within {limit} steps");
                return bad + 1;
            }

            float error = Vector2.Distance(unit.Cell, target);
            if (error > MoveToExecutor.ArrivalCells)
            {
                log.AppendLine($"  FAIL arrived {error:0.###} cells from the target");
                bad++;
            }
            if (unit.Posture != Posture.Moving && unit.Posture != Posture.Halted)
            {
                log.AppendLine("  FAIL unexpected posture after arriving");
                bad++;
            }
            if (unit.Posture == Posture.Moving)
            {
                log.AppendLine("  FAIL unit still in Moving posture after arriving");
                bad++;
            }

            // Speed must come from the capability, not from a constant. Predict the tick
            // count from metres per second and compare — a move that is right to within a
            // couple of ticks is being driven by the capability.
            float metresPerCell = sim.Map.Header.MetresPerCell;
            float openSpeed = caps.SpeedMps(LandcoverClass.Open, 0f, onRoad: false);
            float expectedTicks = distanceCells * metresPerCell / openSpeed / Simulation.SecondsPerTick;

            // Ground varies along the way, so allow a wide band; the point is the order of
            // magnitude, not the exact figure.
            if (steps < expectedTicks * 0.5f || steps > expectedTicks * 4f)
            {
                log.AppendLine($"  FAIL took {steps} ticks, expected roughly {expectedTicks:0} " +
                               $"at {openSpeed:0.#} m/s over {distanceCells * metresPerCell:0} m");
                bad++;
            }

            // A slower unit must take longer over the same ground, which is the check that
            // actually proves capabilities are being read per unit.
            var sim2 = NewRealSim();
            var foot = sim2.Units[0];
            foot.CapabilityId = UnitCatalogue.InfantryFoot;
            foot.Cell = start;
            sim2.Issue(Command.MoveTo(Blue, foot.Id, target));
            sim2.Step();
            int footSteps = 0;
            while (!sim2.QueueOf(foot.Id).IsEmpty && footSteps < 5000) { sim2.Step(); footSteps++; }

            if (footSteps <= steps)
            {
                log.AppendLine($"  FAIL foot took {footSteps} ticks, mechanised {steps} — " +
                               $"speed is not coming from capabilities");
                bad++;
            }

            log.AppendLine($"  movement: {distanceCells * metresPerCell:0} m in {steps} ticks " +
                           $"mechanised vs {footSteps} on foot, arrived within " +
                           $"{error:0.###} cells  {(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        // ─── Pathfinding (#8b) ────────────────────────────────────────────────

        /// <summary>
        /// The route respects the terrain rather than walking through it, prefers roads where
        /// roads are quicker, and fails cleanly where there is no way through.
        /// </summary>
        private static int CheckPathfinding(StringBuilder log)
        {
            int bad = 0;

            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();
            var cat = UnitCatalogue.Default();

            var mech = cat.Get(UnitCatalogue.InfantryMech);
            var grid = Strategos.Movement.MovementGrid.Build(map, mech);
            var (passable, road, barrier) = grid.Census();

            int fords = 0, bridges = 0;
            foreach (var poi in map.Pois)
            {
                if (poi.Kind == MapPoiKind.Ford) fords++;
                if (poi.Kind == MapPoiKind.Bridge) bridges++;
            }

            log.AppendLine($"  grid {map.Width}x{map.Height}: {passable} passable, " +
                           $"{road} road, {barrier} wet-crossing cells " +
                           $"({fords} fords, {bridges} bridges)");

            if (passable == 0) { log.AppendLine("  FAIL nothing is passable"); bad++; }
            if (road == 0) { log.AppendLine("  FAIL no road cells were rasterised"); bad++; }

            // A route must never step on a cell its own capability rejects. This is the
            // assertion that catches a grid built from the wrong unit, or barriers ignored.
            var a = new Vector2Int(58, 72);
            var b = new Vector2Int(180, 176);
            var found = Strategos.Movement.PathFinder.Find(grid, a, b);

            if (!found.Found)
            {
                log.AppendLine($"  FAIL no route from {a} to {b} across open ground");
                bad++;
            }
            else
            {
                int illegal = 0;
                foreach (var c in found.Cells)
                    if (!grid.Passable(c.x, c.y)) illegal++;

                if (illegal > 0)
                {
                    log.AppendLine($"  FAIL route crosses {illegal} impassable cell(s)");
                    bad++;
                }

                // Crossing a river must cost more than the same going without one, or the
                // penalty is not reaching the search.
                int wet = 0;
                foreach (var c in found.Cells) if (grid.IsBarrier(c.x, c.y)) wet++;

                int rawLegs = found.Cells.Count;
                Strategos.Movement.PathFinder.Simplify(found.Cells);
                int simplified = found.Cells.Count;
                Strategos.Movement.PathFinder.Smooth(grid, found.Cells);

                float straightCells = Vector2.Distance(a, b);
                log.AppendLine($"  route {a} -> {b}: {rawLegs} cells -> {simplified} after " +
                               $"simplify -> {found.Cells.Count} after smoothing, " +
                               $"{found.Seconds:0} s, {found.Expanded} expanded, {wet} wet " +
                               $"(straight line is {straightCells:0} cells)");
            }

            // Unreachable must fail rather than hang or return something.
            var water = FindCell(map, LandcoverClass.Water);
            if (water.x >= 0f)
            {
                var into = Strategos.Movement.PathFinder.Find(grid,
                    a, new Vector2Int((int)water.x, (int)water.y));
                if (into.Found)
                {
                    log.AppendLine("  FAIL routed into water, which mechanised cannot enter");
                    bad++;
                }
            }

            // Foot and mechanised disagree about the same ground, because their capabilities
            // do -- the property that proves the grid is built per capability.
            var footGrid = Strategos.Movement.MovementGrid.Build(
                map, cat.Get(UnitCatalogue.InfantryFoot));
            var (footPassable, _, _) = footGrid.Census();
            if (footPassable <= passable)
            {
                log.AppendLine($"  FAIL foot can reach {footPassable} cells, mechanised " +
                               $"{passable}; foot should out-climb and out-wade it");
                bad++;
            }
            else
            {
                log.AppendLine($"  passability differs by unit: foot {footPassable} cells vs " +
                               $"mechanised {passable}");
            }

            // Determinism: the same search twice must give the same cells. Compared before
            // any post-processing, and cell by cell rather than by count -- two different
            // routes of equal length would otherwise pass.
            var first = Strategos.Movement.PathFinder.Find(grid, a, b);
            var again = Strategos.Movement.PathFinder.Find(grid, a, b);
            bool identical = first.Cells.Count == again.Cells.Count;
            if (identical)
                for (int i = 0; i < first.Cells.Count; i++)
                    if (first.Cells[i] != again.Cells[i]) { identical = false; break; }

            if (!identical)
            {
                log.AppendLine("  FAIL the same search returned a different route");
                bad++;
            }
            else
            {
                log.AppendLine($"  determinism: two searches returned identical {first.Cells.Count}-cell routes");
            }

            return bad;
        }

        private static Vector2 FindCell(MapData map, LandcoverClass want)
        {
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.GetLandcover(x, y) == want) return new Vector2(x, y);
            return new Vector2(-1f, -1f);
        }

        // ─── The one that matters ─────────────────────────────────────────────

        /// <summary>
        /// Record a run, replay its log from a fresh simulation, and require byte-identical
        /// state.
        ///
        /// This is what makes replay, after-action review and eventually lockstep possible,
        /// and it is a property that has to be *maintained* rather than added. Determinism
        /// breaks silently: every run still looks right, and the failure only appears when
        /// someone tries to reproduce one.
        /// </summary>
        private static int CheckReplayDivergence(StringBuilder log)
        {
            int bad = 0;
            const int steps = 40;

            // A scripted run: several units, overlapping plans, a cancellation part way.
            static void Script(Simulation sim, int tick)
            {
                var units = sim.Units;
                switch (tick)
                {
                    case 1:
                        sim.Issue(Command.MoveTo(Blue, units[0].Id, new Vector2(62f, 76f)));
                        sim.Issue(Command.MoveTo(Blue, units[0].Id, new Vector2(70f, 80f)));
                        sim.Issue(Command.MoveTo(new ActorId(2), units[3].Id, new Vector2(188f, 172f)));
                        break;
                    case 4:
                        sim.Issue(Command.MoveTo(Blue, units[1].Id, new Vector2(80f, 66f)));
                        break;
                    case 9:
                        sim.Issue(Command.Abort(Blue, units[0].Id));
                        break;
                    case 12:
                        sim.Issue(Command.MoveTo(Blue, units[0].Id, new Vector2(58f, 68f)));
                        sim.Issue(Command.MoveTo(Blue, units[2].Id, new Vector2(92f, 96f)));
                        break;
                    case 20:
                        sim.Issue(Command.CancelFrom(new ActorId(2), units[3].Id, 0));
                        break;
                }
            }

            // Uses the real executor: determinism has to hold for the code that ships, not
            // only for a stub with integer timing.
            var recorded = NewRealSim();
            for (int t = 1; t <= steps; t++)
            {
                Script(recorded, t);
                recorded.Step();
            }

            string original = recorded.Signature();

            // Replay: a fresh simulation fed only the recorded log, by tick.
            var replayed = NewRealSim();
            var byTick = new Dictionary<int, List<Command>>();
            foreach (var c in recorded.Log.Entries)
            {
                if (!byTick.TryGetValue(c.Tick, out var list))
                    byTick[c.Tick] = list = new List<Command>();
                list.Add(c);
            }

            for (int t = 1; t <= steps; t++)
            {
                // Issue in recorded sequence order — the log's order is the authority.
                if (byTick.TryGetValue(t - 1, out var due))
                    foreach (var c in due) replayed.Issue(c);
                replayed.Step();
            }

            string replayedSig = replayed.Signature();

            if (original != replayedSig)
            {
                log.AppendLine("  FAIL replay diverged from the recorded run");
                log.AppendLine($"    recorded: {Trim(original)}");
                log.AppendLine($"    replayed: {Trim(replayedSig)}");
                bad++;
            }

            // And the same run twice must match, which catches ambient nondeterminism that a
            // replay comparison alone could miss.
            var again = NewRealSim();
            for (int t = 1; t <= steps; t++) { Script(again, t); again.Step(); }
            if (again.Signature() != original)
            {
                log.AppendLine("  FAIL two identical runs produced different state");
                bad++;
            }

            log.AppendLine($"  divergence test over {steps} steps, {recorded.Log.Count} orders, " +
                           $"{recorded.Units.Count} units: {(bad == 0 ? "IDENTICAL" : "DIVERGED")}");
            if (bad == 0) log.AppendLine($"    signature {Trim(original, 96)}");
            return bad;
        }

        private static string Trim(string s, int max = 160) =>
            s.Length <= max ? s : s.Substring(0, max) + "...";
    }
}
#endif
