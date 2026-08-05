# SFX inventory (#249 / #44)

Checklist of one-shot cues Strategos needs. **No assets yet** — this page is the
shopping list for #250–#252 (procedural stubs first) and later sourced clips under
`#41`. Runtime entry point is `AudioService.PlayOneShot` / `PlayOneShotResource`
([audio.md](audio.md)).

Sourcing / licence rules: [assets.md](assets.md) (freesound CC0, Sonniss GDC).
When a real file ships, record it in [ATTRIBUTIONS.md](../ATTRIBUTIONS.md).

---

## Resource naming

Shipped one-shots load from `Resources/Audio/Sfx/` (same Resources rule as music
beds — loadable by name on every target). Suggested names (no extension):

| Id | Resource path |
|---|---|
| `ui-click` | `Audio/Sfx/ui-click` |
| `ui-select` | `Audio/Sfx/ui-select` |
| `order-issued` | `Audio/Sfx/order-issued` |
| `order-rejected` | `Audio/Sfx/order-rejected` |
| `combat-fire` | `Audio/Sfx/combat-fire` |
| `combat-hit` | `Audio/Sfx/combat-hit` |
| `unit-destroyed` | `Audio/Sfx/unit-destroyed` |

Procedural stubs (#250–#252) may synthesise in memory and never touch Resources;
keep these ids stable so a later OGG drop-in does not rename call sites.

---

## UI

| Cue | Trigger (intended) | Priority | Notes |
|---|---|---|---|
| Click / press | Button / tab / toggle | **P0 — #250** | Procedural sine via `AudioService.PlayUiClick` |
| Unit select | Map / ORBAT select | P0 | `PlayUiSelect` — distinct pitch from click |
| Order issued | `Simulation.Issue` accepted (Seq ≠ 0) | **P1 — #251** | `PlayOrderIssued` via PlayView.IssuePlayer |
| Order rejected | Scope refuse / DigIn gate | **P1 — #251** | `PlayOrderRejected` |
| Alert / contact | Situation feed observation | P2 | Duck under music; debounce at high compression |
| Menu navigate | Main menu ↔ settings | P3 | Optional; music bed already covers presence |

Out of inventory for now: keyboard remapping blips, modal open/close, save/load
chimes — add only when those UIs need them.

---

## Combat

| Cue | Trigger (intended) | Priority | Notes |
|---|---|---|---|
| Direct fire | `ReportKind.Engaged` (once per engagement) | **P1 — #252** | `PlayCombatFire` — not per combat tick |
| Hit / damage | Strength drop on target | P2 | Can share fire cue initially |
| Destroyed / wreck | Unit becomes wreck | P2 | Longer tail ok; rare |
| Incoming / under fire | Reflex return-fire start | P3 | Easy to spam — hysteresis required |

Do **not** inventory artillery, aviation, or armour loops until a scenario uses
them as first-class events. Prefer one combat one-shot over a weapon taxonomy.

Time-compression rule (same as soundtrack intensity): never fire a cue every
`Step` at ×300. Debounce per unit or per engagement window.

---

## World / ambience

| Cue | Trigger (intended) | Priority | Notes |
|---|---|---|---|
| Terrain bed | Landcover under camera | P3 | Phase 10 ambients; not #44's first cut |
| Move start | Unit begins MoveTo | P3 | Optional foot/track tick; easy to spam |
| Objective taken | VictoryEvaluator ownership flip | P2 | One-shot sting; rare |

World beds stay out of the one-shot bus — they belong on a future ambient source,
not `PlayOneShot`.

---

## Implementation order (unchanged from #44)

1. **#249** — inventory (shipped)  
2. **#250** — procedural click/select stub via `AudioService` (shipped)  
3. **#251** — order issued / rejected (shipped: `IssuePlayer` + DigIn gate)  
4. **#252** — one combat cue (shipped: `Engaged` report → `PlayCombatFire`)

Epic **#44** is closed once these four ship. Sourced OGG drop-ins remain `#41` / `#401`.
