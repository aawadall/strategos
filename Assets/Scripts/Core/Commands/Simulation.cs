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
//   5. Every engagement declared in 4 resolves SIMULTANEOUSLY, then applies.
//   6. Suppression decays.
//   7. Detection sweeps, publishing what changed — for delivery at the next step.
//
// Step 5 is split from step 4 on purpose and the reason is in ExecutionContext.Engagements:
// resolving fire inside the unit loop hands whichever unit the loop reached first a free shot
// at an undamaged enemy, which is a first-mover advantage decided by the order units happen to
// appear in the scenario file.
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
using Strategos.Combat;
using Strategos.Maps;
using Strategos.Objectives;
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

        /// <summary>
        /// Objective control and victory, or null for a scenario that cannot be won.
        ///
        /// Built from the scenario when it declares objectives or conditions, and absent
        /// otherwise — a sandbox is a scenario nobody is trying to win, and it should not pay
        /// for an evaluator that can never fire.
        /// </summary>
        public VictoryEvaluator Victory { get; }

        /// <summary>True once a side has won or the scenario has timed out.</summary>
        public bool IsOver => Victory != null && Victory.IsDecided;

        /// <summary>
        /// Autonomous reaction, or null for a simulation where nothing acts on its own.
        /// </summary>
        /// <remarks>
        /// Opt-in rather than always present, because most probes want a world where units do
        /// exactly what they were told and nothing else — a firefight that starts itself is
        /// very hard to write an assertion against.
        /// </remarks>
        public Reactions.ReactionController Reactions { get; private set; }

        /// <summary>Turns on autonomous reaction. Call once, before stepping.</summary>
        public Reactions.ReactionController EnableReactions() =>
            Reactions ??= new Reactions.ReactionController(this);

        /// <summary>
        /// Side-level intent for whoever is not being played, or null.
        ///
        /// Separate from <see cref="Reactions"/> and deliberately so: that one is scoped to
        /// reflexes that read *reports*, and objective-seeking reads the objective list. One
        /// class doing both would blur a boundary that took an issue to draw.
        /// </summary>
        public Direction.SideDirector Director { get; private set; }

        /// <summary>Turns on side-level intent for the given sides. Call once, before stepping.</summary>
        public Direction.SideDirector EnableDirector(System.Collections.Generic.IEnumerable<SideId> sides) =>
            Director ??= new Direction.SideDirector(this, sides);

        public int Tick { get; private set; }

        /// <summary>
        /// The units that fight — leaves of the ORBAT tree.
        /// </summary>
        /// <remarks>
        /// **This deliberately keeps meaning exactly what it has always meant.** Every consumer
        /// in Core enumerates units to answer "what can be seen, shot at, moved or counted",
        /// and a formation appearing in those lists would be detected separately from its
        /// subordinates, engaged separately and counted separately for victory — every one of
        /// them double-counting the same troops. Leaving this as the things that fight means
        /// none of them needed changing when the tree arrived.
        ///
        /// Ask for <see cref="AllUnits"/> when you want formations too. The dangerous list is
        /// the one you have to name.
        /// </remarks>
        public IReadOnlyList<UnitInstance> Units => _units;

        /// <summary>Every unit including formations, in scenario order.</summary>
        public IReadOnlyList<UnitInstance> AllUnits => Hierarchy.All;

        /// <summary>The ORBAT as a tree. Built once; the shape does not change mid-scenario.</summary>
        public UnitHierarchy Hierarchy { get; }

        public Simulation(Scenario scenario, MapData map, UnitCatalogue catalogue = null)
        {
            Scenario = scenario;
            Map = map;
            Catalogue = catalogue ?? UnitCatalogue.Default();

            Hierarchy = new UnitHierarchy(scenario?.Units);

            // Leaves only. A formation holds no queue: an order addressed to one decomposes at
            // delivery into orders for its subordinates, so there is never anything for it to
            // execute and no second place for plan state to live.
            foreach (var u in Hierarchy.Leaves)
            {
                _units.Add(u);
                _queues[u.Id.Value] = new CommandQueue();
            }

            // Indexed by position in _units, so it must be built after that list is filled.
            _contacts = new ContactTracker(scenario, _units);
            _publishReport = r => Report(r);

            if (scenario != null && (scenario.Objectives.Count > 0 || scenario.Victory.Count > 0))
                Victory = new VictoryEvaluator(scenario.Objectives, scenario.Victory, _units,
                    scenario.TimeLimitTicks);

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

            // Reflexes decide from the picture as it stands at the START of the step, before
            // anyone has moved or fired. A reaction therefore cannot be influenced by something
            // that happens later in the same tick, and the orders it issues are delivered on
            // the next step like everyone else's.
            // Intent before reflexes: a side decides where it is going, then units react to
            // what they meet on the way. The reverse would let a reflex be overwritten by an
            // order issued in the same step.
            Director?.Evaluate();
            Reactions?.Evaluate();

            _context.Engagements.Clear();

            // Stable order, by the scenario's unit order. Never by dictionary iteration.
            for (int i = 0; i < _units.Count; i++)
                AdvanceUnit(_units[i]);

            ResolveEngagements();

            for (int i = 0; i < _units.Count; i++)
                EngagementResolver.DecaySuppression(_units[i], SecondsPerTick);

            // Fatigue after the engagements resolve, so "was this unit in a fight this tick"
            // is answerable — the intent list is still populated here and is cleared at the
            // top of the next step's unit loop. Marching and fighting cost readiness; halted
            // and unengaged gives it back, more slowly.
            for (int i = 0; i < _units.Count; i++)
                FatigueModel.Apply(_units[i], WasEngagedThisTick(_units[i].Id), Map, Catalogue,
                    SecondsPerTick);

            // After movement: a contact should name where the subject ended the tick.
            // Cached delegate, not a lambda — this runs every step of every replay.
            _contacts?.Sweep(Map, Catalogue, Tick, _publishReport);

            // Last, so victory is judged on the state the tick actually ended in.
            Victory?.Evaluate(_units, Tick);
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
            // A formation has no queue. An order addressed to one is a directive: it becomes
            // one order per subordinate, each issued through the same path so the log records
            // the directive *and* what it became, and a replay reconstructs both.
            if (Hierarchy.IsFormation(command.TargetUnit))
            {
                Decompose(command);
                return;
            }

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
                    if (command.Preempt) queue.InsertFront(command);
                    else queue.Enqueue(command);
                    break;
            }
        }

        /// <summary>
        /// Hands a formation's order down to its immediate subordinates.
        /// </summary>
        /// <remarks>
        /// **One level at a time, not straight to the leaves.** A brigade order becomes
        /// battalion orders, which become company orders on the next step. That costs a tick
        /// per echelon and it is not an inefficiency — it *is* the propagation delay
        /// phases.md 5.2 wants, arriving free from the structure rather than as a timer bolted
        /// on top, and it is why commanding at height feels different from commanding at hand.
        ///
        /// Issued rather than enqueued directly, so each derived order is logged, is delivered
        /// on the next step like everything else, and is visible to any observer of the topic.
        /// A subordinate that is itself a formation decomposes again when its turn comes.
        ///
        /// Subordinates are walked in scenario order, which UnitHierarchy fixes at
        /// construction. Dictionary order here would diverge a replay.
        /// </remarks>
        private void Decompose(in Command command)
        {
            var subordinates = Hierarchy.SubordinatesOf(command.TargetUnit);

            for (int i = 0; i < subordinates.Count; i++)
            {
                var derived = command;
                derived.TargetUnit = subordinates[i].Id;

                // Cleared so the derived order is stamped afresh by the log rather than
                // carrying its parent's sequence number, which two orders must never share.
                derived.Seq = 0;
                Issue(derived);
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

            // Training costs time at the head of the queue: a green unit has received the
            // order and has not started on it yet. Reflexes preempt onto the front, so this
            // one gate delays returning fire as well as marching.
            if (!queue.TryBegin(unit.HesitationTicks, out var entry)) return;

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
        /// Whether a unit was attacker or defender in any engagement declared this tick.
        /// </summary>
        /// <remarks>
        /// A linear scan of the tick's intents rather than a flag on the unit. The list is a
        /// handful of entries at this scale, and a flag would be state that has to be cleared
        /// somewhere — which is exactly the sort of thing that survives one tick too long and
        /// makes a replay diverge in a way nothing points at.
        /// </remarks>
        private bool WasEngagedThisTick(UnitId id)
        {
            var intents = _context.Engagements;
            for (int i = 0; i < intents.Count; i++)
                if (intents[i].Attacker == id || intents[i].Defender == id) return true;
            return false;
        }

        // ─── Engagement ───────────────────────────────────────────────────────

        private readonly List<EngagementResult> _shots = new();

        /// <summary>
        /// Engage orders that have already reported opening fire, by command sequence.
        /// </summary>
        /// <remarks>
        /// **The edge is the first tick that fire is actually delivered, not the first tick of
        /// the order.** Those are the same thing only when the target starts in range. Engage
        /// does not advance to contact, so an order given at 3 km with a 1200 m weapon spends
        /// its opening ticks resolving as OutOfRange — and keying the report off
        /// <c>TicksExecuting == 0</c> meant that engagement could never report at all, however
        /// long it later spent shooting. #13 reads these reports to decide things, so a
        /// silently missing one is worse than a duplicate.
        ///
        /// A HashSet is safe here for the same reason MoveToExecutor's route dictionary is:
        /// membership is tested and the set is never iterated, so it cannot influence ordering.
        /// It holds one entry per engage order ever issued — bounded by the command log.
        /// </remarks>
        private readonly HashSet<ulong> _openingReported = new();

        /// <summary>
        /// Resolves every engagement declared this tick, then applies them all.
        ///
        /// TWO PASSES, AND IT MATTERS. Every shot is computed against the state at the start of
        /// the tick, so two units firing at each other both fire at full strength and neither
        /// gets to shoot an already-damaged opponent. One pass would make the outcome depend on
        /// the order units appear in the scenario file — a bias nobody would see in a single
        /// game and nobody could trace after a hundred.
        /// </summary>
        private void ResolveEngagements()
        {
            var intents = _context.Engagements;
            if (intents.Count == 0) return;

            _shots.Clear();
            for (int i = 0; i < intents.Count; i++)
                _shots.Add(EngagementResolver.Resolve(
                    UnitOf(intents[i].Attacker), UnitOf(intents[i].Defender),
                    Map, Catalogue, Tick, SecondsPerTick));

            for (int i = 0; i < intents.Count; i++)
            {
                var intent = intents[i];
                var attacker = UnitOf(intent.Attacker);
                var defender = UnitOf(intent.Defender);
                if (attacker == null || defender == null) continue;

                var shot = _shots[i];

                bool hadAmmunition = attacker.Supply.Ammunition > 0f;
                bool wasAlive = !defender.IsDestroyed;

                EngagementResolver.Apply(attacker, defender, shot);

                if (shot.DidFire && _openingReported.Add(intent.Command))
                    Report(SituationReport.Engaged(attacker.Id, defender, Tick, intent.Command));

                // Crossings, reported once each. State changes, not state.
                if (wasAlive && defender.IsDestroyed)
                    Report(SituationReport.Status(ReportKind.Destroyed, defender, Tick));

                if (hadAmmunition && attacker.Supply.Ammunition <= 0f)
                    Report(SituationReport.Status(ReportKind.Depleted, attacker, Tick,
                        intent.Command));

                EndEngagementIfOver(attacker, shot, intent.Command);
            }
        }

        /// <summary>
        /// Ends an engage order when the shot says there is no point continuing.
        ///
        /// The executor cannot decide this: it declares intent and never learns what came of
        /// it.
        ///
        /// TWO OUTCOMES ARE DELIBERATELY NOT REASONS TO STOP. *Out of range* is temporary — a
        /// target that has pulled away may come back. *Suppressed* is temporary too, and it is
        /// the one that would actually bite: sustained fire pins a unit inside a minute, so
        /// treating it as failure would cancel the order of every unit that came off worst in
        /// an opening exchange, permanently, for a condition that clears in under a minute.
        /// <see cref="EngageExecutor.MaxTicks"/> is what bounds both until #13 gives a unit a
        /// real break-contact rule.
        /// </summary>
        private void EndEngagementIfOver(UnitInstance attacker, in EngagementResult shot,
            ulong command)
        {
            ReportKind kind;
            switch (shot.Outcome)
            {
                case EngagementOutcome.TargetDestroyed: kind = ReportKind.OrderCompleted; break;
                case EngagementOutcome.NoAmmunition:
                case EngagementOutcome.AttackerDestroyed: kind = ReportKind.OrderFailed; break;
                default: return;
            }

            var queue = QueueOf(attacker.Id);
            if (queue == null || queue.IsEmpty) return;
            if (!queue.TryPeek(out var head) || head.Command.Kind != CommandKind.Engage) return;

            queue.Finish();
            attacker.Posture = Posture.Halted;
            Report(SituationReport.Status(kind, attacker, Tick, command));
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
            Victory?.AppendSignature(sb);
            sb.Append('|');

            for (int i = 0; i < _units.Count; i++)
            {
                var u = _units[i];
                sb.Append(u.Id.Value).Append(':')
                  .Append(u.Cell.x.ToString("F4")).Append(',')
                  .Append(u.Cell.y.ToString("F4")).Append(':')
                  .Append(u.Strength.ToString("F3")).Append(':')
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
