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
//   2. The bus delivers everything published before this step (delivery rule 1).
//   3. Each unit advances its plan by one step, in a stable order.
//
// Delivering before executing means an order issued at tick N is acted on at N+1 rather than
// sitting a further step — and doing it in a fixed place means "when does my order take
// effect" has one answer rather than depending on who published it.

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Strategos.Maps;
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

        // ─── Stepping ─────────────────────────────────────────────────────────

        public void Step()
        {
            Tick++;

            _context.Map = Map;
            _context.Catalogue = Catalogue;
            _context.Tick = Tick;
            _context.SecondsPerTick = SecondsPerTick;

            Bus.Deliver();

            // Stable order, by the scenario's unit order. Never by dictionary iteration.
            for (int i = 0; i < _units.Count; i++)
                AdvanceUnit(_units[i]);
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
                    queue.Abort(includeExecuting: true);
                    ApplyAbortPosture(command.TargetUnit);
                    break;

                case CommandKind.CancelFrom:
                    queue.CancelFrom(command.Index);
                    break;

                case CommandKind.Hold:
                    queue.Abort(includeExecuting: true);
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
                return;
            }

            var outcome = executor.Step(unit, entry, _context);
            queue.Tick();

            if (outcome != CommandOutcome.Running) queue.Finish();
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
        /// This is what the divergence test compares. It deliberately covers unit state *and*
        /// queue state: a replay that lands units in the right places with the wrong plans has
        /// still diverged, and would drift apart on the next step.
        /// </summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append("t").Append(Tick).Append('|');

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
