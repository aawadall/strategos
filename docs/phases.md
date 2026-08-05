# Strategos — Phase Definitions

Detailed task breakdown for each development phase. See [ROADMAP.md](../ROADMAP.md) for the
high-level overview, current status, and versioning strategy. Checkboxes below reflect what
ships in the repository as of **2026-08-03** — not the original aspiration list alone.

---

## Phase 0 — Foundation & Project Setup
**Goal:** Establish a buildable Unity 6 project with CI and a walking-skeleton scene.
**Status:** Done for local development. CI and Steamworks remain.

- [x] Unity 6 (6000.x LTS) project initialisation with Universal Render Pipeline (URP)
- [x] Git repository structure, `.gitignore` (Unity), and branch strategy (`main` protected)
- [ ] GitHub Actions: automated Unity build check on every PR *(workflow exists; secrets
  unset — green CI currently means nothing ran; ops checklist [ci-unity-licence.md](ci-unity-licence.md) #216)*
- [x] Folder architecture inside `Assets/` *(adapted: `Scripts/Core/*`, `Scripts/UI`, `Scripts/Demo`, `Resources/`, `Editor/` — not the original flat `Units`/`AI`/`Terrain` layout)*
- [x] Basic main-menu / tab-shell scene with placeholder-then-real UI
- [ ] In-game developer console (log, commands, unit spawning)
- [ ] Unit test framework (Unity Test Framework) wired up *(package present; no EditMode assembly under `Assets/` yet — probes in Editor substitute)*
- [ ] Steamworks partner account created; Steam App ID registered on Steamworks portal (#288 / #300) — business gate; checklist in `docs/steam.md`
- [x] `steam_appid.txt.example` + gitignored `steam_appid.txt`; `SteamClientHost` Init/Shutdown at boot (#302)
- [x] Steamworks.NET chosen; `Strategos.Steam` asmdef + `ISteamClient` / `NullSteamClient` stub (#301) — native package not linked until App ID
- [x] Overlay smoke control in Settings (no-ops without Steam); real Overlay verify still needs App ID (#303)
- [x] Achievement + Cloud stubs on `ISteamClient`; `SteamProbe` CI-safe without App ID (#304 / #305)
- [ ] SteamPipe depot/build scripts to push a build to Steam (#424 — separate from the
  in-game SDK work above; blocked only on the App ID)

**Milestone M0:** Clean local build, tab shell loads, Editor probes pass. *(CI activation still open.)*

---

## Phase 1 — Topographic Map System
**Goal:** Render realistic topographic terrain with interactable overlay.
**Status:** Largely built via procedural generation (not SRTM ingestion).

- [ ] Heightmap ingestion pipeline (SRTM/GeoTIFF → Unity Terrain) *(superseded for now by procedural `MapGenerator`)*
- [x] Procedural contour-line overlay generation from heightmap data
- [x] Terrain classification layer (open ground, forest, urban, mountain, water, swamp, desert)
- [x] Map rendering modes: schematic, topographic, hybrid, terrain, US/NATO topo (#169 / #188–#192); satellite still open
- [x] Smooth zoom / pan on the 2D sheet *(echelon-bounded in PLAY; theater-scale 5,000 km not a target yet)*
- [x] Grid system overlay on the sheet
- [ ] Line-of-sight (LOS) and line-of-communication (LOC) raycasting
- [ ] Fog of war: per-unit observation radius, terrain masking, time decay *(detection range exists; no terrain masking — see known-gaps)*
- [ ] Weather system: clear, rain, snow, sandstorm — affects LOS and movement
- [ ] Day/night cycle with illumination effects on visibility
- [ ] Lake-basin breaching / minimum catchment *(maps run 18–29% lake — known-gaps)*

**Milestone M1:** Zoomable topographic map ships. LOS and fog of war still open.

---

## Phase 2 — NATO APP-6D Symbol Library
**Goal:** Implement a complete, composable NATO APP-6D military symbol system.
**Status:** Complete for land units.

### 2.1 Symbol Composition Engine
- [x] APP-6D frame shapes by dimension *(land frames; sea/air/space/cyber not drawn)*
- [x] Affiliation colours: Friend (blue), Hostile (red), Neutral (green), Unknown (yellow)
- [x] Symbol icons for major land unit types: infantry, armour, artillery, aviation, engineer, signals, logistics, medical, HQ, etc. *(four land entities remain `FRAME ONLY`)*
- [x] Echelon designator renderer (all 12 echelon marks)
- [x] Modifier overlays: task-force indicator, reinforced/reduced, HQ, dummy, feint
- [x] Dynamic text fields: unit designation (T), higher formation (M), strength / unique (F)

### 2.2 Symbol Rendering
- [x] Procedural composition at runtime, baked to sprites *(not an SVG atlas — by design)*
- [ ] LOD: detailed symbol at close zoom, simplified icon at distance, dot at theater scale
- [x] Symbol anchoring to map position
- [x] Selection highlight / unit summary in PLAY
- [ ] Animated states: moving (arrow), engaged (flash), disrupted, destroyed *(in-progress actions draw as plan arrows; symbols themselves are static)*

### 2.3 Symbol Editor (in-game)
- [x] Digit-by-digit symbol composer (BUILDER view)
- [ ] Drag-and-drop symbol placement in scenario editor *(no scenario editor yet; surfaced as unfiled by #371)*
- [ ] Right-click context menu for symbol properties

**Milestone M2:** Any land APP-6D symbol can be created, placed on the map sheet, and rendered. Met for land.

---

## Phase 3 — Unit & Echelon System
**Goal:** Full data model for every echelon with parent-child hierarchy.
**Status:** Built for runtime command; commander careers still open.

### 3.1 Unit Data Model
- [x] Unit definition / catalogue: echelon, type, equipment ratings, mobility, sustainment fields
- [x] `UnitInstance`: runtime state — position, strength, readiness, supply, morale-adjacent fields, ROE, training
- [x] Subordinate unit tree: each unit owns direct subordinates (unlimited depth)
- [x] Equipment / capability ratings per catalogue entry
- [ ] National doctrine profiles (US, NATO, Russia, China, generic — affects default TTPs)

### 3.2 ORBAT (Order of Battle) System
- [x] Hierarchical ORBAT tree view in UI (PLAY rail)
- [x] ORBAT serialisation to JSON for scenarios and campaign carry-over
- [ ] Attach/detach (OPCON/TACON) units between formations
- [ ] Headquarters (HQ) units with C2 radius and morale bonus *(HQ as symbol/type exists; radius bonus does not)*

### 3.3 Commander Assignment
- [x] Command rank as shoulder-board insignia derived from ORBAT echelon + `Side.RankLadder` (#38)
- [ ] Rank insignia decorator variants by country / division / era (#125)
- [ ] `Commander` entity: name, rank, skill stats (leadership, tactics, logistics, intel)
- [ ] Commander bonuses applied to subordinate units within command radius
- [x] Commander career progression — rank gates (#76 / #222–#225); career across campaigns (#109 / #212–#215)
- [ ] Historical commander database (optional DLC/data pack)

**Milestone M3:** Multi-echelon ORBAT displayed; units selectable, state shown. Met.

---

## Phase 4 — Movement & Combat Engine
**Goal:** Physically grounded movement and deterministic combat resolution.
**Status:** Core path done; logistics and indirect fire open.

### 4.1 Movement
- [x] Terrain movement cost table per unit type (landcover speed factors)
- [x] Pathfinding: A* on terrain grid with impassable cells
- [x] Road/trail network present on generated maps *(movement bonus still thin)*
- [ ] Operational movement vs. tactical movement speed distinction
- [ ] Formation movement: line, column, wedge, echelon, bounding overwatch
- [ ] Forced march (speed boost with readiness penalty) *(fatigue from marching exists; forced-march order does not)*
- [ ] River crossing: bridge vs. ford vs. wet gap crossing *(bridges/fords generate; DigIn seam #33 shipped — Bridge/Clear still reserved)*
- [x] Special-action seam: `ActionKind` + DigIn → Hold/Defend (#33 / #278–#282)
- [x] Dynamic world layer: spawn/despawn hazards, Signature, PLAY mark (#34 / #273–#277)
- [x] A* as a living plan — replan when Passable invalidates the route (#35 / #270–#272)

### 4.2 Combat
- [x] Engagement range by weapon / catalogue firepower (direct fire)
- [x] Combat resolution: firepower, protection, terrain, readiness, suppression, posture
- [x] Suppression mechanic: units under fire accumulate suppression, reducing effectiveness
- [ ] Indirect fire: artillery, mortars, MLRS — target area, scatter, time-on-target *(artillery firepower currently mis-spent as direct fire — known-gaps)*
- [ ] Air support: CAS, interdiction, ISR (scheduled or on-call)
- [ ] Naval gunfire support (coastal scenarios)
- [ ] Counter-battery radar and anti-artillery fire
- [ ] NBC (nuclear/biological/chemical) contamination zones (optional scenario element)

### 4.3 Logistics & Sustainment
- [ ] Supply class system (ammunition, fuel, rations, water, spare parts) *(per-unit Supply field exists; no class system or tracing)*
- [ ] Supply line tracing: unit → higher HQ → supply depot → supply point
- [ ] Consumption rates per unit type and activity level
- [ ] Out-of-supply penalties: readiness degradation, movement restriction
- [ ] Engineer: bridging, minefield emplacement/breaching, fortification construction

### 4.4 Attrition & Recovery
- [x] Destroyed units become wrecks; loss recorded in `CasualtyLog` with tick and killer
- [ ] Casualty model: killed, wounded, missing (finer than destroyed/not)
- [ ] Equipment loss tracking
- [ ] Medical evacuation chain
- [ ] Reconstitution (refit and replacement) at rear echelon / between campaign operations

**Milestone M4:** Two opposing forces can manoeuvre, engage, and resolve combat. Met for direct fire. Logistic failure and reconstitution still open.

---

## Phase 5 — Command & Control (C2) System
**Goal:** Model realistic military C2 with orders, communications, and doctrine.
**Status:** Foundations in; C3 depth and remaining mission types open.

### 5.1 Orders System
- [x] Core orders: MoveTo, Engage, Defend/Hold, Abort, CancelFrom (FRAGO-style plan cut)
- [ ] Mission type orders *(parent #85; children below)*
  - [x] Screen — #145
  - [x] Guard — #146
  - [x] Cover — #147
  - [x] Withdraw / Delay — #148
  - [x] Attack — #150 / #149
  - [x] Reconnaissance — #151
  - [x] Exploit — #152
  - [x] Pursue — #153
- [x] Graphic control measures: data model, points/lines, arrows, areas, PLAY afterPixels + per-side fog (#161–#166 under #160)
- [x] Order propagation delay via formation decomposition (one echelon per step)
- [ ] Order propagation delay by distance / terrain / comms modality (#47)
- [x] Directives from higher — player as receiver (#73); acknowledge is replayable (#94)
- [x] Player as a node in the chain (#36 / #266–#269): `PlayerEchelon`, command-scope guard,
  two-interface docs; mid-run directive FRAGO stream still deferred
- [ ] Commander's intent field: free-text or template-based *(directive text exists; free authoring UX does not)*

### 5.2 Communications
- [ ] Communication range by echelon (line-of-sight radio, satellite, landline)
- [ ] Comms degradation in mountains/urban terrain (multipath)
- [ ] EW (Electronic Warfare): jamming, SIGINT detection
- [ ] PACE plan (Primary, Alternate, Contingency, Emergency comms)
- [ ] Spatial multi-modal C3 network (#47); number stations (#62)

### 5.3 Intelligence (INT)
- [x] Contact reports up a situation topic; `ContactTracker` maintains what a side has seen
- [ ] Intelligence cycle: collection, processing, analysis, dissemination
- [ ] Reconnaissance units: scouts, UAVs, satellite passes *(recon catalogue entries exist; special collection does not)*
- [ ] HUMINT, SIGINT, IMINT intelligence types
- [ ] OPFOR position confidence levels (confirmed, suspected, possible) *(contacts name real UnitIds today — known-gaps)*
- [ ] Intel fusion board: correlate multiple sources
- [ ] Deception operations: feints, decoys, radio silence

### 5.4 Doctrine Templates
- [x] Shipped TTP packs as JSON; DRILLS binder (read-only)
- [x] Execute a drill — group addressing and mission executors (#77)
- [x] PLAY drill picker annotated with T/P/U for the selected unit (#97)
- [ ] AI and human players can load and apply doctrine templates in PLAY authoring UX
- [ ] Player can create and save custom doctrine templates (#65 — authoring is echelon-gated)

**Milestone M5:** Player can issue orders; units execute with structural lag; contacts reveal OPFOR. Partially met. Full intel and comms still open.

---

## Phase 6 — Scenario & Campaign System
**Goal:** Replayable standalone scenarios and linked campaign chains.
**Status:** Scenario runtime + campaign data ship; editor and PLAY wiring open.

### 6.1 Scenario Editor
- [x] Scenario model with JSON round-trip; map generation settings; shipped samples
- [ ] Map selection and terrain editing UI (beyond SCENARIO preview) *(surfaced as unfiled by #371)*
- [ ] ORBAT builder (both sides)
- [x] Objectives and victory conditions in data + evaluator
- [x] Objectives may place by map feature (`PlaceNearKind` → `ObjectivePlacement`) (#51 / #235–#238, #358–#359)
- [ ] Objectives placement *UI* (editor) — still open; data path is #51

### 6.2 Historical Scenarios
- [x] Research pack notes at varied echelon scales into `Research/historical/` (#332 / #335–#340)
- [x] Convert researched engagements to Scenario JSON via ScenarioSamples + Write Samples (#333 / #341–#346; starter: Little Round Top)
- [x] Data format allows community-built historical packs (`ScenarioIO` / #341 smoke)
- [ ] Historical AI behaviour seeding (see Phase 8)
- [x] Procedural scenario generation — OOB / objectives / victory from parameters (#334 / #347–#352); feature placement via #51

### 6.3 Campaign Mode
- [x] Linked scenario chain with persistent ORBAT and casualty carry-over (#75 — data + probes)
- [x] Campaign in PLAY — parent #114
  - [x] `CampaignChain.Validate()` (#138)
  - [x] Start and advance a campaign in the player (#139)
  - [x] Mid-campaign save/resume (#140)
- [ ] Strategic map layer: campaign moves between operations
- [ ] Operational-level resupply and reinforcement between scenarios
- [ ] Dynamic campaign generator (procedural) — sequencing; see #334 for per-scenario generation
- [x] Full campaign arc: drills, directives, positions, execution, rank (#78 — closed)
- [x] Multi-echelon climb campaign: seat ladder in one chain (#403 / #404–#409) —
  [climb-campaign.md](climb-campaign.md); distinct from #289 tutorial and #109 Highland

**Milestone M6:** Editor ships, at least five built-in scenarios playable, campaign chain of three linked operations works in PLAY. Data-side three-op probe exists; PLAY entry is #114 / #138–#140. Climb campaign (`climb-campaign`) ships Squad → Company → Battalion beside Valley/Highland.

---

## Phase 7 — Game Modes
**Goal:** All five game modes functional and stable.
**Status:** Core modes shipping under #287 — see [game-modes.md](game-modes.md). Online
multiplayer (§7.3) still open.

### 7.1 Single Player vs AI
- [x] Player selects mode + scenario (#287 / #294–#295) — echelon picker still open
- [x] Opposing side can run under `ISidePolicy` / `SideDirector` (reflex-level intent, not planning AI)
- [x] Difficulty ladder Easy / Normal / Hard via `DifficultyParams` (#291 / #318–#320) — Recruit→Legendary rename still open
- [x] Post-battle AAR (After-Action Review): map replay with analytics (#287 — `REPLAY SAVE` / Replayer; analytics is #421)

### 7.2 Hotseat Multiplayer
- [x] Side switch + per-side GCM fog (#287 / #297) — WEGO simultaneous orders still open
- [x] Hidden information: GCM viewer follows active hotseat side (#186 / #297)
- [ ] Screen-swap prompts between player turns
- [ ] Local AI fill-in if player disconnects
- [ ] Steam Remote Play Together: enable in Steamworks portal to allow internet hotseat at zero networking cost (#288, once #287's hotseat exists)

### 7.3 Online Multiplayer
- [ ] Unity Netcode for GameObjects (primary) / Mirror (fallback) *(a front-door "Server" menu entry is #371, blocked on this and #288)*
- [ ] Lobby system: create/join game, password, invite via link
- [ ] Turn timer with async option (email-style, submit orders when ready)
- [ ] Spectator slot
- [ ] Reconnect and resume after disconnect
- [ ] Ranked and unranked match types
- [ ] Steam Lobbies (`SteamMatchmaking`): optional lobby backend for friend invites and browser
- [ ] Steam Friends invite: invite via Steam overlay using `SteamFriends.InviteUserToGame`
- [ ] Steam Rich Presence: show current game state (scenario name, echelon, turn) in Steam profile

### 7.4 AI vs AI (Watch Mode)
- [x] Spectator mode: both sides under SideDirector (#287 / #296)
- [x] Speed control in PLAY (time compression) — reusable building block
- [ ] Commentary overlay (optional, generated from game events)
- [ ] Export battle as replay file

### 7.5 Replay System
- [x] All commands (and acknowledgements) recorded to `CommandLog`; reports to `ReportLog`
- [x] Deterministic `Replayer` / signature divergence check
- [x] PLAY `REPLAY SAVE` drives `Commands.Replayer` (#287 / #298)
- [ ] Compressed replay file format (`.stgreplay`) with scrub, pause, rewind, speed control UX
- [ ] Replay sharing: upload to server, download community replays
- [x] AI training ingestion: trajectory export from CommandLog + ReportLog (#106)

**Milestone M7:** All five modes launchable and playable end-to-end without crashes.

---

## Phase 8 — AI System
**Goal:** An AI that can be trained, evolved, transferred, and shared.
**Status:** Environment API through trajectory export (#100–#106) shipped under epic (#99);
intelligence / ML-Agents unbuilt.

### 8.1 Rule-Based AI (baseline)
- [x] Reflex-level opposing intent (`SideDirector` / ROE) — not doctrine-driven planning
- [ ] Doctrine-driven behaviour trees per mission type
- [x] Tunable difficulty / personality on the reflex opponent (#291) — RL baseline still open
- [x] Exposes same action API as ML agent for compatibility (#102)

### 8.2 Reinforcement Learning (RL)
- [x] Observation encoding — `SideObservation` / encoder; fog-leak probe (#101)
- [x] Action space — drills + ADVANCE; `SideActionMask` gates; `ActionSpaceProbe` (#102)
- [x] Reward — terminal ±1/0 + potential shaping on objectives/force; no contact term (#103)
- [x] Environment lifecycle — `SideEnv` Reset/Step/(obs,reward,done); `EnvProbe` (#104)
- [x] Headless throughput measurement — `ThroughputProbe` maps/hour and episodes/hour (#105)
- [x] Trajectory export from CommandLog + ReportLog (#106)
- [ ] Unity ML-Agents integration (C# environment, Python trainer)
- [ ] Self-play training loop: two instances of same model play against each other
- [ ] Curriculum learning: start with small scenarios (company level), progress to corps
- [ ] Training dashboard: reward curves, win rate, episode length
- [ ] Model checkpointing every N episodes
- [ ] Serialise trained model to `.onnx` for runtime inference

### 8.3 Transfer Learning
- [ ] Historical battle imitation learning: convert replay files to expert demonstrations
- [ ] Behavioural cloning pre-training from historical data before RL fine-tuning
- [ ] Doctrine transfer: model trained on one national doctrine bootstraps another
- [ ] Cloud model repository: download pre-trained `.onnx` models by echelon/doctrine

### 8.4 Genetic Algorithm (GA) Strategy Evolution
- [ ] Strategy chromosome: encodes tactical preferences (aggression, axis choice, reserve usage, deception propensity)
- [ ] Population of N strategy chromosomes per faction
- [ ] Fitness function: performance across M randomised scenarios
- [ ] Selection, crossover, mutation operators
- [ ] GA runs offline or as background process; best chromosome applied at runtime
- [ ] GA + RL hybrid: GA evolves high-level strategy; RL handles tactical execution

### 8.5 AI Difficulty & Personality
- [x] Near-term `SideDirector` presets: Easy/Normal/Hard × Aggressive/Defensive/Balanced (#291 / #318–#322)
- [ ] Expanded profiles: Feint-heavy, Attrition, Manoeuvre (trained / doctrine-driven)
- [ ] Personality parameters tune RL policy at inference time
- [ ] Named commanders (personalities) with persistent stats across sessions

### 8.6 AI Model Hub
- [ ] REST API server: upload, download, version, rate AI models
- [ ] Client-side: browse community models by echelon, doctrine, win rate
- [ ] Automated tournament: server runs AI-vs-AI matches, ranks models on ELO ladder
- [ ] Provenance: model lineage tree (which models trained which)

**Milestone M8:** RL agent beats rule-based AI at battalion level. Transfer-learning from at least one historical dataset improves training speed by ≥ 20%. GA evolves a strategy that outperforms fixed doctrine on 3 out of 5 test scenarios.

---

## Phase 9 — Online Services & Community
**Goal:** Backend services supporting persistent accounts, matchmaking, and content sharing.
**Status:** Unbuilt.

- [x] Local/API seam for saves, content, and player identity (#355 / #361–#367) — see `docs/local-api-seam.md`
- [ ] User accounts: registration, login, OAuth (Google/Discord) (builds on #355 identity; Steam via #288 likely first)
- [ ] Embedded store for player data when accounts exist (#66)
- [ ] Player profile: rank, win/loss, favourite doctrine, commander history
- [ ] ELO-based matchmaking for online games
- [ ] Global and faction leaderboards
- [ ] Scenario workshop: upload, tag, rate, subscribe to community scenarios
- [ ] Replay library: upload/download `.stgreplay` files
- [ ] AI model hub (see Phase 8.6)
- [ ] Notification system: game invites, turn alerts, tournament results
- [ ] Mod support API: custom units, terrain packs, doctrine packs
- [ ] Steam Leaderboards (`SteamUserStats`): global and friends-only leaderboards as primary backend
- [ ] Steam Rich Presence updates: reflect match status across all online modes

**Milestone M9:** Backend deployed; account creation, matchmaking, and scenario upload/download working in production.

---

## Phase 10 — Polish, Accessibility & Release
**Goal:** Shippable 1.0 build.
**Status:** Early — Windows builds and a GitHub Pages site (OG/CTA) exist; shoulder-board
rank insignia ships; audio issues filed (#40–#46).

### Audio
- [ ] Ambient terrain soundscapes (forest, urban, desert, arctic)
- [x] Unit movement and combat SFX (#44) — inventory + procedural UI/order/combat stubs
  (#249–#252); sourced OGG still `#41`
- [ ] Radio chatter / commander voice lines (localisation-ready) (#42, #45, #46)
- [ ] Dynamic music system: tension ramps with combat intensity (#43 remainder)
- [x] Audio wiring: `AudioService` + volume prefs + probe (#40 / #262–#265)
- [x] Soundtrack beds: menu loop + PLAY ambient via `Resources/Audio/` (#253 / #254)

### UI / UX
- [x] Tab-shell military map aesthetic (working UI, not final skin)
- [x] Outside menu + in-game pause + drills quick-ref (#371) — Help/Server stubs → #124 / #288;
  Options opens settings shell (#306)
- [x] Shoulder-board command-rank insignia in the shell (#38)
- [x] GitHub Pages status site with OG/Twitter meta and header CTA (#120 partial)
- [ ] Full UI skin — finished military map aesthetic
- [ ] Colour-blind accessible symbol and map palettes
- [ ] Keyboard shortcut remapping
- [ ] Controller support (console stretch goal) (#290)
- [ ] Comprehensive settings: graphics, audio, gameplay, accessibility (#289) —
  shell (#306); preference store + ConfirmOrders (#307); graphics resolution/fullscreen
  (#385); AUDIO master/music/SFX (#264); more controls / #311 still open
- [x] Explicit command palette / waypoints (#32, #53, #54) — verb table, arming, click-to-
  issue, keyboard shortcuts, and WAYPOINTS draft/commit (#127–#129, #54); config table (#130)
  still open
- [ ] Site motion capture / GIF for the landing page (#120 remainder)
- [ ] In-game / web field-manual reference (#124)

### Tutorial & Onboarding
- [ ] Interactive tutorial campaign: start at squad, walk through all systems (#289) —
  distinct from the shipped multi-echelon **climb campaign** (#403 / [climb-campaign.md](climb-campaign.md)).
  Squad scenario skeleton (#309); first select→MoveTo beat (#310); more beats open
- [ ] Context-sensitive in-game help overlay (#289) — MOVE help shipped (#308); more controls open
- [ ] Doctrinal reference wiki (in-game and web) (#124)

### Performance & Stability
- [ ] Unit stress tests: 10,000 simultaneous units at theater scale
- [ ] Memory profiling and GC pressure reduction
- [ ] Addressables for async asset loading (no load-screen hitches)
- [ ] Unity DOTS/ECS migration path for massive-scale scenarios (post-1.0 spike)
- [ ] Spatial index for `ContactTracker.Sweep` before hundreds of units (known-gaps)

### Platform Builds
- [x] Windows (primary) — local/`build.ps1` player builds
- [ ] macOS
- [ ] Linux (Steam Deck compatible)
- [ ] WebGL (stretch — limited echelon scale)

### Steam Early Access Exit
- [ ] All Phase 8 AI milestones met (RL agent functional at battalion+ scale)
- [ ] Phase 9 online services stable under load
- [ ] All EA community-reported critical bugs resolved
- [ ] Public EA roadmap updated to reflect 1.0 scope

### Steam Assets & Store
- [ ] Steam Achievements (20–30): echelon progression, mastery, game modes, community — see [docs/steam.md](steam.md)
- [ ] Steam Cloud (`SteamRemoteStorage`): sync saves, settings, and doctrine templates
- [ ] Steam store page complete: capsule art, 5+ screenshots, trailer, short/long descriptions, tags
- [ ] Steam Deck compatibility verified (target Playable; Verified post-1.0)

### Release Checklist
- [ ] Release engineering: versioning, notes, GitHub Releases, Pages and itch.io (#83)
  — first tagged Release + version stamp/chrome (#217–#219); CI licence (#216),
  WebGL (#220), Pages/itch (#221) still open.
- [ ] Legal review: NATO symbol usage, any trademarked unit names
- [ ] ESRB/PEGI rating (expected: T/12+)
- [ ] Steam page approved, trailer published, press kit sent
- [ ] 1.0 launch blog post with AI architecture writeup

**Milestone M10: 1.0 Release.**

---

## Post-1.0 Stretch Goals

- **Naval dimension** — sea-control operations, amphibious landings (APP-6D sea symbols)
- **Air dimension** — air superiority, strategic bombing, airborne operations
- **Space layer** — satellite assets (ISR, GPS denial, anti-satellite)
- **Cyber domain** — network attack/defence as a game mechanic
- **Multiplayer coalitions** — joint commands shared between multiple human players
- **MTTR scoring** — measure AI commander Mean Time to Recovery after setbacks
- **VR spectator mode** — stand inside the operations room
- **Modding SDK** — full SDK + documentation for community content

---

*Last updated: 2026-08-03*
