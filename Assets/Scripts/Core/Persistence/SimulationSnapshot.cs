// SimulationSnapshot.cs
// The engine-owned shape of a saved run. Data only, same rule as Scenario and every other
// model type in Core — nothing here reads or writes a byte. Building one and consuming one
// back is Simulation.Snapshot() / Simulation.Restore(); where the bytes go afterwards is a
// different file entirely (see Assets/Scripts/Persistence/FileGameStore.cs).
//
// #74. SNAPSHOT, NOT LOG REPLAY — see Simulation.Snapshot()'s header for why. Every field below
// exists because something reachable from a Simulation is real state that
// Simulation.Signature() does not cover: the signature is a divergence oracle (did two runs
// diverge), not a completeness oracle (did a save capture everything). The state audit in
// docs/simulation-invariants.md and the PR for #74 enumerate the full reasoning; the short
// version is at each field below.
//
// WHAT IS DELIBERATELY NOT HERE:
//   - Formation (non-leaf) unit state. A formation's strength, readiness and position roll up
//     from its leaves (UnitHierarchy's own rule — "state is never stored twice") and its
//     topology comes back from ScenarioJson, so nothing about a formation is runtime state.
//   - MoveToExecutor's route cache. Routing.md: "PATHS ARE DERIVED, NOT STORED" — a route is
//     recomputed identically from the unit's position, its target and the map, all of which
//     this snapshot already reconstructs, so caching one here would be a second source of the
//     same answer.
//   - ExecutionContext (Simulation._context) and the per-tick shot list (Simulation._shots).
//     Both are scratch cleared and rebuilt every step before anything reads them — see
//     Simulation.Step()'s own ordering. Neither survives from one tick to the next inside a
//     single run, so there is nothing for a snapshot taken *between* steps to capture.
//   - Executors, ReactionController and SideDirector *themselves* — behaviour, not data. A
//     restored Simulation is bare the same way a freshly constructed one is; the caller
//     re-attaches whichever executors it was running and calls EnableReactions()/EnableDirector()
//     again, exactly as it did the first time. What those two carry that is not rebuildable by
//     re-enabling them (SideDirector's retry memory) is captured below; what is rebuildable
//     (ReactionController's picture) is not, and Simulation.Restore() rebuilds it instead of
//     storing it — see ReactionController.RebuildFrom.
//
// NO VERSION FIELD OF ITS OWN. IGameStore.SaveRecord.FormatVersion is the one gate a save is
// refused at (see that file) — a second, independent version number here would be a second
// place for "is this compatible" to be decided, and the two could disagree. This type changes
// exactly when SaveRecord's shape does, so one number covers both.

using System.Collections.Generic;
using Strategos.Commands;
using Strategos.Directives;
using Strategos.Reports;
using Strategos.Units;

namespace Strategos.Persistence
{
    public sealed class SimulationSnapshot
    {
        /// <summary>
        /// The scenario as it stood at save time, as <see cref="Scenarios.ScenarioIO"/> would
        /// write it.
        /// </summary>
        /// <remarks>
        /// Only the *structural* content is actually read back on restore — map settings,
        /// sides, objectives, victory conditions, the directive, player side, and each unit's
        /// identity/topology (Id, Side, ParentId, CapabilityId, Sidc, Designation,
        /// HigherFormation). Every runtime *value* (Cell, Strength, Readiness, ...) is
        /// overwritten from <see cref="Units"/> below regardless of what is in here, because by
        /// save time <see cref="Scenarios.Scenario.Units"/> already holds whatever the run had
        /// mutated it to — Simulation and Scenario share the same UnitInstance objects (see
        /// UnitHierarchy's header) — and that is exactly the value a restore must not trust
        /// twice. Embedding the whole scenario rather than a resource name keeps a save
        /// self-contained: it does not depend on a shipped scenario file staying byte-identical
        /// between the save and the load.
        /// </remarks>
        public string ScenarioJson;

        public int Tick;

        /// <summary>
        /// Every fighting unit's full field state — <see cref="UnitInstance.Clone"/>, one per
        /// <c>Simulation.Units</c> (leaves only; see the file header on formations).
        /// </summary>
        public List<UnitInstance> Units = new();

        /// <summary>Each leaf's live plan, keyed by <see cref="UnitId.Value"/>.</summary>
        public Dictionary<int, List<QueuedCommand>> Queues = new();

        public List<Command> CommandLog = new();
        public List<SituationReport> ReportLog = new();
        public List<Directive> DirectiveLog = new();
        public List<DirectiveResponse> DirectiveResponses = new();
        public List<Casualty> Casualties = new();

        /// <summary>
        /// Messages published during the tick the snapshot was taken, not yet delivered. See
        /// <c>Messaging.MessageBus&lt;T&gt;.Pending</c>'s own remarks for why these are real
        /// state a signature comparison cannot see: they are already in the logs above, but no
        /// consumer has acted on them, and a restore that drops them silently skips one step of
        /// in-flight orders, reports or directives.
        /// </summary>
        public List<Command> CommandBusPending = new();
        public List<SituationReport> ReportBusPending = new();
        public List<Directive> DirectiveBusPending = new();

        /// <summary>
        /// The player's knowledge — <see cref="Reports.ContactTracker.SnapshotSeen"/> and
        /// <see cref="Reports.ContactTracker.SnapshotPending"/>. Null <see cref="ContactsSeen"/>
        /// only for a scenario with zero units, which never happens for a validated one.
        /// </summary>
        public bool[] ContactsSeen;
        public List<ContactTracker.PendingContact> ContactsPending = new();

        // ─── Victory (present only when the scenario has one) ────────────────

        public bool HasVictory;
        public SideId[] VictoryOwner;
        public int[] VictoryHeldSince;
        public int[] VictoryOccupiedSince;
        public Dictionary<int, float> VictoryStartingStrength = new();
        public Objectives.ScenarioOutcome VictoryOutcome;

        // ─── Optional attachments ──────────────────────────────────────────────

        /// <summary>
        /// <see cref="Direction.SideDirector.SnapshotLastOrdered"/>, or null when the saved run
        /// had no director enabled. Not derivable from any log — see that method's remarks.
        /// </summary>
        public Dictionary<int, int> DirectorLastOrdered;
    }
}
