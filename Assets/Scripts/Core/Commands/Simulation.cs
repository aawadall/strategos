// Simulation.cs
// The clock, the bus, the log, and one queue per unit.
//
// FIXED STEP, ALWAYS. Simulation time advances in whole ticks of a fixed length. Presentation
// may interpolate between them, but nothing in here may read Time.deltaTime, wall-clock,
// UnityEngine.Random, or iterate a Dictionary or HashSet — every one of those makes a replay
// diverge, and the divergence surfaces long after the change that caused it.
//
// The step order below is deliberate and is part of the contract:
//
//   1. Tick advances.
//   2. The command bus delivers everything published before this step (delivery rule 1).
//   3. The report bus does the same.
//   4. Each unit advances its plan by one step, in a stable order.
//   5. Detection sweeps, publishing what changed — for delivery at the next step.
//
// Delivering before executing means an order issued at tick N is acted on at N+1 rather than
// sitting a further step — and doing it in a fixed place means "when does my order take
// effect" has one answer rather than depending on who published it.
//
// Commands before reports is arbitrary in its effect and not in its status: because both
// buses publish to the *next* step, nothing observable depends on which delivers first, so it
// costs nothing to fix — and fixing it means a reacting subscriber added later cannot make it
// matter by accident.
//
// Detection last, after movement, so a contact reports where a unit ended the tick rather
// than where it began it.

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Strategos.Maps;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Commands
{
    public sealed class Simulation
    {
        /// <summary>
        /// Simulated seconds per step. One second keeps the arithmetic legible while
        /// movement is the only thing with a duration; it is a constant rather than a
        /// setting because changing it changes every outcome, which is a decision and not a
        /// preference.
        /// </summary>
        public const float SecondsPerTick = 1f;

        private readonly Dictionary<int, CommandQueue> _queues = new();
        private readonly List<ICommandExecutor> _executors = new();
        private readonly ExecutionContext _context = new();

        // Units in a stable, index-based order. NOT the dictionary — iterating a Dictionary
        // has no guaranteed order and is the classic way a replay quietly diverges.
        private readonly List<UnitInstance> _units = new();

        public Scenario Scenario { get; }
        public MapData Map { get; }
        public UnitCatalogue Catalogue { get; }
        public CommandBus Bus { get; } = new();
        public CommandLog Log { get; } = new();

        /// <summary>The situation topic: reports up. Subscribe for events, read for state.</summary>
        public ReportBus Reports { get; } = new();

        /// <summary>Everything ever reported. The counterpart to <see cref="Log"/>.</summary>
        public ReportLog ReportLog { get; } = new();

        private readonly ContactTracker _contacts;
        private readonly System.Action<SituationReport> _publishReport;

        /// <summary>Detection pairs currently held. Diagnostic; the reports are the interface.</summary>
        public int ActiveContacts => _contacts?.ActiveContacts ?? 0;

        public int Tick { get; private set; }

        public IReadOnlyList<UnitInstance> Units => _units;

        public Simulation(Scenario scenario, MapData map, UnitCatalogue catalogue = null)
        {
            Scenario = scenario;
            Map = map;
            Catalogue = catalogue ?? UnitCatalogue.Default();

            if (scenario != null)
                foreach (var u in scenario.Units)
                {
                    _units.Add(u);
                    _queues[u.Id.Value] = new CommandQueue();
                }

            // Indexed by position in _units, so it must be built after that list is filled.
            _contacts = new ContactTracker(scenario, _units);
            _publishReport = r => Report(r);

            // The unit layer is the only subscriber that mutates anything — delivery rule 3.
            // Order 0 so it sees commands before any observer does.
            Bus.Subscribe("units", 0, OnCommandDelivered);
        }

        public void AddExecutor(ICommandExecutor executor)
        {
            if (executor != null) _executors.Add(executor);
        }

        /// <summary>The live plan for a unit. Read it to draw orders; do not mutate it.</summary>
        public CommandQueue QueueOf(UnitId id) =>
            _queues.TryGetValue(id.Value, out var q) ? q : null;

        /// <summary>
        /// The route a unit is currently walking, from whichever executor planned it, or null.
        /// Asked rather than recomputed, so what a view draws is what the unit is following.
        /// </summary>
        public IReadOnlyList<Vector2Int> RouteOf(UnitId id)
        {
            for (int i = 0; i < _executors.Count; i++)
                if (_executors[i] is IRouteProvider provider)
                {
                    var route = provider.RouteOf(id);
                    if (route != null && route.Count > 0) return route;
                }
            return null;
        }

        public UnitInstance UnitOf(UnitId id)
        {
            for (int i = 0; i < _units.Count; i++)
                if (_units[i].Id == id) return _units[i];
            return null;
        }

        // ─── Issuing ──────────────────────────────────────────────────────────

        /// <summary>
        /// Records an order and publishes it. Returns the stamped command.
        ///
        /// Logging happens here rather than at delivery so the log records what was *ordered*
        /// even if it is later cancelled before it runs — which is the whole point of an
        /// append-only log.
        /// </summary>
        public Command Issue(Command command)
        {
            command.Tick = Tick;
            command = Log.Append(command);
            Bus.Publish(command);
            return command;
        }

        /// <summary>
        /// Records a report and publishes it. Returns the stamped report.
        ///
        /// The mirror of <see cref="Issue"/>, and public for the same reason: an executor, a
        /// future engagement pass and eventually a scripted event all need to report, and each
        /// one that reaches for the bus directly is one whose reports never reach the log.
        ///
        /// Stamps the publication tick and deliberately leaves <c>ObservedTick</c> alone. They
        /// are the same today and the publisher sets both; overwriting the observation time
        /// here would quietly destroy the staleness a delayed report exists to carry.
        /// </summary>
        public SituationReport Report(SituationReport report)
        {
            report.Tick = Tick;
            report = ReportLog.Append(report);
            Reports.Publish(report);
            return report;
        }

        // ─── Stepping ─────────────────────────────────────────────────────────

        public void Step()
        {
            Tick++;

            _context.Map = Map;
            _context.Catalogue = Catalogue;
            _context.Tick = Tick;
            _context.SecondsPerTick = SecondsPerTick;

            Bus.Deliver();
            Reports.Deliver();

            // Stable order, by the scenario's unit order. Never by dictionary iteration.
            for (int i = 0; i < _units.Count; i++)
                AdvanceUnit(_units[i]);

            // After movement: a contact should name where the subject ended the tick.
            // Cached delegate, not a lambda — this runs every step of every replay.
            _contacts?.Sweep(Map, Catalogue, Tick, _publishReport);
        }

        public void Step(int count)
        {
            for (int i = 0; i < count; i++) Step();
        }

        /// <summary>
        /// Applies a delivered command to its addressee's queue.
        ///
        /// Control commands act on the queue and resolve at once; world commands are appended
        /// and wait their turn. This is the only place a queue is modified from outside the
        /// unit's own advance.
        /// </summary>
        private void OnCommandDelivered(Command command)
        {
            var queue = QueueOf(command.TargetUnit);
            if (queue == null) return;   // addressed to a group or an unknown unit

            switch (command.Kind)
            {
                case CommandKind.Abort:
                    // Halt-now is the default: militarily "abort" means stop. Interruptibility
                    // as a per-command property is the open decision recorded on issue #9.
                    ReportHalt(command, queue.Abort(includeExecuting: true));
                    ApplyAbortPosture(command.TargetUnit);
                    break;

                case CommandKind.CancelFrom:
                    queue.CancelFrom(command.Index);
                    break;

                case CommandKind.Hold:
                    ReportHalt(command, queue.Abort(includeExecuting: true));
                    ApplyAbortPosture(command.TargetUnit);
                    break;

                default:
                    queue.Enqueue(command);
                    break;
            }
        }

        /// <summary>
        /// A unit that stops because of an abort should not be left in march order.
        ///
        /// This is the second open decision on #9, resolved conservatively: halted rather than
        /// dug in, because digging in takes time the unit has not been given.
        /// </summary>
        private void ApplyAbortPosture(UnitId id)
        {
            var unit = UnitOf(id);
            if (unit != null && unit.Posture == Posture.Moving) unit.Posture = Posture.Halted;
        }

        /// <summary>
        /// Reports a halt, but only when one actually happened.
        ///
        /// An abort addressed to a unit with nothing to abort cancels nothing, and reporting it
        /// anyway would fill the feed with a unit announcing that it has stopped doing what it
        /// was already not doing.
        /// </summary>
        private void ReportHalt(in Command command, int cancelled)
        {
            if (cancelled <= 0) return;
            var unit = UnitOf(command.TargetUnit);
            if (unit == null) return;
            Report(SituationReport.Status(ReportKind.Halted, unit, Tick, command.Seq));
        }

        private void AdvanceUnit(UnitInstance unit)
        {
            var queue = QueueOf(unit.Id);
            if (queue == null || queue.IsEmpty) return;

            if (!queue.TryBegin(out var entry)) return;

            var executor = ExecutorFor(entry.Command.Kind);
            if (executor == null)
            {
                // No executor for this kind: fail rather than blocking the plan for ever.
                // A queue that silently stalls looks exactly like a pathfinding bug.
                queue.Finish();
                Report(SituationReport.Status(ReportKind.OrderFailed, unit, Tick,
                    entry.Command.Seq));
                return;
            }

            var outcome = executor.Step(unit, entry, _context);
            queue.Tick();

            if (outcome == CommandOutcome.Running) return;

            queue.Finish();
            Report(SituationReport.Status(OutcomeKind(entry.Command.Kind, outcome), unit, Tick,
                entry.Command.Seq));
        }

        /// <summary>
        /// Which report a finished command produces.
        ///
        /// Arrival is a MoveTo completing, not a kind of its own that movement has to remember
        /// to raise. Reporting from here rather than from the executor is what keeps executors
        /// to their one rule — mutate the unit, nothing else — and means an executor added
        /// later reports without being written to.
        /// </summary>
        private static ReportKind OutcomeKind(CommandKind kind, CommandOutcome outcome)
        {
            if (outcome == CommandOutcome.Failed) return ReportKind.OrderFailed;
            return kind == CommandKind.MoveTo ? ReportKind.Arrived : ReportKind.OrderCompleted;
        }

        private ICommandExecutor ExecutorFor(CommandKind kind)
        {
            for (int i = 0; i < _executors.Count; i++)
                if (_executors[i].Kind == kind) return _executors[i];
            return null;
        }

        // ─── Determinism ──────────────────────────────────────────────────────

        /// <summary>
        /// A deterministic signature of everything a replay must reproduce.
        ///
        /// This is what the divergence test compares. It deliberately covers unit state, queue
        /// state *and* what was reported: a replay that lands units in the right places with
        /// the wrong plans has still diverged and would drift apart on the next step, and one
        /// that produces different reports has diverged in what its commander knows — which is
        /// exactly what an AI or a reacting unit will act on.
        /// </summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append("t").Append(Tick).Append('|').Append(ReportLog.Signature()).Append('|');

            for (int i = 0; i < _units.Count; i++)
            {
                var u = _units[i];
                sb.Append(u.Id.Value).Append(':')
                  .Append(u.Cell.x.ToString("F4")).Append(',')
                  .Append(u.Cell.y.ToString("F4")).Append(':')
                  .Append(u.Strength).Append(':')
                  .Append(u.Readiness.ToString("F2")).Append(':')
                  .Append(u.Suppression.ToString("F2")).Append(':')
                  .Append((int)u.Posture).Append(':')
                  .Append(u.Supply.Ammunition.ToString("F2"));

                QueueOf(u.Id)?.AppendSignature(sb);
                sb.Append('|');
            }

            return sb.ToString();
        }
    }
}
