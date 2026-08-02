# Campaign invariants

`Core/Campaigns/` — a chain of linked scenarios sharing one persistent ORBAT. **Read before
touching anything under `Core/Campaigns`.** [CLAUDE.md](../CLAUDE.md) is the index.

This page currently covers #75 chunk 1 only: the data shape and its JSON I/O. There is no
simulation behaviour here yet — no carry-over logic, no readiness recovery, no defeat-cost
computation — so there is nothing here yet that can diverge a replay the way
[docs/simulation-invariants.md](simulation-invariants.md) describes for `Core/Commands` and its
neighbours. Extend this page, not that one, as later chunks add behaviour.

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

## What is deliberately not here yet

- **Carry-over logic** — reading a played `Simulation`'s or a `SaveRecord`'s final ORBAT state
  into the next entry's `CarriedOverUnits`.
- **Readiness recovery** — the user has already decided readiness partially recovers between
  operations (#75); the curve is not decided or computed here, only the field it will write
  into (`UnitInstance.Readiness`, already part of the reused shape).
- **Defeat-cost handling** — `OperationOutcome.Lost` records that an operation was lost; what
  losing actually costs the campaign is undecided and unimplemented.
- **Validation** — no `CampaignChain.Validate()`. Skipped for this chunk rather than done
  half-way: a scenario-name reference can only be checked meaningfully against a catalogue/map
  the way `Scenario.Validate` does, which is more than "nearly free" for data-only chunk 1.
- **A multi-operation probe** — `CampaignChainProbe` (#75 chunk 1) proves one hand-built chain
  round-trips through JSON; it does not play three linked operations end to end. That is part
  of #75's acceptance criteria and belongs to a later chunk once carry-over logic exists to
  exercise.

See `Artifacts/agents/campaign-chain-shape-out.md` for the chunk-1 handoff and what the next
chunk should build on top of this.
