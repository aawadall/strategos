# Campaign invariants

`Core/Campaigns/` — a chain of linked scenarios sharing one persistent ORBAT. **Read before
touching anything under `Core/Campaigns`.** [CLAUDE.md](../CLAUDE.md) is the index.

This page covers #75 chunks 1 through 4: the data shape and its JSON I/O, the carry-over logic
that turns a finished `Simulation` into the next entry's starting state, the merge that turns
that carried-over state into the next entry's actual starting `Scenario`/`Simulation`, and the
three-operation acceptance probe proving #75's own criteria end to end. PLAY wiring is tracked
under parent [#114](https://github.com/aawadall/strategos/issues/114) — see
[campaign-play-plan.md](campaign-play-plan.md) for the child sequence (#138–#140).
`CampaignChain.Validate()` (#138) is the load-time check; PLAY/UI wiring is still open — see
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

## The three-operation acceptance probe (#75 chunk 4)

`CampaignChainDriverProbe.CheckThreeOperationChain` — #75's own two acceptance bullets, proven
directly rather than inferred from "no exception was thrown". Chain: `skirmish -> push-north ->
skirmish`, the third link reusing the first scenario — a scenario played a second time is a
legitimate campaign entry and authors nothing new, so this chunk did not add a third fixture.
All three operations are played unattended to a real decision (chunk 3's `RunToDecision`
pattern, reused verbatim). One persistent unit — BLUFOR's armor platoon, id 2, present under
the same `UnitId` in both scenarios per the Id-consistency rule above — is tracked across all
three:

- **One continuous ORBAT**: the tracked unit's `Strength` at the *start* of operation 3 is
  asserted to equal exactly what `CarryOver` wrote out of operation 2 (`Mathf.Approximately`,
  not "no exception") — not skirmish's own authored default (100) and not a regenerated value.
- **The "visibly weaker" effect is cumulative, not a single-hop artifact**: `Strength` is
  printed at the end of every operation and asserted monotonically non-increasing across all
  three, *and* asserted to have actually fallen net across the whole chain — a fixture where the
  tracked unit sat untouched in one hop would fail this even though it would still pass a bare
  non-increasing check. One real run: `98.23 -> 69.82 -> 34.28`.

### The `UnitId(9)` question, resolved

Push-north (chunk 3) authors a reinforcement, `UnitId(9)`, with no counterpart in
`skirmish.json`. Chaining `skirmish -> push-north -> skirmish` risks exactly the failure mode
the Id-consistency rule predicts: if unit 9 survived push-north, it would be carried into
operation 3 with no match, and `MergeCarriedOver` would correctly throw against
`skirmish.json`.

**Investigated rather than assumed** (see `Artifacts/agents/campaign-three-op.md` for the full
transcript): a throwaway probe ran the exact skirmish -> push-north fixture and printed every
leaf unit's `IsDestroyed` at push-north's decision. Unit 9 does not survive — it is destroyed
inside push-north's own fighting, before operation 2 ever ends. **No changes were made to
`MergeCarriedOver`, `skirmish.json`, `ScenarioSamples`, or push-north's battle script.**
`CheckThreeOperationChain` asserts this on every run (not just the one that was inspected by
hand): it fails loudly, naming the risk, if unit 9 ever survives operation 2 in a future run of
this fixture — determinism should keep it dead, but a probe that trusted its own prior
observation without re-checking would be exactly the kind of guard that cannot fail this
project's build-and-verify doc warns against.

This resolves the question for *this* fixture only. A real three-scenario campaign (distinct
content in all three slots) still needs its author to check reinforcement survival the same
way, per the Id-consistency rule above — this chunk did not change that rule or weaken
`MergeCarriedOver`'s throw.

## Validation (#138)

`CampaignChain.Validate(UnitCatalogue catalogue = null, Func<string, Scenario> load = null)`
returns every problem in one pass (empty means the chain may start), matching
`Scenario.Validate`'s shape:

- Campaign name and a non-empty operations list.
- Each entry's `ScenarioName` resolves via `ScenarioIO.Load` (or an injected `load`).
- Each loaded scenario's own `Scenario.Validate(catalogue)` problems are prefixed with the
  operation label.
- Non-empty `CarriedOverUnits` on an entry must every `Id` exist in that entry's scenario —
  otherwise `MergeCarriedOver` would throw at play time.
- Consecutive operations: any `UnitId` present in both scenarios must keep the same `Side`
  and `CapabilityId` (Id-consistency authoring rule above). Units that exist in only one
  operation are allowed (reinforcements / expected losses) — survival is not predicted here.

Probe: `Strategos > Probe Campaign Chain Validate` /
`-executeMethod Strategos.Editor.CampaignChainValidateProbe.Run`.

## Rank gates (#76)

`RankAuthority` / `RankGate` in `Core/Units/`: career rank id → max command echelon.
`AppSession.CareerRankId` (facade over `CareerProfile`) defaults to `battalion` (shipped
ORBATs). PLAY `LoadScenario` / campaign start refuse when required ORBAT echelon exceeds
authority. Winning the last campaign operation promotes one rung.

## Career across campaigns (#109)

`CareerProfile` / `CareerProfileIO` in `Core/Campaigns/`: rank id, formation designation,
and higher-formation label that outlive one `CampaignChain`. On campaign complete PLAY
stamps formation from the finished scenario; a win still promotes via `RankGate`. The
shipped `highland-campaign` opens at `PlayerEchelon = Regiment` with a directive whose
`From` is still `3 BDE` — the same higher HQ that addressed the valley seat. Probe:
`Strategos > Probe Career Across Campaigns`.

## Multi-echelon climb campaign (#403)

Seat escalation inside **one** chain (Squad → Company → Battalion) is specified in
[climb-campaign.md](climb-campaign.md) (#404 design note). Shipped as
`CampaignSamples.ClimbName` / `Assets/Resources/Campaigns/climb-campaign.json`
(#406) over `climb-squad` → `climb-company` → `climb-battalion` (#405). Third
shipped campaign beside Valley and Highland — not a replacement for either, and
not the #289 tutorial. Menu/PLAY entry is #407; carry-over climb probe is #408.

## What is deliberately not here yet

- **Defeat-cost handling beyond a shorter rest** — the rest-hours-by-outcome pattern above is the
  entire "defeat cost" story so far. A resource penalty, forced starting posture, or anything else
  losing might cost the campaign is undecided and unimplemented.
- **Disk-backed career across process restarts** — `CareerProfile` is session memory with JSON
  IO for probes and a Resources default; wiring it into `IGameStore` / save records is still
  open under the broader player-profile story.

See `Artifacts/agents/campaign-chain-shape-out.md` for the chunk-1 handoff,
`Artifacts/agents/campaign-carryover-out.md` for chunk 2's, `Artifacts/agents/campaign-merge-out.md`
for chunk 3's, `Artifacts/agents/campaign-three-op-out.md` for chunk 4's (this one), and what the
next chunk should build on top of this.
