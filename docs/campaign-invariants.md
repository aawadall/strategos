# Campaign invariants

`Core/Campaigns/` — a chain of linked scenarios sharing one persistent ORBAT. **Read before
touching anything under `Core/Campaigns`.** [CLAUDE.md](../CLAUDE.md) is the index.

This page covers #75 chunks 1, 2 and 3: the data shape and its JSON I/O, the carry-over logic
that turns a finished `Simulation` into the next entry's starting state, and the merge that turns
that carried-over state into the next entry's actual starting `Scenario`/`Simulation`. A
multi-operation (three-operation) probe and any wiring into PLAY/UI are still not here — see
"What is deliberately not here yet" below. Neither `CampaignCarryOver.CarryOver` nor
`CampaignChainDriver` runs inside a `Simulation` tick or is part of `Simulation.Signature()`, so
neither has anything to add to [docs/simulation-invariants.md](simulation-invariants.md)'s
replay-divergence rules. Extend this page, not that one, as later `Core/Campaigns` chunks add
behaviour.

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

## Merging carried-over units into the next operation (#75 chunk 3)

`Assets/Scripts/Core/Campaigns/CampaignChainDriver.cs` — the seam that was missing before this
chunk: `Simulation`'s constructor (Simulation.cs:175-181) builds its `UnitHierarchy` directly from
`scenario.Units`, and nothing consumed `CampaignChainEntry.CarriedOverUnits` into a fresh
`Scenario`/`Simulation` at all. `CampaignChainDriver.MergeCarriedOver` and `.StartNext` are that
consumption.

### The Id-consistency authoring rule

**A `UnitId` is a plain hand-authored int** (`Assets/Scripts/Core/Units/UnitIds.cs:31`), assigned
per-scenario by whoever writes the JSON (`ScenarioSamples.cs` — literal `new UnitId(7)`,
`new UnitId(id)`). **There is no global uniqueness guarantee across different scenario files.**
Two unrelated scenarios can both use `UnitId(1)` for completely different units.

`MergeCarriedOver` matches a carried-over unit to a next-scenario unit by `Id` alone. **This is
only correct if every scenario in a `CampaignChain` is authored to keep the same persistent
unit's `Id` consistent across every operation it appears in.** This is a requirement on the
author of a campaign's scenarios, the same register `docs/simulation-invariants.md` states rules
an author must respect in:

- A unit meant to persist across operations (a company that might survive operation 1 and fight
  in operation 2) must be given the **same `UnitId`** in both scenarios' `Units` lists.
- A unit's `Id` in one operation's scenario has no relationship to its `Id` in an *unrelated*
  scenario not in the same chain — id reuse across unrelated content is fine, exactly as it
  always has been.
- Getting this wrong is not silently tolerated: see "an unmatched carried-over unit" below.

### What merges and what does not

Per matched unit (next-scenario `Id` == a carried-over unit's `Id`):

| Field(s) | Source |
|---|---|
| `Strength`, `Readiness`, `Training`, `Roe`, `Supply.*` | The carried-over unit (chunk 2's `CarryOver` output) |
| `Cell`, `ParentId` | Always the next scenario's own authoring — never the carried-over unit's (whose `Cell` is a `Vector2.zero` sentinel; see the carry-over section above) |
| Everything else (`Sidc`, `Designation`, `HigherFormation`, `CapabilityId`, `Side`, `Posture`, `Suppression`, `DestroyedAtTick`) | The next scenario's own authoring, unconditionally — not in the merge list at all |

A next-scenario unit with **no** carried-over match keeps its own authored state entirely —
this is how a new operation introduces reinforcements, proven by assertion in
`CampaignChainDriverProbe`, not assumed.

### An unmatched carried-over unit is a campaign authoring error

A carried-over unit whose `Id` has **no** match anywhere in the next scenario's `Units` is not
silently dropped — `MergeCarriedOver` throws `InvalidOperationException`, naming every
mismatched id and unit in one message (not just the first). This mirrors
`CampaignCarryOver.CarryOver`'s own precedent (an undecided `Simulation` throws rather than
guessing) and keeps `MergeCarriedOver`'s signature exactly `Scenario MergeCarriedOver(Scenario,
List<UnitInstance>)` rather than adding an out-parameter or a problem-list return purely to carry
one error case — the unit that survived has nowhere to go in the next operation, and that is
always a mistake in how the chain's scenarios were authored (see the Id-consistency rule above),
never a runtime state to degrade gracefully from. Proven red-then-green in
`CampaignChainDriverProbe` — the throw was temporarily disabled, the probe FAILED with "silently
dropped instead of surfaced", then reverted and PASSED again.

### The rest-hours-by-outcome pattern ("defeat cost", folded into this chunk)

What was originally planned as its own chunk — "a defeat gets less rest before the next
operation" — needed no new logic once `CarryOver` already took `restHours` as a caller-supplied
parameter (chunk 2). It is a value the **driver** (or whoever calls `CarryOver` between
operations) chooses, not a feature inside `CampaignCarryOver` or `CampaignChainDriver`:

```
float restHours = finishedEntry.Outcome == OperationOutcome.Lost ? shortRest : longRest;
CampaignCarryOver.CarryOver(endedSim, finishedEntry, nextEntry, restHours);
```

`CampaignChainDriverProbe` demonstrates this with a Won duel and a Lost duel of identical
geometry (sides swapped so the player's own side is the one destroyed in the Lost case), a
longer `restHours` for the Won case, and asserts the carried survivor's recovered `Readiness` is
higher after the longer rest. That is the entire "defeat cost" story this issue asks for at this
stage — nothing else changes based on `Outcome` today.

### `StartNext`'s `CarriedOverUnits` index — a brief inconsistency, resolved by the existing data model

`CampaignChainDriver.StartNext(chain, entryIndex, ...)` reads
`chain.Operations[entryIndex].CarriedOverUnits` — **the entry's own field**, not
`chain.Operations[entryIndex - 1]`'s. This is what `CampaignChainEntry.CarriedOverUnits`'s own
doc comment (chunk 1) already says ("The ORBAT this operation starts with, carried over from the
previous entry's result") and what `CampaignCarryOver.CarryOver`'s actual behaviour (chunk 2)
already does — it writes into its `nextEntry` parameter, so a survivor pool lands on the entry it
feeds *into*. Reading `entryIndex - 1` instead would read the *previous* operation's own starting
ORBAT (empty for the first entry in a chain, someone else's survivors for any other), never what
that operation produced at the end. See `Artifacts/agents/campaign-merge-out.md` for this being
flagged back rather than silently "fixed" without a trace.

## What is deliberately not here yet

- **The three-operation acceptance probe** — #75 itself asks for three linked operations played
  end to end with one ORBAT, a formation mauled in operation one visibly weaker in operation two.
  `CampaignChainDriverProbe` (#75 chunk 3) proves a real **two**-operation round trip; extending
  it to three (or building a dedicated acceptance probe) is the next chunk's job.
- **Defeat-cost handling beyond a shorter rest** — the rest-hours-by-outcome pattern above is the
  entire "defeat cost" story so far. A resource penalty, forced starting posture, or anything else
  losing might cost the campaign is undecided and unimplemented.
- **Validation** — no `CampaignChain.Validate()`. Skipped for chunk 1 rather than done half-way: a
  scenario-name reference can only be checked meaningfully against a catalogue/map the way
  `Scenario.Validate` does, which is more than "nearly free" for a data-only chunk.
- **Wiring into PLAY/UI** — nothing under `Assets/Scripts/UI` reads or calls any of this yet.
  `CampaignChainDriver.StartNext` is a standalone function a future campaign runner will call
  between operations.

See `Artifacts/agents/campaign-chain-shape-out.md` for the chunk-1 handoff,
`Artifacts/agents/campaign-carryover-out.md` for chunk 2's, `Artifacts/agents/campaign-merge-out.md`
for chunk 3's, and what the next chunk should build on top of this.
