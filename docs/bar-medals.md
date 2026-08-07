# Bar medals — post-battle ribbons (#467)

In-game **service-ribbon / bar medals** awarded when a fight or campaign finishes.
Shoulder board ([#38](https://github.com/aawadall/strategos/issues/38) /
`RankInsignia`) shows *command rank*. Steam Achievements ([steam.md](steam.md)
Phase 10) are platform trophies. This page is the **local ribbon rack**: military
chrome for finishing content, not XP or loot.

[CLAUDE.md](../CLAUDE.md) is the index. Teaching copy after a fight stays under
[#421](https://github.com/aawadall/strategos/issues/421); historical briefing notes
are [#462](https://github.com/aawadall/strategos/issues/462) / `HistoricalNotes`.

---

## Decisions (binding for #467 children)

### Award moment

Award when a scenario or campaign operation **ends with a decisive outcome**
(win / loss / draw as the victory evaluator already reports). Do **not** award
mid-fight for individual kills — tally at decision. Replays and spectator watches may earn **Mode**
medals only when the player explicitly finishes the session — not on every tick.

### Merit thin slice (shipped)

At decision, `PostBattleReviewer` builds stats and grants:

| Id | Rule |
|---|---|
| `enemy-neutralized` | One medal per hostile in `CasualtyLog` (shown as one bar with numeral / ×N) |
| `objective-secured` | Player **won** and currently holds ≥1 objective |

UI: `PostBattlePanel` after `PlayView.ShowOutcome` — stats + earned ribbons. Esc closes.
Catalogue: `Resources/Medals/alpha-medals.json`. Probe: **Strategos → Probe Bar Medals**.

### Broad categories

Every medal belongs to exactly one `BarMedalCategory`. The rack and post-battle
strip **group by category** (stable order below). Do not invent finer trees in v1.

| Category | Meaning | First examples (ids are illustrative) |
|---|---|---|
| **Training** | Onboarding / drills | `tut-squad-complete` |
| **Campaign** | Authored chain ops | `climb-complete`, `valley-op-win`, `highland-op-win` |
| **Historical** | First-party historical fights | `lrt-complete`, `belleau-complete`, `remagen-complete` |
| **Merit** | How you fought (outcome qualifiers) | `enemy-neutralized` (numeral = kills), `objective-secured`, `no-friendly-losses` |
| **Mode** | How you played | `hotseat-win`, `spectator-finished` |

**Binding order on screen:** Training → Campaign → Historical → Merit → Mode.

Workshop / community scenarios (#464 / Workshop) do **not** grant first-party
Historical medals. Optional later: a separate **Community** category — out of
scope until Workshop ships.

### Data shape

```
BarMedalDef {
  Id            // stable string, e.g. "belleau-complete"
  Category      // BarMedalCategory
  Title         // short rack label
  Description   // one line for tooltips / post-battle
  Stripes[]     // ordered ribbon colours (renderer input)
  Device        // optional overlay mark: None | Star | Numeral | Oak
  SteamAchievementId?  // optional map for Phase 10 — unused until wired
}

BarMedalAward {
  MedalId
  EarnedAtUtc   // or sim tick + scenario name for determinism in probes
  ScenarioName? // which fight granted it
}
```

Defs live as JSON under `Resources/` (catalogue). Awards hang off career /
`IGameStore` player data (#355 / #66) — **not** inside `Scenario` JSON.

**Idempotent grants:** earning the same `Id` twice does not duplicate the rack
entry; Merit medals that should stack use a `Device` numeral or a distinct Id
(`lrt-complete` vs `lrt-perfect`) — pick one pattern per medal in the catalogue,
do not silently stack identical bars.

### Renderer (procedural)

Same contract as `RankInsignia` / `RankLadder`:

- **Geometry, not PNG farms.** Bake a small ribbon sprite from `Stripes` + `Device`.
- **Cache** by `(Id or stripe-key + device)`; never `Destroy` cached sprites.
- **White-friendly / tintable** where useful; default bake uses the stripe colours.
- Share `ProceduralDrawUtil` rectangular overloads (ribbon is wide × short).

Suggested entry points (names may shift in implementation):

| Type | Role |
|---|---|
| `BarMedalCatalog` | Load defs; query by category |
| `BarMedalRenderer` / `BarMedalBaker` | `Sprite For(BarMedalDef)` |
| `BarMedalRackView` | UI: horizontal bars grouped by category |

Contact sheet / Editor probe: bake one strip per category so art regressions are
visible (project rule: generators are pictures — read the picture).

### Surfaces

1. **Post-battle card** — after victory evaluation, show newly earned bars (and
   grey placeholders for category peers not yet earned — optional v1).
2. **Career / menu rack** — compact strip reachable from the front door or
   pause; read-only in v1.
3. **Do not** put medals on the live PLAY HUD during the fight.

### Relation to Steam Achievements

Catalogue may carry an optional `SteamAchievementId`. Wiring
`SteamUserStats.SetAchievement` stays Phase 10 / #288 follow-on. Local awards
must work with `LocalAnonymousIdentity` and no App ID.

---

## Distinct from

| Issue / page | Question |
|---|---|
| **#38 / RankInsignia** | Command rank on the shoulder board |
| **#421** | AAR critique / teach — text and metrics, not ribbons |
| **#462 HistoricalNotes** | Briefing / outcome prose on a scenario |
| **Steam Achievements** | Platform trophies; map later |
| **#465 Scenario Pass** | Who may *play* a fight — orthogonal to medals for finishing one |

---

## Out of scope (v1)

- Full-size hanging medals / dress kits
- Mid-fight kill popups (awards still land only at decision — see Merit below)
- Paid cosmetic ribbons (#465 Pass does not sell medals)
- Live Steam sync, leaderboards, or sharing screenshots as a feature
- Community-category awards
- Career / menu rack (child still open)

---

## Suggested children (file when leaving backlog)

About five minutes each:

1. Docs cross-links (this page + phases / CLAUDE) — design PR.
2. `BarMedalCategory` + def/award POCOs + JSON catalogue stub (3–5 medals).
3. `BarMedalRenderer` bake + cache; contact sheet or probe pixels.
4. Grant hook from victory / campaign advance (idempotent).
5. Post-battle strip UI for newly earned bars.
6. Menu/career rack grouped by category.
7. Optional: SteamAchievementId field reserved; stub no-ops without App ID.

---

*Design for #467. Update when a binding decision above changes.*
