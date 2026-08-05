# Changelog

All notable changes to Strategos are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow the
ROADMAP alpha ladder (`0.1` map+symbols → `0.3` movement+combat). Issue numbers
refer to [github.com/aawadall/strategos](https://github.com/aawadall/strategos).

For the living task list see [ROADMAP.md](ROADMAP.md) and [docs/phases.md](docs/phases.md).
Open work is GitHub issues; deferred defects live in [docs/known-gaps.md](docs/known-gaps.md).

---

## [Unreleased]

### Added
- Price ladder milestone epics (#471): one epic per `$0`→`$30` rung (#472–#478);
  `docs/price-ladder.md`. Next open: `$15` Drill school (#475).
- Pause **ALPHA LIMITS** + deeper player-facing known-gaps (#469): same
  `AlphaHelpOverlay` in-session; wrecks and suppression stall called out; Esc closes
  nested limits before resume.
- Bar-medals design (#467): post-battle ribbon categories + procedural renderer —
  `docs/bar-medals.md` (backlog; distinct from Steam Achievements and #421 AAR).
- Tiered-releases design (#465): Free / Base / Scenario Pass unlocks scenarios —
  `docs/tiered-releases.md` (alpha stays ungated until SKUs exist).

### Changed
- itch pricing: pay-what-you-want with **$2** suggested default — README, Pages, and
  itch-publish note the live page; GitHub Release zip remains free.
- Pages hosts the itch embed widget; README adds itch / Release badges (iframes are
  stripped on GitHub).
- Point Play links at the live itch page [aawadall.itch.io/strategos](https://aawadall.itch.io/strategos)
  (README, Pages header, itch-publish / itch-push defaults).

### Added
- Authored historical fights (#459 / #460 / #461): Belleau Wood and Remagen on the main
  menu; Remagen digest; `HistoricalNotes` on Scenario + pause **HISTORICAL NOTE** (#462);
  LRT/Belleau/Remagen briefing copy from digests.
- Field-manual in-app browser (#206 / #124): pause → FIELD MANUAL list/detail over
  `alpha-glossary`; Esc closes manual before pause; `GlossaryProbe` covers the panel.
- Field-manual glossary pack (#205 / #124): `GlossaryPack` / `GlossaryTerm`,
  `Resources/FieldManual/alpha-glossary.json` (6 terms), `GlossaryProbe`;
  [docs/field-manual.md](docs/field-manual.md).
- itch.io publish handoff (#221 / #83): `docs/itch-publish.md` + `scripts/itch-push.ps1`
  (browser / app / butler); Pages CTA already live — first upload remains human.
- Unity CI licence ops checklist (#216 / #83): `docs/ci-unity-licence.md` — secret names,
  how to export `.ulf`, verify Steps; linked from known-gaps, build-and-verify, phases.
- Tutorial queue/abort beat (#449 / #289): after ENGAGE, Shift-queue a MoveTo then ABORT PLAN;
  `TutorialFirstBeatProbe` covers five advances.
- Context help for DIG IN (#447 / #289): Hold/Defend dig-in clock and half-fire copy;
  `ContextHelpProbe` covers all four authored verbs.
- Context help for WAYPOINTS (#445 / #289): draft legs → CONFIRM ROUTE copy; DigIn still
  stub; `ContextHelpProbe` extended.
- Audio Resources conventions (#261 / #41): `docs/audio-resources.md` — Research vs
  `Resources/Audio` layout, OGG / `.meta` rules, promote checklist; cross-links from
  audio.md, licence, inventory, assets, CLAUDE.md.
- Tutorial second beat (#441 / #289): after MOVE, checklist advances to ENGAGE; `PlayView`
  calls `OnEngageIssued` from the live Engage path; `TutorialFirstBeatProbe` covers the
  three-step advance.
- Context help for ENGAGE (#442 / #289): `ContextHelp.Engage*` + probe; PLAY HELP fallback
  copy mentions MOVE or ENGAGE.
- Audio licence / provenance notes (#260 / #41): `docs/audio-licence.md` — Suno Pro OK,
  ElevenLabs commercial gate, staging audit for chatter/numberstation; cross-links from
  inventory, assets, audio.md, CLAUDE.md.
- Audio sourcing inventory (#259 / #41): `docs/audio-inventory.md` — music / SFX / VO /
  Morse by status and tool (Suno / ElevenLabs / procedural); cross-links from assets,
  sfx-inventory, audio.md, CLAUDE.md.
- Free-alpha HELP: `AlphaHelpOverlay` on the main menu — how-to-play (select → MOVE /
  ENGAGE → pause) plus fog / artillery-as-DF / no-ZoC limits; README how-to section;
  player-facing note at the top of `docs/known-gaps.md`.

---

## [0.3.0-alpha.1] - 2026-08-05

First tagged GitHub Release (#219 / #83). Windows desktop player attached;
`Application.version` is stamped at build (#217) and shown in the top bar (#218).

### Added
- SFX epic wrap (#44 / #251 / #252): procedural order issued/rejected (`PlayView.IssuePlayer`)
  and combat fire on `ReportKind.Engaged`; closes the sound-effects stub track.
- Procedural UI click/select SFX (#250): `ProceduralSfx` + `AudioService.PlayUiClick` /
  `PlayUiSelect`; wired on `UiFactory` buttons/tabs and PLAY unit select.
- SFX inventory (#249 / #44): `docs/sfx-inventory.md` — UI / combat / world cue checklist,
  `Resources/Audio/Sfx/` ids, procedural stub order (#250–#252).
- Audio wiring + soundtrack beds (#40 / #253 / #254 / #262–#265): `AudioService` on AppShell
  (menu loop / PLAY ambient from `Resources/Audio/`), MASTER/MUSIC/SFX prefs in Settings,
  silence-safe `AudioProbe`. Master bus stub is `AudioListener.volume`.
- Climb campaign docs polish (#409): phases §6.3 / Phase 10 + ROADMAP + campaign-invariants
  cross-link the shipped seat ladder; closes epic #403.
- Climb campaign probe (#408): `ClimbCampaignProbe` — Squad → Company through
  `CampaignChainDriver`; asserts `PlayerEchelon` and company-HQ command scope.
- Climb campaign menu/PLAY entry (#407): START CLIMB beside Valley/Highland on main menu
  and PLAY CAMPAIGN rail; `AppShell.StartClimbFromMenu` → `PlayView.StartClimbCampaign`.

### Fixed
- `CampaignChainDriver.MergeCarriedOver` preserves `PlayerEchelon` and `ControlMeasures`
  on the next scenario (#408) — climb seat escalation was dropped to `None` after carry-over.
- Climb campaign chain (#406): `climb-campaign.json` + `CampaignSamples.ClimbName`;
  CampaignSaveLoadProbe loads/Validates the shipped climb fixture.
- Climb campaign scenarios (#405): `climb-squad` / `climb-company` / `climb-battalion` with
  escalating `PlayerEchelon` and Id-stable leaves (1/2 + reinforcement HQs); ScenarioProbe fixtures.
- Climb campaign design note (#404 / #403): `docs/climb-campaign.md` — Squad → Company →
  Battalion seat ladder, Id-stable scenario sketch, reuse rules vs Valley/Highland/#289.
- Display prefs contract probe (#392): `DisplayPrefsProbe` — Fullscreen/WxH round-trip plus
  Settings/F11 shared AppShell Apply* / ToggleFullscreen / boot apply.
- Apply display prefs on AppShell boot (#391): `ApplyDisplayPreferences` seeds remembered
  windowed size and applies fullscreen/windowed from `PlayerPreferences` before chrome builds.
- Settings GRAPHICS windowed size presets (#390): 1280×720 / 1600×900 / 1920×1080 dropdown
  (windowed only — not a fullscreen resolution list); persists `WindowWidth`/`WindowHeight`.
- Settings GRAPHICS fullscreen toggle (#389): calls `AppShell.ApplyFullscreen` /
  `ApplyWindowed` and persists `PlayerPreferences.Fullscreen` (shared with F11).
- Display prefs fields (#388): `PlayerPreferences.Fullscreen` / `WindowWidth` /
  `WindowHeight` (defaults false / 1600×900); `PreferenceStoreProbe` round-trip + legacy JSON.
- Display-mode API on AppShell (#387): `ToggleFullscreen` / `ApplyWindowed` /
  `ApplyFullscreen` shared with F11; `DisplayModeProbe`. Settings wiring is #389.
- Tutorial first beat (#310): `TutorialFirstBeat` checklist on tutorial load — select
  friendly unit then real MoveTo; `TutorialFirstBeatProbe`.
- Squad tutorial scenario skeleton (#309): `ScenarioSamples.Tutorial` /
  `tutorial-squad.json`, `PlayerEchelon` Squad; menu + PLAY load; ScenarioProbe fixture.
- Context help for MOVE (#308): `ContextHelp` + `ContextHelpOverlay`; PLAY rail HELP;
  Esc closes help before clearing the palette; `ContextHelpProbe`.
- Preference store stub (#307): `PlayerPreferences` + `IPreferenceStore` /
  `JsonPreferenceStore`; GAMEPLAY ConfirmOrders toggle on `SettingsView`;
  `PreferenceStoreProbe`.
- Settings screen shell (#306): `SettingsView` with empty GRAPHICS / AUDIO / GAMEPLAY /
  ACCESSIBILITY sections; main-menu Options navigates to it; `UiShellProbe` extended.
- UI revamp (#371 / #375–#379): `MainMenuView` front door; PLAY pause overlay (Esc) with Save/Load/
  Exit; drills quick-ref from pause; Tools tabs from menu; `UiShellProbe`; ui-invariants.
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
- Front-door menu fit (#426–#435): splash boot, two-column compact menu, Footer EXIT /
  OPTIONS / AUDIO, no ScrollRect; `UiShellProbe` height + footer asserts.
- Release engineering slice (#217 / #218 / #219): `build.ps1 -Version` →
  `-bundleVersion` stamp; top-bar `Version` label; first GitHub Release artefact.

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

*First tagged release 2026-08-05 (`v0.3.0-alpha.1`). Earlier history lives in the git log and closed issues.*
