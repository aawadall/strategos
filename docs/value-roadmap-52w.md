# 52-week value-add roadmap (#483)

Weekly **player-visible value** from **2026-08-10** (W01) through **2027-08-02** (W52).
Aligned to price-ladder rungs under [#471](https://github.com/aawadall/strategos/issues/471):
[#475](https://github.com/aawadall/strategos/issues/475) → [#476](https://github.com/aawadall/strategos/issues/476) →
[#477](https://github.com/aawadall/strategos/issues/477) → [#478](https://github.com/aawadall/strategos/issues/478).

Canvas companion: `value-roadmap-52w`. Slip by shifting weeks; do not invent filler chores.

| Band | Weeks | Price rung | Theme |
|---|---|---|---|
| Q1 | W01–W13 | **$15** → edge of **$20** | Teach, reward, polish storefront |
| Q2 | W14–W26 | **$20** | Honest fight (fog, guns, ground) |
| Q3 | W27–W39 | **$25** | Steam EA (store, editor, Pass, Deck) |
| Q4 | W40–W52 | **$30** | 1.0 (AI, online, Workshop, polish) |

---

## Weekly release + addon rule

Every week ships a **tagged or channel release** (itch `windows-alpha` / GitHub pre-release /
Steam depot branch as available) **and** at least one **addon** — a content or help
artifact the player can open without reading the PR.

| Addon kind | Examples | Where it lives |
|---|---|---|
| Help / limits | One `ContextHelp` verb, ALPHA LIMITS bullet, HELP sentence | `AlphaHelpOverlay` / `ContextHelp` / README |
| Glossary | One field-manual term | `Resources/FieldManual/` |
| Doctrine | One drill step fix or new `Ttp` in a pack | `Resources/Doctrine/` |
| Scenario / notes | Historical note, menu fight JSON, practice scenario | `Resources/Scenarios/` |
| Medal / catalog | One `BarMedalDef` | medal catalogue JSON |
| Storefront | itch cover, one screenshot, one devlog paragraph | itch + `docs/itch-publish.md` |
| Release note | CHANGELOG Unreleased → tagged note (always) | `CHANGELOG.md` |

**Minimum bar:** if the feature is spike-only (no player UI), the addon is still a **help
or glossary file** that names what is coming / what changed in probes. Empty weeks are not
allowed under a weekly-release cadence. Process tracker: [#485](https://github.com/aawadall/strategos/issues/485).

Release checklist each week:

1. Headline value from the table (code or content).
2. **Addon** column satisfied (path named in the PR).
3. CHANGELOG bullet under Unreleased (moves to the tag note on release).
4. Push build to the active channel when the player binary changed.

---

## Q1 — $15 Drill school (W01–W13)

| W | Week of | Value added | Addon (must ship) | Tracker |
|---|---|---|---|---|
| 01 | 2026-08-10 | itch storefront marketing live | itch cover + short page copy + 1 screenshot | **#480 done** (in-repo); #496 discoverability open |
| 02 | 2026-08-17 | Procedural splash boot frame | HELP / splash one-liner in ui-invariants + ALPHA LIMITS “new boot art” note | **#482 done** |
| 03 | 2026-08-24 | Field manual ↔ drill cross-link | 1 glossary term with `DrillRefs` | **#207 done** · epic #124 **closed** (#205–#209) |
| 04 | 2026-08-31 | Drill range design + scenario skeleton | **shipped early** — `docs/drill-range.md` + `drill-range-t1` stub, menu-hidden | #475 |
| 05 | 2026-09-07 | Train-on-drills playable (T1 / React) | **shipped early** — HOW TO PLAY step + main-menu **DRILL RANGE: T1** button | #475 |
| 06 | 2026-09-14 | Bar medal catalogue + ribbon renderer | 1 medal def JSON + contact-sheet bake | #467 |
| 07 | 2026-09-21 | Post-battle medal strip + career rack | Glossary: “service ribbon” / medal rack term | #467 / #475 |
| 08 | 2026-09-28 | Thin AAR critique | Post-battle HELP blurb + 1 AAR metric glossary entry | #421 |
| 09 | 2026-10-05 | Outcome HistoricalNotes on replay | Outcome note text on LRT or Belleau JSON | #462 / #298 |
| 10 | 2026-10-12 | +1 historical menu fight | Scenario JSON + Briefing HistoricalNote + ATTRIBUTIONS row | #475 |
| 11 | 2026-10-19 | +1 historical fight + ATP doctrine expand | 1–2 new `Ttp` rows in doctrine pack + binder visible | #516 / #513 / #475 |
| 12 | 2026-10-26 | Close **$15** — harden | ALPHA LIMITS / HELP refresh listing Drill school features | #475 |
| 13 | 2026-11-02 | FoW design spike + LOS probe | Glossary “line of sight” + known-gaps “coming” note (player-facing) | #476 prep |

**Q1 exit:** $15 clear; every week left an addon artifact in Resources/docs/itch.

---

## Q2 — $20 Honest fight (W14–W26)

FoW first (W14–W17), then **real-world map sourcing** (W18–W21, epic
[#487](https://github.com/aawadall/strategos/issues/487)) so LOS runs on true
heightfields, then guns / ground compressed into W22–W26.

| W | Week of | Value added | Addon | Tracker |
|---|---|---|---|---|
| 14 | 2026-11-09 | Terrain LOS raycast (probe) | Glossary: LOS; known-gaps engineering pointer | #476 |
| 15 | 2026-11-16 | Detection gated by LOS | ALPHA LIMITS rewrite — fog now real | #476 |
| 16 | 2026-11-23 | PLAY fog presentation | Context help: what grey/unknown means | #476 |
| 17 | 2026-11-30 | HELP / known-gaps fog honesty | README Alpha limits bullet update | #476 |
| 18 | 2026-12-07 | External map tools + SRTM hunt/convert scaffold | `docs/map-import-tools.md` + glossary DEM/SRTM | #488 / #493 / #487 |
| 19 | 2026-12-14 | GeoTIFF → `MapData.Elevation` (authoredRelief) | HELP / ALPHA LIMITS: real vs procedural maps | #489 / #487 |
| 20 | 2026-12-21 | One historical fight on SRTM height | Scenario JSON note + ATTRIBUTIONS SRTM row | #490 / #487 |
| 21 | 2026-12-28 | OSM roads/settlements + ODbL credit | In-game credits string + glossary OpenStreetMap | #491 / #487 |
| 22 | 2027-01-04 | Indirect fire model | Glossary: ToT / battery; HELP artillery note | #476 |
| 23 | 2027-01-11 | Artillery uses indirect + facing / no pass-through | CombatProbe numbers; glossary facing; drop pass-through limit | #476 |
| 24 | 2027-01-18 | ZoC v1 + formation column / line | Glossary ZoC; doctrine step using formation | #476 |
| 25 | 2027-01-25 | Formation wedge + supply classes bite | Drill pack bind; glossary supply class | #476 |
| 26 | 2027-02-01 | Reconstitution + close **$20** | Campaign HELP reconstitution; full ALPHA LIMITS pass | #476 |

**Q2 exit:** $20 clear; one playable SRTM-backed historical sheet; help/glossary track every ship.

---

## Q3 — $25 Steam EA (W27–W39)

| W | Week of | Value added | Addon | Tracker |
|---|---|---|---|---|
| 27 | 2027-02-08 | Steam App ID + Overlay smoke | `docs/steam.md` “dev Overlay verified” note + store draft stub | #300 |
| 28 | 2027-02-15 | Store capsules + screenshots | 5 screenshots committed under `Research/store/` or itch reuse | #293 |
| 29 | 2027-02-22 | Store copy + tags | Short/long description markdown in repo | #323 |
| 30 | 2027-03-01 | SteamPipe Windows push | Release notes file for that build | #424 |
| 31 | 2027-03-08 | Scenario editor v1 | Editor HELP card / glossary “scenario pack” | #477 |
| 32 | 2027-03-15 | Editor ORBAT + playtest | 1 sample authored-in-editor scenario JSON | #477 |
| 33 | 2027-03-22 | Free/Base/Pass menu unlocks | Menu HELP: what Pass unlocks; tiered-releases one-pager link | #465 |
| 34 | 2027-03-29 | Steam ownership for Pass | itch/Steam FAQ blurb in itch-publish | #465 |
| 35 | 2027-04-05 | Linux64 build | Linux run note in README / itch channel | #477 |
| 36 | 2027-04-12 | Controller shell nav | Glossary / HELP: gamepad map | #290 |
| 37 | 2027-04-19 | Controller map confirm | Steam Input note addon in docs/steam.md | #290 |
| 38 | 2027-04-26 | Remote Play Together | Hotseat HELP: Remote Play path | #477 |
| 39 | 2027-05-03 | Close **$25** | EA launch HELP + store “Early Access” paragraph | #477 |

**Q3 exit:** $25 clear; every Steam/editor week left docs or sample content.

---

## Q4 — $30 1.0 (W40–W52)

| W | Week of | Value added | Addon | Tracker |
|---|---|---|---|---|
| 40 | 2027-05-10 | Planning AI design spike | Glossary: planning AI vs reflexes | #478 |
| 41 | 2027-05-17 | AI plays one sandbox | Spectator HELP: watching AI | #478 |
| 42 | 2027-05-24 | Difficulty uses planning | Settings HELP: difficulty blurb | #478 |
| 43 | 2027-05-31 | Online lobby create/join | Glossary: lobby; menu Server HELP stub text | #478 |
| 44 | 2027-06-07 | Online sync + reconnect | HELP: reconnect / desync | #478 |
| 45 | 2027-06-14 | Scenario server list/get | 1 remote catalog sample JSON / fixture | #464 |
| 46 | 2027-06-21 | Workshop / browse subscribe | Glossary: Workshop item | #478 |
| 47 | 2027-06-28 | Weather affects LOS/move | Glossary: weather; ALPHA LIMITS weather line | #478 |
| 48 | 2027-07-05 | Day/night illumination | Glossary: illumination | #478 |
| 49 | 2027-07-12 | Achievements + Cloud ↔ medals | 1 Steam achievement ↔ medal id mapping table in docs | #467 |
| 50 | 2027-07-19 | macOS build | macOS run note + itch/Steam channel checklist | #478 |
| 51 | 2027-07-26 | 1.0 polish | Full HELP + known-gaps player-facing pass | #478 |
| 52 | 2027-08-02 | 1.0 tag — close $30 + #483 | Release notes + final itch/Steam storefront pass | #478 / #483 |

**Q4 exit:** $30 clear; weekly addons continue through the tag.

---

## Parallel / interruptible (any week)

Still require an addon if they become the week’s primary:

- Audio VO / OGG (#400 / #401) → ship 1 clip + glossary or SFX inventory row
- CI Unity licence (#216) → docs-only week OK (ci-unity-licence note is the addon)
- WebGL (#220) → Pages/itch HTML5 note
- Dark UI (#132) → ui-invariants theme note
- C3 (#47) → after FoW; glossary “link quality”

---

## Cadence rules

1. **One headline value + one addon per week** — both named in the PR description.
2. **Rung close weeks** (W12, W26, W39, W52) harden + HELP/limits pass as the addon.
3. **Update this file** when a week slips; keep the Addon column filled.
4. GitHub #483 tracks the plan; child work stays on rung epics #475–#478.

---

*Start: 2026-08-10. End: 2027-08-02. Parent price ladder: #471. Weekly release + addon: this page.*
