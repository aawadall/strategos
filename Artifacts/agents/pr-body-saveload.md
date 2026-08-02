Implements #74 (save and load a run), following #66's split. Scope: the seam and the file-backed
implementation only, per the issue and per the brief — no SQL written anywhere, no UI wiring.

## Design (already decided in #74, not revisited here)

- Snapshot, not log replay.
- `IGameStore` interface (`Core/Persistence/IGameStore.cs`) with one implementation,
  `FileGameStore` (`Assets/Scripts/Persistence/FileGameStore.cs`), deliberately outside `Core`.
- Structured columns (`SaveRecord`: `SaveId`, `ScenarioName`, `Tick`, `SavedAtUtc`) beside a JSON
  payload (`SimulationSnapshot`) — the shape a SQLite implementation would later split into real
  columns plus a JSON1 column without changing anything above it.
- `Simulation.Snapshot()` / `Simulation.Restore()` do the actual work; neither knows where the
  bytes end up.

## The thing the issue's own wording doesn't cover

#74 asks for "a round-trip signature comparison, not a field checklist" — right, and not
sufficient by itself. `Simulation.Signature()` is a **divergence oracle**, built to answer "did
two runs of the same code diverge" — not a **completeness oracle**. It deliberately omits
anything derivable and anything that cannot differ *within* one run, and both of those are
exactly the state a snapshot is most likely to drop, invisibly, because a signature comparison
cannot see either omission.

So this PR is built around an audit, not around the signature. Every row below is a piece of
state reachable from a `Simulation`, classified, with its own handling and (for everything not
already in `Signature()`) its own dedicated assertion.

## The state audit

| State | Owner | Class | Handling |
|---|---|---|---|
| `Tick` | `Simulation` | In `Signature()` | round-trip covers it |
| Report entries | `ReportLog` | In `Signature()` | `RestoreEntries` (recomputes `_nextSeq`) |
| Directive entries | `DirectiveLog` | In `Signature()` | `RestoreEntries` |
| Casualty entries | `CasualtyLog` | In `Signature()` | `RestoreEntries` — **found `Casualty` was a `readonly struct` with `readonly` fields, silently dropped by `FieldsOnlyResolver`. Fixed at the source; see Findings.** |
| Objective owner/held-since/occupied-since, `Outcome` | `VictoryEvaluator` | In `Signature()` | `RestoreState` |
| `Cell`, `Strength`, `Readiness`, `Suppression`, `Posture`, `Supply.Ammunition` | `UnitInstance` | In `Signature()` | `UnitInstance.Clone()` / `Simulation.CopyInto`, matched by `Id`. **Redundant today** with the embedded `ScenarioJson` — see Findings. |
| `Kind`/`Status`/`TicksExecuting`/`TicksPending`/`TargetCell` per queued entry | `CommandQueue` | In `Signature()` | `RestoreEntries` |
| `_acknowledgedDirectives` | `Simulation` | Derivable (from `DirectiveResponseLog`) | Reconstructed on restore. Assertion 3a. **RED observed**: disabling this passes round-trip and fails step-after-restore — the exact case the brief warns about (#92's own concern). |
| `_openingReported` | `Simulation` | Derivable (from `ReportLog`'s `Engaged.AboutCommand`) | Reconstructed on restore. Assertion 3i. **Found on a deliberate second read of `Simulation.cs`, after the probe was already green.** RED observed. |
| `ContactTracker._seen` | `ContactTracker` | Real state, outside `Signature()` | `Snapshot`/`Restore`. Assertion 3b. RED observed. |
| `ContactTracker._pending` | `ContactTracker` | Real state, outside `Signature()`, **not derivable** (a held report has not reached `ReportLog` yet) | Same call as `_seen`; fixture forces a held report via `Training = 0`. |
| `VictoryEvaluator._startingStrength` | `VictoryEvaluator` | Real state, outside `Signature()` | `RestoreState`. Assertion 3f. **RED observed, real bug class**: a fresh construction recomputes this from *current* (already-damaged) unit strength, not the true tick-zero baseline. Round-trip stays green; only a later `RemainingFraction`/`DestroyEnemy` read is wrong. |
| `Training`, `Roe`, `Supply.{Rations,Water,Fuel}` | `UnitInstance` | Real state, outside `Signature()` | Same `Clone`/`CopyInto` path. Assertion 3c. |
| `DestroyedAtTick`, `Id`, `Side`, `ParentId`, `Sidc`, `Designation`, `HigherFormation`, `CapabilityId` | `UnitInstance` | Structural / immutable at runtime | Same path; not independently RED-tested (see "not done" below). |
| Full order history beyond the live queue | `CommandLog` | Real state, outside `Signature()` (it folds in the *queue's* signature, not the log's) | `RestoreEntries`. Assertion 3d — an empty queue on both sides either way, so only reading `Log.Count` directly proves it. |
| Directive-response entries | `DirectiveResponseLog` | Deliberately outside `Signature()` already (#94) | `RestoreEntries`; consumed to rebuild `_acknowledgedDirectives`. |
| Messages published on the snapshot's own tick, not yet delivered | `MessageBus<T>` ×3 | Real state, outside `Signature()` | New `Pending`/`LoadPending` on the generic bus. Assertion 3e. **RED observed** — textbook "matches at restore, diverges next step." |
| `SideDirector._lastOrdered` | `SideDirector` | Real state, **not derivable** (a director order and a player order are logged under the same `ActorId`) | `SnapshotLastOrdered`/`RestoreLastOrdered`. Assertion 3g. |
| `ReactionController._pictures` | `ReactionController` | Derivable (from delivered `ReportLog`) | `RebuildFrom`. Assertion 3h. |
| `_context`, `_shots` | `Simulation` | Genuinely transient — cleared/reassigned every `Step()`/`ResolveEngagements()`, never read across ticks | Not serialised. |
| `MoveToExecutor._routes`/`_grids` | `MoveToExecutor` | Derivable — the file's own header: "PATHS ARE DERIVED, NOT STORED" | Recomputed lazily next `Step()`. |
| Executors, `ReactionController`/`SideDirector` instances | `Simulation` | Behaviour, not data | Caller re-attaches after `Restore`, same as constructing fresh. |
| Diagnostic counters (`OrdersIssued`, `ReportsSeen`) | both controllers | Genuinely transient/diagnostic | Not restored; documented as such at their own declarations. |
| `Scenario` itself | embedded `ScenarioJson` | Content/structural | Whole, via the existing `ScenarioIO` path. Also carries current unit *values* as a side effect — see Findings. |

## Findings that don't fit the table

- **`Casualty` was a `readonly struct` with `readonly` fields.** `FieldsOnlyResolver` filters on
  `FieldInfo.IsInitOnly`, true for a `readonly` field exactly like a computed property — every
  `Casualty` would have serialised as `{}`. Since `CasualtyLog` is inside `Signature()`, this
  would have failed the round-trip probe loudly, not shipped silently — but it's the identical
  shape of bug this whole exercise is about, caught by reading what the resolver filters on
  rather than trusting the constructor. Fixed at the source; `docs/unity-gotchas.md` gained the
  general rule (`UnitId`/`SideId` already state the same reasoning for their own field, and
  nothing had connected the two).
- **`UnitInstance.Clone()`/`CopyInto` is redundant with `ScenarioJson` today.** `Scenario.Units`
  and `Simulation.Units` are the same objects (`UnitHierarchy`'s own header), so by the time a
  snapshot serialises `Scenario`, it already carries every unit's *current* values as a side
  effect nobody asked for. Two RED attempts against `CopyInto` (`Cell`, then `Strength`) both
  stayed green because of this — recorded rather than hidden. Kept the explicit path anyway:
  depending on an implicit object-sharing invariant to carry state correctly is fragile, and a
  campaign feature (#75) is a plausible reason `Simulation` stops sharing those references.
- **`_openingReported` was absent from the first version of the audit**, found on a deliberate
  second read after the probe was already green — offered as evidence the process the brief
  asked for actually found something, not as a claim the table above is now exhaustive.

## The three assertions, and what was observed RED

1. **Round-trip** — snapshot a scripted run at tick 25, restore, compare `Signature()`.
2. **Step-after-restore** — continue the *same* script (fresh `Issue` calls, not replay) on both
   sides from 25 to 55, including a second `AcknowledgeDirective` at tick 26 specifically to
   exercise idempotence across the boundary. Compare `Signature()` again.
3. **One assertion per audit row outside `Signature()`** — nine of them, each comparing the
   restored value directly.

| Break | Went red | Transcript |
|---|---|---|
| `CommandQueue` restore disabled | 1 **and** 2 | `saveload-red-queue.log` |
| `_acknowledgedDirectives` reconstruction disabled | 2 and 3a only — **1 stays green** | `saveload-red-ack.log` |
| `ContactTracker.Restore` disabled | 2 and 3b | `saveload-red-contacts.log`, `-contacts-2.log` |
| `VictoryEvaluator` starting-strength passed `null` | 2 and 3f — **1 stays green** | `saveload-red-startstrength.log` |
| `_openingReported` reconstruction disabled | 3i | `saveload-red-openingreported.log` |
| `FileGameStore.Load`'s version gate disabled | version refusal | `saveload-red-version.log` |
| `CopyInto`'s `Strength`/`Cell` lines removed | **neither went red** (see Findings) | `saveload-red-strength.log`, `saveload-red-cell.log` |

Every RED reverted immediately after capture. Final green: `saveload-green-final.log`,
`saveload-green-openingreported.log`. All transcripts under `Artifacts/agents/saveload-*.log`.

## Baseline — full suite, before vs. after

19 pre-existing probes + `SaveLoadProbe`, all `PASSED`. Every existing probe's printed block
diffed byte-identical against the committed baseline (`Artifacts/agents/full-*.log`) except one
incidental "Curl error 42" line in the old `FatigueProbe` capture — Unity's own analytics
telemetry, unrelated to fatigue output. **Nothing moved.**

```
                 master (baseline)          this branch
CasualtyProbe    PASSED   7s                PASSED   8s
CombatProbe      PASSED   6s                PASSED   6s
CommandProbe     PASSED   9s                PASSED   9s
DefendProbe      PASSED   7s                PASSED   7s
DirectiveProbe   PASSED   9s                PASSED   9s
DirectorProbe    PASSED   8s                PASSED   8s
DoctrineProbe    PASSED   6s                PASSED   6s
DrillProbe       PASSED   8s                PASSED   8s
EchelonProbe     PASSED   6s                PASSED   6s
FatigueProbe     PASSED   6s                PASSED   6s
HierarchyProbe   PASSED   7s                PASSED   7s
MapMeshProbe     PASSED   7s                PASSED   7s
ReactionProbe    PASSED   7s                PASSED   6s
ReportProbe      PASSED   8s                PASSED   8s
ScenarioProbe    PASSED   7s                PASSED   7s
ShippedMapProbe  PASSED   6s                PASSED   6s
TrainingProbe    PASSED   6s                PASSED   6s
UnitModelProbe   PASSED   6s                PASSED   6s
VictoryProbe     PASSED   6s                PASSED   6s
SaveLoadProbe    —                          PASSED   13s   (new)
```

Build: `Errors: 0 | Warnings: 0`, twice (before and after the `_openingReported` fix).
`capture.ps1 -View play`: PLAY view renders correctly (reviewed directly, not committed — no
existing precedent for committing screenshots in this repo). `Player.log`: clean.

## What this does not do

- **No embedded database.** `FileGameStore` is the only `IGameStore` implementation. No SQL was
  written at any point — the follow-up #66/#74 both call for.
- **No UI wiring.** No save/load button anywhere in `Assets/Scripts/UI`. Said so plainly rather
  than implying otherwise.
- Did not RED-test every individual identity field on `UnitInstance`
  (`Sidc`/`Designation`/`HigherFormation`/`CapabilityId`/`ParentId`/`Side`/`Id`) — covered by the
  round-trip assertion and by construction (nothing mutates them during play); a field-by-field
  RED sweep of seven more static fields felt like the "field checklist" the brief argues against,
  once the mechanism was already proven for the fields that *do* change.

## Docs

- `docs/simulation-invariants.md` — the negative-form note on what `Signature()` does and does
  not cover, with every omitted row named.
- `docs/unity-gotchas.md` — the `readonly`-field-drops-like-a-property rule.
- `docs/build-and-verify.md` — registers `SaveLoadProbe`; updates the "once #74 lands" forward
  reference on `CommandKind` renames now that it has.

Full working record: `Artifacts/agents/save-load.md`. Handoff: `Artifacts/agents/save-load-out.md`.
