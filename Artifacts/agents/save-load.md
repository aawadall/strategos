# #74 — save and load a run — scratchpad

Branch `feat/save-load`. Brief: `Artifacts/agents/save-load-in.md`.

## Order of work

1. Read CLAUDE.md, docs/simulation-invariants.md, docs/unity-gotchas.md, docs/build-and-verify.md.
2. Read issues #74 and #66 in full via `gh issue view`.
3. Read `Simulation.cs` end to end, then every log/bus/tracker/evaluator/controller it owns,
   to build the state audit *before* writing any restore code. This took longer than the code
   did and is the reason the two late fixes below were caught before shipping rather than after.
4. Wrote the audit table (see `save-load-out.md` — the authoritative copy is in the PR body).
5. Implemented the seam: `Core/Persistence/{IGameStore,SimulationSnapshot}.cs`, restore-only
   hooks on every piece of state that needed one, `Simulation.Snapshot()`/`Restore()`, then
   `Assets/Scripts/Persistence/FileGameStore.cs` outside Core.
6. Wrote `Assets/Editor/SaveLoadProbe.cs`: round-trip, step-after-restore, one assertion per
   audit row, file-store round trip, version refusal.
7. Iterated against real Unity batch-mode runs until green (compile errors, then a fixture bug
   in the contact-memory check — the scout was in range of more than one OPFOR leaf and other
   units published immediately rather than holding, so a hard-coded "expect exactly 1" was
   wrong; rewritten to count whatever the fixture produces and separate immediate from held
   traffic explicitly).
8. Observed RED for both top-level assertions together (queue restore disabled), then in
   isolation for step-after-restore (`_acknowledgedDirectives` reconstruction disabled — round
   trip stayed green, which is the exact case the brief warns about), then for four `#3` rows
   (contact memory, victory starting strength, version refusal — plus opening-reported, added
   later). Each reverted immediately after the RED log was saved.
9. **Re-read `Simulation.cs` a second time after the probe was already green**, specifically
   hunting for anything not yet in the audit table. Found `_openingReported` — a real, non-
   obvious gap: derivable from `ReportLog`, not reconstructed, and none of the existing checks
   would have caught it because none of them left an `Engage` order *running* across the
   snapshot boundary. Added `CheckOpeningReportedSurvives`, fixed the restore path, RED/GREEN
   cycle, re-ran the full suite.
10. Full 20-probe suite (19 existing + SaveLoadProbe) twice — once right after the first green
    run, once after the `_openingReported` fix. Diffed every existing probe's printed block
    against the committed baseline (`Artifacts/agents/full-*.log`): byte-identical except one
    incidental Unity-analytics line in the old FatigueProbe capture.
11. Build (Windows64), gateway-asserted on `Errors: (\d+)`. Twice, same reason as the probes.
12. `capture.ps1 -View play` — PLAY view renders correctly; reviewed the screenshot directly,
    not committed (no precedent for committing screenshots in this repo).
13. `Player.log` — clean, no exceptions.
14. Docs: `docs/simulation-invariants.md` gained the negative-form note (what `Signature()`
    does not cover, and why); `docs/unity-gotchas.md` gained the `readonly`-field-drops-like-a-
    property rule found while fixing `Casualty`; `docs/build-and-verify.md` registered the new
    probe and updated the "once #74 lands" forward reference.

## Real findings along the way (not just "wrote the code")

- **`Casualty` was a `readonly struct` with `readonly` fields.** `FieldsOnlyResolver` filters on
  `FieldInfo.IsInitOnly`, true for both a computed property *and* a `readonly` field — so every
  `Casualty` would have round-tripped as `{}`. `CasualtyLog` is inside `Signature()`, so this
  would have failed the round-trip probe outright, not shipped silently — but it is exactly the
  kind of gap the brief is about, caught by reading what the resolver actually filters on rather
  than trusting the constructor. Fixed at the source; documented in `docs/unity-gotchas.md` as a
  general rule, since `UnitId`/`SideId` already avoid it for the identical reason and nothing
  connected the two.
- **`Scenario.Units` and `Simulation.Units` are the same objects** (`UnitHierarchy`'s own
  header). This means the embedded `ScenarioJson` in a snapshot already carries current runtime
  unit values by the time it is serialised — Cell, Strength, Training, Roe, Supply, all of it —
  as a side effect of that sharing, not because anything asked it to. The explicit
  `UnitInstance.Clone()`/`CopyInto` path this change also builds is therefore *redundant* with
  that side effect today. Kept anyway, deliberately: relying on an implicit object-sharing
  invariant to carry runtime state correctly is fragile — a future refactor that gives
  `Simulation` its own copies (plausible; campaign/#75 will want exactly that split) would
  silently break saves the day nobody remembered this coupling. The explicit path is what stays
  correct either way. Documented in the PR body's audit table rather than left as a surprise for
  whoever reads the round-trip test and wonders why breaking `CopyInto`'s `Cell`/`Strength`
  lines didn't turn a check red.
- **`_openingReported` was missing from the original audit entirely.** Found on a deliberate
  second read of `Simulation.cs`, after the probe was already green. See docs/simulation-invariants.md's
  note on it and `SaveLoadProbe.CheckOpeningReportedSurvives`.
- **`MessageBus<T>`'s pending inbox is real, easy-to-miss state.** A command/report/directive
  published during the exact step a snapshot is taken sits there, already logged, not yet
  delivered. Missing it is the textbook "matches at restore, diverges next step" bug the brief
  describes — added `Pending`/`LoadPending` to the generic bus so all three topics get it from
  one change.

## What is explicitly out of scope, and why

- **The embedded database.** #74 says scope to the seam and the file-backed implementation; the
  brief repeats it. No SQL was written. `FileGameStore` is the only `IGameStore` implementation
  this change ships.
- **UI wiring.** No save/load button anywhere. `docs/simulation-invariants.md` and this
  scratchpad both say so plainly rather than implying otherwise. `capture.ps1 -View play` only
  proves the branch has not broken the existing UI, not that save/load is reachable from it.
- **`ReactionController.OrdersIssued`/`ReportsSeen` and `SideDirector.OrdersIssued`.** Diagnostic
  counters only, documented as such at their own declarations; not restored, and resetting to 0
  after a load is a cosmetic stat difference, not a behavioural one.
