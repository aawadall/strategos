# #74 — save and load a run — handoff

PR: https://github.com/aawadall/strategos/pull/108 (open against `master`, not merged)
Branch: `feat/save-load`, pushed to origin.

## The state audit (primary deliverable)

Every piece of state reachable from a `Simulation`, classified per the brief's table:

| State | Owner | Class | Handling |
|---|---|---|---|
| `Tick` | `Simulation` | In `Signature()` | round-trip covers it |
| Report entries | `ReportLog` | In `Signature()` | `RestoreEntries` (also recomputes `_nextSeq`) |
| Directive entries | `DirectiveLog` | In `Signature()` | `RestoreEntries` |
| Casualty entries | `CasualtyLog` | In `Signature()` | `RestoreEntries` — **found `Casualty` was a `readonly struct` with `readonly` fields, which `FieldsOnlyResolver` silently drops (see Findings). Fixed at the source.** |
| Objective owner/held-since/occupied-since, `Outcome` | `VictoryEvaluator` | In `Signature()` | `RestoreState` |
| `Cell`, `Strength`, `Readiness`, `Suppression`, `Posture`, `Supply.Ammunition` | `UnitInstance` | In `Signature()` | `UnitInstance.Clone()` / `Simulation.CopyInto`, matched by `Id`. **Redundant today** with the embedded `ScenarioJson` (see Findings) — kept as an explicit, auditable path rather than relying on that coupling silently. |
| `Kind`, `Status`, `TicksExecuting`, `TicksPending`, `TargetCell` per queued entry | `CommandQueue` | In `Signature()` | `RestoreEntries` |
| `_acknowledgedDirectives` | `Simulation` | Derivable (from `DirectiveResponseLog`) | Reconstructed on restore without re-publishing. Assertion 3a. **Red observed**: disabling this passes round-trip and fails step-after-restore — the exact case the brief warns about. |
| `_openingReported` | `Simulation` | Derivable (from `ReportLog`'s `Engaged.AboutCommand`) | Reconstructed on restore. Assertion 3i. **Found on a deliberate second read after the probe was already green** — none of the original checks left an `Engage` order running across the snapshot boundary, so this gap was invisible to a first pass. Red observed. |
| `ContactTracker._seen` | `ContactTracker` | Real state, outside `Signature()` | `Snapshot`/`Restore`. Assertion 3b. Red observed. |
| `ContactTracker._pending` | `ContactTracker` | Real state, outside `Signature()`, **not derivable** (a held report has not reached `ReportLog` yet) | `Snapshot`/`Restore`, same call as `_seen`. Assertion 3b covers both; the fixture forces a held report via a unit at `Training = 0`. |
| `VictoryEvaluator._startingStrength` | `VictoryEvaluator` | Real state, outside `Signature()` | `RestoreState`. Assertion 3f. **Red observed, and it is a real bug class**: a restored `Simulation`'s constructor recomputes this from whatever `Scenario.Units` currently holds — already-damaged by save time — not the true tick-zero baseline. Round-trip stays green; only a later `RemainingFraction`/`DestroyEnemy` read is wrong. |
| `Training`, `Roe`, `Supply.{Rations,Water,Fuel}` | `UnitInstance` | Real state, outside `Signature()` | Same `Clone`/`CopyInto` path as the six signature fields. Assertion 3c. |
| `DestroyedAtTick`, `Id`, `Side`, `ParentId`, `Sidc`, `Designation`, `HigherFormation`, `CapabilityId` | `UnitInstance` | Structural / immutable at runtime | Same `Clone`/`CopyInto` path; not independently asserted beyond the round-trip and 3c, since nothing mutates these fields during play. |
| Full order history beyond the live queue | `CommandLog` | Real state, outside `Signature()` (the signature folds in the *queue's* signature, not the log's) | `RestoreEntries`. Assertion 3d — a completed-then-aborted order leaves an empty queue on both sides regardless, so only reading `Log.Count` directly proves the history survived. |
| Directive-response entries | `DirectiveResponseLog` | Deliberately outside `Signature()` already (existing doc note, #94) | `RestoreEntries`; consumed to rebuild `_acknowledgedDirectives`. |
| Messages published on the snapshot's own tick, not yet delivered | `MessageBus<T>` (`Bus`, `Reports`, `Directives`) | Real state, outside `Signature()` | New `Pending`/`LoadPending` on the generic bus. Assertion 3e (command bus; report/directive buses share the identical mechanism). **Red observed** — this is the textbook "matches at restore, diverges next step" shape. |
| `SideDirector._lastOrdered` | `SideDirector` | Real state, outside `Signature()`, **not derivable** (a director-issued order and a player-issued one are logged under the same `ActorId.ForSide`, so `CommandLog` cannot tell them apart) | `SnapshotLastOrdered`/`RestoreLastOrdered`, called by the owner after `EnableDirector`. Assertion 3g. |
| `ReactionController._pictures` | `ReactionController` | Derivable (from delivered `ReportLog`, excluding whatever is still in the report bus's pending inbox) | `RebuildFrom`, called by the owner after `EnableReactions`. Assertion 3h. |
| `_context` (`ExecutionContext`: Map/Catalogue/Tick/SecondsPerTick/Engagements) | `Simulation` | Genuinely transient | Reassigned/cleared at the top of every `Step()`; nothing survives between calls. Not serialised. |
| `_shots` | `Simulation` | Genuinely transient | Cleared and refilled once per `ResolveEngagements()` call, never read across ticks. Not serialised. |
| `MoveToExecutor._routes`/`_grids` | `MoveToExecutor` | Derivable (the file's own header: "PATHS ARE DERIVED, NOT STORED") | Recomputed lazily on the next `Step()` after restore from the unit's position, target, map and capability — all already reconstructed. Not serialised. |
| Executors, `ReactionController`/`SideDirector` instances themselves | `Simulation._executors`, `.Reactions`, `.Director` | Behaviour, not data | Caller re-attaches (`AddExecutor`, `EnableReactions`, `EnableDirector`) after `Restore`, exactly as constructing a fresh `Simulation` requires. Documented on `SimulationSnapshot`. |
| `ReactionController.OrdersIssued`/`ReportsSeen`, `SideDirector.OrdersIssued` | both | Genuinely transient / diagnostic | Documented at their own declarations as diagnostic-only; not restored. Resets to 0 after a load — a cosmetic stat, not a behavioural difference. |
| `Scenario` (map settings, sides, objectives, victory conditions, directive, player side, unit topology) | embedded `ScenarioJson` | Content/structural | Serialised whole via the existing, already-tested `ScenarioIO` path. Also — see Findings — happens to carry current runtime unit *values* too, as a side effect of `Scenario`/`Simulation` sharing `UnitInstance` objects. |

## The three probe assertions

1. **Round-trip** — `SaveLoadProbe.CheckRoundTrip`. Snapshot a scripted run at tick 25 (5 orders,
   an acknowledge, an abort), restore, compare `Signature()`.
2. **Step-after-restore** — `CheckStepAfterRestore`. Continue the *same* script (not a replay —
   fresh `Issue` calls) on both the original and the restored simulation from 25 to 55, including
   a second `AcknowledgeDirective` call at tick 26 specifically to exercise idempotence across
   the boundary. Compare `Signature()` again.
3. **One assertion per audit row that isn't "in Signature()"** — nine of them (3a–3i above),
   each comparing the restored value directly rather than inferring correctness from whether a
   signature happened to move, plus the file-store round trip and version refusal.

## Red/green, all kept under `Artifacts/agents/saveload-*.log`

| Break | Assertion(s) that went red | Transcript |
|---|---|---|
| `CommandQueue` restore disabled | 1 (round-trip) **and** 2 (step-after-restore) — queue is in `Signature()` | `saveload-red-queue.log` |
| `_acknowledgedDirectives` reconstruction disabled | 2 and 3a only — **1 stays green**, the case the brief warns about | `saveload-red-ack.log` |
| `ContactTracker.Restore` disabled | 2 and 3b | `saveload-red-contacts.log` / `-contacts-2.log` (second one after a probe fix) |
| `VictoryEvaluator` starting-strength passed as `null` | 2 and 3f — **1 stays green** | `saveload-red-startstrength.log` |
| `_openingReported` reconstruction disabled | 3i only (round-trip and step-after-restore both stay green in this run's script, since it has no ongoing combat across the boundary otherwise — 3i is what catches it) | `saveload-red-openingreported.log` |
| `FileGameStore.Load`'s version gate disabled | version refusal only | `saveload-red-version.log` |
| Two abandoned attempts (`Strength`, then `Cell`, left un-restored in `CopyInto`) | **neither went red** — see Findings, the `ScenarioJson` redundancy | `saveload-red-strength.log`, `saveload-red-cell.log` |

Every RED was reverted immediately after capture; `saveload-green-final.log` and
`saveload-green-openingreported.log` are the confirming green runs.

## Findings that don't fit the table

- **`Casualty` was a `readonly struct` with `readonly` fields.** `FieldsOnlyResolver` filters on
  `FieldInfo.IsInitOnly`, which a `readonly` field satisfies exactly like a computed property —
  every `Casualty` would have serialised as `{}`. `CasualtyLog` is inside `Signature()`, so this
  would have failed the round-trip probe loudly rather than shipped silently, but it is the same
  shape of bug the whole exercise is about. Fixed at the source (dropped `readonly`); documented
  as a general rule in `docs/unity-gotchas.md`, since `UnitId`/`SideId` already state the same
  reasoning for their own backing field and nothing connected the two.
- **The `UnitInstance.Clone()`/`CopyInto` path is redundant with `ScenarioJson` today.**
  `Scenario.Units` and `Simulation.Units` are the same objects (`UnitHierarchy`'s own header
  says so), so by the time a snapshot serialises `Scenario` via `ScenarioIO`, it already carries
  every unit's *current* field values — Cell, Strength, Training, Roe, Supply, all of it — as a
  side effect nobody asked for. My two attempted RED demonstrations against `CopyInto`
  (`Cell`, then `Strength`) both stayed green because of this: the "wrong" value was never
  reached, since the embedded scenario already had the right one. I kept the explicit path
  anyway rather than deleting it — depending on an implicit object-sharing invariant to carry
  runtime state correctly is fragile, and #75 (campaign) is a plausible reason `Simulation`
  stops sharing those references with the `Scenario` it was built from. Recording this here so
  the next person reading `CopyInto` and wondering why it never fails a probe has the answer
  without re-deriving it.
- **`_openingReported` was absent from the first version of the audit.** Caught on a deliberate
  second read of `Simulation.cs` after the probe was already fully green — see
  `docs/simulation-invariants.md`'s note on it. This is offered as evidence the process asked
  for ("check what exists; do not assume the list is complete") actually found something, not
  as a claim the audit above is now exhaustive.

## What I could not verify / did not do

- **UI wiring.** No save/load button exists anywhere in `Assets/Scripts/UI`. Not implied
  otherwise anywhere in this change. `capture.ps1 -View play` only proves the branch has not
  broken the existing PLAY view.
- **The embedded database.** Not started. `FileGameStore` (`Assets/Scripts/Persistence/FileGameStore.cs`)
  is the only `IGameStore` implementation. No SQL was written at any point.
- **Multiplayer/lockstep implications of a save mid-session are not considered.** Out of scope
  per the brief and per #74/#66.
- I did **not** write a dedicated red/green pair for every UnitInstance field individually
  (Sidc/Designation/HigherFormation/CapabilityId/ParentId/Side/Id) — these are covered by the
  round-trip assertion and by construction (nothing mutates them during play), and a
  field-by-field RED sweep of seven more fields felt like exactly the "field checklist" the
  brief is arguing against, once the mechanism (`Clone`/`CopyInto`, one line per field, same
  place a new field has to be added) was already proven correct for the fields that *do*
  change.
- Two RED attempts against `CopyInto` (`Strength`, `Cell`) produced no observable failure,
  for the reason in Findings above — recorded rather than deleted, since a red attempt that
  turns out uninformative is itself information.

## Things I found wrong or underspecified in the brief

- Nothing. The brief's central claim — that a signature-only probe is a field checklist wearing
  a signature comparison's clothes — held up exactly as stated, twice: once for
  `_acknowledgedDirectives` (as predicted) and once for `_openingReported` (not named in the
  brief, found by following its own instruction).

## Verification checklist

- [x] All three probe assertions observed RED by breaking what each guards; transcripts kept.
- [x] All three GREEN; transcripts kept.
- [x] Version refusal exercised against a deliberately bad save; observed RED and GREEN.
- [x] Full suite, 19 existing + SaveLoadProbe, all PASSED. Every existing probe's printed block
      diffed byte-identical against the committed baseline, except one incidental Unity-analytics
      line in the old `FatigueProbe` capture (unrelated to fatigue output). Nothing moved.
- [x] Build; gateway assertion on `Errors: (\d+)` — `Errors: 0 | Warnings: 0`, twice (before and
      after the `_openingReported` fix).
- [x] `capture.ps1 -View play` — PLAY view renders correctly, reviewed directly. UI was not
      wired for save/load; said so plainly above rather than implying otherwise.
- [x] `Player.log` clean — no exceptions, no stack traces.
- [x] `docs/simulation-invariants.md` gained the negative-form note on what `Signature()` does
      and does not cover.
- [x] `docs/unity-gotchas.md` updated for the `readonly`-field finding.
- [x] Commit per step (5 commits). Branch `feat/save-load`, PR against `master`, not merged.
