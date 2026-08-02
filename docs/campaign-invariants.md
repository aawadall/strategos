# Campaign invariants

`Core/Campaigns/` — a chain of linked scenarios sharing one persistent ORBAT. **Read before
touching anything under `Core/Campaigns`.** [CLAUDE.md](../CLAUDE.md) is the index.

This page covers #75 chunks 1 and 2: the data shape and its JSON I/O, and the carry-over logic
that turns a finished `Simulation` into the next entry's starting state. Defeat-cost computation
beyond recording `Outcome`, a multi-operation probe, and any wiring into PLAY/UI are still not
here — see "What is deliberately not here yet" below. `CampaignCarryOver.CarryOver` reads a
`Simulation` and writes into `CampaignChainEntry` fields; it does not itself run inside a
`Simulation` tick and is not part of `Simulation.Signature()`, so it has nothing to add to
[docs/simulation-invariants.md](simulation-invariants.md)'s replay-divergence rules. Extend this
page, not that one, as later `Core/Campaigns` chunks add behaviour.

---

## The shape

`CampaignChain` is an ordered list of `CampaignChainEntry`, each naming a scenario
(`ScenarioName`, resolved the same way `ScenarioIO.Load`/`ScenarioSamples.SkirmishName` already
resolve a shipped scenario — a `Resources/Scenarios` file name, not a new id scheme), an
`OperationOutcome` (`Unplayed` / `Won` / `Lost` / `Drew`), and `CarriedOverUnits`.

`CampaignChainIO` mirrors `ScenarioIO` exactly: same Newtonsoft settings, same
`FieldsOnlyResolver`, same `Vector2Converter`/`ColorConverter`, reused directly rather than
duplicated — both live in the one assembly (`Strategos.Runtime.asmdef`), so `ScenarioIO`'s
`internal` resolver and converters are visible from `Strategos.Campaigns` without change.

- **`CarriedOverUnits` reuses `UnitInstance`, not a new type.** `SimulationSnapshot.Units` is
  already "every fighting unit's full field state, one per leaf, `UnitInstance.Clone()`" — the
  exact shape a carried-over ORBAT needs, since strength, readiness and casualties are already
  fields on `UnitInstance`. A parallel `CampaignUnitState` would duplicate that shape and have
  to be kept in step with it for no gain — the same reasoning `Scenario.Units` gives for using
  `UnitInstance` directly at t = 0 rather than a separate placement type.
- **Authored, not generated.** A `CampaignChain` is a fixed, ordered JSON list, hand-authored
  the same way a `Scenario` or a doctrine pack is. #75 depends on #78's own epic boundary: the
  procedural campaign generator (phases.md 6.3) is a later, separate thing, and this format must
  not be shaped around it. There is no generator here and none is planned for this issue.
- **Nothing here computes anything.** `CampaignChain`/`CampaignChainEntry` are data only, same
  rule as `Scenario`. `CarriedOverUnits` is empty on a freshly authored chain — the first entry
  starts from its own scenario's `Units` the way any scenario does, and every later entry's
  `CarriedOverUnits` is a field a later chunk writes into once an operation is played and reads
  out of before the next one starts. This chunk never populates it from a `Simulation` or a
  `SaveRecord`.

## Carry-over (#75 chunk 2)

`Assets/Scripts/Core/Campaigns/CampaignCarryOver.cs` — `CampaignCarryOver.CarryOver(Simulation
endedSim, CampaignChainEntry finishedEntry, CampaignChainEntry nextEntry, float restHours)`. A
pure function: no mutation of `endedSim`, no side effects beyond the two writes described below.
Kept out of `CampaignChain.cs` on purpose — that file is data only, this is the thing that
computes.

- **Outcome mapping.** `endedSim.Victory.Outcome` (a `ScenarioOutcome`, VictoryEvaluator.cs:26-42)
  is mapped against `endedSim.Scenario.PlayerSide` (Scenario.cs:64): the outcome's `IsDraw` maps
  to `Drew`; otherwise `Winner == PlayerSide` maps to `Won` and anything else decided maps to
  `Lost`. An undecided simulation — `endedSim.Victory` is null (no objectives or victory
  conditions at all), or non-null but `Outcome.Decided` is still false — is a caller error:
  `CarryOver` throws `InvalidOperationException` rather than guessing.
- **Wreck exclusion.** `Simulation.Units` includes destroyed units — `Simulation.Snapshot()`
  (Simulation.cs:961) adds every unit in `_units` unconditionally via `Clone()`, because a save
  needs to remember a wreck happened. A campaign carry-over is a different question: a wreck
  (`UnitInstance.IsDestroyed`, UnitInstance.cs:144 — `Strength <= 0.0001f`) does not walk into the
  next operation, so `CarryOver` filters it out before building `CarriedOverUnits`. This is
  proven red-then-green in `CampaignCarryOverProbe`, not just asserted from reading the code:
  commenting out the exclusion fails the probe with the wreck present in `CarriedOverUnits`.
- **Readiness recovery reuses `FatigueModel`, not a second formula.** `CarryOver` resets a
  surviving unit's `Posture` to `Posture.Halted` (so it is not mid-order arriving somewhere that
  no longer exists), then calls `FatigueModel.Apply(unit, engaged: false, map: null, catalogue,
  restHours * 3600f)` — with `Posture` off `Moving` and `engaged` false, `Apply` takes exactly the
  resting branch at FatigueModel.cs:89 (`Readiness = Min(100, Readiness + RecoveryPerHourResting *
  hours)`), the same call shape `FatigueProbe.RestingRecovers` already uses. The formula itself is
  not restated here — see FatigueModel.cs:89 for it.
- **What else changes and what does not**, per surviving unit: `Strength`, `Training`, `Roe`,
  `Supply.*` and identity fields carry over unchanged via `UnitInstance.Clone()`. `Cell` resets to
  `Vector2.zero` — the ended map is not the next operation's map, so a real-looking but stale
  position is worse than an explicit sentinel; the next operation's scenario placement (a later
  chunk) must not read `Cell` off `CarriedOverUnits` as a real position. `Suppression` is left as
  `Clone()` gives it (unchanged) — the chunk-2 brief did not specify a decay rule for it across a
  rest gap, and inventing one (e.g. reusing `EngagementResolver.DecaySuppression`) was judged to be
  exactly the kind of scope creep the brief warns against; flagged here rather than decided
  silently, for whoever picks this up next.

## What is deliberately not here yet

- **Defeat-cost handling** — `OperationOutcome.Lost` records that an operation was lost; what
  losing actually costs the campaign is undecided and unimplemented.
- **Validation** — no `CampaignChain.Validate()`. Skipped for chunk 1 rather than done half-way: a
  scenario-name reference can only be checked meaningfully against a catalogue/map the way
  `Scenario.Validate` does, which is more than "nearly free" for a data-only chunk.
- **A multi-operation probe** — `CampaignChainProbe` (#75 chunk 1) proves one hand-built chain
  round-trips through JSON; `CampaignCarryOverProbe` (#75 chunk 2) proves `CarryOver` in
  isolation against scripted single-operation fixtures. Neither plays three linked operations end
  to end with one ORBAT. That is part of #75's acceptance criteria and belongs to a later chunk.
- **Wiring into PLAY/UI** — nothing under `Assets/Scripts/UI` reads or calls any of this yet.
  `CarryOver` is a standalone function a future campaign runner will call between operations.

See `Artifacts/agents/campaign-chain-shape-out.md` for the chunk-1 handoff, and
`Artifacts/agents/campaign-carryover-out.md` for chunk 2's, and what the next chunk should build
on top of this.
