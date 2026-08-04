# Changelog

All notable changes to Strategos are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); the project is still
pre-versioned (pre-production / playable sandbox). Issue numbers refer to
[github.com/aawadall/strategos](https://github.com/aawadall/strategos).

For the living task list see [ROADMAP.md](ROADMAP.md) and [docs/phases.md](docs/phases.md).
Open work is GitHub issues; deferred defects live in [docs/known-gaps.md](docs/known-gaps.md).

---

## [Unreleased]

### Added
- Local/API seam (#355 / #361–#367): `StoreResult` + async `IGameStore` (`FileGameStore`);
  `IContentSource` + Resources Scenario/Campaign/Doctrine adapters; `IPlayerIdentity` +
  `LocalAnonymousIdentity`; `docs/local-api-seam.md`; `GameStoreSeamProbe`.
- Historical scenario convert (#333 / #341–#346): `ScenarioSamples.LittleRoundTop` + shipped
  JSON; Hills procedural terrain caveat; BattlePosition + Axis GCMs; ATTRIBUTIONS row;
  `CommunityScenarioLoadProbe` (arbitrary JSON load, no name allowlist).
- Historical research pack (#332 / #335–#340): `Research/historical/` digests (Little Round
  Top, Belleau Wood, Normandy corps context) + shortlist; `docs/historical-research.md`;
  ATTRIBUTIONS hook; gitignore allows committed notes while PDFs stay ignored.
- Feature-placed objectives (#51 / #235–#238, #358–#359): `Objective.PlaceNearKind` /
  `PlaceNearName`; `ObjectivePlacement.Apply` from `GenerateMap`; Validate on missing POI;
  PushNorth SpotHeight sample; ScenarioGenerator PlaceNear with stub fallback;
  `ObjectivePlacementProbe`.
- A* replan mid-march (#35 / #270–#272): `MoveToExecutor` invalidates cached routes when
  remaining waypoints or the current leg are no longer `Passable` (e.g. `HazardBlocking`);
  recomputes toward the same target or Fails; `ReplanProbe`.
- Procedural scenario generation (#334 / #347–#352): `ScenarioGenerationSettings` +
  `ScenarioGenerator` (ORBAT from catalogue, engagement victory templates, MapGenerator
  reuse); `ValidateGenerated` (reachability + force balance); `ScenarioGeneratorProbe`;
  `docs/scenario-generation.md`. Objective feature placement stays #51.
- Dynamic world layer (#34 / #273–#277): `WorldObject` / `WorldLayer` on `Simulation`;
  `HazardBlocking` blocks `MovementGrid.Passable`; PLAY sheet mark via `WorldObjectDrawer`;
  snapshot + Signature; `WorldLayerProbe`.
- Special-action seam (#33 / #278–#282): `ActionKind` + `UnitCapabilities.CanDigIn`; DigIn
  expands to Hold/Defend (same dig-in clock / half fire); PLAY **DIG IN** palette verb; HOLD
  gated the same way; artillery ships without dig-in; `SpecialActionsProbe`.
- AI difficulty ladder (#291 / #318–#322): `DifficultyParams` + Easy/Normal/Hard and
  Aggressive/Defensive/Balanced presets for `SideDirector`; PLAY **AI DIFFICULTY** /
  **AI PERSONALITY** dropdowns; `AiDifficultyProbe`; `docs/ai-difficulty.md`.
- Game modes (#287 / #294–#299): `ModeKind` + PLAY mode-select (solo / hotseat / spectator /
  replay); spectator directs both sides; hotseat **SWITCH SIDE** + GCM fog; **REPLAY SAVE**
  via `Replayer`; `GameModesProbe`; `docs/game-modes.md`.
- Career across campaigns (#109 / #212–#215): `CareerProfile` (rank + formation + higher),
  highland regiment-seat campaign, PLAY **START HIGHLAND**, same higher HQ still addresses
  after the switch; `CareerAcrossCampaignsProbe`.
- Player as a node in the chain (#36 / #266–#269): `Scenario.PlayerEchelon`,
  `CommandScope` / `Issue` refuse out-of-band addresses, PLAY zoom and order chrome follow
  the seat; design note for directives-in / orders-out; FRAGO plan cut remains `CancelFrom`
  (mid-run directive stream deferred); `CommandScopeProbe`.
- Rank gates (#76 / #222–#225): `RankAuthority` table (rank → max echelon), PLAY refuses
  under-rank scenario/campaign start, promote one rung on campaign win; `RankGateProbe`.
- US/NATO topographic map palette (#169 / #188–#192): `MapRenderMode.NatoTopo` with
  FM 3-25.26 / FM 21-31 five-colour ground colours; PLAY / SCENARIO / EXPLORE dropdowns;
  `MapPaletteProbe` + `MapContactSheet` coverage.
- Graphic control measures complete (#160 / #161–#166): arrows (axis / direction / retirement /
  counterattack), areas (battle position / engagement area / kill zone), PLAY `afterPixels`
  with per-side fog (#283); earlier #174 shipped data model, checkpoints, phase lines,
  boundaries. `ControlMeasureProbe` covers round-trip, draw, and viewer filter.
- PLAY scenario pick: **PUSH NORTH** beside **SKIRMISH ONLY** so the shipped second
  scenario is reachable without editing code (#133).
- `Trajectory` / `TrajectoryExporter` — (obs, action) export from CommandLog + ReportLog via
  belief-only encoding; `TrajectoryProbe` (#106 / #99).
- `ThroughputProbe` — times map generation (erosion on/off) and 3,600-tick step loops;
  reports maps/hour and episodes/hour (#105 / #99).
- `SideEnv` — Reset/Step/(observation, reward, done); cached-map Restore; `EnvProbe` (#104 / #99).
- `SideReward` / `SideRewardSnapshot` — terminal ±1/0/−1 plus potential shaping on
  owned objectives and force advantage (no contact term); `RewardProbe` (#103 / #99).
- `SideActionSpace` / `SideActionMask` — drill + ADVANCE vocabulary and legality gates;
  `ActionSpaceProbe` (#102 / #99).
- `SideObservation` / `SideObservationEncoder` — fixed-shape side knowledge from report
  belief (not enemy ground truth); `ObservationProbe` fog-leak guard (#101 / #99).
- Pursue mission order: tight standoff MoveTo + Engage; PLAY PURSUE; `PursueProbe`
  (#153 / #85). Closes the #85 mission-type children.
- Exploit mission order: MoveTo past threat + Engage; PLAY EXPLOIT; `ExploitProbe` (#152 / #85).
- Recon mission order: MoveTo standoff + Screen; PLAY RECON; `ReconProbe` (#151 / #85).
- Attack mission order: expands to MoveTo (standoff) + Engage; PLAY ATTACK; `AttackProbe`
  (#85 slice).
- Withdraw + Delay mission orders: Withdraw expands to Abort+MoveTo; Delay holds until the
  break threshold then Issues Withdraw; PLAY buttons; probes (#85 slice).
- Cover mission order: dig-in with no detection stretch; suppresses break-contact while
  Cover is on the queue; PLAY COVER button; `CoverProbe` (#85 slice).
- Guard mission order: dig-in like Defend plus detection ×1.15 once prepared; PLAY GUARD
  button; `GuardProbe` (#85 slice).
- Screen mission order: never-ending hold that sets `Posture.Screening` (detection ×1.35,
  no dig-in); PLAY SCREEN button; `ScreenProbe` (#85 first slice).
- Mid-campaign SAVE/LOAD: `SaveRecord` carries live chain JSON + operation index; PLAY
  quicksave restores session and sim (#140 / #114).
- PLAY campaign rail: START VALLEY / CONTINUE / SKIRMISH ONLY — shipped `valley-campaign`
  chain, `AppSession` campaign context, carry-over between ops (#139 / #114).
- `CampaignChain.Validate()` — scenario resolution, carried-over id checks, Id-consistency
  across consecutive operations; probe coverage (#138 / #114).
- PLAY WAYPOINTS palette verb: draft multi-leg marches with pathfinder preview, commit as
  ordinary queued MoveTos; pending legs also draw via `PlanCells` (#54 / #32).
- Campaign PLAY entry broken out: parent #114 → Validate (#138), start+advance (#139),
  mid-campaign save (#140).
- PLAY command palette: table-driven MOVE / ENGAGE arming chrome (#127); armed left-click
  issues that verb via the command bus while right-click stays the engage-or-march shortcut
  (#128 / #53); M / E / Esc arm and clear from the verb table (#129).
- Shoulder-board insignia for the echelon the player commands — procedural marks from
  `Side.RankLadder` (US / Soviet JSON), shown in the AppShell top bar (#38).
- PLAY drill dropdown annotates each entry with the selected unit's T/P/U readiness and
  rebuilds on selection change (#97).
- GitHub Pages landing: Open Graph / Twitter Card meta and a header "View source" CTA
  (#120 — site GIF still open).
- Roadmap and phase checkboxes reconciled against the playable sandbox (2026-08-03 pass).

### Fixed
- Plan-card CANCEL FROM addresses `QueuedCommand.Ordinal` rather than the live list index
  (#54 / #57).
- CancelFrom posture and stale queue indices; paused-clock order visibility (#56, #57, #59).
- Objective hold clock leave-and-return sampling (#91).

---

## 2026-08-02

### Added
- Save / load a run (#74).
- Campaign chain data and ORBAT carry-over with three-operation probes (#75); PLAY entry
  still open (#114).
- Directives from higher — player as receiver; acknowledgements enter the command log
  (#73, #94).
- `ISidePolicy` seam — `SideDirector` and future agents behind one interface (#100).

---

## 2026-08-01

### Added
- Drill execution with group addressing (#77).
- Hold / Defend dig-in as a real order (#58); mission-type Defend / Occupy path (#72).
- Formation tree (`ParentId` real) (#70); fatigue / readiness decay (#67).
- Wrecks and `CasualtyLog` (#71).
- TTP binder (DRILLS view) (#61).

### Fixed
- Shipped `THE CROSSROADS` / objective placement on the erosion-on map (#95).
- Probes that skipped the shipped erosion setting (#96).
- Hold clock that kept running after ground was abandoned (#87).

---

## 2026-07-31 — Playable scenario sandbox

Sandbox milestone complete: scenario load, selection, A\* movement, direct fire, ROE
reflexes, situation reports, objectives / victory, opposing `SideDirector`, live plan and
abort, in-progress order drawing (#4–#15, #39, #48, #52).

---

*Started 2026-08-03. Earlier history lives in the git log and closed issues.*
