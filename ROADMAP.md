# Strategos — Development Roadmap

> **Engine:** Unity 6 (`6000.0.75f1` / URP) | **Status:** Pre-production — playable sandbox

This roadmap describes the high-level development path for Strategos. Detailed implementation
tasks for each phase are maintained in [docs/phases.md](docs/phases.md). Open work is tracked
as GitHub issues; known defects that are deliberate or deferred live in
[docs/known-gaps.md](docs/known-gaps.md).

---

## Where we are (2026-08-04)

The **Playable scenario sandbox** milestone is complete (9/9). Two forces load onto generated
terrain, take orders, move by terrain-cost A\*, fight under ROE, dig in, tire, report contacts,
and resolve victory — all deterministically, save/loadable, and replayable from the command log.
The shell shows the player's command rank as a shoulder board; the drill picker rates the
selected unit T/P/U. See [CHANGELOG.md](CHANGELOG.md) for what landed recently.

| Area | State |
|---|---|
| Phase 0 — Foundation | **Done** for a local buildable project. CI is scaffolded but Unity license secrets are unset (green CI ≠ coverage). No EditMode test assembly yet. Steamworks not started. |
| Phase 1 — Map | **Largely built.** Procedural generation + 2D sheet (schematic / topographic / hybrid / terrain / NatoTopo) + 3D drape preview. Still missing: terrain LOS, real fog of war, weather, day/night, satellite mode, basin-breaching for lake-heavy maps. |
| Phase 2 — Symbols | **Complete** for land units (composer, baker, library, BUILDER). Gaps: symbol LOD, non-land sets, four land entity icons (`FRAME ONLY`), animated states. |
| Phase 3 — Units / ORBAT | **Built.** Hierarchy, roll-up, fatigue, training, capabilities, sides, command-rank ladders (#38). Still missing: commander entities, OPCON/TACON attach-detach, national doctrine profiles. |
| Phase 4 — Movement & combat | **Core path done.** A\* movement + mid-march replan (#35), direct fire, suppression, dig-in Hold/Defend, wrecks + `CasualtyLog`. Still missing: indirect fire, logistics, reconstitution, formation movement, collision/ZoC. |
| Phase 5 — C2 | **Foundations in.** Command/report/directive buses, queues, FRAGO-style `CancelFrom`, ROE reflexes, TTP binder + drill execution (#97), mission types (#85), graphic control measures (#160), `ISidePolicy` seam. Still missing: spatial comms (#47), intel fusion, doctrine authoring (#65). |
| Phase 6 — Scenario & campaign | **Partial.** Scenario JSON + objectives/victory ship (feature-placed objectives #51); campaign chain data + carry-over + PLAY start/advance/save (#75, #114 / #138–#140). Still missing: scenario editor UI, historical packs, full arc (#78). |
| Phases 7–10 | **Mostly unbuilt.** Windows builds + GitHub Pages site (OG/CTA in; GIF still #120). Audio and release-engineering issues filed. |

### Near-term focus — "First playable game"

Command controls (#32), campaign in PLAY (#114 / #138–#140), mission types (#85), AI
environment API (#99 / #100–#106), graphic control measures (#160 / #161–#166), US/NATO
topo palette (#169), and rank gates (#76) are done. Player-as-node (#36), career across
campaigns (#109), game modes (#287), AI difficulty (#291), special-action DigIn (#33), and dynamic world
layer (#34), procedural scenario generation (#334), and A* replan (#35) are done.
Feature-placed objectives (#51) and historical research into `Research/historical/` (#332)
are done. First historical Scenario JSON (Little Round Top, #333) is done. Local/API seam
(#355) is done. UI front door + pause + drills quick-ref (#371 / #375–#379) is done.

**Recommended next** (player-facing):

| Issue | Title |
|---|---|
| [#289](https://github.com/aawadall/strategos/issues/289) | Epic: tutorial / contextual help / settings (#306–#311) |
| [#288](https://github.com/aawadall/strategos/issues/288) | Epic: Steamworks SDK (App ID, Overlay, Achievements, Cloud) |

### Outstanding themes (open issues, grouped)

- **Command UX / play** — #32 / #53 / #54 shipped; later: config-loaded verb table (#130)
- **Campaign & progression** — #114 / #138–#140 / #76 / #109 shipped
- **C2 / C3 depth** — #36 closed (#266–#269); still open: #47, #62, #65 (#85 mission types shipped)
- **Steam readiness** — #287 / #291 **closed**; still open: #288–#290, #293 (children #300+)
- **Graphic control measures** — #160 **closed** (#161–#166 via #174 / #283)
- **World & movement depth** — #33 / #34 / #35 / #51 **closed**
- **AI as environment** — #99 closed (#100–#106 shipped); episode-generation bottleneck
  #334 **closed** (procedural scenarios — OOB/objectives/victory, not just terrain)
- **Persistence beyond run saves** — #66 (local store choice); mid-campaign save is #140
  (shipped); local/API seam #355 **closed** (#361–#367)
- **Audio (Phase 10 early)** — #40–#46
- **Docs / reference** — #124 (field manual), #125 (rank insignia decoration variants)
- **UI / accessibility** — #132 (dark theme, alongside colour-blind palettes)
- **UI revamp** — #371 **closed** (#375–#379 via #374; Options/Help/Server stubs → #289 / #124 / #288)
- **Scenario / map access** — #133 closed (PLAY **PUSH NORTH** / **SKIRMISH ONLY**)
- **Map palette** — #169 **closed** (`NatoTopo` fifth mode; FM 3-25.26 / FM 21-31 colours)
- **Release / site** — #83; #120 (site GIF only — meta + CTA shipped)
- **Steam-readiness gaps (filed 2026-08-04)** — six epics found to have zero prior tracking
  during a backlog review: #287 game modes (mode select, hotseat, AI-vs-AI spectator, replay
  viewer), #288 Steamworks SDK integration (App ID, Overlay, Achievements, Cloud), #289
  tutorial/contextual help/settings screen, #290 controller support (Steam Deck), #291 an
  AI difficulty ladder from tunable `SideDirector` parameters, #293 Steam store page assets
- **Historical scenarios** — #332 research **closed**; #333 convert **closed** (starter:
  Little Round Top; more engagements remain optional follow-ons from the shortlist)
- **Procedural scenarios** — #334 **closed** (#347–#352); distinct from §6.3 campaign sequencing

---

## Vision

Strategos is a tactical command simulation game built around topographic maps, NATO APP-6D
symbology, multi-echelon command, and adaptive AI. The player can grow from commanding a
fireteam to controlling a theater-level combatant command.

The game supports:

- Solo play against AI
- Hotseat play
- Online multiplayer
- AI vs AI spectator mode
- Replay of saved or historical battles

---

## Echelon Progression

| Symbol | Echelon | Typical Strength |
|---|---|---|
| ○ | Fireteam / Crew | 2–4 |
| • | Squad | 9–13 |
| •• | Section | 8–13 |
| ••• | Platoon / Detachment | 30–50 |
| I | Company / Battery / Troop | 80–250 |
| II | Battalion / Squadron | 300–1,000 |
| III | Regiment / Group | 1,000–3,000 |
| X | Brigade / Combat Team | 3,000–5,000 |
| XX | Division | 10,000–20,000 |
| XXX | Corps | 20,000–80,000 |
| XXXX | Army | 100,000+ |
| XXXXX | Army Group / Front | multiple Armies |
| XXXXXX | Theater / Combatant Command | multiple Army Groups |

> Squad and section are near-synonyms and vary by army, but APP-6D and the code treat
> them as distinct echelons, so they are listed separately here. The marks above match
> `AmplifierDecorator.DrawEchelon` — company is **one** bar, battalion two, regiment three.

### Echelon is the difficulty curve

The echelon the player commands is not just a matter of scale. **It determines how much of
the command problem is present at all**, and that is the intended progression:

| Commanding | What command feels like |
|---|---|
| Fireteam / squad | No C2 problem — you *are* the unit. You see what it sees; orders are instant. |
| Platoon / company | Subordinates exist but are close. Orders are near-immediate. |
| Battalion | Subordinates out of sight. Reports mediate what you know. Delay begins to bite. |
| Brigade / division | You command through subordinate headquarters. You cannot see the front. |
| Corps / army | You work from reports already stale. You issue intent, not instructions. |
| Theater | Coalition. Some formations you cannot order at all, only ask. |

This is why the later phases exist. Communications degradation, order propagation delay,
intelligence fusion, and the divergence between what a commander believes and what is true
are not features bolted onto a finished game — **they are what makes each echelon feel
different rather than merely bigger.** Introduced all at once at squad level they would be
noise; unlocked as the player climbs, they are the learning curve.

The design consequence: these effects should be **parameterised by echelon, not toggled**.
Order delay is a function of echelon, distance and terrain that returns approximately zero
at squad level and grows from there — one code path from tutorial to theatre, with the
degenerate case serving as the tutorial. Formation-addressed orders already decompose one
echelon per step (the structural form of that delay). See
[docs/command-architecture.md](docs/command-architecture.md). Rank-gated progression is
tracked as #76 / #78 / #109.

---

## Phase Overview

### Phase 0 — Foundation & Project Setup — **done (local)**
Buildable Unity 6 / URP project, repository structure, tab-shell UI, probe-based verification,
GitHub Actions scaffolding. Remaining: real Unity CI secrets, EditMode tests, Steamworks.

### Phase 1 — Topographic Map System — **largely built**
Procedural heightfield pipeline, landcover, hydrology, settlements/roads, 2D topographic
rasterizer, pannable/zoomable viewport, 3D drape preview, grid overlay. Remaining: LOS, fog of
war, weather, day/night, alternate map modes, lake-basin breaching.

### Phase 2 — NATO APP-6D Symbol Library — **complete (land)**
Composable decorator pipeline, baker, library browser, BUILDER digit composer, map placement.
Remaining: LOD, non-land symbol sets, missing land entity icons, animated states.

### Phase 3 — Unit & Echelon System — **built**
`UnitInstance` / capabilities / sides, ORBAT tree with roll-up, fatigue and training, scenario
serialisation. Remaining: commander entities, OPCON/TACON, national doctrine profiles.

### Phase 4 — Movement & Combat Engine — **core done; depth open**
Terrain-cost A\*, direct-fire engagement, suppression, Hold/Defend dig-in, wrecks and casualty
log, save/load of a run. Remaining: indirect fire, logistics, reconstitution, formation
movement, collision/ZoC, dynamic replan (#35), special actions / world objects (#33/#34).

### Phase 5 — Command & Control (C2) System — **foundations in**
Orders down / reports up / directives in, per-unit queues, live plan + cancel, ROE reflexes,
TTP pack IO + drill execution, mission types (#85), graphic control measures (#160),
`SideDirector` behind `ISidePolicy`. Remaining: spatial multi-modal comms (#47), intel
fusion, doctrine authoring (#65).

### Phase 6 — Scenario & Campaign System — **partial**
Scenario model + validation, objectives and victory, shipped samples, campaign chain + ORBAT
carry-over + PLAY start/advance/save (#114 / #138–#140). Remaining: scenario editor,
feature-placed objectives (#51), historical packs, full campaign arc (#78).

### Phase 7 — Game Modes
Solo vs AI, hotseat, online multiplayer, AI vs AI watch mode, and replay playback. Unbuilt;
command log already records the stream replay and multiplayer will ship.

### Phase 8 — AI System
Rule-based → RL → transfer → GA. Unbuilt as intelligence; reflexes and `ISidePolicy` are the
seams. Epic #99: #100–#106 shipped (environment API complete; intelligence unbuilt).

### Phase 9 — Online Services & Community
Accounts, matchmaking, workshop, leaderboards, mod support. Unbuilt.

### Phase 10 — Polish, Accessibility & Release
Audio (#40–#46 filed early), UI/UX, tutorials, performance, platform builds, Steam assets,
1.0 launch. Early: Windows player builds and a GitHub Pages site (meta + CTA; GIF still open).
Command-rank shoulder insignia ships in the shell.

See [docs/phases.md](docs/phases.md) for the full task breakdown and checkboxes.
See [CHANGELOG.md](CHANGELOG.md) for what landed recently.
See [docs/command-architecture.md](docs/command-architecture.md) for the command/situation topic design that Phases 3–5 build on.
See [docs/steam.md](docs/steam.md) for the Steam publishing guide, Early Access strategy, and Steamworks integration details.

---

## Versioning Strategy

| Release | Focus | Progress |
|---|---|---|
| 0.1 Alpha | Phases 0–3: Map + Symbols + Units | **Reached** — sandbox loads and draws a fightable ORBAT |
| 0.3 Alpha | Phases 4–5: Movement + Combat + C2 | **In progress** — core path ships; mission-type and C3 depth remain |
| 0.5 Beta | Phases 6–7: Scenarios + All Game Modes | Campaign data started; modes and editor not started |
| 0.8 Beta | Phase 8: Full AI pipeline | Policy seam only |
| 0.9 RC | Phase 9: Online services | Not started |
| 1.0 | Phase 10: Polish + Release | Site + Windows builds only |

---

## Post-1.0 Direction

Post-release development may expand into naval, air, space, and cyber dimensions; coalition
multiplayer; large-scale AI tournaments; VR spectator tools; and a public modding SDK.

---

*Last updated: 2026-08-03*
