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
//   4. The directive bus does the same — see below.
//   5. Each unit advances its plan by one step, in a stable order.
//   6. Every engagement declared in 5 resolves SIMULTANEOUSLY, then applies.
//   7. Suppression decays.
//   8. Detection sweeps, publishing what changed — for delivery at the next step.
//
// Step 6 is split from step 5 on purpose and the reason is in ExecutionContext.Engagements:
// resolving fire inside the unit loop hands whichever unit the loop reached first a free shot
// at an undamaged enemy, which is a first-mover advantage decided by the order units happen to
// appear in the scenario file.
//
// Delivering before executing means an order issued at tick N is acted on at N+1 rather than
// sitting a further step — and doing it in a fixed place means "when does my order take
// effect" has one answer rather than depending on who published it.
//
// Commands before reports before directives is arbitrary in its effect and not in its status:
// because all three buses publish to the *next* step, nothing observable depends on which
// delivers first, so it costs nothing to fix — and fixing it means a reacting subscriber added
// later cannot make it matter by accident. The directive bus in particular reaches no
// subscriber that mutates simulation state in v1 (#73's own requirement — see
// OnCommandDelivered's note on why a directive never reaches it), so its position here is
// inert today and only matters once something consumes directives mid-step.
//
// Detection last, after movement, so a contact reports where a unit ended the tick rather
// than where it began it.

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Strategos.Combat;
using Strategos.Directives;
using Strategos.Doctrine;
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

        /// <summary>
        /// The directive topic: a message from higher, standing rather than addressed to a
        /// task. A third <c>MessageBus&lt;T&gt;</c> instantiation, not a reuse of either
        /// existing one — see <c>DirectiveBus.cs</c> for why.
        /// </summary>
        public DirectiveBus Directives { get; } = new();

        /// <summary>Everything ever published on <see cref="Directives"/>. The counterpart to <see cref="Log"/>.</summary>
        public DirectiveLog DirectiveLog { get; } = new();

        /// <summary>
        /// Every player action ever taken against a directive — acknowledgement today. The
        /// counterpart to <see cref="Log"/> for the directive topic's response side: #94 found
        /// that <see cref="AcknowledgeDirective"/> touched no log at all, so a replay driven
        /// from <see cref="Log"/> alone never acknowledged anything the original run had.
        /// <see cref="Replayer"/> is what reads this back.
        /// </summary>
        public Directives.DirectiveResponseLog DirectiveResponses { get; } = new();

        /// <summary>What this scenario cost, in the order it was paid.</summary>
        public CasualtyLog Casualties { get; } = new();

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
        /// <remarks>
        /// #100: backed by <see cref="_policy"/>, typed as <see cref="Direction.ISidePolicy"/> so
        /// any implementation can occupy the role. This accessor stays typed as the concrete
        /// <see cref="Direction.SideDirector"/> — an <c>as</c> cast, null when a different policy
        /// is plugged in via <see cref="SetPolicy"/> — because every existing caller
        /// (<c>DirectorProbe</c>, <c>SaveLoadProbe</c>, ...) reads <c>OrdersIssued</c> and the
        /// save/load memory off the default implementation specifically, not off the interface.
        /// </remarks>
        public Direction.SideDirector Director => _policy as Direction.SideDirector;

        /// <summary>The active side policy, whichever implementation it is. See <see cref="Director"/>.</summary>
        public Direction.ISidePolicy Policy => _policy;

        private Direction.ISidePolicy _policy;

        /// <summary>Turns on side-level intent for the given sides. Call once, before stepping.</summary>
        public Direction.SideDirector EnableDirector(System.Collections.Generic.IEnumerable<SideId> sides)
        {
            _policy ??= new Direction.SideDirector(sides);
            return Director;
        }

        /// <summary>
        /// #100: the general seam. Plugs any <see cref="Direction.ISidePolicy"/> in as the side
        /// policy for <see cref="Step"/> to evaluate, in place of <see cref="EnableDirector"/>'s
        /// default <see cref="Direction.SideDirector"/>. Overwrites whatever was there.
        /// </summary>
        public void SetPolicy(Direction.ISidePolicy policy) => _policy = policy;

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

            // The scenario's one directive, published once — mirroring how Victory is built
            // from scenario data above, not appended to as play continues. #36 explicitly
            // defers a FRAGO stream past this issue; v1 is one directive, published near
            // scenario start, standing for the whole run.
            //
            // DELIBERATELY GOES THROUGH Directives, NEVER Bus. A directive published onto the
            // command topic would reach OnCommandDelivered, which decomposes any
            // formation-addressed command unconditionally before inspecting its kind — exactly
            // the auto-decomposition #73 forbids. Publishing here only stamps it into
            // DirectiveLog and queues it on DirectiveBus for delivery next step, same as
            // everything else on either topic (rule 1).
            if (scenario?.Directive != null)
            {
                var directive = scenario.Directive.Value;
                directive.Tick = Tick;
                directive = DirectiveLog.Append(directive);
                Directives.Publish(directive);
            }
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

        /// <summary>
        /// Directive <see cref="Directive.Seq"/> values already acknowledged, so a second call
        /// for the same directive is a no-op rather than a second report.
        /// </summary>
        /// <remarks>
        /// #93 review: nothing guarded the UI's ACKNOWLEDGE button against a second press, and
        /// appending was per-call rather than per-directive — five presses would have appended
        /// five <see cref="ReportKind.DirectiveAcknowledged"/> reports, all entering
        /// <see cref="Reports.ReportLog.Signature"/> and therefore <see cref="Signature"/>.
        /// The button-level guard alone cannot close this: the view only learns a directive was
        /// acknowledged one step later, off the same report stream every other observer reads
        /// (rule 1), so a second press inside that one-tick window would reach this method
        /// before the view knew better. The membership test has to live here, at the one place
        /// that cannot be raced.
        ///
        /// A HashSet tested by membership and never iterated, the same idiom
        /// <see cref="_openingReported"/> uses for the same reason: order cannot depend on it.
        ///
        /// MEMBERSHIP ONLY. NEVER ENUMERATE THIS SET. `docs/simulation-invariants.md`'s own
        /// rule bans iterating a `Dictionary`/`HashSet` anywhere in the simulation, because
        /// enumeration order is not part of .NET's contract and a replay would diverge on
        /// whichever run happened to walk it differently. `.Add()`'s bool return is a membership
        /// test, not iteration, and must stay the only operation this field is used for.
        ///
        /// Deliberately absent from <see cref="Signature"/>: unlike the rest of the simulation's
        /// state, this set is fully derivable from <see cref="Reports.ReportLog"/>, which the
        /// signature already covers — a run that acknowledged and one that did not already
        /// differ there. #74 (save/load) will need to reconstruct it from the loaded
        /// `ReportLog` rather than assume a fresh save always starts empty, or a loaded game
        /// would let an already-acknowledged directive be acknowledged again.
        ///
        /// #94 built the mechanism that reconstruction can now use: <see cref="Replayer"/>
        /// rebuilds this set for free by calling this same method for every logged
        /// <see cref="Directives.DirectiveResponse"/>, exactly as a live run would have. #74
        /// still has its own problem — a save is not always a full log to replay from — but
        /// "replay the response log through this method" is now a real option rather than
        /// something to invent from scratch.
        /// </remarks>
        private readonly HashSet<ulong> _acknowledgedDirectives = new();

        /// <summary>
        /// Whether <paramref name="directiveSeq"/> has been acknowledged. A membership test,
        /// same idiom and same restriction as <see cref="_acknowledgedDirectives"/> itself:
        /// this exists so a probe can compare a replayed run's acknowledgement state against
        /// the original's one directive at a time — never by enumerating the set.
        /// </summary>
        public bool HasAcknowledged(ulong directiveSeq) => _acknowledgedDirectives.Contains(directiveSeq);

        /// <summary>
        /// The addressed formation acknowledges a directive from higher. Appends and publishes
        /// exactly one <see cref="ReportKind.DirectiveAcknowledged"/> report the first time a
        /// given directive is acknowledged; the directive entry in <see cref="DirectiveLog"/> is
        /// never touched — the same "nothing is ever rewritten" rule <see cref="CommandLog"/>
        /// and <see cref="Reports.ReportLog"/> hold. Idempotent per <see cref="Directive.Seq"/>
        /// thereafter: a second call returns null and appends nothing (see
        /// <see cref="_acknowledgedDirectives"/>).
        /// </summary>
        /// <remarks>
        /// ACKNOWLEDGE ONLY, NO REFUSAL, DELIBERATELY. #73 says a directive "can be refused, or
        /// failed", but nothing in either issue states what refusal changes mechanically, and
        /// building a refusal path with no mechanical consequence is an unreachable branch
        /// pretending to be a feature. Add <c>ReportKind.DirectiveRefused</c> and the matching
        /// helper when a caller actually needs one, not "for later".
        ///
        /// Sourced from <see cref="UnitHierarchy.Find"/> rather than <see cref="UnitOf"/>: the
        /// addressee is the formation the directive named, and formations are not in the
        /// fighting-unit list <see cref="UnitOf"/> searches — the same leaves-vs-all-units split
        /// <see cref="Units"/> and <see cref="AllUnits"/> document.
        /// </remarks>
        public SituationReport? AcknowledgeDirective(in Directive directive)
        {
            if (!_acknowledgedDirectives.Add(directive.Seq)) return null;

            // #94: this is the log entry that used to not exist. Appended before the report so
            // a probe reading DirectiveResponses mid-call never observes the report without the
            // response that caused it — the same reason Issue() logs before it publishes.
            DirectiveResponses.Append(new DirectiveResponse
            {
                Tick = Tick,
                DirectiveSeq = directive.Seq,
                Kind = DirectiveResponseKind.Acknowledged,
            });

            var unit = Hierarchy.Find(directive.TargetUnit);

            var report = unit != null
                ? SituationReport.DirectiveAcknowledged(unit, Tick, directive.Seq)
                : new SituationReport
                {
                    Tick = Tick,
                    ObservedTick = Tick,
                    Source = directive.TargetUnit,
                    Kind = ReportKind.DirectiveAcknowledged,
                    Confidence = Confidence.Confirmed,
                    Subject = directive.TargetUnit,
                    AboutDirective = directive.Seq,
                };

            return Report(report);
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
            Directives.Deliver();

            // Reflexes decide from the picture as it stands at the START of the step, before
            // anyone has moved or fired. A reaction therefore cannot be influenced by something
            // that happens later in the same tick, and the orders it issues are delivered on
            // the next step like everyone else's.
            // Intent before reflexes: a side decides where it is going, then units react to
            // what they meet on the way. The reverse would let a reflex be overwritten by an
            // order issued in the same step.
            //
            // #100: the policy hands back commands rather than issuing them itself, so this is
            // where Simulation does the issuing on its behalf — same call, same order, same
            // tick as when Evaluate() used to call _sim.Issue directly.
            if (_policy != null)
            {
                var knowledge = new Direction.SideKnowledge(Tick, IsOver, _units, Victory, QueueOf);
                var decided = _policy.Decide(knowledge);
                if (decided != null)
                    for (int i = 0; i < decided.Count; i++) Issue(decided[i]);
            }
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
            // A formation has no queue. An order addressed to one decomposes at delivery into
            // one order per subordinate, each issued through the same path so the log records
            // the parent order *and* what it became, and a replay reconstructs both.
            //
            // NOT CALLED "A DIRECTIVE": that word is reserved for Core/Directives/Directive — a
            // message from higher that must NOT decompose, which is the opposite of what
            // happens here. A Directive is published on Directives, never on Bus, and never
            // reaches this method at all; see Simulation's constructor and DirectiveBus.cs.
            if (Hierarchy.IsFormation(command.TargetUnit))
            {
                Decompose(command);
                return;
            }

            // A drill unpacks into the orders its steps become. After the formation check, so
            // a drill given to a battalion reaches its companies and each of them expands it —
            // which is what "2 Squad, React to Contact" has to mean.
            if (command.Kind == CommandKind.Drill)
            {
                ExpandDrill(command);
                return;
            }

            // Withdraw unpacks into Abort + MoveTo away from the threat — the same shape as
            // the break-contact reflex, so one path answers both the player order and the
            // autonomous pullback (#85).
            if (command.Kind == CommandKind.Withdraw)
            {
                ExpandWithdraw(command);
                return;
            }

            if (command.Kind == CommandKind.Attack)
            {
                ExpandAttack(command);
                return;
            }

            if (command.Kind == CommandKind.Recon)
            {
                ExpandRecon(command);
                return;
            }

            if (command.Kind == CommandKind.Exploit)
            {
                ExpandExploit(command);
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
                    // #56: cancelling the entry actually under way must halt and reset posture
                    // exactly like Abort — otherwise the unit stays flagged Posture.Moving for
                    // ever, taking EngagementResolver's 1.25x posture factor while standing
                    // still. Cancelling only a still-pending tail must touch neither, so the
                    // queue itself is asked which case this was rather than assumed from
                    // whether anything at all was cancelled.
                    {
                        int cancelled = queue.CancelFrom(command.Index, out bool executingCancelled);
                        if (executingCancelled)
                        {
                            ReportHalt(command, cancelled);
                            ApplyAbortPosture(command.TargetUnit);
                        }
                    }
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

        // ─── Drills ───────────────────────────────────────────────────────────

        /// <summary>How far a bounding step moves, in cells.</summary>
        /// <remarks>
        /// One figure rather than a distance per step, because a drill step says *which way*
        /// and not how far — "bound forward" is a rush, not a march to a grid reference. When
        /// steps want their own distances they can carry one; until then a single authored
        /// number is honest about how much the model actually knows.
        /// </remarks>
        public const float DrillBoundCells = 12f;

        /// <summary>
        /// Unpacks a drill into orders bound to the unit's own situation.
        /// </summary>
        /// <remarks>
        /// **A drill is parameterised and its steps are unbound** — that is what makes it
        /// reusable rather than an order. The only thing available to bind against without a
        /// tactical planner is the threat, and most of doctrine is directional relative to
        /// contact anyway: you assault toward it, you break away from it, you hold where you
        /// are. Ground chosen for its own qualities — a reverse slope, a treeline — needs
        /// terrain reasoning and is not attempted.
        ///
        /// Steps that are not orders are **not silently dropped**. Inherent ones need nothing
        /// issued because the simulation already does them; unmodelled ones are counted and
        /// reported, so a player who calls a drill that is half unimplemented is told, rather
        /// than watching a unit do part of it for no stated reason.
        ///
        /// Each derived order goes out through <see cref="Issue"/>, so the log records the
        /// drill and everything it became.
        /// </remarks>
        private void ExpandDrill(in Command command)
        {
            var unit = UnitOf(command.TargetUnit);
            if (unit == null) return;

            var drill = TtpLibrary.Find(command.DrillCode);
            if (drill == null)
            {
                Report(SituationReport.Status(ReportKind.OrderFailed, unit, Tick, command.Seq));
                return;
            }

            var threat = NearestHostile(unit);
            int issued = 0, unbindable = 0;

            for (int i = 0; i < drill.Steps.Length; i++)
            {
                var step = drill.Steps[i];
                if (!step.IsMechanised) continue;

                if (!Bind(command, unit, step, threat, out var derived)) { unbindable++; continue; }

                Issue(derived);
                issued++;
            }

            // Told, not guessed at. A drill that put nothing in the queue looks exactly like a
            // dropped click, and one that put half of itself there looks like a bug.
            if (issued == 0 || unbindable > 0 || drill.UnmodelledSteps > 0)
                Report(SituationReport.Status(
                    issued == 0 ? ReportKind.OrderFailed : ReportKind.Halted,
                    unit, Tick, command.Seq));
        }

        /// <summary>Turns one step into an order, or reports that it cannot be bound.</summary>
        private bool Bind(in Command command, UnitInstance unit, in TtpStep step,
            UnitInstance threat, out Command derived)
        {
            derived = default;
            var actor = command.IssuedBy;

            switch (step.Binding)
            {
                case StepBinding.Here:
                    derived = Command.Defend(actor, unit.Id);
                    return true;

                case StepBinding.AtThreat:
                    if (threat == null) return false;
                    derived = Command.Engage(actor, unit.Id, threat.Id);
                    return true;

                case StepBinding.TowardThreat:
                case StepBinding.AwayFromThreat:
                {
                    if (threat == null) return false;

                    var away = unit.Cell - threat.Cell;
                    if (away.sqrMagnitude < 0.0001f) return false;

                    away.Normalize();
                    if (step.Binding == StepBinding.TowardThreat) away = -away;

                    derived = Command.MoveTo(actor, unit.Id,
                        unit.Cell + away * DrillBoundCells);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The nearest hostile fighting unit, or null.
        /// </summary>
        /// <remarks>
        /// Walked in scenario order with a strict comparison, so ties resolve to the earlier
        /// unit and never to whichever the iteration happened to reach first. This feeds
        /// command generation, so an unstable answer here would diverge a replay.
        ///
        /// Ground truth rather than what the unit has been told, which is a shortcut: once
        /// belief layers land (#34) a drill should bind against the threat its commander
        /// *knows about*, and a unit that binds against an enemy it cannot see is cheating.
        /// </remarks>
        private UnitInstance NearestHostile(UnitInstance unit)
        {
            var side = Scenario?.FindSide(unit.Side);
            UnitInstance best = null;
            float bestSq = float.MaxValue;

            for (int i = 0; i < _units.Count; i++)
            {
                var other = _units[i];
                if (other == null || other.IsDestroyed || other.Id == unit.Id) continue;
                // Fully qualified: `Units` on this type is the fighting-unit list, not the namespace.
                if (!Strategos.Units.Side.AreHostile(side, Scenario?.FindSide(other.Side)))
                    continue;

                float sq = (other.Cell - unit.Cell).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = other; }
            }

            return best;
        }

        /// <summary>
        /// Unpacks Withdraw into Abort + MoveTo away from the named (or nearest) hostile.
        /// </summary>
        /// <remarks>
        /// Same geometry as the old break-contact reflex. Threat position uses ground truth
        /// via <see cref="NearestHostile"/> when <see cref="Command.AgainstUnit"/> is empty —
        /// the #34 belief-layer shortcut drills already document. Distance is
        /// <see cref="Reactions.ReactionController.WithdrawCells"/>.
        /// </remarks>
        private void ExpandWithdraw(in Command command)
        {
            var unit = UnitOf(command.TargetUnit);
            if (unit == null) return;

            var actor = command.IssuedBy;
            Issue(Command.Abort(actor, unit.Id));

            UnitInstance threat = null;
            if (command.AgainstUnit.IsValid)
                threat = UnitOf(command.AgainstUnit);
            if (threat == null || threat.IsDestroyed)
                threat = NearestHostile(unit);

            // Prefer the caller's believed position (LastSeen from a reaction); else live cell.
            Vector2 threatPos = command.TargetCell;
            if (threatPos.sqrMagnitude < 0.0001f)
            {
                if (threat == null) return;
                threatPos = threat.Cell;
            }

            Vector2 away = unit.Cell - threatPos;
            if (away.sqrMagnitude < 0.0001f) return;

            var map = Map;
            Vector2 destination = unit.Cell +
                away.normalized * Strategos.Reactions.ReactionController.WithdrawCells;
            destination.x = Mathf.Clamp(destination.x, 0f, map.Width - 1f);
            destination.y = Mathf.Clamp(destination.y, 0f, map.Height - 1f);

            Issue(Command.MoveTo(actor, unit.Id, destination));
        }

        /// <summary>
        /// How close an Attack marches before Engaging, in cells.
        /// </summary>
        /// <remarks>
        /// Far enough that the unit is closing rather than standing off at detection range,
        /// short enough that Engage's envelope usually covers the last metres without a second
        /// march. Facing and assault formations are not modelled — this is MoveTo + Engage
        /// under one order name (#85).
        /// </remarks>
        public const float AttackStandoffCells = 8f;

        /// <summary>
        /// Unpacks Attack into MoveTo (if needed) + Engage against the named or nearest hostile.
        /// </summary>
        private void ExpandAttack(in Command command)
        {
            var unit = UnitOf(command.TargetUnit);
            if (unit == null) return;

            UnitInstance threat = null;
            if (command.AgainstUnit.IsValid)
                threat = UnitOf(command.AgainstUnit);
            if (threat == null || threat.IsDestroyed)
                threat = NearestHostile(unit);
            if (threat == null) return;

            var actor = command.IssuedBy;
            float dist = Vector2.Distance(unit.Cell, threat.Cell);
            if (dist > AttackStandoffCells)
            {
                Vector2 toward = threat.Cell - unit.Cell;
                toward.Normalize();
                var map = Map;
                Vector2 destination = threat.Cell - toward * AttackStandoffCells;
                destination.x = Mathf.Clamp(destination.x, 0f, map.Width - 1f);
                destination.y = Mathf.Clamp(destination.y, 0f, map.Height - 1f);
                Issue(Command.MoveTo(actor, unit.Id, destination));
            }

            Issue(Command.Engage(actor, unit.Id, threat.Id));
        }

        /// <summary>
        /// How far Recon stands off to observe, in cells — farther than Attack's close.
        /// </summary>
        public const float ReconStandoffCells = 24f;

        /// <summary>
        /// Unpacks Recon into MoveTo (standoff) + Screen — move to see, then hold watch (#151).
        /// </summary>
        private void ExpandRecon(in Command command)
        {
            var unit = UnitOf(command.TargetUnit);
            if (unit == null) return;

            UnitInstance threat = null;
            if (command.AgainstUnit.IsValid)
                threat = UnitOf(command.AgainstUnit);
            if (threat == null || threat.IsDestroyed)
                threat = NearestHostile(unit);
            if (threat == null) return;

            var actor = command.IssuedBy;
            float dist = Vector2.Distance(unit.Cell, threat.Cell);
            if (dist > ReconStandoffCells)
            {
                Vector2 toward = threat.Cell - unit.Cell;
                toward.Normalize();
                var map = Map;
                Vector2 destination = threat.Cell - toward * ReconStandoffCells;
                destination.x = Mathf.Clamp(destination.x, 0f, map.Width - 1f);
                destination.y = Mathf.Clamp(destination.y, 0f, map.Height - 1f);
                Issue(Command.MoveTo(actor, unit.Id, destination));
            }

            Issue(Command.Screen(actor, unit.Id));
        }

        /// <summary>
        /// How far past the threat an Exploit drives, in cells.
        /// </summary>
        public const float ExploitDepthCells = 16f;

        /// <summary>
        /// Unpacks Exploit into MoveTo *through* the threat + Engage — follow-through (#152).
        /// </summary>
        private void ExpandExploit(in Command command)
        {
            var unit = UnitOf(command.TargetUnit);
            if (unit == null) return;

            UnitInstance threat = null;
            if (command.AgainstUnit.IsValid)
                threat = UnitOf(command.AgainstUnit);
            if (threat == null || threat.IsDestroyed)
                threat = NearestHostile(unit);
            if (threat == null) return;

            var actor = command.IssuedBy;
            Vector2 toward = threat.Cell - unit.Cell;
            if (toward.sqrMagnitude < 0.0001f) toward = Vector2.right;
            else toward.Normalize();

            var map = Map;
            Vector2 destination = threat.Cell + toward * ExploitDepthCells;
            destination.x = Mathf.Clamp(destination.x, 0f, map.Width - 1f);
            destination.y = Mathf.Clamp(destination.y, 0f, map.Height - 1f);
            Issue(Command.MoveTo(actor, unit.Id, destination));
            Issue(Command.Engage(actor, unit.Id, threat.Id));
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

            var finished = entry.Command;
            queue.Finish();
            Report(SituationReport.Status(OutcomeKind(finished.Kind, outcome), unit, Tick,
                finished.Seq));

            // Delay Completes when pressed — convert into an ordered Withdraw so giving ground
            // is visible in the command log rather than a silent queue clear (#85).
            if (outcome == CommandOutcome.Completed && finished.Kind == CommandKind.Delay)
                Issue(Command.Withdraw(finished.IssuedBy, unit.Id));
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
                {
                    // Stamped here because this is the only moment the information exists.
                    // Afterwards the sole evidence is a strength of zero, which cannot say
                    // when it happened or what did it.
                    defender.DestroyedAtTick = Tick;
                    Casualties.Add(new Casualty(defender.Id, defender.Side, attacker.Id, Tick));
                    Report(SituationReport.Status(ReportKind.Destroyed, defender, Tick));
                }

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
        /// state, what was reported *and* what was directed: a replay that lands units in the
        /// right places with the wrong plans has still diverged and would drift apart on the
        /// next step; one that produces different reports has diverged in what its commander
        /// knows, which is exactly what an AI or a reacting unit will act on; and two runs that
        /// land identical units and reports while disagreeing on which directives from higher
        /// were ever published have diverged in what the *player* was told —
        /// <c>DirectiveLog.Signature()</c> is folded in for the same reason
        /// <c>ReportLog.Signature()</c> already is.
        ///
        /// <c>DirectiveResponses</c> — the player's answers to a directive (#94) — is
        /// deliberately NOT folded in here, and that is a decision rather than an oversight.
        /// Acknowledging one already produces a <see cref="ReportKind.DirectiveAcknowledged"/>
        /// report, which <c>ReportLog.Signature()</c> already covers (it includes
        /// <c>AboutDirective</c>); two runs that disagree on whether a directive was
        /// acknowledged already disagree there. Folding <c>DirectiveResponses</c> in as well
        /// would assert the same fact twice and move every archived baseline for it — see
        /// <c>_acknowledgedDirectives</c>'s own note on why that set stays outside this method
        /// for the identical reason.
        /// </summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append("t").Append(Tick).Append('|').Append(ReportLog.Signature()).Append('|');
            sb.Append(DirectiveLog.Signature()).Append('|');
            Casualties.AppendSignature(sb);
            sb.Append('|');
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

        // ─── Save/load (#74) ─────────────────────────────────────────────────
        //
        // SNAPSHOT, NOT LOG REPLAY. #74 decided this: replay is exact only as long as executor
        // and reaction behaviour is unchanged, and this project changes both often — training
        // and fatigue each moved every divergence baseline in the week before #74 was written.
        // A save that stops loading the moment gameplay is retuned is worth less than one that
        // survives a patch.
        //
        // Signature() ANSWERS A DIFFERENT QUESTION THAN A SNAPSHOT NEEDS ANSWERED. It exists to
        // catch divergence — did two runs of the SAME code end up in different states — and
        // deliberately omits anything derivable or anything that cannot differ within one run.
        // Both of those are exactly the state a snapshot is most likely to drop: derivable state
        // still has to be put back, just without re-deriving it via replay, and "cannot differ
        // within one run" says nothing about whether it survives being torn down and rebuilt.
        // See docs/simulation-invariants.md's own note on Signature(), and the state audit in
        // #74's PR description for the field-by-field reasoning this method is built from.

        /// <summary>Captures everything needed to resume this run exactly, as data.</summary>
        public Persistence.SimulationSnapshot Snapshot()
        {
            var snap = new Persistence.SimulationSnapshot
            {
                ScenarioJson = Scenarios.ScenarioIO.ToJson(Scenario),
                Tick = Tick,
            };

            for (int i = 0; i < _units.Count; i++)
            {
                var u = _units[i];
                snap.Units.Add(u.Clone());

                var queue = QueueOf(u.Id);
                var entries = new List<QueuedCommand>();
                if (queue != null)
                    for (int e = 0; e < queue.Entries.Count; e++) entries.Add(queue.Entries[e]);
                snap.Queues[u.Id.Value] = entries;
            }

            snap.CommandLog.AddRange(Log.Entries);
            snap.ReportLog.AddRange(ReportLog.Entries);
            snap.DirectiveLog.AddRange(DirectiveLog.Entries);
            snap.DirectiveResponses.AddRange(DirectiveResponses.Entries);
            snap.Casualties.AddRange(Casualties.Entries);

            snap.CommandBusPending.AddRange(Bus.Pending);
            snap.ReportBusPending.AddRange(Reports.Pending);
            snap.DirectiveBusPending.AddRange(Directives.Pending);

            if (_contacts != null)
            {
                snap.ContactsSeen = _contacts.SnapshotSeen();
                snap.ContactsPending = _contacts.SnapshotPending();
            }

            if (Victory != null)
            {
                snap.HasVictory = true;
                snap.VictoryOwner = Victory.SnapshotOwner();
                snap.VictoryHeldSince = Victory.SnapshotHeldSince();
                snap.VictoryOccupiedSince = Victory.SnapshotOccupiedSince();
                snap.VictoryStartingStrength = Victory.SnapshotStartingStrength();
                snap.VictoryOutcome = Victory.Outcome;
            }

            if (Director != null) snap.DirectorLastOrdered = Director.SnapshotLastOrdered();

            return snap;
        }

        /// <summary>
        /// Rebuilds a Simulation from a snapshot.
        /// </summary>
        /// <remarks>
        /// Bare on return, exactly as a freshly constructed Simulation is: the caller must
        /// <see cref="AddExecutor"/> the same executors the saved run had, and call
        /// <see cref="EnableReactions"/> / <see cref="EnableDirector"/> again — followed by
        /// <see cref="RestoreReactionPicture"/> / <see cref="RestoreDirectorMemory"/> — if the
        /// saved run had them. Executors and controllers are behaviour, not data; see
        /// <see cref="Persistence.SimulationSnapshot"/>'s header.
        /// </remarks>
        public static Simulation Restore(Persistence.SimulationSnapshot snapshot,
            UnitCatalogue catalogue = null)
        {
            if (snapshot == null) throw new System.ArgumentNullException(nameof(snapshot));

            var scenario = Scenarios.ScenarioIO.FromJson(snapshot.ScenarioJson);
            var map = scenario.GenerateMap();
            var sim = new Simulation(scenario, map, catalogue);

            sim.Tick = snapshot.Tick;

            // Overwritten unit by unit, matched by Id rather than trusted by index — Hierarchy's
            // leaf order matches Scenario.Units' order today, but matching by Id is what stays
            // correct if that ever stops being true.
            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                var saved = snapshot.Units[i];
                var live = sim.UnitOf(saved.Id);
                if (live == null) continue;
                CopyInto(live, saved);
            }

            for (int i = 0; i < sim._units.Count; i++)
            {
                var id = sim._units[i].Id;
                var queue = sim.QueueOf(id);
                if (queue == null) continue;

                if (snapshot.Queues != null && snapshot.Queues.TryGetValue(id.Value, out var entries))
                    queue.RestoreEntries(entries);
                else
                    queue.RestoreEntries(System.Array.Empty<QueuedCommand>());
            }

            sim.Log.RestoreEntries(snapshot.CommandLog);
            sim.ReportLog.RestoreEntries(snapshot.ReportLog);
            sim.DirectiveLog.RestoreEntries(snapshot.DirectiveLog);
            sim.DirectiveResponses.RestoreEntries(snapshot.DirectiveResponses);
            sim.Casualties.RestoreEntries(snapshot.Casualties);

            sim.Bus.LoadPending(snapshot.CommandBusPending);
            sim.Reports.LoadPending(snapshot.ReportBusPending);
            sim.Directives.LoadPending(snapshot.DirectiveBusPending);

            sim._contacts?.Restore(snapshot.ContactsSeen, snapshot.ContactsPending);

            // _acknowledgedDirectives is deliberately outside Signature() and is derivable from
            // DirectiveResponses, already restored above — see that field's own remarks and
            // HasAcknowledged. Rebuilt directly here rather than by replaying through
            // AcknowledgeDirective, which would re-append to a log already restored and
            // re-publish a report ReportLog already holds.
            sim._acknowledgedDirectives.Clear();
            for (int i = 0; i < snapshot.DirectiveResponses.Count; i++)
            {
                var r = snapshot.DirectiveResponses[i];
                if (r.Kind == Strategos.Directives.DirectiveResponseKind.Acknowledged)
                    sim._acknowledgedDirectives.Add(r.DirectiveSeq);
            }

            // _openingReported is deliberately outside Signature() too, for the same shape of
            // reason as _acknowledgedDirectives: it guards "has this Engage order already
            // published its one Engaged report" against every further tick it keeps firing, and
            // it is derivable from ReportLog — every ReportKind.Engaged entry's AboutCommand is
            // the Command.Seq of the order that opened. Left unreconstructed, a restored
            // Simulation would republish Engaged for the very next tick of any engagement still
            // running across the snapshot boundary — the guard exists exactly to stop that, and
            // an empty one after restore is the same defect #94 found in AcknowledgeDirective,
            // one field over.
            sim._openingReported.Clear();
            for (int i = 0; i < snapshot.ReportLog.Count; i++)
            {
                var r = snapshot.ReportLog[i];
                if (r.Kind == ReportKind.Engaged) sim._openingReported.Add(r.AboutCommand);
            }

            if (snapshot.HasVictory && sim.Victory != null)
                sim.Victory.RestoreState(snapshot.VictoryOwner, snapshot.VictoryHeldSince,
                    snapshot.VictoryOccupiedSince, snapshot.VictoryStartingStrength,
                    snapshot.VictoryOutcome);

            return sim;
        }

        /// <summary>
        /// Rebuilds <see cref="Reactions"/>' picture from reports the original run had already
        /// delivered by the snapshot's tick. Call after <see cref="EnableReactions"/>, and only
        /// when the saved run had reactions enabled.
        /// </summary>
        /// <remarks>
        /// Derived, not stored — see <see cref="Reactions.ReactionController.RebuildFrom"/>. The
        /// filter against <see cref="Persistence.SimulationSnapshot.ReportBusPending"/> is what
        /// stops a report the original had merely *published* this tick, and not yet delivered,
        /// from reaching a restored unit's reflexes a step early.
        /// </remarks>
        public void RestoreReactionPicture(Persistence.SimulationSnapshot snapshot)
        {
            if (Reactions == null || snapshot == null) return;

            var pendingSeqs = new HashSet<ulong>();
            for (int i = 0; i < snapshot.ReportBusPending.Count; i++)
                pendingSeqs.Add(snapshot.ReportBusPending[i].Seq);

            var delivered = new List<SituationReport>();
            for (int i = 0; i < snapshot.ReportLog.Count; i++)
                if (!pendingSeqs.Contains(snapshot.ReportLog[i].Seq))
                    delivered.Add(snapshot.ReportLog[i]);

            Reactions.RebuildFrom(delivered);
        }

        /// <summary>
        /// Restores <see cref="Director"/>'s retry memory. Call after <see cref="EnableDirector"/>,
        /// and only when the saved run had a director enabled.
        /// </summary>
        public void RestoreDirectorMemory(Persistence.SimulationSnapshot snapshot)
        {
            if (Director == null || snapshot?.DirectorLastOrdered == null) return;
            Director.RestoreLastOrdered(snapshot.DirectorLastOrdered);
        }

        /// <summary>
        /// Every field <see cref="UnitInstance.Clone"/> copies, restored back onto a live unit.
        /// Kept as its own method so it stays the one place that has to be updated in step with
        /// <c>Clone</c> when the model grows a field — the same failure mode #74's audit exists
        /// to catch, applied to the restore path rather than the save path.
        /// </summary>
        private static void CopyInto(UnitInstance live, UnitInstance saved)
        {
            live.Side = saved.Side;
            live.ParentId = saved.ParentId;
            live.Sidc = saved.Sidc;
            live.Designation = saved.Designation;
            live.HigherFormation = saved.HigherFormation;
            live.CapabilityId = saved.CapabilityId;
            live.Cell = saved.Cell;
            live.Strength = saved.Strength;
            live.DestroyedAtTick = saved.DestroyedAtTick;
            live.Readiness = saved.Readiness;
            live.Suppression = saved.Suppression;
            live.Training = saved.Training;
            live.Posture = saved.Posture;
            live.Roe = saved.Roe;
            live.Supply = saved.Supply;
        }
    }
}
