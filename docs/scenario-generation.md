# Scenario generation

Procedural *content* for a single `Scenario` — ORBAT, objectives, victory — on top of
already-procedural terrain. **Read before touching `ScenarioGenerator` /
`ScenarioGenerationSettings`.** [CLAUDE.md](../CLAUDE.md) is the index.

Cross-references: epic [#334](https://github.com/aawadall/strategos/issues/334)
(children #347–#352), [#51](https://github.com/aawadall/strategos/issues/51) (objective
feature placement — reuse, do not duplicate), [#332](https://github.com/aawadall/strategos/issues/332) /
[#333](https://github.com/aawadall/strategos/issues/333) (historical authored content — the
opposite approach), `docs/phases.md` §6.3 (campaign *sequencing*, a different problem).

---

## What this is

`MapGenerator.Generate` already varies ground from `MapGenerationSettings`. What was still
hand-authored in `ScenarioSamples` is everything else a `Scenario` needs: sides, units,
objectives, victory. `ScenarioGenerator.Generate(settings)` builds those from:

| Parameter | Role |
|---|---|
| `Echelon` | Root formation echelon per side; leaves sit one rung below |
| `ForceRatio` | Enemy leaf count ÷ friendly leaf count |
| `Engagement` | Meeting / Defend / Attack → victory templates + objective `InitialOwner` |
| Map fields | Seed, size, profile, erosion — forwarded to `MapGenerationSettings` |

`SideEnv.Create` takes an already-built `Scenario`. This generator is what feeds varied
episodes into that path without hand-writing every ORBAT.

Generated scenarios are **not** committed to PLAY's picker in this epic — training /
probe use is enough. Shipping them as player content is a later decision.

---

## What "valid" means (#348)

A generator with no gate is the same failure class as maps that generate unplayable.
Before train or play, `ScenarioGenerator.ValidateGenerated(scenario, map, settings, catalogue)`
must return an empty problem list. That combines:

1. **`Scenario.Validate(catalogue, map)`** — name, sides, unique ids, SIDC, capability ids,
   start cells passable, unit–unit reachability flood-fill, objectives passable for every
   fielded capability, victory references intact.
2. **Per-side path to every objective** — `ShippedMapProbe`-style: for each side that has
   leaves, at least one leaf must `PathFinder.Find` to the objective cell. Flood-fill
   reachability between units is not enough if every unit sits in a connected pocket that
   never touches the objective.
3. **Force balance** — leaf counts exist on both sides, and
   `enemyLeaves / friendlyLeaves` lies within `ForceRatio ± ForceRatioTolerance`.
   Authored scenarios skip this; generated ones cannot, because nothing else checks that a
   ratio did not produce a walkover or an unwinnable defence.

Objective **feature** placement (town, bridge, ridge) stays [#51](https://github.com/aawadall/strategos/issues/51).
Until then the generator stubs a centre-ish passable cell.

---

## Engagement templates (#350)

| Type | `InitialOwner` | Victory shape |
|---|---|---|
| Meeting | `None` | Both: `HoldObjectives` (high) + `DestroyEnemy` (low); `TimeLimitTicks` draw |
| Defend | Player | Player: `SurviveUntil` + `HoldObjectives`; enemy: `HoldObjectives` + `DestroyEnemy` |
| Attack | Enemy | Player: `HoldObjectives` + `DestroyEnemy`; enemy: `SurviveUntil` + `HoldObjectives` |

No new `VictoryKind` — combinations of the three existing kinds.

---

## Probe

```
Strategos.Editor.ScenarioGeneratorProbe.Run
```

Asserts a meeting sample validates, Defend/Attack templates and ownership, force-ratio
assembly, and `SideEnv.Create` / `Reset` / `Step` on a generated scenario.
